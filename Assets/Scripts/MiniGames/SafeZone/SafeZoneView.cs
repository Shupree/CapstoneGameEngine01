using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.SafeZone
{
    // 경고와 실제 타격을 색 농도로 구분합니다. 정지 중에도 마지막 판정 상태 그대로 표시합니다.
    public sealed class SafeZoneView : MonoBehaviour
    {
        [SerializeField] private SafeZoneGame game;
        [SerializeField] private RectTransform player;
        [SerializeField] private Image[] regions;
        [SerializeField] private TMP_Text timer;
        public void Configure(SafeZoneGame owner, RectTransform playerImage, Image[] attackImages, TMP_Text timeLabel)
        { game = owner; player = playerImage; regions = attackImages; timer = timeLabel; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            player.anchoredPosition = new Vector2(game.Model.Player.x - 200, 150 - game.Model.Player.y);
            timer.text = $"남은 시간 {game.Model.TimeLeft:F1}초";
            for (int i = 0; i < regions.Length; i++)
            {
                bool active = i < game.Model.Attacks.Count; regions[i].gameObject.SetActive(active);
                if (!active) continue;
                var attack = game.Model.Attacks[i]; var rect = attack.Bounds;
                regions[i].rectTransform.anchoredPosition = new Vector2(rect.center.x - 200, 150 - rect.center.y);
                regions[i].rectTransform.sizeDelta = rect.size;
                regions[i].color = new Color(231 / 255f, 76 / 255f, 60 / 255f, attack.Striking ? .9f : .3f);
            }
        }
    }
}
