using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Dino
{
    // 입력과 상태 화면은 MiniGameTestUI에 맡깁니다. 모델 시간만 읽어 정지 시 날개와 지면도 함께 멈춥니다.
    public sealed class DinoView : MonoBehaviour
    {
        [SerializeField] private DinoGame game;
        [SerializeField] private RectTransform field;
        [SerializeField] private Image dinosaur, flag;
        [SerializeField] private RawImage ground;
        [SerializeField] private TMP_Text percentage;
        [SerializeField] private RectTransform progressFill;
        private readonly List<Image> obstacleImages = new List<Image>();

        public void Configure(DinoGame controller, RectTransform playfield, Image runner, Image finish,
            RawImage floor, TMP_Text label, RectTransform fill)
        { game = controller; field = playfield; dinosaur = runner; flag = finish; ground = floor; percentage = label; progressFill = fill; }

        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || dinosaur == null) return;
            var model = game.Model;
            var rules = model?.Rules ?? game.Settings;
            var art = game.Art;
            if (art == null) return;
            // 크기·지면 위치 또는 아트 참조를 바꿔도 표시와 모델의 논리 좌표를 함께 맞춥니다.
            field.sizeDelta = rules.viewport;
            ground.texture = art.ground.texture;
            Place(ground.rectTransform, rules, Vector2.zero, new Vector2(rules.viewport.x, rules.GroundY));
            flag.sprite = art.flag;
            int animation = model?.AnimationFrame ?? 0;
            dinosaur.sprite = model != null && !model.Grounded ? art.dinosaurJump.sprite : art.dinosaurRun[animation % art.dinosaurRun.Length].sprite;
            Place(dinosaur.rectTransform, rules, new Vector2(rules.dinosaurLeft, rules.GroundY + (model?.Height ?? 0)), rules.dinosaurSize);
            float ratio = model?.Progress01 ?? 0;
            percentage.text = (ratio >= 1 ? 100 : Mathf.FloorToInt(ratio * 100)) + "%";
            progressFill.anchorMax = new Vector2(ratio, 1);
            float distance = model?.Distance ?? 0;
            ground.uvRect = new Rect(Mathf.Repeat(distance / art.ground.rect.width, 1), 0, rules.viewport.x / art.ground.rect.width, 1);
            flag.enabled = model != null && model.FlagX <= rules.viewport.x;
            if (flag.enabled) Place(flag.rectTransform, rules, new Vector2(model.FlagX, rules.GroundY), new Vector2(28, 54));
            int shown = 0;
            if (model != null) for (int i = 0; i < model.Course.Count; i++)
            {
                var obstacle = model.Course[i];
                var size = rules.Size(obstacle.Kind, obstacle.Variant);
                float x = model.ObstacleX(i);
                if (x > rules.viewport.x || x + size.x < 0) continue;
                if (shown == obstacleImages.Count)
                {
                    var image = new GameObject("Obstacle visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                    image.gameObject.layer = field.gameObject.layer;
                    image.rectTransform.SetParent(field, false); image.raycastTarget = false;
                    // 동적 그림이 공통 시작·정지·결과 패널 위에 올라오지 않도록 게임 레이어에 삽입합니다.
                    image.transform.SetSiblingIndex(dinosaur.transform.GetSiblingIndex());
                    obstacleImages.Add(image);
                }
                var visual = obstacleImages[shown++];
                var frames = art.Frames(obstacle.Kind, obstacle.Variant);
                visual.enabled = true; visual.sprite = frames[animation % frames.Length].sprite;
                Place(visual.rectTransform, rules, new Vector2(x, rules.GroundY + rules.Bottom(obstacle.Kind)), size);
            }
            for (int i = shown; i < obstacleImages.Count; i++) obstacleImages[i].enabled = false;
        }

        // 논리 좌표의 왼쪽 아래를 Canvas 중심 좌표로 한 번만 변환합니다.
        private static void Place(RectTransform rect, DinoRules rules, Vector2 bottomLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size; rect.anchoredPosition = bottomLeft + size * 0.5f - rules.viewport * 0.5f;
        }
    }
}
