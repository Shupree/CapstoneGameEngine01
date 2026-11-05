using System;
using UnityEngine;

namespace GE.Rendering
{
    // RetroScreen 셰이더에 넘기는 값 묶음.
    // Volume(RetroScreenVolume)과 카메라 컴포넌트(RetroScreenCamera)가 같은 형식으로 값을 넘기도록 공통으로 쓴다.
    // 필드 범위는 RetroScreenVolume의 Clamped 파라미터 범위와 맞춰 둔다.
    [Serializable]
    public struct RetroScreenSettings
    {
        [Header("Screen")]
        [Tooltip("CRT처럼 화면이 볼록해지는 정도. 가장자리는 항상 채워져 테두리가 생기지 않는다.")]
        [Range(0f, 0.5f)] public float curvature;

        [Tooltip("레트로 픽셀 하나의 크기(화면 픽셀 단위). 1이면 픽셀화하지 않는다.")]
        [Range(1, 16)] public int pixelSize;

        [Header("Color")]
        [Tooltip("채널별 색 단계 수. 0이면 제한하지 않는다.")]
        [Range(0, 64)] public int redLevels;
        [Range(0, 64)] public int greenLevels;
        [Range(0, 64)] public int blueLevels;

        [Tooltip("4x4 Bayer 행렬 디더링. 고정 패턴이라 깜빡이지 않는다.")]
        [Range(0f, 1f)] public float dither4;

        [Tooltip("8x8 Bayer 행렬 디더링. 고정 패턴이라 깜빡이지 않는다.")]
        [Range(0f, 1f)] public float dither8;

        [Tooltip("1이면 완전한 흑백이 된다.")]
        [Range(0f, 1f)] public float desaturation;

        [ColorUsage(false)] public Color tint;

        [Header("Lines & Vignette")]
        [Tooltip("초록 / 빨강·파랑이 번갈아 나오는 고정 스캔라인. 움직이지 않는다.")]
        [Range(0f, 1f)] public float scanlineIntensity;

        [Tooltip("스캔라인 한 줄의 높이(화면 픽셀 단위).")]
        [Range(1f, 16f)] public float scanlineSize;

        [Tooltip("모서리가 어두워지는 정도.")]
        [Range(0f, 1f)] public float vignette;

        [Tooltip("비네트가 화면 중앙 쪽으로 퍼지는 범위.")]
        [Range(0.01f, 1f)] public float vignetteSmoothness;

        // 화면 변화가 없는 기본값. RetroScreenVolume의 기본값과 같다.
        public static RetroScreenSettings Default => new RetroScreenSettings
        {
            curvature = 0f,
            pixelSize = 1,
            tint = Color.white,
            scanlineSize = 2f,
            vignetteSmoothness = 0.6f,
        };

        // 모든 값이 기본값이면 화면 변화가 없으므로, 렌더러 피처가 전체 화면 블릿을 건너뛰는 데 쓴다.
        public bool HasVisibleEffect =>
            curvature > 0f || pixelSize > 1
            || redLevels > 0 || greenLevels > 0 || blueLevels > 0
            || dither4 > 0f || dither8 > 0f || desaturation > 0f
            || tint != Color.white || scanlineIntensity > 0f || vignette > 0f;

        // Volume 스택에 섞인 현재 값을 그대로 옮긴다. 카메라 컴포넌트의 Volume 모드와 컴포넌트가 없는 카메라가 사용한다.
        public static RetroScreenSettings FromVolume(RetroScreenVolume volume)
        {
            return new RetroScreenSettings
            {
                curvature = volume.curvature.value,
                pixelSize = volume.pixelSize.value,
                redLevels = volume.redLevels.value,
                greenLevels = volume.greenLevels.value,
                blueLevels = volume.blueLevels.value,
                dither4 = volume.dither4.value,
                dither8 = volume.dither8.value,
                desaturation = volume.desaturation.value,
                tint = volume.tint.value,
                scanlineIntensity = volume.scanlineIntensity.value,
                scanlineSize = volume.scanlineSize.value,
                vignette = volume.vignette.value,
                vignetteSmoothness = volume.vignetteSmoothness.value,
            };
        }

        // 카메라별 전체 강도(0~1)를 적용한 복사본을 만든다.
        // 연속값(곡률·디더링·채도·틴트·스캔라인·비네트)만 비례해서 줄이고,
        // 단계형 값(픽셀 크기·색 단계 수)은 중간값이 어색하므로 그대로 둔다. 강도 0은 피처 쪽에서 패스 자체를 건너뛴다.
        public RetroScreenSettings Scaled(float intensity)
        {
            float t = Mathf.Clamp01(intensity);
            RetroScreenSettings result = this;
            result.curvature *= t;
            result.dither4 *= t;
            result.dither8 *= t;
            result.desaturation *= t;
            result.tint = Color.LerpUnclamped(Color.white, tint, t);
            result.scanlineIntensity *= t;
            result.vignette *= t;
            return result;
        }
    }
}
