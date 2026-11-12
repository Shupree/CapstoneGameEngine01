using TMPro;
using UnityEngine;

namespace GE.MiniGames.Gust
{
    // 원본의 매 프레임 무작위 바람 이펙트는 생략하고 실제 위치와 바람 세기만 표시합니다.
    public sealed class GustView : MonoBehaviour
    {
        [SerializeField] private GustGame game;
        [SerializeField] private RectTransform player;
        [SerializeField] private TMP_Text timer, wind;
        public void Configure(GustGame owner, RectTransform playerImage, TMP_Text timeLabel, TMP_Text windLabel)
        { game = owner; player = playerImage; timer = timeLabel; wind = windLabel; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            player.anchoredPosition = new Vector2((float)game.Model.PlayerX - 200, 0);
            timer.text = $"남은 시간 {game.Model.TimeLeft:F1}초";
            wind.text = $"왼쪽으로 부는 바람 {game.Model.WindForce:F0}";
        }
    }
}
