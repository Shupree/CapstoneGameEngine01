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
    // 저장된 모니터 슬롯, 공유 배우와 Door_7을 통해 컴퓨터 이벤트의 실제 시간과 발각 경계를 확인합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameComputerVisitEventTests
    {
        private MonoBehaviour manager, director, visit, mother, exitDoor, closet;
        private Renderer monitor;
        private AudioSource sound;
        private Transform model;
        private Material[] originalMaterials;
        private Vector3 originalScale;
        private float originalTimeScale;

        [UnitySetUp]
        public IEnumerator LoadMainGame()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            visit = Find("MotherComputerVisitEvent");
            closet = Find("ClosetDoors");
            Assert.That(visit, Is.Not.Null);
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            // 입력을 고정해 각 발각 경계에서 게임기의 가시성을 재현합니다.
            manager.enabled = false;
            Field<MiniGameController>(manager, "currentMiniGameController").KeyboardInputEnabled = false;
            Call(director, "StopEvents");
            Set(manager, "isMiniGameHidden", true);
            mother = Field<MonoBehaviour>(visit, "mother");
            exitDoor = Field<MonoBehaviour>(visit, "exitDoor");
            sound = Field<AudioSource>(visit, "computerSource");
            monitor = Field<Renderer>(visit, "monitorRenderer");
            model = Field<Transform>(mother, "actorRoot");
            originalMaterials = monitor.sharedMaterials;
            originalScale = model.localScale;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (director != null) Call(director, "StopEvents");
            if (manager != null)
            {
                Call(manager, "CloseMiniGameImmediately");
                yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
                Object.Destroy(manager.gameObject);
            }
            var menu = Find("MenuManager");
            if (menu != null) Object.Destroy(menu.gameObject);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Computer visit cleanup"));
            var scene = SceneManager.GetSceneByName("MainGame");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarningThenCrawling_GrowsAndStands_WatchesAndLeavesThroughTheDoor()
        {
            Set(mother, "moveSpeed", 8f);
            int screenSlot = Field<int>(visit, "screenMaterialSlot");
            Assert.That(Field<AudioClip>(visit, "computerClip").name, Is.EqualTo("Computer_Noise"));
            Assert.That(sound.spatialBlend, Is.EqualTo(1f));
            Assert.That(Field<float>(visit, "noiseSeconds"), Is.EqualTo(1.5f));
            // 관찰 시간은 저장된 Inspector 값이 실제 연출에 반영되는지 검사합니다.
            float watchSeconds = Field<float>(visit, "watchSeconds");
            StartVisit(visit);
            float warnedAt = Time.time;
            Assert.That(Phase, Is.EqualTo("Warning"));
            Assert.That(Detecting, Is.False);
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(sound.isPlaying, Is.True);
            Assert.That(monitor.sharedMaterials[screenSlot], Is.SameAs(Field<Material>(visit, "warningScreenMaterial")));
            Assert.That(monitor.sharedMaterials[0], Is.SameAs(originalMaterials[0]));

            yield return WaitFor(() => Phase == "Crawling");
            Assert.That(Time.time - warnedAt, Is.EqualTo(1.5f).Within(0.25f));
            Assert.That(Detecting && model.gameObject.activeSelf, Is.True);
            Assert.That(sound.isPlaying, Is.False);
            CollectionAssert.AreEqual(originalMaterials, monitor.sharedMaterials);
            Assert.That(model.position, Is.EqualTo(Field<Transform>(visit, "monitorOpening").position));
            float firstScale = model.localScale.x;
            Assert.That(firstScale, Is.LessThan(originalScale.x * 0.25f));
            // 실제 관절 회전이 변하는지 확인해 모델 전체가 미끄러지기만 하는 회귀를 잡습니다.
            var animator = Field<Animator>(mother, "animator");
            var forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Quaternion firstPose = forearm.localRotation;
            yield return new WaitForSeconds(0.4f);
            Assert.That(model.localScale.x, Is.InRange(firstScale + 0.01f, originalScale.x - 0.01f));
            Assert.That(Quaternion.Angle(firstPose, forearm.localRotation), Is.GreaterThan(5f));

            yield return WaitFor(() => Phase == "Standing");
            Assert.That(model.position, Is.EqualTo(Field<Transform>(visit, "deskEdge").position));
            Assert.That(model.localScale, Is.EqualTo(originalScale));
            yield return WaitFor(() => Phase == "Approaching");
            Assert.That(Vector3.Distance(model.position, Field<Transform>(visit, "standInFrontOfDesk").position), Is.LessThan(0.4f));
            Assert.That(Vector3.Angle(model.up, Vector3.up), Is.LessThan(0.1f));
            yield return WaitFor(() => Phase == "Watching");
            var route = Field<Transform[]>(visit, "approachBedPath");
            Assert.That(model.position, Is.EqualTo(route[route.Length - 1].position));
            float watchedAt = Time.time;
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - watchedAt, Is.EqualTo(watchSeconds).Within(0.25f));
            Assert.That(Property<bool>(closet, "IsClosed"), Is.True);
            yield return WaitFor(() => Phase == "Exiting");
            Assert.That(Property<bool>(exitDoor, "IsOpen"), Is.True);
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Detecting, Is.True);
            yield return WaitFor(() => !Property<bool>(visit, "IsRunning"));
            AssertClean();
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
        }

        [UnityTest]
        public IEnumerator VisibleGameIsSafeDuringWarning_ThenOpensRetryOnEmergence()
        {
            Set(visit, "noiseSeconds", 0.2f);
            Set(manager, "isMiniGameHidden", false);
            StartVisit(visit);
            yield return null;
            Assert.That(Phase, Is.EqualTo("Warning"));
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            yield return WaitFor(() => Property<bool>(manager, "IsGameOver"));
            Assert.That(Property<object>(manager, "GameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            Assert.That(Field<GameObject>(manager, "gameOverOverlay").activeSelf, Is.True);
            AssertClean();
        }

        [UnityTest]
        public IEnumerator SwitchingEventsDuringWarningOrCrawling_RestoresSharedResources()
        {
            StartVisit(visit);
            Call(director, "StopEvents");
            AssertClean();

            Set(visit, "noiseSeconds", 0f);
            StartVisit(visit);
            yield return WaitFor(() => Phase == "Crawling");
            yield return null;
            Assert.That(model.localScale.x, Is.LessThan(originalScale.x));
            // 다른 이벤트가 공용 배우를 사용할 수 있도록 같은 프레임에 크기와 자세가 복원되어야 합니다.
            StartVisit(Find("MotherDoorVisitEvent"));
            AssertClean();
        }

        private void AssertClean()
        {
            Assert.That(Phase, Is.EqualTo("Idle"));
            Assert.That(Detecting, Is.False);
            Assert.That(sound.isPlaying, Is.False);
            CollectionAssert.AreEqual(originalMaterials, monitor.sharedMaterials);
            Assert.That(model.localScale, Is.EqualTo(originalScale));
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(exitDoor, "IsClosed"), Is.True);
        }

        private void StartVisit(MonoBehaviour selected) => Assert.That((bool)Call(director, "TryRunEventForDevelopment", selected), Is.True);
        private string Phase => Property<object>(visit, "Phase").ToString();
        private bool Detecting => Property<bool>(visit, "IsDetectionActive");
        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static FieldInfo FieldInfo(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static T Field<T>(object target, string name) => (T)FieldInfo(target, name).GetValue(target);
        private static void Set(object target, string name, object value) => FieldInfo(target, name).SetValue(target, value);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "컴퓨터 이벤트의 상태 전환 시간이 초과되었습니다.");
        }
    }
}
