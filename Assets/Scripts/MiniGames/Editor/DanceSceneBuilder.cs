using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Dance;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame08 최초 제작 전용입니다. 기존 씬·공용 에셋·메인 목록은 빌더에서 변경하지 않습니다.
    public static class DanceSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame08.unity";
        private const string ArtPath = "Assets/MiniGames/Dance";
        private static readonly Color Ink = new Color32(51, 65, 85, 255);
        private static readonly Color Paper = new Color32(248, 250, 252, 255);
        private static readonly Color Teal = new Color32(15, 118, 110, 255);

        [MenuItem("Tools/Mini Games/08 Dance/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame08 - Dance");
            var game = owner.AddComponent<DanceGame>();
            var font = CreateFont(game.StartInstructions); var disc = CreateDisc();
            var cameraRoot = new GameObject("Dance Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 8; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(241, 245, 249, 255); camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Dance Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(880, 680);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(880, 680));
            Label("Index", content, font, "MINI GAME / 08", new Vector2(-250, 303), new Vector2(300, 26), 15, Teal, TextAlignmentOptions.Left);
            Label("Title", content, font, "D A N C E", new Vector2(-200, 262), new Vector2(400, 48), 36, Ink, TextAlignmentOptions.Left);
            Label("Subtitle", content, font, "RHYTHM MATCH", new Vector2(260, 262), new Vector2(280, 34), 20, Teal, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 228), new Vector2(800, 4), new Color32(203, 213, 225, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Teal).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;

            // HTML의 800×450 비율과 바닥·캐릭터 배치를 유지합니다. 화면 배율은 공통 방식의 CanvasScaler가 처리합니다.
            var field = Panel("Playfield 800 x 450", content, new Vector2(0, -5), new Vector2(800, 450), new Color32(226, 232, 240, 255)).rectTransform;
            field.gameObject.AddComponent<RectMask2D>();
            Panel("Ground - replaceable", field, new Vector2(0, -165), new Vector2(800, 120), new Color32(203, 213, 225, 255));
            var round = Label("Round", field, font, "ROUND 1 / 10", new Vector2(-240, 190), new Vector2(260, 35), 22, Ink, TextAlignmentOptions.Left);
            var clock = Label("Timer", field, font, "4.8 s", new Vector2(300, 190), new Vector2(140, 35), 22, Ink, TextAlignmentOptions.Right);
            var timeTrack = Panel("Time track", field, new Vector2(0, 158), new Vector2(740, 8), new Color32(203, 213, 225, 255));
            var timeFill = Panel("Time fill", timeTrack.transform, Vector2.zero, Vector2.zero, Ink);
            timeFill.rectTransform.anchorMin = Vector2.zero; timeFill.rectTransform.anchorMax = Vector2.one;
            timeFill.rectTransform.offsetMin = timeFill.rectTransform.offsetMax = Vector2.zero;
            var cards = new Image[10]; var arrows = new RectTransform[10]; var letters = new TMP_Text[10];
            for (int i = 0; i < 10; i++)
            {
                cards[i] = Panel("Direction card " + (i + 1), field, new Vector2(0, 55), new Vector2(56, 64), Paper);
                cards[i].sprite = disc;
                arrows[i] = Rect("Arrow", cards[i].transform, new Vector2(0, 7), new Vector2(24, 24));
                Panel("Shaft", arrows[i], Vector2.zero, new Vector2(4, 24), Ink);
                Panel("Left tip", arrows[i], new Vector2(-5, 6), new Vector2(4, 16), Ink).rectTransform.localEulerAngles = new Vector3(0, 0, -45);
                Panel("Right tip", arrows[i], new Vector2(5, 6), new Vector2(4, 16), Ink).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
                letters[i] = Label("Key", cards[i].transform, font, "W", new Vector2(0, -19), new Vector2(30, 20), 12, Ink);
            }
            var status = Label("Input progress", field, font, "입력 0 / 4", new Vector2(0, -5), new Vector2(400, 32), 18, Teal);
            // 캐릭터는 원본과 같은 교체용 정적 도형입니다. 점프·흔들림 애니메이션 없이 성공/실패 색만 바꿉니다.
            var character = Panel("Character - replaceable sprite", field, new Vector2(0, -65), new Vector2(80, 80), new Color32(100, 116, 139, 255));
            character.sprite = disc;
            Panel("Eye left", character.transform, new Vector2(-15, 10), new Vector2(12, 12), Paper).sprite = disc;
            Panel("Eye right", character.transform, new Vector2(15, 10), new Vector2(12, 12), Paper).sprite = disc;
            Label("Mission", field, font, "표시된 방향을 왼쪽부터 차례대로", new Vector2(0, -165), new Vector2(740, 40), 20, Ink);
            Label("Controls", content, font, "W A S D  방향 입력     TAB  일시정지", new Vector2(0, -273), new Vector2(840, 35), 22, Ink);
            Label("Timing", content, font, "4 → 6 → 8 → 10개   /   입력당 1.2초", new Vector2(0, -310), new Vector2(840, 30), 18, Teal);

            // 상태와 Space 재시도는 기존 MiniGameTestUI가 담당하며 로컬 화면만 참조합니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(800, 450), new Color(.886f, .91f, .94f, .97f));
            var title = Label("State title", overlay.transform, font, "DANCE", new Vector2(0, 125), new Vector2(740, 60), 38, Ink);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(750, 150), 23, Ink);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -130), new Vector2(750, 48), 24, Teal);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(commonUI);
            fields.FindProperty("gameTitle").stringValue = "DANCE"; fields.FindProperty("progressLabel").stringValue = "완료 라운드";
            fields.ApplyModifiedPropertiesWithoutUndo();
            owner.AddComponent<DanceView>().Configure(game, cards, arrows, letters, round, clock, status, timeFill, character, fill);
            game.ResetGame(); owner.GetComponent<DanceView>().Refresh();
            Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("DANCE_SCENE_BUILT " + ScenePath);
        }

        // 교체용 원형 도형만 전용 에셋에 저장합니다. 캐릭터와 카드의 실제 게임 규칙은 스프라이트와 분리되어 있습니다.
        private static Sprite CreateDisc()
        {
            string path = ArtPath + "/Disc.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(32 - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)))));
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // 전용 정적 한글 폰트만 생성해 공용 폰트와 다른 미니게임 에셋에 영향을 주지 않습니다.
        private static TMP_FontAsset CreateFont(string instructions)
        {
            string path = ArtPath + "/DanceUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "10라운드를 모두 완료했습니다! 방향 순서가 틀렸습니다. 시간 초과! 완료 라운드 다음 준비 입력 표시된 방향을 왼쪽부터 차례대로 입력당 1.2초 →" +
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
