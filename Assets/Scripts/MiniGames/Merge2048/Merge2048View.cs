using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Merge2048
{
    // 고정된 16칸의 숫자와 색만 갱신합니다. 애니메이션 없이 판정이 끝난 보드와 표시를 일치시킵니다.
    [DefaultExecutionOrder(100)]
    public sealed class Merge2048View : MonoBehaviour
    {
        [SerializeField] private Merge2048Game game;
        [SerializeField] private Image[] tiles;
        [SerializeField] private TMP_Text[] numbers;
        [SerializeField] private TMP_Text highest, moves;
        [SerializeField] private RectTransform progressFill;
        private static readonly Color[] Palette = {
            new Color32(205, 193, 180, 255), new Color32(238, 228, 218, 255), new Color32(237, 224, 200, 255),
            new Color32(242, 177, 121, 255), new Color32(245, 149, 99, 255), new Color32(246, 124, 95, 255),
            new Color32(246, 94, 59, 255), new Color32(237, 207, 114, 255) };

        // 씬의 Image/TMP 참조만 연결해 팀원이 타일 스프라이트나 글꼴을 교체할 수 있게 합니다.
        public void Configure(Merge2048Game controller, Image[] cells, TMP_Text[] labels, TMP_Text maximum, TMP_Text turnCount, RectTransform fill)
        { game = controller; tiles = cells; numbers = labels; highest = maximum; moves = turnCount; progressFill = fill; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            for (int i = 0; i < tiles.Length; i++)
            {
                int value = game.Model.Tiles[i], rank = 0;
                for (int n = value; n > 1; n /= 2) rank++;
                tiles[i].color = Palette[Mathf.Min(rank, Palette.Length - 1)];
                numbers[i].text = value == 0 ? string.Empty : value.ToString();
                numbers[i].color = value <= 4 || value >= 128 ? new Color32(92, 80, 65, 255) : new Color32(255, 250, 243, 255);
            }
            highest.text = $"최대 {game.Progress} / {game.Target}";
            moves.text = $"이동 {game.Model.MoveCount}회";
            progressFill.anchorMax = new Vector2(Mathf.Clamp01((float)game.Progress / game.Target), 1);
        }
    }
}
