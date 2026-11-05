using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GE.Rendering
{
    // URP 렌더러(PC_Renderer 등)에 추가하는 레트로 화면 피처.
    // 실제 켜기/끄기와 값은 카메라마다 다음 순서로 정한다.
    //  1) 카메라에 RetroScreenCamera가 있으면 그 컴포넌트의 켜짐 여부·강도·값을 쓴다.
    //  2) 없으면 Volume의 RetroScreenVolume Override 값을 쓴다(기존 방식).
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

        // 카메라마다 매 프레임 호출된다. 프리뷰·반사 카메라와 효과가 꺼진 카메라는 건너뛴다.
        // URP는 카메라를 하나씩 순서대로 기록·실행하므로, 여기서 정한 값을 pass에 넣어 두면
        // 같은 카메라의 RecordRenderGraph에서 그대로 사용된다. VolumeManager.instance.stack도 현재 카메라용으로 갱신된 상태다.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null) return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (cameraType == CameraType.SceneView && !showInSceneView) return;

            if (!TryResolveSettings(renderingData.cameraData.camera, out RetroScreenSettings settings)) return;

            pass.Settings = settings;
            renderer.EnqueuePass(pass);
        }

        // 카메라별 컴포넌트를 먼저 보고, 없으면 Volume 값으로 대신한다.
        // 컴포넌트가 붙어 있으면 꺼져 있어도 Volume으로 넘어가지 않는다(= 그 카메라만 효과 해제).
        private static bool TryResolveSettings(Camera camera, out RetroScreenSettings settings)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            if (camera != null && camera.TryGetComponent(out RetroScreenCamera cameraSettings))
                return cameraSettings.TryResolve(stack, out settings);

            settings = default;
            var volume = stack?.GetComponent<RetroScreenVolume>();
            if (volume == null || !volume.IsActive()) return false;

            settings = RetroScreenSettings.FromVolume(volume);
            return true;
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

            // 카메라별 값은 공유 머티리얼이 아니라 이 블록으로 넘긴다.
            // 머티리얼에 직접 Set하면 명령 실행 시점의 마지막 값이 모든 카메라에 쓰일 수 있지만,
            // 블록 값은 블릿 그리기 명령이 기록될 때 복사되므로 카메라끼리 섞이지 않는다.
            private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            // AddRenderPasses에서 현재 카메라용으로 정한 값. 카메라마다 덮어쓴다.
            public RetroScreenSettings Settings { get; set; }

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

                ApplySettings(Settings, desc.width, desc.height);

                var blitParameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0)
                {
                    propertyBlock = propertyBlock,
                };
                renderGraph.AddBlitPass(blitParameters, "Retro Screen");
                resourceData.cameraColor = destination;
            }

            // 현재 카메라의 값을 셰이더 상수로 옮긴다. 해상도는 렌더 스케일·viewport가 적용된 실제 텍스처 크기를 쓴다.
            private void ApplySettings(RetroScreenSettings settings, int width, int height)
            {
                propertyBlock.SetVector(ResolutionId, new Vector4(width, height, 1f / width, 1f / height));
                propertyBlock.SetFloat(PixelSizeId, settings.pixelSize);
                propertyBlock.SetFloat(CurvatureId, settings.curvature);
                propertyBlock.SetVector(ColorLevelsId, new Vector4(settings.redLevels, settings.greenLevels, settings.blueLevels, 0f));
                propertyBlock.SetFloat(Dither4Id, settings.dither4);
                propertyBlock.SetFloat(Dither8Id, settings.dither8);
                propertyBlock.SetFloat(DesaturationId, settings.desaturation);
                // SetColor는 Linear 변환을 하므로 SetVector로 sRGB 값을 그대로 넘긴다. 셰이더는 sRGB 공간에서 틴트를 곱한다.
                propertyBlock.SetVector(TintId, settings.tint);
                propertyBlock.SetFloat(VignetteId, settings.vignette);
                propertyBlock.SetFloat(VignetteSmoothnessId, settings.vignetteSmoothness);
                propertyBlock.SetFloat(ScanlineIntensityId, settings.scanlineIntensity);
                propertyBlock.SetFloat(ScanlineSizeId, settings.scanlineSize);
            }
        }
    }
}
