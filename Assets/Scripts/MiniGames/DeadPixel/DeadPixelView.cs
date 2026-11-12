using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.DeadPixel
{
    // 9칸의 명도 차이와 선택 테두리만 표시합니다. 새 문제의 커서는 항상 중앙으로 돌아갑니다.
    public sealed class DeadPixelView : MonoBehaviour
    {
        [SerializeField] private DeadPixelGame game;
        [SerializeField] private Image[] cells;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private TMP_Text score;
        public void Configure(DeadPixelGame owner, Image[] tileImages, RectTransform selection, TMP_Text label)
        { game = owner; cells = tileImages; cursor = selection; score = label; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            for (int i = 0; i < cells.Length; i++) cells[i].color = i == game.Model.TargetIndex ? game.Model.TargetColor : game.Model.BaseColor;
            cursor.anchoredPosition = new Vector2(-70 + game.Model.CursorX * 70, 60 - game.Model.CursorY * 70);
            score.text = $"찾음 {game.Model.Score} / 15    남은 시간 {game.Model.TimeLeft:F1}초";
        }
    }
}
