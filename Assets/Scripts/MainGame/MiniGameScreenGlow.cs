using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 게임기(미니게임) 화면의 빛이 방을 비추는 연출입니다.
// - MainGame의 Point Light 하나에 붙여 사용합니다. 화면 색을 주기적으로 읽어 조명 색·밝기로 옮깁니다.
// - 미니게임 씬·MiniGameManager의 동작은 바꾸지 않고 조회만 합니다(Inspector 참조, 싱글턴 생성 없음).
// - Shift로 숨기면 화면이 이불 속으로 들어간 것으로 보고 빛을 줄입니다(hiddenIntensityMultiplier).
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class MiniGameScreenGlow : MonoBehaviour
{
    [SerializeField] private MiniGameManager miniGameManager;

    [Header("Brightness")]
    [Tooltip("화면이 가장 밝을 때의 조명 세기입니다.")]
    [SerializeField, Min(0f)] private float maxIntensity = 2f;
    [Tooltip("화면이 어두운 게임(검은 배경 등)도 이 비율만큼은 빛납니다. 0이면 화면 밝기 그대로 따라갑니다.")]
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.35f;
    [Tooltip("Shift로 게임기를 숨긴 동안 남길 빛의 비율입니다. 0이면 완전히 꺼집니다(이불 속에 숨긴 상태).")]
    [SerializeField, Range(0f, 1f)] private float hiddenIntensityMultiplier = 0f;

    [Header("Color")]
    [Tooltip("켜면 실제 게임 화면의 평균 색을 따라갑니다. 끄면 Fallback Color만 사용합니다.")]
    [SerializeField] private bool followScreenColor = true;
    [Tooltip("0이면 흰빛, 1이면 화면 색을 그대로 사용합니다.")]
    [SerializeField, Range(0f, 1f)] private float colorSaturation = 0.6f;
    [Tooltip("화면 색을 읽지 못할 때(로딩 중, 색 따라가기 끔)의 조명 색입니다.")]
    [SerializeField] private Color fallbackColor = new Color(0.65f, 0.8f, 1f);

    [Header("Timing")]
    [Tooltip("화면 색을 다시 읽는 간격(초)입니다. 작을수록 빠르게 반응하지만 비용이 늘어납니다.")]
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
    [Tooltip("색·밝기가 목표값으로 따라가는 속도입니다. 클수록 빠르게 바뀝니다(깜빡임처럼 보일 수 있음).")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;

    // 매니저의 미니게임 viewport와 같은 영역만 평균 낸다. 매니저 값을 읽지 못하면 이 기본값을 쓴다.
    private static readonly Rect DefaultViewport = new Rect(0.15f, 0.15f, 0.7f, 0.7f);
    private const int SampleSize = 64;

    private Light glowLight;
    private RenderTexture screenTexture;
    private RenderTexture sampleTexture;
    private Coroutine sampleRoutine;
    private bool readbackPending;
    private bool hasSample;
    private Color sampledColor = Color.black;
    private Color currentColor;
    private float currentIntensity;

    public float MaxIntensity { get => maxIntensity; set => maxIntensity = Mathf.Max(0f, value); }
    public float HiddenIntensityMultiplier { get => hiddenIntensityMultiplier; set => hiddenIntensityMultiplier = Mathf.Clamp01(value); }

    private void Awake()
    {
        glowLight = GetComponent<Light>();
        currentColor = fallbackColor;
        currentIntensity = 0f;
        ApplyToLight();
    }

    private void OnEnable()
    {
        sampleRoutine = StartCoroutine(SampleLoop());
    }

    private void OnDisable()
    {
        if (sampleRoutine != null) StopCoroutine(sampleRoutine);
        sampleRoutine = null;
        ReleaseTextures();
    }

    private void Update()
    {
        // 게임오버 시 timeScale이 0이 되므로 실제 시간으로 서서히 꺼지게 한다.
        float step = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
        Color targetColor = ComputeTargetColor(out float brightness);
        float targetIntensity = maxIntensity * Mathf.Lerp(minBrightness, 1f, brightness) * VisibilityFactor();
        currentColor = Color.Lerp(currentColor, targetColor, step);
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, step);
        ApplyToLight();
    }

    // 화면이 실제로 보이는 정도(0~1). 미니게임이 없거나 게임오버면 0, 숨김이면 hiddenIntensityMultiplier.
    private float VisibilityFactor()
    {
        if (miniGameManager == null) return 0f;
        if (!miniGameManager.IsMiniGameActive || miniGameManager.IsGameOver || miniGameManager.IsGameFinished) return 0f;
        return miniGameManager.IsMiniGameHidden ? hiddenIntensityMultiplier : 1f;
    }

    private Color ComputeTargetColor(out float brightness)
    {
        if (!followScreenColor || !hasSample)
        {
            brightness = 1f;
            return fallbackColor;
        }
        // 화면 평균색의 색상만 남기고(가장 밝은 채널을 1로) 흰색과 섞는다. 밝기는 따로 세기에 반영한다.
        float maxChannel = Mathf.Max(sampledColor.r, Mathf.Max(sampledColor.g, sampledColor.b));
        Color hue = maxChannel > 0.001f ? sampledColor / maxChannel : Color.white;
        hue.a = 1f;
        float luminance = 0.2126f * sampledColor.r + 0.7152f * sampledColor.g + 0.0722f * sampledColor.b;
        // 어두운 화면도 차이가 보이도록 감마를 살짝 올린다.
        brightness = Mathf.Sqrt(Mathf.Clamp01(luminance));
        return Color.Lerp(Color.white, hue, colorSaturation);
    }

    private void ApplyToLight()
    {
        glowLight.color = currentColor;
        glowLight.intensity = currentIntensity;
        // 거의 꺼졌을 때는 그림자 비용도 없애기 위해 컴포넌트를 끈다.
        glowLight.enabled = currentIntensity > 0.001f;
    }

    // 프레임이 다 그려진 뒤 화면을 복사해 미니게임 영역 평균색을 GPU에서 비동기로 읽는다.
    private IEnumerator SampleLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            // 숨김·로딩 중에는 방 화면이 찍히므로 읽지 않는다(방 조명이 다시 조명에 들어가는 순환 방지).
            if (followScreenColor && !readbackPending && VisibilityFactor() >= 1f && CanSample())
                CaptureAndRequest();
            yield return new WaitForSecondsRealtime(sampleInterval);
        }
    }

    private static bool CanSample()
    {
        return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsAsyncGPUReadback;
    }

    private void CaptureAndRequest()
    {
        EnsureTextures();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);

        Rect rect = miniGameManager != null ? miniGameManager.MiniGameViewportRect : DefaultViewport;
        // 일부 그래픽 API는 화면 복사본이 위아래로 뒤집혀 있으므로 세로 위치를 맞춘다.
        float y = SystemInfo.graphicsUVStartsAtTop ? 1f - rect.y - rect.height : rect.y;
        Graphics.Blit(screenTexture, sampleTexture, rect.size, new Vector2(rect.x, y));

        readbackPending = true;
        AsyncGPUReadback.Request(sampleTexture, sampleTexture.mipmapCount - 1, TextureFormat.RGBA32, OnReadback);
    }

    private void OnReadback(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;
        if (this == null || request.hasError) return;
        var data = request.GetData<Color32>();
        if (data.Length == 0) return;
        // 가장 작은 밉(1x1)이 영역 전체 평균이다.
        sampledColor = data[0];
        hasSample = true;
    }

    private void EnsureTextures()
    {
        if (screenTexture == null || screenTexture.width != Screen.width || screenTexture.height != Screen.height)
        {
            if (screenTexture != null) screenTexture.Release();
            screenTexture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 0, RenderTextureFormat.ARGB32)
            { name = "MiniGameScreenGlow_Screen" };
        }
        if (sampleTexture == null)
        {
            sampleTexture = new RenderTexture(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32)
            { name = "MiniGameScreenGlow_Sample", useMipMap = true, autoGenerateMips = true };
        }
    }

    private void ReleaseTextures()
    {
        if (screenTexture != null) { screenTexture.Release(); Destroy(screenTexture); screenTexture = null; }
        if (sampleTexture != null) { sampleTexture.Release(); Destroy(sampleTexture); sampleTexture = null; }
        readbackPending = false;
    }

    private void OnValidate()
    {
        maxIntensity = Mathf.Max(0f, maxIntensity);
        sampleInterval = Mathf.Max(0.02f, sampleInterval);
        fadeSpeed = Mathf.Max(0.1f, fadeSpeed);
    }
}
