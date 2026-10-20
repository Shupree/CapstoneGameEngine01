using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Sorting
{
    // FlappyView와 같은 씬 전용 표시/시작 입력 계층입니다. 결과와 메인 진행 처리는 UI에 의존하지 않습니다.
    [DefaultExecutionOrder(100)]
    public sealed class SortingView : MonoBehaviour
    {
        [SerializeField] private SortingGame game;
        [SerializeField] private Image currentItem;
        [SerializeField] private Sprite[] itemSprites;
        [SerializeField] private GameObject[] stackCards;
        [SerializeField] private TMP_Text progress, remaining, itemName, heading, description, hint;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private GameObject overlay;
        private bool previousSpace, spaceArmed;
        private int stateFrame;
        private static readonly string[] ItemNames = { "상자", "병", "돌", "잎", "볼트" };

        // 참조는 저장된 씬에 직렬화합니다. 런타임에 다른 씬이나 에셋을 찾아 수정하지 않습니다.
        public void Configure(SortingGame controller, Image item, Sprite[] sprites, GameObject[] cards,
            TMP_Text count, TMP_Text left, TMP_Text name, RectTransform fill, GameObject panel,
            TMP_Text title, TMP_Text body, TMP_Text prompt)
        {
            game = controller; currentItem = item; itemSprites = sprites; stackCards = cards;
            progress = count; remaining = left; itemName = name; progressFill = fill;
            overlay = panel; heading = title; description = body; hint = prompt;
        }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += OnStateChanged;
            previousSpace = (MiniGameController.ReadButtons() & GameButtons.Space) != 0;
            spaceArmed = false;
            Refresh();
        }
        private void OnDisable()
        {
            if (game != null) game.StateChanged -= OnStateChanged;
            spaceArmed = false;
        }
        private void OnStateChanged(MiniGameState state) { stateFrame = Time.frameCount; spaceArmed = false; }
        private void Update()
        {
            ProcessStartInput((MiniGameController.ReadButtons() & GameButtons.Space) != 0, Time.frameCount == stateFrame);
            Refresh();
        }

        // 결과를 발생시킨 Space를 놓아야 재시도할 수 있습니다. 시작 Space는 첫 물건 분류에 재사용하지 않습니다.
        internal void ProcessStartInput(bool held, bool transitionFrame = false)
        {
            bool pressed = held && !previousSpace;
            previousSpace = held;
            if (game == null || !game.KeyboardInputEnabled) return;
            bool waiting = game.State == MiniGameState.Ready || game.State == MiniGameState.Failed || game.State == MiniGameState.Succeeded;
            if (!waiting) { spaceArmed = false; return; }
            if (!held) spaceArmed = true;
            if (!transitionFrame && spaceArmed && pressed) { spaceArmed = false; game.StartGame(); }
        }

        public void Refresh()
        {
            if (game == null || currentItem == null) return;
            var model = game.Model;
            bool hasItem = model != null && model.Remaining > 0;
            currentItem.enabled = hasItem;
            itemName.text = hasItem ? ItemNames[(int)model.Current] : "완료";
            if (hasItem && itemSprites != null && itemSprites.Length == 5)
                currentItem.sprite = itemSprites[(int)model.Current];
            for (int i = 0; i < stackCards.Length; i++)
                stackCards[i].SetActive(model != null && model.Remaining > i + 1);
            progress.text = $"정답  {game.Progress:00} / {game.Target:00}";
            remaining.text = $"남은 수  {(model?.Remaining ?? 0):00}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
            var state = game.State;
            // MainGame의 정지 오버레이가 있을 때만 로컬 정지 화면을 숨깁니다. 시작/결과 화면은 유지합니다.
            overlay.SetActive(state != MiniGameState.Playing && state != MiniGameState.Stopped &&
                !(state == MiniGameState.Paused && game.ExternalPauseControl));
            if (state == MiniGameState.Ready)
            { heading.text = "좌우 분류"; description.text = game.StartInstructions; hint.text = "SPACE  시작"; }
            else if (state == MiniGameState.Paused)
            { heading.text = "일시정지"; description.text = "정답 수와 현재 오브젝트는 유지됩니다."; hint.text = "Tab으로 재개"; }
            else if (state == MiniGameState.Failed || state == MiniGameState.Succeeded)
            {
                heading.text = state == MiniGameState.Succeeded ? "성공!" : "다시 도전!";
                description.text = $"{game.ResultReason}\n정답 {game.Progress} / {game.Target}";
                hint.text = "SPACE를 놓고 다시 눌러 재시도";
            }
        }
    }
}
