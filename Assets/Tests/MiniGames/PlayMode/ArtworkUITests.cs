#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 기존 매니저는 별도 어셈블리이므로 테스트에서만 리플렉션으로 실제 상태·이벤트 연결을 확인합니다.
    public sealed class ArtworkUITests
    {
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static T Property<T>(object owner,string name)=>(T)owner.GetType().GetProperty(name).GetValue(owner);
        static void Finish(MiniGameController game,MiniGameOutcome outcome)
        { typeof(MiniGameController).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{outcome,"UI TEST"}); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            foreach(string typeName in new[]{"MiniGameManager","MenuManager"})
            {
                var type=Type.GetType(typeName+", Assembly-CSharp");
                if(type!=null) foreach(var component in Object.FindObjectsByType(type,FindObjectsSortMode.None)) Object.Destroy(((Component)component).gameObject);
            }
            yield return null;
            var keep=SceneManager.CreateScene("Artwork test cleanup"); SceneManager.SetActiveScene(keep);
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            { var scene=SceneManager.GetSceneAt(i); if(scene!=keep) yield return SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTest]
        public IEnumerator EveryMiniGame_ShowsArtworkAndClickRetriesWithoutDuplicateResults()
        {
            for(int number=1;number<=18;number++)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/MiniGame{number:00}.unity",new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game=Object.FindFirstObjectByType<MiniGameController>(); var ui=game.GetComponent<MiniGameArtworkUI>();
                Assert.That(ui,Is.Not.Null); ui.Refresh(); Assert.That(Field<GameObject>(ui,"panel").activeSelf,Is.False);
                int count=0; game.Completed+=_=>count++;
                game.StartGame(); game.PauseGame(); ui.Refresh();
                Assert.That(Field<RawImage>(ui,"artwork").texture.name,Is.EqualTo("ui_2d_pause"));
                Assert.That(Field<CanvasGroup>(ui,"legacy").alpha,Is.Zero);
                game.ResumeGame(); Finish(game,MiniGameOutcome.Failure); ui.Refresh();
                Assert.That(Field<RawImage>(ui,"artwork").texture.name,Is.EqualTo("ui_2d_fail"));
                var retry=Field<Button>(ui,"retry"); Assert.That(retry.gameObject.activeSelf,Is.True);
                // 실제 화면 위치로 레이캐스트해 이미지 버튼이 클릭 대상으로 잡히는지도 확인합니다.
                yield return null;
                Canvas.ForceUpdateCanvases();
                var events=UnityEngine.EventSystems.EventSystem.current;
                Assert.That(events,Is.Not.Null,game.GameId+" input system");
                var point=RectTransformUtility.WorldToScreenPoint(Field<Canvas>(ui,"canvas").worldCamera,retry.transform.position);
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=point},hits);
                Assert.That(hits.Any(hit=>hit.gameObject.GetComponentInParent<Button>()==retry),Is.True,
                    game.GameId+" point="+point+" canvas="+Field<Canvas>(ui,"canvas").enabled+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name))+
                    " groups="+string.Join(",",retry.GetComponentsInParent<CanvasGroup>().Select(g=>g.name+":"+g.blocksRaycasts)));
                if(number==3) Capture(game,"FAIL");
                retry.onClick.Invoke(); ui.Refresh(); Assert.That(game.RunId,Is.EqualTo(2)); Assert.That(game.State,Is.EqualTo(MiniGameState.Playing));
                Assert.That(Field<CanvasGroup>(ui,"legacy").alpha,Is.EqualTo(1));
                Finish(game,MiniGameOutcome.Success); ui.Refresh();
                Assert.That(Field<RawImage>(ui,"artwork").texture.name,Is.EqualTo("ui_2d_pass"));
                Assert.That(count,Is.EqualTo(2));
                if(number==10) Capture(game,"PASS");
                game.StopGame(); ui.Refresh(); Assert.That(Field<GameObject>(ui,"panel").activeSelf,Is.False);
            }
        }
        private IEnumerator LoadMain(Action<MonoBehaviour> receive)
        {
            yield return SceneManager.LoadSceneAsync("MainGame",LoadSceneMode.Single);
            var type=Type.GetType("MiniGameManager, Assembly-CSharp",true);
            var manager=(MonoBehaviour)Object.FindFirstObjectByType(type);
            float deadline=Time.realtimeSinceStartup+15;
            while((!Property<bool>(manager,"IsMiniGameActive") || Property<bool>(manager,"IsTransitioning")) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(Property<bool>(manager,"IsTransitioning"),Is.False); receive(manager);
        }
        [UnityTest]
        public IEnumerator MainGame_LivesFollowFailures_GameOverUsesExistingRestartButton()
        {
            MonoBehaviour manager=null; yield return LoadMain(m=>manager=m);
            var hudType=Type.GetType("MainGameArtworkHUD, Assembly-CSharp",true); var hud=manager.GetComponent(hudType);
            Assert.That(hud,Is.Not.Null);
            var game=Field<MiniGameController>(manager,"currentMiniGameController");
            for(int lives=2;lives>=0;lives--)
            {
                game.StartGame(); Finish(game,MiniGameOutcome.Failure); yield return null;
                Assert.That(Property<int>(manager,"CurrentLives"),Is.EqualTo(lives));
                Assert.That(Field<RawImage[]>(hud,"digits")[0].texture.name,Is.EqualTo("ui_2d_"+lives));
            }
            Assert.That(Property<bool>(manager,"IsGameOver"),Is.True);
            Assert.That(Field<GameObject>(manager,"gameOverOverlay").activeSelf,Is.True);
            Assert.That(Field<TMP_Text>(hud,"gameOverReason").text,Is.EqualTo("NO LIVES LEFT"));
            var button=Field<Button>(manager,"gameOverRetryButton"); Assert.That(button.interactable,Is.True);
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2);
            var replacement=(MonoBehaviour)Object.FindFirstObjectByType(manager.GetType());
            Assert.That(replacement,Is.Not.Null); Assert.That(Property<int>(replacement,"CurrentLives"),Is.EqualTo(3));
            Assert.That(Property<bool>(replacement,"IsGameOver"),Is.False);
        }
        [UnityTest]
        public IEnumerator MainGame_PassIsVisibleBeforeSingleSceneTransition()
        {
            MonoBehaviour manager=null; yield return LoadMain(m=>manager=m);
            var game=Field<MiniGameController>(manager,"currentMiniGameController"); int cleared=Property<int>(manager,"CurrentClearedCount");
            game.StartGame(); Finish(game,MiniGameOutcome.Success); game.GetComponent<MiniGameArtworkUI>().Refresh();
            Assert.That(game.KeyboardInputEnabled,Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Field<MiniGameController>(manager,"currentMiniGameController"),Is.SameAs(game));
            Assert.That(Property<int>(manager,"CurrentClearedCount"),Is.EqualTo(cleared));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(Property<int>(manager,"CurrentClearedCount"),Is.EqualTo(cleared+1));
        }
        // 실제 UI 카메라에서 이미지 비율과 안내 문구를 확인할 수 있게 결과 화면을 저장합니다.
        private static void Capture(MiniGameController game,string name)
        {
            var camera=Field<Canvas>(game.GetComponent<MiniGameArtworkUI>(),"canvas").worldCamera; var target=new RenderTexture(900,1000,24); var pixels=new Texture2D(900,1000,TextureFormat.RGB24,false);
            var old=camera.targetTexture; var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target; Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,900,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Artwork-"+name+".png",pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=old; RenderTexture.active=active; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
