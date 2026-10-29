using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // New Input System 사용
using GE.MiniGames;

// 미니게임의 Update보다 먼저 입력을 처리해, 정지 프레임에 게임이 한 틱 더 진행되지 않게 합니다.
[DefaultExecutionOrder(-100)]
public class MiniGameManager : SingletonMono<MiniGameManager>
{
    [Header("Scene Configuration")]
    [Tooltip("18개의 미니게임 씬 이름 목록 (Build Profiles / Settings에 등록 필요)")]
    [SerializeField] private List<string> miniGameSceneNames = new List<string>();

    [Header("Game Progress")]
    [SerializeField] private int totalMiniGamesToClear = 18;
    [SerializeField] private int currentClearedCount = 0;

    [Header("MainGame Lives")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField] private int currentLives = 3;

    [Header("MainGame Game Over UI")]
    [Tooltip("MainGame 하이어라키에 배치한 전체 화면 게임오버 Image입니다.")]
    [SerializeField] private GameObject gameOverOverlay;
    [SerializeField] private UnityEngine.UI.Button gameOverRetryButton;
    private bool isGameOver;
    private bool isRestarting;
    private float timeScaleBeforeGameOver = 1f;
    private EGameOverReason gameOverReason = EGameOverReason.None;
    private int lastFailedRunId = -1;

    private string currentLoadedMiniGame = string.Empty;            // 현재 로드된 미니게임 씬 이름
    private List<string> remainingMiniGames = new List<string>();   // 아직 로드되지 않은 미니게임 씬 이름 목록
    private bool isLoading = false;
    private bool isMiniGameHidden;
    private string loadingMiniGameScene = string.Empty;
    // 루트를 끄면 로직도 멈추므로, 그리기 컴포넌트만 원래 활성 상태와 함께 보관합니다.
    private readonly Dictionary<Camera, bool> cameraVisibility = new Dictionary<Camera, bool>();
    private readonly Dictionary<Canvas, bool> canvasVisibility = new Dictionary<Canvas, bool>();

    [Header("MiniGame Pause UI")]
    [Tooltip("MainGame 하이어라키에 배치한 일시정지 Image의 RectTransform입니다. 자식 Text도 함께 표시됩니다.")]
    [SerializeField] private RectTransform miniGamePauseOverlay;
    private bool isMiniGamePaused;
    private MiniGameTestUI currentMiniGameTestUI;

    // 현재 로드된 미니게임 씬의 루트 오브젝트 리스트
    private List<GameObject> currentMiniGameRootObjects = new List<GameObject>();

    // 현재 클리어한 미니게임 수
    public int CurrentClearedCount => currentClearedCount;
    public int CurrentLives => currentLives;
    public bool IsGameOver => isGameOver;
    public bool IsGameFinished { get; private set; }
    public EGameOverReason GameOverReason => gameOverReason;

    // 목숨 표시나 엄마 시스템은 이 이벤트를 구독할 수 있습니다. UI 오브젝트를 코드로 만들지는 않습니다.
    public event System.Action<int> LivesChanged;
    public event System.Action<EGameOverReason> GameOverTriggered;
    // 엔딩 화면은 기존 개발 영역에 두고, 목표 달성 시 MainGame 이벤트의 중단만 알립니다.
    public event System.Action GameFinished;

    // 현재 미니게임이 활성화되었는지 여부
    public bool IsMiniGameActive => !string.IsNullOrEmpty(currentLoadedMiniGame);
    public bool IsMiniGameHidden => isMiniGameHidden;
    public bool IsMiniGamePaused => isMiniGamePaused;
    // 개발자 UI는 전환 중 버튼을 잠그고 기존 비동기 씬 작업이 끝난 뒤 다시 요청합니다.
    public bool IsTransitioning => isLoading || isRestarting;

    [Header("MiniGame Camera Viewport Settings")]
    [Tooltip("미니게임 카메라의 위치 및 크기 (X, Y: 위치 / W, H: 크기 비율 0~1)")]
    [SerializeField] private Rect miniGameRect = new Rect(0.15f, 0.15f, 0.7f, 0.7f);

