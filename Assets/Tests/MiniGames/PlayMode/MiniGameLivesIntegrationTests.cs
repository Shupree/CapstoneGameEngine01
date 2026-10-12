using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 실제 MainGame·스네이크 결과와 저장된 클릭 UI로 전체 실행의 목숨 및 재시작 경계를 검증합니다.
    public sealed class MiniGameLivesIntegrationTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Type managerType;
        private MonoBehaviour manager;
        private SnakeGame snake;
        private MethodInfo processFrame;
        private Type buttonsType;
        private MiniGameResult? latestResult;
        private float originalTimeScale;
        private readonly Dictionary<InputActionAsset, InputDevice[]> originalActionDevices = new Dictionary<InputActionAsset, InputDevice[]>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            // 씬이 공유하는 UI 액션 에셋도 새 테스트 장치에 바인딩합니다. 이전 테스트의 장치 인덱스를 재사용하지 않습니다.
            originalActionDevices.Clear();
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                originalActionDevices[asset] = asset.devices.HasValue ? asset.devices.Value.ToArray() : null;
                asset.Disable();
                asset.devices = new InputDevice[] { keyboard };
            }
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        public override void TearDown()
        {
            Time.timeScale = originalTimeScale;
            base.TearDown();
            // 실제 입력 시스템을 복원한 뒤 에셋의 원래 장치 필터도 돌려놓습니다. 씬 파일은 변경하지 않습니다.
            foreach (var entry in originalActionDevices)
            {
                if (entry.Key == null) continue;
                entry.Key.Disable();
                entry.Key.devices = entry.Value;
            }
            originalActionDevices.Clear();
        }

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            managerType = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            processFrame = typeof(MiniGameController).GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            buttonsType = processFrame.GetParameters()[1].ParameterType;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            Assert.That(manager, Is.Not.Null);
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") != string.Empty);
            yield return EnsureSnake();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(3));
            Assert.That(Field<bool>("isGameOver"), Is.False);
        }

        [UnityTearDown]
        public IEnumerator UnloadHost()
        {
            // 재시작 후 생성된 새 매니저도 정리해 시간 정지와 싱글톤이 다음 테스트에 남지 않게 합니다.
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            if (manager != null)
            {
                yield return WaitFor(() => !Field<bool>("isLoading"));
                Call("CloseMiniGameImmediately");
                yield return WaitFor(() => !Field<bool>("isLoading"));
                Object.Destroy(manager.gameObject);
            }
            var menuType = Type.GetType("MenuManager, Assembly-CSharp");
            var menu = menuType == null ? null : Object.FindFirstObjectByType(menuType) as MonoBehaviour;
            if (menu != null) Object.Destroy(menu.gameObject);
            foreach (string sceneName in new[] { "MiniGame01", "MiniGame02", "MainGame" })
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Lives integration cleanup");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Failure_CostsOneLife_DuplicateAndStaleResultsAreIgnored_SpaceRetries()
        {
            snake.StartGame();
            FailRun();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(2));
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Failed));
            Assert.That(Field<string>("currentLoadedMiniGame"), Is.EqualTo("MiniGame01"));
            Assert.That(Field<GameObject>("gameOverOverlay").activeSelf, Is.False);
            var oldFailure = latestResult.Value;
            var handler = Field<Action<MiniGameResult>>("currentResultHandler");
            handler(oldFailure);
            Call("OnMiniGameFailed");
            Assert.That(Field<int>("currentLives"), Is.EqualTo(2), "동일 실행의 실패는 한 번만 차감합니다.");

            // 기존 Space 재시도가 다음 실행 번호를 만들며, 전체 게임의 남은 목숨은 초기화하지 않습니다.
            int failedRun = snake.RunId;
            snake.KeyboardInputEnabled = true;
            yield return Keys();
            yield return Keys(Key.Space);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(snake.RunId, Is.EqualTo(failedRun + 1));
            handler(oldFailure);
            Assert.That(Field<int>("currentLives"), Is.EqualTo(2), "이전 재시도의 늦은 결과는 무시합니다.");
            FailRun();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(1));
            Assert.That(Field<bool>("isGameOver"), Is.False);
        }

        [UnityTest]
        public IEnumerator ThirdFailure_ShowsFullScreenGameOver_UIClickRecreatesMainGame()
        {
            // 단순 변수 리셋이 아니라 MainGame의 모든 일반 씬 오브젝트가 재생성되는지 확인합니다.
            var marker = new GameObject("MainGame run marker");
            SceneManager.MoveGameObjectToScene(marker, SceneManager.GetSceneByName("MainGame"));
            managerType.GetField("currentClearedCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, 5);
            for (int i = 0; i < 3; i++) { snake.StartGame(); FailRun(); }
            Assert.That(Field<int>("currentLives"), Is.Zero);
            Assert.That(Field<bool>("isGameOver"), Is.True);
            Assert.That(Field<object>("gameOverReason").ToString(), Is.EqualTo("LivesDepleted"));
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Stopped));
            var overlay = Field<GameObject>("gameOverOverlay");
            var button = Field<UnityEngine.UI.Button>("gameOverRetryButton");
            Assert.That(overlay.activeInHierarchy, Is.True);
            Assert.That(overlay.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(Color.black));
            Assert.That(overlay.GetComponent<RectTransform>().anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.GetComponent<RectTransform>().anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(button.GetComponent<TMP_Text>().text, Is.EqualTo("Retry?"));
            Assert.That(button.GetComponent<UnityEngine.UI.Image>(), Is.Null, "Retry? 버튼에는 별도 Image를 만들지 않습니다.");
            Assert.That(Time.timeScale, Is.Zero);
            snake.StartGame();
            // 외부에서 로컬 재시도를 시도해도 매니저가 추가 결과나 다음 씬을 처리해서는 안 됩니다.
            Call("OnMiniGameFailed");
            Call("OnMiniGameCleared");
            Assert.That(Field<int>("currentLives"), Is.Zero);
            Assert.That(Field<int>("currentClearedCount"), Is.EqualTo(5));

            // 활성화 프레임 뒤 실제 Canvas 렌더 순서가 갱신되어야 GraphicRaycaster의 depth가 유효해집니다.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var raycasts = new List<RaycastResult>();
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = center }, raycasts);
            Assert.That(raycasts.Count, Is.GreaterThan(0));
            Assert.That(raycasts[0].gameObject, Is.SameAs(button.gameObject), "글자가 전체 화면 Image보다 먼저 클릭되어야 합니다.");
            var oldManager = manager;
            var oldSnake = snake;
            // 포인터 클릭 이벤트를 실제 Text의 Button에 전달해 저장된 UnityEvent 연결을 검증합니다.
            // 입력 장치의 공유 에셋과 무관하게 0배 시간에서 클릭 콜백과 전체 씬 재생성이 동작해야 합니다.
            Assert.That(ExecuteEvents.Execute(button.gameObject,
                new PointerEventData(eventSystem) { position = center, button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler), Is.True);
            yield return WaitForNewHost(oldManager);
            Assert.That(marker == null, Is.True, "기존 MainGame의 일반 오브젝트가 제거되어야 합니다.");
            Assert.That(oldSnake == null, Is.True, "기존 Additive 씬도 제거되어야 합니다.");
            AssertFreshHost();
        }

        [UnityTest]
        public IEnumerator MotherCaughtDuringLoading_UsesSameGameOver_AndRetryWaitsForLoading()
        {
            Call("LoadRandomMiniGame");
            Assert.That(Field<bool>("isLoading"), Is.True);
            TriggerMotherGameOver();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(3), "엄마에게 발각되는 패배는 목숨 차감과 독립적입니다.");
            Assert.That(Field<object>("gameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            TriggerMotherGameOver();
            var oldManager = manager;
            Call("RestartMainGame");
            Call("RestartMainGame");
            yield return WaitForNewHost(oldManager);
            AssertFreshHost();
        }

        [UnityTest]
        public IEnumerator SwitchingMiniGames_DoesNotRestoreSpentLives()
        {
            snake.StartGame();
            FailRun();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(2));
            Call("LoadRandomMiniGame");
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame02");
            Assert.That(Field<int>("currentLives"), Is.EqualTo(2));
            Assert.That(Array.FindAll(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None),
                e => e.enabled).Length, Is.EqualTo(1), "미완성 씬에도 MainGame의 입력 시스템 하나만 활성화되어야 합니다.");
            yield return EnsureSnake();
            snake.StartGame();
            FailRun();
            Assert.That(Field<int>("currentLives"), Is.EqualTo(1));
        }

        // 실제 벽 충돌 규칙을 실행해 정상 실패 결과를 발생시킵니다. 매니저 메서드만 호출하는 모의 실패는 사용하지 않습니다.
        private void FailRun()
        {
            snake.KeyboardInputEnabled = false;
            latestResult = null;
            for (int i = 0; i < 60 && snake.State == MiniGameState.Playing; i++)
                processFrame.Invoke(snake, new[] { (object)0.1f, Enum.ToObject(buttonsType, 0) });
            Assert.That(latestResult.HasValue, Is.True);
            Assert.That(latestResult.Value.Outcome, Is.EqualTo(MiniGameOutcome.Failure));
        }

        private IEnumerator EnsureSnake()
        {
            if (Field<string>("currentLoadedMiniGame") != "MiniGame01")
            {
                Call("LoadRandomMiniGame");
                yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame01");
            }
            snake = Object.FindFirstObjectByType<SnakeGame>();
            Assert.That(snake, Is.Not.Null);
            snake.KeyboardInputEnabled = false;
            snake.Completed += result => latestResult = result;
        }

        // 메인 어셈블리의 enum 값을 반사로 전달해 미니게임 테스트 어셈블리와 기존 메인 구조를 분리합니다.
        private void TriggerMotherGameOver()
        {
            var method = managerType.GetMethod("TriggerGameOver");
            method.Invoke(manager, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "CaughtByMother") });
        }

        private IEnumerator WaitForNewHost(MonoBehaviour oldManager)
        {
            yield return WaitFor(() =>
            {
                var candidate = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
                if (candidate == null || candidate == oldManager) return false;
                manager = candidate;
                return !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") != string.Empty;
            });
        }

        private void AssertFreshHost()
        {
            Assert.That(Field<int>("currentLives"), Is.EqualTo(3));
            Assert.That(Field<int>("currentClearedCount"), Is.Zero);
            Assert.That(Field<bool>("isGameOver"), Is.False);
            Assert.That(Field<GameObject>("gameOverOverlay").activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Object.FindObjectsByType(managerType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private IEnumerator Keys(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private T Field<T>(string name) => (T)managerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        private void Call(string name) => managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(manager, null);

        // 로드 실패 시 무한 대기하지 않고 실제 시간을 기준으로 제한해 원인을 드러냅니다.
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "MainGame의 씬 전환 시간이 초과되었습니다.");
        }
    }
}
