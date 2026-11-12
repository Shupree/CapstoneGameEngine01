using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Baseball;
using GE.MiniGames.Fishing;
using GE.MiniGames.Gust;
using GE.MiniGames.Tomato;
using GE.MiniGames.DeadPixel;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // 전달받은 HTML 순서로 예약 씬만 채웁니다. 공통 시스템·메인게임·이미 완성된 씬은 수정하지 않습니다.
    public static class RemainingHtmlSceneBuilder
    {
        private static readonly Color Ink = new Color32(44, 62, 80, 255);
        private static readonly Color Paper = new Color32(189, 195, 199, 255);
        [MenuItem("Tools/Mini Games/13 Baseball/Open or Create Scene")]
        public static void OpenBaseball() { Open(13); }
        [MenuItem("Tools/Mini Games/11 Fishing/Open or Create Scene")]
        public static void OpenFishing() { Open(11); }
        [MenuItem("Tools/Mini Games/16 Gust/Open or Create Scene")]
        public static void OpenGust() { Open(16); }
        [MenuItem("Tools/Mini Games/15 Tomato/Open or Create Scene")]
        public static void OpenTomato() { Open(15); }
        [MenuItem("Tools/Mini Games/18 Dead Pixel/Open or Create Scene")]
        public static void OpenDeadPixel() { Open(18); }
        private static void Open(int number) { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildOne(number); }
        public static void Build() { foreach (int number in new[] { 13, 11, 16, 15, 18 }) BuildOne(number); }
        private static void BuildOne(int number)
        {
            string path = "Assets/Scenes/MiniGame" + number + ".unity";
            if (File.Exists(path))
            {
                var existing = EditorSceneManager.OpenScene(path); var roots = existing.GetRootGameObjects();
                // 이름과 구성 모두 예약 상태인지 확인해 협업자의 작업을 덮어쓰지 않습니다.
                if (roots.Length != 1 || !roots[0].name.EndsWith("(구현 예정)") ||
                    roots[0].transform.childCount != 0 || roots[0].GetComponents<Component>().Length != 1) return;
            }
            string kind, titleText, controls, hint, progressLabel;
            switch (number)
            {
                case 13: kind = "Baseball"; titleText = "야구"; controls = "SPACE  타격     TAB  일시정지"; hint = "공 중심이 초록 원 안에 있을 때 누르세요."; progressLabel = "타격"; break;
                case 11: kind = "Fishing"; titleText = "낚시"; controls = "W / S  이동     TAB  일시정지"; hint = "영역 안에 물고기를 유지하세요."; progressLabel = "포획"; break;
                case 16: kind = "Gust"; titleText = "강풍 버티기"; controls = "SPACE  연타     TAB  일시정지"; hint = "너무 많이 눌러도 오른쪽 벽에 부딪힙니다."; progressLabel = "버틴 시간"; break;
                case 15: kind = "Tomato"; titleText = "토마토 받기"; controls = "A / D  이동     TAB  일시정지"; hint = "빨간 토마토는 받고 회색 돌은 피하세요."; progressLabel = "받은 토마토"; break;
                default: kind = "DeadPixel"; titleText = "틀린그림 찾기"; controls = "W A S D  선택     SPACE  확인"; hint = "색이 밝은 한 칸을 찾아 확인하세요."; progressLabel = "찾음"; break;
            }
            string artPath = "Assets/MiniGames/" + kind; Directory.CreateDirectory(artPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame" + number + " - " + kind);
            MiniGameController game;
            switch (number)
            {
                case 13: game = owner.AddComponent<BaseballGame>(); break;
                case 11: game = owner.AddComponent<FishingGame>(); break;
                case 16: game = owner.AddComponent<GustGame>(); break;
                case 15: game = owner.AddComponent<TomatoGame>(); break;
                default: game = owner.AddComponent<DeadPixelGame>(); break;
            }
            var font = CreateFont(game.StartInstructions, artPath);
            var cameraRoot = new GameObject(kind + " Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect(kind + " Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 760); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(720, 760));
            Label("Index", content, font, "MINI GAME / " + number, new Vector2(0, 335), new Vector2(650, 30), 17, Color.white);
            Label("Title", content, font, titleText, new Vector2(0, 284), new Vector2(650, 60), 32, Color.white);
            var border = Panel("Frame", content, Vector2.zero, new Vector2(652, 492), Ink);
            var field = Panel("Playfield 400 x 300", border.transform, Vector2.zero, new Vector2(400, 300), Paper).rectTransform;
            field.localScale = Vector3.one * 1.6f; field.gameObject.AddComponent<RectMask2D>();
            var label = Label("Status", field, font, "", new Vector2(0, 128), new Vector2(380, 32), 16, Ink);
            var disc = number == 16 || number == 18 ? null : CreateDisc(artPath);
            if (number == 13)
            {
                var zone = Panel("Hit zone radius 21", field, new Vector2(-120, 0), new Vector2(42, 42), new Color(46/255f,204/255f,113/255f,.4f)); zone.sprite = disc;
                Panel("Bat", field, new Vector2(-120, -40), new Vector2(6, 80), Ink);
                var ball = Panel("Ball - replaceable sprite", field, Vector2.zero, new Vector2(20, 20), new Color32(231,76,60,255)); ball.sprite = disc;
                owner.AddComponent<BaseballView>().Configure((BaseballGame)game, ball.rectTransform, label);
            }
            else if (number == 11)
            {
                Panel("Track", field, Vector2.zero, new Vector2(20, 300), new Color32(127,140,141,255));
                var area = Panel("Capture zone", field, Vector2.zero, new Vector2(30, 70), Color.green);
                var fish = Panel("Fish - replaceable sprite", field, Vector2.zero, new Vector2(16, 16), new Color32(52,152,219,255)); fish.sprite = disc;
                var track = Panel("Capture progress", field, new Vector2(-130, -120), new Vector2(100, 20), Ink);
                var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, new Color32(46,204,113,255)).rectTransform;
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0,1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                var percent = Label("Capture percent", field, font, "", new Vector2(-130, -92), new Vector2(140,32), 15, Ink);
                owner.AddComponent<FishingView>().Configure((FishingGame)game, area, fish.rectTransform, fill, label, percent);
            }
            else if (number == 16)
            {
                Panel("Left danger", field, new Vector2(-180,0), new Vector2(40,300), new Color(231/255f,76/255f,60/255f,.3f));
                Panel("Right danger", field, new Vector2(180,0), new Vector2(40,300), new Color(231/255f,76/255f,60/255f,.3f));
                var player = Panel("Player - replaceable sprite", field, Vector2.zero, new Vector2(30,30), Ink);
                var wind = Label("Wind", field, font, "", new Vector2(0,-100), new Vector2(350,40), 16, Ink);
                owner.AddComponent<GustView>().Configure((GustGame)game, player.rectTransform, label, wind);
            }
            else if (number == 15)
            {
                var basket = Panel("Basket - replaceable sprite", field, new Vector2(0,-120), new Vector2(48,20), new Color32(243,156,18,255));
                // 생성 간격과 최대 낙하 시간을 고려해 넉넉한 4개 표시 슬롯을 미리 확보합니다.
                var items = new Image[4];
                for (int i=0; i<items.Length; i++) { items[i] = Panel("Item " + i + " - replaceable sprite", field, Vector2.zero, new Vector2(24,24), Color.red); items[i].sprite = disc; }
                owner.AddComponent<TomatoView>().Configure((TomatoGame)game, basket.rectTransform, items, label);
            }
            else
            {
                var cells = new Image[9];
                for (int y=0; y<3; y++) for (int x=0; x<3; x++) cells[y*3+x] = Panel("Cell " + (y*3+x), field, new Vector2(-70+x*70,60-y*70), new Vector2(60,60), Color.gray);
                var cursor = Rect("Selection border", field, Vector2.zero, new Vector2(64,64));
                for (int i=0; i<4; i++) Panel("Edge " + i, cursor, i<2 ? new Vector2(i==0 ? -30:30,0) : new Vector2(0,i==2 ? -30:30), i<2 ? new Vector2(4,64) : new Vector2(64,4), Color.white);
                owner.AddComponent<DeadPixelView>().Configure((DeadPixelGame)game, cells, cursor, label);
            }
            label.transform.SetAsLastSibling();
            Label("Controls", content, font, controls, new Vector2(0,-275), new Vector2(680,40), 22, Color.white);
            Label("Hint", content, font, hint, new Vector2(0,-313), new Vector2(680,30), 19, new Color32(230,126,34,255));
            // R 시작·재시도 및 결과 표시 책임은 기존 공통 UI에 둡니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(400,300), new Color(.17f,.24f,.31f,.97f));
            var title = Label("State title", overlay.transform, font, titleText, new Vector2(0,103), new Vector2(380,50), 26, Color.white);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, Vector2.zero, new Vector2(380,140), 16, Color.white);
            var prompt = Label("State prompt", overlay.transform, font, "R로 시작", new Vector2(0,-108), new Vector2(380,38), 20, new Color32(230,126,34,255));
            var ui = owner.AddComponent<MiniGameTestUI>(); ui.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(ui); fields.FindProperty("gameTitle").stringValue = titleText; fields.FindProperty("progressLabel").stringValue = progressLabel; fields.ApplyModifiedPropertiesWithoutUndo();
            game.ResetGame(); owner.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
            EditorSceneManager.SaveScene(scene,path); Debug.Log("REMAINING_HTML_SCENE_BUILT " + path);
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
                "야구 낚시 강풍 버티기 토마토 받기 틀린그림 찾기 불량 화소 타격 포획 버틴 시간 받은 찾음 남은 시간 초 왼쪽으로 부는 바람 이동 일시정지 확인 선택 연타 타이밍이 빗나갔습니다 너무 일찍 휘둘렀습니다 공을 놓쳤습니다 7번 모두 타격했습니다 포획 성공 시간 초과 6초 동안 버텼습니다 벽에 부딪혔습니다 토마토 7개를 모두 받았습니다 돌을 받았습니다 토마토를 놓쳤습니다 15개 모두 찾았습니다 틀렸습니다 공 중심이 초록 원 안에 있을 때 누르세요 영역 안에 물고기를 유지하세요 너무 많이 눌러도 오른쪽 벽에 부딪힙니다 빨간 토마토는 받고 회색 돌은 피하세요 색이 밝은 한 칸을 찾아 확인하세요" +
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
