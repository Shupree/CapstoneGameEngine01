using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Tomato
{
    // 미리 만든 Image 슬롯만 재사용하며 빨간 토마토와 회색 돌을 구분합니다.
    public sealed class TomatoView : MonoBehaviour
    {
        [SerializeField] private TomatoGame game;
        [SerializeField] private RectTransform basket;
        [SerializeField] private Image[] items;
        [SerializeField] private TMP_Text score;
        public void Configure(TomatoGame owner, RectTransform basketImage, Image[] itemImages, TMP_Text label)
        { game = owner; basket = basketImage; items = itemImages; score = label; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            basket.anchoredPosition = new Vector2((float)game.Model.BasketX + 24 - 200, -120);
            score.text = $"받은 토마토 {game.Model.Catches} / 7";
            for (int i = 0; i < items.Length; i++)
            {
                bool active = i < game.Model.Items.Count; items[i].gameObject.SetActive(active); if (!active) continue;
                var item = game.Model.Items[i]; items[i].rectTransform.anchoredPosition = new Vector2((float)item.X - 200, 150 - (float)item.Y);
                items[i].color = item.Rock ? new Color32(127, 140, 141, 255) : new Color32(231, 76, 60, 255);
            }
        }
    }
}
