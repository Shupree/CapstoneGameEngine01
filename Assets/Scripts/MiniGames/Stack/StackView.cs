using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Stack
{
    // 7층 모두 300px 화면 안에 들어가므로 원본의 상단 잘림을 만드는 추가 스크롤은 적용하지 않습니다.
    public sealed class StackView : MonoBehaviour
    {
        [SerializeField] private StackGame game;
        [SerializeField] private Image[] blocks;
        [SerializeField] private Image current;
        [SerializeField] private TMP_Text score;
        public void Configure(StackGame owner, Image[] stackImages, Image movingImage, TMP_Text label)
        { game = owner; blocks = stackImages; current = movingImage; score = label; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            score.text = $"쌓은 블록 {game.Model.Count} / 7";
            for (int i = 0; i < blocks.Length; i++)
            {
                bool active = i < game.Model.Blocks.Count; blocks[i].gameObject.SetActive(active);
                if (active) Place(blocks[i], game.Model.Blocks[i]);
            }
            current.gameObject.SetActive(game.Model.Result == StackStep.Running);
            Place(current, game.Model.Current);
        }
        private static void Place(Image image, StackBlock block)
        {
            image.rectTransform.anchoredPosition = new Vector2(block.X + block.Width / 2 - 200, 150 - block.Y - 12.5f);
            image.rectTransform.sizeDelta = new Vector2(block.Width, 25);
        }
    }
}
