using UnityEngine;
using UnityEngine.SceneManagement;

// 1인칭 화면에 보이는 게임기(HandheldConsole)와 그것을 쥔 손을 배치·표시합니다.
// - MainGame의 Main Camera 자식(HandheldConsoleRig)에 붙입니다. 카메라를 따라 함께 회전합니다.
// - 게임기 화면(Screen 렌더러)이 미니게임 viewport와 정확히 겹치도록 위치·크기를 매 프레임 맞춥니다.
//   미니게임 화면 자체는 매니저가 지정한 viewport에 미니게임 카메라가 그리므로, 이 컴포넌트는 테두리만 맞춥니다.
// - 매니저는 Inspector 참조로 조회만 합니다(IsMiniGameHidden, IsGameFinished, MiniGameViewportRect).
//   싱글턴을 만들거나 숨김·발각 판정을 바꾸지 않습니다. 아래 연출은 화면에 보이는 모습만 다룹니다.
// - Shift로 숨기면 게임기를 가슴 쪽 이불 속으로 눕히며 내리고, 다 내려가면 침대 위 몸(PlayerInBed)의 쉬는 팔을 보여 줍니다.
//   Shift를 놓으면 쉬는 팔을 감추고 게임기를 다시 들어 올립니다.
// - 게임기가 제자리에 있지 않은 동안(올리는 중 등)에는 미니게임 카메라의 출력을 RenderTexture로 받아 게임기 화면에 그려,
//   미니게임 화면이 게임기 모델을 따라 움직이게 합니다. 제자리에 오면 원래대로 매니저 viewport에 직접 그리게 되돌립니다.
//   매니저가 정한 카메라 rect·depth·enabled는 바꾸지 않고 targetTexture만 잠시 빌립니다.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class HandheldConsoleView : MonoBehaviour
{
    [SerializeField] private MiniGameManager miniGameManager;
    [Tooltip("게임기를 들고 있는 카메라입니다. 비우면 부모 카메라를 사용합니다.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("미니게임 viewport에 맞출 게임기 화면 렌더러입니다(HandheldConsole/Model/Screen).")]
    [SerializeField] private Renderer screenRenderer;
    [Tooltip("게임기를 숨겼을 때 대신 보일 침대 위 쉬는 팔(PlayerInBed/Arms)입니다. 비워도 됩니다.")]
    [SerializeField] private GameObject restingArms;
    [Tooltip("게임기가 움직이는 동안 미니게임 화면을 게임기 화면에 그릴 Unlit 머티리얼(M_Console_ScreenLive)입니다. 비우면 이 기능을 쓰지 않습니다.")]
    [SerializeField] private Material liveScreenMaterial;

    [Header("Placement")]
    [Tooltip("카메라에서 게임기 화면까지의 거리(m)입니다. 카메라 Near Clip보다 커야 손가락·버튼이 잘리지 않습니다.")]
    [SerializeField, Min(0.01f)] private float screenDistance = 0.38f;

    [Header("Hide Motion")]
    [Tooltip("Shift를 눌렀을 때 게임기를 이불 속으로 내리는 시간(초)입니다. 0이면 즉시 사라집니다.")]
    [SerializeField, Min(0f)] private float lowerSeconds = 0.3f;
    [Tooltip("Shift를 놓았을 때 게임기를 다시 드는 시간(초)입니다. 미니게임 화면은 매니저가 즉시 다시 그리므로, " +
             "길게 하면 게임기가 올라오는 동안 화면이 허공에 먼저 보입니다. 짧게 유지합니다.")]
    [SerializeField, Min(0f)] private float raiseSeconds = 0.12f;
    [Tooltip("다 내렸을 때 화면 아래로 내려가는 거리(카메라에 보이는 화면 높이의 배수)입니다. " +
             "눕히는 각도와 함께 마지막 순간에 화면 밖으로 빠지도록 맞춘 값이며, 너무 크면 중간에 일찍 사라져 눕는 동작이 보이지 않습니다.")]
    [SerializeField, Min(0f)] private float lowerDrop = 0.85f;
    [Tooltip("다 내렸을 때 앞(가슴·이불 쪽)으로 밀려나는 거리(화면 높이의 배수)입니다.")]
    [SerializeField] private float lowerForward = 0.15f;
    [Tooltip("다 내렸을 때 게임기 윗부분이 앞으로 눕는 각도(도)입니다. 화면이 위를 보며 가슴 위에 놓이는 모습이 됩니다.")]
    [SerializeField, Range(0f, 90f)] private float lowerTiltDegrees = 55f;

    // 매니저를 읽지 못할 때(에디터 편집 중 등) 쓰는 기본 viewport. 매니저 기본값과 같다.
    private static readonly Rect DefaultViewport = new Rect(0.15f, 0.15f, 0.7f, 0.7f);
    private const float Epsilon = 1e-5f;

    private Renderer[] rigRenderers;
    private Bounds screenLocalBounds;
    private bool hasScreenBounds;
    private bool? lastVisible;
    // 0 = 들고 있음, 1 = 이불 속으로 다 내림. 숨김 판정과 무관한 화면 연출용 값이다.
    private float lowered;

    // 움직이는 동안 미니게임 화면을 게임기 화면에 옮겨 그리기 위한 상태.
    private RenderTexture screenTexture;
    private Material liveScreenInstance;
    private Material originalScreenMaterial;
    private Camera routedCamera;

    /// <summary>연출 진행도(0 = 들고 있음, 1 = 다 내림). 테스트·다른 연출에서 읽기 전용으로 사용한다.</summary>
    public float LoweredAmount => lowered;

    /// <summary>지금 미니게임 화면을 게임기 화면 텍스처로 옮겨 그리는 중인지(테스트·디버그용 읽기 전용).</summary>
    public bool IsScreenRoutedToConsole => routedCamera != null;

    private void OnEnable()
    {
        rigRenderers = GetComponentsInChildren<Renderer>(true);
        if (screenRenderer != null && originalScreenMaterial == null) originalScreenMaterial = screenRenderer.sharedMaterial;
        // 화면 크기는 리그 로컬 값으로 저장한다(InverseTransformPoint가 리그의 크기·회전을 되돌린다).
        hasScreenBounds = TryMeasureScreen(out screenLocalBounds);
        lastVisible = null;
        // 씬 시작·재시작 때는 내리고 올리는 동작 없이 현재 상태에서 바로 시작한다.
        lowered = Application.isPlaying && ShouldLower() ? 1f : 0f;
        Refresh(0f);
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼지면 쉬는 팔을 다시 켜서 침대 위 몸이 팔 없이 남지 않게 한다.
        if (Application.isPlaying && restingArms != null) restingArms.SetActive(true);
        lastVisible = null;
        // 빌린 미니게임 카메라 출력과 게임기 화면 머티리얼을 원래대로 돌려놓고 임시 자원을 정리한다.
        StopScreenRouting();
        if (screenTexture != null) { screenTexture.Release(); Destroy(screenTexture); screenTexture = null; }
        if (liveScreenInstance != null) { Destroy(liveScreenInstance); liveScreenInstance = null; }
    }

    // 카메라 회전(MainGameCameraLookController)과 매니저의 숨김 갱신(Update)이 끝난 뒤 맞춘다.
    // 게임오버 때 timeScale이 0이 되어도 연출이 멈추지 않도록 실제 시간으로 진행한다.
    private void LateUpdate()
    {
        Refresh(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 화면 크기를 다시 재고 즉시 맞춘다. 편집 모드에서는 LateUpdate가 매 프레임 돌지 않으므로
    /// 배치 도구나 Inspector 메뉴(우클릭 → Fit To Viewport Now)에서 호출한다.
    /// </summary>
    [ContextMenu("Fit To Viewport Now")]
    public void FitNow()
    {
        hasScreenBounds = TryMeasureScreen(out screenLocalBounds);
        Refresh(0f);
    }

    private void Refresh(float deltaTime)
    {
        if (Application.isPlaying)
        {
            float target = ShouldLower() ? 1f : 0f;
            float seconds = target > lowered ? lowerSeconds : raiseSeconds;
            lowered = seconds <= 0f ? target : Mathf.MoveTowards(lowered, target, deltaTime / seconds);
        }
        else
        {
            // 편집 중에는 배치를 확인할 수 있게 항상 든 상태로 보여 준다.
            lowered = 0f;
        }

        ApplyPose();
        // 다 내린 뒤에만 게임기를 끄고 쉬는 팔로 바꾼다. 올리기 시작하면 바로 게임기를 켠다.
        SetVisible(lowered < 1f);
        UpdateScreenRouting();
    }

    /// <summary>
    /// 게임기가 제자리(lowered = 0)가 아니고 미니게임 카메라가 켜져 있으면(= 화면이 보이는 상태로 움직이는 중),
    /// 그 카메라 출력을 RenderTexture로 받아 게임기 화면에 붙인다. 카메라의 viewport rect는 그대로 두므로
    /// RenderTexture 안에서도 같은 영역에 그려지고, 화면 머티리얼의 Tiling/Offset으로 그 영역만 잘라 쓴다.
    /// 숨김(Shift)으로 매니저가 카메라를 끄면 꺼진 화면(원래 머티리얼)을 보여 준다.
    /// </summary>
    private void UpdateScreenRouting()
    {
        if (!Application.isPlaying || screenRenderer == null || liveScreenMaterial == null) return;

        Camera miniCamera = lowered > 0f && miniGameManager != null && miniGameManager.IsMiniGameActive ? FindMiniGameCamera() : null;
        if (miniCamera == null || !miniCamera.enabled || !miniCamera.gameObject.activeInHierarchy)
        {
            StopScreenRouting();
            return;
        }

        EnsureScreenTexture();
        if (routedCamera != miniCamera)
        {
            StopScreenRouting();
            routedCamera = miniCamera;
        }
        miniCamera.targetTexture = screenTexture;

        if (liveScreenInstance == null) liveScreenInstance = new Material(liveScreenMaterial) { name = liveScreenMaterial.name + " (Live)" };
        Rect rect = miniGameManager.MiniGameViewportRect;
        liveScreenInstance.mainTexture = screenTexture;
        liveScreenInstance.mainTextureScale = rect.size;
        liveScreenInstance.mainTextureOffset = rect.position;
        if (screenRenderer.sharedMaterial != liveScreenInstance) screenRenderer.sharedMaterial = liveScreenInstance;
    }

    // 빌렸던 카메라 출력을 화면으로 되돌리고 게임기 화면 머티리얼을 원래 것으로 바꾼다.
    private void StopScreenRouting()
    {
        if (routedCamera != null && routedCamera.targetTexture == screenTexture) routedCamera.targetTexture = null;
        routedCamera = null;
        if (screenRenderer != null && originalScreenMaterial != null && screenRenderer.sharedMaterial != originalScreenMaterial)
        {
            screenRenderer.sharedMaterial = originalScreenMaterial;
        }
    }

    // 화면 해상도와 같은 크기여야 미니게임 Canvas 배율과 viewport 비율이 평소와 같다.
    private void EnsureScreenTexture()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (screenTexture != null && screenTexture.width == width && screenTexture.height == height) return;
        if (screenTexture != null)
        {
            if (routedCamera != null && routedCamera.targetTexture == screenTexture) routedCamera.targetTexture = null;
            screenTexture.Release();
            Destroy(screenTexture);
        }
        screenTexture = new RenderTexture(width, height, 24) { name = "HandheldConsole_LiveScreen" };
    }

    /// <summary>
    /// 로드된 미니게임 씬의 대표 카메라. 매니저와 같은 규칙(씬 루트에서 처음 찾은 활성 오브젝트의 Camera)으로 찾는다.
    /// 매니저의 비공개 필드는 읽지 않는다.
    /// </summary>
    private Camera FindMiniGameCamera()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene == gameObject.scene || !scene.name.StartsWith("MiniGame")) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = root.GetComponentInChildren<Camera>();
                if (camera != null) return camera;
            }
        }
        return null;
    }

    // 게임기를 내릴지: 숨김(Shift)이나 목표 달성 뒤에는 내린다. 미니게임 로딩 중에는 꺼진 화면의 게임기를 계속 든다.
    private bool ShouldLower()
    {
        if (miniGameManager == null) return false;
        return miniGameManager.IsMiniGameHidden || miniGameManager.IsGameFinished;
    }

    private void SetVisible(bool visible)
    {
        if (lastVisible == visible) return;
        lastVisible = visible;
        // 렌더러만 끄고 오브젝트는 켜 둬서 이 컴포넌트가 계속 상태를 따라가게 한다.
        foreach (Renderer rigRenderer in rigRenderers)
        {
            if (rigRenderer != null) rigRenderer.enabled = visible;
        }
        // 편집 중에 켜고 끄면 씬·프리팹에 비활성 상태가 저장되므로 플레이 중에만 바꾼다.
        if (Application.isPlaying && restingArms != null) restingArms.SetActive(!visible);
    }

    /// <summary>
    /// 든 자세: 게임기 화면이 viewport 사각형을 덮도록 리그의 로컬 위치·크기를 정한다.
    /// 화면 비율이 viewport와 다르면(16:9가 아닌 해상도) 긴 쪽에 맞춰 덮고, 남는 부분은 꺼진 화면색으로 보인다.
    /// 내린 자세: 화면 중심을 축으로 윗부분을 앞으로 눕히면서 아래·앞으로 옮겨 화면 밖(이불 쪽)으로 뺀다.
    /// </summary>
    private void ApplyPose()
    {
        Camera cam = targetCamera != null ? targetCamera : GetComponentInParent<Camera>();
        if (cam == null || !hasScreenBounds) return;

        Rect rect = miniGameManager != null ? miniGameManager.MiniGameViewportRect : DefaultViewport;
        float distance = Mathf.Max(screenDistance, cam.nearClipPlane + 0.01f);
        float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cam.aspect;

        var targetCenter = new Vector3((rect.center.x - 0.5f) * viewWidth, (rect.center.y - 0.5f) * viewHeight, distance);
        var targetSize = new Vector2(rect.width * viewWidth, rect.height * viewHeight);
        Vector3 size = screenLocalBounds.size;
        if (size.x < Epsilon || size.y < Epsilon) return;

        float scale = Mathf.Max(targetSize.x / size.x, targetSize.y / size.y);
        Vector3 screenCenter = screenLocalBounds.center * scale;

        // 내릴 때는 처음에 천천히, 올릴 때는 끝에서 천천히 움직이도록 같은 곡선(부드러운 S자)을 쓴다.
        float t = lowered * lowered * (3f - 2f * lowered);
        Quaternion rotation = Quaternion.Euler(lowerTiltDegrees * t, 0f, 0f);
        Vector3 offset = new Vector3(0f, -lowerDrop * viewHeight, lowerForward * viewHeight) * t;
        Vector3 position = targetCenter - rotation * screenCenter + offset;

        // 같은 값이면 쓰지 않아 편집 중 씬이 매 프레임 변경됨으로 표시되지 않게 한다.
        Transform rig = transform;
        if (rig.parent != cam.transform) return;
        if ((rig.localPosition - position).sqrMagnitude > Epsilon * Epsilon) rig.localPosition = position;
        if (Quaternion.Angle(rig.localRotation, rotation) > 0.01f) rig.localRotation = rotation;
        if (Mathf.Abs(rig.localScale.x - scale) > Epsilon || Mathf.Abs(rig.localScale.y - scale) > Epsilon || Mathf.Abs(rig.localScale.z - scale) > Epsilon)
        {
            rig.localScale = Vector3.one * scale;
        }
    }

    // 화면 메시의 여덟 모서리를 리그 로컬 좌표로 옮겨 경계 상자를 만든다.
    private bool TryMeasureScreen(out Bounds bounds)
    {
        bounds = default;
        var filter = screenRenderer != null ? screenRenderer.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return false;

        Bounds meshBounds = filter.sharedMesh.bounds;
        Transform screen = filter.transform;
        bool first = true;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
            Vector3 local = transform.InverseTransformPoint(screen.TransformPoint(corner));
            if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
            else bounds.Encapsulate(local);
        }
        return true;
    }

    private void OnValidate()
    {
        screenDistance = Mathf.Max(0.01f, screenDistance);
        lowerSeconds = Mathf.Max(0f, lowerSeconds);
        raiseSeconds = Mathf.Max(0f, raiseSeconds);
        if (isActiveAndEnabled)
        {
            hasScreenBounds = TryMeasureScreen(out screenLocalBounds);
        }
    }
}
