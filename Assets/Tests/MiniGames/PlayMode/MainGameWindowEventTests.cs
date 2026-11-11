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
    // 저장된 창문·효과음·공용 배우와 기존 게임오버 경로를 함께 검사합니다. 메인 타입의 리플렉션은 테스트 경계에서만 사용합니다.
    [Category("MainGameEventIntegration")]
    public sealed class MainGameWindowEventTests
    {
        private MonoBehaviour manager, director, windowEvent, window, mother;
        private AudioSource source;
        private Transform model, panel;
        private Vector3 closedPanelPosition;
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
            Connect();
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            // 숨김 상태를 테스트에서 지정하므로 실제 입력 장치와 공유 UI 액션은 교체하지 않습니다.
            manager.enabled = false;
            Field<MiniGameController>(manager, "currentMiniGameController").KeyboardInputEnabled = false;
            Call(director, "StopEvents");
            MainGameTestEvents.UseOnly(director, windowEvent);
            HideScreen(true);
        }

        private void Connect()
        {
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            windowEvent = Find("MotherWindowPeekEvent");
            Assert.That(windowEvent, Is.Not.Null, "MainGame에 창문 이벤트가 저장되어 있어야 합니다.");
            window = Field<MonoBehaviour>(windowEvent, "window");
            mother = Field<MonoBehaviour>(windowEvent, "mother");
            model = Field<Transform>(mother, "actorRoot");
            source = Field<AudioSource>(windowEvent, "windowSource");
            panel = Field<Transform>(window, "slidingPanel");
            closedPanelPosition = panel.localPosition;
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Window event cleanup"));
            var scene = SceneManager.GetSceneByName("MainGame");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (shortClip != null) Object.Destroy(shortClip);
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullSequence_KnocksOnly_SilentWindowMotion_HorizontalUpperBody_AndRestoresWindow()
        {
            var knock = Field<AudioClip>(windowEvent, "knockClip");
            Assert.That(knock.name, Is.EqualTo("Knock_Window"));
            Assert.That(window.gameObject.name, Is.EqualTo("wallWindowSlide"));
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            // 사용자가 Inspector에서 조절한 값을 보존하고, 특정 기본값 대신 실제 설정 시간의 적용을 검증합니다.
            float firstDelay = Field<float>(windowEvent, "afterFirstKnockDelay");
            float secondDelay = Field<float>(windowEvent, "afterSecondKnockDelay");
            float watchSeconds = Field<float>(windowEvent, "watchSeconds");
            Assert.That(Field<MonoBehaviour>(Find("MotherDoorVisitEvent"), "mother"), Is.SameAs(mother));
            float started = Time.time;
            StartWindow();
            Assert.That(Phase, Is.EqualTo("FirstKnock"));
            Assert.That(source.clip, Is.SameAs(knock));
            yield return WaitFor(() => Phase == "AfterFirstKnock");
            Assert.That(Time.time - started, Is.EqualTo(knock.length / source.pitch).Within(0.25f));
            Assert.That(source.isPlaying, Is.False);
            float firstEnded = Time.time;
            yield return WaitFor(() => Phase == "SecondKnock");
            Assert.That(Time.time - firstEnded, Is.EqualTo(firstDelay).Within(0.12f));
            Assert.That(source.clip, Is.SameAs(knock));
            yield return WaitFor(() => Phase == "AfterSecondKnock");
            float secondEnded = Time.time;
            yield return WaitFor(() => Phase == "Opening");
            Assert.That(Time.time - secondEnded, Is.EqualTo(secondDelay).Within(0.12f));
            Assert.That(source.clip, Is.SameAs(knock));
            Assert.That(source.isPlaying, Is.False, "창문을 여는 동안에는 효과음을 재생하지 않습니다.");
            Assert.That(Detecting, Is.False);
            Assert.That(model.gameObject.activeSelf, Is.False);
            float openingAt = Time.time;
            yield return WaitFor(() => Property<float>(window, "Openness") > 0.15f);
            Assert.That(panel.localPosition.y, Is.GreaterThan(closedPanelPosition.y));
            Assert.That(panel.localPosition.x, Is.EqualTo(closedPanelPosition.x));
            yield return WaitFor(() => Phase == "Entering");
            Assert.That(Time.time - openingAt, Is.EqualTo(Field<float>(windowEvent, "windowOpenSeconds")).Within(0.15f));
            Assert.That(Property<bool>(window, "IsOpen"), Is.True);
            Assert.That(Detecting, Is.True);
            Assert.That(model.gameObject.activeSelf, Is.True);
            yield return WaitFor(() => Phase == "Watching");
            Assert.That(Vector3.Distance(model.position, Field<Transform>(windowEvent, "peekPoint").position), Is.LessThan(0.001f));
            Assert.That(Property<bool>(mother, "IsMoving"), Is.False);
            // 루트의 세로축이 수평이 되고 머리·상체가 벽의 방 안쪽 면을 넘어야, 창밖에 서 있는 이전 배치로 회귀하지 않습니다.
            Assert.That(Mathf.Abs(model.up.y), Is.LessThan(0.05f));
            var animator = model.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            float innerWindowZ = window.GetComponent<Renderer>().bounds.max.z;
            Assert.That(head.position.z, Is.GreaterThan(innerWindowZ));
            Assert.That(chest.position.z, Is.GreaterThan(innerWindowZ));
            var view = GameObject.Find("Base/Main Camera").GetComponent<Camera>();
            view.transform.localRotation = Quaternion.Euler(0f, 113f, 0f);
            Assert.That(view.WorldToViewportPoint(head.position).x, Is.LessThan(view.WorldToViewportPoint(chest.position).x));
            Assert.That(Vector3.Dot(model.forward, (view.transform.position - head.position).normalized), Is.GreaterThan(0.8f));
            float watchedAt = Time.time;
            yield return WaitFor(() => Phase == "Leaving");
            Assert.That(Time.time - watchedAt, Is.EqualTo(watchSeconds).Within(0.12f));
            Assert.That(Detecting, Is.True);
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(source.clip, Is.SameAs(knock));
            Assert.That(source.isPlaying, Is.False, "창문을 닫는 동안에도 효과음을 재생하지 않습니다.");
            Assert.That(Detecting, Is.True);
            yield return WaitFor(() => !Property<bool>(windowEvent, "IsRunning"));
            yield return null;
            AssertClean();
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Waiting"));
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.InRange(29f, 120f));
        }

        [UnityTest]
        public IEnumerator VisibleScreen_IsCaughtOnAppearance_UsesRetryAndRecreatesScene()
        {
            UseShortSequence();
            HideScreen(false);
            StartWindow();
            yield return WaitFor(() => Property<bool>(manager, "IsGameOver"));
            Assert.That(Property<object>(manager, "GameOverReason").ToString(), Is.EqualTo("CaughtByMother"));
            Assert.That(Field<GameObject>(manager, "gameOverOverlay").activeSelf, Is.True);
            AssertClean();
            Assert.That(Time.timeScale, Is.Zero);
            var oldManager = manager;
            Call(manager, "RestartMainGame");
            yield return WaitFor(() => Find("MiniGameManager") != null && Find("MiniGameManager") != oldManager);
            Connect();
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            Call(director, "StopEvents");
            Assert.That(Property<int>(manager, "CurrentLives"), Is.EqualTo(3));
            Assert.That(Property<int>(manager, "CurrentClearedCount"), Is.Zero);
            Assert.That(Property<bool>(windowEvent, "CanRun"), Is.True);
            Assert.That(Property<bool>(window, "IsClosed"), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ClosingWindow_RemainsDangerousAfterMotherDisappears()
        {
            UseShortSequence();
            StartWindow();
            yield return WaitFor(() => Phase == "Closing");
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(window, "IsClosed"), Is.False);
            Assert.That(Detecting, Is.True);
            HideScreen(false);
            yield return null;
            yield return null;
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.True);
            AssertClean();
        }

        [UnityTest]
        public IEnumerator WarningIsSafe_ClosedWindowIsSafe_AndLocalPauseIsPreserved()
        {
            UseShortSequence();
            var game = Field<MiniGameController>(manager, "currentMiniGameController");
            game.StartGame();
            game.PauseGame();
            HideScreen(false);
            StartWindow();
            yield return WaitFor(() => Phase == "Opening");
            Assert.That(Detecting, Is.False);
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            HideScreen(true);
            yield return WaitFor(() => !Property<bool>(windowEvent, "IsRunning"));
            HideScreen(false);
            yield return null;
            yield return null;
            Assert.That(Property<bool>(manager, "IsGameOver"), Is.False);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            AssertClean();
        }

        [UnityTest]
        public IEnumerator CancellationAtEveryPhase_RestoresSoundModelAndWindow_AndAllowsDoorVisit()
        {
            UseShortSequence();
            foreach (var phase in new[] { "FirstKnock", "AfterFirstKnock", "SecondKnock", "AfterSecondKnock", "Opening", "Entering", "Watching", "Leaving", "Closing" })
            {
                StartWindow();
                yield return WaitFor(() => Phase == phase);
                Call(director, "StopEvents");
                AssertClean();
                yield return null;
                AssertClean();
            }
            // 개발자 도구와 같은 즉시 실행 경로에서 기존 창문 이벤트가 정리되고 공용 배우를 문 이벤트가 다시 사용할 수 있어야 합니다.
            StartWindow();
            yield return WaitFor(() => Phase == "Watching");
            var visit = Find("MotherDoorVisitEvent");
            MainGameTestEvents.UseOnly(director, visit);
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
            AssertClean();
            Assert.That(Property<Object>(director, "ActiveEvent"), Is.SameAs(visit));
            Assert.That(Field<AudioSource>(visit, "footstepsSource").isPlaying, Is.True);
            // 수평으로 눕힌 창문 자세가 다음 문 방문에 남지 않는지도 공용 배우의 실제 진입 시점에서 확인합니다.
            Set(visit, "footstepsClip", shortClip);
            Set(visit, "afterFootstepsDelay", 0f);
            Set(visit, "detectionDelay", 30f);
            Set(Find("DoorController"), "openDuration", 0.1f);
            Call(director, "StopEvents");
            Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
            yield return WaitFor(() => Property<object>(visit, "Phase").ToString() == "Entering");
            Assert.That(Vector3.Dot(model.up, Vector3.up), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator SharedAppearance_DarkensEveryMaterial_PreservingSourceColorsAndAlpha()
        {
            var appearance = model.GetComponent(Type.GetType("MotherAppearance, Assembly-CSharp", true)) as MonoBehaviour;
            Assert.That(appearance, Is.Not.Null);
            var profile = Property<ScriptableObject>(appearance, "Profile");
            Assert.That(profile, Is.Not.Null);
            var tint = Property<Color>(profile, "Tint");
            Assert.That(tint.r, Is.LessThanOrEqualTo(0.15f));
            Call(mother, "ShowAt", Field<Transform>(windowEvent, "peekPoint"));
            yield return null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    int colorId = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    if (!material.HasProperty(colorId)) continue;
                    Color original = material.GetColor(colorId);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, index);
                    Color displayed = block.GetColor(colorId);
                    Assert.That(displayed.r, Is.EqualTo(original.r * tint.r).Within(0.001f));
                    Assert.That(displayed.g, Is.EqualTo(original.g * tint.g).Within(0.001f));
                    Assert.That(displayed.b, Is.EqualTo(original.b * tint.b).Within(0.001f));
                    Assert.That(displayed.a, Is.EqualTo(original.a));
                    Assert.That(block.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(Property<float>(profile, "MaximumSmoothness")));
                    Assert.That(material.GetColor(colorId), Is.EqualTo(original));
                }
            }
            Call(mother, "Hide");
        }

        private void UseShortSequence()
        {
            // 타이밍 순서의 전체 원본 검증은 첫 테스트에서 수행하고, 종료·발각 조합은 짧은 테스트 클립으로 검증합니다.
            shortClip = AudioClip.Create("Window event transition test", 6615, 1, 44100, false);
            Set(windowEvent, "knockClip", shortClip);
            Set(windowEvent, "windowOpenSeconds", 0.15f);
            Set(windowEvent, "windowCloseSeconds", 0.15f);
            Set(windowEvent, "afterFirstKnockDelay", 0.1f);
            Set(windowEvent, "afterSecondKnockDelay", 0.12f);
            Set(windowEvent, "watchSeconds", 0.15f);
            Set(windowEvent, "peekTransitionSeconds", 0.08f);
        }

        private void StartWindow() => Assert.That((bool)Call(director, "TryRunNextEventImmediately"), Is.True);
        private string Phase => Property<object>(windowEvent, "Phase").ToString();
        private bool Detecting => Property<bool>(windowEvent, "IsDetectionActive");
        private void HideScreen(bool hidden) => manager.GetType().GetMethod("SetMiniGameHidden", PrivateInstance).Invoke(manager, new object[] { hidden });

        private void AssertClean()
        {
            Assert.That(Detecting, Is.False);
            Assert.That(Property<bool>(windowEvent, "IsRunning"), Is.False);
            Assert.That(Phase, Is.EqualTo("Idle"));
            Assert.That(source.isPlaying, Is.False);
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(Property<bool>(window, "IsClosed"), Is.True);
            Assert.That(Vector3.Distance(panel.localPosition, closedPanelPosition), Is.LessThan(0.0001f));
        }

        private static MonoBehaviour Find(string name) => Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp", true)) as MonoBehaviour;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name).Invoke(target, arguments);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "창문 이벤트의 상태 전환 시간이 초과되었습니다.");
        }
    }
}
