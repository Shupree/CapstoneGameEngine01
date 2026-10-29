#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Invaders;
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
    // MiniGame05와 같은 실제 씬·공통 UI·가상 키보드 검증입니다. 다른 게임이나 메인 시스템을 생성하지 않습니다.
    public sealed class InvadersSceneTests : InputTestFixture
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
            var scene = SceneManager.GetSceneByName("MiniGame06");
            var cleanup = SceneManager.GetSceneByName("Invaders test cleanup");
            if (!cleanup.IsValid()) cleanup = SceneManager.CreateScene("Invaders test cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame06.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }

        [UnityTest]
        public IEnumerator CommonUI_HeldMovementFirePauseAndFreshInputAfterResume()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<InvadersGame>();
            Assert.That(game.GetComponent<MiniGameTestUI>(), Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready)); Capture(game, "Ready", 900, 1000);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Model.PlayerShots, Is.Zero, "시작 Space는 발사로 재사용하지 않습니다.");
            yield return Keys(Key.W, Key.S); float x = game.Model.PlayerX;
            yield return new WaitForSeconds(.05f); Assert.That(game.Model.PlayerX, Is.EqualTo(x));
            yield return Keys(Key.D, Key.Space); yield return new WaitForSeconds(.15f);
            Assert.That(game.Model.PlayerX, Is.GreaterThan(x)); Assert.That(game.Model.PlayerShots, Is.EqualTo(1));
            yield return new WaitForSeconds(.3f); Assert.That(game.Model.PlayerShots, Is.EqualTo(2));
            yield return Keys(Key.Tab, Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            double time = game.Model.Elapsed; int shots = game.Model.PlayerShots; var positions = game.Model.Enemies.Select(e => e.Position).ToArray();
            Capture(game, "Paused", 1280, 720); yield return new WaitForSeconds(.15f);
            Assert.That(game.Model.Elapsed, Is.EqualTo(time)); Assert.That(game.Model.Enemies.Select(e => e.Position), Is.EqualTo(positions));
            yield return Keys(Key.Space); yield return Keys(Key.Tab, Key.Space); yield return Keys(Key.Space);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); yield return new WaitForSeconds(.45f);
            Assert.That(game.Model.PlayerShots, Is.EqualTo(shots), "재개 중 계속 누른 발사 키는 해제까지 차단됩니다.");
            yield return Keys(); yield return Keys(Key.Space); Assert.That(game.Model.PlayerShots, Is.EqualTo(shots + 1));
            Capture(game, "Playing", 1280, 960);
        }

        [UnityTest]
        public IEnumerator FailureAndRetry_UseCommonResultAndResetAllLocalState()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<InvadersGame>(); int results = 0;
            game.Completed += _ => results++; yield return Keys(Key.Space);
            typeof(InvadersModel).GetMethod("SpawnBullet", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game.Model, new object[] { false, new Vector2(game.Model.PlayerX, InvadersRules.PlayerY) });
            yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(game.ResultReason, Is.EqualTo("기체 격추"));
            Assert.That(game.Model.Lives, Is.Zero); Assert.That(results, Is.EqualTo(1)); Capture(game, "Failed", 900, 1000);
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            yield return Keys(); yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.RunId, Is.EqualTo(2)); Assert.That(game.Model.Lives, Is.EqualTo(1));
            Assert.That(game.Model.Shields.Count(s => s.Alive), Is.EqualTo(24)); Assert.That(game.Progress, Is.Zero);
            Assert.That(game.Model.PlayerShots, Is.Zero); Assert.That(game.Model.EnemyShots, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ZxcCheat_UsesSameSuccessEvent_AndDoesNotRepeatWhileHeld()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<InvadersGame>();
            int results = 0; MiniGameResult last = default; game.Completed += result => { results++; last = result; };
            yield return Keys(Key.Space); yield return Keys(Key.Z, Key.X, Key.C);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success));
            Assert.That(last.GameId, Is.EqualTo("MiniGame06")); Assert.That(results, Is.EqualTo(1)); Capture(game, "Cheat", 900, 1000);
            game.StartGame(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); yield return Keys(Key.Z, Key.X, Key.C); Assert.That(results, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AuthoredCameraCanvas_FitsPortraitLandscapeAndAdditiveViewport()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<InvadersGame>();
            game.KeyboardInputEnabled = false; game.enabled = false; game.StartGame();
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = canvas.worldCamera;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 600 x 480");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(800, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target; camera.rect = new Rect(.15f, .15f, .7f, .7f);
                yield return null; Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; field.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - .1f)); Assert.That(bottom.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - .1f));
                Assert.That(top.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax + .1f)); Assert.That(top.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax + .1f));
                Assert.That((top.x - bottom.x) / (top.y - bottom.y), Is.EqualTo(1.25f).Within(.001));
                camera.targetTexture = null; Object.Destroy(target);
            }
            camera.rect = new Rect(0, 0, 1, 1); Capture(game, "Wide", 1280, 720);
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true)) Assert.That(label.isTextOverflowing, Is.False, label.name);
        }

        private static void Capture(InvadersGame game, string label, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; game.GetComponent<InvadersView>().Refresh(); Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Invaders-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
