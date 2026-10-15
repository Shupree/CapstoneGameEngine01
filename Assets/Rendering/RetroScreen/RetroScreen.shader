// URP 전체 화면 레트로 이펙트.
// CRT-Free 샘플의 느낌(곡률, 픽셀화, 색 수 제한, 순서 디더링, 비네트)은 유지하고,
// 모니터 테두리와 시간에 따라 변하는 노이즈·깜빡임은 넣지 않는다.
// RetroScreenRendererFeature가 _BlitTexture에 카메라 색을 넣고 아래 _Retro* 값을 매 프레임 채운다.
Shader "Hidden/GE/RetroScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "RetroScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RetroResolution;   // xy: 원본 픽셀 크기, zw: 1 / 크기
            float  _RetroPixelSize;
            float  _RetroCurvature;
            float4 _RetroColorLevels;  // rgb: 채널별 단계 수 (0 = 제한 없음)
            float  _RetroDither4;
            float  _RetroDither8;
            float  _RetroDesaturation;
            float4 _RetroTint;
            float  _RetroVignette;
            float  _RetroVignetteSmoothness;
            float  _RetroScanlineIntensity;
            float  _RetroScanlineSize;

            static const float kBayer4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            static const float kBayer8[64] =
            {
                 0, 32,  8, 40,  2, 34, 10, 42,
                48, 16, 56, 24, 50, 18, 58, 26,
                12, 44,  4, 36, 14, 46,  6, 38,
                60, 28, 52, 20, 62, 30, 54, 22,
                 3, 35, 11, 43,  1, 33,  9, 41,
                51, 19, 59, 27, 49, 17, 57, 25,
                15, 47,  7, 39, 13, 45,  5, 37,
                63, 31, 55, 23, 61, 29, 53, 21
            };

            // 모서리를 고정한 배럴 왜곡: 화면이 볼록해지지만 화면 밖을 샘플링하지 않으므로
            // 원본 셰이더처럼 가장자리에 테두리가 생기지 않는다.
            float2 Curve(float2 uv, float k)
            {
                float2 p = uv * 2.0 - 1.0;
                p *= (1.0 + k * dot(p, p)) / (1.0 + 2.0 * k);
                return p * 0.5 + 0.5;
            }

            float Quantize(float value, float levels)
            {
                if (levels <= 0.0)
                    return value;
                float steps = max(levels, 2.0) - 1.0;
                return floor(value * steps + 0.5) / steps;
            }

            float3 StepSize(float3 levels)
            {
                // 색 수 제한이 꺼져 있어도 디더링은 약한 고정 패턴으로 적용되게 한다.
                return lerp(1.0 / 32.0, 1.0 / (max(levels, 2.0) - 1.0), step(0.5, levels));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = input.texcoord;
                float2 uv = Curve(screenUv, _RetroCurvature);

                float pixelSize = max(_RetroPixelSize, 1.0);
                float2 cells = _RetroResolution.xy / pixelSize;
                float2 cell = floor(uv * cells);

                half3 color;
                if (pixelSize > 1.0)
                {
                    uv = (cell + 0.5) / cells;
                    color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
                }
                else
                {
                    color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                }

                // 프로젝트가 Linear 색 공간이므로 sRGB로 바꿔서 색을 줄여야 단계가 눈에 고르게 보인다.
                color = LinearToSRGB(saturate(color));

                uint2 c = (uint2)cell;
                float m4 = kBayer4[(c.y % 4) * 4 + (c.x % 4)] / 16.0 - 0.5;
                float m8 = kBayer8[(c.y % 8) * 8 + (c.x % 8)] / 64.0 - 0.5;
                color += (m4 * _RetroDither4 + m8 * _RetroDither8) * StepSize(_RetroColorLevels.rgb);

                color.r = Quantize(color.r, _RetroColorLevels.r);
                color.g = Quantize(color.g, _RetroColorLevels.g);
                color.b = Quantize(color.b, _RetroColorLevels.b);
                color = saturate(color);

                float grey = dot(color, float3(0.21, 0.71, 0.07));
                color = lerp(color, grey.xxx, _RetroDesaturation) * _RetroTint.rgb;

                // CRT-Free의 색 스캔라인과 같은 방식이지만 시간 항이 없어 줄이 움직이지 않는다.
                float phase = uv.y * _RetroResolution.y * PI / max(_RetroScanlineSize, 1.0);
                float green = 1.0 - _RetroScanlineIntensity * (0.5 + 0.5 * sin(phase));
                float redBlue = 1.0 - _RetroScanlineIntensity * (0.5 + 0.5 * cos(phase));
                color *= float3(redBlue, green, redBlue);

                float2 d = screenUv * 2.0 - 1.0;
                float radius = dot(d, d) * 0.5; // 중앙 0, 모서리 1
                float vignette = smoothstep(1.0 - _RetroVignetteSmoothness, 1.0, radius);
                color *= 1.0 - _RetroVignette * vignette;

                return half4(SRGBToLinear(saturate(color)), 1.0);
            }
            ENDHLSL
        }
    }
}
