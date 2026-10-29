using UnityEngine;

// 하이어라키에 미리 배치한 텍스트 버튼만 제어합니다. 씬·이벤트의 실제 진행은 각 매니저에 맡깁니다.
[DefaultExecutionOrder(-200)]
public sealed class MainGameDeveloperTools : MonoBehaviour
{
    [SerializeField] private MiniGameManager miniGameManager;
    [SerializeField] private MainGameEventDirector eventDirector;
    [SerializeField] private GameObject toolsPanel;
    [SerializeField] private UnityEngine.UI.Button menuButton;
    [SerializeField] private UnityEngine.UI.Button nextEventButton;
    [SerializeField] private UnityEngine.UI.Button flappyButton;
    [SerializeField] private UnityEngine.UI.Button snakeButton;
    [SerializeField] private UnityEngine.UI.Button dinoButton;
    [SerializeField] private UnityEngine.UI.Button sortingButton;
    [SerializeField] private string flappySceneName = "MiniGame02";
    [SerializeField] private string snakeSceneName = "MiniGame01";
    [SerializeField] private string dinoSceneName = "MiniGame04";
    [SerializeField] private string sortingSceneName = "MiniGame03";

    // 빌드 플래그로 표시와 실행을 함께 제한합니다. 일반 배포 빌드에서는 루트 전체를 숨깁니다.
    public static bool IsAvailableInThisBuild
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    private bool CanUse => IsAvailableInThisBuild && Application.isPlaying && miniGameManager != null &&
        miniGameManager.isActiveAndEnabled && !miniGameManager.IsGameOver && !miniGameManager.IsGameFinished;

    private void Awake()
    {
        if (!IsAvailableInThisBuild)
        {
            gameObject.SetActive(false);
            return;
        }
        if (toolsPanel != null) toolsPanel.SetActive(false);
        RefreshButtons();
    }

    private void Update() => RefreshButtons();

    // 게임오버·전환 중의 연속 클릭을 막습니다. 패널을 열어도 게임 시간이나 엄마 이벤트를 정지하지 않습니다.
    private void RefreshButtons()
    {
        bool usable = CanUse;
        if (menuButton != null) menuButton.interactable = usable;
        if (nextEventButton != null) nextEventButton.interactable = usable && eventDirector != null && eventDirector.isActiveAndEnabled;
        bool canSwitch = usable && !miniGameManager.IsTransitioning;
        if (flappyButton != null) flappyButton.interactable = canSwitch;
        if (snakeButton != null) snakeButton.interactable = canSwitch;
        if (dinoButton != null) dinoButton.interactable = canSwitch;
        if (sortingButton != null) sortingButton.interactable = canSwitch;
        if (!usable && toolsPanel != null) toolsPanel.SetActive(false);
    }

    // 각 메서드는 Inspector에 저장한 Button OnClick에서 호출합니다. 일반 빌드의 수동 호출도 차단합니다.
    public void TogglePanel()
    {
        if (CanUse && toolsPanel != null) toolsPanel.SetActive(!toolsPanel.activeSelf);
    }

    public void RunNextEventImmediately()
    {
        if (CanUse && eventDirector != null) eventDirector.TryRunNextEventImmediately();
        RefreshButtons();
    }

    public void OpenFlappyMiniGame() => OpenMiniGame(flappySceneName);
    public void OpenSnakeMiniGame() => OpenMiniGame(snakeSceneName);
    // 추가 게임도 동일한 전환 경로를 사용해 목숨·진행도·엄마 이벤트와 Space 시작 대기 규칙을 유지합니다.
    public void OpenDinoMiniGame() => OpenMiniGame(dinoSceneName);
    public void OpenSortingMiniGame() => OpenMiniGame(sortingSceneName);

    private void OpenMiniGame(string sceneName)
    {
        if (CanUse) miniGameManager.TryLoadMiniGameForDevelopment(sceneName);
        RefreshButtons();
    }
}
