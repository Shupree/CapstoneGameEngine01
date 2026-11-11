using System.Collections;
using UnityEngine;

// 옷장의 세 번의 소리부터 공용 엄마의 방문 퇴장까지 제어합니다. 발각과 게임오버는 Director가 처리합니다.
public sealed class MotherClosetVisitEvent : MainGameEventBase
{
    public enum VisitPhase
    {
        Idle, FirstQuietSound, BetweenQuietSounds, SecondQuietSound, BeforeReveal,
        Revealing, Rushing, Watching, Leaving, OpeningExit, Exiting, Closing
    }

    [SerializeField] private ClosetDoors closetDoors;
    [SerializeField] private DoorController exitDoor;
    [SerializeField] private MotherActorController mother;
    [SerializeField] private AudioSource closetSource;
    [SerializeField] private AudioClip closetClip;
    [SerializeField] private Transform revealPoint;
    [SerializeField] private Transform[] approachBedPath;
    [SerializeField] private Transform[] approachExitPath;
    [SerializeField] private Transform[] exitPath;
    [SerializeField] private Transform playerLookTarget;

    [Header("Closet Visit Timing")]
    [Tooltip("첫 번째 작은 소리의 재생이 끝난 뒤 두 번째 작은 소리까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float quietSoundInterval = 0.5f;
    [Tooltip("두 번째 작은 소리가 끝난 뒤 원래 음량의 소리와 문 열림을 시작하기까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float revealDelay = 1f;
    [Tooltip("옷장 문틈으로 엄마가 보인 뒤 달려오기까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float beforeRushDelay = 0.5f;
    [Tooltip("침대 앞에 멈춰 플레이어를 보는 시간입니다.")]
    [SerializeField, Min(0f)] private float watchSeconds = 1f;
    [SerializeField, Min(0.01f)] private float rushMoveSpeed = 8.25f;
    [SerializeField, Range(0.1f, 3f)] private float rushPlaybackSpeed = 1f;

    private const float QuietVolumeMultiplier = 0.75f;
    private float originalVolume;
    private bool detectionActive;
    public VisitPhase Phase { get; private set; }
    public override bool IsDetectionActive => IsRunning && detectionActive;
    public override bool CanRun =>
        closetDoors != null && closetDoors.isActiveAndEnabled && closetDoors.IsConfigured &&
        exitDoor != null && exitDoor.isActiveAndEnabled && mother != null && mother.IsConfigured &&
        closetSource != null && closetSource.isActiveAndEnabled && closetClip != null &&
        revealPoint != null && playerLookTarget != null &&
        ValidPath(approachBedPath) && ValidPath(approachExitPath) && ValidPath(exitPath);

    protected override IEnumerator ExecuteEvent()
    {
        originalVolume = closetSource.volume;
        closetSource.clip = closetClip;
        closetSource.loop = false;
        closetDoors.CloseImmediately();
        exitDoor.SetOpenImmediate(false);
        mother.Hide();

        // 각 작은 소리는 끝까지 재생합니다. 간격은 첫 재생이 끝난 시점부터 셉니다.
        Phase = VisitPhase.FirstQuietSound;
        PlaySound(QuietVolumeMultiplier);
        while (closetSource.isPlaying) yield return null;
        Phase = VisitPhase.BetweenQuietSounds;
        yield return new WaitForSeconds(quietSoundInterval);
        Phase = VisitPhase.SecondQuietSound;
        PlaySound(QuietVolumeMultiplier);
        while (closetSource.isPlaying) yield return null;
        Phase = VisitPhase.BeforeReveal;
        yield return new WaitForSeconds(revealDelay);

        // 본래 음량과 함께 문을 열고 배우를 표시합니다. 문틈으로 배우가 보일 때 발각과 돌진 대기를 시작합니다.
        Phase = VisitPhase.Revealing;
        PlaySound(1f);
        closetDoors.Open();
        mother.ShowAt(revealPoint);
        while (!closetDoors.HasVisibleOpening) yield return null;
        detectionActive = true;
        yield return new WaitForSeconds(beforeRushDelay);
        while (!closetDoors.IsOpen) yield return null;

        Phase = VisitPhase.Rushing;
        yield return mother.FollowPath(approachBedPath, rushMoveSpeed, rushPlaybackSpeed);
        Phase = VisitPhase.Watching;
        mother.LookAt(playerLookTarget);
        yield return new WaitForSeconds(watchSeconds);

        // 엄마가 침대를 떠나는 동안 옷장 문은 닫히고, 방문 앞에 도착하면 방문을 열어 퇴장합니다.
        Phase = VisitPhase.Leaving;
        closetDoors.Close();
        yield return mother.FollowPath(approachExitPath);
        Phase = VisitPhase.OpeningExit;
        exitDoor.Open();
        while (!exitDoor.IsOpen) yield return null;
        Phase = VisitPhase.Exiting;
        yield return mother.FollowPath(exitPath);
        mother.Hide();
        Phase = VisitPhase.Closing;
        exitDoor.Close();
        while (!exitDoor.IsClosed) yield return null;
        detectionActive = false;
        while (!closetDoors.IsClosed || closetSource.isPlaying) yield return null;
        Phase = VisitPhase.Idle;
    }

    private void PlaySound(float multiplier)
    {
        closetSource.volume = originalVolume * multiplier;
        closetSource.Play();
    }

    protected override void OnCancelled()
    {
        // 발각·재시작·다른 이벤트로 전환할 때 공용 배우, 두 문, 음향을 같은 프레임에 정리합니다.
        detectionActive = false;
        if (closetSource != null)
        {
            closetSource.Stop();
            closetSource.volume = originalVolume;
        }
        if (mother != null) mother.Hide();
        if (closetDoors != null) closetDoors.CloseImmediately();
        if (exitDoor != null) exitDoor.SetOpenImmediate(false);
        Phase = VisitPhase.Idle;
    }

    private static bool ValidPath(Transform[] points)
    {
        if (points == null || points.Length == 0) return false;
        foreach (var point in points)
            if (point == null) return false;
        return true;
    }
}
