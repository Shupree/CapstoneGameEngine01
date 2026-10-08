using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // New Input System 사용
using GE.MiniGames;

public class MiniGameSceneManager : SingletonMono<MiniGameSceneManager>
{
    [Header("Scene Configuration")]
    [Tooltip("18개의 미니게임 씬 이름 목록 (Build Profiles / Settings에 등록 필요)")]
    [SerializeField] private List<string> miniGameSceneNames = new List<string>();

    [Header("Game Progress")]
    [SerializeField] private int totalMiniGamesToClear = 18;
    [SerializeField] private int currentClearedCount = 0;

    private string currentLoadedMiniGame = string.Empty;            // 현재 로드된 미니게임 씬 이름
    private List<string> remainingMiniGames = new List<string>();   // 아직 로드되지 않은 미니게임 씬 이름 목록
    private bool isLoading = false;
    private bool isHidingAndPaused = false;

    // 현재 로드된 미니게임 씬의 루트 오브젝트 리스트
    private List<GameObject> currentMiniGameRootObjects = new List<GameObject>();

    // 현재 클리어한 미니게임 수
    public int CurrentClearedCount => currentClearedCount;

    // 현재 미니게임이 활성화되었는지 여부
    public bool IsMiniGameActive => !string.IsNullOrEmpty(currentLoadedMiniGame);
    public bool IsHidingAndPaused => isHidingAndPaused;

    [Header("MiniGame Camera Viewport Settings")]
    [Tooltip("미니게임 카메라의 위치 및 크기 (X, Y: 위치 / W, H: 크기 비율 0~1)")]
    [SerializeField] private Rect miniGameRect = new Rect(0.15f, 0.15f, 0.7f, 0.7f);

    private Camera currentMiniGameCamera;
    private MiniGameController currentMiniGameController;
    private System.Action<MiniGameResult> currentResultHandler;
    private bool currentClearHandled;

    protected override void Awake()
    {
        // SingletonMono의 Awake()를 호출하여 DontDestroyOnLoad 및 중복 방지 처리
        base.Awake();

        ResetMiniGamePool();
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

        // 미니게임이 진행 중이고 로딩 중이 아닐 때만 Shift 키 감지
        if (IsMiniGameActive && !isLoading)
        {
            HandleShiftHideInput();
        }
    }

    // Shift 키 입력을 감지하여 미니게임 화면 숨김 및 일시정지를 제어
    private void HandleShiftHideInput()
    {
        if (Keyboard.current == null) return;

        bool isShiftPressed = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

        // Shift 키를 누르고 있는 동안 -> 숨기기 & 일시정지
        if (isShiftPressed && !isHidingAndPaused)
        {
            PauseAndHideMiniGame(true);
        }
        // Shift 키에서 손을 뗐을 때 -> 다시 보이기 & 재개
        else if (!isShiftPressed && isHidingAndPaused)
        {
            PauseAndHideMiniGame(false);
        }
    }

    /// 미니게임 씬의 오브젝트들을 숨기거나 보이게 하고 시간을 정지/재개
    private void PauseAndHideMiniGame(bool pauseAndHide)
    {
        isHidingAndPaused = pauseAndHide;

        // 미니게임 씬의 루트 오브젝트 활성화/비활성화
        foreach (var rootObj in currentMiniGameRootObjects)
        {
            if (rootObj != null)
            {
                rootObj.SetActive(!pauseAndHide);
            }
        }

        // 미니게임 내부 타이머 및 물리 정지를 위해 TimeScale 조절 (필요 시)
        Time.timeScale = pauseAndHide ? 0f : 1f;

        if (pauseAndHide)
        {
            Debug.Log("[MiniGame Paused]: Shift 입력 - 미니게임 화면을 숨기고 일시정지했습니다.");
        }
        else
        {
            Debug.Log("[MiniGame Resumed]: Shift 해제 - 미니게임을 다시 시작합니다.");
        }
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
        if (isLoading) return;
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

    // 실제 미니게임 씬 로딩 코루틴
    private IEnumerator RoutineLoadMiniGame(string sceneName)
    {
        isLoading = true;

        if (isHidingAndPaused) PauseAndHideMiniGame(false);
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

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            currentMiniGameRootObjects.AddRange(loadedScene.GetRootGameObjects());

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
        BindMiniGameResult(loadedScene);
    }

    private void BindMiniGameResult(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<MiniGameController>(true);
            if (controller == null) continue;

            currentMiniGameController = controller;
            currentResultHandler = result => HandleMiniGameCompleted(controller, result);
            controller.Completed += currentResultHandler;
            return;
        }
        // 아직 공통 컨트롤러가 없는 기존 씬은 OnMiniGameCleared 직접 호출을 유지합니다.
    }

    private void HandleMiniGameCompleted(MiniGameController source, MiniGameResult result)
    {
        if (source == null || source != currentMiniGameController || isLoading || currentClearHandled) return;
        if (source.gameObject.scene.name != currentLoadedMiniGame) return;
        if (result.GameId != source.GameId || result.RunId != source.RunId) return;
        if (result.Outcome != MiniGameOutcome.Success || source.State != MiniGameState.Succeeded) return;

        OnMiniGameCleared();
    }

    private void UnbindMiniGameResult(bool stopGame)
    {
        MiniGameController controller = currentMiniGameController;
        var handler = currentResultHandler;
        currentMiniGameController = null;
        currentResultHandler = null;
        if (controller == null) return;
        if (handler != null) controller.Completed -= handler;
        if (stopGame) controller.StopGame();
    }

    protected override void OnDestroy()
    {
        UnbindMiniGameResult(false);
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

        if (isHidingAndPaused)
        {
            Time.timeScale = 1f;
            isHidingAndPaused = false;
        }

        string sceneToUnload = currentLoadedMiniGame;
        currentLoadedMiniGame = string.Empty;
        currentMiniGameRootObjects.Clear();

        yield return SceneManager.UnloadSceneAsync(sceneToUnload);

        isLoading = false;
        Debug.Log("[MiniGame Closed]: 미니게임이 완전 종료되었습니다.");
    }

    // 미니게임 클리어 시 호출
    public void OnMiniGameCleared()
    {
        if (isLoading || !IsMiniGameActive || currentClearHandled) return;

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

    private void OnGameEnding()
    {
        Debug.Log("게임 승리! 엄마 몰래 18개의 미니게임을 모두 클리어했습니다.");
    }
}
