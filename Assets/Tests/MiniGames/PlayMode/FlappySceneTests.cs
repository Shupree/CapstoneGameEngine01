#if UNITY_EDITOR
// 저장된 씬을 여는 이 검증은 에디터 전용이며 플레이어 빌드에는 포함하지 않습니다.
using System.Collections;
using System.IO;
using GE.MiniGames.FlappyBird;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GE.MiniGames.Tests
{
    // MiniGame02 단독 씬만 로드합니다. 다른 협업자의 씬이나 진행 시스템은 검증 대상이 아닙니다.
    public sealed class FlappySceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        {
            // 실제 장치의 포커스 이벤트가 가상 키보드 입력을 덮어쓰지 않도록 격리합니다.
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
        }
        public override void TearDown()
        {
            Application.runInBackground = previousBackground;
            base.TearDown();
        }
        private IEnumerator LoadScene()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame02.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("MiniGame02");
            var empty = SceneManager.CreateScene("Flappy test cleanup");
            SceneManager.SetActiveScene(empty);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedScene_RealKeyboard_HoldReleasePauseAndRetry()
        {
            yield return LoadScene();
            var game = Object.FindFirstObjectByType<FlappyGame>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Capture(game, "Ready", 1280, 960);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return new WaitForSeconds(0.15f);
            float v = game.Model.Velocity;
            yield return new WaitForSeconds(0.15f);
            Assert.That(game.Model.Velocity, Is.LessThan(v));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null; yield return null;
            Assert.That(game.Model.Velocity, Is.GreaterThan(v));
            Capture(game, "Playing", 1280, 960);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            float y = game.Model.BirdY;
            yield return new WaitForSeconds(0.1f);
            Assert.That(game.Model.BirdY, Is.EqualTo(y));
            Capture(game, "Paused", 720, 960);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(2f);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Capture(game, "Failed", 1280, 720);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Progress, Is.Zero);
            Assert.That(game.RunId, Is.EqualTo(2));
        }

        // 그래픽 장치가 있는 검증 실행에서 실제 Canvas 렌더를 저장합니다. 카메라 설정은 복원합니다.
        private static void Capture(FlappyGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>();
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                game.GetComponent<FlappyView>().Refresh();
                Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
                else camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/Flappy-" + label + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.Destroy(rt); Object.Destroy(pixels);
            }
        }
    }
}
#endif
