using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Rope
{
    // 판 위의 공과 판을 벗어난 뒤의 실제 낙하 위치를 모델에 맞춰 표시합니다.
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
            if (game.Model.Falling)
            {
                // 공은 판의 자식이므로 월드 방향 낙하 좌표를 판의 로컬 좌표로 역회전합니다.
                float angle = (float)game.Model.Angle * Mathf.Deg2Rad;
                float x = (float)game.Model.FallX, y = (float)game.Model.FallY;
                ball.anchoredPosition = new Vector2(x * Mathf.Cos(angle) - y * Mathf.Sin(angle), x * Mathf.Sin(angle) + y * Mathf.Cos(angle));
            }
            timer.text = $"남은 시간 {game.Model.TimeLeft:F1}초";
            warning.color = Mathf.Abs((float)game.Model.Angle) > 36 || Mathf.Abs((float)game.Model.BallPosition) > 75
                ? new Color32(230, 126, 34, 255) : new Color32(52, 73, 94, 255);
        }
    }
}
