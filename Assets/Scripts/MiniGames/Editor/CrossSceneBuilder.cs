using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Cross;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame05의 최초 제작만 담당합니다. 기존 씬, 공용 폰트, 빌드 목록, 메인 게임 목록은 변경하지 않습니다.
    public static class CrossSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame05.unity";
        private const string ArtPath = "Assets/MiniGames/Cross";
        private static readonly Color Ink = new Color32(51, 65, 85, 255);
        private static readonly Color Paper = new Color32(248, 250, 252, 255);
        private static readonly Color Road = new Color32(148, 163, 184, 255);
        private static readonly Color Mint = new Color32(204, 251, 241, 255);
        private static readonly Color Teal = new Color32(13, 148, 136, 255);

        [MenuItem("Tools/Mini Games/05 Cross the Street/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            // 재실행은 열기만 합니다. 이후 팀원이 Inspector에서 편집한 씬을 덮어쓰지 않습니다.
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame05 - Cross the Street");
            var game = owner.AddComponent<CrossGame>();
            var font = CreateFont(game.StartInstructions);
            var rounded = CreateRoundedSprite();

            // 기존 미니게임처럼 독립 카메라와 ScreenSpaceCamera 캔버스를 사용하여 Additive 뷰포트에 대응합니다.
            var cameraRoot = new GameObject("Cross Camera", typeof(Camera));
            cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 8;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Paper;
            camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Cross Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(600, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(600, 800));
            Label("Index", content, font, "MINI GAME / 05", new Vector2(-130, 364), new Vector2(240, 24), 15, Road, TextAlignmentOptions.Left);
            Label("Title", content, font, "CROSS THE STREET", new Vector2(-60, 327), new Vector2(380, 40), 28, Ink, TextAlignmentOptions.Left);
            var counter = Label("Crossed lanes", content, font, "00 / 10", new Vector2(192, 327), new Vector2(116, 40), 24, Teal, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 297), new Vector2(500, 4), new Color32(226, 232, 240, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Teal).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;

            // 원본의 500×600 보드와 실제 크기를 유지하고 CanvasScaler에서만 비율을 조정합니다.
            var field = Panel("Playfield 500 x 600", content, new Vector2(0, -15), new Vector2(500, 600), Road).rectTransform;
            field.gameObject.AddComponent<RectMask2D>();
            Panel("Goal safe zone - row 0", field, new Vector2(0, 275), new Vector2(500, 50), Mint);
            Panel("Start safe zone - row 11", field, new Vector2(0, -275), new Vector2(500, 50), new Color32(226, 232, 240, 255));
            for (int row = 2; row <= CrossModel.RoadRows; row++)
            {
                var divider = Rect("Lane divider " + row, field, new Vector2(0, 300 - row * 50), new Vector2(500, 2));
                for (int x = 0; x < 500; x += 30)
                    Panel("Dash " + x, divider, new Vector2(x - 250 + 7.5f, 0), new Vector2(15, 2), new Color32(203, 213, 225, 255));
            }
            Label("Goal label", field, font, "SAFE ZONE", new Vector2(0, 275), new Vector2(480, 30), 16, Teal);
            var cars = new Image[CrossModel.RoadRows * 2];
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i] = Panel("Car " + (i + 1) + " - replaceable sprite", field, Vector2.zero, new Vector2(75, 36), Paper);
                cars[i].sprite = rounded; cars[i].type = Image.Type.Sliced;
            }
            var player = Panel("Player - replaceable sprite", field, Vector2.zero, new Vector2(36, 36), Ink);
            player.sprite = rounded; player.type = Image.Type.Sliced;
            Label("Controls", content, font, "W / SPACE  전진     A S D  이동", new Vector2(0, -347), new Vector2(550, 30), 20, Ink);
            Label("Goal", content, font, "10개 차선 건너기   /   TAB  일시정지", new Vector2(0, -378), new Vector2(550, 26), 17, Teal);

            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(500, 600), new Color(.886f, .91f, .94f, .96f));
            var title = Label("State title", overlay.transform, font, "길 건너기", new Vector2(0, 130), new Vector2(460, 70), 36, Ink);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 5), new Vector2(470, 150), 20, Ink);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -140), new Vector2(470, 50), 21, Teal);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(commonUI);
            fields.FindProperty("gameTitle").stringValue = "길 건너기";
            fields.FindProperty("progressLabel").stringValue = "통과 차선";
            fields.ApplyModifiedPropertiesWithoutUndo();
            var view = owner.AddComponent<CrossView>();
            view.Configure(game, player, cars, counter, fill);
            game.ResetGame(); view.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("CROSS_SCENE_BUILT " + ScenePath);
        }

        // 한글은 전용 정적 폰트에 미리 구워 런타임 또는 임포트가 다른 게임의 동적 폰트를 변경하지 않게 합니다.
        private static TMP_FontAsset CreateFont(string instructions)
        {
            string path = ArtPath + "/CrossUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "길 건너기 전진 이동 개 차선 통과 성공! 모든 차선을 건너 안전지대에 도착했습니다! 차량에 부딪혔습니다. 다시 도전하세요." +
                "로 시작 대기 중 일시정지 으로 재개 현재 위치와 진행도는 유지됩니다 다시 도전! 를 놓고 다시 눌러 재시도 진행 대기 중";
            chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        // 프로토타입의 둥근 단색 도형을 전용 9-slice 이미지로 저장합니다. 추후 씬 Inspector에서 아트로 교체할 수 있습니다.
        private static Sprite CreateRoundedSprite()
        {
            string path = ArtPath + "/Rounded.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Max(8 - (x + .5f), (x + .5f) - 24);
                    float dy = Mathf.Max(8 - (y + .5f), (y + .5f) - 24);
                    float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                    pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01(8.5f - distance));
                }
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(8, 8, 8, 8); importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 모든 UI는 같은 중앙 기준 좌표와 UI 레이어를 사용하며 입력은 공통 키보드 경로만 사용합니다.
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = alignment; label.raycastTarget = false; return label;
        }
    }
}
