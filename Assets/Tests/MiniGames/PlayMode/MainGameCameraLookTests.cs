using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 MainGame 카메라와 실제 Shift·화살표 입력을 함께 검사합니다. 메인 어셈블리 경계는 테스트에서만 리플렉션으로 넘습니다.
    public sealed class MainGameCameraLookTests : InputTestFixture
    {
        private MonoBehaviour manager, director, look;
        private Camera camera;
        private Keyboard keyboard;
        private float originalTimeScale;
        private readonly Dictionary<InputActionAsset, InputDevice[]> actionDevices = new Dictionary<InputActionAsset, InputDevice[]>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            // 씬 UI의 공유 액션도 테스트 장치로 제한하고 종료 때 원래 필터를 복원합니다.
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
            Call(director, "StopEvents");
            look = Find("MainGameCameraLookController");
            Assert.That(look, Is.Not.Null, "MainGame 카메라에 저장된 시선 컴포넌트가 필요합니다.");
            camera = look.GetComponent<Camera>();
            Assert.That(look.transform.parent.name, Is.EqualTo("Base"));
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Camera look cleanup"));
            foreach (var name in new[] { "MainGame", "MiniGame01", "MiniGame02", "MiniGame03", "MiniGame04" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenArrowInput_MovesSmoothly_ClampsAndRetainsLastAngle()
        {
            var start = camera.transform.localRotation;
            Vector3 position = camera.transform.position;
            float fieldOfView = camera.fieldOfView;
            var miniCamera = Field<Camera>(manager, "currentMiniGameCamera");
            Quaternion miniRotation = miniCamera.transform.rotation;
            float miniFieldOfView = miniCamera.fieldOfView;
            yield return Keys(Key.LeftArrow);
            AssertAngle(camera.transform.localRotation, start);
            Assert.That(Property<bool>(look, "CanLook"), Is.False);

            // 한 프레임에 끝까지 점프하지 않고, 누르고 있는 시간에 따라 이동하는지 확인합니다.
            Set(look, "rotationSpeedDegreesPerSecond", 50f);
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.InRange(-0.8f, -0.001f));
            Set(look, "rotationSpeedDegreesPerSecond", 500f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.EqualTo(-1f));
            var left = camera.transform.localRotation;
            float leftFieldOfView = camera.fieldOfView;
            Assert.That(leftFieldOfView, Is.EqualTo(fieldOfView));
            yield return new WaitForSeconds(0.2f);
            AssertAngle(camera.transform.localRotation, left);
            Assert.That(camera.fieldOfView, Is.EqualTo(leftFieldOfView));
            yield return Keys(Key.LeftShift);
            AssertAngle(camera.transform.localRotation, left);
            yield return Keys(Key.RightArrow);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.False);
            AssertAngle(camera.transform.localRotation, left);

            // 오른쪽 Shift도 같은 숨김 계약을 따르며, 반대쪽 끝에서도 더 이상 회전하지 않습니다.
            yield return Keys(Key.RightShift, Key.RightArrow);
            yield return new WaitForSeconds(0.4f);
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.EqualTo(1f));
            var right = camera.transform.localRotation;
            yield return Keys(Key.RightShift, Key.LeftArrow, Key.RightArrow);
            AssertAngle(camera.transform.localRotation, right);
            yield return Keys();
            AssertAngle(camera.transform.localRotation, right);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
            AssertAngle(miniCamera.transform.rotation, miniRotation);
            Assert.That(miniCamera.fieldOfView, Is.EqualTo(miniFieldOfView));
        }

        [UnityTest]
        public IEnumerator ArrowDirections_TurnHorizontally_WithoutChangingPositionPitchRollOrFieldOfView()
        {
            // 입력 이름·진행량뿐 아니라 최초 시선의 좌우 벡터와 비교해 실제 회전 방향도 검증합니다.
            camera.aspect = 16f / 9f;
            Vector3 initialPosition = camera.transform.position;
            Vector3 initialAngles = camera.transform.localEulerAngles;
            Vector3 initialRight = camera.transform.right;
            float fieldOfView = camera.fieldOfView;
            Set(look, "rotationSpeedDegreesPerSecond", 500f);
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            AssertHorizontalPose(initialPosition, initialAngles, fieldOfView);
            Assert.That(Vector3.Dot(camera.transform.forward, initialRight), Is.LessThan(0f), "왼쪽 키는 최초 시선의 왼쪽으로 돌아야 합니다.");
            yield return new WaitForSeconds(0.3f);
            AssertHorizontalPose(initialPosition, initialAngles, fieldOfView);
            foreach (var path in new[] { "Map/desk", "Map/computerScreen" })
                foreach (var renderer in GameObject.Find(path).GetComponentsInChildren<Renderer>())
                    AssertInView(renderer.bounds);
            yield return Keys(Key.LeftShift, Key.RightArrow);
            yield return new WaitForSeconds(0.4f);
            Assert.That(Vector3.Dot(camera.transform.forward, initialRight), Is.GreaterThan(0f), "오른쪽 키는 최초 시선의 오른쪽으로 돌아야 합니다.");
            AssertHorizontalPose(initialPosition, initialAngles, fieldOfView);
            // 창문 방향의 수평 한계만 유지합니다. 사용자 요청에 따라 창틀 위아래를 맞추기 위한 보정은 검사하지 않습니다.
            foreach (var glass in GameObject.Find("Map/wallWindowSlide").GetComponentsInChildren<Renderer>()
                .Where(item => item.name == "windowBottom" || item.name == "windowTop"))
            {
                Vector3 viewport = camera.WorldToViewportPoint(glass.bounds.center);
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.InRange(0f, 1f));
            }
        }

        [UnityTest]
        public IEnumerator UnequalLimits_UseTheSameAngularSpeed_InBothDirectionsAndAcrossCenter()
        {
            // 실제 카메라 각도와 경과 시간으로 측정해, 정규화 진행량만 같고 회전 속도는 다른 회귀를 방지합니다.
            float expectedSpeed = Field<float>(look, "rotationSpeedDegreesPerSecond");
            float initialYaw = camera.transform.localEulerAngles.y;
            float leftSpeed = 0f, crossingSpeed = 0f, rightSpeed = 0f;
            Assert.That(Mathf.Abs(Field<float>(look, "leftYawOffset")), Is.GreaterThan(Field<float>(look, "rightYawOffset")));
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            yield return MeasureAngularSpeed(0.12f, value => leftSpeed = value);
            yield return Keys(Key.LeftShift, Key.RightArrow);
            Assert.That(Mathf.DeltaAngle(initialYaw, camera.transform.localEulerAngles.y), Is.LessThan(0f));
            yield return MeasureAngularSpeed(0.28f, value => crossingSpeed = value);
            Assert.That(Mathf.DeltaAngle(initialYaw, camera.transform.localEulerAngles.y), Is.GreaterThan(0f));
            yield return MeasureAngularSpeed(0.12f, value => rightSpeed = value);
            // 코루틴과 LateUpdate의 한 프레임 측정 경계 차이만 허용합니다. 기존 약 40.69도/초의 오른쪽 속도는 통과하지 못합니다.
            float tolerance = Mathf.Max(1f, expectedSpeed * 0.08f);
            Assert.That(leftSpeed, Is.EqualTo(expectedSpeed).Within(tolerance), "책상 방향의 기존 각속도를 유지해야 합니다.");
            Assert.That(crossingSpeed, Is.EqualTo(expectedSpeed).Within(tolerance), "정면을 통과해도 속도가 바뀌면 안 됩니다.");
            Assert.That(rightSpeed, Is.EqualTo(leftSpeed).Within(tolerance), "창문 방향도 책상 방향과 같은 각속도여야 합니다.");
        }

        [UnityTest]
        public IEnumerator LookingWhileTabPaused_PreservesPause_AndMainGameClockKeepsRunning()
        {
            yield return Keys(Key.Space);
            yield return Keys();
            yield return Keys(Key.Tab);
            var miniGame = Field<MiniGameController>(manager, "currentMiniGameController");
            Assert.That(miniGame.State, Is.EqualTo(MiniGameState.Paused));
            // 카메라 조작 중에도 기존 Director의 다음 이벤트 대기가 실제로 감소해야 합니다.
            CallPrivate(director, "ScheduleNextEvent");
            float remaining = Property<float>(director, "SecondsUntilNextEvent");
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.LessThan(0f));
            Assert.That(miniGame.State, Is.EqualTo(MiniGameState.Paused));
            Assert.That(Property<float>(director, "SecondsUntilNextEvent"), Is.LessThan(remaining - 0.15f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return Keys();
            Assert.That(miniGame.State, Is.EqualTo(MiniGameState.Paused));
        }

        [UnityTest]
        public IEnumerator MiniGameReplacement_PreservesLookWhileScreenIsHidden()
        {
            Set(look, "rotationSpeedDegreesPerSecond", 500f);
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            yield return new WaitForSeconds(0.3f);
            yield return Keys(Key.LeftShift);
            var saved = camera.transform.localRotation;
            Assert.That((bool)Call(manager, "TryLoadMiniGameForDevelopment", "MiniGame01"), Is.True);
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.True);
            AssertAngle(camera.transform.localRotation, saved);
            yield return Keys();
            AssertAngle(camera.transform.localRotation, saved);
            Assert.That(Field<MiniGameController>(manager, "currentMiniGameController").State, Is.EqualTo(MiniGameState.Ready));
        }

        [UnityTest]
        public IEnumerator GameOver_BlocksLooking_AndRetryCreatesOriginalView()
        {
            Quaternion original = camera.transform.localRotation;
            float originalFieldOfView = camera.fieldOfView;
            Set(look, "rotationSpeedDegreesPerSecond", 500f);
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            yield return new WaitForSeconds(0.3f);
            Quaternion last = camera.transform.localRotation;
            var reason = Type.GetType("EGameOverReason, Assembly-CSharp", true);
            Call(manager, "TriggerGameOver", Enum.Parse(reason, "CaughtByMother"));
            yield return Keys(Key.LeftShift, Key.RightArrow);
            Assert.That(Property<bool>(look, "CanLook"), Is.False);
            AssertAngle(camera.transform.localRotation, last);
            yield return Keys();
            Call(manager, "RestartMainGame");
            var oldManager = manager;
            yield return WaitFor(() => Find("MiniGameManager") != null && Find("MiniGameManager") != oldManager);
            manager = Find("MiniGameManager");
            director = Find("MainGameEventDirector");
            Call(director, "StopEvents");
            look = Find("MainGameCameraLookController");
            camera = look.GetComponent<Camera>();
            AssertAngle(camera.transform.localRotation, original);
            Assert.That(camera.fieldOfView, Is.EqualTo(originalFieldOfView));
            Assert.That(Property<float>(look, "CurrentLookAmount"), Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // 중간 회전과 양쪽 끝 모두 최초 상하 각도·기울기·위치·FOV가 유지되어야 합니다.
        private void AssertHorizontalPose(Vector3 position, Vector3 angles, float fieldOfView)
        {
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.localEulerAngles.x, angles.x)), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.localEulerAngles.z, angles.z)), Is.LessThan(0.01f));
            Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
        }

        private void AssertInView(Bounds bounds)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                AssertPointInView(point);
            }
        }

        private void AssertPointInView(Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(viewport.x, Is.InRange(0f, 1f), "대상 좌우가 화면 밖으로 잘렸습니다.");
            Assert.That(viewport.y, Is.InRange(0f, 1f), "대상 위아래가 화면 밖으로 잘렸습니다.");
        }

        // 실제 입력을 유지한 채 프레임을 진행시켜 끝점에 닿지 않은 구간의 초당 회전 각도를 구합니다.
        private IEnumerator MeasureAngularSpeed(float seconds, Action<float> measured)
        {
            float startTime = Time.time;
            float startYaw = camera.transform.localEulerAngles.y;
            yield return new WaitForSeconds(seconds);
            measured(Mathf.Abs(Mathf.DeltaAngle(startYaw, camera.transform.localEulerAngles.y)) / (Time.time - startTime));
        }

        // 입력 이벤트는 실제 프레임에서 처리시켜 매니저 Update → 시선 LateUpdate 순서도 검증합니다.
        private IEnumerator Keys(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private static void AssertAngle(Quaternion actual, Quaternion expected) => Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f));
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
            Assert.That(condition(), Is.True, "카메라 검증의 상태 전환 시간이 초과되었습니다.");
        }
    }
}
