using UnityEngine;
using UnityEngine.Rendering;

namespace GE.Rendering
{
    // 카메라마다 레트로 화면 효과를 따로 켜고 끄거나 값을 다르게 줄 때 카메라 오브젝트에 붙이는 컴포넌트.
    // RetroScreenRendererFeature가 렌더링 중인 카메라에서 이 컴포넌트를 찾아 읽는다.
    //  - 컴포넌트가 없는 카메라: 기존처럼 Volume의 RetroScreenVolume 값을 따른다.
    //  - 컴포넌트가 있고 꺼져 있음(체크 해제): 이 카메라에는 효과를 적용하지 않는다(Volume 값과 무관).
    //  - 컴포넌트가 켜져 있음: source에 따라 Volume 값 또는 이 컴포넌트의 customSettings를 쓰고 intensity를 곱한다.
    // 렌더링 쪽에서 값을 읽기만 하므로 Update가 없고, Play 중 Inspector나 스크립트로 바꾸면 다음 프레임에 바로 반영된다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("GE/Rendering/Retro Screen Camera")]
    public sealed class RetroScreenCamera : MonoBehaviour
    {
        public enum SettingsSource
        {
            // 이 카메라에 섞인 Volume 스택(Volume Mask·Trigger 기준)의 값을 쓴다.
            Volume,
            // 아래 customSettings 값을 쓴다. Volume 구성과 상관없이 카메라마다 독립된 값이 된다.
            Custom,
        }

        [Tooltip("Volume: 씬의 Volume(GE/Retro Screen) 값을 사용 / Custom: 아래 Custom Settings 값을 사용")]
        [SerializeField] private SettingsSource source = SettingsSource.Custom;

        [Tooltip("효과 전체 강도. 곡률·디더링·채도·틴트·스캔라인·비네트를 비례해서 줄인다. 0이면 효과를 끈다.")]
        [Range(0f, 1f)]
        [SerializeField] private float intensity = 1f;

        [SerializeField] private RetroScreenSettings customSettings = RetroScreenSettings.Default;

        public SettingsSource Source
        {
            get => source;
            set => source = value;
        }

        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp01(value);
        }

        // 스크립트에서 값 전체를 바꿀 때 사용한다. struct이므로 꺼내서 수정한 뒤 다시 대입해야 반영된다.
        public RetroScreenSettings CustomSettings
        {
            get => customSettings;
            set => customSettings = value;
        }

        // 렌더러 피처가 카메라마다 호출한다. 이번 프레임에 적용할 최종 값을 만들고, 화면 변화가 없으면 false를 돌려준다.
        // Volume 모드의 stack은 URP가 현재 카메라용으로 갱신해 둔 VolumeManager.instance.stack이다.
        internal bool TryResolve(VolumeStack stack, out RetroScreenSettings settings)
        {
            settings = default;
            if (!enabled || intensity <= 0f) return false;

            RetroScreenSettings baseSettings;
            if (source == SettingsSource.Custom)
            {
                baseSettings = customSettings;
            }
            else
            {
                var volume = stack?.GetComponent<RetroScreenVolume>();
                if (volume == null || !volume.active) return false;
                baseSettings = RetroScreenSettings.FromVolume(volume);
            }

            settings = baseSettings.Scaled(intensity);
            return settings.HasVisibleEffect;
        }

        // 컴포넌트를 처음 붙이거나 Inspector에서 Reset할 때 화면 변화가 없는 값으로 시작한다.
        private void Reset()
        {
            source = SettingsSource.Custom;
            intensity = 1f;
            customSettings = RetroScreenSettings.Default;
        }

        // Inspector 우클릭 메뉴: 현재 씬 Volume에 설정된 값을 Custom Settings로 복사한다.
        // 기존 Global Volume에서 맞춰 둔 값을 카메라별 값의 출발점으로 쓰기 위한 편집 도구다.
        [ContextMenu("Copy Values From Volume Profiles In Scene")]
        private void CopyFromVolume()
        {
            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var volume in volumes)
            {
                if (!volume.isGlobal || volume.sharedProfile == null) continue;
                if (!volume.sharedProfile.TryGet(out RetroScreenVolume retro)) continue;

                // Override 체크가 꺼진 항목은 기본값을 쓰도록, 기본값 위에 체크된 항목만 덮어쓴다.
                var result = RetroScreenSettings.Default;
                if (retro.curvature.overrideState) result.curvature = retro.curvature.value;
                if (retro.pixelSize.overrideState) result.pixelSize = retro.pixelSize.value;
                if (retro.redLevels.overrideState) result.redLevels = retro.redLevels.value;
                if (retro.greenLevels.overrideState) result.greenLevels = retro.greenLevels.value;
                if (retro.blueLevels.overrideState) result.blueLevels = retro.blueLevels.value;
                if (retro.dither4.overrideState) result.dither4 = retro.dither4.value;
                if (retro.dither8.overrideState) result.dither8 = retro.dither8.value;
                if (retro.desaturation.overrideState) result.desaturation = retro.desaturation.value;
                if (retro.tint.overrideState) result.tint = retro.tint.value;
                if (retro.scanlineIntensity.overrideState) result.scanlineIntensity = retro.scanlineIntensity.value;
                if (retro.scanlineSize.overrideState) result.scanlineSize = retro.scanlineSize.value;
                if (retro.vignette.overrideState) result.vignette = retro.vignette.value;
                if (retro.vignetteSmoothness.overrideState) result.vignetteSmoothness = retro.vignetteSmoothness.value;

#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(this, "Copy Retro Screen Values From Volume");
#endif
                customSettings = result;
                source = SettingsSource.Custom;
                return;
            }

            Debug.LogWarning("[RetroScreenCamera] 열린 씬에서 GE/Retro Screen Override가 있는 Global Volume을 찾지 못했습니다.", this);
        }
    }
}
