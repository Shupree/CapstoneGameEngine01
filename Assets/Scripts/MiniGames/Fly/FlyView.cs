using TMPro;
using UnityEngine;

namespace GE.MiniGames.Fly
{
    // 파리와 파리채의 판정 좌표를 그대로 표시합니다. 팀원이 Image 스프라이트를 교체할 수 있습니다.
    public sealed class FlyView : MonoBehaviour
    {
        [SerializeField] private FlyGame game;
        [SerializeField] private RectTransform fly, swatter;
        [SerializeField] private TMP_Text score;
        public void Configure(FlyGame owner, RectTransform flyImage, RectTransform swatterImage, TMP_Text label)
        { game = owner; fly = flyImage; swatter = swatterImage; score = label; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            fly.anchoredPosition = ToUI(game.Model.Position);
            swatter.anchoredPosition = ToUI(game.Model.Swatter);
            score.text = $"잡은 파리 {game.Model.Hits} / 5";
        }
        private static Vector2 ToUI(Vector2 point) => new Vector2(point.x - 200, 150 - point.y);
    }
}
