using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GE.Rendering
{
    // URP 렌더러(PC_Renderer 등)에 추가하는 레트로 화면 피처.
    // 실제 켜기/끄기와 값 조절은 Volume의 RetroScreenVolume Override가 담당한다.
    // CRT-Free 샘플은 Built-in 전용(OnRenderImage)이라 URP에서는 이 피처를 대신 사용한다.
    public sealed class RetroScreenRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/GE/RetroScreen";

        [Tooltip("빌드에 셰이더가 포함되도록 RetroScreen.shader를 연결한다.")]
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        [Tooltip("Scene 뷰에도 이펙트를 표시한다.")]
        [SerializeField] private bool showInSceneView;

        private Material material;
        private RetroScreenPass pass;

        // 렌더러 에셋이 로드되거나 Inspector 값이 바뀔 때 URP가 호출한다.
        // Shader.Find는 에디터 대비용이며, 빌드에서는 shader 필드가 연결돼 있어야 셰이더가 빠지지 않는다.
        public override void Create()
        {
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null) return;

            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new RetroScreenPass(material) { renderPassEvent = injectionPoint };
        }

        // 카메라마다 매 프레임 호출된다. 프리뷰·반사 카메라와 Volume 값이 비활성인 경우는 건너뛴다.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null) return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (cameraType == CameraType.SceneView && !showInSceneView) return;

            var settings = VolumeManager.instance.stack.GetComponent<RetroScreenVolume>();
            if (settings == null || !settings.IsActive()) return;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        private sealed class RetroScreenPass : ScriptableRenderPass
        {
            private static readonly int ResolutionId = Shader.PropertyToID("_RetroResolution");
            private static readonly int PixelSizeId = Shader.PropertyToID("_RetroPixelSize");
            private static readonly int CurvatureId = Shader.PropertyToID("_RetroCurvature");
            private static readonly int ColorLevelsId = Shader.PropertyToID("_RetroColorLevels");
            private static readonly int Dither4Id = Shader.PropertyToID("_RetroDither4");
            private static readonly int Dither8Id = Shader.PropertyToID("_RetroDither8");
            private static readonly int DesaturationId = Shader.PropertyToID("_RetroDesaturation");
            private static readonly int TintId = Shader.PropertyToID("_RetroTint");
            private static readonly int VignetteId = Shader.PropertyToID("_RetroVignette");
            private static readonly int VignetteSmoothnessId = Shader.PropertyToID("_RetroVignetteSmoothness");
            private static readonly int ScanlineIntensityId = Shader.PropertyToID("_RetroScanlineIntensity");
            private static readonly int ScanlineSizeId = Shader.PropertyToID("_RetroScanlineSize");

            private readonly Material material;

            public RetroScreenPass(Material material)
            {
                this.material = material;
                // 백버퍼가 아닌 중간 텍스처가 있어야 카메라 색을 읽어 다시 쓸 수 있다.
                requiresIntermediateTexture = true;
            }

            // Render Graph 기록 단계: 카메라 색을 새 텍스처로 블릿한 뒤 그 텍스처를 카메라 색으로 교체한다.
            // 이후 패스(UI 등)는 교체된 cameraColor를 이어서 사용한다.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_RetroScreenColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                ApplySettings(VolumeManager.instance.stack.GetComponent<RetroScreenVolume>(), desc.width, desc.height);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Retro Screen");
                resourceData.cameraColor = destination;
            }

            // Volume 스택의 현재 값을 셰이더 상수로 옮긴다. 해상도는 렌더 스케일이 적용된 실제 텍스처 크기를 쓴다.
            private void ApplySettings(RetroScreenVolume settings, int width, int height)
            {
                material.SetVector(ResolutionId, new Vector4(width, height, 1f / width, 1f / height));
                material.SetFloat(PixelSizeId, settings.pixelSize.value);
                material.SetFloat(CurvatureId, settings.curvature.value);
                material.SetVector(ColorLevelsId, new Vector4(settings.redLevels.value, settings.greenLevels.value, settings.blueLevels.value, 0f));
                material.SetFloat(Dither4Id, settings.dither4.value);
                material.SetFloat(Dither8Id, settings.dither8.value);
                material.SetFloat(DesaturationId, settings.desaturation.value);
                // SetColor는 Linear 변환을 하므로 SetVector로 sRGB 값을 그대로 넘긴다. 셰이더는 sRGB 공간에서 틴트를 곱한다.
                material.SetVector(TintId, settings.tint.value);
                material.SetFloat(VignetteId, settings.vignette.value);
                material.SetFloat(VignetteSmoothnessId, settings.vignetteSmoothness.value);
                material.SetFloat(ScanlineIntensityId, settings.scanlineIntensity.value);
                material.SetFloat(ScanlineSizeId, settings.scanlineSize.value);
            }
        }
    }
}
