using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Cross
{
    // 저장된 UI만 갱신합니다. 시작·재시도·정지 화면은 MiniGame04와 동일한 MiniGameTestUI가 담당합니다.
    [DefaultExecutionOrder(100)]
    public sealed class CrossView : MonoBehaviour
    {
        [SerializeField] private CrossGame game;
        [SerializeField] private Image player;
        [SerializeField] private Image[] cars;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private RectTransform progressFill;

        // 씬 빌더가 최대 20대의 차량 슬롯을 직렬화하므로 플레이 중 오브젝트 생성/삭제가 필요 없습니다.
        public void Configure(CrossGame controller, Image character, Image[] vehicles, TMP_Text counter, RectTransform fill)
        { game = controller; player = character; cars = vehicles; progress = counter; progressFill = fill; }

        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (game == null || game.Model == null || player == null) return;
            var model = game.Model;
            Place(player, model.PlayerX, model.PlayerY, CrossModel.PlayerSize, CrossModel.PlayerSize);
            for (int i = 0; i < cars.Length; i++)
            {
                bool visible = i < model.Cars.Count;
                cars[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var car = model.Cars[i];
                Place(cars[i], car.X, car.Row * CrossModel.CellSize + (CrossModel.CellSize - CrossModel.CarHeight) / 2,
                    CrossModel.CarWidth, CrossModel.CarHeight);
            }
            progress.text = $"{game.Progress:00} / {game.Target:00}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
        }

        // HTML 좌상단 좌표를 중앙 원점 UI로 변환합니다. 표시 크기와 충돌 크기를 같은 상수에서 가져옵니다.
        private static void Place(Image image, float x, float y, float width, float height)
        {
            image.rectTransform.anchoredPosition = new Vector2(x + width / 2 - CrossModel.Width / 2f,
                CrossModel.Height / 2f - y - height / 2);
            image.rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}
