using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Bomb;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // 기존 미니게임처럼 최초 씬 제작만 담당하며, 이미 존재하는 씬과 공통 에셋은 덮어쓰지 않습니다.
    public static class BombSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame07.unity";
        private const string ArtPath = "Assets/MiniGames/Bomb";
        private static readonly Color Ink = new Color32(44, 62, 80, 255);
        private static readonly Color Paper = new Color32(236, 240, 241, 255);
        private static readonly Color Orange = new Color32(230, 126, 34, 255);

        [MenuItem("Tools/Mini Games/07 Bomb/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame07 - Bomb");
            var game = owner.AddComponent<BombGame>();
            var font = CreateFont(game.StartInstructions);
            var cameraRoot = new GameObject("Bomb Camera", typeof(Camera));
            cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 8; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Bomb Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720, 700);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(720, 700));
            Label("Index", content, font, "MINI GAME / 07", new Vector2(-170, 310), new Vector2(300, 26), 15, Orange, TextAlignmentOptions.Left);
            Label("Title", content, font, "B O M B", new Vector2(-180, 267), new Vector2(280, 50), 36, Paper, TextAlignmentOptions.Left);
            var counter = Label("Cut progress", content, font, "절단 0 / 4", new Vector2(215, 267), new Vector2(210, 40), 24, Paper, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 234), new Vector2(640, 4), new Color32(80, 98, 112, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Orange).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;

            // HTML의 400×300 논리 화면을 유지하고 UI 계층만 1.6배 확대합니다. Additive 뷰포트는 CanvasScaler가 맞춥니다.
            var field = Panel("Playfield 400 x 300", content, new Vector2(0, -20), new Vector2(400, 300), new Color32(189, 195, 199, 255)).rectTransform;
            field.localScale = Vector3.one * 1.6f; field.gameObject.AddComponent<RectMask2D>();
            Label("Sequence title", field, font, "TARGET SEQUENCE", new Vector2(0, 117), new Vector2(360, 22), 13, Ink);
            var markers = new Image[4]; var markerLetters = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -60 + i * 40;
                Panel("Target border " + i, field, new Vector2(x, 85), new Vector2(28, 28), Ink);
                markers[i] = Panel("Target " + i, field, new Vector2(x, 85), new Vector2(24, 24), Paper);
                markerLetters[i] = Label("Target letter " + i, markers[i].transform, font, "R", Vector2.zero, new Vector2(24, 24), 14, Ink);
                if (i < 3) Panel("Target connector " + i, field, new Vector2(x + 20, 85), new Vector2(8, 2), Ink);
            }
            Panel("Bomb housing - replaceable", field, new Vector2(0, -25), new Vector2(300, 150), new Color32(127, 140, 141, 255));
            Panel("Clock background", field, new Vector2(0, 20), new Vector2(140, 40), Ink);
            var clock = Label("Timer", field, font, "04:70", new Vector2(0, 20), new Vector2(130, 38), 25, new Color32(231, 76, 60, 255));
            var tops = new Image[4]; var middles = new Image[4]; var bottoms = new Image[4]; var wireLetters = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -100 + i * 70;
                Panel("Wire socket " + i, field, new Vector2(x, -50), new Vector2(14, 64), Ink);
                tops[i] = Panel("Wire top " + i, field, new Vector2(x, -30), new Vector2(10, 20), Paper);
                middles[i] = Panel("Wire middle " + i, field, new Vector2(x, -50), new Vector2(10, 20), Paper);
                bottoms[i] = Panel("Wire bottom " + i, field, new Vector2(x, -70), new Vector2(10, 20), Paper);
                wireLetters[i] = Label("Wire letter " + i, field, font, "R", new Vector2(x, -130), new Vector2(32, 22), 14, Ink);
            }
            // 선택 표시는 위·아래 고정 표식만 이동하며 절단 애니메이션은 사용하지 않습니다.
            var cursor = Rect("Selection cursor", field, new Vector2(-100, -50), new Vector2(30, 100));
            Label("Selection above", cursor, font, "v", new Vector2(0, 47), new Vector2(30, 24), 18, Paper);
            Label("Selection below", cursor, font, "^", new Vector2(0, -47), new Vector2(30, 24), 18, Paper);
            var tint = Panel("Failure tint", field, Vector2.zero, new Vector2(400, 300), new Color(0.91f, .3f, .24f, .4f));
            tint.enabled = false;
            Label("Controls", content, font, "A / D  선택    SPACE  절단    TAB  일시정지", new Vector2(0, -291), new Vector2(680, 30), 20, Paper);
            Label("Hint", content, font, "상단 색상 순서대로!  전선 배치와 순서는 매번 바뀝니다.", new Vector2(0, -325), new Vector2(680, 28), 17, Orange);

            // 시작·결과·재시도는 기존 공통 UI를 사용하고, 게임별 이름과 진행 표시만 지정합니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(400, 300), new Color(.17f, .24f, .31f, .97f));
            var title = Label("State title", overlay.transform, font, "폭탄 해체", new Vector2(0, 85), new Vector2(370, 45), 26, Paper);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(380, 112), 13, Paper);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -90), new Vector2(380, 38), 16, Orange);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(commonUI);
            fields.FindProperty("gameTitle").stringValue = "폭탄 해체"; fields.FindProperty("progressLabel").stringValue = "절단";
            fields.ApplyModifiedPropertiesWithoutUndo();
            owner.AddComponent<BombView>().Configure(game, markers, markerLetters, tops, middles, bottoms, wireLetters, cursor, clock, counter, fill, tint);
            game.ResetGame(); owner.GetComponent<BombView>().Refresh();
            Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("BOMB_SCENE_BUILT " + ScenePath);
        }
        // 전용 정적 한글 폰트만 생성해 공용 폰트와 다른 미니게임 에셋에 영향을 주지 않습니다.
        private static TMP_FontAsset CreateFont(string instructions)
        {
            string path = ArtPath + "/BombUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "폭탄 해체 성공! 순서가 틀렸습니다. 시간 초과! 절단 선택 상단 색상 순서대로 전선 배치와 순서는 매번 바뀝니다." +
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
