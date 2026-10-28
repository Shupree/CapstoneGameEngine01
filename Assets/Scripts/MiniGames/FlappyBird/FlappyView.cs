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
        // 모델 좌표의 Scale과 별개로, 모든 그림의 원본 픽셀을 동일한 UI 배율로 표시합니다.
        private const float SpriteScale = 1.8f;

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
                if (birdSprite != null) bird.rectTransform.sizeDelta = birdSprite.rect.size * SpriteScale;
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
            float tileWidth = texture.width * SpriteScale;
            float tileHeight = texture.height * SpriteScale;
            float distance = (model.Rules.firstPipeX - model.Pipes[0].X) * Scale * backgroundParallax;
            // U 좌표를 증가시키면 무늬가 왼쪽으로 흐릅니다. 화면 폭에 맞춰 늘리는 대신
            // 원본의 1.8배 크기로 반복하므로 물고기·해초와 배경의 도트 크기가 같습니다.
            flowingBackground.uvRect = new Rect(Mathf.Repeat(distance / tileWidth, 1f), 0f,
                viewport.width / tileWidth, viewport.height / tileHeight);
        }

        // 통과 공간에서 화면 경계까지 닿는 가장 짧은 원본을 선택합니다. 초과한 뿌리는
        // Playfield의 기존 마스크 밖으로 보내며, 그림 자체의 가로·세로 배율은 바꾸지 않습니다.
        private Sprite SeaweedSprite(float height)
        {
            Sprite selected = pipeSprite;
            float bestDifference = float.PositiveInfinity;
            if (seaweedSprites == null) return selected;
            foreach (Sprite sprite in seaweedSprites)
            {
                if (sprite == null) continue;
                float difference = sprite.rect.height * SpriteScale - height * Scale;
                // 기본 씬에서는 가장 긴 해초가 모든 높이를 덮습니다. 부족한 후보보다 긴 후보를 우선합니다.
                if (difference < 0f) difference = 10000f - difference;
                if (difference >= bestDifference) continue;
                selected = sprite;
                bestDifference = difference;
            }
            return selected;
        }

        private void PlacePipe(Image image, float x, float y, float height, float width, bool upper)
        {
            image.sprite = SeaweedSprite(height);
            image.color = image.sprite == null ? new Color32(54, 149, 118, 255) : Color.white;
            Vector2 size = image.sprite != null ? image.sprite.rect.size * SpriteScale : new Vector2(width, height) * Scale;
            float tip = (y + (upper ? -height : height) * 0.5f) * Scale;
            image.rectTransform.anchoredPosition = new Vector2(x * Scale, tip + (upper ? size.y : -size.y) * 0.5f);
            image.rectTransform.sizeDelta = size;
            // 위쪽 해초의 뿌리는 천장에, 잎 끝은 통과 공간을 향하게 합니다.
            image.rectTransform.localScale = new Vector3(1f, upper ? -1f : 1f, 1f);
        }

        // FlappyGame이 모델을 만든 직후 호출합니다. 확대된 물고기와 해초의 표시 크기를
        // 게임 단위로 넘겨, 보이는 그림을 통과하거나 점수를 너무 일찍 얻지 않도록 합니다.
        internal void ConfigureVisualBounds(FlappyModel model)
        {
            if (birdSprite != null) model.BirdHalfSize = birdSprite.rect.size * (SpriteScale / Scale * 0.5f);
            foreach (var pipe in model.Pipes)
            {
                float bottomHeight = FlappyRules.Height * 0.5f + pipe.GapY - model.Rules.gapHeight * 0.5f;
                float topHeight = FlappyRules.Height * 0.5f - pipe.GapY - model.Rules.gapHeight * 0.5f;
                Sprite lower = SeaweedSprite(bottomHeight), upper = SeaweedSprite(topHeight);
                if (lower != null) pipe.LowerWidth = lower.rect.width * SpriteScale / Scale;
                if (upper != null) pipe.UpperWidth = upper.rect.width * SpriteScale / Scale;
            }
        }
    }
}
