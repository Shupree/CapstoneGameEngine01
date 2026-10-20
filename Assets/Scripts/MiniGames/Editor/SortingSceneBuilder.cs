using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Sorting;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame03 및 전용 에셋만 생성합니다. 기존 씬·공통 코드·메인 목록·빌드 설정은 변경하지 않습니다.
    public static class SortingSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame03.unity";
        private const string ArtPath = "Assets/MiniGames/Sorting";
        private static readonly Color Ink = new Color32(20, 44, 51, 255);
        private static readonly Color Paper = new Color32(241, 245, 224, 255);
        private static readonly Color Mint = new Color32(166, 222, 169, 255);

        [MenuItem("Tools/Mini Games/03 Sorting/Open or Create Scene")]
        public static void OpenOrCreate()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        public static void Build()
        {
            // 이미 생성된 씬은 검증만 합니다. 팀원이 Inspector로 변경한 배치와 수치를 재생성하지 않습니다.
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Validate();
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var font = CreateFont();
            var sprites = Enumerable.Range(0, 5).Select(CreateIcon).ToArray();
            var owner = new GameObject("MiniGame03 - Sorting");
            var game = owner.AddComponent<SortingGame>();
            var cameraObject = new GameObject("Sorting Camera", typeof(Camera));
            cameraObject.transform.SetParent(owner.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            camera.cullingMask = 1 << 5; camera.depth = 1; camera.tag = "Untagged";
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Sorting Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(1080, 800));
            Label("Index", content, font, "MINI GAME  /  03", new Vector2(-280, 355), new Vector2(400, 28), 18, Mint, TextAlignmentOptions.Left);
            Label("Title", content, font, "좌우 분류", new Vector2(-230, 306), new Vector2(500, 60), 42, Paper, TextAlignmentOptions.Left);
            Label("Subtitle", content, font, "SORT & CLEAR", new Vector2(280, 310), new Vector2(400, 50), 24, Mint, TextAlignmentOptions.Right);
            var count = Label("Correct count", content, font, "정답  00 / 30", new Vector2(-250, 245), new Vector2(460, 45), 27, Paper, TextAlignmentOptions.Left);
            var remaining = Label("Remaining count", content, font, "남은 수  30", new Vector2(250, 245), new Vector2(460, 45), 27, Mint, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 210), new Vector2(960, 6), new Color32(50, 74, 76, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Mint).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            Panel("Playfield", content, new Vector2(0, 0), new Vector2(960, 380), new Color32(34, 62, 67, 255));
            // 뒤의 두 카드는 남은 묶음을 표시하는 장식입니다. 정답 입력은 맨 앞 모델 하나에만 적용됩니다.
            var back2 = Panel("Stack back 2", content, new Vector2(24, 38), new Vector2(250, 240), new Color32(61, 91, 93, 255));
            var back1 = Panel("Stack back 1", content, new Vector2(12, 24), new Vector2(250, 240), new Color32(104, 133, 126, 255));
            var card = Panel("Current card", content, new Vector2(0, 10), new Vector2(250, 240), Paper);
            var item = Icon("Current object", card.transform, sprites[0], new Vector2(0, 16), new Vector2(128, 128));
            var name = Label("Object name", card.transform, font, "상자", new Vector2(0, -83), new Vector2(220, 40), 28, Ink);
            Label("Stack hint", content, font, "맨 앞의 오브젝트를 분류하세요", new Vector2(0, -148), new Vector2(800, 32), 20, Mint);
            var legend = Panel("Classification legend", content, new Vector2(0, -275), new Vector2(960, 145), new Color32(34, 62, 67, 255));
            Icon("Left box", legend.transform, sprites[0], new Vector2(-325, 28), new Vector2(56, 56));
            Label("Left arrow", legend.transform, font, "←  A  상자", new Vector2(-325, -40), new Vector2(270, 40), 27, Paper);
            Icon("Right bottle", legend.transform, sprites[1], new Vector2(325, 28), new Vector2(56, 56));
            Label("Right arrow", legend.transform, font, "병  D  →", new Vector2(325, -40), new Vector2(270, 40), 27, Paper);
            for (int i = 0; i < 3; i++) Icon("Dummy " + i, legend.transform, sprites[i + 2], new Vector2((i - 1) * 65, 28), new Vector2(48, 48));
            Label("Discard key", legend.transform, font, "SPACE  더미", new Vector2(0, -40), new Vector2(300, 40), 27, Mint);
            Label("Controls", content, font, "한 번 누를 때 하나씩  /  TAB  일시정지", new Vector2(0, -378), new Vector2(960, 32), 20, Paper);
            // 상단 진행 숫자 아래부터 덮어 시작·정지·결과에서도 정답 수와 남은 수가 가려지지 않게 합니다.
            var panel = Panel("Local state overlay", content, new Vector2(0, -75), new Vector2(960, 550), Ink);
            var title = Label("State title", panel.transform, font, "좌우 분류", new Vector2(0, 155), new Vector2(850, 70), 42, Paper);
            var body = Label("State description", panel.transform, font, game.StartInstructions, new Vector2(0, 10), new Vector2(870, 195), 25, Paper);
            var prompt = Label("State prompt", panel.transform, font, "SPACE  시작", new Vector2(0, -170), new Vector2(850, 60), 24, Mint);
            var view = owner.AddComponent<SortingView>();
            view.Configure(game, item, sprites, new[] { back1.gameObject, back2.gameObject }, count, remaining, name, fill, panel.gameObject, title, body, prompt);
            game.ResetGame(); view.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Validate();
        }

        [MenuItem("Tools/Mini Games/03 Sorting/Validate Scene")]
        public static void Validate()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("MiniGame03 씬을 먼저 여세요.");
            var root = scene.GetRootGameObjects().Single(x => x.GetComponent<SortingGame>() != null);
            if (root.GetComponent<SortingView>() == null || root.GetComponentsInChildren<Image>(true).Length < 12)
                throw new InvalidOperationException("MiniGame03 UI 참조가 누락되었습니다.");
            var canvas = root.GetComponentInChildren<Canvas>();
            if (canvas.worldCamera == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
                throw new InvalidOperationException("카메라 Canvas 연결이 누락되었습니다.");
            Debug.Log("SORTING_SCENE_VALIDATED");
        }

        // 기존 폰트 에셋의 아틀라스를 변경하지 않고, 프로젝트 원본 글꼴에서 전용 정적 폰트를 생성합니다.
        private static TMP_FontAsset CreateFont()
        {
            string path = ArtPath + "/SortingUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = string.Concat(Directory.GetFiles("Assets/Scripts/MiniGames/Sorting", "*.cs").Select(File.ReadAllText))
                + File.ReadAllText("Assets/Scripts/MiniGames/Editor/SortingSceneBuilder.cs")
                + new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray());
            chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        // 외부 이미지 없이 32px 도형으로 임시 분류 대상을 만듭니다. 색뿐 아니라 실루엣으로도 구분됩니다.
        private static Sprite CreateIcon(int index)
        {
            string path = ArtPath + "/" + ((SortingItem)index) + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color32[32 * 32];
                Color32[] colors = { new Color32(223, 146, 65, 255), new Color32(78, 160, 202, 255),
                    new Color32(120, 132, 143, 255), new Color32(98, 168, 94, 255), new Color32(225, 182, 58, 255) };
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    bool filled = index == 0 ? x >= 5 && x <= 26 && y >= 5 && y <= 26
                        : index == 1 ? (y >= 4 && y <= 19 && x >= 9 && x <= 22) || (y > 19 && y <= 28 && x >= 12 && x <= 19)
                        : index == 2 ? y >= 6 && y <= 23 && x >= 4 + Math.Abs(y - 14) / 3 && x <= 27 - Math.Abs(y - 14) / 2
                        : index == 3 ? Math.Abs(x - y) <= 6 && x + y >= 12 && x + y <= 48
                        : (y >= 16 && y <= 28 && x >= 16 - (28 - y) / 2 && x <= 23 - (28 - y) / 2)
                            || (y >= 4 && y < 19 && x >= 8 + (19 - y) / 2 && x <= 18 + (19 - y) / 3);
                    if (!filled) continue;
                    bool detail = index == 0 ? x >= 14 && x <= 17 || y == 19
                        : index == 1 ? y >= 10 && y <= 15 : index == 3 ? x == y : y % 8 == 0;
                    pixels[y * 32 + x] = detail ? (Color32)Paper : colors[index];
                }
                texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // MiniGame02와 같은 1080×800 기준 Canvas를 사용해 메인의 축소 viewport에서도 비율을 유지합니다.
        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private static Image Icon(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var image = Panel(name, parent, pos, size, Color.white);
            image.sprite = sprite; image.preserveAspect = true; return image;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 pos,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = alignment; label.raycastTarget = false; return label;
        }
    }
}
