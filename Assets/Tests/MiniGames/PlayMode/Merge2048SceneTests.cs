#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Merge2048;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 실제 씬·가상 키보드로 기존 입력/결과 계약과 정사각 보드 표시를 검증합니다.
    public sealed class Merge2048SceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        { base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); previousBackground = Application.runInBackground; Application.runInBackground = true; }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("MiniGame09");
            var cleanup = SceneManager.CreateScene("2048 test cleanup"); SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame09.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }
        // 성공/막힘 직전 고정 배치는 검증에만 주입합니다. 실제 이동은 키보드→공통 컨트롤러 경로로 처리합니다.
        private static void SetBoard(Merge2048Game game, params int[] values)
        {
            var cells = (int[])typeof(Merge2048Model).GetField("cells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Model);
            Array.Clear(cells, 0, cells.Length); Array.Copy(values, cells, values.Length);
            game.GetComponent<Merge2048View>().Refresh();
        }

        [UnityTest]
        public IEnumerator StartHeldKeysPauseAndResume_UseExistingInputContract()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<Merge2048Game>();
            Assert.That(game.GetComponent<MiniGameTestUI>(), Is.Not.Null); Capture(game, "Ready", 900, 1000);
            yield return Keys(Key.Space); yield return new WaitForSeconds(.1f);
            Assert.That(game.Model.MoveCount, Is.Zero); Assert.That(game.Model.Tiles.Count(x => x == 2), Is.EqualTo(2));
            SetBoard(game, 0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,2);
            yield return Keys(Key.A); yield return new WaitForSeconds(.15f);
            Assert.That(game.Model.MoveCount, Is.EqualTo(1)); Assert.That(game.Model.Tiles[12], Is.EqualTo(2));
            yield return Keys(Key.Space); Assert.That(game.Model.MoveCount, Is.EqualTo(1));
            yield return Keys(Key.Tab); var paused = game.Model.Tiles.ToArray();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused)); Capture(game, "Paused", 900, 1000);
            yield return Keys(Key.D); yield return new WaitForSeconds(.1f); Assert.That(game.Model.Tiles, Is.EqualTo(paused));
            yield return Keys(Key.Tab, Key.D); yield return Keys(Key.D);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Model.Tiles, Is.EqualTo(paused));
            yield return Keys(); yield return Keys(Key.D); Assert.That(game.Model.MoveCount, Is.EqualTo(2));
            Capture(game, "Playing", 1280, 960);
        }
        [UnityTest]
        public IEnumerator Merge128_SendsSingleSuccess_AndSpaceRestarts()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<Merge2048Game>();
            int results = 0; MiniGameResult last = default; game.Completed += result => { results++; last = result; };
            yield return Keys(Key.Space); yield return Keys(); SetBoard(game, 64,64);
            yield return Keys(Key.A); yield return Keys(Key.A);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1));
            Assert.That(last.GameId, Is.EqualTo("MiniGame09")); Assert.That(last.Progress, Is.EqualTo(128));
            Assert.That(last.Target, Is.EqualTo(128)); Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success));
            Assert.That(game.Model.Tiles.Count(x => x != 0), Is.EqualTo(1)); Capture(game, "Success", 900, 1000);
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.RunId, Is.EqualTo(2));
            Assert.That(game.Model.Tiles.Count(x => x == 2), Is.EqualTo(2)); Assert.That(game.Model.MoveCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator NoMoves_FailsOnce_AndRetryRestoresBoard()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<Merge2048Game>();
            int results = 0; game.Completed += _ => results++;
            yield return Keys(Key.Space); yield return Keys();
            SetBoard(game, 2,4,2,4, 4,2,4,2, 2,4,2,4, 4,2,4,2);
            yield return Keys(Key.A); yield return Keys(Key.D);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(results, Is.EqualTo(1));
            Assert.That(game.ResultReason, Is.EqualTo("더 이상 움직일 수 없습니다.")); Capture(game, "Failed", 900, 1000);
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Model.MoveCount, Is.Zero);
            Assert.That(game.Model.Tiles.Sum(), Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator ZxcCheat_SendsSuccessOnce_AndRequiresRelease()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<Merge2048Game>();
            int results = 0; MiniGameResult last = default; game.Completed += result => { results++; last = result; };
            yield return Keys(Key.Space); yield return Keys(Key.Z, Key.X, Key.C);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(last.GameId, Is.EqualTo("MiniGame09"));
            Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success)); Assert.That(results, Is.EqualTo(1));
            game.StartGame(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(results, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator CameraCanvas_FitsSixteenTilesAndAdditiveViewport()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<Merge2048Game>();
            game.KeyboardInputEnabled = false; game.enabled = false; game.StartGame();
            SetBoard(game, 2,4,8,16, 32,64,128,0, 4,8,16,0, 2,4,0,0);
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = canvas.worldCamera;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 480 x 480");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(800, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target; camera.rect = new Rect(.15f, .15f, .7f, .7f);
                yield return null; Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; field.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - .1f)); Assert.That(bottom.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - .1f));
                Assert.That(top.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax + .1f)); Assert.That(top.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax + .1f));
                Assert.That((top.x - bottom.x) / (top.y - bottom.y), Is.EqualTo(1).Within(.001));
                camera.targetTexture = null; Object.Destroy(target);
            }
            camera.rect = new Rect(0, 0, 1, 1); Capture(game, "Tiles", 1280, 960);
            Assert.That(game.GetComponentsInChildren<Image>().Count(x => x.name.StartsWith("Tile ")), Is.EqualTo(16));
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true)) Assert.That(label.isTextOverflowing, Is.False, label.name);
        }
        // 실제 URP 카메라 화면에서 숫자와 한글 및 보드 배치를 확인합니다.
        private static void Capture(Merge2048Game game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; game.GetComponent<Merge2048View>().Refresh(); Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Merge2048-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
