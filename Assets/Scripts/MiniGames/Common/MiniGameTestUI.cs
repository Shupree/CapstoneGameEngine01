using TMPro;
using UnityEngine;

namespace GE.MiniGames
{
    /// <summary>Optional standalone controls/presentation. The host subscribes to Completed independently.</summary>
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
        [SerializeField, TextArea] private string instructions = "WASD  방향 전환\n사과 5개를 먹으면 성공!\n벽과 자신의 몸을 피하세요.";
        private bool spaceArmed;
        private bool previousSpace;
        private int changedFrame;

        public void Configure(MiniGameController controller, GameObject panel, TMP_Text heading,
            TMP_Text body, TMP_Text hint, string startInstructions)
        {
            game = controller; overlay = panel; title = heading;
            description = body; prompt = hint; instructions = startInstructions;
        }

        public void SetStandaloneControls(bool enabled) { keyboardStartAndRetry = enabled; spaceArmed = false; }
        public void SetOverlaysVisible(bool visible) { showOverlays = visible; Refresh(); }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += OnStateChanged;
            spaceArmed = false;
            previousSpace = (MiniGameController.ReadButtons() & GameButtons.Space) != 0;
            Refresh();
        }
        private void Start() { Refresh(); }
        private void OnDisable()
        {
            if (game != null) game.StateChanged -= OnStateChanged;
            spaceArmed = false;
        }
        private void OnStateChanged(MiniGameState state)
        {
            changedFrame = Time.frameCount;
            spaceArmed = false;
            Refresh();
        }

        private void Update()
        {
            ProcessSpace((MiniGameController.ReadButtons() & GameButtons.Space) != 0,
                Time.frameCount == changedFrame);
        }

        internal void ProcessSpace(bool held, bool isTransitionFrame = false)
        {
            bool pressed = held && !previousSpace;
            previousSpace = held;
            if (game == null || !keyboardStartAndRetry || !game.KeyboardInputEnabled) return;
            bool waiting = game.State == MiniGameState.Ready || game.State == MiniGameState.Succeeded || game.State == MiniGameState.Failed;
            if (!waiting) { spaceArmed = false; return; }
            if (!held) spaceArmed = true;
            if (isTransitionFrame || !spaceArmed || !pressed) return;
            spaceArmed = false;
            game.StartGame();
        }

        private void Refresh()
        {
            if (game == null || overlay == null) return;
            MiniGameState state = game.State;
            overlay.SetActive(showOverlays && state != MiniGameState.Playing && state != MiniGameState.Stopped);
            if (title == null || description == null || prompt == null) return;
            switch (state)
            {
                case MiniGameState.Ready:
                    title.text = gameTitle;
                    description.text = string.IsNullOrEmpty(game.StartInstructions) ? instructions : game.StartInstructions;
                    prompt.text = keyboardStartAndRetry ? "Spacebar로 시작" : "시작 대기 중";
                    break;
                case MiniGameState.Paused:
                    title.text = "일시정지";
                    description.text = "Tab으로 재개";
                    prompt.text = "현재 위치와 진행도는 유지됩니다";
                    break;
                case MiniGameState.Succeeded:
                case MiniGameState.Failed:
                    title.text = state == MiniGameState.Succeeded ? "성공!" : "다시 도전!";
                    description.text = $"{game.ResultReason}\n사과 {game.Progress} / {game.Target}";
                    prompt.text = keyboardStartAndRetry ? "Spacebar를 놓고 다시 눌러 재시도" : "진행 대기 중";
                    break;
            }
        }
    }
}
