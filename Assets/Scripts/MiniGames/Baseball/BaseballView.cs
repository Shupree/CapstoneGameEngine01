using TMPro;
using UnityEngine;

namespace GE.MiniGames.Baseball
{
    // 초록 타격존은 x=80, 반지름 21로 모델 판정과 동일합니다. 별도 타격 애니메이션은 사용하지 않습니다.
    public sealed class BaseballView : MonoBehaviour
    {
        [SerializeField] private BaseballGame game;
        [SerializeField] private RectTransform ball;
        [SerializeField] private TMP_Text score;
        public void Configure(BaseballGame owner, RectTransform ballImage, TMP_Text label) { game = owner; ball = ballImage; score = label; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            ball.gameObject.SetActive(game.Model.Active);
            ball.anchoredPosition = new Vector2((float)game.Model.BallX - 200, 0);
            score.text = $"타격 {game.Model.Hits} / 7";
        }
    }
}
