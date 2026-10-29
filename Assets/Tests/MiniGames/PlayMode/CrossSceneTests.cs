#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Cross;
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
    // 실제 저장된 MiniGame05와 공통 UI/키보드 입력을 검증합니다. 빌드 목록과 다른 씬은 저장하지 않습니다.
    public sealed class CrossSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        {
            base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>();
            previousBackground = Application.runInBackground; Application.runInBackground = true;
        }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("MiniGame05");
            var cleanup = SceneManager.GetSceneByName("Cross test cleanup");
            if (!cleanup.IsValid()) cleanup = SceneManager.CreateScene("Cross test cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame05.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }

        [UnityTest]
        public IEnumerator SavedScene_StartHoldPauseFailureAndRetry()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<CrossGame>();
            Assert.That(game, Is.Not.Null); Assert.That(game.GetComponent<MiniGameTestUI>(), Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready)); Capture(game, "Ready", 900, 1200);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Model.PlayerRow, Is.EqualTo(11), "시작 Space는 전진으로 재사용하지 않습니다.");
            yield return Keys(Key.A); Assert.That(game.Model.PlayerColumn, Is.EqualTo(4));
            yield return Keys(Key.A); Assert.That(game.Model.PlayerColumn, Is.EqualTo(4));
            Capture(game, "Playing", 900, 1200);
            yield return Keys(Key.Tab); Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            var xs = game.Model.Cars.Select(c => c.X).ToArray();
            yield return Keys(Key.Tab, Key.W); yield return new WaitForSeconds(.1f);
            Assert.That(game.Model.Cars.Select(c => c.X), Is.EqualTo(xs));
            Assert.That(game.Model.PlayerRow, Is.EqualTo(11)); Capture(game, "Paused", 1280, 720);
            yield return Keys(Key.W); yield return Keys(Key.Tab, Key.W); yield return Keys(Key.W);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Model.PlayerRow, Is.EqualTo(11));
            yield return Keys();
            // 다음 차선을 막아 실패를 결정적으로 발생시킵니다. 실제 컨트롤러와 충돌 판정은 그대로 사용합니다.
            SetX(game.Model.Cars.First(c => c.Row == 10), game.Model.PlayerX);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Capture(game, "Failed", 900, 1200);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.RunId, Is.EqualTo(2)); Assert.That(game.Model.PlayerRow, Is.EqualTo(11));
        }

        [UnityTest]
        public IEnumerator SavedScene_SuccessUsesSharedResultContractAndStopsVehicles()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<CrossGame>();
            game.KeyboardInputEnabled = false; game.enabled = false;
            int results = 0; MiniGameResult last = default;
            game.Completed += result => { results++; last = result; };
            game.StartGame(); Frame(game, 0);
            foreach (var car in game.Model.Cars) SetX(car, 0);
            for (int i = 0; i < 11; i++) { Frame(game, 0, 1); Frame(game, 0); }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1));
            Assert.That(last.GameId, Is.EqualTo("MiniGame05")); Assert.That(last.Progress, Is.EqualTo(10));
            Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success));
            var xs = game.Model.Cars.Select(c => c.X).ToArray(); Frame(game, 1, 16);
            Assert.That(game.Model.Cars.Select(c => c.X), Is.EqualTo(xs)); Assert.That(results, Is.EqualTo(1));
            Capture(game, "Success", 900, 1200);
            game.StopGame(); yield return null; Assert.That(game.Model, Is.Null);
        }

        [UnityTest]
        public IEnumerator CameraCanvas_FitsPortraitLandscapeAndAdditiveViewport()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<CrossGame>();
            game.KeyboardInputEnabled = false; game.enabled = false; game.StartGame();
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = game.GetComponentInChildren<Camera>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera)); Assert.That(canvas.worldCamera, Is.EqualTo(camera));
            var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 500 x 600");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(800, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target; camera.rect = new Rect(.15f, .15f, .7f, .7f);
                yield return null; Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; field.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - .1f));
                Assert.That(bottom.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - .1f));
                Assert.That(top.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax + .1f));
                Assert.That(top.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax + .1f));
                Assert.That((top.x - bottom.x) / (top.y - bottom.y), Is.EqualTo(500f / 600).Within(.001));
                camera.targetTexture = null; Object.Destroy(target);
            }
            camera.rect = new Rect(0, 0, 1, 1); Capture(game, "Wide", 1280, 720);
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.isTextOverflowing, Is.False, label.name);
        }

        // PlayMode 어셈블리의 접근 범위를 넓히지 않고 기존 테스트들과 같은 리플렉션 방식으로 프레임을 공급합니다.
        private static void Frame(CrossGame game, float dt, int buttons = 0)
        {
            var method = typeof(MiniGameController).GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(game, new[] { (object)dt, Enum.ToObject(method.GetParameters()[1].ParameterType, buttons) });
        }
        private static void SetX(CrossCar car, float x) { typeof(CrossCar).GetProperty("X").SetValue(car, x); }

        // 실제 카메라 출력을 Logs에만 보관합니다. 캡처 후 렌더 타깃을 복원하여 씬 상태에 남기지 않습니다.
        private static void Capture(CrossGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            game.GetComponent<CrossView>().Refresh();
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Cross-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
