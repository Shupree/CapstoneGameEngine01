using System;
using System.IO;
using System.Linq;
using GE.MiniGames.SafeZone;
using GE.MiniGames.Stack;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GE.MiniGames.Editor
{
    // 예약된 12·14 씬만 구현합니다. 완성된 씬은 열기만 하며 메인·공통 코드와 다른 예약 씬은 건드리지 않습니다.
    public static class SafeZoneStackSceneBuilder
    {
        private static readonly Color Ink = new Color32(44, 62, 80, 255);
        private static readonly Color Paper = new Color32(189, 195, 199, 255);
        [MenuItem("Tools/Mini Games/12 SafeZone/Open or Create Scene")]
        public static void OpenSafeZone() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildOne(true); }
        [MenuItem("Tools/Mini Games/14 Stack/Open or Create Scene")]
        public static void OpenStack() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildOne(false); }
        public static void Build() { BuildOne(true); BuildOne(false); }
        private static void BuildOne(bool safe)
        {
            string number = safe ? "12" : "14", kind = safe ? "SafeZone" : "Stack";
            string path = "Assets/Scenes/MiniGame" + number + ".unity";
            if (File.Exists(path))
            {
                var existing = EditorSceneManager.OpenScene(path);
                var roots = existing.GetRootGameObjects();
                // 이름뿐 아니라 빈 Transform 하나만 있는지 확인해 협업자의 씬 편집을 덮어쓰지 않습니다.
                if (roots.Length != 1 || !roots[0].name.EndsWith("(구현 예정)") ||
                    roots[0].transform.childCount != 0 || roots[0].GetComponents<Component>().Length != 1) return;
            }
            string artPath = "Assets/MiniGames/" + kind;
            Directory.CreateDirectory(artPath); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("MiniGame" + number + " - " + kind);
            MiniGameController game = safe ? (MiniGameController)owner.AddComponent<SafeZoneGame>() : owner.AddComponent<StackGame>();
            var font = CreateFont(game.StartInstructions, artPath);
            var cameraRoot = new GameObject(kind + " Camera", typeof(Camera)); cameraRoot.transform.SetParent(owner.transform, false);
            var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink; camera.cullingMask = 1 << 5; camera.depth = 1;
            camera.allowHDR = camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasRoot = Rect(kind + " Canvas", owner.transform, Vector2.zero, Vector2.zero);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720, 760);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", canvasRoot, Vector2.zero, new Vector2(720, 760));
            string titleText = safe ? "안전지대" : "박스 쌓기";
            Label("Index", content, font, "MINI GAME / " + number, new Vector2(0, 335), new Vector2(650, 30), 17, Color.white);
            Label("Title", content, font, titleText, new Vector2(0, 284), new Vector2(650, 60), 32, Color.white);
            var border = Panel("Warning frame", content, Vector2.zero, new Vector2(652, 492), Ink);
            var field = Panel("Playfield 400 x 300", border.transform, Vector2.zero, new Vector2(400, 300), Paper).rectTransform;
            field.localScale = Vector3.one * 1.6f;
            field.gameObject.AddComponent<RectMask2D>();
            var label = Label("Status", field, font, "", new Vector2(0, 128), new Vector2(380, 32), 16, Ink);
            if (safe)
            {
                // 겹치지 않는 80px 띠는 최대 5개이므로 고정 슬롯 5개로 충분합니다.
                var regions = new Image[5];
                for (int i = 0; i < regions.Length; i++) regions[i] = Panel("Attack " + i, field, Vector2.zero, Vector2.zero, Color.red);
                var player = Panel("Player - replaceable sprite", field, Vector2.zero, new Vector2(20, 20), Ink);
                owner.AddComponent<SafeZoneView>().Configure((SafeZoneGame)game, player.rectTransform, regions, label);
                label.transform.SetAsLastSibling();
            }
            else
            {
                Panel("Floor", field, new Vector2(0, -150), new Vector2(400, 10), Ink);
                var blocks = new Image[8];
                for (int i = 0; i < blocks.Length; i++)
                {
                    blocks[i] = Panel("Block " + i + " - replaceable sprite", field, Vector2.zero, Vector2.zero,
                        i % 2 == 0 ? new Color32(52, 152, 219, 255) : new Color32(46, 204, 113, 255));
                    var outline = blocks[i].gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(1, -1);
                }
                var moving = Panel("Moving block - replaceable sprite", field, Vector2.zero, Vector2.zero, new Color32(241, 196, 15, 255));
                owner.AddComponent<StackView>().Configure((StackGame)game, blocks, moving, label);
            }
            Label("Controls", content, font, safe ? "W A S D  이동     TAB  일시정지" : "W A S D / SPACE  블록 쌓기", new Vector2(0, -275), new Vector2(680, 40), 22, Color.white);
            Label("Hint", content, font, safe ? "붉은 경고 영역을 피해 7초 동안 생존하세요." : "아래 블록과 겹칠 때 누르세요. 목표는 7개!", new Vector2(0, -313), new Vector2(680, 30), 19, new Color32(230, 126, 34, 255));
            // 공통 UI가 R 시작·재시도, Tab 정지, 결과 표시를 담당합니다.
            var overlay = Panel("Common state overlay", field, Vector2.zero, new Vector2(400, 300), new Color(.17f, .24f, .31f, .97f));
            var title = Label("State title", overlay.transform, font, titleText, new Vector2(0, 103), new Vector2(380, 50), 26, Color.white);
            var body = Label("State instructions", overlay.transform, font, game.StartInstructions, new Vector2(0, 0), new Vector2(380, 140), 16, Color.white);
            var prompt = Label("State prompt", overlay.transform, font, "R로 시작", new Vector2(0, -108), new Vector2(380, 38), 20, new Color32(230, 126, 34, 255));
            var ui = owner.AddComponent<MiniGameTestUI>(); ui.Configure(game, overlay.gameObject, title, body, prompt, game.StartInstructions);
            var fields = new SerializedObject(ui); fields.FindProperty("gameTitle").stringValue = titleText;
            fields.FindProperty("progressLabel").stringValue = safe ? "버틴 시간" : "쌓은 블록"; fields.ApplyModifiedPropertiesWithoutUndo();
            game.ResetGame();
            if (safe) owner.GetComponent<SafeZoneView>().Refresh(); else owner.GetComponent<StackView>().Refresh();
            EditorSceneManager.SaveScene(scene, path); Debug.Log("SAFEZONE_STACK_SCENE_BUILT " + path);
        }
        // 전용 정적 한글 폰트만 생성해 공용 폰트와 다른 미니게임 에셋에 영향을 주지 않습니다.
        private static TMP_FontAsset CreateFont(string instructions, string artPath)
        {
            string path = artPath + "/UI SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            string chars = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray()) + instructions +
                "안전지대 박스 쌓기 남은 시간 초 쌓은 블록 버틴 시간 이동 일시정지 붉은 경고 영역을 피해 7초 동안 생존하세요 아래 블록과 겹칠 때 누르세요 목표는 7개! 7초 동안 생존했습니다! 타격에 맞았습니다. 블록 7개를 쌓았습니다! 블록이 빗나갔습니다!" +
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
