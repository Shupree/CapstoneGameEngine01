using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 버튼의 클릭 연결과 실제 Additive 전환을 검증합니다. 메인 타입은 기존 어셈블리에 유지합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameDeveloperToolsTests
    {
        private MonoBehaviour manager, director, visit, tools;
        private Keyboard keyboard;
        private Mouse mouse;
        private AudioClip shortcutTestClip;
        private float originalTimeScale;
        private bool originalRunInBackground;
        private InputSettings.BackgroundBehavior originalBackgroundBehavior;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
#endif
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();

        [SetUp]
        public void Setup()
        {
            originalTimeScale = Time.timeScale;
            originalRunInBackground = Application.runInBackground;
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            // Game 뷰가 비활성일 때도 등록 순간부터 테스트 장치가 입력을 받을 수 있도록 포커스 정책을 먼저 적용합니다.
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            // 실행 중인 입력 시스템과 UI 액션을 유지하면서 실제 장치만 잠시 테스트 장치로 교체합니다.
            foreach (var device in InputSystem.devices.ToArray())
            {
                if (!device.enabled || !(device is UnityEngine.InputSystem.Pointer || device is Keyboard)) continue;
                disabledDevices.Add(device);
                InputSystem.DisableDevice(device);
            }
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            // 공유 액션의 바인딩/활성 상태를 수정하지 않고 장치만 교체·복원하여 UI 입력 수명까지 그대로 검증합니다.
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var device in disabledDevices)
                if (device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
#endif
            Application.runInBackground = originalRunInBackground;
        }

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            visit = Find("MotherDoorVisitEvent");
            // 버튼 교체·문 방문 취소의 기존 검증은 문 이벤트를 고정하고, 확률 자체는 전용 테스트에서 확인합니다.
            MainGameTestEvents.UseOnly(director, visit);
            tools = Find("MainGameDeveloperTools");
            Assert.That(tools, Is.Not.Null, "저장된 MainGame에 개발자 도구가 필요합니다.");
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (director != null) Call(director, "StopEvents");
            if (manager != null)
            {
                yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
                Call(manager, "CloseMiniGameImmediately");
                yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
                Object.Destroy(manager.gameObject);
            }
            var menu = Find("MenuManager");
            if (menu != null) Object.Destroy(menu.gameObject);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Developer tools cleanup"));
            foreach (var name in new[] { "MainGame", "MiniGame01", "MiniGame02", "MiniGame03", "MiniGame04" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            if (shortcutTestClip != null) Object.Destroy(shortcutTestClip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraDrag_StartingOnDeveloperToolsDoesNotRotateTheRoom()
        {
            yield return Keys(Key.LeftShift);
            var look = Find("MainGameCameraLookController");
            Assert.That(Property<bool>(look, "CanLook"), Is.True);
            var menu = Field<UnityEngine.UI.Button>(tools, "menuButton");
            Vector2 menuPoint = ButtonScreenPosition(menu);
            var roomPoint = new Vector2(Screen.width * 0.65f, Screen.height * 0.55f);

            // 실제 UI 위에서 버튼을 누르고 방으로 끌어도 시작점의 입력 소유권을 유지해야 합니다.
            InputSystem.QueueStateEvent(mouse, new MouseState { position = menuPoint });
            yield return null;
            yield return null;
            Assert.That(EventSystem.current.IsPointerOverGameObject(mouse.deviceId), Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = menuPoint, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = roomPoint, delta = roomPoint - menuPoint, buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SpecificEventButtons_UseMouse_IgnoreWeights_AndPreserveMiniGameProgress()
        {
            yield return Keys(Key.LeftShift);
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            var shortcuts = Field<IEnumerable>(tools, "eventShortcuts").Cast<object>().ToArray();
            // 컴퓨터 등장까지 실제 패널에서 클릭할 수 있어야 합니다.
            Assert.That(shortcuts.Length, Is.EqualTo(6));
            var entries = MainGameTestEvents.Entries(director);
            // 가중치 0인 연출도 직접 재현하되 버튼이 제품 확률이나 현재 미니게임을 바꾸지 않아야 합니다.
            foreach (var entry in entries) MainGameTestEvents.SetWeight(entry, 0);
            Set(manager, "currentLives", 2);
            Set(manager, "currentClearedCount", 5);
            var game = Field<MiniGameController>(manager, "currentMiniGameController");
            MonoBehaviour previous = null;
            foreach (var shortcut in shortcuts)
            {
                var button = Field<UnityEngine.UI.Button>(shortcut, "button");
                var selected = Field<MonoBehaviour>(shortcut, "eventBehaviour");
                Assert.That(button.GetComponentInChildren<UnityEngine.UI.Image>(true), Is.Null);
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("RunEventImmediately"));
                // 같은 버튼을 연속해서 눌러도 이전 실행을 취소한 뒤 처음부터 시작해야 합니다.
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    yield return PointerClick(button);
                    Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(selected));
                    Assert.That(Property<bool>(selected, "IsRunning"), Is.True);
                    Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Running"));
                    Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.Zero);
                    Assert.That(entries.Cast<object>().Count(entry => Property<bool>(MainGameTestEvents.Event(entry), "IsRunning")), Is.EqualTo(1));
                }
                if (previous != null) Assert.That(Property<bool>(previous, "IsRunning"), Is.False);
                previous = selected;
                Assert.That(Field<MiniGameController>(manager, "currentMiniGameController"), Is.SameAs(game));
                Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(2));
                Assert.That(Property<int>(manager, "CurrentClearedCount"), Is.EqualTo(5));
                Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.True);
                Assert.That(entries.Cast<object>().All(entry => Property<int>(entry, "Weight") == 0), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SpecificEventSwitches_RejectInvalidTargets_AndCleanUpMovingMotherAndOpenWindow()
        {
            yield return Keys(Key.LeftShift);
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            var shortcuts = Field<IEnumerable>(tools, "eventShortcuts").Cast<object>().ToArray();
            var rushShortcut = shortcuts.First(item => Field<MonoBehaviour>(item, "eventBehaviour").GetType().Name == "MotherDoorRushEvent");
            var windowShortcut = shortcuts.First(item => Field<MonoBehaviour>(item, "eventBehaviour").GetType().Name == "MotherWindowPeekEvent");
            var footstepsShortcut = shortcuts.First(item => Field<MonoBehaviour>(item, "eventBehaviour").GetType().Name == "MotherFootstepsOnlyEvent");
            var rush = Field<MonoBehaviour>(rushShortcut, "eventBehaviour");
            var windowEvent = Field<MonoBehaviour>(windowShortcut, "eventBehaviour");
            var actor = Find("MotherActorController");
            var model = Field<Transform>(actor, "actorRoot");
            shortcutTestClip = AudioClip.Create("Developer event switch test", 4410, 1, 44100, false);
            Set(rush, "footstepsClip", shortcutTestClip);
            Set(rush, "afterFootstepsDelay", 0f);
            Set(rush, "beforeRushDelay", 0f);
            Set(rush, "rushMoveSpeed", 0.1f);
            Set(Find("DoorController"), "openDuration", 0.1f);
            yield return PointerClick(Field<UnityEngine.UI.Button>(rushShortcut, "button"));
            yield return WaitFor(() => Property<object>(rush, "Phase").ToString() == "Entering");

            // 잘못된 요청은 진행 중인 돌진을 취소하지 않습니다. 비활성/미등록 이벤트는 UI와 API 양쪽에서 거부합니다.
            Assert.That((bool)Call(director, "TryRunEventForDevelopment", (object)null), Is.False);
            windowEvent.enabled = false;
            yield return null;
            Assert.That(Field<UnityEngine.UI.Button>(windowShortcut, "button").interactable, Is.False);
            Assert.That((bool)Call(director, "TryRunEventForDevelopment", windowEvent), Is.False);
            windowEvent.enabled = true;
            var entries = MainGameTestEvents.Entries(director);
            int windowIndex = entries.Cast<object>().ToList().FindIndex(entry => MainGameTestEvents.Event(entry) == windowEvent);
            var removed = entries[windowIndex];
            entries.RemoveAt(windowIndex);
            try { Assert.That((bool)Call(director, "TryRunEventForDevelopment", windowEvent), Is.False); }
            finally { entries.Insert(windowIndex, removed); }
            Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(rush));
            Assert.That(Property<bool>(rush, "IsRunning"), Is.True);

            Set(windowEvent, "knockClip", shortcutTestClip);
            Set(windowEvent, "afterFirstKnockDelay", 0f);
            Set(windowEvent, "afterSecondKnockDelay", 0f);
            Set(windowEvent, "windowOpenSeconds", 0.05f);
            Set(windowEvent, "peekTransitionSeconds", 0.05f);
            Set(windowEvent, "watchSeconds", 10f);
            yield return null;
            yield return PointerClick(Field<UnityEngine.UI.Button>(windowShortcut, "button"));
            Assert.That(Property<bool>(rush, "IsRunning"), Is.False);
            Assert.That(Property<bool>(actor, "IsMoving"), Is.False);
            Assert.That(Property<bool>(Find("DoorController"), "IsClosed"), Is.True);
            Assert.That(model.gameObject.activeSelf, Is.False);
            Vector3 stopped = model.position;
            yield return new WaitForSeconds(0.1f);
            Assert.That(model.position, Is.EqualTo(stopped));
            yield return WaitFor(() => Property<object>(windowEvent, "Phase").ToString() == "Watching");
            Assert.That(model.gameObject.activeSelf, Is.True);
            yield return PointerClick(Field<UnityEngine.UI.Button>(footstepsShortcut, "button"));
            Assert.That(Property<bool>(windowEvent, "IsRunning"), Is.False);
            Assert.That(Property<bool>(Find("WindowController"), "IsClosed"), Is.True);
            Assert.That(Field<AudioSource>(windowEvent, "windowSource").isPlaying, Is.False);
            Assert.That(model.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator MouseClicks_WorkWithLegacyCanvasHidden_AndAfterMiniGameSwitch()
        {
            var legacy = Resources.FindObjectsOfTypeAll<GameObject>().First(go => go.scene.name == "MainGame" && go.name == "UIsRoot");
            legacy.SetActive(false);
            // EventSystem이 숨길 수 있는 Canvas의 자식이면 이 시점부터 실제 마우스 클릭이 모두 사라집니다.
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(EventSystem.current.isActiveAndEnabled, Is.True);
            Assert.That(EventSystem.current.transform.IsChildOf(legacy.transform), Is.False);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(system => system.isActiveAndEnabled), Is.EqualTo(1));

            var panel = Field<GameObject>(tools, "toolsPanel");
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            Assert.That(panel.activeSelf, Is.True, "마우스 클릭으로 개발자 메뉴가 열려야 합니다.");
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "snakeButton"));
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            Assert.That(Field<MiniGameController>(manager, "currentMiniGameController").GameId, Is.EqualTo("MiniGame01"));
            Assert.That(Field<MiniGameController>(manager, "currentMiniGameController").State, Is.EqualTo(MiniGameState.Ready));
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            Assert.That(panel.activeSelf, Is.False);

            // 기존 테스트 Canvas를 잠깐 켜도 같은 EventSystem으로 그 안의 버튼을 처리해야 합니다.
            var page = legacy.GetComponentInChildren(Type.GetType("PageBase, Assembly-CSharp", true), true) as MonoBehaviour;
            page.GetType().GetField("_fakeLoadDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(page, 0f);
            // 보관된 테스트 Canvas는 Raycaster도 꺼져 있으므로, 입력 수명을 검사할 실행 인스턴스에서만 수신을 활성화합니다.
            legacy.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled = true;
            legacy.SetActive(true);
            yield return null;
            yield return null;
            var legacyButton = legacy.GetComponentsInChildren<UnityEngine.UI.Button>().First();
            // 숨겨 둔 과거 테스트 화면의 버튼은 화면 밖에 배치되어 있습니다. 입력 수명 검증에서는 실행 인스턴스만 빈 화면 모서리로 옮깁니다.
            Canvas.ForceUpdateCanvases();
            var legacyCanvas = legacy.GetComponent<Canvas>();
            Assert.That(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)legacyCanvas.transform,
                new Vector2(Screen.width * 0.9f, Screen.height * 0.1f), legacyCanvas.worldCamera, out var legacyPosition), Is.True);
            legacyButton.transform.position = legacyPosition;
            int legacyClicks = 0;
            legacyButton.onClick.AddListener(() => legacyClicks++);
            yield return PointerClick(legacyButton);
            Assert.That(legacyClicks, Is.EqualTo(1), "기존 테스트 UI도 실제 마우스 입력을 받아야 합니다.");
            legacy.SetActive(false);
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            Assert.That(panel.activeSelf, Is.True, "Canvas를 다시 숨겨도 개발자 도구 입력은 유지되어야 합니다.");
        }

        [UnityTest]
        public IEnumerator MouseRetry_RestartsMainGame_AndNewDeveloperMenuStillReceivesClicks()
        {
            var reason = Type.GetType("EGameOverReason, Assembly-CSharp", true);
            Call(manager, "TriggerGameOver", Enum.Parse(reason, "CaughtByMother"));
            Assert.That(Time.timeScale, Is.Zero);
            var oldManager = manager;
            yield return PointerClick(Field<UnityEngine.UI.Button>(manager, "gameOverRetryButton"));
            yield return WaitFor(() => Find("MiniGameManager") != null && Find("MiniGameManager") != oldManager);
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            visit = Find("MotherDoorVisitEvent");
            tools = Find("MainGameDeveloperTools");
            Call(director, "StopEvents");
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(system => system.isActiveAndEnabled), Is.EqualTo(1));
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            Assert.That(Field<GameObject>(tools, "toolsPanel").activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator AuthoredButtons_SwitchGamesToReadyForR_PreserveProgressAndMotherEvent()
        {
            var panel = Field<GameObject>(tools, "toolsPanel");
            Assert.That(panel.activeSelf, Is.False);
            Assert.That((bool)tools.GetType().GetProperty("IsAvailableInThisBuild").GetValue(null), Is.True);
            Click("menuButton");
            // 실제 마우스의 다음 클릭처럼 펼친 TMP 그래픽이 Canvas에 등록될 프레임을 기다립니다.
            yield return null;
            Assert.That(panel.activeInHierarchy, Is.True);
            Set(manager, "currentLives", 2);
            Set(manager, "currentClearedCount", 5);
            Click("nextEventButton");
            var active = Property<Object>(director, "ActiveEvent");
            Assert.That(active, Is.Not.Null);

            Click("flappyButton");
            Assert.That(Property<bool>(manager, "IsTransitioning"), Is.True);
            Assert.That(Field<UnityEngine.UI.Button>(tools, "snakeButton").interactable, Is.False);
            Assert.That((bool)Call(manager, "TryLoadMiniGameForDevelopment", "MiniGame01"), Is.False,
                "전환 중 직접 요청해도 중복 로드를 시작하면 안 됩니다.");
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            var flappy = Field<MiniGameController>(manager, "currentMiniGameController");
            Assert.That(flappy.GameId, Is.EqualTo("MiniGame02"));
            Assert.That(flappy.State, Is.EqualTo(MiniGameState.Ready));
            Assert.That(flappy.Progress, Is.Zero);
            yield return Keys(Key.R);
            Assert.That(flappy.State, Is.EqualTo(MiniGameState.Playing));
            yield return Keys();
            yield return Keys(Key.Tab);
            Assert.That(flappy.State, Is.EqualTo(MiniGameState.Paused));

            Click("snakeButton");
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            var snake = Field<MiniGameController>(manager, "currentMiniGameController");
            Assert.That(flappy == null, Is.True, "이전 미니게임 인스턴스가 실제로 제거되어야 합니다.");
            Assert.That(snake.GameId, Is.EqualTo("MiniGame01"));
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Ready));
            Assert.That(Property<bool>(manager, "IsMiniGamePaused"), Is.False);
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(2));
            Assert.That(Property<int>(manager, "CurrentClearedCount"), Is.EqualTo(5));
            Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(active));
            Assert.That(Property<bool>(visit, "IsRunning"), Is.True, "미니게임 교체는 엄마 이벤트를 취소하지 않습니다.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return Keys();
            yield return Keys(Key.R);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Playing));
            Click("menuButton");
            Assert.That(panel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DinoAndSortingButtons_OpenReadyGames_AndPreserveMainGameProgress()
        {
            // 패널이 열린 뒤 텍스트가 렌더링될 프레임을 거쳐 실제 포인터로 이벤트를 실행합니다.
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "menuButton"));
            Set(manager, "currentLives", 2);
            Set(manager, "currentClearedCount", 6);
            yield return PointerClick(Field<UnityEngine.UI.Button>(tools, "nextEventButton"));
            var activeEvent = Property<Object>(director, "ActiveEvent");
            Assert.That(activeEvent, Is.SameAs(visit));

            // 두 버튼의 실제 클릭 연결과 교체된 게임의 Ready 상태 및 R 시작을 함께 검증합니다.
            foreach (var entry in new[] { (button: "dinoButton", scene: "MiniGame04"), (button: "sortingButton", scene: "MiniGame03") })
            {
                // 앞 게임의 시작 키를 놓은 뒤 버튼을 누릅니다. 씬 로드 중 테스트 장치의 눌림 상태를 넘기지 않습니다.
                yield return Keys();
                Click(entry.button);
                Assert.That(Field<UnityEngine.UI.Button>(tools, "dinoButton").interactable, Is.False);
                Assert.That(Field<UnityEngine.UI.Button>(tools, "sortingButton").interactable, Is.False);
                yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
                yield return null;
                var game = Field<MiniGameController>(manager, "currentMiniGameController");
                Assert.That(game.GameId, Is.EqualTo(entry.scene));
                Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
                Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(2));
                Assert.That(Property<int>(manager, "CurrentClearedCount"), Is.EqualTo(6));
                Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(activeEvent));
                Assert.That(Property<bool>(visit, "IsRunning"), Is.True);
                yield return Keys();
                yield return Keys(Key.R);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            }
        }

        [UnityTest]
        public IEnumerator ImmediateEvent_CancelsCurrentMovementAndRestartsWithoutOverlap()
        {
            yield return Keys(Key.LeftShift);
            Click("menuButton");
            yield return null;
            Set(visit, "afterFootstepsDelay", 0f);
            Set(visit, "detectionDelay", 30f);
            var actor = Find("MotherActorController");
            Set(actor, "moveSpeed", 0.1f);
            Click("nextEventButton");
            yield return WaitFor(() => Property<object>(visit, "Phase").ToString() == "Entering");
            var model = Field<Transform>(actor, "actorRoot");
            yield return null;
            Vector3 previousPosition = model.position;
            // 새 경고 재생 동안 예전 이동 코루틴이 살아 있지 않은지도 함께 확인합니다.
            Click("nextEventButton");
            Assert.That(Property<object>(visit, "Phase").ToString(), Is.EqualTo("Footsteps"));
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(actor, "IsMoving"), Is.False);
            Assert.That(Property<bool>(Find("DoorController"), "IsClosed"), Is.True);
            yield return new WaitForSeconds(0.2f);
            Assert.That(model.position, Is.EqualTo(previousPosition));
            Assert.That(Property<bool>(visit, "IsRunning"), Is.True);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Running"));
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator GameOver_DisablesToolsAndRejectsDirectRequests()
        {
            Click("menuButton");
            var reason = Type.GetType("EGameOverReason, Assembly-CSharp", true);
            Call(manager, "TriggerGameOver", Enum.Parse(reason, "CaughtByMother"));
            yield return null;
            Assert.That(Field<GameObject>(tools, "toolsPanel").activeSelf, Is.False);
            foreach (var button in tools.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                Assert.That(button.interactable, Is.False);
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.False);
            Assert.That((bool)Call(manager, "TryLoadMiniGameForDevelopment", "MiniGame02"), Is.False);
            Call(tools, "RunNextEventImmediately");
            Call(tools, "OpenSnakeMiniGame");
            foreach (var shortcut in Field<IEnumerable>(tools, "eventShortcuts"))
            {
                var selected = Field<MonoBehaviour>(shortcut, "eventBehaviour");
                Assert.That((bool)Call(director, "CanRunEventForDevelopment", selected), Is.False);
                Assert.That((bool)Call(director, "TryRunEventForDevelopment", selected), Is.False);
                Call(tools, "RunEventImmediately", selected);
            }
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Stopped"));
            Assert.That(Property<bool>(manager, "IsTransitioning"), Is.False);
        }

        [UnityTest]
        public IEnumerator InspectorDurations_AreUsedForNextWaitAndBedsideWatch()
        {
            yield return Keys(Key.LeftShift);
            Set(director, "intervalSeconds", new Vector2(0.1f, 0.1f));
            CallPrivate(director, "ScheduleNextEvent");
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.EqualTo(0.1f).Within(0.0001f));
            Set(visit, "afterFootstepsDelay", 0f);
            Set(visit, "watchSeconds", new Vector2(0.25f, 0.25f));
            Set(Find("MotherActorController"), "moveSpeed", 30f);
            yield return WaitFor(() => Property<object>(visit, "Phase").ToString() == "Watching");
            float started = Time.time;
            yield return WaitFor(() => Property<object>(visit, "Phase").ToString() == "Leaving");
            Assert.That(Time.time - started, Is.InRange(0.23f, 0.4f));
        }

        // 실제 입력 액션 → InputSystemUIInputModule → EventSystem → Button 경로를 밟습니다. onClick/ExecuteEvents를 직접 호출하지 않습니다.
        private IEnumerator PointerClick(UnityEngine.UI.Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True,
                $"{button.name} 버튼을 클릭할 수 없습니다. 활성={button.isActiveAndEnabled}, 상호작용={button.interactable}");
            Assert.That(EventSystem.current != null && EventSystem.current.isActiveAndEnabled, Is.True);
            var module = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Assert.That(module.isActiveAndEnabled, Is.True);
            Assert.That(module.leftClick.action.enabled, Is.True);
            Assert.That(module.leftClick.action.controls.Any(control => control.device == mouse), Is.True);
            Vector2 position = ButtonScreenPosition(button);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        private static Vector2 ButtonScreenPosition(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            // 클릭과 드래그 모두 버튼이 속한 Canvas의 렌더링 카메라로 화면 좌표를 구합니다.
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        // 버튼 연결과 기능 조합의 빠른 검증용 직접 호출입니다. 실제 포인터 경로의 회귀는 위 PointerClick 테스트들이 별도로 담당합니다.
        private void Click(string field)
        {
            var button = Field<UnityEngine.UI.Button>(tools, field);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.GetComponent<UnityEngine.UI.Image>(), Is.Null);
            Assert.That(button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
            var data = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Any(hit => hit.gameObject == button.gameObject), Is.True, "텍스트 버튼이 실제 UI Raycaster에 잡혀야 합니다.");
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator Keys(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name).Invoke(target, arguments);
        private static void CallPrivate(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "개발자 도구의 상태 전환 시간이 초과되었습니다.");
        }
    }
}
