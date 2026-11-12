#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Baseball;
using GE.MiniGames.Fishing;
using GE.MiniGames.Gust;
using GE.MiniGames.Tomato;
using GE.MiniGames.DeadPixel;
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
    public sealed class RemainingHtmlSceneTests : InputTestFixture
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
            if(game is BaseballGame baseball) return baseball.Model.BallX;
            if(game is FishingGame fishing) return fishing.Model.TimeLeft;
            if(game is GustGame gust) return gust.Model.TimeLeft;
            if(game is TomatoGame tomato) return tomato.Model.BasketX;
            return ((DeadPixelGame)game).Model.TimeLeft;
        }
        [UnityTest] public IEnumerator Baseball_CommonLifecycleAndViewport() { yield return Common(13); }
        [UnityTest] public IEnumerator Fishing_CommonLifecycleAndViewport() { yield return Common(11); }
        [UnityTest] public IEnumerator Gust_CommonLifecycleAndViewport() { yield return Common(16); }
        [UnityTest] public IEnumerator Tomato_CommonLifecycleAndViewport() { yield return Common(15); }
        [UnityTest] public IEnumerator DeadPixel_CommonLifecycleAndViewport() { yield return Common(18); }
        [UnityTest]
        public IEnumerator Baseball_SevenNewSpacePressesWin_HoldDoesNotSwingAgain()
        {
            yield return Load(13); var game=Object.FindFirstObjectByType<BaseballGame>(); yield return Keys(Key.R); yield return Keys();
            int results=0; game.Completed += _=>results++;
            for(int i=1;i<=7;i++)
            {
                yield return Keys(); Set(game.Model,"BallX",80d); Set(game.Model,"Active",true);
                yield return Keys(Key.Space); Assert.That(game.Model.Hits,Is.EqualTo(i));
                yield return Keys(Key.Space); Assert.That(game.Model.Hits,Is.EqualTo(i));
            }
            Assert.That(game.State,Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator FishingAndGust_SurvivalCompletionUsesCommonResult()
        {
            foreach(int number in new[]{11,16})
            {
                yield return Load(number); var game=Object.FindFirstObjectByType<MiniGameController>();
                yield return Keys(Key.R); yield return Keys(); int results=0; game.Completed += _=>results++;
                if(game is FishingGame fishing)
                {
                    Set(fishing.Model,"Progress",99.9d); Set(fishing.Model,"FishY",150d);
                    Field(fishing.Model,"target",150d); Field(fishing.Model,"moveTimer",100d);
                }
                else { Field(((GustGame)game).Model,"ticks",719); Set(((GustGame)game).Model,"PlayerX",200d); }
                yield return new WaitForSeconds(.1f);
                Assert.That(game.State,Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results,Is.EqualTo(1));
            }
        }
        // 생성은 모델에서 담당하므로 테스트에서는 마지막 충돌 직전의 아이템만 주입합니다.
        private static void AddItem(TomatoModel model,bool rock)
        {
            var list=(System.Collections.Generic.List<FallingItem>)typeof(TomatoModel).GetField("items",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(model);
            var item=new FallingItem(); Set(item,"X",200d); Set(item,"Y",248d); Set(item,"Speed",400d); Set(item,"Rock",rock); list.Add(item);
        }
        [UnityTest]
        public IEnumerator Tomato_SevenCatchesCompleteOnce()
        {
            yield return Load(15); var game=Object.FindFirstObjectByType<TomatoGame>(); yield return Keys(Key.R); yield return Keys();
            Field(game.Model,"spawnTimer",100d); int results=0; game.Completed += _=>results++;
            for(int i=1;i<=7;i++) { AddItem(game.Model,false); yield return new WaitForSeconds(.04f); Assert.That(game.Model.Catches,Is.EqualTo(i)); }
            Assert.That(game.State,Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DeadPixel_NewPressMovesOnce_SelectionResetsAndFifteenWin()
        {
            yield return Load(18); var game=Object.FindFirstObjectByType<DeadPixelGame>(); yield return Keys(Key.R); yield return Keys();
            yield return Keys(Key.A); Assert.That(game.Model.CursorX,Is.Zero); yield return Keys(Key.A); Assert.That(game.Model.CursorX,Is.Zero);
            yield return Keys(); yield return Keys(Key.D); Assert.That(game.Model.CursorX,Is.EqualTo(1));
            int results=0; game.Completed += _=>results++;
            for(int i=1;i<=15;i++)
            {
                yield return Keys(); Set(game.Model,"TargetIndex",4); yield return Keys(Key.Space); Assert.That(game.Model.Score,Is.EqualTo(i));
                yield return Keys(Key.Space); Assert.That(game.Model.Score,Is.EqualTo(i)); Assert.That(game.Model.CursorX,Is.EqualTo(1));
            }
            Assert.That(game.State,Is.EqualTo(MiniGameState.Succeeded)); Assert.That(results,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator AllFive_RealFailureAndRRetryUseCommonContract()
        {
            foreach(int number in new[]{13,11,16,15,18})
            {
                yield return Load(number); var game=Object.FindFirstObjectByType<MiniGameController>(); yield return Keys(Key.R); yield return Keys();
                int results=0; game.Completed += _=>results++;
                if(game is BaseballGame baseball) { Set(baseball.Model,"Active",false); yield return Keys(Key.Space); }
                else if(game is FishingGame fishing) { Field(fishing.Model,"ticks",1199); }
                else if(game is GustGame gust) { Set(gust.Model,"PlayerX",14d); }
                else if(game is TomatoGame tomato) { AddItem(tomato.Model,true); }
                else { Set(((DeadPixelGame)game).Model,"TargetIndex",0); yield return Keys(Key.Space); }
                yield return new WaitForSeconds(.1f); Assert.That(game.State,Is.EqualTo(MiniGameState.Failed)); Assert.That(results,Is.EqualTo(1)); Capture(game,"Failed");
                yield return Keys(); yield return Keys(Key.R); Assert.That(game.State,Is.EqualTo(MiniGameState.Playing)); Assert.That(game.RunId,Is.EqualTo(2));
            }
        }
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
