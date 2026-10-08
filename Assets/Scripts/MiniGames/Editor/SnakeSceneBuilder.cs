using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Snake;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    public static class SnakeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame01.unity";
        public const string FontPath = "Assets/MiniGames/Art/SnakeUI SDF.asset";
        private const string RootName = "MiniGame01 - Snake";
        private static readonly Color Ink = new Color32(12, 27, 29, 255);
        private static readonly Color Paper = new Color32(232, 242, 218, 255);
        private static readonly Color Mint = new Color32(159, 222, 163, 255);

        [MenuItem("Tools/Mini Games/01 Snake/Open or Create Scene")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // Safe to call repeatedly: once built, retain authored Inspector settings and hierarchy.
        public static void Build()
        {
            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (existing != null)
            {
                var rules = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(text => text.name == "Goal");
                existing.GetComponent<SnakeView>().ConfigureRulesLabel(rules);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                ValidateScene();
                Debug.Log("Snake scene already configured; retained existing content.");
                return;
            }

            TMP_FontAsset font = CreateFont();
            GameObject[] roots = scene.GetRootGameObjects();
            Camera camera = roots.SelectMany(go => go.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (camera == null) camera = new GameObject("MiniGame Camera", typeof(Camera)).GetComponent<Camera>();
            camera.gameObject.SetActive(true);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.cullingMask = 1 << 5;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.depth = 1;
            camera.tag = "Untagged"; // Do not compete for Camera.main when loaded additively.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (var light in roots.SelectMany(go => go.GetComponentsInChildren<Light>(true))) light.enabled = false;

            Canvas canvas = roots.SelectMany(go => go.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
            if (canvas == null) canvas = new GameObject("UIsRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.gameObject.layer = 5;
            canvas.gameObject.SetActive(true);
            foreach (Transform child in canvas.transform) child.gameObject.SetActive(false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 1;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 760);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            foreach (var es in roots.SelectMany(go => go.GetComponentsInChildren<EventSystem>(true))) es.gameObject.SetActive(false);
            foreach (var root in roots.Where(go => go.name == "TestObject")) root.SetActive(false);

            var controllerRoot = new GameObject(RootName);
            SnakeGame game = controllerRoot.AddComponent<SnakeGame>();
            RectTransform screen = Rect("Snake Screen", canvas.transform, Vector2.zero, Vector2.zero);
            Stretch(screen);
            Image backdrop = screen.gameObject.AddComponent<Image>();
            backdrop.color = Ink; backdrop.raycastTarget = false;
            RectTransform content = Rect("Content 1000 x 760", screen, Vector2.zero, new Vector2(1000, 760));
            Text("Index", content, font, "MINI GAME  /  01", new Vector2(-215, 334), new Vector2(360, 32), 18, Mint, TextAlignmentOptions.Left);
            Text("Mode", content, font, "1 PLAYER", new Vector2(284, 334), new Vector2(220, 32), 18, Mint, TextAlignmentOptions.Right);
            Text("Title", content, font, "S N A K E", new Vector2(-140, 290), new Vector2(510, 52), 40, Paper, TextAlignmentOptions.Left);
            TMP_Text progress = Text("Progress", content, font, "사과  0 / 5", new Vector2(285, 290), new Vector2(220, 42), 25, Paper, TextAlignmentOptions.Right);
            Image track = Panel("Progress track", content, new Vector2(0, 247), new Vector2(790, 8), new Color32(41, 67, 59, 255));
            Image fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Mint);
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            Panel("Board border", content, new Vector2(0, -22), new Vector2(512, 512), new Color32(77, 114, 90, 255));
            Image board = Panel("Board", content, new Vector2(0, -22), new Vector2(504, 504), Ink);
            RectTransform cells = Rect("Cells", board.transform, Vector2.zero, new Vector2(500, 500));
            Text("Controls", content, font, "W A S D  방향 전환     |     Tab  일시정지", new Vector2(0, -310), new Vector2(840, 36), 22, Paper);
            TMP_Text rulesLabel = Text("Goal", content, font, "10 × 10    /    사과 5개를 모으세요    /    0.25초마다 이동", new Vector2(0, -347), new Vector2(920, 28), 18, Mint);
            SnakeView view = controllerRoot.AddComponent<SnakeView>();
            view.Configure(game, cells, progress, fill);
            view.ConfigureRulesLabel(rulesLabel);
            view.BuildGrid(10, 10);
            game.ResetGame();
            view.Refresh();

            Image overlay = Panel("State overlay", screen, Vector2.zero, Vector2.zero, new Color(0.025f, 0.065f, 0.07f, 0.87f));
            Stretch(overlay.rectTransform);
            Panel("Modal border", overlay.transform, Vector2.zero, new Vector2(612, 352), Mint);
            Image modal = Panel("Modal", overlay.transform, Vector2.zero, new Vector2(604, 344), Ink);
            TMP_Text heading = Text("State title", modal.transform, font, "스네이크", new Vector2(0, 113), new Vector2(560, 64), 43, Paper);
            TMP_Text body = Text("Instructions", modal.transform, font, "WASD  방향 전환\n사과 5개를 먹으면 성공!\n벽과 자신의 몸을 피하세요.", new Vector2(0, 10), new Vector2(558, 138), 25, Paper);
            TMP_Text hint = Text("Prompt", modal.transform, font, "Spacebar로 시작", new Vector2(0, -112), new Vector2(562, 44), 22, Mint);
            var testUI = controllerRoot.AddComponent<MiniGameTestUI>();
            testUI.Configure(game, overlay.gameObject, heading, body, hint, body.text);

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Snake scene.");
            AssetDatabase.SaveAssets();
            ValidateScene();
            Debug.Log("SNAKE_SCENE_BUILT " + ScenePath);
        }

        private static TMP_FontAsset CreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;
            Directory.CreateDirectory("Assets/MiniGames/Art");
            AssetDatabase.Refresh();
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            if (source == null) throw new InvalidOperationException("Project Korean font is missing.");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "SnakeUI SDF";
            string glyphs = new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray()) +
                "× 스네이크 방향 전환 사과 개를 먹으면 성공! 벽과 자신의 몸을 피하세요. Spacebar로 시작 " +
                "일시정지 Tab으로 재개 현재 위치와 진행도는 유지됩니다 다시 도전! 사과 목표를 달성했습니다! " +
                "벽에 부딪혔습니다. 몸에 부딪혔습니다. Spacebar를 놓고 다시 눌러 재시도 시작 대기 중 진행 대기 중 " +
                "사과 개를 모으세요 초마다 이동";
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing UI glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        [MenuItem("Tools/Mini Games/01 Snake/Validate Scene")]
        public static void ValidateScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open MiniGame01 before validation.");
            var roots = scene.GetRootGameObjects();
            if (roots.SelectMany(go => go.GetComponentsInChildren<SnakeGame>()).Count() != 1) throw new InvalidOperationException("Expected one active SnakeGame.");
            Canvas canvas = roots.SelectMany(go => go.GetComponentsInChildren<Canvas>()).Single();
            if (canvas.worldCamera == null || canvas.renderMode != RenderMode.ScreenSpaceCamera) throw new InvalidOperationException("Camera-space canvas not configured.");
            foreach (var transform in roots.SelectMany(go => go.GetComponentsInChildren<Transform>(true)))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Missing script on " + transform.name);
            Debug.Log("SNAKE_SCENE_VALIDATED: camera, canvas, controller and serialized scripts.");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value,
            Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize;
            text.color = color; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false; return text;
        }
    }
}
