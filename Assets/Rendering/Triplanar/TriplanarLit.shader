// UV 없이 위치 기준으로 텍스처를 투영하는 URP Lit 셰이더.
// Map의 가구 모델 중 일부(desk, Bookshelf, Door_7 등)는 UV가 없고, 임포트 단위·스케일도 제각각이라
// 일반 UV 텍스처 대신 X·Y·Z 세 방향 투영(트라이플래너)으로 "텍스처 1장 = _TileSize 미터"를 맞춘다.
// 조명 계산은 URP의 UniversalFragmentPBR을 그대로 사용하므로 Forward+ 조명·그림자·SSAO와 함께 동작한다.
Shader "GE/Triplanar Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map (OpenGL)", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        [NoScaleOffset] _RoughnessMap ("Roughness Map", 2D) = "grey" {}
        _Smoothness ("Smoothness Multiplier", Range(0, 1)) = 1
        [NoScaleOffset] _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Triplanar)]
        _TileSize ("Tile Size (meters)", Float) = 1
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 6
        _UVRotation ("Texture Rotation (degrees)", Range(0, 360)) = 0
        [Toggle(_TRIPLANAR_OBJECT_SPACE)] _ObjectSpace ("Object Space (texture follows moving objects)", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // SRP Batcher 호환을 위해 머티리얼 값은 모두 UnityPerMaterial에 둔다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Smoothness;
            half _OcclusionStrength;
            float _TileSize;
            float _BlendSharpness;
            float _UVRotation;
        CBUFFER_END

        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        TEXTURE2D(_RoughnessMap);
        TEXTURE2D(_OcclusionMap);

        // 투영에 쓸 좌표계를 정한다.
        // 월드 모드: 벽·바닥처럼 고정된 오브젝트끼리 무늬가 끊기지 않고 이어진다.
        // 오브젝트 모드: 문처럼 움직이는 오브젝트에서 텍스처가 미끄러지지 않는다.
        //   오브젝트 좌표에 스케일을 곱해 모델마다 다른 임포트 단위(30, 80, 100 등)를 미터로 맞춘다.
        void GetTriplanarSpace(float3 positionOS, float3 normalOS, out float3 position, out float3 normal, out float3 objectScale)
        {
            float4x4 m = GetObjectToWorldMatrix();
            objectScale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
        #if defined(_TRIPLANAR_OBJECT_SPACE)
            position = positionOS * objectScale;
            normal = normalOS / objectScale;
        #else
            position = TransformObjectToWorld(positionOS);
            normal = TransformObjectToWorldNormal(normalOS);
        #endif
        }

        // 투영 공간의 법선을 월드 법선으로 되돌린다. 스케일 공간에서 계산했으므로 회전만 남도록 스케일을 나눈다.
        float3 TriplanarNormalToWorld(float3 normal, float3 objectScale)
        {
        #if defined(_TRIPLANAR_OBJECT_SPACE)
            return TransformObjectToWorldDir(normal / objectScale);
        #else
            return normal;
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _TRIPLANAR_OBJECT_SPACE

            // URP Lit(ForwardLit)과 같은 조명 키워드. 디버그 뷰·데칼·LOD 크로스페이드는 쓰지 않아 제외했다.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS        : POSITION;
                float3 normalOS          : NORMAL;
                float2 staticLightmapUV  : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS      : TEXCOORD0;
                float3 normalWS        : TEXCOORD1;
                float3 triplanarPos    : TEXCOORD2;
                float3 triplanarNormal : TEXCOORD3;
                float3 objectScale     : TEXCOORD4;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4 fogFactorAndVertexLight : TEXCOORD5;
            #else
                half  fogFactor               : TEXCOORD5;
            #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD7;
            #endif
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD8;
            #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 텍스처 회전(나무결 방향 조절용). 노멀맵은 회전된 UV 기준으로 읽히므로 역회전으로 되돌린다.
            float2 RotateUV(float2 uv, float s, float c) { return float2(c * uv.x - s * uv.y, s * uv.x + c * uv.y); }
            half2 UnrotateNormal(half2 n, float s, float c) { return half2(c * n.x + s * n.y, -s * n.x + c * n.y); }

            // 세 방향 투영을 섞어 albedo·법선·거칠기·AO를 얻는다.
            // 법선은 Whiteout 블렌드(Ben Golus 방식)로 각 투영면의 노멀맵을 표면 법선에 합친다.
            void SampleTriplanar(float3 p, float3 n, out half3 albedo, out float3 normal, out half roughness, out half occlusion)
            {
                n = normalize(n);
                float3 weights = pow(abs(n), _BlendSharpness);
                weights /= max(dot(weights, 1.0), 1e-5);

                // 뒷면 투영에서 무늬가 좌우 반전되지 않도록 축 방향 부호로 U를 뒤집는다.
                float3 axisSign = step(0.0, n) * 2.0 - 1.0;
                float3 q = p / max(_TileSize, 1e-4);

                float s, c;
                sincos(radians(_UVRotation), s, c);
                float2 uvX = RotateUV(float2(q.z * axisSign.x, q.y), s, c);
                float2 uvY = RotateUV(float2(q.x * axisSign.y, q.z), s, c);
                float2 uvZ = RotateUV(float2(-q.x * axisSign.z, q.y), s, c);

                albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb * weights.x
                       + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb * weights.y
                       + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb * weights.z;
                albedo *= _BaseColor.rgb;

                roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_BaseMap, uvX).r * weights.x
                          + SAMPLE_TEXTURE2D(_RoughnessMap, sampler_BaseMap, uvY).r * weights.y
                          + SAMPLE_TEXTURE2D(_RoughnessMap, sampler_BaseMap, uvZ).r * weights.z;

                half ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, uvX).r * weights.x
                        + SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, uvY).r * weights.y
                        + SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, uvZ).r * weights.z;
                occlusion = lerp(1.0, ao, _OcclusionStrength);

                half3 tnX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, uvX), _BumpScale);
                half3 tnY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, uvY), _BumpScale);
                half3 tnZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, uvZ), _BumpScale);

                // 회전 → 좌우 반전 순서로 적용했으므로 되돌릴 때는 역회전 후 반전한다.
                tnX.xy = UnrotateNormal(tnX.xy, s, c);
                tnY.xy = UnrotateNormal(tnY.xy, s, c);
                tnZ.xy = UnrotateNormal(tnZ.xy, s, c);
                tnX.x *= axisSign.x;
                tnY.x *= axisSign.y;
                tnZ.x *= -axisSign.z;

                tnX = half3(tnX.xy + n.zy, abs(tnX.z) * n.x);
                tnY = half3(tnY.xy + n.xz, abs(tnY.z) * n.y);
                tnZ = half3(tnZ.xy + n.xy, abs(tnZ.z) * n.z);
                normal = normalize(tnX.zyx * weights.x + tnY.xzy * weights.y + tnZ.xyz * weights.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.positionCS = vertexInput.positionCS;
                GetTriplanarSpace(input.positionOS.xyz, input.normalOS, output.triplanarPos, output.triplanarNormal, output.objectScale);

                half fogFactor = 0;
            #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.fogFactorAndVertexLight = half4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
            #else
                output.fogFactor = fogFactor;
            #endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
                return output;
            }

            // LitForwardPass.hlsl의 InitializeBakedGIData와 같은 처리(라이트맵·라이트 프로브·APV).
            void InitializeBakedGI(Varyings input, inout InputData inputData)
            {
            #if defined(_SCREEN_SPACE_IRRADIANCE)
                inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
            #elif defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                inputData.bakedGI = SAMPLE_GI(input.vertexSH,
                    GetAbsolutePositionWS(inputData.positionWS),
                    inputData.normalWS,
                    inputData.viewDirectionWS,
                    input.positionCS.xy,
                    input.probeOcclusion,
                    inputData.shadowMask);
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #endif
            }

            void Frag(Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 albedo;
                float3 triplanarNormal;
                half roughness;
                half occlusion;
                SampleTriplanar(input.triplanarPos, input.triplanarNormal, albedo, triplanarNormal, roughness, occlusion);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.smoothness = (1.0 - roughness) * _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(TriplanarNormalToWorld(triplanarNormal, input.objectScale));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
            #else
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
            #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                InitializeBakedGI(input, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                outColor = half4(color.rgb, 1.0);

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        // 그림자 맵 렌더링. URP ShadowCasterPass.hlsl과 같은 바이어스 처리를 한다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // 깊이 텍스처용 패스.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // PC_Renderer의 SSAO가 읽는 깊이+법선 패스. 노멀맵 대신 메시 법선을 쓴다(URP Lit의 노멀맵 미사용 경로와 동일).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS    : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void DepthNormalsFrag(Varyings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
