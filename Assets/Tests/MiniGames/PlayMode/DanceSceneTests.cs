#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using GE.MiniGames.Dance;
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
    // MiniGame08 실제 씬에서 키 유지·공통 상태 UI·10라운드 완료·뷰포트를 확인합니다.
    public sealed class DanceSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        { base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); previousBackground = Application.runInBackground; Application.runInBackground = true; }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("MiniGame08");
            var cleanup = SceneManager.CreateScene("Dance test cleanup"); SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame08.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }
        private static Key KeyFor(DanceDirection direction) => new[] { Key.W, Key.A, Key.S, Key.D }[(int)direction];

        [UnityTest]
        public IEnumerator StartHeldKeysAndPause_UseExistingInputContract()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DanceGame>();
            Assert.That(game.GetComponent<MiniGameTestUI>(), Is.Not.Null);
            yield return new WaitForSeconds(.1f); Assert.That(game.Model.TimeLeft, Is.EqualTo(4.8)); Capture(game, "Ready", 900, 1000);
            yield return Keys(Key.Space); yield return new WaitForSeconds(.1f);
            Assert.That(game.Model.InputIndex, Is.Zero); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(KeyFor(game.Model.Sequence[0])); yield return new WaitForSeconds(.1f);
            Assert.That(game.Model.InputIndex, Is.EqualTo(1), "같은 키를 유지해도 다음 칸을 자동으로 입력하지 않습니다.");
            yield return Keys(Key.Tab); double time = game.Model.TimeLeft;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused)); Capture(game, "Paused", 900, 1000);
            var blocked = KeyFor((DanceDirection)(((int)game.Model.Sequence[1] + 1) % 4));
            yield return Keys(blocked); yield return new WaitForSeconds(.1f); Assert.That(game.Model.TimeLeft, Is.EqualTo(time));
            yield return Keys(Key.Tab, blocked); yield return Keys(blocked);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Model.InputIndex, Is.EqualTo(1));
            Capture(game, "Playing", 1280, 960);
        }

        [UnityTest]
        public IEnumerator KeyboardSequence_CompletesTenRounds_AndSendsOneSuccess()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DanceGame>();
            int results = 0; game.Completed += _ => results++;
            yield return Keys(Key.Space); yield return Keys();
            for (int round = 1; round <= 10; round++)
            {
                Assert.That(game.Model.Round, Is.EqualTo(round));
                var sequence = game.Model.Sequence.ToArray();
                foreach (var direction in sequence)
                {
                    yield return Keys(KeyFor(direction)); yield return Keys();
                    Assert.That(game.State, Is.Not.EqualTo(MiniGameState.Failed));
                }
                Assert.That(game.Progress, Is.EqualTo(round));
                if (round < 10)
                {
                    Assert.That(game.Model.IsTransitioning, Is.True);
                    yield return Keys(Key.W, Key.A, Key.S, Key.D); yield return Keys();
                    yield return new WaitForSeconds(.42f);
                }
            }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1));
            Capture(game, "Success", 900, 1000);
        }

        [UnityTest]
        public IEnumerator WrongInputAndTimeout_RequireSpaceRetryFromRoundOne()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DanceGame>();
            yield return Keys(Key.Space); yield return Keys();
            yield return Keys(KeyFor((DanceDirection)(((int)game.Model.Sequence[0] + 1) % 4)));
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(game.ResultReason, Is.EqualTo("방향 순서가 틀렸습니다."));
            Capture(game, "Failed", 900, 1000); yield return new WaitForSeconds(.6f);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed), "원본의 자동 초기화 대신 공통 재시도를 사용합니다.");
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.RunId, Is.EqualTo(2));
            Assert.That(game.Model.Round, Is.EqualTo(1)); Assert.That(game.Progress, Is.Zero); Assert.That(game.Model.InputIndex, Is.Zero);
            game.Model.Step(5); yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(game.ResultReason, Is.EqualTo("시간 초과!"));
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            yield return Keys(); yield return Keys(Key.Space); Assert.That(game.RunId, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator ZxcCheat_SendsSuccessOnce_AndRequiresRelease()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DanceGame>();
            int results = 0; MiniGameResult last = default; game.Completed += result => { results++; last = result; };
            yield return Keys(Key.Space); yield return Keys(Key.Z, Key.X, Key.C);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(last.GameId, Is.EqualTo("MiniGame08"));
            Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success)); Assert.That(results, Is.EqualTo(1));
            game.StartGame(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(results, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CameraCanvas_FitsTenDirectionsAndAdditiveViewport()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DanceGame>();
            game.KeyboardInputEnabled = false; game.enabled = false; game.StartGame();
            for (int round = 1; round < 10; round++) { foreach (var key in game.Model.Sequence) game.Model.Input(key); game.Model.Step(.4); }
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = canvas.worldCamera;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 800 x 450");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(800, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target; camera.rect = new Rect(.15f, .15f, .7f, .7f);
                yield return null; Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; field.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - .1f)); Assert.That(bottom.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - .1f));
                Assert.That(top.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax + .1f)); Assert.That(top.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax + .1f));
                Assert.That((top.x - bottom.x) / (top.y - bottom.y), Is.EqualTo(16f / 9).Within(.001));
                camera.targetTexture = null; Object.Destroy(target);
            }
            camera.rect = new Rect(0, 0, 1, 1); Capture(game, "Round10", 1280, 720);
            Assert.That(game.GetComponentsInChildren<Image>().Count(x => x.name.StartsWith("Direction card")), Is.EqualTo(10));
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true)) Assert.That(label.isTextOverflowing, Is.False, label.name);
        }
        // URP 카메라 화면을 저장해 한글과 10개 방향 표시를 시각적으로 확인합니다.
        private static void Capture(DanceGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; game.GetComponent<DanceView>().Refresh(); Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Dance-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
