using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Dino;
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
    // 최초 생성 전용입니다. 재실행하면 기존 씬을 열기만 하여 이후 Inspector/아트 교체 작업을 보존합니다.
    public static class DinoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame04.unity";
        public const string ArtPath = "Assets/MiniGames/Dino";
        private static readonly Color Paper = new Color32(245, 241, 226, 255);
        private static readonly Color Ink = new Color32(48, 65, 68, 255);
        private static readonly Color Green = new Color32(62, 139, 104, 255);

        [MenuItem("Tools/Mini Games/04 Dino/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            DinoArt art = CreateArt();
            TMP_FontAsset font = CreateFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame04 - Dino Run");
            var game = owner.AddComponent<DinoGame>(); game.Configure(new DinoRules(), art);
            var cameraRoot = new GameObject("Dino Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3.3f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Paper;
            camera.cullingMask = 1 << 5; camera.depth = 1; camera.allowHDR = camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Dino Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 330); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var field = Panel("Playfield 720 x 330", canvasRoot, Vector2.zero, new Vector2(720, 330), Paper).rectTransform;
            field.gameObject.AddComponent<RectMask2D>();
            var floor = Rect("Ground - same speed as obstacles", field, new Vector2(0, -133), new Vector2(720, 64)).gameObject.AddComponent<RawImage>();
            floor.texture = art.ground.texture; floor.raycastTarget = false; floor.uvRect = new Rect(0, 0, 720f / 64, 1);
            var runner = Panel("Dinosaur - replaceable art", field, new Vector2(-240, -73), new Vector2(48, 56), Color.white);
            runner.sprite = art.dinosaurRun[0].sprite;
            var flag = Panel("Finish flag", field, new Vector2(400, -74), new Vector2(28, 54), Color.white);
            flag.sprite = art.flag; flag.enabled = false;
            Label("Index", field, font, "MINI GAME / 04", new Vector2(-290, 143), new Vector2(125, 20), 11, Ink);
            Label("Title", field, font, "DINO RUN", new Vector2(295, 143), new Vector2(115, 20), 13, Ink);
            var track = Panel("Progress track", field, new Vector2(0, 143), new Vector2(270, 7), new Color32(207, 211, 193, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Green).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var percentage = Label("Distance percentage", field, font, "0%", new Vector2(0, 121), new Vector2(120, 24), 16, Ink);
            var overlay = Panel("Common state overlay", field, new Vector2(0, -28), new Vector2(720, 230), Paper);
            var title = Label("State title", overlay.transform, font, "크롬 공룡 게임", new Vector2(0, 75), new Vector2(650, 38), 25, Ink);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 7), new Vector2(690, 95), 17, Ink);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -78), new Vector2(670, 30), 18, Green);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var uiFields = new SerializedObject(commonUI);
            uiFields.FindProperty("gameTitle").stringValue = "크롬 공룡 게임";
            uiFields.FindProperty("progressLabel").stringValue = "거리";
            uiFields.ApplyModifiedPropertiesWithoutUndo();
            owner.AddComponent<DinoView>().Configure(game, field, runner, flag, floor, percentage, fill);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("DINO_SCENE_BUILT " + ScenePath);
        }

        // 코드의 다각형을 2px 도트로 채웁니다. 같은 다각형을 물리 실루엣에 저장해 빈 가지·날개 주변을 보존합니다.
        private static DinoArt CreateArt()
        {
            string path = ArtPath + "/DinoArt.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DinoArt>(path); if (existing != null) return existing;
            var art = ScriptableObject.CreateInstance<DinoArt>();
            art.dinosaurRun = new[] { DinoPose(0), DinoPose(1) }; art.dinosaurJump = DinoPose(2);
            var rules = new DinoRules();
            art.cactus = rules.cactusSizes.Select((size, index) => Frame("Cactus-" + index, size,
                new[] { Box(.42f,0,.25f,1), Box(.1f,.36f,.4f,.18f), Box(.04f,.45f,.2f,.36f), Box(.6f,.6f,.3f,.18f), Box(.78f,.69f,.18f,.26f) }, Green)).ToArray();
            art.rocks = rules.rockSizes.Select((size, index) => Frame("Rock-" + index, size,
                new[] { Polygon(0,0, 0,.45f, .22f,.9f, .55f,1, .9f,.64f, 1,.12f, 1,0) }, new Color32(132, 116, 101, 255))).ToArray();
            art.bird = new[] { BirdPose(0), BirdPose(1) };
            art.flag = Frame("Finish", new Vector2(28,54), new[] { Box(0,0,.14f,1), Box(.14f,.62f,.86f,.34f) }, Green).sprite;
            var ground = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat((Color32)new Color32(226, 219, 195, 255), 4096).ToArray();
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                if (y >= 62 || (y == 48 && x % 24 < 8) || (y == 28 && (x + 11) % 31 < 4)) pixels[y * 64 + x] = (Color32)Ink;
            ground.SetPixels32(pixels); ground.Apply(); art.ground = SaveTexture("Ground", ground, true);
            AssetDatabase.CreateAsset(art, path); return art;
        }

        private static DinoFrame DinoPose(int pose)
        {
            float leftLeg = pose == 1 ? 4 : 0, rightLeg = pose == 0 ? 4 : 0;
            return Frame("Dinosaur-" + pose, new Vector2(48,56), new[] {
                Pixels(48,56, 0,30, 4,30, 4,20, 18,16, 24,20, 24,28, 10,28),
                PixelBox(48,56,14,16,22,22), PixelBox(48,56,26,30,12,16), PixelBox(48,56,26,42,22,14),
                PixelBox(48,56,30,36,18,8), PixelBox(48,56,32,24,10,4),
                PixelBox(48,56,18,leftLeg,6,18-leftLeg), PixelBox(48,56,28,rightLeg,6,18-rightLeg)
            }, new Color32(70, 91, 96, 255), true);
        }
        private static DinoFrame BirdPose(int pose) => Frame("Bird-" + pose, new Vector2(40,28), new[] {
            PixelBox(40,28,8,8,24,12), Pixels(40,28,0,18,10,16,10,8,0,12), PixelBox(40,28,32,10,8,4),
            pose == 0 ? Pixels(40,28,12,18,20,28,26,18) : Pixels(40,28,12,10,18,0,26,10)
        }, new Color32(151, 89, 68, 255));

        private static DinoFrame Frame(string name, Vector2 size, DinoPolygon[] shapes, Color color, bool eye = false)
        {
            int width = (int)size.x, height = (int)size.y;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y += 2) for (int x = 0; x < width; x += 2)
            {
                Vector2 point = new Vector2((x + 1f) / width, (y + 1f) / height);
                if (!shapes.Any(shape => Inside(point, shape.points))) continue;
                bool edge = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down }.Any(dir =>
                    !shapes.Any(shape => Inside(point + dir * new Vector2(2f / width, 2f / height), shape.points)));
                Color32 fill = edge ? (Color32)Ink : (Color32)color;
                if (eye && x >= 36 && x < 40 && y >= 48 && y < 52) fill = (Color32)Paper;
                for (int dy = 0; dy < 2 && y + dy < height; dy++) for (int dx = 0; dx < 2 && x + dx < width; dx++) pixels[(y + dy) * width + x + dx] = fill;
            }
            texture.SetPixels32(pixels); texture.Apply();
            return new DinoFrame { sprite = SaveTexture(name, texture, false), collisionPolygons = shapes };
        }
        private static bool Inside(Vector2 point, Vector2[] path)
        {
            bool inside = false;
            for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
                if ((path[i].y > point.y) != (path[j].y > point.y) && point.x < (path[j].x - path[i].x) * (point.y - path[i].y) / (path[j].y - path[i].y) + path[i].x) inside = !inside;
            return inside;
        }
        private static DinoPolygon Polygon(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return new DinoPolygon { points = points };
        }
        private static DinoPolygon Box(float x, float y, float width, float height) => Polygon(x,y, x,y+height, x+width,y+height, x+width,y);
        private static DinoPolygon Pixels(float width, float height, params float[] xy)
        { var shape = Polygon(xy); for (int i = 0; i < shape.points.Length; i++) shape.points[i] /= new Vector2(width, height); return shape; }
        private static DinoPolygon PixelBox(float w, float h, float x, float y, float width, float height) => Box(x/w,y/h,width/w,height/h);

        private static Sprite SaveTexture(string name, Texture2D texture, bool repeat)
        {
            string path = ArtPath + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static TMP_FontAsset CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 40, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = "크롬 공룡 게임 목표지점까지 도달하기 점프 일시정지 재개 낮은 새는 높은 뛰지 않고 통과 거리 현재 위치와 진행도는 유지됩니다 다시 도전 성공 실패 장애물에 부딪혔습니다 목표지점에 도착했습니다 Spacebar를 놓고 눌러 재시도 시작 대기 중 으로" + new string(Enumerable.Range(32,95).Select(i => (char)i).ToArray());
            if (!font.TryAddCharacters(new string(chars.Distinct().ToArray()), out string missing)) throw new InvalidOperationException(missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; AssetDatabase.CreateAsset(font, ArtPath + "/DinoUI SDF.asset");
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font); return font;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = 5;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        { var image = Rect(name,parent,position,size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var label = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
        }
    }
}