    private Camera currentMiniGameCamera;
    // 추가 연결부: 현재 씬의 결과 구독만 보관하며, 클리어 집계는 기존 OnMiniGameCleared가 담당합니다.
    private MiniGameController currentMiniGameController;
    private System.Action<MiniGameResult> currentResultHandler;
    private bool currentClearHandled;

    protected override void Awake()
    {
        // SingletonMono의 Awake()를 호출하여 DontDestroyOnLoad 및 중복 방지 처리
        base.Awake();
        if (Singleton != this) return;
        SceneManager.sceneLoaded += HandleMiniGameSceneLoaded;

        // MainGame을 새로 생성할 때마다 진행도와 목숨을 초기화합니다. 미니게임 로컬 재시도에는 적용하지 않습니다.
        currentClearedCount = 0;
        currentLives = Mathf.Max(1, startingLives);
        ResetMiniGamePool();
        RefreshPauseOverlay();
        if (gameOverOverlay != null) gameOverOverlay.SetActive(false);
    }

    private void Start()
    {
        // Lobby 씬에서 MainGame 씬으로 진입했을 때 자동으로 첫 번째 미니게임 로드
        LoadRandomMiniGame();
    }

    private void Update()
    {
        // 미니게임이 동작 중일 때, Inspector에서 바꾼 Viewport Rect 수치를 실시간 반영 (디버깅용 코드)
        if (IsMiniGameActive && currentMiniGameCamera != null)
        {
            currentMiniGameCamera.rect = miniGameRect;
        }

        // 숨김을 먼저 처리합니다. Shift로 게임기를 닫은 동안에는 Tab 전환을 받지 않습니다.
        if (!isGameOver && !isRestarting && !IsGameFinished)
        {
            HandleShiftHideInput();
            if (IsMiniGameActive && !isLoading) HandleTabPauseInput();
        }

        // Inspector에서 바꾼 카메라 영역과 하이어라키의 정지 UI 영역을 함께 맞춥니다.
        RefreshPauseOverlay();
    }

    // Tab의 눌림 순간만 처리합니다. 키를 계속 누르고 있어도 정지/재개가 반복되지 않습니다.
    private void HandleTabPauseInput()
    {
        if (Keyboard.current == null || isMiniGameHidden) return;
        if (!Keyboard.current.tabKey.wasPressedThisFrame) return;
        if (currentMiniGameController == null) return;

        // 시작 대기와 결과 화면은 그대로 두고, 실제 플레이 중인 미니게임만 전환합니다.
        if (currentMiniGameController.State == MiniGameState.Playing)
            currentMiniGameController.PauseGame();
        else if (currentMiniGameController.State == MiniGameState.Paused)
            currentMiniGameController.ResumeGame();
    }

    // 컨트롤러의 실제 상태를 기준으로 UI를 갱신합니다. 포커스 상실로 정지해도 표시가 일치합니다.
    private void HandleMiniGameStateChanged(MiniGameState state)
    {
        isMiniGamePaused = state == MiniGameState.Paused;
        RefreshPauseOverlay();
    }

    // UI는 씬에 미리 배치하고 여기서는 표시와 영역만 제어합니다. Tab은 전역 시간을 멈추지 않습니다.
    private void RefreshPauseOverlay()
    {
        if (miniGamePauseOverlay == null) return;

        miniGamePauseOverlay.anchorMin = miniGameRect.position;
        miniGamePauseOverlay.anchorMax = miniGameRect.position + miniGameRect.size;
        miniGamePauseOverlay.offsetMin = Vector2.zero;
        miniGamePauseOverlay.offsetMax = Vector2.zero;

        bool visible = IsMiniGameActive && !isLoading && isMiniGamePaused && !isMiniGameHidden && !isGameOver;
        if (miniGamePauseOverlay.gameObject.activeSelf != visible)
            miniGamePauseOverlay.gameObject.SetActive(visible);
    }

    // Shift는 누르고 있는 동안 화면만 숨깁니다. 정지가 필요하면 숨기기 전에 Tab을 눌러야 합니다.
    private void HandleShiftHideInput()
    {
        bool held = Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        if (held != isMiniGameHidden) SetMiniGameHidden(held);
    }

