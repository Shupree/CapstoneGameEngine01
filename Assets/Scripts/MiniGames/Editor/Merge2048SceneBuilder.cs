using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Merge2048;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame09 최초 제작만 담당합니다. 기존 씬과 다른 미니게임의 에셋, 메인 목록은 이 빌더에서 수정하지 않습니다.
    public static class Merge2048SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame09.unity";
        private const string ArtPath = "Assets/MiniGames/Merge2048";
        private static readonly Color Ink = new Color32(119, 110, 101, 255);
        private static readonly Color Paper = new Color32(250, 248, 239, 255);
        private static readonly Color Gold = new Color32(173, 126, 36, 255);

        [MenuItem("Tools/Mini Games/09 2048/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame09 - 2048");
            var game = owner.AddComponent<Merge2048Game>(); var font = CreateFont(game.StartInstructions);
            var cameraRoot = new GameObject("2048 Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 8; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Paper; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("2048 Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(680, 780);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(680, 780));
            Label("Index", content, font, "MINI GAME / 09", new Vector2(-90, 350), new Vector2(300, 26), 15, Gold, TextAlignmentOptions.Left);
            // 숫자 전용 제목도 한글 폰트의 행 높이를 확보해 작은 뷰포트에서 넘침 판정이 생기지 않게 합니다.
            Label("Title", content, font, "2048", new Vector2(-130, 299), new Vector2(220, 80), 52, Ink, TextAlignmentOptions.Left);
            var maximum = Label("Highest tile", content, font, "최대 2 / 128", new Vector2(120, 313), new Vector2(240, 32), 24, Ink, TextAlignmentOptions.Right);
            var moves = Label("Move count", content, font, "이동 0회", new Vector2(120, 278), new Vector2(240, 28), 18, Ink, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 252), new Vector2(480, 4), new Color32(220, 211, 197, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Gold).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;

            // 정사각형 보드와 좌상단부터 행 우선인 모델 인덱스를 맞춥니다. 화면 배율은 CanvasScaler만 조절합니다.
            var field = Panel("Playfield 480 x 480", content, new Vector2(0, -10), new Vector2(480, 480), new Color32(187, 173, 160, 255)).rectTransform;
            field.gameObject.AddComponent<RectMask2D>();
            var tiles = new Image[16]; var numbers = new TMP_Text[16];
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
            {
                int i = row * 4 + column;
                tiles[i] = Panel("Tile " + i + " - replaceable sprite", field, new Vector2(-175.5f + column * 117, 175.5f - row * 117), new Vector2(105, 105), Paper);
                numbers[i] = Label("Value " + i, tiles[i].transform, font, "", Vector2.zero, new Vector2(101, 101), 44, Ink);
            }
            Label("Controls", content, font, "W A S D  이동     TAB  일시정지", new Vector2(0, -292), new Vector2(640, 36), 23, Ink);
            Label("Mission", content, font, "같은 숫자를 합쳐 128 만들기", new Vector2(0, -331), new Vector2(640, 30), 21, Gold);

            // 시작·결과·Space 재시도는 기존 공통 UI를 그대로 사용합니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(480, 480), new Color(.98f, .973f, .937f, .97f));
            var title = Label("State title", overlay.transform, font, "128 만들기", new Vector2(0, 130), new Vector2(450, 60), 34, Ink);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(456, 160), 19, Ink);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -135), new Vector2(450, 48), 23, Gold);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(commonUI);
            fields.FindProperty("gameTitle").stringValue = "128 만들기"; fields.FindProperty("progressLabel").stringValue = "최대 타일";
            fields.ApplyModifiedPropertiesWithoutUndo();
            owner.AddComponent<Merge2048View>().Configure(game, tiles, numbers, maximum, moves, fill);
            game.ResetGame(); owner.GetComponent<Merge2048View>().Refresh();
            Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("MERGE2048_SCENE_BUILT " + ScenePath);
        }
        // 전용 정적 한글 폰트만 생성해 공용 폰트와 다른 미니게임 에셋에 영향을 주지 않습니다.
        private static TMP_FontAsset CreateFont(string instructions)
        {
            string path = ArtPath + "/Merge2048UI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "최대 타일 이동 회 같은 숫자를 합쳐 128 만들기 128 타일을 만들었습니다! 더 이상 움직일 수 없습니다." +
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
