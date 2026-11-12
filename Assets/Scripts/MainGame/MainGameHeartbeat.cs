using UnityEngine;

// 매니저의 Shift 처리와 Director의 발각 판정이 끝난 뒤 플레이어의 심장 소리를 갱신합니다.
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class MainGameHeartbeat : MonoBehaviour
{
    // 안전 구간에 적용하던 75% 음량을 한 번 더 25% 낮춥니다.
    private const float SafeVolumeMultiplier = 0.5625f;

    [SerializeField] private MiniGameManager miniGameManager;
    [SerializeField] private MainGameEventDirector eventDirector;
    [SerializeField] private AudioClip fastHeartbeat;

    private AudioSource heartbeatSource;
    private float baseVolume;

    private void Awake()
    {
        heartbeatSource = GetComponent<AudioSource>();
        // 씬의 Audio Source 음량을 발각 구간의 기준값으로 사용합니다.
        baseVolume = heartbeatSource.volume;
    }

    private void LateUpdate()
    {
        // 발각 판정을 기준으로 같은 심장 소리의 음량을 조절합니다.
        bool hiding = miniGameManager != null && miniGameManager.IsMiniGameHidden &&
            !miniGameManager.IsGameOver && !miniGameManager.IsGameFinished;
        if (hiding && fastHeartbeat != null)
            PlayHeartbeat(eventDirector != null && eventDirector.IsDetectionActive);
        else
            StopHeartbeat();
    }

    private void PlayHeartbeat(bool detectionActive)
    {
        // 안전·발각 구간을 오갈 때 음량만 바꿔 재생 위치를 이어갑니다.
        heartbeatSource.volume = detectionActive ? baseVolume : baseVolume * SafeVolumeMultiplier;
        if (heartbeatSource.clip == fastHeartbeat) return;
        heartbeatSource.clip = fastHeartbeat;
        heartbeatSource.Play();
    }

    private void StopHeartbeat()
    {
        // Shift 해제나 씬 종료 후에는 다음 재생을 위해 기준 음량을 복원합니다.
        if (heartbeatSource == null || heartbeatSource.clip == null) return;
        heartbeatSource.Stop();
        heartbeatSource.clip = null;
        heartbeatSource.volume = baseVolume;
    }

    private void OnDisable() => StopHeartbeat();
}
