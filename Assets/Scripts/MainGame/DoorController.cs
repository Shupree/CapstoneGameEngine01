using System;
using UnityEngine;

// 방 문을 경첩(오브젝트 피벗) 기준으로 Y축 회전시켜 열고 닫습니다.
// 입력은 직접 받지 않습니다. 엄마 시스템 등 MainGame 쪽 코드가 Open()/Close()/Toggle()을 호출하고,
// 열림 정도(Openness)와 이벤트로 등장 연출이나 발각 판정을 연결하는 용도입니다.
public class DoorController : MonoBehaviour
{
    [Header("Hinge")]
    [Tooltip("회전시킬 Transform입니다. 비우면 이 오브젝트를 회전시킵니다. 피벗이 경첩 위치에 있어야 합니다.")]
    [SerializeField] private Transform hinge;
    [Tooltip("닫힌 상태 기준, 부모 공간 Y축(위쪽) 회전 각도입니다. MainGame의 Door_7은 양수일 때 방 안쪽으로 열립니다.")]
    [SerializeField] private float openAngle = 90f;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float openDuration = 1.2f;
    [SerializeField, Min(0.01f)] private float closeDuration = 0.8f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("켜면 Time.timeScale의 영향을 받지 않습니다. 현재 Shift 숨김과 게임오버는 전역 timeScale을 0으로 만들기 때문에, " +
             "끈 상태(기본)에서는 그동안 문도 멈춥니다. MainGame 시간 정책이 바뀌면 함께 정합니다.")]
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private bool startOpen;

    private Quaternion closedRotation;
    private float openness;   // 0 = 완전히 닫힘, 1 = 완전히 열림
    private float target;
    private bool initialized;

    // 0(닫힘) ~ 1(열림). 엄마가 문틈으로 보는지 등 부분 열림 상태 판정에 사용할 수 있습니다.
    public float Openness => openness;
    public bool IsOpen => target >= 1f && openness >= 1f;
    public bool IsClosed => target <= 0f && openness <= 0f;
    public bool IsMoving => openness != target;
    public bool IsOpening => target > openness;

    // 인자: true = 여는 중 / 완전히 열림, false = 닫는 중 / 완전히 닫힘.
    // 구독한 쪽은 자신의 수명에 맞춰 구독을 해제해야 합니다.
    public event Action<bool> MovementStarted;
    public event Action<bool> MovementFinished;

    private void Awake()
    {
        EnsureInitialized();
    }

    // 닫힌 회전값은 씬에 저장된 문 회전을 기준으로 한 번만 기록합니다.
    // Awake 전에 다른 오브젝트가 Open()을 호출해도 같은 기준을 쓰도록 여기서 초기화합니다.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        if (hinge == null) hinge = transform;
        closedRotation = hinge.localRotation;
        openness = target = startOpen ? 1f : 0f;
        ApplyRotation();
    }

    public void Open() => SetTarget(1f);
    public void Close() => SetTarget(0f);
    public void Toggle() => SetTarget(target > 0.5f ? 0f : 1f);

    // 애니메이션 없이 즉시 상태를 맞춥니다. 재시작 직후 초기 배치 등에 사용하며 이벤트는 보내지 않습니다.
    public void SetOpenImmediate(bool open)
    {
        EnsureInitialized();
        openness = target = open ? 1f : 0f;
        ApplyRotation();
    }

    // 움직이는 도중 반대 명령이 오면 현재 위치에서 바로 방향을 바꿉니다.
    private void SetTarget(float value)
    {
        EnsureInitialized();
        if (target == value) return;
        target = value;
        MovementStarted?.Invoke(value > 0.5f);
    }

    private void Update()
    {
        if (openness == target) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float duration = target > openness ? openDuration : closeDuration;
        openness = Mathf.MoveTowards(openness, target, dt / duration);
        ApplyRotation();

        if (openness == target) MovementFinished?.Invoke(target >= 1f);
    }

    // 부모 공간의 위쪽(Y) 축을 기준으로 회전합니다.
    // Door_7처럼 FBX 임포트로 X축이 -90도 돌아간 모델은 자기 자신의 Y축이 위가 아니므로,
    // 닫힌 회전값 앞에 곱해(부모 기준 회전) 모델 축 방향과 상관없이 수평으로 여닫히게 합니다.
    private void ApplyRotation()
    {
        hinge.localRotation = Quaternion.Euler(0f, openAngle * easing.Evaluate(openness), 0f) * closedRotation;
    }

    // Play 모드에서 Inspector 우클릭 메뉴로 동작을 확인하기 위한 항목입니다.
    [ContextMenu("Toggle Door")]
    private void ToggleFromInspector()
    {
        if (Application.isPlaying) Toggle();
        else Debug.LogWarning("[DoorController] Toggle Door는 Play 모드에서만 동작합니다.", this);
    }
}
