using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GE.Rendering
{
    // MainGame에 Additive로 로드되는 미니게임 씬의 카메라에 "게임기 화면"용 레트로 효과를 공통으로 적용한다.
    //  - 미니게임 씬(팀원 작업 영역)을 수정하지 않고, MainGame 한 곳에서 모든 미니게임 화면 값을 관리한다.
    //  - 미니게임 카메라에 RetroScreenCamera가 이미 붙어 있으면 그 게임 전용 값으로 보고 건드리지 않는다.
    //  - 방(MainGame) 카메라는 Main Camera의 RetroScreenCamera가 따로 담당하므로 두 화면 값이 섞이지 않는다.
    // MiniGameManager의 로드·숨김(Shift) 흐름과는 독립적이다. 매니저가 카메라 enabled를 꺼도 여기서 붙인 값은 유지된다.
    // 미니게임 단독 실행(MainGame 없음)에는 이 컴포넌트가 없으므로 미니게임 씬 자체 설정(Volume 등)을 그대로 쓴다.
    [DisallowMultipleComponent]
    [AddComponentMenu("GE/Rendering/Retro Screen MiniGame Applier")]
    public sealed class RetroScreenMiniGameApplier : MonoBehaviour
    {
        [Tooltip("끄면 미니게임 카메라에 효과를 넣지 않는다. 이미 붙인 카메라도 Play 중 바로 꺼진다.")]
        [SerializeField] private bool applyToMiniGames = true;

        [Tooltip("미니게임 화면 효과 전체 강도. 0이면 효과를 끈다.")]
        [Range(0f, 1f)]
        [SerializeField] private float intensity = 1f;

        [Tooltip("모든 미니게임 카메라에 공통으로 쓰는 값. Play 중 바꾸면 현재 미니게임에 바로 반영된다.")]
        [SerializeField] private RetroScreenSettings miniGameSettings = DefaultMiniGameSettings;

        // 이 컴포넌트가 직접 붙인 카메라 설정만 추적한다(게임 전용 설정은 목록에 넣지 않는다).
        private readonly List<RetroScreenCamera> appliedCameras = new List<RetroScreenCamera>();

        // 게임기 화면 기본값: 픽셀화 없이 약한 곡률·스캔라인·비네트와 아주 옅은 초록 틴트.
        public static RetroScreenSettings DefaultMiniGameSettings
        {
            get
            {
                RetroScreenSettings settings = RetroScreenSettings.Default;
                settings.curvature = 0.06f;
                settings.tint = new Color(0.96f, 1f, 0.96f);
                settings.scanlineIntensity = 0.3f;
                settings.scanlineSize = 2f;
                settings.vignette = 0.4f;
                settings.vignetteSmoothness = 0.6f;
                return settings;
            }
        }

        public bool ApplyToMiniGames
        {
            get => applyToMiniGames;
            set { applyToMiniGames = value; RefreshAppliedCameras(); }
        }

        public float Intensity
        {
            get => intensity;
            set { intensity = Mathf.Clamp01(value); RefreshAppliedCameras(); }
        }

        // struct이므로 꺼내서 수정한 뒤 다시 대입해야 반영된다.
        public RetroScreenSettings MiniGameSettings
        {
            get => miniGameSettings;
            set { miniGameSettings = value; RefreshAppliedCameras(); }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            // 이 컴포넌트보다 먼저 로드된 미니게임 씬(예: 실행 순서 차이)도 놓치지 않도록 현재 열린 씬을 한 번 훑는다.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != gameObject.scene) ApplyToScene(scene);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // Inspector에서 값을 바꾸면 Play 중인 미니게임 카메라에 바로 반영한다.
        private void OnValidate()
        {
            intensity = Mathf.Clamp01(intensity);
            if (Application.isPlaying) RefreshAppliedCameras();
        }

        // MainGame 안에서 Additive로 열리는 씬은 미니게임뿐이다. MainGame 자체의 재로드(Single)는 제외한다.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && scene != gameObject.scene) ApplyToScene(scene);
        }

        private void ApplyToScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            appliedCameras.RemoveAll(item => item == null);   // 언로드된 이전 미니게임 카메라 정리

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    // 미니게임 씬에 미리 붙여 둔 설정은 게임 전용 값이므로 덮어쓰지 않는다.
                    if (camera.TryGetComponent(out RetroScreenCamera _)) continue;

                    var retro = camera.gameObject.AddComponent<RetroScreenCamera>();
                    appliedCameras.Add(retro);
                    Configure(retro);
                }
            }
        }

        private void RefreshAppliedCameras()
        {
            appliedCameras.RemoveAll(item => item == null);
            foreach (RetroScreenCamera retro in appliedCameras) Configure(retro);
        }

        private void Configure(RetroScreenCamera retro)
        {
            retro.Source = RetroScreenCamera.SettingsSource.Custom;
            retro.CustomSettings = miniGameSettings;
            retro.Intensity = intensity;
            retro.enabled = applyToMiniGames;
        }
    }
}
