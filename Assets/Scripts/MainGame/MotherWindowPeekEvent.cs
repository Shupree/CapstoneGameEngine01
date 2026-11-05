using System.Collections;
using UnityEngine;

// 새 이벤트는 공통 계약으로만 Director에 등록합니다. 문 방문·발소리 이벤트와 같은 엄마 배우를 한 번에 하나씩 사용합니다.
public sealed class MotherWindowPeekEvent : MainGameEventBase
{
    public enum PeekPhase { Idle, FirstKnock, AfterFirstKnock, SecondKnock, AfterSecondKnock, Opening, Entering, Watching, Leaving, Closing }

    [SerializeField] private WindowController window;
    [SerializeField] private MotherActorController mother;
    [SerializeField] private AudioSource windowSource;
    [SerializeField] private AudioClip knockClip;
    [SerializeField] private Transform outsidePoint;
    [SerializeField] private Transform peekPoint;
    [Header("Inspector Delays (Seconds)")]
    [Tooltip("첫 번째 노크 효과음이 완전히 끝난 뒤 두 번째 노크까지의 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float afterFirstKnockDelay = 1f;
    [Tooltip("두 번째 노크 효과음이 완전히 끝난 뒤 창문 열기까지의 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float afterSecondKnockDelay = 1f;
    [Tooltip("얼굴과 어깨를 들이민 자세에 도착한 뒤 다시 물러나기 전까지의 시간입니다.")]
    [SerializeField, Min(0f)] private float watchSeconds = 1f;
    [Tooltip("얼굴을 들이밀거나 물러나는 데 걸리는 시간입니다. 걷기 애니메이션 대신 Idle 자세로 보간합니다.")]
    [SerializeField, Min(0.01f)] private float peekTransitionSeconds = 0.35f;
    [Header("Silent Window Motion (Seconds)")]
    [Tooltip("창문이 열리는 시간입니다. 개폐 효과음을 사용하지 않으므로 음원 길이와 독립적으로 조절합니다.")]
    [SerializeField, Min(0.01f)] private float windowOpenSeconds = 0.8f;
    [Tooltip("창문이 닫히는 시간입니다. 닫힘이 끝날 때까지 발각 판정을 유지합니다.")]
    [SerializeField, Min(0.01f)] private float windowCloseSeconds = 0.8f;
    [SerializeField] private PeekPhase phase;
    private bool detectionActive;

    public PeekPhase Phase => phase;
    public override bool CanRun => window != null && window.IsConfigured && mother != null && mother.IsConfigured &&
        windowSource != null && knockClip != null && outsidePoint != null && peekPoint != null;

    // 등장 시작부터 창문이 완전히 닫힐 때까지 위험합니다. 카메라 방향·Tab 정지는 면제 조건이 아니며 Shift 숨김은 Director가 판정합니다.
    public override bool IsDetectionActive => IsRunning && detectionActive && window != null && !window.IsClosed;

    protected override IEnumerator ExecuteEvent()
    {
        detectionActive = false;
        window.SetOpenImmediate(false);
        mother.Hide();
        phase = PeekPhase.FirstKnock;
        yield return PlaySound(knockClip);
        phase = PeekPhase.AfterFirstKnock;
        yield return new WaitForSeconds(afterFirstKnockDelay);
        phase = PeekPhase.SecondKnock;
        yield return PlaySound(knockClip);
        phase = PeekPhase.AfterSecondKnock;
        yield return new WaitForSeconds(afterSecondKnockDelay);

        phase = PeekPhase.Opening;
        // 개폐음은 재생하지 않습니다. 노크와 별도로 Inspector의 개폐 시간만 기다립니다.
        window.Open(windowOpenSeconds);
        while (!window.IsOpen) yield return null;

        phase = PeekPhase.Entering;
        mother.ShowAt(outsidePoint);
        detectionActive = true;
        yield return mother.BlendPose(peekPoint, peekTransitionSeconds);
        phase = PeekPhase.Watching;
        yield return new WaitForSeconds(watchSeconds);

        phase = PeekPhase.Leaving;
        yield return mother.BlendPose(outsidePoint, peekTransitionSeconds);
        mother.Hide();
        phase = PeekPhase.Closing;
        window.Close(windowCloseSeconds);
        // 엄마가 사라져도 닫히는 동안에는 발각 판정을 유지합니다. 완전히 닫힌 뒤에만 다음 이벤트 대기를 시작합니다.
        while (!window.IsClosed) yield return null;
        detectionActive = false;
        phase = PeekPhase.Idle;
    }

    // 노크 종료 시점은 고정 길이 추정 대신 AudioSource의 실제 재생 상태로 확인합니다.
    private IEnumerator PlaySound(AudioClip clip)
    {
        windowSource.clip = clip;
        windowSource.loop = false;
        windowSource.Play();
        while (windowSource.isPlaying) yield return null;
    }

    protected override void OnCancelled()
    {
        // 기존 Retry·목표 달성·개발자 즉시 실행 취소 경로에서 소리, 공용 배우, 창문을 함께 정리합니다.
        detectionActive = false;
        if (windowSource != null) windowSource.Stop();
        if (mother != null) mother.Hide();
        if (window != null) window.SetOpenImmediate(false);
        phase = PeekPhase.Idle;
    }

    private void OnValidate()
    {
        afterFirstKnockDelay = Mathf.Max(0f, afterFirstKnockDelay);
        afterSecondKnockDelay = Mathf.Max(0f, afterSecondKnockDelay);
        watchSeconds = Mathf.Max(0f, watchSeconds);
        peekTransitionSeconds = Mathf.Max(0.01f, peekTransitionSeconds);
        windowOpenSeconds = Mathf.Max(0.01f, windowOpenSeconds);
        windowCloseSeconds = Mathf.Max(0.01f, windowCloseSeconds);
    }
}
