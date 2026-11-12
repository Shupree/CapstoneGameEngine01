using TMPro;
using UnityEngine;

namespace GE.MiniGames
{
    /// <summary>테스트용 시작·결과 화면과 재시도 입력입니다. 메인의 결과 구독은 이 UI와 독립적입니다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class MiniGameTestUI : MonoBehaviour
    {
        [SerializeField] private MiniGameController game;
        [SerializeField] private GameObject overlay;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private bool keyboardStartAndRetry = true;
        [SerializeField] private bool showOverlays = true;
        [SerializeField] private string gameTitle = "스네이크";
        // 기존 스네이크의 기본 표기를 유지하면서 거리형 게임도 같은 결과 화면을 사용합니다.
        [SerializeField] private string progressLabel = "사과";
        [SerializeField, TextArea] private string instructions = "WASD  방향 전환\n사과 5개를 먹으면 성공!\n벽과 자신의 몸을 피하세요.";
        private bool startKeyArmed;
        private bool previousStartKey;
        private int changedFrame;
        private bool useExternalPauseOverlay;

        public void Configure(MiniGameController controller, GameObject panel, TMP_Text heading,
            TMP_Text body, TMP_Text hint, string startInstructions)
        {
            game = controller; overlay = panel; title = heading;
            description = body; prompt = hint; instructions = startInstructions;
        }

        public void SetStandaloneControls(bool enabled) { keyboardStartAndRetry = enabled; startKeyArmed = false; }
        public void SetOverlaysVisible(bool visible) { showOverlays = visible; Refresh(); }

        // MainGame의 검은 정지 UI와 중복되지 않도록 Paused 화면만 숨깁니다. 시작·실패 화면은 유지합니다.
        public void SetExternalPauseOverlay(bool enabled) { useExternalPauseOverlay = enabled; Refresh(); }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += OnStateChanged;
            startKeyArmed = false;
            previousStartKey = MiniGameController.IsStartKeyHeld;
            Refresh();
        }
        private void Start() { Refresh(); }
        private void OnDisable()
        {
            if (game != null) game.StateChanged -= OnStateChanged;
            startKeyArmed = false;
        }
        private void OnStateChanged(MiniGameState state)
        {
            changedFrame = Time.frameCount;
            startKeyArmed = false;
            Refresh();
        }

        private void Update()
        {
            ProcessStartInput(MiniGameController.IsStartKeyHeld,
                Time.frameCount == changedFrame);
        }

        // 씬 전환이나 결과 표시 전에 누른 R은 놓은 뒤 다시 눌러야 시작 입력으로 처리됩니다.
        internal void ProcessStartInput(bool held, bool isTransitionFrame = false)
        {
            bool pressed = held && !previousStartKey;
            previousStartKey = held;
            if (game == null || !keyboardStartAndRetry || !game.KeyboardInputEnabled) return;
            bool waiting = game.State == MiniGameState.Ready || game.State == MiniGameState.Succeeded || game.State == MiniGameState.Failed;
            if (!waiting) { startKeyArmed = false; return; }
            if (!held) startKeyArmed = true;
            if (isTransitionFrame || !startKeyArmed || !pressed) return;
            startKeyArmed = false;
            // 재시도는 같은 게임을 초기화합니다. 다음 씬 선택은 메인의 성공 결과 처리에서만 수행합니다.
            game.StartGame();
        }

        private void Refresh()
        {
            if (game == null || overlay == null) return;
            MiniGameState state = game.State;
            overlay.SetActive(showOverlays && state != MiniGameState.Playing && state != MiniGameState.Stopped
                && !(useExternalPauseOverlay && state == MiniGameState.Paused));
            if (title == null || description == null || prompt == null) return;
            switch (state)
            {
                case MiniGameState.Ready:
                    title.text = gameTitle;
                    description.text = string.IsNullOrEmpty(game.StartInstructions) ? instructions : game.StartInstructions;
                    prompt.text = keyboardStartAndRetry ? "R로 시작" : "시작 대기 중";
                    break;
                case MiniGameState.Paused:
                    title.text = "일시정지";
                    description.text = "Tab으로 재개";
                    prompt.text = "현재 위치와 진행도는 유지됩니다";
                    break;
                case MiniGameState.Succeeded:
                case MiniGameState.Failed:
                    title.text = state == MiniGameState.Succeeded ? "성공!" : "다시 도전!";
                    description.text = $"{game.ResultReason}\n{progressLabel} {game.Progress} / {game.Target}";
                    prompt.text = keyboardStartAndRetry ? "R로 재시도" : "진행 대기 중";
                    break;
            }
        }
    }
}
