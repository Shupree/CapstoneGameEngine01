using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.FlappyBird
{
    // 정적 이미지의 위치만 게임 모델에 맞춥니다. Animator나 연출용 회전·트윈은 사용하지 않습니다.
    [DefaultExecutionOrder(100)]
    public sealed class FlappyView : MonoBehaviour
    {
        [SerializeField] private FlappyGame game;
        [SerializeField] private Image bird;
        [SerializeField] private Image[] lowerPipes, upperPipes;
        [SerializeField] private Sprite birdSprite;
        [SerializeField] private Sprite pipeSprite;
        [SerializeField] private TMP_Text progress, heading, description, hint;
        [SerializeField] private GameObject overlay;
        [SerializeField] private RectTransform progressFill;
        private bool previousSpace, spaceArmed;
        private int stateFrame;
        private const float Scale = 60f;

        // 씬 제작 시 한 번 연결하며 팀원 에셋은 Inspector의 두 Sprite 슬롯에서 교체합니다.
        public void Configure(FlappyGame controller, Image birdImage, Image[] lower, Image[] upper,
            Sprite sprite, TMP_Text counter, RectTransform fill, GameObject panel,
            TMP_Text title, TMP_Text body, TMP_Text prompt)
        {
            game = controller; bird = birdImage; lowerPipes = lower; upperPipes = upper;
            birdSprite = sprite; progress = counter; progressFill = fill; overlay = panel;
            heading = title; description = body; hint = prompt;
        }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += StateChanged;
            previousSpace = (MiniGameController.ReadButtons() & GameButtons.Space) != 0;
            spaceArmed = false;
            Refresh();
        }
        private void OnDisable() { if (game != null) game.StateChanged -= StateChanged; spaceArmed = false; }
        private void StateChanged(MiniGameState state) { stateFrame = Time.frameCount; spaceArmed = false; }
        private void Update()
        {
            ProcessStartInput((MiniGameController.ReadButtons() & GameButtons.Space) != 0, Time.frameCount == stateFrame);
            Refresh();
        }

        // 실패를 일으킨 Space가 재시도로 이어지지 않도록 결과 이후 키 해제를 요구합니다.
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
            if (game == null || bird == null) return;
            var model = game.Model;
            if (model != null)
            {
                bird.sprite = birdSprite;
                bird.color = birdSprite == null ? new Color32(255, 215, 75, 255) : Color.white;
                bird.rectTransform.anchoredPosition = new Vector2(FlappyRules.BirdX, model.BirdY) * Scale;
                for (int i = 0; i < model.Pipes.Count; i++)
                {
                    var pipe = model.Pipes[i];
                    float bottom = pipe.GapY - model.Rules.gapHeight / 2f;
                    float top = pipe.GapY + model.Rules.gapHeight / 2f;
                    PlacePipe(lowerPipes[i], pipe.X, (-4.5f + bottom) / 2f, bottom + 4.5f, model.Rules.pipeWidth);
                    PlacePipe(upperPipes[i], pipe.X, (4.5f + top) / 2f, 4.5f - top, model.Rules.pipeWidth);
                }
            }
            progress.text = $"{game.Progress:00} / {game.Target:00}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1f);
            var state = game.State;
            // 외부 정지 UI가 있는 연결에서는 로컬 정지 패널만 숨깁니다. 공통 UI는 수정하지 않습니다.
            overlay.SetActive(state != MiniGameState.Playing && state != MiniGameState.Stopped &&
                !(state == MiniGameState.Paused && game.ExternalPauseControl));
            if (state == MiniGameState.Ready)
            { heading.text = "플래피버드"; description.text = game.StartInstructions; hint.text = "SPACE  시작"; }
            else if (state == MiniGameState.Paused)
            { heading.text = "일시정지"; description.text = "위치와 통과 횟수는 유지됩니다."; hint.text = "TAB  계속하기"; }
            else if (state == MiniGameState.Failed || state == MiniGameState.Succeeded)
            {
                heading.text = state == MiniGameState.Succeeded ? "성공!" : "다시 도전!";
                description.text = $"{game.ResultReason}\n파이프 {game.Progress} / {game.Target}";
                hint.text = "SPACE를 놓고 다시 눌러 재시도";
            }
        }

        private void PlacePipe(Image image, float x, float y, float height, float width)
        {
            image.sprite = pipeSprite;
            image.color = pipeSprite == null ? new Color32(54, 149, 118, 255) : Color.white;
            image.rectTransform.anchoredPosition = new Vector2(x, y) * Scale;
            image.rectTransform.sizeDelta = new Vector2(width, height) * Scale;
        }
    }
}
