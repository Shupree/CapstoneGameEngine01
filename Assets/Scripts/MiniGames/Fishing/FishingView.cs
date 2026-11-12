using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Fishing
{
    // 포획 영역·물고기·진행 게이지를 모델과 일치시킵니다. 위치는 HTML의 아래쪽 양수를 변환합니다.
    public sealed class FishingView : MonoBehaviour
    {
        [SerializeField] private FishingGame game;
        [SerializeField] private Image capture;
        [SerializeField] private RectTransform fish, progress;
        [SerializeField] private TMP_Text timer, percent;
        public void Configure(FishingGame owner, Image area, RectTransform fishImage, RectTransform fill, TMP_Text timeLabel, TMP_Text progressLabel)
        { game = owner; capture = area; fish = fishImage; progress = fill; timer = timeLabel; percent = progressLabel; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            capture.rectTransform.anchoredPosition = new Vector2(0, 150 - (float)game.Model.CaptureY);
            capture.color = game.Model.Captured ? new Color32(46, 204, 113, 204) : new Color32(149, 165, 166, 204);
            fish.anchoredPosition = new Vector2(0, 150 - (float)game.Model.FishY);
            progress.anchorMax = new Vector2((float)game.Model.Progress / 100, 1);
            timer.text = $"남은 시간 {game.Model.TimeLeft:F1}초"; percent.text = $"포획 {game.Progress}%";
        }
    }
}
