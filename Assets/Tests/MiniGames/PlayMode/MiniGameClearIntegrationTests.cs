using System;
using System.Collections;
using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // The legacy host is in Assembly-CSharp. Reflection keeps these tests from
    // moving the other developer's manager/common scripts into a new assembly.
    public sealed class MiniGameClearIntegrationTests
    {
        private Type managerType;
        private MonoBehaviour manager;
        private SnakeGame snake;
        private MethodInfo frame;
        private Type buttonsType;
        private float originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            // 메인 개발자가 변경한 실제 매니저 타입 이름으로 기존 클리어 연결을 검증합니다.
            managerType = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            frame = typeof(MiniGameController).GetMethod("ProcessFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            buttonsType = frame.GetParameters()[1].ParameterType;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            Assert.That(manager, Is.Not.Null, "MainGame must contain the existing mini-game manager.");
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") != string.Empty);
            MainGameTestScenes.UseSnakeAndSecondGame(manager);
            if (Field<string>("currentLoadedMiniGame") != "MiniGame01")
            {
                // 제품의 선택 목록은 늘어날 수 있으므로 테스트 인스턴스의 다음 씬만 지정합니다.
                MainGameTestScenes.Load(manager, "MiniGame01");
                yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame01");
            }
            snake = Object.FindFirstObjectByType<SnakeGame>();
            Assert.That(snake, Is.Not.Null);
            Assert.That(Field<MiniGameController>("currentMiniGameController"), Is.SameAs(snake));
            snake.KeyboardInputEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                yield return WaitFor(() => !Field<bool>("isLoading"));
                Call("CloseMiniGameImmediately");
                yield return WaitFor(() => !Field<bool>("isLoading"));
                Object.Destroy(manager.gameObject);
            }
            // PageBase creates the legacy MenuManager; remove only the test host's singleton.
            Type menuType = Type.GetType("MenuManager, Assembly-CSharp");
            var menu = menuType == null ? null : Object.FindFirstObjectByType(menuType) as MonoBehaviour;
            if (menu != null) Object.Destroy(menu.gameObject);
            foreach (string sceneName in new[] { "MiniGame01", "MiniGame02", "MainGame" })
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Clear integration cleanup");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnakeSuccess_CallsLegacyClearOnce_LoadsOtherScene_RejectsOldCallback()
        {
            int before = Field<int>("currentClearedCount");
            var oldHandler = Field<Action<MiniGameResult>>("currentResultHandler");
            snake.StartGame();
            int run = snake.RunId;
            // A forged/stale payload cannot clear a still-playing game.
            oldHandler(new MiniGameResult("MiniGame01", run, MiniGameOutcome.Success, 5, 5, "test"));
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before));

            DriveSnakeToSuccess();
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before + 1));
            Call("OnMiniGameCleared"); // Same frame duplicate must not count or load twice.
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before + 1));
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame02");
            Assert.That(SceneManager.GetSceneByName("MiniGame01").isLoaded, Is.False);
            // MiniGame02에도 공통 컨트롤러가 연결된 현재 씬을 검증합니다. 과거의 빈 씬 가정을 사용하지 않습니다.
            Assert.That(Field<MiniGameController>("currentMiniGameController"), Is.Not.Null);
            Assert.That(Field<MiniGameController>("currentMiniGameController").GameId, Is.EqualTo("MiniGame02"));
            oldHandler(new MiniGameResult("MiniGame01", run, MiniGameOutcome.Success, 5, 5, "late"));
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before + 1));

            // Legacy callers remain compatible; a replenished pool picks a different scene.
            ((System.Collections.IList)Field<object>("remainingMiniGames")).Clear();
            Call("OnMiniGameCleared");
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame01");
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before + 2));
            Assert.That(Object.FindFirstObjectByType<SnakeGame>().State, Is.EqualTo(MiniGameState.Ready));
        }

        [UnityTest]
        public IEnumerator SnakeFailure_StaysInScene_AndRetrySuccessAdvances()
        {
            int before = Field<int>("currentClearedCount");
            int livesBefore = Field<int>("currentLives");
            snake.StartGame();
            Frame(0f);
            for (int i = 0; i < 40 && snake.State == MiniGameState.Playing; i++) Frame(0.1f);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Failed));
            // 실패는 클리어 수를 늘리지 않으며, 같은 게임 재시도에서도 차감된 목숨을 유지합니다.
            Assert.That(Field<int>("currentLives"), Is.EqualTo(livesBefore - 1));
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before));
            Assert.That(Field<string>("currentLoadedMiniGame"), Is.EqualTo("MiniGame01"));
            Assert.That(Field<bool>("isLoading"), Is.False);
            int previousRun = snake.RunId;
            snake.StartGame();
            Assert.That(snake.RunId, Is.EqualTo(previousRun + 1));
            DriveSnakeToSuccess();
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame02");
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(before + 1));
            Assert.That(Field<int>("currentLives"), Is.EqualTo(livesBefore - 1));
        }

        [UnityTest]
        public IEnumerator LastRequiredClear_ClosesGameWithoutLoadingAnother()
        {
            int target = Field<int>("currentClearedCount") + 1;
            managerType.GetField("totalMiniGamesToClear", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, target);
            snake.StartGame();
            DriveSnakeToSuccess();
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == string.Empty);
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(target));
            Assert.That(SceneManager.GetSceneByName("MiniGame01").isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneByName("MiniGame02").isLoaded, Is.False);
            Call("OnMiniGameCleared");
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(target));
            Assert.That(Field<MiniGameController>("currentMiniGameController"), Is.Null);
            Assert.That(Field<Action<MiniGameResult>>("currentResultHandler"), Is.Null);
        }

        private void DriveSnakeToSuccess()
        {
            MiniGameResult? completion = null;
            snake.Completed += result => completion = result;
            Frame(0f);
            for (int i = 0; i < 600 && snake.State == MiniGameState.Playing; i++)
            {
                Vector2Int head = snake.Model.Body[0];
                int direction;
                if (head.x == 0) direction = head.y < 9 ? 1 : 8;
                else if (head.y == 0) direction = 2;
                else if ((head.y & 1) == 1) direction = head.x < 9 ? 8 : 4;
                else direction = head.x > 1 ? 2 : 4;
                Frame(0.1f, direction);
                Frame(0.1f);
                Frame(0.05f);
            }
            Assert.That(completion.HasValue, Is.True, "Reach five real apples through production movement rules.");
            Assert.That(completion.Value.Outcome, Is.EqualTo(MiniGameOutcome.Success));
            Assert.That(completion.Value.Progress, Is.EqualTo(5));
        }

        private void Frame(float seconds, int held = 0) => frame.Invoke(snake, new[] { (object)seconds, Enum.ToObject(buttonsType, held) });
        private T Field<T>(string name) => (T)managerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        private void Call(string name) => managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(manager, null);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Mini-game scene transition timed out.");
        }
    }
}
