using System;
using System.IO;
using System.Linq;
using GE.MiniGames.Invaders;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // MiniGame05 빌더와 같은 최초 제작 방식입니다. 기존 씬/폰트/전체 목록은 빌더에서 수정하지 않습니다.
    public static class InvadersSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MiniGame06.unity";
        private const string ArtPath = "Assets/MiniGames/Invaders";
        private static readonly Color Ink = new Color32(15, 30, 46, 255);
        private static readonly Color Field = new Color32(9, 20, 33, 255);
        private static readonly Color Paper = new Color32(237, 245, 249, 255);
        private static readonly Color Mint = new Color32(153, 246, 228, 255);

        [MenuItem("Tools/Mini Games/06 Invaders/Open or Create Scene")]
        public static void OpenOrCreate()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(); }

        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame06 - Invaders");
            var game = owner.AddComponent<InvadersGame>();
            var font = CreateFont(game.StartInstructions);
            var shipSprite = CreateSprite("Ship", new[] { "00011000", "00011000", "00111100", "01111110", "11111111", "11011011" });
            var enemySprite = CreateSprite("Enemy", new[] { "01000010", "00111100", "01111110", "11011011", "11111111", "00100100" });
            var cameraRoot = new GameObject("Invaders Camera", typeof(Camera));
            cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 8; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect("Invaders Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(680, 740);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(680, 740));
            Label("Index", content, font, "MINI GAME / 06", new Vector2(-160, 322), new Vector2(280, 25), 15, Mint, TextAlignmentOptions.Left);
            Label("Title", content, font, "SPACE INVADERS", new Vector2(-90, 281), new Vector2(420, 48), 32, Paper, TextAlignmentOptions.Left);
            var counter = Label("Kills", content, font, "처치 0 / 18", new Vector2(215, 286), new Vector2(170, 40), 22, Mint, TextAlignmentOptions.Right);
            var track = Panel("Progress track", content, new Vector2(0, 248), new Vector2(600, 4), new Color32(46, 65, 82, 255));
            var fill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.zero, Mint).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var field = Panel("Playfield 600 x 480", content, new Vector2(0, -12), new Vector2(600, 480), Field).rectTransform;
            field.gameObject.AddComponent<RectMask2D>();
            // 배경과 방어선은 정적 UI입니다. 좌우 테두리는 실제 이동 경계(20, 580)와 일치합니다.
            Panel("Left boundary", field, new Vector2(-280, 0), new Vector2(1, 480), new Color32(45, 65, 82, 255));
            Panel("Right boundary", field, new Vector2(280, 0), new Vector2(1, 480), new Color32(45, 65, 82, 255));
            for (int i = 0; i < 28; i++)
                Panel("Defense dash " + i, field, new Vector2(-275 + i * 20, -180), new Vector2(10, 2), new Color32(251, 113, 133, 255));
            Label("Defense label", field, font, "방어선", new Vector2(245, -166), new Vector2(65, 20), 12, new Color32(251, 113, 133, 255));
            var enemies = new Image[18];
            for (int i = 0; i < 18; i++)
            {
                Color color = i < 6 ? new Color32(251, 191, 100, 255) : i < 12 ? new Color32(167, 185, 250, 255) : new Color32(103, 216, 223, 255);
                enemies[i] = Panel("Enemy " + (i + 1), field, Vector2.zero, new Vector2(32, 24), color);
                enemies[i].sprite = enemySprite;
            }
            // 새 씬은 방어막 2개에 각각 6조각만 만들며, 배치와 크기는 모델 규칙을 따릅니다.
            var shields = new Image[2 * InvadersRules.ShieldPiecesPerShield];
            for (int i = 0; i < shields.Length; i++) shields[i] = Panel("Shield piece " + (i + 1), field, Vector2.zero, Vector2.one * InvadersRules.ShieldPieceSize, new Color32(87, 170, 142, 255));
            var bulletLayer = Rect("Bullets", field, Vector2.zero, new Vector2(600, 480));
            var bullet = Panel("Bullet template - inactive", bulletLayer, Vector2.zero, new Vector2(4, 10), Mint);
            bullet.gameObject.SetActive(false);
            var player = Panel("Player ship", field, Vector2.zero, new Vector2(32, 24), Mint); player.sprite = shipSprite;
            var health = Label("Lives", content, font, "목숨 1", new Vector2(-225, -279), new Vector2(150, 30), 21, Mint, TextAlignmentOptions.Left);
            Label("Mission", content, font, "적 18기 모두 처치하기", new Vector2(115, -279), new Vector2(370, 30), 19, Paper, TextAlignmentOptions.Right);
            Label("Controls", content, font, "A / D  이동    SPACE  연사    TAB  일시정지", new Vector2(0, -326), new Vector2(620, 32), 19, Paper);
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(600, 480), new Color(.035f, .078f, .13f, .96f));
            var title = Label("State title", overlay.transform, font, "우주 방어전", new Vector2(0, 112), new Vector2(560, 60), 36, Paper);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(570, 145), 21, Paper);
            var prompt = Label("State prompt", overlay.transform, font, "Spacebar로 시작", new Vector2(0, -131), new Vector2(570, 45), 22, Mint);
            var commonUI = owner.AddComponent<MiniGameTestUI>();
            commonUI.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(commonUI);
            fields.FindProperty("gameTitle").stringValue = "우주 방어전"; fields.FindProperty("progressLabel").stringValue = "처치";
            fields.ApplyModifiedPropertiesWithoutUndo();
            var view = owner.AddComponent<InvadersView>();
            view.Configure(game, player, enemies, shields, bullet, bulletLayer, counter, health, fill);
            game.ResetGame(); view.Refresh();
            Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("INVADERS_SCENE_BUILT " + ScenePath);
        }

        private static TMP_FontAsset CreateFont(string instructions)
        {
            string path = ArtPath + "/InvadersUI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "우주 방어전 목숨 처치 적 기를 모두 처치했습니다! 방어선 돌파 기체 격추" +
                "로 시작 대기 중 일시정지 으로 재개 현재 위치와 진행도는 유지됩니다 다시 도전! 를 놓고 다시 눌러 재시도 진행 대기 중 성공!";
            chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path); foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }

        // 교체 가능한 작은 픽셀 도형을 전용 이미지로 저장합니다. 다른 미니게임 아트는 수정하지 않습니다.
        private static Sprite CreateSprite(string name, string[] pixels)
        {
            string path = ArtPath + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(pixels[0].Length, pixels.Length, TextureFormat.RGBA32, false);
                for (int y = 0; y < pixels.Length; y++) for (int x = 0; x < pixels[y].Length; x++)
                    texture.SetPixel(x, pixels.Length - y - 1, pixels[y][x] == '1' ? Color.white : Color.clear);
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
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
