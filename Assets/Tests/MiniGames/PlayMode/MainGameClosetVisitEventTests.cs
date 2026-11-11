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
    // MainGame에 저장된 옷장과 공용 배우로 음량, 정지 지점, 문을 통한 퇴장과 발각을 검사합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameClosetVisitEventTests
    {
        private MonoBehaviour manager, director, visit, closetDoors, exitDoor, mother;
        private AudioSource sound;
        private AudioClip shortClip;
        private Transform model, firstHinge, secondHinge;
        private Quaternion firstClosed, secondClosed;
        private float volume, originalTimeScale;

        [UnitySetUp]
        public IEnumerator LoadMainGame()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            visit = Find("MotherClosetVisitEvent");
            Assert.That(visit, Is.Not.Null);
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            // 테스트 입력을 실제 키 상태에서 분리하고 다른 연출은 추첨에서 제외합니다.
            manager.enabled = false;
            Field<MiniGameController>(manager, "currentMiniGameController").KeyboardInputEnabled = false;
            Call(director, "StopEvents");
            MainGameTestEvents.UseOnly(director, visit);
            Set(manager, "isMiniGameHidden", true);
            closetDoors = Field<MonoBehaviour>(visit, "closetDoors");
            exitDoor = Field<MonoBehaviour>(visit, "exitDoor");
            mother = Field<MonoBehaviour>(visit, "mother");
            model = Field<Transform>(mother, "actorRoot");
            sound = Field<AudioSource>(visit, "closetSource");
            volume = sound.volume;
            firstHinge = Field<Transform>(closetDoors, "firstHinge");
            secondHinge = Field<Transform>(closetDoors, "secondHinge");
            firstClosed = firstHinge.localRotation;
            secondClosed = secondHinge.localRotation;
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Closet visit cleanup"));
            var scene = SceneManager.GetSceneByName("MainGame");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (shortClip != null) Object.Destroy(shortClip);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuietSoundsThenFullVolume_StopsAtSharedBedside_AndExitsThroughRoomDoor()
        {
            Assert.That(Field<AudioClip>(visit, "closetClip").name, Is.EqualTo("Closet_Sound"));
            Assert.That(sound.spatialBlend, Is.EqualTo(1f));
            Assert.That(Field<float>(visit, "revealDelay"), Is.EqualTo(1f));
            Assert.That(Field<float>(visit, "beforeRushDelay"), Is.EqualTo(0.5f));
            Assert.That(Field<float>(visit, "watchSeconds"), Is.EqualTo(1f));
            float started = Time.time;
            StartVisit();
            Assert.That(Phase, Is.EqualTo("FirstQuietSound"));
            Assert.That(sound.isPlaying, Is.True);
            Assert.That(sound.volume, Is.EqualTo(volume * 0.75f).Within(0.001f));
            Assert.That(Detecting, Is.False);
            yield return WaitFor(() => Phase == "BetweenQuietSounds");
            Assert.That(Time.time - started, Is.EqualTo(sound.clip.length).Within(0.3f));
            Assert.That(sound.isPlaying, Is.False);
            float firstEnded = Time.time;
            yield return WaitFor(() => Phase == "SecondQuietSound");
            Assert.That(Time.time - firstEnded, Is.EqualTo(Field<float>(visit, "quietSoundInterval")).Within(0.2f));
            Assert.That(sound.isPlaying, Is.True);
            Assert.That(sound.volume, Is.EqualTo(volume * 0.75f).Within(0.001f));
            Assert.That(Property<bool>(closetDoors, "IsClosed"), Is.True);
            Assert.That(model.gameObject.activeSelf, Is.False);
            yield return WaitFor(() => Phase == "BeforeReveal");
            Assert.That(Detecting, Is.False);
            float secondEnded = Time.time;
            yield return WaitFor(() => Phase == "Revealing");
            Assert.That(Time.time - secondEnded, Is.EqualTo(1f).Within(0.2f));
            Assert.That(sound.isPlaying, Is.True);
            Assert.That(sound.volume, Is.EqualTo(volume).Within(0.001f));
            Assert.That(Property<bool>(closetDoors, "IsClosed"), Is.False);
            Assert.That(model.position, Is.EqualTo(Field<Transform>(visit, "revealPoint").position));
            Assert.That(model.gameObject.activeSelf, Is.True);
            // 배우를 표시한 직후에는 문짝에 가려져 있으므로 틈이 열린 뒤에만 발각됩니다.
            Assert.That(Detecting, Is.False);
            yield return WaitFor(() => Detecting);
            Assert.That(Quaternion.Angle(firstHinge.localRotation, firstClosed), Is.GreaterThan(1f));
            float revealedAt = Time.time;
            yield return WaitFor(() => Phase == "Rushing");
            Assert.That(Time.time - revealedAt, Is.EqualTo(0.5f).Within(0.2f));
            Assert.That(Property<bool>(closetDoors, "IsOpen"), Is.True);
            Assert.That(Quaternion.Angle(firstHinge.localRotation, firstClosed), Is.GreaterThan(70f));
            Assert.That(Quaternion.Angle(secondHinge.localRotation, secondClosed), Is.GreaterThan(70f));
            yield return WaitFor(() => Phase == "Watching");
            var route = Field<Transform[]>(visit, "approachBedPath");
            var regularRoute = Field<Transform[]>(Find("MotherDoorVisitEvent"), "entryPath");
            Assert.That(route[route.Length - 1], Is.SameAs(regularRoute[regularRoute.Length - 1]));
            Assert.That(model.position, Is.EqualTo(route[route.Length - 1].position));
            Assert.That(Property<bool>(mother, "IsMoving"), Is.False);
            float watchedAt = Time.time;
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - watchedAt, Is.EqualTo(1f).Within(0.2f));
            Assert.That(Property<bool>(closetDoors, "IsOpen"), Is.False);
            yield return WaitFor(() => Phase == "Exiting");
            Assert.That(Property<bool>(exitDoor, "IsOpen"), Is.True);
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Detecting, Is.True);
            yield return WaitFor(() => !Property<bool>(visit, "IsRunning"));
            yield return null;
            AssertClean();
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
        }

        [UnityTest]
        public IEnumerator VisibleGameIsCaughtAtReveal_AndCancellationRestoresSoundAndBothDoors()
        {
            // 짧은 테스트 클립으로 두 경고음 동안 안전하고 등장 순간에만 발각되는지 확인합니다.
            shortClip = AudioClip.Create("Closet reveal test", 4410, 1, 44100, false);
            Set(visit, "closetClip", shortClip);
            Set(visit, "quietSoundInterval", 0.15f);
            Set(visit, "revealDelay", 0.15f);
            Set(manager, "isMiniGameHidden", false);
            StartVisit();
            yield return WaitFor(() => Phase == "BeforeReveal");
            Assert.That(Detecting, Is.False);
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            yield return WaitFor(() => Property<bool>(manager, "IsGameOver"));
            Assert.That(Property<object>(manager, "GameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            AssertClean();
        }

        private void AssertClean()
        {
            Assert.That(Phase, Is.EqualTo("Idle"));
            Assert.That(Detecting, Is.False);
            Assert.That(sound.isPlaying, Is.False);
            Assert.That(sound.volume, Is.EqualTo(volume).Within(0.001f));
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(closetDoors, "IsClosed"), Is.True);
            Assert.That(Property<bool>(exitDoor, "IsClosed"), Is.True);
            Assert.That(Quaternion.Angle(firstHinge.localRotation, firstClosed), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(secondHinge.localRotation, secondClosed), Is.LessThan(0.05f));
        }

        private void StartVisit() => Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
        private string Phase => Property<object>(visit, "Phase").ToString();
        private bool Detecting => Property<bool>(visit, "IsDetectionActive");
        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "옷장 이벤트의 상태 전환이 완료되지 않았습니다.");
        }
    }
}
