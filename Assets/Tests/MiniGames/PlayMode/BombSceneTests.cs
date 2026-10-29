#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using GE.MiniGames.Bomb;
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
    // 저장된 MiniGame07과 가상 키보드로 공통 입력·상태 UI·화면 비율을 검사합니다.
    public sealed class BombSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        { base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); previousBackground = Application.runInBackground; Application.runInBackground = true; }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("MiniGame07");
            var cleanup = SceneManager.CreateScene("Bomb test cleanup"); SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame07.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }

        [UnityTest]
        public IEnumerator StartHoldPauseResume_UsesCommonInputAndFreezesCountdown()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<BombGame>();
            Assert.That(game.GetComponent<MiniGameTestUI>(), Is.Not.Null);
            double readyTime = game.Model.TimeLeft; yield return new WaitForSeconds(.1f);
            Assert.That(game.Model.TimeLeft, Is.EqualTo(readyTime)); Capture(game, "Ready", 900, 1000);
            yield return Keys(Key.Space); yield return new WaitForSeconds(.15f);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Progress, Is.Zero);
            yield return Keys(Key.D); yield return new WaitForSeconds(.1f); Assert.That(game.Model.SelectedIndex, Is.EqualTo(1));
            yield return Keys(Key.W, Key.S); Assert.That(game.Model.SelectedIndex, Is.EqualTo(1));
            yield return Keys(Key.Tab); Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            double time = game.Model.TimeLeft; Capture(game, "Paused", 900, 1000);
            yield return Keys(Key.Space); yield return new WaitForSeconds(.15f);
            Assert.That(game.Model.TimeLeft, Is.EqualTo(time));
            yield return Keys(Key.Tab, Key.Space); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.Progress, Is.Zero);
            Capture(game, "Playing", 1280, 960);
        }

        [UnityTest]
        public IEnumerator CorrectCuts_ReachCommonSuccess_AndRepeatedSpaceDoesNotRecut()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<BombGame>();
            int results = 0; game.Completed += _ => results++;
            yield return Keys(Key.Space); yield return Keys();
            for (int step = 0; step < 4; step++)
            {
                int target = game.Model.Wires.ToList().IndexOf(game.Model.Sequence[step]);
                yield return Select(game, target);
                yield return Keys(Key.Space); yield return new WaitForSeconds(.05f);
                Assert.That(game.Progress, Is.EqualTo(step + 1));
                if (step == 0) { Capture(game, "Cut", 1280, 960); yield return Keys(); yield return Keys(Key.Space); Assert.That(game.Progress, Is.EqualTo(1)); }
                yield return Keys();
            }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1));
            Capture(game, "Success", 900, 1000);
        }

        [UnityTest]
        public IEnumerator WrongCutAndTimeout_RetryResetsAllLocalState()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<BombGame>();
            yield return Keys(Key.Space); yield return Keys();
            int wrong = game.Model.Wires.ToList().FindIndex(c => c != game.Model.Sequence[0]);
            yield return Select(game, wrong); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(game.ResultReason, Is.EqualTo("순서가 틀렸습니다."));
            Capture(game, "Failed", 900, 1000);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.RunId, Is.EqualTo(2));
            Assert.That(game.Progress, Is.Zero); Assert.That(game.Model.CutWires.All(x => !x), Is.True);
            Assert.That(game.Model.TimeLeft, Is.GreaterThan(4.5)); Assert.That(game.Model.SelectedIndex, Is.Zero);
            game.Model.Step(4.7); yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(game.ResultReason, Is.EqualTo("시간 초과!"));
        }

        [UnityTest]
        public IEnumerator ZxcCheat_SendsSuccessOnce_AndRequiresRelease()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<BombGame>();
            int results = 0; MiniGameResult last = default; game.Completed += result => { results++; last = result; };
            yield return Keys(Key.Space); yield return Keys(Key.Z, Key.X, Key.C);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(last.GameId, Is.EqualTo("MiniGame07"));
            Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success)); Assert.That(results, Is.EqualTo(1));
            game.StartGame(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(results, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CameraCanvas_FitsPortraitLandscapeAndAdditiveViewport()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<BombGame>();
            game.KeyboardInputEnabled = false; game.enabled = false; game.StartGame();
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = canvas.worldCamera;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 400 x 300");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(800, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target; camera.rect = new Rect(.15f, .15f, .7f, .7f);
                yield return null; Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; field.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - .1f)); Assert.That(bottom.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - .1f));
                Assert.That(top.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax + .1f)); Assert.That(top.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax + .1f));
                Assert.That((top.x - bottom.x) / (top.y - bottom.y), Is.EqualTo(4f / 3).Within(.001));
                camera.targetTexture = null; Object.Destroy(target);
            }
            camera.rect = new Rect(0, 0, 1, 1); Capture(game, "Wide", 1280, 720);
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true)) Assert.That(label.isTextOverflowing, Is.False, label.name);
        }
        // 눌림/해제를 실제 입력 경로로 전달해 키 유지와 별개로 전선 선택을 진행합니다.
        private IEnumerator Select(BombGame game, int target)
        {
            for (int move = 0; move < 4 && game.Model.SelectedIndex != target; move++)
            {
                yield return Keys(); yield return Keys(game.Model.SelectedIndex < target ? Key.D : Key.A);
            }
            Assert.That(game.Model.SelectedIndex, Is.EqualTo(target), "선택 입력이 처리되어야 합니다.");
            yield return Keys();
        }
        // 실제 URP 카메라의 상태별 화면을 저장해 레이아웃과 폰트 표시를 확인합니다.
        private static void Capture(BombGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; game.GetComponent<BombView>().Refresh(); Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Bomb-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
