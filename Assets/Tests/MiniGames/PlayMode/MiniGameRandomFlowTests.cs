using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GE.MiniGames.FlappyBird;
using GE.MiniGames.Snake;
using GE.MiniGames.Sorting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 MainGame의 목록과 실제 성공 이벤트를 사용합니다. 테스트가 선택 풀이나 씬 목록을 덮어쓰지 않습니다.
    public sealed class MiniGameRandomFlowTests
    {
        private MonoBehaviour manager;
        private Type managerType, buttonsType;
        private MethodInfo processFrame;
        private float originalTimeScale;
        private bool originalBackground;
        private UnityEngine.Random.State originalRandom;

        [SetUp]
        public void Setup()
        {
            originalTimeScale = Time.timeScale;
            originalBackground = Application.runInBackground;
            originalRandom = UnityEngine.Random.state;
            Time.timeScale = 1;
            Application.runInBackground = true;
            UnityEngine.Random.InitState(303);
            managerType = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            processFrame = typeof(MiniGameController).GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            buttonsType = processFrame.GetParameters()[1].ParameterType;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            Application.runInBackground = originalBackground;
            UnityEngine.Random.state = originalRandom;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (manager != null)
            {
                yield return WaitFor(() => !Field<bool>("isLoading"));
                managerType.GetMethod("CloseMiniGameImmediately").Invoke(manager, null);
                yield return WaitFor(() => !Field<bool>("isLoading"));
                Object.Destroy(manager.gameObject);
            }
            var menuType = Type.GetType("MenuManager, Assembly-CSharp", true);
            var menu = Object.FindFirstObjectByType(menuType) as MonoBehaviour;
            if (menu != null) Object.Destroy(menu.gameObject);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Random flow cleanup"));
            foreach (string name in new[] { "Lobby", "MainGame", "MiniGame01", "MiniGame02", "MiniGame03" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainGame_ActualEighteenClears_VisitsAllThreeEachCycle()
        {
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return CheckFlow(18, "MainGame");
        }

        [UnityTest]
        public IEnumerator LobbyToMainGame_ActualThreeClears_IncludesSorting()
        {
            yield return SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Single);
            yield return null; yield return null;
            var menuType = Type.GetType("MenuManager, Assembly-CSharp", true);
            var menu = Object.FindFirstObjectByType(menuType);
            Assert.That(menu, Is.Not.Null);
            menuType.GetMethod("NextScene").Invoke(menu, null);
            yield return WaitFor(() => SceneManager.GetSceneByName("MainGame").isLoaded);
            yield return CheckFlow(3, "Lobby");
        }

        private IEnumerator CheckFlow(int rounds, string entry)
        {
            yield return null;
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            Assert.That(manager, Is.Not.Null);
            CollectionAssert.AreEquivalent(new[] { "MiniGame01", "MiniGame02", "MiniGame03" }, Field<List<string>>("miniGameSceneNames"));
            Assert.That(Application.CanStreamedLevelBeLoaded("MiniGame03"), Is.True);
            int originalLives = Field<int>("currentLives");
            var sequence = new List<string>();
            for (int round = 0; round < rounds; round++)
            {
                yield return WaitFor(() => !Field<bool>("isLoading") && Field<MiniGameController>("currentMiniGameController") != null);
                var game = Field<MiniGameController>("currentMiniGameController");
                Assert.That(game.gameObject.scene.name, Is.EqualTo(game.GameId));
                Assert.That(game.ExternalPauseControl, Is.True);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
                sequence.Add(game.GameId);
                Debug.Log($"[RandomFlow/{entry}] round={round + 1}, scene={game.GameId}, pool={string.Join(",", Field<List<string>>("remainingMiniGames"))}");
                MiniGameResult? completion = null;
                game.Completed += result => completion = result;
                // 사용자 키보드와 테스트를 분리하되 공통 눌림 감지·규칙·결과 이벤트는 실제 경로를 사용합니다.
                game.KeyboardInputEnabled = false;
                game.enabled = false;
                if (game is SortingGame sorting)
                {
                    // 입력 장치 자체의 검증은 SortingSceneTests가 담당하고, 여기서는 연속 씬 진행을 검증합니다.
                    CaptureSorting(sorting, entry);
                    game.StartGame();
                    Frame(game, 0);
                    Assert.That(sorting.State, Is.EqualTo(MiniGameState.Playing));
                    Assert.That(sorting.Progress, Is.Zero);
                    for (int i = 0; i < 30; i++)
                    {
                        Frame(game, 0);
                        var choice = SortingModel.RequiredChoice(sorting.Model.Current);
                        Frame(game, 0, choice == SortingChoice.Left ? 2 : choice == SortingChoice.Right ? 8 : 16);
                    }
                }
                else
                {
                    // 01·02는 프레임 입력/시간만 가속합니다. 모델·목표·충돌·성공 결과를 조작하거나 Clear를 직접 호출하지 않습니다.
                    game.StartGame();
                    Frame(game, 0);
                    if (game is SnakeGame snake) DriveSnake(snake);
                    else if (game is FlappyGame flappy) DriveFlappy(flappy);
                    else Assert.Fail("등록되지 않은 게임 타입입니다: " + game.GetType().Name);
                }
                Assert.That(completion.HasValue, Is.True, $"{game.GameId} 결과가 전달되지 않았습니다.");
                Assert.That(completion.Value.Outcome, Is.EqualTo(MiniGameOutcome.Success), completion.Value.Reason);
                Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(round + 1));
                Assert.That(Field<int>("currentLives"), Is.EqualTo(originalLives));
                yield return WaitFor(() => !Field<bool>("isLoading"));
            }
            // 한 묶음에서는 중복 없이 세 게임 모두 선택되어야 합니다. 재충전한 다음 묶음도 같은 조건입니다.
            for (int start = 0; start < rounds; start += 3)
                CollectionAssert.AreEquivalent(new[] { "MiniGame01", "MiniGame02", "MiniGame03" }, sequence.Skip(start).Take(3));
            if (rounds == 18) Assert.That(Field<string>("currentLoadedMiniGame"), Is.Empty);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Sorting-RandomFlow-" + entry + ".txt", string.Join(" -> ", sequence));
        }

        private void DriveSnake(SnakeGame game)
        {
            // 10×10 보드의 순환 경로를 따라 실제 사과를 먹습니다. 사과 위치나 점수를 강제로 바꾸지 않습니다.
            for (int i = 0; i < 1000 && game.State == MiniGameState.Playing; i++)
            {
                var head = game.Model.Body[0];
                int direction = head.x == 0 ? (head.y < 9 ? 1 : 8)
                    : head.y == 0 ? 2 : (head.y & 1) == 1 ? (head.x < 9 ? 8 : 4) : (head.x > 1 ? 2 : 4);
                Frame(game, 0.1f, direction);
                Frame(game, 0.1f);
                Frame(game, 0.05f);
            }
        }

        private void DriveFlappy(FlappyGame game)
        {
            // 다음 파이프의 통로를 추적하는 테스트 조작입니다. 실제 중력과 파이프 충돌을 그대로 거칩니다.
            bool previousFlap = false;
            for (int i = 0; i < 12000 && game.State == MiniGameState.Playing; i++)
            {
                var next = game.Model.Pipes.FirstOrDefault(pipe => !pipe.Counted);
                float target = next?.GapY ?? 0;
                bool flap = !previousFlap && game.Model.Velocity <= 0 && game.Model.BirdY < target - 0.35f;
                Frame(game, 1f / 120f, flap ? 16 : 0);
                previousFlap = flap;
            }
        }

        private void Frame(MiniGameController game, float delta, int buttons = 0)
        {
            processFrame.Invoke(game, new[] { (object)delta, Enum.ToObject(buttonsType, buttons) });
        }
        private T Field<T>(string name) => (T)managerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "MainGame 씬 전환이 제한 시간 안에 완료되지 않았습니다.");
        }

        // 메인이 지정한 카메라 영역에서 MiniGame03이 실제 렌더되는지 캡처합니다. 저장된 씬은 변경하지 않습니다.
        private static void CaptureSorting(SortingGame game, string entry)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GetComponentInChildren<Camera>();
            Assert.That(camera.rect, Is.EqualTo(new Rect(0.15f, 0.15f, 0.7f, 0.7f)));
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                game.GetComponent<SortingView>().Refresh();
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/Sorting-RandomFlow-" + entry + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.Destroy(target); Object.Destroy(pixels);
            }
        }
    }
}
