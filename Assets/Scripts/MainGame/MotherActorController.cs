using System.Collections;
using UnityEngine;

// 배우의 이동과 애니메이션만 담당합니다. 이벤트 종류·간격·발각 조건과 무관하므로 다른 진입 경로에서도 재사용합니다.
public sealed class MotherActorController : MonoBehaviour
{
    [SerializeField] private Transform actorRoot;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.2f;
    [SerializeField, Min(1f)] private float turnSpeed = 240f;
    [SerializeField, Range(0.1f, 1f)] private float runPlaybackSpeed = 0.45f;
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    public bool IsMoving { get; private set; }
    public bool IsConfigured => actorRoot != null && animator != null;

    private void Awake()
    {
        // 원본 프리팹의 Root Motion은 사용하지 않습니다. 이동 거리는 이 컴포넌트가 일관되게 제어합니다.
        if (animator != null) animator.applyRootMotion = false;
        Hide();
    }

    public void ShowAt(Transform point)
    {
        actorRoot.SetPositionAndRotation(point.position, point.rotation);
        actorRoot.gameObject.SetActive(true);
        SetMoving(false);
    }

    // 창문 등장처럼 걷지 않는 연출용입니다. Inspector 자세의 상하 기울기까지 보간하고 Idle·Root Motion 해제 정책을 유지합니다.
    public IEnumerator BlendPose(Transform point, float seconds)
    {
        SetMoving(false);
        Vector3 startPosition = actorRoot.position;
        Quaternion startRotation = actorRoot.rotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, seconds);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            actorRoot.SetPositionAndRotation(Vector3.Lerp(startPosition, point.position, blend),
                Quaternion.Slerp(startRotation, point.rotation, blend));
            yield return null;
        }
        actorRoot.SetPositionAndRotation(point.position, point.rotation);
    }

    // MainGame 하이어라키의 경로를 따릅니다. 가구를 피해 배치한 중간 지점은 진입/퇴장 경로 양쪽에서 사용할 수 있습니다.
    public IEnumerator FollowPath(Transform[] points) => FollowPath(points, moveSpeed, runPlaybackSpeed);

    // 돌진 등 개별 연출의 속도는 이번 이동에만 적용합니다. 공유 배우의 기본값을 덮어쓰지 않아 다음 일반 방문·취소에 영향을 주지 않습니다.
    public IEnumerator FollowPath(Transform[] points, float movementSpeed, float animationSpeed)
    {
        SetMoving(true, animationSpeed);
        float speed = Mathf.Max(0.01f, movementSpeed);
        foreach (var point in points)
        {
            while (Vector3.Distance(actorRoot.position, point.position) > 0.01f)
            {
                Vector3 direction = point.position - actorRoot.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    actorRoot.rotation = Quaternion.RotateTowards(actorRoot.rotation,
                        Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
                actorRoot.position = Vector3.MoveTowards(actorRoot.position, point.position, speed * Time.deltaTime);
                yield return null;
            }
            actorRoot.position = point.position;
        }
        SetMoving(false);
    }

    // 침대 옆에서는 플레이어 쪽을 향합니다. Y축 높이 차이 때문에 배우가 기울어지지 않게 합니다.
    public void LookAt(Transform target)
    {
        Vector3 direction = target.position - actorRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) actorRoot.rotation = Quaternion.LookRotation(direction);
    }

    private void SetMoving(bool moving, float? animationSpeed = null)
    {
        IsMoving = moving;
        if (animator == null) return;
        animator.SetFloat(SpeedParameter, moving ? 1f : 0f);
        // 일반 방문은 감속한 Run, 돌진은 전달받은 속도로 재생합니다. 관찰·취소 뒤 Idle은 항상 1배로 복원합니다.
        animator.speed = moving ? Mathf.Max(0.1f, animationSpeed ?? runPlaybackSpeed) : 1f;
    }

    public void Hide()
    {
        SetMoving(false);
        if (actorRoot != null) actorRoot.gameObject.SetActive(false);
    }

    private void OnDisable() => Hide();
}
