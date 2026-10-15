using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GE.Rendering
{
    // RetroScreenRendererFeature가 읽는 설정값.
    // Volume 프로필에 Override(GE/Retro Screen)로 추가하면 씬·카메라별로 켜고 끄거나 값을 섞을 수 있다.
    [Serializable, VolumeComponentMenu("GE/Retro Screen")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class RetroScreenVolume : VolumeComponent
    {
        [Header("Screen")]
        [Tooltip("CRT처럼 화면이 볼록해지는 정도. 가장자리는 항상 채워져 테두리가 생기지 않는다.")]
        public ClampedFloatParameter curvature = new ClampedFloatParameter(0f, 0f, 0.5f);

        [Tooltip("레트로 픽셀 하나의 크기(화면 픽셀 단위). 1이면 픽셀화하지 않는다.")]
        public ClampedIntParameter pixelSize = new ClampedIntParameter(1, 1, 16);

        [Header("Color")]
        [Tooltip("채널별 색 단계 수. 0이면 제한하지 않는다.")]
        public ClampedIntParameter redLevels = new ClampedIntParameter(0, 0, 64);
        public ClampedIntParameter greenLevels = new ClampedIntParameter(0, 0, 64);
        public ClampedIntParameter blueLevels = new ClampedIntParameter(0, 0, 64);

        [Tooltip("4x4 Bayer 행렬 디더링. 고정 패턴이라 깜빡이지 않는다.")]
        public ClampedFloatParameter dither4 = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("8x8 Bayer 행렬 디더링. 고정 패턴이라 깜빡이지 않는다.")]
        public ClampedFloatParameter dither8 = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("1이면 완전한 흑백이 된다.")]
        public ClampedFloatParameter desaturation = new ClampedFloatParameter(0f, 0f, 1f);

        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Header("Lines & Vignette")]
        [Tooltip("초록 / 빨강·파랑이 번갈아 나오는 고정 스캔라인. 움직이지 않는다.")]
        public ClampedFloatParameter scanlineIntensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("스캔라인 한 줄의 높이(화면 픽셀 단위).")]
        public ClampedFloatParameter scanlineSize = new ClampedFloatParameter(2f, 1f, 16f);

        [Tooltip("모서리가 어두워지는 정도.")]
        public ClampedFloatParameter vignette = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("비네트가 화면 중앙 쪽으로 퍼지는 범위.")]
        public ClampedFloatParameter vignetteSmoothness = new ClampedFloatParameter(0.6f, 0.01f, 1f);

        // 렌더러 피처가 패스를 추가할지 판단할 때 호출한다.
        // 모든 값이 기본값이면 화면 변화가 없으므로 전체 화면 블릿 비용을 쓰지 않는다.
        public bool IsActive()
        {
            return active && (curvature.value > 0f || pixelSize.value > 1
                || redLevels.value > 0 || greenLevels.value > 0 || blueLevels.value > 0
                || dither4.value > 0f || dither8.value > 0f || desaturation.value > 0f
                || tint.value != Color.white || scanlineIntensity.value > 0f || vignette.value > 0f);
        }
    }
}
