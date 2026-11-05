using System.Collections;
using UnityEngine;

// 일반 문 방문과 독립된 추첨 항목입니다. 문·배우·경로는 공유하고 뜸 들이기·돌진·짧은 관찰의 순서만 소유합니다.
public sealed class MotherDoorRushEvent : MainGameEventBase
{
    public enum RushPhase { Idle, Footsteps, BeforeOpening, Opening, Hesitating, Entering, Watching, Leaving, Closing }

    [SerializeField] private DoorController door;
    [SerializeField] private MotherActorController mother;
    [SerializeField] private AudioSource footstepsSource;
    [SerializeField] private AudioClip footstepsClip;
    [SerializeField] private Transform outsidePoint;
    [SerializeField] private Transform[] entryPath;
    [SerializeField] private Transform[] exitPath;
    [SerializeField] private Transform playerLookTarget;
    [Header("Door Warning (Same Timing As Normal Visit)")]
    [SerializeField, Min(0f)] private float afterFootstepsDelay = 1f;
    [SerializeField, Min(0f)] private float detectionDelay = 1f;
    [Header("Rush Visit")]
    [Tooltip("문이 완전히 열린 뒤, 문 밖에서 움직이지 않고 뜸을 들이는 시간입니다.")]
    [SerializeField, Min(0f), InspectorName("돌진 전 대기 시간 (초)")] private float beforeRushDelay = 1f;
    [Tooltip("침대 옆에 도착해 플레이어를 바라보는 시간입니다. 왕복 이동 시간은 포함하지 않습니다.")]
    [SerializeField, Min(0f), InspectorName("관찰 시간 (초)")] private float watchSeconds = 1f;
    [Tooltip("진입·퇴장에 함께 적용하는 초당 이동 거리입니다. 일반 방문의 기본 속도는 변경하지 않습니다.")]
    // 왕복 이동의 기본값을 기존 5.5에서 50% 높입니다. 일반 방문 속도와 Run 재생 배율은 별도 설정으로 유지합니다.
    [SerializeField, Min(0.01f), InspectorName("돌진 이동 속도")] private float rushMoveSpeed = 8.25f;
    [SerializeField, Range(0.1f, 3f), InspectorName("Run 재생 속도")] private float rushPlaybackSpeed = 1f;
    [SerializeField] private RushPhase phase;
    private float detectionStartsAt = float.PositiveInfinity;

    public RushPhase Phase => phase;
    public override bool CanRun => door != null && mother != null && mother.IsConfigured &&
        footstepsSource != null && footstepsClip != null && outsidePoint != null && playerLookTarget != null &&
        ValidPath(entryPath) && ValidPath(exitPath);

    // 기존 문 방문과 같은 발각 시계입니다. 뜸·돌진·퇴장·닫힘을 포함하며 게임오버 처리는 Director에 위임합니다.
    public override bool IsDetectionActive => IsRunning && Time.time >= detectionStartsAt && !door.IsClosed;

    protected override IEnumerator ExecuteEvent()
    {
        detectionStartsAt = float.PositiveInfinity;
        door.SetOpenImmediate(false);
        mother.Hide();
        phase = RushPhase.Footsteps;
        footstepsSource.clip = footstepsClip;
        footstepsSource.loop = false;
        footstepsSource.Play();
        // 일반 방문처럼 실제 Step_Walk 종료 후 대기하고 문을 엽니다. 재생 길이나 문 열기 속도를 따로 추정하지 않습니다.
        while (footstepsSource.isPlaying) yield return null;
        phase = RushPhase.BeforeOpening;
        yield return new WaitForSeconds(afterFootstepsDelay);

        phase = RushPhase.Opening;
        door.Open();
        detectionStartsAt = Time.time + detectionDelay;
        mother.ShowAt(outsidePoint);
        while (!door.IsOpen) yield return null;
        phase = RushPhase.Hesitating;
        yield return new WaitForSeconds(beforeRushDelay);

        phase = RushPhase.Entering;
        yield return mother.FollowPath(entryPath, rushMoveSpeed, rushPlaybackSpeed);
        phase = RushPhase.Watching;
        mother.LookAt(playerLookTarget);
        yield return new WaitForSeconds(watchSeconds);

        phase = RushPhase.Leaving;
        yield return mother.FollowPath(exitPath, rushMoveSpeed, rushPlaybackSpeed);
        mother.Hide();
        phase = RushPhase.Closing;
        door.Close();
        while (!door.IsClosed) yield return null;
        detectionStartsAt = float.PositiveInfinity;
        phase = RushPhase.Idle;
    }

    protected override void OnCancelled()
    {
        // 개발자 즉시 실행·게임오버·씬 종료는 같은 정리를 사용합니다. 이전 이동이나 바뀐 애니메이션 속도를 다음 이벤트에 남기지 않습니다.
        detectionStartsAt = float.PositiveInfinity;
        if (footstepsSource != null) footstepsSource.Stop();
        if (mother != null) mother.Hide();
        if (door != null) door.SetOpenImmediate(false);
        phase = RushPhase.Idle;
    }

    private static bool ValidPath(Transform[] path)
    {
        if (path == null || path.Length == 0) return false;
        foreach (var point in path) if (point == null) return false;
        return true;
    }
}
