using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 실제 씬의 공용 문·배우·발소리를 사용합니다. 추첨만 고정하고 연출·발각·취소는 제품 경로로 검증합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameDoorRushEventTests
    {
        private MonoBehaviour manager, director, rush, regular, mother, door;
        private Transform model;
        private AudioSource source;
        private AudioClip shortClip;
        private float originalTimeScale;
        private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            Connect();
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            // 입력 정책 자체는 기존 테스트에 맡기고, 사용자 조작이나 미니게임 실패가 연출 시간 측정에 섞이지 않게 합니다.
            manager.enabled = false;
            Field<MiniGameController>(manager, "currentMiniGameController").KeyboardInputEnabled = false;
            Call(director, "StopEvents");
            MainGameTestEvents.UseOnly(director, rush);
            HideScreen(true);
        }

        private void Connect()
        {
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            rush = Find("MotherDoorRushEvent");
            regular = Find("MotherDoorVisitEvent");
            Assert.That(rush, Is.Not.Null, "MainGame에 돌진 이벤트를 저장해야 합니다.");
            Assert.That(Property<bool>(rush, "CanRun"), Is.True);
            mother = Field<MonoBehaviour>(rush, "mother");
            door = Field<MonoBehaviour>(rush, "door");
            model = Field<Transform>(mother, "actorRoot");
            source = Field<AudioSource>(rush, "footstepsSource");
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Door rush cleanup"));
            var scene = SceneManager.GetSceneByName("MainGame");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (shortClip != null) Object.Destroy(shortClip);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullRush_UsesOriginalWarning_WaitsAtDoor_RunsBothWays_AndSchedulesAfterClosing()
        {
            Assert.That(Field<MonoBehaviour>(regular, "mother"), Is.SameAs(mother));
            Assert.That(Field<MonoBehaviour>(regular, "door"), Is.SameAs(door));
            Assert.That(Field<AudioSource>(regular, "footstepsSource"), Is.SameAs(source));
            var clip = Field<AudioClip>(rush, "footstepsClip");
            Assert.That(clip, Is.SameAs(Field<AudioClip>(regular, "footstepsClip")));
            Assert.That(clip.name, Is.EqualTo("Step_Walk"));
            Assert.That(Field<float>(rush, "afterFootstepsDelay"), Is.EqualTo(1f));
            Assert.That(Field<float>(rush, "detectionDelay"), Is.EqualTo(1f));
            Assert.That(Field<float>(rush, "beforeRushDelay"), Is.EqualTo(1f));
            // 씬에서 조정한 관찰 시간으로 실제 정지 구간을 검증합니다.
            float watchSeconds = Field<float>(rush, "watchSeconds");
            Assert.That(Field<float>(rush, "rushMoveSpeed"), Is.GreaterThan(Field<float>(mother, "moveSpeed")));
            float started = Time.time;
            Begin();
            Assert.That(source.isPlaying, Is.True);
            yield return WaitFor(() => Phase == "BeforeOpening");
            Assert.That(Time.time - started, Is.InRange(clip.length / source.pitch - 0.1f, clip.length / source.pitch + 0.3f));
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            float soundEnded = Time.time;
            yield return WaitFor(() => Phase == "Opening");
            float openingAt = Time.time;
            Assert.That(openingAt - soundEnded, Is.InRange(0.98f, 1.2f));
            Assert.That(Property<bool>(rush, "IsDetectionActive"), Is.False);
            yield return WaitFor(() => Property<bool>(rush, "IsDetectionActive"));
            Assert.That(Time.time - openingAt, Is.InRange(0.98f, 1.15f));
            yield return WaitFor(() => Phase == "Hesitating");
            Assert.That(Property<bool>(door, "IsOpen"), Is.True);
            var outside = Field<Transform>(rush, "outsidePoint");
            Assert.That(Vector3.Distance(model.position, outside.position), Is.LessThan(0.01f));
            Assert.That(Property<bool>(mother, "IsMoving"), Is.False);
            float hesitationAt = Time.time;
            yield return new WaitForSeconds(0.8f);
            Assert.That(Phase, Is.EqualTo("Hesitating"));
            Assert.That(model.position, Is.EqualTo(outside.position));
            yield return WaitFor(() => Phase == "Entering");
            Assert.That(Time.time - hesitationAt, Is.InRange(0.98f, 1.2f));
            float enteringAt = Time.time;
            Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(Field<float>(rush, "rushPlaybackSpeed")));
            float expectedEntry = PathLength(outside.position, Field<Transform[]>(rush, "entryPath")) / Field<float>(rush, "rushMoveSpeed");
            yield return WaitFor(() => Phase == "Watching");
            Assert.That(Time.time - enteringAt, Is.InRange(expectedEntry - 0.1f, expectedEntry + 0.3f));
            var entry = Field<Transform[]>(rush, "entryPath");
            Assert.That(Vector3.Distance(model.position, entry[entry.Length - 1].position), Is.LessThan(0.01f));
            Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(1f));
            float watchingAt = Time.time;
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - watchingAt, Is.EqualTo(watchSeconds).Within(0.2f));
            float leavingAt = Time.time;
            float expectedExit = PathLength(model.position, Field<Transform[]>(rush, "exitPath")) / Field<float>(rush, "rushMoveSpeed");
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(Time.time - leavingAt, Is.InRange(expectedExit - 0.1f, expectedExit + 0.3f));
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(rush, "IsDetectionActive"), Is.True);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Running"));
            yield return WaitFor(() => Property<object>(director, "State").ToString() == "Waiting");
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            Assert.That(Property<bool>(rush, "IsDetectionActive"), Is.False);
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.InRange(29.7f, 120f));
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator InspectorDelaysAndSpeed_AreApplied_ThenNormalVisitKeepsItsOwnSpeed()
        {
            ShortSequence();
            Set(rush, "beforeRushDelay", 0.25f);
            Set(rush, "watchSeconds", 0.3f);
            Set(rush, "rushMoveSpeed", 12f);
            Set(rush, "rushPlaybackSpeed", 1.4f);
            Begin();
            yield return WaitFor(() => Phase == "Hesitating");
            float at = Time.time;
            yield return WaitFor(() => Phase == "Entering");
            Assert.That(Time.time - at, Is.InRange(0.23f, 0.4f));
            Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(1.4f));
            at = Time.time;
            float expected = PathLength(model.position, Field<Transform[]>(rush, "entryPath")) / 12f;
            yield return WaitFor(() => Phase == "Watching");
            Assert.That(Time.time - at, Is.InRange(expected - 0.1f, expected + 0.25f));
            at = Time.time;
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - at, Is.InRange(0.28f, 0.45f));
            Call(director, "StopEvents");
            Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(1f));
            Assert.That(Field<float>(mother, "moveSpeed"), Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(Field<float>(mother, "runPlaybackSpeed"), Is.EqualTo(0.45f).Within(0.001f));
            MainGameTestEvents.UseOnly(director, regular);
            Set(regular, "footstepsClip", shortClip);
            Set(regular, "afterFootstepsDelay", 0f);
            Begin();
            yield return WaitFor(() => Property<object>(regular, "Phase").ToString() == "Entering");
            Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(0.45f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator RevealingDuringHesitation_UsesExistingGameOverAndRetry()
        {
            yield return RevealAndRetry("Hesitating");
        }

        [UnityTest]
        public IEnumerator RevealingWhileClosing_IsCaughtUntilFullyClosed()
        {
            yield return RevealAndRetry("Closing");
        }

        // 발각 조건은 화면 숨김 여부이며 시선·Tab 정지와 독립적입니다. 공통 Retry가 새 이벤트까지 재생성하는지도 확인합니다.
        private IEnumerator RevealAndRetry(string phase)
        {
            ShortSequence();
            Begin();
            yield return WaitFor(() => Phase == phase);
            Assert.That(Property<bool>(rush, "IsDetectionActive"), Is.True);
            HideScreen(false);
            yield return WaitFor(() => Property<bool>(manager, "IsGameOver"));
            Assert.That(Property<object>(manager, "GameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            Assert.That(Field<GameObject>(manager, "gameOverOverlay").activeInHierarchy, Is.True);
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Phase, Is.EqualTo("Idle"));
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            var oldManager = manager;
            Call(manager, "RestartMainGame");
            yield return WaitFor(() => Find("MiniGameManager") != null && Find("MiniGameManager") != oldManager);
            Connect();
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Waiting"));
            Assert.That(Field<float>(rush, "beforeRushDelay"), Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator CancellationAtEveryPhase_LeavesNoSoundMovementOrSpeedOverride()
        {
            ShortSequence();
            foreach (string phase in new[] { "Footsteps", "BeforeOpening", "Opening", "Hesitating", "Entering", "Watching", "Leaving", "Closing" })
            {
                Begin();
                yield return WaitFor(() => Phase == phase);
                Call(director, "StopEvents");
                Assert.That(Phase, Is.EqualTo("Idle"));
                Assert.That(Property<bool>(rush, "IsRunning"), Is.False);
                Assert.That(source.isPlaying, Is.False);
                Assert.That(Property<bool>(door, "IsClosed"), Is.True);
                Assert.That(model.gameObject.activeSelf, Is.False);
                Assert.That(Property<bool>(mother, "IsMoving"), Is.False);
                Assert.That(Field<Animator>(mother, "animator").speed, Is.EqualTo(1f));
                Vector3 cancelledPosition = model.position;
                yield return new WaitForSeconds(0.1f);
                Assert.That(model.position, Is.EqualTo(cancelledPosition), "취소한 하위 이동 코루틴이 남으면 안 됩니다.");
            }
        }

        // 긴 원본 소리는 첫 테스트에서 검증합니다. 나머지는 단계별 취소·발각에 집중하도록 실행 인스턴스만 단축합니다.
        private void ShortSequence()
        {
            shortClip = AudioClip.Create("Door rush test warning", 4410, 1, 44100, false);
            Set(rush, "footstepsClip", shortClip);
            Set(rush, "afterFootstepsDelay", 0.1f);
            Set(rush, "beforeRushDelay", 0.15f);
            Set(rush, "watchSeconds", 0.15f);
            Set(rush, "detectionDelay", 0.02f);
            Set(door, "openDuration", 0.1f);
            Set(door, "closeDuration", 0.15f);
        }

        private string Phase => Property<object>(rush, "Phase").ToString();
        private void Begin() => Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
        private void HideScreen(bool hidden) => Set(manager, "isMiniGameHidden", hidden);
        private static float PathLength(Vector3 start, Transform[] path)
        {
            float length = 0f;
            foreach (var point in path) { length += Vector3.Distance(start, point.position); start = point.position; }
            return length;
        }
        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "돌진 이벤트 상태 전환 시간이 초과되었습니다.");
        }
    }
}
