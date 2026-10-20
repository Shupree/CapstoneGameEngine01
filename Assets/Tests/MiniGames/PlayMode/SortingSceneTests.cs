#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using GE.MiniGames.Sorting;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 MiniGame03을 직접 로드합니다. 빌드 목록이나 다른 씬을 수정하지 않고 실제 키 입력을 검증합니다.
    public sealed class SortingSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        private MonoBehaviour host;
        public override void Setup()
        {
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
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame03.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (host != null) Object.Destroy(host.gameObject);
            var scene = SceneManager.GetSceneByName("MiniGame03");
            SceneManager.SetActiveScene(SceneManager.CreateScene("Sorting cleanup"));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Keyboard_StartHoldPauseFailureRetryAndClear()
        {
            yield return Load();
            var game = Object.FindFirstObjectByType<SortingGame>();
            Assert.That(game, Is.Not.Null);
            Capture(game, "Ready", 1280, 960);
            int results = 0;
            game.Completed += result => results++;
            yield return Keys(Key.Space);
            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Progress, Is.Zero, "시작 Space로 첫 물건까지 처리하면 안 됩니다.");
            yield return Keys();
            yield return Keys(KeyFor(game.Model.Current));
            Assert.That(game.Progress, Is.EqualTo(1));
            yield return null; yield return null;
            Assert.That(game.Progress, Is.EqualTo(1));
            Capture(game, "Playing", 1280, 960);
            // 메인에서 사용하는 축소 viewport에서도 화면 비율과 하단 조작 안내를 확인합니다.
            var camera = game.GetComponentInChildren<Camera>();
            camera.rect = new Rect(0.15f, 0.15f, 0.7f, 0.7f);
            Capture(game, "Viewport", 1280, 720);
            camera.rect = new Rect(0, 0, 1, 1);
            yield return Keys();
            yield return Keys(Key.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            var item = game.Model.Current;
            yield return Keys(Key.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            yield return Keys(KeyFor(item));
            Capture(game, "Paused", 720, 960);
            yield return Keys(Key.Tab, KeyFor(item));
            yield return Keys(KeyFor(item));
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Progress, Is.EqualTo(1));
            // Space가 오답이 되는 물건까지 정답 처리한 뒤, 누른 채 결과에 머무는지 확인합니다.
            while (SortingModel.RequiredChoice(game.Model.Current) == SortingChoice.Discard)
            { yield return Keys(); yield return Keys(Key.Space); }
            yield return Keys();
            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Capture(game, "Failed", 1280, 720);
            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Assert.That(results, Is.EqualTo(1));
            yield return Keys(); yield return Keys(Key.Space);
            Assert.That(game.Progress, Is.Zero);
            Assert.That(game.Model.Remaining, Is.EqualTo(30));
            Assert.That(game.RunId, Is.EqualTo(2));
            for (int i = 0; i < 30; i++)
            {
                yield return Keys();
                yield return Keys(KeyFor(game.Model.Current));
                Assert.That(game.Progress, Is.EqualTo(i + 1));
            }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded));
            Assert.That(results, Is.EqualTo(2));
            Capture(game, "Success", 1280, 960);
            game.StopGame();
            Assert.That(game.Model, Is.Null);
        }

        [UnityTest]
        public IEnumerator ExistingManager_ReceivesFailureOnce_AndFinalClearUnloadsScene()
        {
            yield return Load();
            var game = Object.FindFirstObjectByType<SortingGame>();
            // 테스트에서만 리플렉션으로 Assembly-CSharp 경계를 넘습니다. 런타임 게임에는 메인 직접 참조가 없습니다.
            var type = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            // 마지막 일반 씬은 언로드할 수 없으므로 메인 역할의 빈 씬을 메모리에만 둡니다.
            SceneManager.SetActiveScene(SceneManager.CreateScene("Sorting host harness"));
            var root = new GameObject("Sorting test host");
            host = (MonoBehaviour)root.AddComponent(type);
            host.enabled = false; // Start의 기존 무작위 씬 선택 대신 테스트 대상 씬만 연결합니다.
            Set(type, "currentLoadedMiniGame", "MiniGame03");
            Set(type, "totalMiniGamesToClear", 1);
            type.GetMethod("BindMiniGameResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host, new object[] { game.gameObject.scene });
            yield return Keys(Key.Space); yield return Keys();
            int lives = (int)type.GetField("currentLives", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            yield return Keys(KeyFor(game.Model.Current) == Key.A ? Key.D : Key.A);
            Assert.That((int)type.GetField("currentLives", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host), Is.EqualTo(lives - 1));
            type.GetMethod("OnMiniGameFailed").Invoke(host, null);
            Assert.That((int)type.GetField("currentLives", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host), Is.EqualTo(lives - 1));
            yield return Keys(); yield return Keys(Key.Space);
            for (int i = 0; i < 30; i++)
            { yield return Keys(); yield return Keys(KeyFor(game.Model.Current)); }
            for (int i = 0; i < 120 && SceneManager.GetSceneByName("MiniGame03").isLoaded; i++) yield return null;
            Assert.That((int)type.GetProperty("CurrentClearedCount").GetValue(host), Is.EqualTo(1));
            Assert.That(SceneManager.GetSceneByName("MiniGame03").isLoaded, Is.False);
        }

        private void Set(Type type, string field, object value)
        {
            type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(host, value);
        }
        private static Key KeyFor(SortingItem item)
        {
            var choice = SortingModel.RequiredChoice(item);
            return choice == SortingChoice.Left ? Key.A : choice == SortingChoice.Right ? Key.D : Key.Space;
        }

        // 화면 검증용 렌더만 Logs에 저장합니다. 카메라나 씬 데이터는 저장하지 않습니다.
        private static void Capture(SortingGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>();
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                game.GetComponent<SortingView>().Refresh();
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/Sorting-" + label + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.Destroy(target); Object.Destroy(pixels);
            }
        }
    }
}
#endif
