using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.FlappyBird
{
    // 물고기·해초와 반복 배경을 게임 모델의 이동에 맞춥니다. 판정과 시간 진행은 FlappyGame이 담당합니다.
    [DefaultExecutionOrder(100)]
    public sealed class FlappyView : MonoBehaviour
    {
        [SerializeField] private FlappyGame game;
        [SerializeField] private Image bird;
        [SerializeField] private Image[] lowerPipes, upperPipes;
        [SerializeField] private Sprite birdSprite;
        [SerializeField] private Sprite pipeSprite;
        // MiniGame02 씬에 연결된 원본을 사용하며 다른 씬이나 공통 에셋을 런타임에 수정하지 않습니다.
        [SerializeField] private Sprite[] seaweedSprites;
        [SerializeField] private RawImage flowingBackground;
        [SerializeField, Range(0f, 1f)] private float backgroundParallax = 0.35f;
        [SerializeField] private TMP_Text progress, heading, description, hint;
        [SerializeField] private GameObject overlay;
        [SerializeField] private RectTransform progressFill;
        private bool previousSpace, spaceArmed;
        private int stateFrame;
        private const float Scale = 60f;

        // 기본 참조는 씬 제작 시 연결하고, 수중 스프라이트와 반복 배경은 씬 Inspector에 저장합니다.
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
                RefreshBackground(model);
                bird.sprite = birdSprite;
                bird.color = birdSprite == null ? new Color32(255, 215, 75, 255) : Color.white;
                bird.rectTransform.anchoredPosition = new Vector2(FlappyRules.BirdX, model.BirdY) * Scale;
                for (int i = 0; i < model.Pipes.Count; i++)
                {
                    var pipe = model.Pipes[i];
                    float bottom = pipe.GapY - model.Rules.gapHeight / 2f;
                    float top = pipe.GapY + model.Rules.gapHeight / 2f;
                    PlacePipe(lowerPipes[i], pipe.X, (-4.5f + bottom) / 2f, bottom + 4.5f, model.Rules.pipeWidth, false);
                    PlacePipe(upperPipes[i], pipe.X, (4.5f + top) / 2f, 4.5f - top, model.Rules.pipeWidth, true);
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

        // 별도 타이머 대신 실제 장애물 이동 거리를 사용합니다. 정지·숨김·결과 화면에서는
        // 모델이 멈추므로 배경도 고정되고, 새 모델로 재시작하면 스크롤이 처음 위치로 돌아옵니다.
        private void RefreshBackground(FlappyModel model)
        {
            if (flowingBackground == null || flowingBackground.texture == null || model.Pipes.Count == 0) return;
            Rect viewport = flowingBackground.rectTransform.rect;
            if (viewport.width <= 0f || viewport.height <= 0f) return;
            Texture texture = flowingBackground.texture;
            float tileWidth = viewport.height * texture.width / texture.height;
            float distance = (model.Rules.firstPipeX - model.Pipes[0].X) * Scale * backgroundParallax;
            // U 좌표를 증가시키면 무늬가 왼쪽으로 흐릅니다. 원본 비율과 Repeat 샘플링을
            // 유지하여 화면 크기 변경과 반복 경계에서도 빈틈 없이 연결합니다.
            flowingBackground.uvRect = new Rect(Mathf.Repeat(distance / tileWidth, 1f), 0f,
                viewport.width / tileWidth, 1f);
        }

        // 판정 사각형은 그대로 유지하되 가장 비슷한 종횡비의 해초를 골라 늘어남을 줄입니다.
        private Sprite SeaweedSprite(float height, float width)
        {
            Sprite selected = pipeSprite;
            float bestDifference = float.PositiveInfinity;
            if (seaweedSprites == null) return selected;
            foreach (Sprite sprite in seaweedSprites)
            {
                if (sprite == null) continue;
                float difference = Mathf.Abs(sprite.rect.height / sprite.rect.width - height / width);
                if (difference >= bestDifference) continue;
                selected = sprite;
                bestDifference = difference;
            }
            return selected;
        }

        private void PlacePipe(Image image, float x, float y, float height, float width, bool upper)
        {
            image.sprite = SeaweedSprite(height, width);
            image.color = image.sprite == null ? new Color32(54, 149, 118, 255) : Color.white;
            image.rectTransform.anchoredPosition = new Vector2(x, y) * Scale;
            image.rectTransform.sizeDelta = new Vector2(width, height) * Scale;
            // 위쪽 해초의 뿌리는 천장에, 잎 끝은 통과 공간을 향하게 합니다.
            image.rectTransform.localScale = new Vector3(1f, upper ? -1f : 1f, 1f);
        }
    }
}
