using UnityEngine;

// 옷장 몸체와 분리한 문짝을 한 쌍으로 움직입니다. 이벤트는 열기·닫기 목표만 지정합니다.
public sealed class ClosetDoors : MonoBehaviour
{
    [SerializeField] private Transform firstHinge;
    [SerializeField] private Transform secondHinge;
    [SerializeField, Range(1f, 120f)] private float openAngle = 95f;
    [SerializeField, Min(0.01f)] private float openSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float closeSeconds = 1f;

    private Quaternion firstClosedRotation;
    private Quaternion secondClosedRotation;
    private float openness;
    private float target;

    public bool IsConfigured => firstHinge != null && secondHinge != null;
    public bool IsOpen => openness >= 1f && target >= 1f;
    public bool IsClosed => openness <= 0f && target <= 0f;
    // 문이 움직이기 시작한 프레임에는 배우가 아직 가려져 있으므로 틈이 생긴 시점을 노출 기준으로 씁니다.
    public bool HasVisibleOpening => openness >= 0.33f;

    private void Awake()
    {
        if (!IsConfigured) return;
        firstClosedRotation = firstHinge.localRotation;
        secondClosedRotation = secondHinge.localRotation;
    }

    public void Open() => target = 1f;
    public void Close() => target = 0f;

    // 게임오버와 즉시 실행으로 중단될 때 다음 이벤트가 닫힌 상태에서 시작하도록 복구합니다.
    public void CloseImmediately()
    {
        openness = target = 0f;
        if (IsConfigured) ApplyPose();
    }

    private void Update()
    {
        if (!IsConfigured || openness == target) return;
        float duration = target > openness ? openSeconds : closeSeconds;
        openness = Mathf.MoveTowards(openness, target, Time.deltaTime / Mathf.Max(0.01f, duration));
        ApplyPose();
    }

    private void ApplyPose()
    {
        float angle = openAngle * Mathf.SmoothStep(0f, 1f, openness);
        firstHinge.localRotation = Quaternion.AngleAxis(-angle, Vector3.up) * firstClosedRotation;
        secondHinge.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * secondClosedRotation;
    }
}
