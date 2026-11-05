using UnityEngine;

// 창짝의 개폐만 담당합니다. 효과음·엄마 등장·발각은 개별 이벤트가 제어하므로 다른 창문 연출에도 재사용할 수 있습니다.
public sealed class WindowController : MonoBehaviour
{
    [SerializeField] private Transform slidingPanel;
    [Tooltip("닫힌 위치에서 열린 위치까지의 부모 공간 이동량입니다. 현재 창문은 아래 창짝을 위로 밀어 엽니다.")]
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 3.32f, 0f);
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    private Vector3 closedLocalPosition;
    private float openness, target, motionSeconds = 1f;
    private bool initialized;

    public bool IsConfigured => slidingPanel != null;
    public float Openness => openness;
    public bool IsOpen => target == 1f && openness == 1f;
    public bool IsClosed => target == 0f && openness == 0f;

    private void Awake() => EnsureInitialized();

    // 다른 컴포넌트의 Awake/시작 호출이 먼저 와도 씬에 저장한 닫힌 위치를 한 번만 기록합니다.
    private void EnsureInitialized()
    {
        if (initialized || slidingPanel == null) return;
        closedLocalPosition = slidingPanel.localPosition;
        initialized = true;
    }

    // 이벤트가 개폐 시간을 넘깁니다. 음원 유무와 관계없이 같은 창짝 이동과 상태 판정을 재사용합니다.
    public void Open(float seconds) => SetTarget(1f, seconds);
    public void Close(float seconds) => SetTarget(0f, seconds);

    private void SetTarget(float value, float seconds)
    {
        EnsureInitialized();
        motionSeconds = Mathf.Max(0.01f, seconds);
        target = value;
    }

    // 취소·게임오버·재시작에서 코루틴이나 전역 시간에 의존하지 않고 원래 창문을 복원합니다.
    public void SetOpenImmediate(bool open)
    {
        EnsureInitialized();
        openness = target = open ? 1f : 0f;
        ApplyPosition();
    }

    private void Update()
    {
        if (!initialized || openness == target) return;
        openness = Mathf.MoveTowards(openness, target, Time.deltaTime / motionSeconds);
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        if (slidingPanel != null && initialized)
            slidingPanel.localPosition = closedLocalPosition + openLocalOffset * easing.Evaluate(openness);
    }
}
