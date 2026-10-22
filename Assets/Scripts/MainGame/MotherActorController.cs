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

    // MainGame 하이어라키의 경로를 따릅니다. 가구를 피해 배치한 중간 지점은 진입/퇴장 경로 양쪽에서 사용할 수 있습니다.
    public IEnumerator FollowPath(Transform[] points)
    {
        SetMoving(true);
        foreach (var point in points)
        {
            while (Vector3.Distance(actorRoot.position, point.position) > 0.01f)
            {
                Vector3 direction = point.position - actorRoot.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    actorRoot.rotation = Quaternion.RotateTowards(actorRoot.rotation,
                        Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
                actorRoot.position = Vector3.MoveTowards(actorRoot.position, point.position, moveSpeed * Time.deltaTime);
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

    private void SetMoving(bool moving)
    {
        IsMoving = moving;
        if (animator == null) return;
        animator.SetFloat(SpeedParameter, moving ? 1f : 0f);
        // 기존 Run만 감속합니다. 멈춘 뒤 Idle까지 느려지지 않도록 재생 속도를 복원합니다.
        animator.speed = moving ? runPlaybackSpeed : 1f;
    }

    public void Hide()
    {
        SetMoving(false);
        if (actorRoot != null) actorRoot.gameObject.SetActive(false);
    }

    private void OnDisable() => Hide();
}
