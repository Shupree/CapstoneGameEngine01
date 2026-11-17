using System.IO;
using System.Linq;
using GE.MiniGames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 제공된 UI 그림을 직렬화된 씬 참조로 연결합니다. 게임 규칙과 원본 PNG, README는 수정하지 않습니다.
public static class MiniGameArtworkInstaller
{
    const string Art="Assets/_Art/Sprites/2d_ui/ui_2d_";
    static Texture Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture>(Art+name+".png");
    [MenuItem("Tools/Mini Games/Connect result artwork")]
    public static void Install()
    {
        // 작은 픽셀 UI는 밉맵과 압축을 끄고 포인트 필터로 원본 선명도를 유지합니다.
        foreach(string name in new[]{"pass","fail","pause","fail_retry"}.Concat(Enumerable.Range(0,10).Select(i=>i.ToString())))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+name+".png");
            importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
            importer.SaveAndReimport();
        }
        for(int i=1;i<=18;i++) InstallMini(i);
        InstallMain(); AssetDatabase.SaveAssets();
        Debug.Log("MINIGAME_ARTWORK_INSTALLED");
    }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.gameObject.layer=5; r.anchorMin=r.anchorMax=Vector2.one*.5f; r.anchoredPosition=position; r.sizeDelta=size; return r;
    }
    static RawImage Image(string name,Transform parent,Texture texture,Vector2 position,float width)
    {
        var r=Rect(name,parent,position,new Vector2(width,texture==null?width:width*texture.height/texture.width));
        var image=r.gameObject.AddComponent<RawImage>(); image.texture=texture; image.raycastTarget=false; return image;
    }
    static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,string value,Vector2 position,Vector2 size,int fontSize)
    {
        var label=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font; label.text=value; label.fontSize=fontSize; label.color=Color.white; label.alignment=TextAlignmentOptions.Center;
        label.raycastTarget=false; return label;
    }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
    static Canvas NewCanvas(Transform parent,Camera camera,int sorting)
    {
        var root=Rect("Result artwork canvas",parent,Vector2.zero,Vector2.zero);
        var canvas=root.gameObject.AddComponent<Canvas>(); canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;
        canvas.worldCamera=camera; canvas.planeDistance=1; canvas.sortingOrder=sorting;
        var scale=root.gameObject.AddComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution=new Vector2(720,760); scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>(); return canvas;
    }
    static GameObject OverlayOf(MiniGameController game)
    {
        // 초기 네 게임은 전용 View, 이후 게임은 공통 UI에 오버레이 참조가 있습니다.
        foreach(var component in game.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if(component==null) continue;
            var property=new SerializedObject(component).FindProperty("overlay");
            if(property!=null && property.objectReferenceValue is GameObject overlay) return overlay;
        }
        throw new System.InvalidOperationException("Missing legacy overlay: "+game.GameId);
    }
    static void InstallMini(int number)
    {
        var scene=EditorSceneManager.OpenScene($"Assets/Scenes/MiniGame{number:00}.unity");
        var game=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MiniGameController>(true)).Single();
        // 기존 입력 시스템이 표시 패널 아래에 있어 함께 꺼지는 씬도 있으므로 독립 루트를 사용합니다.
        var inputs=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<EventSystem>(true)).ToArray();
        var input=inputs.FirstOrDefault(x=>x.name=="Minigame artwork input" && x.transform.parent==null);
        foreach(var other in inputs.Where(x=>x!=input))
        { other.enabled=false; foreach(var oldModule in other.GetComponents<BaseInputModule>()) oldModule.enabled=false; }
        if(input==null) input=new GameObject("Minigame artwork input",typeof(EventSystem)).GetComponent<EventSystem>();
        input.gameObject.SetActive(true); input.enabled=true;
        var module=input.GetComponent<InputSystemUIInputModule>();
        if(module==null) { module=input.gameObject.AddComponent<InputSystemUIInputModule>(); module.AssignDefaultActions(); }
        module.enabled=true;
        var old=OverlayOf(game); var group=old.GetComponent<CanvasGroup>();
        // Unity 오브젝트의 null 비교를 사용해야 에디터의 빈 컴포넌트 참조에도 실제 CanvasGroup이 추가됩니다.
        if(group==null) group=old.AddComponent<CanvasGroup>();
        var installed=game.GetComponent<MiniGameArtworkUI>();
        // 보관된 옛 Canvas의 폰트가 아니라 현재 결과 오버레이에서 실제 사용하는 폰트를 재사용합니다.
        var font=old.GetComponentInChildren<TMP_Text>(true).font;
        if(installed!=null)
        {
            var binding=new SerializedObject(installed); binding.FindProperty("legacy").objectReferenceValue=group;
            ((TMP_Text)binding.FindProperty("reason").objectReferenceValue).font=font;
            ((TMP_Text)binding.FindProperty("hint").objectReferenceValue).font=font;
            binding.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(scene); return;
        }
        // 초기 씬은 컨트롤러·카메라·Canvas가 서로 다른 루트에 있으므로 해당 씬 안에서만 찾습니다.
        var roots=scene.GetRootGameObjects();
        var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).First();
        var canvas=NewCanvas(game.transform,camera,500);
        var panel=Rect("Result artwork panel",canvas.transform,Vector2.zero,Vector2.zero); Stretch(panel);
        var background=panel.gameObject.AddComponent<Image>(); background.color=new Color(0,0,0,.93f);
        var art=Image("Result stamp",panel,Texture("pass"),new Vector2(0,95),280);
        var reason=Text("Result reason",panel,font,"",new Vector2(0,-45),new Vector2(640,100),22);
        var retryImage=Image("Retry click",panel,Texture("fail_retry"),new Vector2(0,-145),64); retryImage.raycastTarget=true;
        var retry=retryImage.gameObject.AddComponent<Button>(); retry.targetGraphic=retryImage;
        retry.navigation=new Navigation {mode=Navigation.Mode.None};
        var hint=Text("Result key",panel,font,"R / CLICK",new Vector2(0,-210),new Vector2(620,40),20);
        game.gameObject.AddComponent<MiniGameArtworkUI>().Configure(game,canvas,panel.gameObject,group,art,Texture("pass"),Texture("fail"),Texture("pause"),retry,reason,hint);
        panel.gameObject.SetActive(false);
        // Additive 진입 시 기존 매니저가 이 EventSystem을 끄고 메인 입력 시스템만 유지합니다.
        EditorSceneManager.SaveScene(scene);
    }
    static void InstallMain()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity");
        var manager=Object.FindFirstObjectByType<MiniGameManager>();
        if(manager.GetComponent<MainGameArtworkHUD>()!=null) return;
        var data=new SerializedObject(manager);
        var over=(GameObject)data.FindProperty("gameOverOverlay").objectReferenceValue;
        var retry=(Button)data.FindProperty("gameOverRetryButton").objectReferenceValue;
        var paused=(RectTransform)data.FindProperty("miniGamePauseOverlay").objectReferenceValue;
        var font=over.GetComponentInChildren<TMP_Text>(true).font;
        // 기존 패널과 재시작 버튼의 이벤트 연결은 보존하고 텍스트 표지만 그림으로 교체합니다.
        foreach(var label in over.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        foreach(var label in paused.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        Image("FAIL artwork",over.transform,Texture("fail"),new Vector2(0,100),280);
        var reason=Text("Game over reason",over.transform,font,"",Vector2.zero,new Vector2(600,60),24);
        var retryRect=retry.GetComponent<RectTransform>(); retryRect.anchorMin=retryRect.anchorMax=Vector2.one*.5f;
        retryRect.anchoredPosition=new Vector2(0,-110); retryRect.sizeDelta=new Vector2(88,88);
        var existing=retry.GetComponent<Image>(); if(existing!=null) existing.color=Color.clear;
        var retryArt=Image("Retry artwork",retry.transform,Texture("fail_retry"),Vector2.zero,70); retryArt.raycastTarget=true;
        Text("Retry key",over.transform,font,"R / CLICK",new Vector2(0,-190),new Vector2(500,50),24);
        Image("PAUSE artwork",paused,Texture("pause"),Vector2.zero,280);
        Text("Pause key",paused,font,"TAB",new Vector2(0,-90),new Vector2(500,50),24);
        var canvas=NewCanvas(manager.transform,null,100);
        var area=Rect("Minigame HUD viewport",canvas.transform,Vector2.zero,Vector2.zero); Stretch(area);
        var life=Rect("Lives",area,Vector2.zero,new Vector2(180,48)); life.anchorMin=life.anchorMax=new Vector2(0,1); life.pivot=new Vector2(0,1); life.anchoredPosition=new Vector2(14,-12);
        Text("Lives label",life,font,"LIFE",new Vector2(-35,0),new Vector2(80,40),24);
        var numbers=Enumerable.Range(0,10).Select(i=>Texture(i.ToString())).ToArray(); var digits=new RawImage[10];
        for(int i=0;i<digits.Length;i++) digits[i]=Image("Life digit "+i,life,numbers[0],new Vector2(20+i*25,0),20);
        manager.gameObject.AddComponent<MainGameArtworkHUD>().Configure(manager,area,life.gameObject,digits,numbers,reason);
        EditorSceneManager.SaveScene(scene);
    }
}
