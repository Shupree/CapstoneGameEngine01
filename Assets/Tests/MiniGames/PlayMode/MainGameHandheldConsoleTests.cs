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
    // 저장된 MainGame에서 1인칭 게임기(HandheldConsoleRig)가 미니게임 viewport에 맞춰지고,
    // Shift 숨김에 따라 게임기·손과 침대 위 쉬는 팔이 서로 바뀌는지 검사합니다. 메인 타입은 테스트 경계에서만 리플렉션으로 읽습니다.
    public sealed class MainGameHandheldConsoleTests : InputTestFixture
    {
        private MonoBehaviour manager, director, view;
        private Camera camera;
        private Keyboard keyboard;
        private float originalTimeScale;
        private readonly Dictionary<InputActionAsset, InputDevice[]> actionDevices = new Dictionary<InputActionAsset, InputDevice[]>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            // 공유 입력 액션을 테스트 키보드로 제한하고 종료 시 원래 장치 필터를 복원합니다.
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
            view = Find("HandheldConsoleView");
            Assert.That(view, Is.Not.Null, "MainGame의 Main Camera 아래에 HandheldConsoleRig가 저장되어 있어야 합니다.");
            camera = view.transform.parent.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null, "게임기 리그는 Main Camera의 자식이어야 카메라를 따라 회전합니다.");
            yield return WaitFor(() => !Property<bool>(manager, "IsTransitioning"));
            yield return null;
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Handheld console cleanup"));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && (scene.name == "MainGame" || scene.name.StartsWith("MiniGame")))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Shown_ScreenCoversMiniGameViewport_AndRestingArmsAreHidden()
        {
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.False);
            AssertConsoleVisible(true);
            AssertScreenCoversViewport();
            yield break;
        }

        [UnityTest]
        public IEnumerator Shift_LowersConsoleThenSwapsToRestingArms_AndRaisesAlignedAfterLooking()
        {
            Rect rect = Property<Rect>(manager, "MiniGameViewportRect");
            // 프레임 시간이 길어져도 중간 상태를 확인할 수 있게 이 테스트에서만 내리는 시간을 늘립니다.
            Set(view, "lowerSeconds", 1f);
            yield return Keys(Key.LeftShift);
            // 숨김 판정은 즉시 바뀌지만, 게임기는 바로 사라지지 않고 아래로 내려가는 중이어야 합니다.
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.True);
            Assert.That(Property<float>(view, "LoweredAmount"), Is.InRange(0.001f, 0.999f), "Shift 직후에는 게임기를 내리는 중이어야 합니다.");
            yield return WaitFor(() => Property<float>(view, "LoweredAmount") >= 0.4f);
            Assert.That(Property<float>(view, "LoweredAmount"), Is.LessThan(1f));
            AssertConsoleVisible(true);
            Assert.That(ScreenViewportBounds().center.y, Is.LessThan(rect.center.y - 0.05f), "내리는 동안 게임기 화면이 아래로 움직여야 합니다.");

            yield return WaitFor(() => Property<float>(view, "LoweredAmount") >= 1f);
            AssertConsoleVisible(false);

            // 숨긴 동안 시선을 돌린 뒤 화면을 다시 올려도 게임기가 카메라를 따라와 viewport와 겹쳐야 합니다.
            Quaternion before = camera.transform.localRotation;
            yield return Keys(Key.LeftShift, Key.LeftArrow);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Quaternion.Angle(before, camera.transform.localRotation), Is.GreaterThan(1f), "숨김 중 화살표로 시선이 돌아가야 합니다.");

            // 올리기 시작하면 쉬는 팔을 바로 감추고 게임기를 다시 보여 줍니다.
            yield return Keys();
            Assert.That(Property<bool>(manager, "IsMiniGameHidden"), Is.False);
            AssertConsoleVisible(true);
            yield return WaitFor(() => Property<float>(view, "LoweredAmount") <= 0f);
            AssertScreenCoversViewport();
        }

        [UnityTest]
        public IEnumerator Shift_TurnsScreenGlowOffImmediately_AndFadesBackIn()
        {
            var glow = Find("MiniGameScreenGlow");
            Assert.That(glow, Is.Not.Null, "MainGame에 게임기 화면 빛(MiniGameScreenGlow)이 있어야 합니다.");
            var light = glow.GetComponent<Light>();
            yield return WaitFor(() => light.intensity > 0.1f);

            // 숨기는 프레임에 바로 꺼져야 합니다(서서히 줄어들지 않음).
            yield return Keys(Key.LeftShift);
            Assert.That(light.intensity, Is.LessThan(0.001f), "Shift를 누르면 화면 빛이 즉시 꺼져야 합니다.");
            Assert.That(light.enabled, Is.False);

            // 다시 꺼낼 때는 기존처럼 서서히 켜집니다.
            yield return Keys();
            Assert.That(light.intensity, Is.GreaterThan(0f));
            yield return WaitFor(() => light.intensity > 0.1f);
        }

        [UnityTest]
        public IEnumerator Raising_DrawsMiniGameOnConsoleScreen_ThenReturnsToViewport()
        {
            var screen = Field<Renderer>(view, "screenRenderer");
            Material restingMaterial = screen.sharedMaterial;
            Camera miniCamera = FindMiniGameCamera();
            Assert.That(miniCamera, Is.Not.Null, "MainGame 시작 시 미니게임이 로드되어 있어야 합니다.");
            Rect rect = miniCamera.rect;

            yield return Keys(Key.LeftShift);
            yield return WaitFor(() => Property<float>(view, "LoweredAmount") >= 1f);
            // 숨김 중에는 매니저가 카메라를 끄므로 옮겨 그리지 않고 꺼진 화면을 둡니다.
            Assert.That(Property<bool>(view, "IsScreenRoutedToConsole"), Is.False);
            Assert.That(miniCamera.targetTexture, Is.Null);

            // 올리는 동안에는 미니게임 카메라 출력이 게임기 화면 텍스처로 들어가 게임기를 따라 움직입니다.
            Set(view, "raiseSeconds", 1f);
            yield return Keys();
            yield return WaitFor(() => Property<float>(view, "LoweredAmount") < 0.9f);
            Assert.That(Property<bool>(view, "IsScreenRoutedToConsole"), Is.True);
            Assert.That(miniCamera.targetTexture, Is.Not.Null);
            Assert.That(screen.sharedMaterial.mainTexture, Is.SameAs(miniCamera.targetTexture));
            Assert.That(miniCamera.rect, Is.EqualTo(rect), "매니저가 정한 viewport rect는 바꾸지 않아야 합니다.");

            // 제자리에 오면 원래대로 화면에 직접 그리고 게임기 화면 머티리얼도 돌아옵니다.
            yield return WaitFor(() => Property<float>(view, "LoweredAmount") <= 0f);
            yield return null;
            Assert.That(Property<bool>(view, "IsScreenRoutedToConsole"), Is.False);
            Assert.That(miniCamera.targetTexture, Is.Null);
            Assert.That(screen.sharedMaterial, Is.SameAs(restingMaterial));
            Assert.That(miniCamera.enabled, Is.True);
        }

        private static Camera FindMiniGameCamera()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.name.StartsWith("MiniGame")) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var camera = root.GetComponentInChildren<Camera>();
                    if (camera != null) return camera;
                }
            }
            return null;
        }

        // 게임기·손 렌더러와 쉬는 팔은 항상 반대 상태여야 합니다.
        private void AssertConsoleVisible(bool expected)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(renderers.All(r => r.enabled == expected), Is.True, $"게임기 렌더러 표시 상태가 {expected}여야 합니다.");
            var arms = Field<GameObject>(view, "restingArms");
            Assert.That(arms, Is.Not.Null, "PlayerInBed/Arms 참조가 연결되어 있어야 합니다.");
            Assert.That(arms.activeSelf, Is.EqualTo(!expected), "게임기를 들면 쉬는 팔이 꺼지고, 숨기면 켜져야 합니다.");
        }

        // 게임기 화면 메시의 모서리를 카메라 viewport 좌표로 옮깁니다(z는 카메라로부터의 거리).
        private List<Vector3> ScreenViewportPoints()
        {
            var screen = Field<Renderer>(view, "screenRenderer");
            Bounds mesh = screen.GetComponent<MeshFilter>().sharedMesh.bounds;
            var points = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                points.Add(camera.WorldToViewportPoint(screen.transform.TransformPoint(corner)));
            }
            return points;
        }

        private Rect ScreenViewportBounds()
        {
            var points = ScreenViewportPoints();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        // 게임기 화면이 미니게임 viewport를 빈틈없이 덮는지(한 축은 거의 일치) 확인합니다.
        private void AssertScreenCoversViewport()
        {
            Rect rect = Property<Rect>(manager, "MiniGameViewportRect");
            var points = ScreenViewportPoints();
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            const float tolerance = 0.005f;
            Assert.That(minX, Is.LessThanOrEqualTo(rect.xMin + tolerance));
            Assert.That(maxX, Is.GreaterThanOrEqualTo(rect.xMax - tolerance));
            Assert.That(minY, Is.LessThanOrEqualTo(rect.yMin + tolerance));
            Assert.That(maxY, Is.GreaterThanOrEqualTo(rect.yMax - tolerance));
            bool widthMatches = Mathf.Abs((maxX - minX) - rect.width) < 0.01f;
            bool heightMatches = Mathf.Abs((maxY - minY) - rect.height) < 0.01f;
            Assert.That(widthMatches || heightMatches, Is.True, "게임기 화면이 viewport보다 지나치게 크면 안 됩니다.");
            Assert.That(points.All(p => p.z > camera.nearClipPlane), Is.True, "게임기 화면이 카메라 Near Clip 안쪽에 있어야 합니다.");
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
        private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name).Invoke(target, arguments);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "게임기 검증의 상태 전환 시간이 초과되었습니다.");
        }
    }
}
