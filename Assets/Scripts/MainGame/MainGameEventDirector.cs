using System.Collections.Generic;
using UnityEngine;

// MainGame 씬에만 존재합니다. 미니게임 로딩/Tab 정지와 독립적으로 한 번에 하나의 이벤트를 진행합니다.
public sealed class MainGameEventDirector : MonoBehaviour
{
    public enum DirectorState { Waiting, Running, Stopped }

    // 이벤트 종류와 선택 비중을 한 목록에서 관리합니다. 합이 100이 아니어도 유효한 가중치의 비율로 선택합니다.
    [System.Serializable]
    public sealed class EventEntry
    {
        [SerializeField, InspectorName("이벤트")] private MainGameEventBase eventBehaviour;
        [SerializeField, Min(0), InspectorName("가중치")]
        [Tooltip("0이면 선택하지 않습니다. 예: 문 방문 75, 발소리 25를 입력하면 각각 75%, 25%입니다.")]
        private int weight = 1;

        public MainGameEventBase Event => eventBehaviour;
        public int Weight => Mathf.Max(0, weight);

        public EventEntry(MainGameEventBase eventBehaviour, int weight)
        {
            this.eventBehaviour = eventBehaviour;
            this.weight = Mathf.Max(0, weight);
        }
    }

    [SerializeField] private MiniGameManager miniGameManager;
    // 이전 씬의 단순 참조 목록을 유지해 역직렬화 시 참조를 잃지 않습니다. 새 목록으로 옮긴 뒤 비웁니다.
    [SerializeField, HideInInspector]
    private List<MainGameEventBase> events = new List<MainGameEventBase>();
    [Header("Weighted Random Events")]
    [Tooltip("이벤트 참조와 가중치를 함께 설정합니다. 실행 가능한 이벤트들의 가중치 합을 기준으로 확률을 계산합니다.")]
    [SerializeField, InspectorName("이벤트별 선택 가중치")]
    private List<EventEntry> eventEntries = new List<EventEntry>();
    [Header("Next Event Delay (Seconds)")]
    [Tooltip("이벤트가 완전히 종료된 뒤 다음 이벤트까지의 대기 시간입니다. X=최소, Y=최대이며 같으면 고정 시간입니다. 변경값은 다음 대기 예약부터 적용됩니다.")]
    [SerializeField] private Vector2 intervalSeconds = new Vector2(30f, 120f);
    [Header("Runtime State (Read for debugging)")]
    [SerializeField] private DirectorState state;
    [SerializeField] private float secondsUntilNextEvent;
    private readonly List<EventEntry> candidates = new List<EventEntry>();
    private MainGameEventBase activeEvent;

    public DirectorState State => state;
    public MainGameEventBase ActiveEvent => activeEvent;
    public float SecondsUntilNextEvent => secondsUntilNextEvent;

    private void OnEnable()
    {
        UpgradeLegacyEvents();
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
        TryStartRandomEvent();
    }

    // 자동 예약과 개발자 도구가 같은 가중치 선택 경로를 사용합니다. 이전 종류가 다시 선택되는 것도 허용합니다.
    private bool TryStartRandomEvent()
    {
        activeEvent = SelectEvent(Random.value);
        if (activeEvent == null)
        {
            Debug.LogWarning("[MainGame Event] 실행 가능한 이벤트가 없어 다음 주기에 다시 확인합니다.", this);
            ScheduleNextEvent();
            return false;
        }
        state = DirectorState.Running;
        secondsUntilNextEvent = 0f;
        if (!activeEvent.TryBegin())
        {
            activeEvent = null;
            ScheduleNextEvent();
            return false;
        }
        return true;
    }

    // 누적 가중치 구간으로 선택합니다. double 합계로 큰 정수들의 덧셈 오버플로를 피하고, 0 가중치는 제외합니다.
    private MainGameEventBase SelectEvent(float normalizedRoll)
    {
        candidates.Clear();
        double totalWeight = 0d;
        foreach (var entry in eventEntries)
        {
            if (entry == null || entry.Weight <= 0) continue;
            var behaviour = entry.Event;
            if (behaviour == null || !behaviour.isActiveAndEnabled || behaviour.IsRunning || !behaviour.CanRun) continue;
            candidates.Add(entry);
            totalWeight += entry.Weight;
        }
        if (candidates.Count == 0) return null;

        double roll = Mathf.Clamp01(normalizedRoll) * totalWeight;
        double cumulativeWeight = 0d;
        foreach (var entry in candidates)
        {
            cumulativeWeight += entry.Weight;
            if (roll < cumulativeWeight) return entry.Event;
        }
        // Random.value의 상한 1도 포함될 수 있으므로 마지막 유효 항목으로 마무리합니다.
        return candidates[candidates.Count - 1].Event;
    }

    // 기존 씬/프리팹은 기본 가중치 1로 한 번만 이전합니다. 이미 설정된 가중치와 새 목록은 덮어쓰지 않습니다.
    private void UpgradeLegacyEvents()
    {
        foreach (var legacyEvent in events)
        {
            if (legacyEvent == null || eventEntries.Exists(entry => entry != null && entry.Event == legacyEvent)) continue;
            eventEntries.Add(new EventEntry(legacyEvent, 1));
        }
        events.Clear();
    }

    // 즉시 실행은 현재 연출을 먼저 취소합니다. 소리·엄마·문 정리는 각 이벤트의 Cancel 계약을 통해 수행합니다.
    public bool TryRunNextEventImmediately()
    {
        if (!isActiveAndEnabled || miniGameManager == null || miniGameManager.IsGameOver || miniGameManager.IsGameFinished)
            return false;
        if (activeEvent != null) activeEvent.Cancel();
        activeEvent = null;
        return TryStartRandomEvent();
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

    // Inspector의 역전된 범위를 보정합니다. 이미 진행 중인 대기 시간은 즉시 실행 버튼으로 건너뛸 수 있습니다.
    private void OnValidate()
    {
        UpgradeLegacyEvents();
        intervalSeconds.x = Mathf.Max(0f, intervalSeconds.x);
        intervalSeconds.y = Mathf.Max(intervalSeconds.x, intervalSeconds.y);
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
