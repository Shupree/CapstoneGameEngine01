using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Rope
{
    // 모델의 실제 기울기와 공 위치만 표시하며 장식 애니메이션은 사용하지 않습니다.
    public sealed class RopeView : MonoBehaviour
    {
        [SerializeField] private RopeGame game;
        [SerializeField] private RectTransform pivot, ball;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Image warning;
        public void Configure(RopeGame owner, RectTransform boardPivot, RectTransform ballImage, TMP_Text timeLabel, Image border)
        { game = owner; pivot = boardPivot; ball = ballImage; timer = timeLabel; warning = border; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            // HTML의 아래쪽 양수 좌표를 Unity UI의 위쪽 양수 좌표로 변환합니다.
            pivot.localRotation = Quaternion.Euler(0, 0, -(float)game.Model.Angle);
            ball.anchoredPosition = new Vector2((float)game.Model.BallPosition, 32);
            timer.text = $"남은 시간 {game.Model.TimeLeft:F1}초";
            warning.color = Mathf.Abs((float)game.Model.Angle) > 36 || Mathf.Abs((float)game.Model.BallPosition) > 75
                ? new Color32(230, 126, 34, 255) : new Color32(52, 73, 94, 255);
        }
    }
}
