using System;
using System.IO;
using System.Linq;
using GE.MiniGames.FlappyBird;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame02만 구성합니다. 전체 게임 목록·빌드 설정·다른 씬에는 접근하지 않습니다.
    public static class FlappySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame02.unity";
        private const string ArtPath = "Assets/MiniGames/FlappyBird";
        private static readonly Color Ink = new Color32(20, 44, 51, 255);
        private static readonly Color Paper = new Color32(241, 245, 224, 255);
        private static readonly Color Mint = new Color32(166, 222, 169, 255);

        [MenuItem("Tools/Mini Games/02 Flappy Bird/Open or Create Scene")]
        public static void OpenOrCreate()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        public static void Build()
        {
            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (scene.GetRootGameObjects().Any(x => x.GetComponentInChildren<FlappyGame>(true) != null))
            { Validate(); return; } // 재실행해도 팀원이 조정한 씬과 에셋을 덮어쓰지 않습니다.
            var oldRoots = scene.GetRootGameObjects();
            var owner = new GameObject("MiniGame02 - Flappy Bird");
            if (oldRoots.Length > 0)
            {
                // 씬 루트를 다시 활성화해도 옛 테스트 오브젝트가 실행되지 않도록 비활성 자식에 보관합니다.
                var archive = new GameObject("Legacy placeholders - inactive");
                archive.transform.SetParent(owner.transform, false);
                archive.SetActive(false);
                foreach (var oldRoot in oldRoots)
                { oldRoot.SetActive(false); oldRoot.transform.SetParent(archive.transform, true); }
            }
            var game = owner.AddComponent<FlappyGame>();
            var cameraObject = new GameObject("Flappy Camera", typeof(Camera));
            cameraObject.transform.SetParent(owner.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            camera.cullingMask = 1 << 5; camera.depth = 1; camera.tag = "Untagged";
            camera.allowHDR = false; camera.allowMSAA = false;
            // 미니게임 자체 화면에는 카메라 후처리를 적용하지 않습니다.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Flappy Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var font = Font();
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(1080, 800));
            Label("Index", content, font, "MINI GAME  /  02", new Vector2(-280, 349), new Vector2(400, 28), 18, Mint, TextAlignmentOptions.Left);
            Label("Title", content, font, "FLAPPY BIRD", new Vector2(-230, 302), new Vector2(500, 56), 40, Paper, TextAlignmentOptions.Left);
            var counter = Label("Progress", content, font, "00 / 15", new Vector2(340, 306), new Vector2(280, 50), 34, Mint, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 263), new Vector2(960, 5), new Color32(50, 74, 76, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Mint).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var field = Panel("Playfield - collision boundaries", content, new Vector2(0, -20), new Vector2(960, 540), new Color32(195, 227, 221, 255));
            field.gameObject.AddComponent<RectMask2D>();
            // 30개의 이미지를 미리 저장해 플레이 중 생성/삭제 없이 15쌍을 표시합니다.
            var lower = new Image[15]; var upper = new Image[15];
            for (int i = 0; i < 15; i++)
            {
                lower[i] = Panel("Pipe " + (i + 1) + " lower", field.transform, Vector2.zero, Vector2.one, Mint);
                upper[i] = Panel("Pipe " + (i + 1) + " upper", field.transform, Vector2.zero, Vector2.one, Mint);
                foreach (var pipe in new[] { lower[i], upper[i] })
                {
                    var edge = pipe.gameObject.AddComponent<Outline>();
                    edge.effectColor = new Color32(26, 85, 76, 255); edge.effectDistance = new Vector2(2, -2);
                }
            }
            var bird = Panel("Bird - replaceable sprite", field.transform, Vector2.zero, new Vector2(43, 36), Color.white);
            bird.preserveAspect = true;
            Panel("Upper boundary", content, new Vector2(0, 250), new Vector2(960, 3), Ink);
            Panel("Lower boundary", content, new Vector2(0, -290), new Vector2(960, 3), Ink);
            Label("Controls", content, font, "SPACE  날갯짓     /     TAB  일시정지", new Vector2(0, -332), new Vector2(960, 38), 23, Paper);
            Label("Goal", content, font, "파이프 15쌍 통과  ·  파이프와 위·아래 경계를 피하세요", new Vector2(0, -371), new Vector2(980, 28), 18, Mint);
            var panel = Panel("Local state overlay", content, new Vector2(0, -20), new Vector2(960, 540), new Color(0.078f, 0.173f, 0.2f, 0.94f));
            var title = Label("State title", panel.transform, font, "플래피버드", new Vector2(0, 108), new Vector2(800, 70), 42, Paper);
            var body = Label("State description", panel.transform, font, game.StartInstructions, new Vector2(0, -5), new Vector2(850, 130), 25, Paper);
            var prompt = Label("State prompt", panel.transform, font, "SPACE  시작", new Vector2(0, -140), new Vector2(850, 40), 24, Mint);
            var importer = AssetImporter.GetAtPath(ArtPath + "/Bird.png") as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Bird.png is required.");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            var view = owner.AddComponent<FlappyView>();
            view.Configure(game, bird, lower, upper, AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/Bird.png"), counter, fill, panel.gameObject, title, body, prompt);
            game.ResetGame(); view.Refresh();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets(); Validate();
        }

        public static void Validate()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("MiniGame02 only.");
            var root = scene.GetRootGameObjects().Single(x => x.GetComponent<FlappyGame>() != null);
            if (root.GetComponent<FlappyView>() == null || root.GetComponentsInChildren<Image>(true).Length < 31)
                throw new InvalidOperationException("Flappy scene references missing.");
            var canvas = root.GetComponentInChildren<Canvas>();
            if (canvas.worldCamera == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
                throw new InvalidOperationException("Camera canvas missing.");
            Debug.Log("FLAPPY_SCENE_VALIDATED");
        }

        private static TMP_FontAsset Font()
        {
            string path = ArtPath + "/FlappyUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) +
                "플래피버드 파이프 쌍을 통과하세요 누를 때마다 위로 날아갑니다 와 위·아래 경계에 닿으면 실패합니다 " +
                "날갯짓 일시정지 시작 성공! 다시 도전! 화면 부딪혔습니다 통과했습니다 위치와 횟수는 유지됩니다 계속하기 놓고 눌러 재시도 피하세요";
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            return font;
        }

        // UI 좌표와 판정 좌표는 60px : 1 단위로 고정해 해상도 변경 시 함께 축소합니다.
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
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
