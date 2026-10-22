using System.Collections;
using UnityEngine;

// 문 방문 한 종류의 연출입니다. 다른 이벤트는 MainGameEventBase를 별도로 상속해 Director 목록에 등록합니다.
public sealed class MotherDoorVisitEvent : MainGameEventBase
{
    public enum VisitPhase { Idle, Footsteps, BeforeOpening, Opening, Entering, Watching, Leaving, Closing }

    [SerializeField] private DoorController door;
    [SerializeField] private MotherActorController mother;
    [SerializeField] private AudioSource footstepsSource;
    [SerializeField] private AudioClip footstepsClip;
    [SerializeField] private Transform outsidePoint;
    [SerializeField] private Transform[] entryPath;
    [SerializeField] private Transform[] exitPath;
    [SerializeField] private Transform playerLookTarget;
    [SerializeField, Min(0f)] private float afterFootstepsDelay = 1f;
    [SerializeField, Min(0f)] private float detectionDelay = 1f;
    [SerializeField] private Vector2 watchSeconds = new Vector2(4f, 7f);
    [SerializeField] private VisitPhase phase;
    private float detectionStartsAt = float.PositiveInfinity;

    public VisitPhase Phase => phase;
    public override bool CanRun => door != null && mother != null && mother.IsConfigured &&
        footstepsSource != null && footstepsClip != null && outsidePoint != null && playerLookTarget != null &&
        ValidPath(entryPath) && ValidPath(exitPath);

    // 문 애니메이션 완료를 기다리는 구간과 독립된 시계입니다. Open() 시작 + 1초부터 닫힘 완료까지 유지합니다.
    public override bool IsDetectionActive => IsRunning && Time.time >= detectionStartsAt && !door.IsClosed;

    protected override IEnumerator ExecuteEvent()
    {
        detectionStartsAt = float.PositiveInfinity;
        door.SetOpenImmediate(false);
        mother.Hide();
        phase = VisitPhase.Footsteps;
        footstepsSource.clip = footstepsClip;
        footstepsSource.loop = false;
        footstepsSource.Play();
        // 클립 길이를 추정해 기다리지 않고 실제 재생 종료를 기준으로 이후 1초를 셉니다.
        while (footstepsSource.isPlaying) yield return null;
        phase = VisitPhase.BeforeOpening;
        yield return new WaitForSeconds(afterFootstepsDelay);

        phase = VisitPhase.Opening;
        door.Open();
        detectionStartsAt = Time.time + detectionDelay;
        mother.ShowAt(outsidePoint);
        while (!door.IsOpen) yield return null;
        phase = VisitPhase.Entering;
        yield return mother.FollowPath(entryPath);

        phase = VisitPhase.Watching;
        mother.LookAt(playerLookTarget);
        float minWatch = Mathf.Max(0f, watchSeconds.x);
        yield return new WaitForSeconds(Random.Range(minWatch, Mathf.Max(minWatch, watchSeconds.y)));

        phase = VisitPhase.Leaving;
        yield return mother.FollowPath(exitPath);
        mother.Hide();
        phase = VisitPhase.Closing;
        door.Close();
        while (!door.IsClosed) yield return null;
        detectionStartsAt = float.PositiveInfinity;
        phase = VisitPhase.Idle;
    }

    protected override void OnCancelled()
    {
        // 게임오버는 공통 Retry 화면에서 처리합니다. 여기서는 소리·이동·문 연출을 남기지 않습니다.
        detectionStartsAt = float.PositiveInfinity;
        if (footstepsSource != null) footstepsSource.Stop();
        if (mother != null) mother.Hide();
        if (door != null) door.SetOpenImmediate(false);
        phase = VisitPhase.Idle;
    }

    private static bool ValidPath(Transform[] path)
    {
        if (path == null || path.Length == 0) return false;
        foreach (var point in path) if (point == null) return false;
        return true;
    }
}
