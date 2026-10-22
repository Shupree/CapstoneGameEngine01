using System.Collections.Generic;
using UnityEngine;

// MainGame 씬에만 존재합니다. 미니게임 로딩/Tab 정지와 독립적으로 한 번에 하나의 이벤트를 진행합니다.
public sealed class MainGameEventDirector : MonoBehaviour
{
    public enum DirectorState { Waiting, Running, Stopped }

    [SerializeField] private MiniGameManager miniGameManager;
    [SerializeField] private List<MainGameEventBase> events = new List<MainGameEventBase>();
    [SerializeField] private Vector2 intervalSeconds = new Vector2(30f, 120f);
    [SerializeField] private DirectorState state;
    [SerializeField] private float secondsUntilNextEvent;
    private readonly List<MainGameEventBase> candidates = new List<MainGameEventBase>();
    private MainGameEventBase activeEvent;

    public DirectorState State => state;
    public MainGameEventBase ActiveEvent => activeEvent;
    public float SecondsUntilNextEvent => secondsUntilNextEvent;

    private void OnEnable()
    {
        // Singleton getter로 없는 매니저를 생성하지 않습니다. 저장된 MainGame 참조로만 연결합니다.
        if (miniGameManager == null)
        {
            state = DirectorState.Stopped;
            Debug.LogError("[MainGame Event] MiniGameManager 참조가 필요합니다.", this);
            return;
        }
        miniGameManager.GameOverTriggered += HandleGameOver;
        miniGameManager.GameFinished += StopEvents;
        if (miniGameManager.IsGameOver || miniGameManager.IsGameFinished) StopEvents();
        else ScheduleNextEvent();
    }

    private void Update()
    {
        if (state == DirectorState.Stopped) return;
        if (state == DirectorState.Running)
        {
            // 완료는 엄마 퇴장과 문 닫힘까지 포함합니다. 그 프레임부터 새 대기 시간을 셉니다.
            if (activeEvent == null || !activeEvent.IsRunning)
            {
                activeEvent = null;
                ScheduleNextEvent();
            }
            return;
        }

        secondsUntilNextEvent = Mathf.Max(0f, secondsUntilNextEvent - Time.deltaTime);
        if (secondsUntilNextEvent > 0f) return;
        candidates.Clear();
        foreach (var entry in events)
            if (entry != null && entry.isActiveAndEnabled && entry.CanRun) candidates.Add(entry);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[MainGame Event] 실행 가능한 이벤트가 없어 다음 주기에 다시 확인합니다.", this);
            ScheduleNextEvent();
            return;
        }
        activeEvent = candidates[Random.Range(0, candidates.Count)];
        state = DirectorState.Running;
        if (!activeEvent.TryBegin())
        {
            activeEvent = null;
            ScheduleNextEvent();
        }
    }

    private void LateUpdate()
    {
        // 매니저의 Update에서 Shift 숨김을 먼저 반영합니다. Tab 정지 여부는 발각을 막지 않습니다.
        if (state != DirectorState.Running || activeEvent == null || miniGameManager == null) return;
        if (activeEvent.IsDetectionActive && !miniGameManager.IsMiniGameHidden)
            miniGameManager.TriggerGameOver(EGameOverReason.CaughtByMother);
    }

    private void ScheduleNextEvent()
    {
        float min = Mathf.Max(0f, intervalSeconds.x);
        secondsUntilNextEvent = Random.Range(min, Mathf.Max(min, intervalSeconds.y));
        state = DirectorState.Waiting;
    }

    private void HandleGameOver(EGameOverReason reason) => StopEvents();

    // 공통 종료만 담당하며 개별 이벤트의 문/배우 종류는 알지 못합니다.
    public void StopEvents()
    {
        state = DirectorState.Stopped;
        secondsUntilNextEvent = 0f;
        if (activeEvent != null) activeEvent.Cancel();
        activeEvent = null;
    }

    private void OnDisable()
    {
        if (miniGameManager != null)
        {
            miniGameManager.GameOverTriggered -= HandleGameOver;
            miniGameManager.GameFinished -= StopEvents;
        }
        StopEvents();
    }
}
