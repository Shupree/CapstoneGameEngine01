using System.Collections;
using UnityEngine;

// 인기척만 재생하는 안전한 연출입니다. 엄마/문 참조와 게임오버 호출을 갖지 않아 시각 연출이나 발각에 관여하지 않습니다.
public sealed class MotherFootstepsOnlyEvent : MainGameEventBase
{
    public enum FootstepsPhase { Idle, Approaching, Waiting, Retreating }

    [SerializeField] private AudioSource footstepsSource;
    [SerializeField] private AudioClip footstepsClip;
    [SerializeField, Min(0f)] private float turnaroundDelay = 1f;
    [Tooltip("돌아가는 소리의 음량 배율입니다. X는 재생 진행률(0~1), Y는 원래 음량의 배율이며 기본값은 1에서 0으로 줄어듭니다.")]
    [SerializeField] private AnimationCurve retreatVolumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private FootstepsPhase phase;
    private float volumeBeforeEvent;
    private bool hasSavedVolume;

    public FootstepsPhase Phase => phase;
    public override bool CanRun => footstepsSource != null && footstepsSource.isActiveAndEnabled && footstepsClip != null;
    public override bool IsDetectionActive => false;

    protected override IEnumerator ExecuteEvent()
    {
        // Inspector에서 정한 원래 크기를 기준으로 줄입니다. 접근 소리와 다음 이벤트에 감쇠된 음량을 넘기지 않습니다.
        volumeBeforeEvent = footstepsSource.volume;
        hasSavedVolume = true;
        footstepsSource.clip = footstepsClip;
        footstepsSource.loop = false;
        phase = FootstepsPhase.Approaching;
        footstepsSource.Play();
        // 문 방문과 같은 클립/재생 설정을 사용하고 실제 재생 종료를 기다려 접근 소리 길이를 맞춥니다.
        while (footstepsSource.isPlaying) yield return null;

        phase = FootstepsPhase.Waiting;
        yield return new WaitForSeconds(turnaroundDelay);

        // 되돌아가는 인기척도 같은 원본을 정상 속도로 한 번 더 재생합니다. 역재생이나 엄마 이동은 하지 않습니다.
        phase = FootstepsPhase.Retreating;
        double retreatStartedAt = AudioSettings.dspTime;
        float duration = footstepsClip.length / Mathf.Max(0.01f, Mathf.Abs(footstepsSource.pitch));
        footstepsSource.Play();
        while (footstepsSource.isPlaying)
        {
            // 오디오 시계로 진행률을 계산해 Tab 정지 등 게임 시간과 분리합니다. 클립 자체의 속도와 길이는 바꾸지 않습니다.
            float progress = Mathf.Clamp01((float)((AudioSettings.dspTime - retreatStartedAt) / duration));
            float multiplier = retreatVolumeCurve != null ? retreatVolumeCurve.Evaluate(progress) : 1f - progress;
            footstepsSource.volume = volumeBeforeEvent * Mathf.Clamp01(multiplier);
            yield return null;
        }
        RestoreVolume();
        phase = FootstepsPhase.Idle;
    }

    protected override void OnCancelled()
    {
        // 개발자 즉시 실행·게임오버·씬 종료는 공통 Cancel을 사용하며, 대기 뒤 소리가 다시 재생되지 않게 합니다.
        if (footstepsSource != null) footstepsSource.Stop();
        RestoreVolume();
        phase = FootstepsPhase.Idle;
    }

    // 정상 종료와 도중 취소가 같은 복원 경로를 사용해 재실행 시 접근 소리가 작게 시작하는 문제를 막습니다.
    private void RestoreVolume()
    {
        if (hasSavedVolume && footstepsSource != null) footstepsSource.volume = volumeBeforeEvent;
        hasSavedVolume = false;
    }

    private void OnValidate() => turnaroundDelay = Mathf.Max(0f, turnaroundDelay);
}
