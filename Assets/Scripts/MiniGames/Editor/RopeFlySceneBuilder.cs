using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Rope;
using GE.MiniGames.Fly;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // 예약된 10·17 씬만 구현합니다. 완성된 씬은 열기만 하며 메인·공통 코드와 다른 예약 씬은 건드리지 않습니다.
    public static class RopeFlySceneBuilder
    {
        private static readonly Color Ink = new Color32(44, 62, 80, 255);
        private static readonly Color Paper = new Color32(189, 195, 199, 255);
        [MenuItem("Tools/Mini Games/10 Rope/Open or Create Scene")]
        public static void OpenRope() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildOne(true); }
        [MenuItem("Tools/Mini Games/17 Fly/Open or Create Scene")]
        public static void OpenFly() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildOne(false); }
        public static void Build() { BuildOne(true); BuildOne(false); }
        private static void BuildOne(bool rope)
        {
            string number = rope ? "10" : "17", kind = rope ? "Rope" : "Fly";
            string path = "Assets/Scenes/MiniGame" + number + ".unity";
            if (File.Exists(path))
            {
                var existing = EditorSceneManager.OpenScene(path);
                var roots = existing.GetRootGameObjects();
                // 이름뿐 아니라 빈 Transform 하나만 있는지 확인해 협업자의 씬 편집을 덮어쓰지 않습니다.
                if (roots.Length != 1 || !roots[0].name.EndsWith("(구현 예정)") ||
                    roots[0].transform.childCount != 0 || roots[0].GetComponents<Component>().Length != 1) return;
            }
            string artPath = "Assets/MiniGames/" + kind;
            Directory.CreateDirectory(artPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame" + number + " - " + kind);
            MiniGameController game = rope ? (MiniGameController)owner.AddComponent<RopeGame>() : owner.AddComponent<FlyGame>();
            var font = CreateFont(game.StartInstructions, artPath);
            var cameraRoot = new GameObject(kind + " Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect(kind + " Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720, 760);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(720, 760));
            string titleText = rope ? "중심잡기" : "파리잡기";
            Label("Index", content, font, "MINI GAME / " + number, new Vector2(0, 335), new Vector2(650, 30), 17, Color.white);
            Label("Title", content, font, titleText, new Vector2(0, 284), new Vector2(650, 60), 32, Color.white);
            var border = Panel("Warning frame", content, Vector2.zero, new Vector2(652, 492), Ink);
            var field = Panel("Playfield 400 x 300", border.transform, Vector2.zero, new Vector2(400, 300), Paper).rectTransform;
            field.localScale = Vector3.one * 1.6f;
            field.gameObject.AddComponent<RectMask2D>();
            var label = Label("Status", field, font, "", new Vector2(0, 128), new Vector2(380, 32), 16, Ink);
            var disc = CreateDisc(artPath);
            if (rope)
            {
                Panel("Rope", field, new Vector2(0, -100), new Vector2(400, 4), new Color32(127, 140, 141, 255));
                Panel("Support", field, new Vector2(0, -55), new Vector2(30, 30), Ink);
                var pivot = Rect("Board pivot", field, new Vector2(0, -70), Vector2.zero);
                Panel("Board - replaceable sprite", pivot, new Vector2(0, 15), new Vector2(200, 10), new Color32(149, 165, 166, 255));
                var ball = Panel("Ball - replaceable sprite", pivot, new Vector2(0, 32), new Vector2(24, 24), new Color32(231, 76, 60, 255));
                ball.sprite = disc;
                owner.AddComponent<RopeView>().Configure((RopeGame)game, pivot, ball.rectTransform, label, border);
            }
            else
            {
                var fly = Panel("Fly - replaceable sprite", field, Vector2.zero, new Vector2(8, 8), Ink); fly.sprite = disc;
                var swatter = Panel("Swatter - replaceable sprite", field, Vector2.zero, new Vector2(40, 40), new Color(1, 1, 1, .3f));
                // 단순 사각 파리채는 교체 가능한 Image로 구성하고 타격 애니메이션은 생략합니다.
                for (int i = 0; i < 4; i++) Panel("Edge " + i, swatter.transform,
                    i < 2 ? new Vector2(i == 0 ? -18.5f : 18.5f, 0) : new Vector2(0, i == 2 ? -18.5f : 18.5f),
                    i < 2 ? new Vector2(3, 40) : new Vector2(40, 3), Ink);
                Panel("Handle", swatter.transform, new Vector2(0, -35), new Vector2(6, 30), Ink);
                owner.AddComponent<FlyView>().Configure((FlyGame)game, fly.rectTransform, swatter.rectTransform, label);
            }
            Label("Controls", content, font, rope ? "A / D  균형잡기     TAB  일시정지" : "W A S D  이동     SPACE  휘두르기", new Vector2(0, -275), new Vector2(680, 40), 22, Color.white);
            Label("Hint", content, font, rope ? "관성이 큽니다. 반대 키로 중심을 잡으세요." : "정지한 파리에 접근해 휘두르세요.", new Vector2(0, -313), new Vector2(680, 30), 19, new Color32(230, 126, 34, 255));
            // 공통 UI가 R 시작·재시도, Tab 정지, 결과 표시를 담당합니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(400, 300), new Color(.17f, .24f, .31f, .97f));
            var title = Label("State title", overlay.transform, font, titleText, new Vector2(0, 103), new Vector2(380, 50), 26, Color.white);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(380, 140), 16, Color.white);
            var prompt = Label("State prompt", overlay.transform, font, "R로 시작", new Vector2(0, -108), new Vector2(380, 38), 20, new Color32(230, 126, 34, 255));
            var ui = owner.AddComponent<MiniGameTestUI>(); ui.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(ui); fields.FindProperty("gameTitle").stringValue = titleText;
            fields.FindProperty("progressLabel").stringValue = rope ? "버틴 시간" : "잡은 파리"; fields.ApplyModifiedPropertiesWithoutUndo();
            game.ResetGame();
            if (rope) owner.GetComponent<RopeView>().Refresh(); else owner.GetComponent<FlyView>().Refresh();
            EditorSceneManager.SaveScene(scene, path); Debug.Log("ROPE_FLY_SCENE_BUILT " + path);
        }
        // 원본 HTML의 원형 도형을 정적 스프라이트로 보관합니다. 공유 에셋에는 쓰지 않습니다.
        private static Sprite CreateDisc(string artPath)
        {
            string path = artPath + "/Disc.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)))));
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // 전용 정적 한글 폰트만 생성해 공용 폰트와 다른 미니게임 에셋에 영향을 주지 않습니다.
        private static TMP_FontAsset CreateFont(string instructions, string artPath)
        {
            string path = artPath + "/UI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "중심잡기 외줄타기 파리잡기 남은 시간 초 잡은 파리 버틴 시간 관성이 큽니다 반대 키로 중심을 잡으세요 정지한 파리에 접근해 휘두르세요 5초 동안 균형을 지켰습니다! 판이 너무 기울었습니다. 공이 떨어졌습니다. 파리 5마리를 모두 잡았습니다!" +
                "로 시작 대기 중 일시정지 으로 재개 현재 위치와 진행도는 유지됩니다 다시 도전! 를 놓고 다시 눌러 재시도 진행 대기 중 성공!";
            chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path); foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }

        // 모든 표시 요소는 중앙 기준 UI 좌표이며, 팀원이 각 Image의 스프라이트를 교체할 수 있습니다.
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        { var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 pos, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
            label.text = text; label.fontSize = fontSize; label.color = color; label.alignment = alignment; label.raycastTarget = false; return label;
        }
    }
}
