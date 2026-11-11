using System.Collections;
using UnityEngine;

// 모니터 경고부터 방문 퇴장까지 진행합니다. Shift를 확인하고 Retry를 여는 일은 기존 Director가 맡습니다.
public sealed class MotherComputerVisitEvent : MainGameEventBase
{
    public enum VisitPhase { Idle, Warning, Crawling, Standing, Approaching, Watching, Leaving, OpeningExit, Exiting, Closing }

    [SerializeField] private MotherActorController mother;
    [SerializeField] private DoorController exitDoor;
    [SerializeField] private AudioSource computerSource;
    [SerializeField] private AudioClip computerClip;
    [SerializeField] private Renderer monitorRenderer;
    [SerializeField, Min(0)] private int screenMaterialSlot = 1;
    [SerializeField] private Material warningScreenMaterial;
    [SerializeField] private Transform monitorOpening;
    [SerializeField] private Transform deskEdge;
    [SerializeField] private Transform standInFrontOfDesk;
    [SerializeField] private Transform[] approachBedPath;
    [SerializeField] private Transform[] approachExitPath;
    [SerializeField] private Transform[] exitPath;
    [SerializeField] private Transform playerLookTarget;

    [Header("Computer Visit Timing")]
    [Tooltip("컴퓨터 소리와 회색 모니터가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float noiseSeconds = 1.5f;
    [Tooltip("모니터에서 책상 끝까지 기어 나오며 크기가 커지는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float crawlSeconds = 2f;
    [Tooltip("책상 끝에서 바닥으로 내려와 일어서는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float standSeconds = 0.65f;
    [Tooltip("침대 앞에서 플레이어를 지켜보는 시간입니다.")]
    [SerializeField, Min(0f)] private float watchSeconds = 1f;
    [Tooltip("등장 순간의 크기를 현재 엄마 모델 크기에 대한 비율로 지정합니다.")]
    [SerializeField, Range(0.01f, 1f)] private float startScale = 0.13f;

    private Material[] originalMonitorMaterials;
    private ComputerCrawlPose crawlPose;
    private Vector3 standingScale;
    private float crawlTime;
    private float crawlWeight;
    private bool detectionActive;

    public VisitPhase Phase { get; private set; }
    public override bool IsDetectionActive => IsRunning && detectionActive;
    public override bool CanRun =>
        mother != null && mother.IsConfigured && mother.ActorAnimator.isHuman &&
        exitDoor != null && exitDoor.isActiveAndEnabled &&
        computerSource != null && computerSource.isActiveAndEnabled && computerClip != null &&
        monitorOpening != null && deskEdge != null && standInFrontOfDesk != null && playerLookTarget != null &&
        ValidPath(approachBedPath) && ValidPath(approachExitPath) && ValidPath(exitPath);

    protected override IEnumerator ExecuteEvent()
    {
        mother.Hide();
        exitDoor.SetOpenImmediate(false);
        Phase = VisitPhase.Warning;
        if (noiseSeconds > 0f)
        {
            StartWarning();
            yield return new WaitForSeconds(noiseSeconds);
            StopWarning();
        }

        // 경고가 끝나고 모델을 표시하는 순간부터 공통 발각 판정에 참여합니다.
        Transform root = mother.ActorRoot;
        standingScale = root.localScale;
        root.localScale = standingScale * startScale;
        mother.ShowAt(monitorOpening);
        Animator animator = mother.ActorAnimator;
        animator.Update(0f);
        crawlPose = new ComputerCrawlPose(animator);
        crawlTime = 0f;
        crawlWeight = 1f;
        detectionActive = true;
        Phase = VisitPhase.Crawling;
        yield return MovePose(deskEdge, crawlSeconds, standingScale, 1f);

        Phase = VisitPhase.Standing;
        yield return MovePose(standInFrontOfDesk, standSeconds, standingScale, 0f);
        RestoreCrawlPose();
        Phase = VisitPhase.Approaching;
        yield return mother.FollowPath(approachBedPath);
        Phase = VisitPhase.Watching;
        mother.LookAt(playerLookTarget);
        yield return new WaitForSeconds(watchSeconds);

        // 기존 방문의 공용 퇴장 지점을 사용하고 문이 완전히 닫힐 때까지 발각을 유지합니다.
        Phase = VisitPhase.Leaving;
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
        Phase = VisitPhase.Idle;
    }

    private void StartWarning()
    {
        computerSource.clip = computerClip;
        computerSource.Play();
        if (monitorRenderer == null || warningScreenMaterial == null) return;

        // 화면은 모니터 메시의 별도 슬롯입니다. 프레임과 원본 공유 재질은 편집하지 않습니다.
        Material[] materials = monitorRenderer.sharedMaterials;
        if (screenMaterialSlot < 0 || screenMaterialSlot >= materials.Length) return;
        originalMonitorMaterials = (Material[])materials.Clone();
        materials[screenMaterialSlot] = warningScreenMaterial;
        monitorRenderer.sharedMaterials = materials;
    }

    private void StopWarning()
    {
        if (computerSource != null) computerSource.Stop();
        if (originalMonitorMaterials == null) return;
        if (monitorRenderer != null) monitorRenderer.sharedMaterials = originalMonitorMaterials;
        originalMonitorMaterials = null;
    }

    // 하이어라키의 자세·위치에 맞춰 모니터에서 나오고, 책상 앞에 도착할 때 기본 서 있는 자세로 섞습니다.
    private IEnumerator MovePose(Transform destination, float seconds, Vector3 targetScale, float targetCrawlWeight)
    {
        Transform root = mother.ActorRoot;
        Vector3 initialPosition = root.position;
        Quaternion initialRotation = root.rotation;
        Vector3 initialScale = root.localScale;
        float initialWeight = crawlWeight;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, seconds);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            root.SetPositionAndRotation(Vector3.Lerp(initialPosition, destination.position, blend),
                Quaternion.Slerp(initialRotation, destination.rotation, blend));
            root.localScale = Vector3.Lerp(initialScale, targetScale, blend);
            crawlWeight = Mathf.Lerp(initialWeight, targetCrawlWeight, blend);
        }
    }

    private void LateUpdate()
    {
        // Animator가 기본 자세를 갱신한 뒤 컴퓨터 등장에 필요한 관절만 덮어씁니다.
        if (crawlPose == null) return;
        crawlTime += Time.deltaTime;
        crawlPose.Apply(mother.ActorRoot, crawlTime, crawlWeight);
    }

    private void RestoreCrawlPose()
    {
        if (crawlPose == null) return;
        crawlPose.Restore();
        crawlPose = null;
        mother.ActorRoot.localScale = standingScale;
    }

    protected override void OnCancelled()
    {
        // 발각·다른 이벤트 강제 실행·씬 종료 시 소리, 모니터, 공용 배우의 크기와 방문을 모두 복원합니다.
        detectionActive = false;
        StopWarning();
        if (mother != null)
        {
            RestoreCrawlPose();
            mother.Hide();
        }
        if (exitDoor != null) exitDoor.SetOpenImmediate(false);
        Phase = VisitPhase.Idle;
    }

    private static bool ValidPath(Transform[] points) =>
        points != null && points.Length > 0 && System.Array.TrueForAll(points, point => point != null);
}
