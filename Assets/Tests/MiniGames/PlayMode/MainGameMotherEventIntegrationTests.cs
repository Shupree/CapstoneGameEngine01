using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 메인 어셈블리 경계를 유지한 채 저장된 씬·소리·문·배우와 실제 입력의 연결을 검증합니다.
    public sealed class MainGameMotherEventIntegrationTests : InputTestFixture
    {
        private Keyboard keyboard;
        private MonoBehaviour manager, director, visit, actor, door;
        private AudioSource source;
        private AudioClip shortClip;
        private float originalTimeScale;
        private readonly Dictionary<InputActionAsset, InputDevice[]> actionDevices = new Dictionary<InputActionAsset, InputDevice[]>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            // 공유 UI 액션이 이전 테스트의 가상 장치를 계속 참조하지 않게 장치 필터를 교체합니다.
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                actionDevices[asset] = asset.devices.HasValue ? asset.devices.Value.ToArray() : null;
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
            foreach (var item in actionDevices)
            {
                if (item.Key == null) continue;
                item.Key.Disable();
                item.Key.devices = item.Value;
            }
            actionDevices.Clear();
        }

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            visit = Find("MotherDoorVisitEvent");
            actor = Find("MotherActorController");
            door = Find("DoorController");
            Assert.That(director, Is.Not.Null);
            Assert.That(Property<bool>(visit, "CanRun"), Is.True, "저장된 MainGame의 Inspector 참조가 모두 필요합니다.");
            source = Field<AudioSource>(visit, "footstepsSource");
            yield return WaitFor(() => !Field<bool>(manager, "isLoading"));
        }

        [UnityTearDown]
        public IEnumerator UnloadHost()
        {
            if (director != null) Call(director, "StopEvents");
            manager = Find("MiniGameManager");
            if (manager != null)
            {
                yield return WaitFor(() => !Field<bool>(manager, "isLoading"));
                Call(manager, "CloseMiniGameImmediately");
                yield return WaitFor(() => !Field<bool>(manager, "isLoading"));
                Object.Destroy(manager.gameObject);
            }
            var menu = Find("MenuManager");
            if (menu != null) Object.Destroy(menu.gameObject);
            foreach (string name in new[] { "MiniGame01", "MiniGame02", "MainGame" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Mother integration cleanup");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (shortClip != null) Object.Destroy(shortClip);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullVisit_UsesSoundEndAndDoorStartClocks_WatchesAtBed_ThenSchedulesAfterExit()
        {
            Assert.That(Field<Vector2>(director, "intervalSeconds"), Is.EqualTo(new Vector2(30, 120)));
            Assert.That(Field<Vector2>(visit, "watchSeconds"), Is.EqualTo(new Vector2(4, 7)));
            Assert.That(Field<float>(visit, "afterFootstepsDelay"), Is.EqualTo(1f));
            Assert.That(Field<float>(visit, "detectionDelay"), Is.EqualTo(1f));
            Assert.That(Field<AudioClip>(visit, "footstepsClip").name, Is.EqualTo("Step_Walk"));
            Assert.That(Field<float>(actor, "runPlaybackSpeed"), Is.LessThan(1f));
            // 경로·관찰 시간은 실제 계약대로 검증하되 테스트의 왕복 이동만 빠르게 합니다.
            Set(actor, "moveSpeed", 20f);
            yield return Keys(Key.LeftShift);
            BeginNext();
            yield return WaitFor(() => Phase == "BeforeOpening");
            float soundEnded = Time.time;
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            yield return WaitFor(() => Phase == "Opening");
            float openedAt = Time.time;
            Assert.That(openedAt - soundEnded, Is.InRange(0.95f, 1.2f));
            Assert.That(Property<bool>(visit, "IsDetectionActive"), Is.False);
            yield return WaitFor(() => Property<bool>(visit, "IsDetectionActive"));
            Assert.That(Time.time - openedAt, Is.InRange(0.95f, 1.15f));
            Assert.That(Property<bool>(door, "IsOpen"), Is.False,
                "1.2초 문 애니메이션이 끝나기 전인 시작+1초에 발각 구간이 켜져야 합니다.");
            yield return WaitFor(() => Phase == "Watching");
            float watchStarted = Time.time;
            var path = Field<Transform[]>(visit, "entryPath");
            var model = Field<Transform>(actor, "actorRoot");
            Assert.That(Vector3.Distance(model.position, path[path.Length - 1].position), Is.LessThan(0.02f));
            Assert.That(Property<bool>(actor, "IsMoving"), Is.False);
            Assert.That(Field<Animator>(actor, "animator").speed, Is.EqualTo(1f));
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - watchStarted, Is.InRange(3.95f, 7.2f));
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(model.gameObject.activeSelf, Is.False, "엄마가 완전히 퇴장한 뒤 문을 닫습니다.");
            Assert.That(Property<bool>(visit, "IsDetectionActive"), Is.True);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Running"));
            yield return WaitFor(() => Property<object>(director, "State").ToString() == "Waiting");
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            Assert.That(Property<bool>(visit, "IsDetectionActive"), Is.False);
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.InRange(29.8f, 120f));
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
        }

        [UnityTest]
        public IEnumerator VisibleTabPause_IsCaught_AndRetryCreatesNewDirectorAndTimer()
        {
            yield return EnsureSnake();
            var snake = Object.FindFirstObjectByType<SnakeGame>();
            snake.StartGame();
            yield return Keys(Key.Tab);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Paused));
            ShortWarning();
            Set(visit, "detectionDelay", 0.1f);
            int oldDirector = director.GetInstanceID();
            BeginNext();
            yield return WaitFor(() => Property<bool>(manager, "IsGameOver"));
            Assert.That(Property<object>(manager, "GameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            Assert.That(Field<GameObject>(manager, "gameOverOverlay").activeInHierarchy, Is.True);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Stopped"));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<bool>(visit, "IsRunning"), Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            // 클릭 연결 자체는 기존 목숨 통합 테스트가 검증합니다. 여기서는 새 이벤트 수명과 타이머 복원을 확인합니다.
            Call(manager, "RestartMainGame");
            yield return WaitFor(() => Find("MainGameEventDirector") != null && Find("MainGameEventDirector").GetInstanceID() != oldDirector);
            director = Find("MainGameEventDirector");
            manager = Find("MiniGameManager");
            yield return null;
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Waiting"));
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.InRange(29f, 120f));
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator RevealingWhileDoorCloses_IsCaughtUntilFullyClosed()
        {
            ShortWarning();
            Set(actor, "moveSpeed", 40f);
            Set(visit, "watchSeconds", new Vector2(0.1f, 0.1f));
            Set(door, "closeDuration", 2f);
            yield return Keys(Key.LeftShift);
            BeginNext();
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            Assert.That(Property<bool>(visit, "IsDetectionActive"), Is.True);
            yield return Keys();
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.True,
                "엄마 모델이 사라졌어도 문이 닫히는 동안 게임기를 다시 켜면 발각됩니다.");
        }

        [UnityTest]
        public IEnumerator FinalClear_CancelsWarningAndPreventsFurtherEvents()
        {
            yield return Keys(Key.LeftShift);
            BeginNext();
            yield return WaitFor(() => Phase == "Footsteps");
            Set(manager, "totalMiniGamesToClear", 1);
            Call(manager, "OnMiniGameCleared");
            Assert.That(Property<bool>(manager, "IsGameFinished"), Is.True);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Stopped"));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<bool>(visit, "IsRunning"), Is.False);
            yield return new WaitForSeconds(0.1f);
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
        }

        private string Phase => Property<object>(visit, "Phase").ToString();
        private void BeginNext() => Set(director, "secondsUntilNextEvent", 0f);

        private void ShortWarning()
        {
            // 발각·종료 경계 테스트에서만 짧은 소리를 씁니다. 전체 순서 테스트는 저장된 Step_Walk를 사용합니다.
            shortClip = AudioClip.Create("Mother test warning", 2205, 1, 44100, false);
            Set(visit, "footstepsClip", shortClip);
            Set(visit, "afterFootstepsDelay", 0.01f);
        }

        private IEnumerator EnsureSnake()
        {
            if (Field<string>(manager, "currentLoadedMiniGame") == "MiniGame01") yield break;
            // 실제 목록에 세 번째 이후 게임이 있어도 Tab 입력 검증에는 스네이크를 확실하게 로드합니다.
            MainGameTestScenes.Load(manager, "MiniGame01");
            yield return WaitFor(() => !Field<bool>(manager, "isLoading") && Field<string>(manager, "currentLoadedMiniGame") == "MiniGame01");
        }

        private IEnumerator Keys(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static void Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "MainGame 이벤트 상태 전환 시간이 초과되었습니다.");
        }
    }
}