    // 시간·컨트롤러 상태·루트 활성 상태는 변경하지 않습니다. 원래 꺼져 있던 카메라/Canvas도 보존합니다.
    private void SetMiniGameHidden(bool hidden)
    {
        isMiniGameHidden = hidden;
        if (hidden)
        {
            CaptureAndHidePresentation(SceneManager.GetSceneByName(currentLoadedMiniGame));
            CaptureAndHidePresentation(SceneManager.GetSceneByName(loadingMiniGameScene));
        }
        else RestorePresentation();
        RefreshPauseOverlay();
    }

    private void LateUpdate()
    {
        if (!isMiniGameHidden) return;
        // 게임 실행 중 새 Canvas가 생성되거나 로컬 UI가 갱신되어도 렌더링 직전까지 숨김을 유지합니다.
        CaptureAndHidePresentation(SceneManager.GetSceneByName(currentLoadedMiniGame));
        CaptureAndHidePresentation(SceneManager.GetSceneByName(loadingMiniGameScene));
    }

    private void HandleMiniGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive 로드 완료 콜백은 첫 렌더링보다 먼저 옵니다. Shift 중 씬이 바뀌어도 화면이 깜빡이지 않습니다.
        if (isMiniGameHidden && scene.name == loadingMiniGameScene) CaptureAndHidePresentation(scene);
    }

    private void CaptureAndHidePresentation(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (!cameraVisibility.ContainsKey(camera)) cameraVisibility.Add(camera, camera.enabled);
                camera.enabled = false;
            }
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvasVisibility.ContainsKey(canvas)) canvasVisibility.Add(canvas, canvas.enabled);
                canvas.enabled = false;
            }
        }
    }

    private void RestorePresentation()
    {
        foreach (var item in cameraVisibility) if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in canvasVisibility) if (item.Key != null) item.Key.enabled = item.Value;
        cameraVisibility.Clear();
        canvasVisibility.Clear();
    }

    private void ResetMiniGamePool()
    {
        remainingMiniGames.Clear();
        foreach (string sceneName in miniGameSceneNames)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && !remainingMiniGames.Contains(sceneName))
                remainingMiniGames.Add(sceneName);
        }
    }

    // 랜덤 미니게임 씬을 Additive 모드로 로드
    public void LoadRandomMiniGame()
    {
        if (isLoading || isGameOver || isRestarting || IsGameFinished) return;
        if (currentClearedCount >= totalMiniGamesToClear)
        {
            OnGameEnding();
            return;
        }

        if (remainingMiniGames.Count == 0)
        {
            ResetMiniGamePool();
        }

        if (remainingMiniGames.Count == 0)
        {
            Debug.LogWarning("[MiniGame] 등록된 미니게임 씬이 없습니다.");
            return;
        }

        // 풀이 새로 채워져도 다른 후보가 있으면 방금 끝낸 씬을 연속 선택하지 않습니다.
        var candidateIndices = new List<int>();
        for (int i = 0; i < remainingMiniGames.Count; i++)
        {
            if (remainingMiniGames[i] != currentLoadedMiniGame) candidateIndices.Add(i);
        }
        int randomIndex = candidateIndices.Count > 0
            ? candidateIndices[Random.Range(0, candidateIndices.Count)]
            : 0;
        string selectedScene = remainingMiniGames[randomIndex];
        if (!Application.CanStreamedLevelBeLoaded(selectedScene))
        {
            Debug.LogError($"[MiniGame] Build Profiles에 등록되지 않은 씬입니다: {selectedScene}");
            return;
        }
        remainingMiniGames.RemoveAt(randomIndex);

        StartCoroutine(RoutineLoadMiniGame(selectedScene));
    }

    // 테스트용 지정 씬 교체입니다. 진행도·목숨·엄마 이벤트는 유지하고 새 컨트롤러의 Ready/Space 시작 흐름을 그대로 사용합니다.
    public bool TryLoadMiniGameForDevelopment(string sceneName)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isActiveAndEnabled || isLoading || isGameOver || isRestarting || IsGameFinished) return false;
        if (string.IsNullOrWhiteSpace(sceneName) || !miniGameSceneNames.Contains(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[Developer Tools] 등록되지 않았거나 로드할 수 없는 미니게임입니다: {sceneName}", this);
            return false;
        }
        // 강제 선택도 이번 선택 풀에서 사용한 씬으로 처리하되 전체 풀이나 누적 기록은 초기화하지 않습니다.
        remainingMiniGames.Remove(sceneName);
        StartCoroutine(RoutineLoadMiniGame(sceneName));
        return true;
#else
        // 일반 빌드에서는 UI 외부에서 메서드를 호출해도 테스트용 씬 전환을 수행하지 않습니다.
        return false;
#endif
    }

    // 실제 미니게임 씬 로딩 코루틴
    private IEnumerator RoutineLoadMiniGame(string sceneName)
    {
        isLoading = true;
        // 숨김 입력은 씬 전환 중에도 유지합니다. 이전 씬의 참조만 정리하고 새 씬을 같은 숨김 상태로 받습니다.
        loadingMiniGameScene = sceneName;
        RestorePresentation();
        UnbindMiniGameResult(true);
        currentMiniGameCamera = null;

        if (!string.IsNullOrEmpty(currentLoadedMiniGame))
        {
            yield return SceneManager.UnloadSceneAsync(currentLoadedMiniGame);
            currentMiniGameRootObjects.Clear();
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (!asyncLoad.isDone) yield return null;

        currentLoadedMiniGame = sceneName;
        currentClearHandled = false;
        lastFailedRunId = -1;

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            currentMiniGameRootObjects.AddRange(loadedScene.GetRootGameObjects());

            // Additive 씬의 UI도 MainGame의 EventSystem 하나로 처리합니다. 중복 입력 시스템이 Retry 클릭을 경쟁하지 않게 합니다.
            foreach (GameObject root in currentMiniGameRootObjects)
            {
                foreach (var eventSystem in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                {
                    foreach (var module in eventSystem.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
                        module.enabled = false;
                    eventSystem.enabled = false;
                }
            }

            // 로드된 미니게임 씬의 메인 카메라 찾기
            currentMiniGameCamera = null;
            foreach (var rootObj in loadedScene.GetRootGameObjects())
            {
                Camera miniGameCam = rootObj.GetComponentInChildren<Camera>();
                if (miniGameCam != null)
                {
                    currentMiniGameCamera = miniGameCam;
                    currentMiniGameCamera.rect = miniGameRect;

                    // URP 적용 시 Priority 설정 (구버전 Depth 대체)
                    currentMiniGameCamera.depth = 1;
                    break;
                }
            }
        }

        isLoading = false;
        loadingMiniGameScene = string.Empty;
        BindMiniGameResult(loadedScene);
        if (isMiniGameHidden) CaptureAndHidePresentation(loadedScene);
        // 엄마의 발각이 로딩 중 발생했어도, 뒤늦게 로드된 미니게임을 플레이할 수 없게 합니다.
        if (isGameOver && currentMiniGameController != null) currentMiniGameController.StopGame();
    }

    // 추가 연결부: MainGame이 로드한 씬에서만 결과를 구독합니다. 단독 테스트 씬은 매니저를 만들지 않습니다.
    private void BindMiniGameResult(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<MiniGameController>(true);
            if (controller == null) continue;

            currentMiniGameController = controller;
            // MainGame에서는 매니저가 Tab과 정지 표시를 맡고, 단독 씬의 기존 조작은 유지합니다.
            controller.ExternalPauseControl = true;
            currentMiniGameTestUI = controller.GetComponent<MiniGameTestUI>();
            if (currentMiniGameTestUI != null)
                currentMiniGameTestUI.SetExternalPauseOverlay(true);
            currentResultHandler = result => HandleMiniGameCompleted(controller, result);
            controller.Completed += currentResultHandler;
            controller.StateChanged += HandleMiniGameStateChanged;
            HandleMiniGameStateChanged(controller.State);
            return;
        }
        // 아직 공통 컨트롤러가 없는 기존 씬은 OnMiniGameCleared 직접 호출을 유지합니다.
    }

    // 스네이크를 포함한 공통 컨트롤러의 결과를 메인 진행으로 연결합니다. 실패 재시도는 같은 씬에서 유지합니다.
    private void HandleMiniGameCompleted(MiniGameController source, MiniGameResult result)
    {
        // 이전 씬/재시도의 늦은 결과와 중복 통지로 클리어 수가 잘못 증가하는 것을 차단합니다.
        if (source == null || source != currentMiniGameController || isLoading || currentClearHandled || isGameOver) return;
        if (source.gameObject.scene.name != currentLoadedMiniGame) return;
        if (result.GameId != source.GameId || result.RunId != source.RunId) return;
        if (result.Outcome == MiniGameOutcome.Success && source.State == MiniGameState.Succeeded)
            OnMiniGameCleared();
        else if (result.Outcome == MiniGameOutcome.Failure && source.State == MiniGameState.Failed)
            OnMiniGameFailed();
    }

    // 실패 결과의 공통 진입점입니다. 실행 번호로 중복 차감을 막고, 목숨이 남으면 기존 Space 재시도를 허용합니다.
    public void OnMiniGameFailed()
    {
        if (isGameOver || isRestarting || isLoading || currentMiniGameController == null) return;
        if (currentMiniGameController.State != MiniGameState.Failed || currentClearHandled) return;
        if (lastFailedRunId == currentMiniGameController.RunId) return;

        lastFailedRunId = currentMiniGameController.RunId;
        currentLives = Mathf.Max(0, currentLives - 1);
        Debug.Log($"[MainGame] 미니게임 실패: 남은 목숨 {currentLives}/{startingLives}");
        LivesChanged?.Invoke(currentLives);
        if (currentLives == 0) TriggerGameOver(EGameOverReason.LivesDepleted);
    }

    // 엄마의 발각 등 새 패배 조건도 이 메서드를 호출합니다. 첫 원인만 확정하고 이후 입력과 결과는 차단합니다.
    public void TriggerGameOver(EGameOverReason reason)
    {
        if (reason == EGameOverReason.None || isGameOver || isRestarting || IsGameFinished) return;

        isGameOver = true;
        gameOverReason = reason;
        // 전역 시간 정지는 MainGame 게임오버에서만 수행합니다. Shift와 Tab은 이 값에 관여하지 않습니다.
        timeScaleBeforeGameOver = Time.timeScale;
        if (isMiniGameHidden) SetMiniGameHidden(false);
        if (currentMiniGameController != null) currentMiniGameController.StopGame();
        RefreshPauseOverlay();
        Time.timeScale = 0f;
        if (gameOverRetryButton != null) gameOverRetryButton.interactable = true;
        if (gameOverOverlay != null) gameOverOverlay.SetActive(true);
        Debug.Log($"[MainGame] 게임오버: {reason}");
        GameOverTriggered?.Invoke(reason);
    }

    // 하이어라키의 Retry? Text에 붙은 Button이 호출합니다. 연속 클릭으로 중복 로드되지 않게 막습니다.
    public void RestartMainGame()
    {
        if (!isGameOver || isRestarting) return;
        string sceneName = ESceneType.MainGame.ToString();
        Scene mainScene = SceneManager.GetSceneByName(sceneName);
        if (!mainScene.IsValid() || !mainScene.isLoaded || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[MainGame] 재시작할 MainGame 씬을 찾을 수 없습니다.");
            return;
        }

        isRestarting = true;
        if (gameOverRetryButton != null) gameOverRetryButton.interactable = false;
        StartCoroutine(RoutineRestartMainGame(mainScene));
    }

    // Additive 씬을 먼저 정리한 뒤 MainGame 전체를 다시 로드합니다. 목숨·진행도 초기화는 새 매니저 Awake의 책임입니다.
    private IEnumerator RoutineRestartMainGame(Scene mainScene)
    {
        // 로딩 중 발각된 경우 현재 작업의 완료를 기다려 두 씬 전환이 서로 충돌하지 않게 합니다.
        while (isLoading) yield return null;
        isLoading = true;
        UnbindMiniGameResult(true);
        currentMiniGameCamera = null;
        if (!string.IsNullOrEmpty(currentLoadedMiniGame))
        {
            Scene miniScene = SceneManager.GetSceneByName(currentLoadedMiniGame);
            if (miniScene.IsValid() && miniScene.isLoaded) yield return SceneManager.UnloadSceneAsync(miniScene);
        }
        currentLoadedMiniGame = string.Empty;
        currentMiniGameRootObjects.Clear();
        Time.timeScale = timeScaleBeforeGameOver;

        // DontDestroyOnLoad 매니저도 기존 MainGame에 돌려놓아 함께 파괴되게 합니다. 새 매니저가 중복 싱글톤으로 제거되는 것을 방지합니다.
        transform.SetParent(null);
        if (gameObject.scene != mainScene) SceneManager.MoveGameObjectToScene(gameObject, mainScene);
        yield return SceneManager.LoadSceneAsync(mainScene.name, LoadSceneMode.Single);
    }

    // 씬 언로드 전에 구독을 먼저 해제합니다. StopGame은 로컬 정리만 하고 결과를 다시 보내지 않습니다.
    private void UnbindMiniGameResult(bool stopGame)
    {
        MiniGameController controller = currentMiniGameController;
        var handler = currentResultHandler;
        currentMiniGameController = null;
        currentResultHandler = null;
        isMiniGamePaused = false;
        RefreshPauseOverlay();
        if (currentMiniGameTestUI != null)
            currentMiniGameTestUI.SetExternalPauseOverlay(false);
        currentMiniGameTestUI = null;
        if (controller == null) return;
        if (handler != null) controller.Completed -= handler;
        controller.StateChanged -= HandleMiniGameStateChanged;
        controller.ExternalPauseControl = false;
        if (stopGame) controller.StopGame();
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleMiniGameSceneLoaded;
        RestorePresentation();
        UnbindMiniGameResult(false);
        // 게임오버 상태에서 씬을 외부에서 닫더라도 0배 시간이 다음 화면에 남지 않게 합니다.
        if (isGameOver) Time.timeScale = timeScaleBeforeGameOver;
        base.OnDestroy();
    }

    // 미니게임 완전 종료/언로드 (필요시 사용)
    public void CloseMiniGameImmediately()
    {
        if (isLoading || string.IsNullOrEmpty(currentLoadedMiniGame)) return;

        StartCoroutine(RoutineCloseMiniGame());
    }

    private IEnumerator RoutineCloseMiniGame()
    {
        isLoading = true;
        UnbindMiniGameResult(true);
        currentMiniGameCamera = null;

        SetMiniGameHidden(false);

        string sceneToUnload = currentLoadedMiniGame;
        currentLoadedMiniGame = string.Empty;
        currentMiniGameRootObjects.Clear();

        yield return SceneManager.UnloadSceneAsync(sceneToUnload);

        isLoading = false;
        Debug.Log("[MiniGame Closed]: 미니게임이 완전 종료되었습니다.");
    }

    // 기존 메인 진행 진입점: 클리어 수 증가와 다음 씬/전체 종료 결정은 이 함수에서만 처리합니다.
    public void OnMiniGameCleared()
    {
        if (isLoading || !IsMiniGameActive || currentClearHandled || isGameOver || isRestarting) return;

        currentClearHandled = true;
        currentClearedCount++;
        Debug.Log($"미니게임 클리어! 현재 클리어 수: {currentClearedCount}/{totalMiniGamesToClear}");

        if (currentClearedCount >= totalMiniGamesToClear)
        {
            CloseMiniGameImmediately();
            OnGameEnding();
        }
        else
        {
            LoadRandomMiniGame();
        }
    }

    // 목표 달성은 한 번만 알립니다. 엄마 이벤트를 중단하며, 별도의 엔딩 화면을 여기서 만들지는 않습니다.
    private void OnGameEnding()
    {
        if (IsGameFinished) return;
        IsGameFinished = true;
        GameFinished?.Invoke();
        Debug.Log("게임 승리! 엄마 몰래 18개의 미니게임을 모두 클리어했습니다.");
    }
}
