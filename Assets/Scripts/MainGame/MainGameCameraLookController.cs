using UnityEngine;
using UnityEngine.InputSystem;

// MainGame 카메라에만 배치합니다. 매니저의 Shift 숨김과 이벤트의 발각 판정이 끝난 뒤 시선을 갱신합니다.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MainGameCameraLookController : MonoBehaviour
{
    [SerializeField] private MiniGameManager miniGameManager;

    [Header("Horizontal Look Limits (Relative Degrees)")]
    [Tooltip("왼쪽 키의 수평 회전 한계(도)입니다. 최초 시선에서 음수 Y축 방향으로 돌아 책상·모니터를 바라봅니다.")]
    [SerializeField, Range(-180f, 0f)] private float leftYawOffset = -50.17f;
    [Tooltip("오른쪽 키의 수평 회전 한계(도)입니다. 최초 시선에서 양수 Y축 방향으로 돌아 창문을 바라봅니다.")]
    [SerializeField, Range(0f, 180f)] private float rightYawOffset = 30.52f;
    [Tooltip("좌우 공통 회전 속도(도/초)입니다. 기존 책상 방향 속도인 약 66.89도/초를 기준으로 설정합니다. 회전 범위와 별개로 속도를 유지합니다.")]
    [SerializeField, Min(0.01f)] private float rotationSpeedDegreesPerSecond = 66.89351f;

    private Vector3 initialLocalEuler;
    private float currentYawOffset;

    // -1=왼쪽(책상), 0=최초 시선, 1=오른쪽(창문)입니다. 키 방향과 실제 수평 회전 방향을 일치시킵니다.
    // 조회용 정규화 값만 좌우 범위에 맞춥니다. 실제 이동은 각도 단위로 계산해 정면 통과 시에도 속도가 변하지 않습니다.
    public float CurrentLookAmount => currentYawOffset < 0f ?
        -Mathf.InverseLerp(0f, leftYawOffset, currentYawOffset) : Mathf.InverseLerp(0f, rightYawOffset, currentYawOffset);
    public bool CanLook => miniGameManager != null && miniGameManager.IsMiniGameHidden &&
        !miniGameManager.IsGameOver && !miniGameManager.IsGameFinished;

    private void Awake()
    {
        // MainGame 재생성 때만 기준 시선을 새로 저장합니다. Shift 해제나 미니게임 교체 때 초기화하지 않습니다.
        initialLocalEuler = transform.localEulerAngles;
    }

    private void LateUpdate()
    {
        // 숨김 여부는 매니저 한 곳에서 결정합니다. 카메라 입력은 Tab 정지·미니게임 로직·이벤트 시간을 바꾸지 않습니다.
        if (!CanLook || Keyboard.current == null) return;
        var keyboard = Keyboard.current;
        int direction = (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0);
        // 키를 놓거나 양쪽 키를 함께 누르면 마지막 각도를 그대로 유지합니다. 화면을 올려도 자동 복귀하지 않습니다.
        if (direction == 0) return;
        // 좌우 한계까지 같은 시간을 쓰면 범위가 넓은 쪽이 빨라집니다. 공통 각속도로 실제 각도를 이동해 이를 방지합니다.
        float targetYawOffset = direction < 0 ? leftYawOffset : rightYawOffset;
        currentYawOffset = Mathf.MoveTowards(currentYawOffset, targetYawOffset,
            Mathf.Max(0.01f, rotationSpeedDegreesPerSecond) * Time.deltaTime);
        ApplyLookRotation();
    }

    private void ApplyLookRotation()
    {
        // Y축만 회전합니다. 최초 상하 각도·기울기·위치·시야각을 유지하고 창문을 위한 자동 보정은 하지 않습니다.
        transform.localRotation = Quaternion.Euler(initialLocalEuler + new Vector3(0f, currentYawOffset, 0f));
    }

    private void OnValidate()
    {
        // 방향별 부호를 고정하고 공통 속도를 양수로 제한합니다. 한계 각도를 바꿔도 각속도는 별도로 유지합니다.
        rotationSpeedDegreesPerSecond = Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
        leftYawOffset = Mathf.Clamp(leftYawOffset, -180f, 0f);
        rightYawOffset = Mathf.Clamp(rightYawOffset, 0f, 180f);
    }
}
