using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Dance
{
    // 화살표·진행·타이머만 표시합니다. 애니메이션과 별도 타이머 없이 공통 상태 화면 아래에서 갱신합니다.
    [DefaultExecutionOrder(100)]
    public sealed class DanceView : MonoBehaviour
    {
        [SerializeField] private DanceGame game;
        [SerializeField] private Image[] cards;
        [SerializeField] private RectTransform[] arrows;
        [SerializeField] private TMP_Text[] keys;
        [SerializeField] private TMP_Text round, timer, status;
        [SerializeField] private Image timerFill, character;
        [SerializeField] private RectTransform progressFill;
        private static readonly string[] KeyLabels = { "W", "A", "S", "D" };
        private static readonly float[] Angles = { 0, 90, 180, -90 };
        private static readonly Color Ink = new Color32(51, 65, 85, 255);
        private static readonly Color Teal = new Color32(15, 118, 110, 255);

        // 각 표시 요소를 씬 참조로 연결해 팀원이 스프라이트와 글꼴을 교체할 수 있게 합니다.
        public void Configure(DanceGame controller, Image[] slots, RectTransform[] directions, TMP_Text[] letters,
            TMP_Text roundLabel, TMP_Text clock, TMP_Text hint, Image timeBar, Image actor, RectTransform fill)
        {
            game = controller; cards = slots; arrows = directions; keys = letters; round = roundLabel;
            timer = clock; status = hint; timerFill = timeBar; character = actor; progressFill = fill;
        }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            var model = game.Model;
            for (int i = 0; i < cards.Length; i++)
            {
                bool visible = i < model.Sequence.Count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                bool done = i < model.InputIndex;
                cards[i].rectTransform.anchoredPosition = new Vector2((i - (model.Sequence.Count - 1) * .5f) * 64, 55);
                cards[i].color = done ? new Color32(153, 246, 228, 255)
                    : i == model.InputIndex ? new Color32(203, 213, 225, 255) : new Color32(248, 250, 252, 255);
                int direction = (int)model.Sequence[i];
                arrows[i].localEulerAngles = new Vector3(0, 0, Angles[direction]);
                keys[i].text = KeyLabels[direction]; keys[i].color = done ? Teal : Ink;
            }
            round.text = $"ROUND {model.Round} / {DanceModel.Goal}";
            // 표시만 소수 첫째 자리로 올리며 판정은 모델의 원래 시간을 사용합니다.
            timer.text = $"{Math.Ceiling(model.TimeLeft * 10) / 10:0.0} s";
            float ratio = (float)(model.TimeLeft / model.TimeLimit);
            timerFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            timerFill.color = ratio < .3f ? new Color32(252, 165, 165, 255) : new Color32(148, 163, 184, 255);
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
            status.text = model.IsTransitioning ? "다음 라운드 준비" : $"입력 {model.InputIndex} / {model.Sequence.Count}";
            character.color = game.State == MiniGameState.Failed ? new Color32(252, 165, 165, 255)
                : model.IsTransitioning || game.State == MiniGameState.Succeeded ? new Color32(153, 246, 228, 255) : new Color32(100, 116, 139, 255);
        }
    }
}
