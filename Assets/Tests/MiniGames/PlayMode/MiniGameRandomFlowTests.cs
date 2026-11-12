using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GE.MiniGames.FlappyBird;
using GE.MiniGames.Snake;
using GE.MiniGames.Sorting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 MainGame의 목록과 실제 성공 이벤트를 사용합니다. 테스트가 선택 풀이나 씬 목록을 덮어쓰지 않습니다.
    // 기대 목록은 하드코딩하지 않고 Build Settings의 MiniGame## 씬에서 읽습니다. 미니게임이 늘어나도 테스트를 고칠 필요가 없게 합니다.
    // 가상 키보드(InputTestFixture)를 사용해 사용자의 실제 키 입력과 테스트를 분리합니다.
    public sealed class MiniGameRandomFlowTests : InputTestFixture
    {
        private static readonly Regex MiniGameScenePattern = new Regex(@"^MiniGame\d+$");

        private Keyboard keyboard;
        private MonoBehaviour manager;
        private Type managerType, buttonsType;
        private MethodInfo processFrame;
        private float originalTimeScale;
        private bool originalBackground;
        private UnityEngine.Random.State originalRandom;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
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

        public override void TearDown()
        {
            Time.timeScale = originalTimeScale;
            Application.runInBackground = originalBackground;
            UnityEngine.Random.state = originalRandom;
            base.TearDown();
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
            // 씬 목록이 늘어나도 남은 Additive 씬이 다음 테스트에 섞이지 않도록 로드된 MiniGame* 씬을 모두 내립니다.
            var leftovers = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && (scene.name == "Lobby" || scene.name == "MainGame" || scene.name.StartsWith("MiniGame")))
                    leftovers.Add(scene);
            }
            foreach (var scene in leftovers)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainGame_ActualEighteenClears_VisitsEveryRegisteredGameEachCycle()
        {
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return CheckFlow(18, "MainGame");
        }

        // Lobby 진입 경로에서는 선택 풀 한 바퀴(등록된 게임 수만큼)를 클리어해 모든 게임이 한 번씩 로드되는지 확인합니다.
        [UnityTest]
        public IEnumerator LobbyToMainGame_OneFullCycle_VisitsEveryRegisteredGame()
        {
            yield return SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Single);
            yield return null; yield return null;
            var menuType = Type.GetType("MenuManager, Assembly-CSharp", true);
            var menu = Object.FindFirstObjectByType(menuType);
            Assert.That(menu, Is.Not.Null);
            menuType.GetMethod("NextScene").Invoke(menu, null);
            yield return WaitFor(() => SceneManager.GetSceneByName("MainGame").isLoaded);
            yield return CheckFlow(0, "Lobby");
        }

        // rounds가 0이면 등록된 게임 수만큼(선택 풀 한 바퀴) 진행합니다.
        private IEnumerator CheckFlow(int rounds, string entry)
        {
            yield return null;
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            Assert.That(manager, Is.Not.Null);

            // 매니저의 저장된 목록이 Build Settings에 등록된 MiniGame## 씬과 정확히 일치해야 합니다.
            var registered = BuildMiniGameScenes();
            var configured = Field<List<string>>("miniGameSceneNames");
            Assert.That(registered, Is.Not.Empty, "Build Settings에 MiniGame## 씬이 없습니다.");
            Assert.That(configured, Is.Unique, "매니저 씬 목록에 중복 항목이 있습니다.");
            CollectionAssert.AreEquivalent(registered, configured, "MainGame 매니저 목록과 Build Settings의 미니게임 씬이 다릅니다.");
            foreach (string sceneName in configured)
                Assert.That(Application.CanStreamedLevelBeLoaded(sceneName), Is.True, sceneName + "을 로드할 수 없습니다.");
            int poolSize = configured.Count;
            if (rounds <= 0) rounds = poolSize;
            int totalToClear = Field<int>("totalMiniGamesToClear");
            Assert.That(rounds, Is.LessThanOrEqualTo(totalToClear));

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
                // 컨트롤러의 Update 대신 테스트가 프레임을 공급합니다. 공통 눌림 감지·규칙·결과 이벤트는 실제 경로를 사용합니다.
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
                else if (game is SnakeGame || game is FlappyGame)
                {
                    // 01·02는 프레임 입력/시간만 가속합니다. 모델·목표·충돌·성공 결과를 조작하거나 Clear를 직접 호출하지 않습니다.
                    game.StartGame();
                    Frame(game, 0);
                    if (game is SnakeGame snake) DriveSnake(snake);
                    else DriveFlappy((FlappyGame)game);
                }
                else
                {
                    // 04 이후 게임의 규칙 기반 성공은 각 <게임>SceneTests가 검증합니다. 여기서는 씬 흐름이 목적이므로
                    // 모든 게임이 구현한 Editor 전용 Z+X+C 클리어(DEV_CLEAR_CHEAT)를 가상 키보드로 눌러
                    // 같은 Finish → Completed → 매니저 경로로 성공을 보냅니다. 새 미니게임도 별도 드라이버 없이 이 경로를 탑니다.
                    yield return PressDevClear(game);
                }
                Assert.That(completion.HasValue, Is.True, $"{game.GameId} 결과가 전달되지 않았습니다.");
                Assert.That(completion.Value.Outcome, Is.EqualTo(MiniGameOutcome.Success), completion.Value.Reason);
                Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(round + 1));
                Assert.That(Field<int>("currentLives"), Is.EqualTo(originalLives));
                yield return WaitFor(() => !Field<bool>("isLoading"));
            }

            // 한 묶음(등록된 게임 수)에서는 중복 없이 모든 게임이 선택되어야 합니다. 재충전한 다음 묶음도 같은 조건입니다.
            // 목표 횟수가 풀 크기로 나누어떨어지지 않으면 마지막 부분 묶음은 중복이 없는지만 검사합니다.
            for (int start = 0; start < rounds; start += poolSize)
            {
                var chunk = sequence.Skip(start).Take(poolSize).ToList();
                if (chunk.Count == poolSize) CollectionAssert.AreEquivalent(configured, chunk, $"{start + 1}번째부터의 한 묶음");
                else Assert.That(chunk, Is.Unique, $"{start + 1}번째부터의 마지막 부분 묶음");
            }
            // 풀을 다시 채운 직후에도 방금 끝낸 씬을 연속으로 고르지 않아야 합니다(후보가 둘 이상일 때).
            if (poolSize > 1)
                for (int i = 1; i < sequence.Count; i++)
                    Assert.That(sequence[i], Is.Not.EqualTo(sequence[i - 1]), $"{i + 1}번째 선택이 직전 씬과 같습니다.");
            if (rounds == totalToClear) Assert.That(Field<string>("currentLoadedMiniGame"), Is.Empty);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Sorting-RandomFlow-" + entry + ".txt", string.Join(" -> ", sequence));
        }

        // Build Settings에 등록·활성화된 MiniGame## 씬 이름입니다. Lobby·MainGame과 테스트 씬은 제외합니다.
        private static List<string> BuildMiniGameScenes()
        {
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (MiniGameScenePattern.IsMatch(name)) names.Add(name);
            }
            return names;
        }

        // 가상 키보드로 Z+X+C를 새로 누른 프레임에 한 틱을 공급하고, 다음 게임의 연속 스킵을 막기 위해 바로 놓습니다.
        private IEnumerator PressDevClear(MiniGameController game)
        {
            // 치트 판정은 KeyboardInputEnabled일 때만 동작합니다. 실제 키보드는 InputTestFixture가 분리하므로 가상 장치만 읽힙니다.
            game.KeyboardInputEnabled = true;
            game.StartGame();
            Frame(game, 0); // 시작 직후 공통 컨트롤러가 건너뛰는 첫 틱을 소비합니다.
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing), game.GameId);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z, Key.X, Key.C));
            yield return null; // 다음 프레임의 입력 갱신에서 wasPressedThisFrame이 참이 됩니다.
            Frame(game, 0);
            game.KeyboardInputEnabled = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
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
