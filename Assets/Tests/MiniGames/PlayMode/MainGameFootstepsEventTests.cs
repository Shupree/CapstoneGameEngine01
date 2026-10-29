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
    // 실제 저장된 클립·Director·문·배우를 사용해 안전한 발소리 연출과 가중치 선택을 검증합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameFootstepsEventTests
    {
        private MonoBehaviour manager, director, footsteps, visit, actor, door;
        private AudioSource source;
        private Transform model;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalTimeScale;
        private AudioClip shortClip;
        private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            footsteps = Find("MotherFootstepsOnlyEvent");
            visit = Find("MotherDoorVisitEvent");
            actor = Find("MotherActorController");
            door = Find("DoorController");
            Assert.That(footsteps, Is.Not.Null);
            yield return WaitFor(() => !Field<bool>(manager, "isLoading"));
            // 게임기가 보일 때도 안전한 연출인지 검증하며, 실제 사용자 입력과 미니게임의 로컬 실패는 분리합니다.
            manager.enabled = false;
            Field<MiniGameController>(manager, "currentMiniGameController").KeyboardInputEnabled = false;
            source = Field<AudioSource>(footsteps, "footstepsSource");
            model = Field<Transform>(actor, "actorRoot");
            originalPosition = model.position;
            originalRotation = model.rotation;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (director != null) Call(director, "StopEvents");
            if (manager != null)
            {
                Call(manager, "CloseMiniGameImmediately");
                yield return WaitFor(() => !Field<bool>(manager, "isLoading"));
                Object.Destroy(manager.gameObject);
            }
            var menu = Find("MenuManager");
            if (menu != null) Object.Destroy(menu.gameObject);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Footsteps integration cleanup"));
            foreach (var name in new[] { "MainGame", "MiniGame01", "MiniGame02", "MiniGame03", "MiniGame04" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (shortClip != null) Object.Destroy(shortClip);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WeightedSelection_Uses75And25_HandlesZeroDisabledAndBoundaries()
        {
            var entries = MainGameTestEvents.Entries(director);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(MainGameTestEvents.Event(entries[0]), Is.SameAs(visit));
            Assert.That(MainGameTestEvents.Event(entries[1]), Is.SameAs(footsteps));
            Assert.That(Property<int>(entries[0], "Weight"), Is.EqualTo(75));
            Assert.That(Property<int>(entries[1], "Weight"), Is.EqualTo(25));
            int doorCount = 0, footstepsCount = 0;
            // 전체 추첨 구간을 고르게 조사해 임의 표본의 편향 때문에 비율 검증이 흔들리지 않게 합니다.
            for (int i = 0; i < 10000; i++)
            {
                var selected = Select((i + 0.5f) / 10000f);
                if (selected == visit) doorCount++;
                else if (selected == footsteps) footstepsCount++;
                else Assert.Fail("유효한 이벤트가 선택되지 않았습니다.");
            }
            Assert.That(doorCount, Is.EqualTo(7500));
            Assert.That(footstepsCount, Is.EqualTo(2500));
            Assert.That(Select(0f), Is.SameAs(visit));
            Assert.That(Select(0.75f), Is.SameAs(footsteps));
            Assert.That(Select(1f), Is.SameAs(footsteps));
            MainGameTestEvents.SetWeight(entries[0], 2);
            MainGameTestEvents.SetWeight(entries[1], 6);
            Assert.That(Select(0.249f), Is.SameAs(visit));
            Assert.That(Select(0.25f), Is.SameAs(footsteps), "가중치 합이 100이 아니어도 비율로 선택합니다.");
            MainGameTestEvents.SetWeight(entries[0], 0);
            Assert.That(Select(0f), Is.SameAs(footsteps));
            MainGameTestEvents.SetWeight(entries[0], 75);
            footsteps.enabled = false;
            Assert.That(Select(1f), Is.SameAs(visit));
            MainGameTestEvents.SetWeight(entries[0], 0);
            Assert.That(Select(0f), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullAudioSequence_WaitsOneSecond_NeverDetectsOrMovesMotherOrDoor()
        {
            var originalSource = Field<AudioSource>(visit, "footstepsSource");
            var clip = Field<AudioClip>(footsteps, "footstepsClip");
            Assert.That(clip, Is.SameAs(Field<AudioClip>(visit, "footstepsClip")));
            Assert.That(clip.name, Is.EqualTo("Step_Walk"));
            Assert.That(source.pitch, Is.EqualTo(originalSource.pitch));
            Assert.That(source.volume, Is.EqualTo(originalSource.volume));
            Assert.That(source.spatialBlend, Is.EqualTo(originalSource.spatialBlend));
            Assert.That(Field<float>(footsteps, "turnaroundDelay"), Is.EqualTo(1f));
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.False);
            int lives = Property<int>(manager, "CurrentLives");
            MainGameTestEvents.UseOnly(director, footsteps);
            float started = Time.time;
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
            Assert.That(source.isPlaying, Is.True);
            yield return WaitSafe(() => Phase == "Waiting");
            Assert.That(Time.time - started, Is.InRange(clip.length / source.pitch - 0.1f, clip.length / source.pitch + 0.3f));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(source.volume, Is.EqualTo(originalSource.volume), "접근 소리의 음량은 줄이지 않습니다.");
            float waitingAt = Time.time;
            yield return WaitSafe(() => Phase == "Retreating");
            Assert.That(Time.time - waitingAt, Is.InRange(0.98f, 1.2f));
            Assert.That(source.isPlaying, Is.True);
            float retreatAt = Time.time;
            // 원본 재생 시간을 유지하면서 실제 AudioSource 음량이 줄고, 종료 뒤에는 원래 값으로 돌아오는지 확인합니다.
            yield return WaitSafe(() => source.volume <= originalSource.volume * 0.6f);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.GreaterThan(0f));
            yield return WaitSafe(() => Property<object>(director, "State").ToString() == "Waiting");
            Assert.That(Time.time - retreatAt, Is.InRange(clip.length / source.pitch - 0.1f, clip.length / source.pitch + 0.3f));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(source.volume, Is.EqualTo(originalSource.volume));
            Assert.That(Property<bool>(footsteps, "IsRunning"), Is.False);
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.InRange(29.7f, 120f));
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(lives));
        }

        [UnityTest]
        public IEnumerator CancellationDuringFade_RestoresConfiguredVolumeForNextApproach()
        {
            source.volume = 0.65f;
            shortClip = AudioClip.Create("Footsteps fade cancellation test", 22050, 1, 44100, false);
            Set(footsteps, "footstepsClip", shortClip);
            Set(footsteps, "turnaroundDelay", 0.05f);
            MainGameTestEvents.UseOnly(director, footsteps);
            Call(director, "TryRunNextEventImmediately");
            yield return WaitSafe(() => Phase == "Retreating" && source.volume < 0.4f);
            Assert.That(source.isPlaying, Is.True);
            Call(director, "StopEvents");
            Assert.That(source.isPlaying, Is.False);
            Assert.That(source.volume, Is.EqualTo(0.65f).Within(0.0001f));
            Call(director, "TryRunNextEventImmediately");
            Assert.That(Phase, Is.EqualTo("Approaching"));
            Assert.That(source.volume, Is.EqualTo(0.65f).Within(0.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancellationDuringPause_PreventsReturnSound()
        {
            UseShortSound();
            MainGameTestEvents.UseOnly(director, footsteps);
            Call(director, "TryRunNextEventImmediately");
            yield return WaitSafe(() => Phase == "Waiting");
            Call(director, "StopEvents");
            yield return new WaitForSeconds(0.5f);
            Assert.That(Phase, Is.EqualTo("Idle"));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<bool>(footsteps, "IsRunning"), Is.False);
            AssertSafe();
        }

        [UnityTest]
        public IEnumerator ImmediateRestart_CancelsOldAudioBeforeStartingOtherEvent()
        {
            MainGameTestEvents.UseOnly(director, footsteps);
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
            Assert.That(source.isPlaying, Is.True);
            MainGameTestEvents.UseOnly(director, visit);
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
            Assert.That(Property<bool>(footsteps, "IsRunning"), Is.False);
            Assert.That(source.isPlaying, Is.False);
            Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(visit));
            Assert.That(Field<AudioSource>(visit, "footstepsSource").isPlaying, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyReferences_MigrateOnceWithoutDroppingExistingWeight()
        {
            // 이전 씬의 참조 목록을 가진 비활성 오브젝트로 실제 이전 처리와 반복 실행 시 중복 방지를 확인합니다.
            var root = new GameObject("Legacy event list migration test");
            root.SetActive(false);
            var legacy = (MonoBehaviour)root.AddComponent(director.GetType());
            try
            {
                ((IList)legacy.GetType().GetField("events", PrivateInstance).GetValue(legacy)).Add(visit);
                CallPrivate(legacy, "OnValidate");
                var entries = MainGameTestEvents.Entries(legacy);
                Assert.That(entries.Count, Is.EqualTo(1));
                Assert.That(MainGameTestEvents.Event(entries[0]), Is.SameAs(visit));
                MainGameTestEvents.SetWeight(entries[0], 75);
                ((IList)legacy.GetType().GetField("events", PrivateInstance).GetValue(legacy)).Add(visit);
                CallPrivate(legacy, "OnValidate");
                Assert.That(entries.Count, Is.EqualTo(1));
                Assert.That(Property<int>(entries[0], "Weight"), Is.EqualTo(75));
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        private string Phase => Property<object>(footsteps, "Phase").ToString();
        private Object Select(float roll) => director.GetType().GetMethod("SelectEvent", PrivateInstance).Invoke(director, new object[] { roll }) as Object;
        private void UseShortSound()
        {
            shortClip = AudioClip.Create("Footsteps cancellation test", 4410, 1, 44100, false);
            Set(footsteps, "footstepsClip", shortClip);
            Set(footsteps, "turnaroundDelay", 0.3f);
        }

        private void AssertSafe()
        {
            Assert.That(Property<bool>(footsteps, "IsDetectionActive"), Is.False);
            Assert.That(Property<bool>(door, "IsClosed"), Is.True);
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(model.position, Is.EqualTo(originalPosition));
            Assert.That(model.rotation, Is.EqualTo(originalRotation));
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
        }

        private IEnumerator WaitSafe(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) { AssertSafe(); yield return null; }
            Assert.That(condition(), Is.True, "발소리 이벤트 전환 시간이 초과되었습니다.");
            AssertSafe();
        }

        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static object Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);
        private static void CallPrivate(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "MainGame 초기화 시간이 초과되었습니다.");
        }
    }
}
