using System;
using System.Collections;
using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 저장된 MainGame과 Additive 씬의 실제 Update 입력 경로를 검증합니다. 메인 매니저는 기존 어셈블리에 둡니다.
    public sealed class MiniGamePauseIntegrationTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Type managerType;
        private MonoBehaviour manager;
        private SnakeGame snake;
        private RectTransform overlay;
        private float originalTimeScale;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        public override void TearDown()
        {
            Time.timeScale = originalTimeScale;
            base.TearDown();
        }

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            managerType = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Single);
            yield return null;
            manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
            Assert.That(manager, Is.Not.Null);
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") != string.Empty);
            MainGameTestScenes.UseSnakeAndSecondGame(manager);
            // 실제 프로젝트의 씬 개수와 관계없이 이 테스트의 대상인 스네이크를 명시적으로 선택합니다.
            if (Field<string>("currentLoadedMiniGame") != "MiniGame01")
            {
                MainGameTestScenes.Load(manager, "MiniGame01");
                yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame01");
            }
            snake = Object.FindFirstObjectByType<SnakeGame>();
            overlay = Field<RectTransform>("miniGamePauseOverlay");
            Assert.That(snake, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null, "MainGame에 저장한 정지 UI의 Inspector 연결이 필요합니다.");
            Assert.That(snake.ExternalPauseControl, Is.True);
            snake.StartGame();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadHost()
        {
            // 정지/숨김 상태에서 실패해도 다음 테스트에 싱글톤이나 전역 시간 상태를 남기지 않습니다.
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
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Pause integration cleanup");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = originalTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tab_PausesOnce_ShowsAuthoredUI_AndResumesWithoutFreezingMainGame()
        {
            yield return Keys(Key.Tab);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Paused));
            Assert.That(overlay.gameObject.activeInHierarchy, Is.True);
            Assert.That(overlay.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(new Color(0, 0, 0, 0.5f)));
            Assert.That(overlay.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("-PAUSED-"));
            Assert.That(overlay.GetComponentInParent<Canvas>().sortingOrder, Is.EqualTo(32767));
            var localOverlay = SceneManager.GetSceneByName("MiniGame01").GetRootGameObjects();
            Assert.That(Array.Exists(localOverlay, r => Array.Exists(r.GetComponentsInChildren<Transform>(true),
                t => t.name == "State overlay" && t.gameObject.activeSelf)), Is.False, "기존 정지 모달이 겹치면 안 됩니다.");

            var head = snake.Model.Body[0];
            int progress = snake.Progress;
            float hostTime = Time.time;
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Paused), "Tab을 길게 눌러도 한 번만 전환합니다.");
            Assert.That(snake.Model.Body[0], Is.EqualTo(head));
            Assert.That(snake.Progress, Is.EqualTo(progress));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.time, Is.GreaterThan(hostTime), "미니게임이 정지해도 MainGame의 시간은 진행되어야 합니다.");

            // 정지 중 누른 방향키는 재개 시 바로 적용되지 않고, 놓은 뒤 새로 누를 때만 입력됩니다.
            yield return Keys(Key.Tab, Key.W);
            yield return Keys(Key.W);
            yield return Keys(Key.Tab, Key.W);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(snake.Model.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(snake.Model.Body[0].x, Is.GreaterThan(head.x));
        }

        [UnityTest]
        public IEnumerator Shift_HidesPauseUI_AndRestoresTheExistingTabPause()
        {
            yield return Keys(Key.Tab);
            var head = snake.Model.Body[0];
            yield return Keys(Key.LeftShift);
            Assert.That(Field<bool>("isMiniGameHidden"), Is.True);
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            // Shift는 컨트롤러를 끄지 않고 그리기만 숨겨, Tab 정지 상태를 그대로 유지합니다.
            Assert.That(snake.gameObject.activeInHierarchy, Is.True);
            AssertPresentationHidden();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            // 닫힌 게임기에 Tab 입력이 들어와도 원래의 정지 상태를 바꾸지 않습니다.
            yield return Keys(Key.LeftShift, Key.Tab);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Paused));
            yield return Keys();
            Assert.That(Field<bool>("isMiniGameHidden"), Is.False);
            Assert.That(snake.gameObject.activeInHierarchy, Is.True);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Paused));
            Assert.That(overlay.gameObject.activeInHierarchy, Is.True);
            Assert.That(snake.Model.Body[0], Is.EqualTo(head));
            yield return Keys(Key.Tab);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(overlay.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ShiftAlone_KeepsSnakeAndMainGameRunning_AndPreservesHiddenAcrossSceneSwitch()
        {
            var head = snake.Model.Body[0];
            yield return Keys(Key.LeftShift);
            AssertPresentationHidden();
            float hostTime = Time.time;
            yield return new WaitForSeconds(0.3f);
            Assert.That(snake.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(snake.Model.Body[0], Is.Not.EqualTo(head), "숨기기 전에 Tab을 누르지 않으면 미니게임도 진행됩니다.");
            Assert.That(Time.time, Is.GreaterThan(hostTime));

            // Shift를 계속 누른 채 다음 씬이 들어와도 새 카메라와 Canvas를 노출하지 않습니다.
            Call("LoadRandomMiniGame");
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame02");
            Assert.That(Field<bool>("isMiniGameHidden"), Is.True);
            AssertPresentationHidden();
            yield return Keys();
            Assert.That(Field<bool>("isMiniGameHidden"), Is.False);
            Assert.That(Field<Camera>("currentMiniGameCamera").enabled, Is.True);
        }

        // 로직의 활성 상태와 별개로 실제 화면 출력 경로가 모두 차단됐는지 검사합니다.
        private void AssertPresentationHidden()
        {
            var scene = SceneManager.GetSceneByName(Field<string>("currentLoadedMiniGame"));
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Assert.That(camera.enabled, Is.False);
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) Assert.That(canvas.enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SceneSwitch_ClearsPauseUI_AndKeepsTheHierarchyReference()
        {
            yield return Keys(Key.Tab);
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Call("LoadRandomMiniGame");
            yield return WaitFor(() => !Field<bool>("isLoading") && Field<string>("currentLoadedMiniGame") == "MiniGame02");
            Assert.That(overlay, Is.Not.Null, "매니저와 함께 유지되는 UI는 Additive 씬 언로드로 파괴되지 않아야 합니다.");
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            Assert.That(Field<bool>("isMiniGamePaused"), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // 실제 키 이벤트를 다음 PlayerLoop의 Input System에서 처리해 매니저와 컨트롤러의 실행 순서를 검증합니다.
        private IEnumerator Keys(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            yield return null;
            yield return null;
        }

        private T Field<T>(string name) => (T)managerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        private void Call(string name) => managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(manager, null);

        // 전환이 끝나지 않는 경우 무한 대기 대신 원인을 드러내도록 실제 시간으로 제한합니다.
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "미니게임 씬 전환 시간이 초과되었습니다.");
        }
    }
}
