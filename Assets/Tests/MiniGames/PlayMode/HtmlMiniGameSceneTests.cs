#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Rope;
using GE.MiniGames.Fly;
using GE.MiniGames.SafeZone;
using GE.MiniGames.Stack;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 실제 씬과 가상 키보드로 공통 수명·입력 연결을 확인합니다. 경계 상태 주입은 테스트에서만 수행합니다.
    public sealed class HtmlMiniGameSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private bool previousBackground;
        public override void Setup()
        { base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); previousBackground = Application.runInBackground; Application.runInBackground = true; }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetActiveScene(); var empty = SceneManager.CreateScene("HTML test cleanup");
            SceneManager.SetActiveScene(empty); if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private IEnumerator Load(int number)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame" + number + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }
        private static void Set(object model, string name, object value) => model.GetType().GetProperty(name).SetValue(model, value);
        private static void Field(object model, string name, object value) => model.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, value);
        private static object Snapshot(MiniGameController game)
        {
            if (game is RopeGame rope) return rope.Model.TimeLeft;
            if (game is FlyGame fly) return fly.Model.Position;
            if (game is SafeZoneGame safe) return safe.Model.TimeLeft;
            return ((StackGame)game).Model.Current;
        }
        [UnityTest] public IEnumerator Rope_CommonLifecycleAndViewport() { yield return Common(10); }
        [UnityTest] public IEnumerator Fly_CommonLifecycleAndViewport() { yield return Common(17); }
        [UnityTest] public IEnumerator SafeZone_CommonLifecycleAndViewport() { yield return Common(12); }
        [UnityTest] public IEnumerator Stack_CommonLifecycleAndViewport() { yield return Common(14); }
        private IEnumerator Common(int number)
        {
            yield return Load(number); var game = Object.FindFirstObjectByType<MiniGameController>();
            Assert.That(game.GameId, Is.EqualTo("MiniGame" + number)); Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Capture(game, "Ready");
            yield return Keys(Key.R); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); Capture(game, "Playing");
            yield return Keys(Key.Tab); Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            object snapshot = Snapshot(game); yield return Keys(Key.A, Key.Space); yield return new WaitForSeconds(.1f);
            Assert.That(Snapshot(game), Is.EqualTo(snapshot));
            yield return Keys(Key.Tab, Key.A, Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys(); int results = 0; game.Completed += _ => results++;
            yield return Keys(Key.Z, Key.X, Key.C); Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded));
            yield return Keys(Key.Z, Key.X, Key.C); Assert.That(results, Is.EqualTo(1)); Capture(game, "Success");
            yield return Keys(); yield return Keys(Key.R); Assert.That(game.RunId, Is.EqualTo(2)); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            game.PauseGame();
            var canvas = game.GetComponentInChildren<Canvas>(); Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            var camera = canvas.worldCamera; var field = game.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Playfield 400 x 300");
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
            foreach (var label in game.GetComponentsInChildren<TMP_Text>(true))
            { label.ForceMeshUpdate(true); Assert.That(label.isTextOverflowing, Is.False, label.name); }
        }
        [UnityTest]
        public IEnumerator RopeAndSafeZone_ActualFailuresReportOnceAndRetry()
        {
            foreach (int number in new[] { 10, 12 })
            {
                yield return Load(number); var game = Object.FindFirstObjectByType<MiniGameController>();
                int results = 0; game.Completed += _ => results++;
                yield return Keys(Key.R); yield return Keys();
                if (game is RopeGame rope) Set(rope.Model, "BallPosition", 110d);
                else
                {
                    var model = ((SafeZoneGame)game).Model;
                    var attacks = (System.Collections.Generic.List<ZoneAttack>)typeof(SafeZoneModel).GetField("attacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
                    var attack = new ZoneAttack(); Set(attack, "Bounds", new Rect(160, 0, 80, 300)); Set(attack, "Striking", true); attacks.Add(attack);
                }
                yield return new WaitForSeconds(.1f);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(results, Is.EqualTo(1)); Capture(game, "Failed");
                yield return Keys(Key.R); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing)); Assert.That(game.RunId, Is.EqualTo(2));
            }
        }
        [UnityTest]
        public IEnumerator Fly_SpaceHitsOncePerPressAndFiveHitsSucceed()
        {
            yield return Load(17); var game = Object.FindFirstObjectByType<FlyGame>();
            yield return Keys(Key.R); yield return Keys(); int results = 0; game.Completed += _ => results++;
            for (int i = 1; i <= 5; i++)
            {
                yield return Keys(); Set(game.Model, "Position", game.Model.Swatter);
                yield return Keys(Key.Space); Assert.That(game.Model.Hits, Is.EqualTo(i));
                Set(game.Model, "Position", game.Model.Swatter); yield return Keys(Key.Space); Assert.That(game.Model.Hits, Is.EqualTo(i));
            }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator Stack_HeldAndSimultaneousKeysDoNotDropTwice_ThenSevenWin()
        {
            yield return Load(14); var game = Object.FindFirstObjectByType<StackGame>();
            yield return Keys(Key.R); yield return Keys(); int results = 0; game.Completed += _ => results++;
            for (int i = 1; i <= 7; i++)
            {
                yield return Keys(); var top = game.Model.Blocks.Last(); Set(game.Model, "Current", new StackBlock(top.X, top.Y - 25, top.Width));
                yield return Keys(Key.W, Key.A, Key.Space); Assert.That(game.Model.Count, Is.EqualTo(i));
                yield return Keys(Key.W, Key.A, Key.Space); Assert.That(game.Model.Count, Is.EqualTo(i));
            }
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results, Is.EqualTo(1)); Capture(game, "FullTower");
        }
        // 실제 URP 카메라의 정적 화면을 저장해 한글·배치·공통 오버레이를 확인합니다.
        private static void Capture(MiniGameController game, string label)
        {
            var camera = game.GetComponentInChildren<Camera>(); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(900, 1000, 24); var pixels = new Texture2D(900, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + game.GameId + "-" + label + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
