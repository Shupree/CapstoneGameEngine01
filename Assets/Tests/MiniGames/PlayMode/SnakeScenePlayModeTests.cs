using System;
using System.Collections;
using System.IO;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    /// <summary>Loads the authored scene and uses its normal Update/input/UI paths.</summary>
    public sealed class SnakeScenePlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private SnakeGame game;
        private float originalTimeScale;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        public override void TearDown()
        {
            Time.timeScale = originalTimeScale;
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator UnloadTestScene()
        {
            Scene scene = SceneManager.GetSceneByName("MiniGame01");
            if (scene.IsValid() && scene.isLoaded)
                yield return UnloadSnakeScene();
        }

        [UnityTest]
        public IEnumerator SavedScene_SpaceStarts_TabPausesAndDropsPausedInput()
        {
            yield return LoadSnakeScene();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Vector2Int readyHead = game.Model.Body[0];
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(game.Model.Body[0], Is.EqualTo(readyHead));
            yield return Capture("Ready");

            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Capture("Playing");
            yield return Keys(Key.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            Vector2Int pausedHead = game.Model.Body[0];
            int pausedProgress = game.Progress;
            yield return Capture("Paused");
            yield return new WaitForSecondsRealtime(0.32f);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused), "A held Tab must toggle only once.");
            Assert.That(game.Model.Body[0], Is.EqualTo(pausedHead));
            Assert.That(game.Progress, Is.EqualTo(pausedProgress));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Local pause cannot freeze the host game.");

            yield return Keys(Key.Tab, Key.W); // Press W while still paused.
            yield return Keys(Key.W);          // Release Tab while keeping W held.
            yield return Keys(Key.Tab, Key.W);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(game.Model.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(game.Model.Body[0].y, Is.EqualTo(pausedHead.y));
            Assert.That(game.Model.Body[0].x, Is.GreaterThan(pausedHead.x));
            yield return Keys();
            yield return UnloadSnakeScene();
            AssertNoMiniGameObjects();
        }

        [UnityTest]
        public IEnumerator SavedScene_ResultRequiresFreshSpace_HideAndUnloadCleanUp()
        {
            yield return LoadSnakeScene();
            int resultCount = 0;
            game.Completed += result => resultCount++;
            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            int firstRun = game.RunId;
            yield return WaitUntilBounded(() => game.State == MiniGameState.Failed, 4f,
                "The straight-moving snake should hit the right wall.");
            Assert.That(resultCount, Is.EqualTo(1));
            yield return Capture("Result");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Assert.That(game.RunId, Is.EqualTo(firstRun), "Held Space from gameplay must not retry.");
            yield return Keys(Key.Space, Key.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed), "Tab cannot resume a terminal game.");

            yield return Keys();
            yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.RunId, Is.EqualTo(firstRun + 1));
            Assert.That(game.Progress, Is.Zero);
            Assert.That(game.Model.Body.Count, Is.EqualTo(3));
            yield return Keys();
            SnakeModel retainedModel = game.Model;
            Vector2Int hiddenHead = retainedModel.Body[0];
            GameObject controllerRoot = game.gameObject;
            controllerRoot.SetActive(false);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(retainedModel.Body[0], Is.EqualTo(hiddenHead));
            controllerRoot.SetActive(true);
            yield return null; // OnEnable's first Update intentionally cannot advance time.
            Assert.That(game.Model, Is.SameAs(retainedModel));
            Assert.That(game.Model.Body[0], Is.EqualTo(hiddenHead));
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));

            yield return UnloadSnakeScene();
            AssertNoMiniGameObjects();
            yield return null;
            Assert.That(resultCount, Is.EqualTo(1), "Unloading cannot deliver another result.");
            yield return LoadSnakeScene();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Assert.That(game.RunId, Is.Zero, "Reloaded scene must not inherit the previous run.");
            Assert.That(game.Progress, Is.Zero);
            yield return UnloadSnakeScene();
            AssertNoMiniGameObjects();
        }

        [UnityTest]
        public IEnumerator SavedScene_AdditiveViewportAndAspectChanges_KeepContentVisible()
        {
            yield return LoadSnakeScene();
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Camera camera = canvas.worldCamera;
            Rect originalRect = camera.rect;
            try
            {
                camera.rect = new Rect(0.15f, 0.15f, 0.7f, 0.7f);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                // Capture validates both the real Game View viewport and the actual
                // targetTexture viewport, so a target changing camera behavior cannot
                // conceal clipping that occurs when MainGame resizes the camera.
                yield return Capture("Viewport", true);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertContentInside(camera.pixelRect, ContentCorners(camera), "MainGame viewport");

                foreach (float aspect in new[] { 16f / 9f, 3f / 4f })
                {
                    camera.rect = CenteredViewport(aspect);
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(camera.pixelRect.width / camera.pixelRect.height,
                        Is.EqualTo(aspect).Within(0.02f));
                    AssertContentInside(camera.pixelRect, ContentCorners(camera), "Aspect " + aspect);
                }
            }
            finally
            {
                if (camera != null) camera.rect = originalRect;
            }
            yield return UnloadSnakeScene();
        }

        private IEnumerator LoadSnakeScene()
        {
            yield return SceneManager.LoadSceneAsync("MiniGame01", LoadSceneMode.Single);
            yield return null;
            yield return null;
            game = Object.FindFirstObjectByType<SnakeGame>();
            Assert.That(game, Is.Not.Null, "The saved MiniGame01 scene must contain SnakeGame.");
            Assert.That(game.gameObject.scene.path, Is.EqualTo("Assets/Scenes/MiniGame01.unity"));
            Assert.That(Object.FindFirstObjectByType<MiniGameTestUI>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SnakeView>(), Is.Not.Null);
        }

        private IEnumerator Keys(params Key[] held)
        {
            // InputTestFixture isolates real keyboard devices and processes this event
            // in Unity's next player-loop input update before the gameplay Update.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntilBounded(Func<bool> condition, float seconds, string failure)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private IEnumerator UnloadSnakeScene()
        {
            Scene snakeScene = SceneManager.GetSceneByName("MiniGame01");
            if (!snakeScene.IsValid() || !snakeScene.isLoaded) yield break;
            Scene empty = SceneManager.CreateScene("Snake smoke cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(snakeScene);
            yield return null;
            game = null;
        }

        private static void AssertNoMiniGameObjects()
        {
            Assert.That(Object.FindObjectsByType<SnakeGame>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<SnakeView>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<MiniGameTestUI>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        private static IEnumerator Capture(string state, bool verifyViewport = false)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log("SNAKE_CAPTURE_SKIPPED: graphics device unavailable for " + state);
                yield break;
            }

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Camera camera = canvas.worldCamera;
            Assert.That(camera, Is.Not.Null);
            Rect gameViewViewport = camera.pixelRect;
            Vector3[] gameViewCorners = verifyViewport ? ContentCorners(camera) : null;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            var target = new RenderTexture(1000, 760, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                yield return null; // Allow the camera-space CanvasScaler to use this target.
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True,
                        "The current pipeline must support a screenshot render request.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();

                RenderTexture.active = target;
                pixels = new Texture2D(1000, 760, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1000, 760), 0, 0);
                pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Snake-" + state + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log("SNAKE_CAPTURE " + path);
                if (verifyViewport)
                {
                    Debug.Log("SNAKE_VIEWPORT requested=" + camera.rect +
                        " gameViewPixels=" + gameViewViewport + " targetPixels=" + camera.pixelRect);
                    AssertContentInside(gameViewViewport, gameViewCorners, "Game View before targetTexture");
                    AssertContentInside(camera.pixelRect, ContentCorners(camera), "Rendered targetTexture viewport");
                }
            }
            finally
            {
                if (camera != null) camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                if (pixels != null) Object.Destroy(pixels);
                target.Release();
                Object.Destroy(target);
            }
        }

        private static Rect CenteredViewport(float aspect)
        {
            float width = Mathf.Min(Screen.width * 0.8f, Screen.height * 0.8f * aspect);
            float height = width / aspect;
            float normalizedWidth = width / Screen.width;
            float normalizedHeight = height / Screen.height;
            return new Rect((1f - normalizedWidth) * 0.5f, (1f - normalizedHeight) * 0.5f,
                normalizedWidth, normalizedHeight);
        }

        private static Vector3[] ContentCorners(Camera camera)
        {
            RectTransform content = null;
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (rect.name != "Content 1000 x 760") continue;
                content = rect;
                break;
            }
            Assert.That(content, Is.Not.Null, "Authored content bounds are required for viewport verification.");
            var corners = new Vector3[4];
            content.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++) corners[i] = camera.WorldToScreenPoint(corners[i]);
            return corners;
        }

        private static void AssertContentInside(Rect viewport, Vector3[] corners, string context)
        {
            const float tolerance = 2f;
            for (int i = 0; i < corners.Length; i++)
            {
                Assert.That(corners[i].x, Is.InRange(viewport.xMin - tolerance, viewport.xMax + tolerance),
                    context + ": content corner " + i + " extends beyond viewport horizontally.");
                Assert.That(corners[i].y, Is.InRange(viewport.yMin - tolerance, viewport.yMax + tolerance),
                    context + ": content corner " + i + " extends beyond viewport vertically.");
            }
        }
    }
}
