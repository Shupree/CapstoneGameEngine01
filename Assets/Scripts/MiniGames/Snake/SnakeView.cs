using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Snake
{
    public sealed class SnakeView : MonoBehaviour
    {
        [SerializeField] private SnakeGame game;
        [SerializeField] private RectTransform board;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private List<Image> pixels = new List<Image>();
        [SerializeField] private RectTransform grid;
        [SerializeField] private RectTransform eyes;
        [SerializeField] private RectTransform stem;
        [SerializeField] private int columns;
        [SerializeField] private int rows;

        // MiniGame01 씬에서만 연결합니다. 규칙 모델은 이미지나 에셋 경로에 의존하지 않습니다.
        [Header("Snake sprites")]
        [SerializeField] private Sprite tileSprite;
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private Sprite appleSprite;
        [SerializeField] private Sprite headUpSprite, headLeftSprite, headDownSprite, headRightSprite;

        // 타일·몸통·사과·머리에 같은 원본 픽셀 배율을 적용합니다. 격자 개수가 바뀌어도 도트 크기는 유지합니다.
        private const float SpriteScale = 1.8f;

        private static readonly Color BodyColor = new Color32(106, 204, 143, 255);
        private static readonly Color HeadColor = new Color32(202, 245, 142, 255);
        private static readonly Color AppleColor = new Color32(252, 108, 101, 255);

        public void Configure(SnakeGame controller, RectTransform boardRect, TMP_Text progress, Image fill)
        {
            game = controller; board = boardRect; progressText = progress; progressFill = fill;
        }
        public void ConfigureRulesLabel(TMP_Text label) { rulesText = label; }
        private void OnEnable() { if (game != null) game.Changed += Refresh; Refresh(); }
        private void Start() { Refresh(); }
        private void OnDisable() { if (game != null) game.Changed -= Refresh; }

        // 보드 크기가 달라질 때와 에디터 씬 구성 시 호출하며, 씬에 지정된 타일 이미지를 재사용합니다.
        public void BuildGrid(int width, int height)
        {
            if (grid != null)
            {
                grid.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(grid.gameObject); else DestroyImmediate(grid.gameObject);
            }
            pixels.Clear();
            columns = width; rows = height;
            if (tileSprite != null) board.sizeDelta = new Vector2(width, height) * tileSprite.rect.width * SpriteScale;
            grid = Rect("Grid", board, Vector2.zero, board.sizeDelta);
            float cell = Mathf.Min(board.sizeDelta.x / width, board.sizeDelta.y / height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 position = Position(x, y);
                Image tile = Pixel($"Cell {x},{y}", grid, position, Vector2.one * (cell - 2),
                    (x + y) % 2 == 0 ? new Color32(27, 48, 46, 255) : new Color32(30, 53, 49, 255));
                tile.sprite = tileSprite;
                if (tileSprite != null)
                {
                    tile.color = Color.white;
                    tile.rectTransform.sizeDelta = tileSprite.rect.size * SpriteScale;
                }
            }
            // 머리의 돌출된 혀가 이웃 타일에 가려지지 않도록 모든 토큰을 타일보다 나중에 그립니다.
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Image token = Pixel("Token", grid, Position(x, y), Vector2.one * (cell * 0.78f), BodyColor);
                token.enabled = false;
                pixels.Add(token);
            }
            eyes = Rect("Head eyes", grid, Vector2.zero, Vector2.one * cell);
            Pixel("Eye upper", eyes, new Vector2(cell * 0.17f, cell * 0.18f), Vector2.one * (cell * 0.10f), new Color32(22, 43, 40, 255));
            Pixel("Eye lower", eyes, new Vector2(cell * 0.17f, -cell * 0.18f), Vector2.one * (cell * 0.10f), new Color32(22, 43, 40, 255));
            stem = Pixel("Apple leaf", grid, Vector2.zero, new Vector2(cell * 0.18f, cell * 0.12f), BodyColor).rectTransform;
            eyes.gameObject.SetActive(false); stem.gameObject.SetActive(false);
        }

        // 기존 Changed 이벤트에 맞춰 역할과 방향을 갱신합니다. 한 셀이 사과에서 몸통으로 바뀌어도
        // 이전 이미지·크기·위치 보정이 남지 않도록 매번 함께 설정합니다.
        public void Refresh()
        {
            if (game == null || board == null) return;
            var model = game.Model;
            if (model == null)
            {
                if (grid != null) grid.gameObject.SetActive(false);
                return;
            }
            if (grid == null || columns != model.Width || rows != model.Height) BuildGrid(model.Width, model.Height);
            grid.gameObject.SetActive(true);
            foreach (Image pixel in pixels) pixel.enabled = false;
            Sprite headSprite = HeadSprite(model.Direction);
            for (int i = model.Body.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = model.Body[i];
                var pixel = pixels[cell.y * columns + cell.x];
                pixel.enabled = true;
                SetToken(pixel, i == 0 ? headSprite : bodySprite, cell,
                    i == 0 ? HeadColor : BodyColor, 0.78f, i == 0 ? model.Direction : Vector2Int.zero);
            }
            if (model.HasApple)
            {
                var apple = pixels[model.Apple.y * columns + model.Apple.x];
                apple.enabled = true;
                SetToken(apple, appleSprite, model.Apple, AppleColor, 0.56f, Vector2Int.zero);
                stem.anchoredPosition = Position(model.Apple.x, model.Apple.y) + new Vector2(CellSize * 0.08f, CellSize * 0.32f);
            }
            // 새 그림에 눈과 꼭지가 포함되어 있으므로, 기존 장식은 이미지가 없는 경우에만 표시합니다.
            stem.gameObject.SetActive(model.HasApple && appleSprite == null);
            Vector2Int head = model.Body[0];
            eyes.anchoredPosition = Position(head.x, head.y);
            eyes.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(model.Direction.y, model.Direction.x) * Mathf.Rad2Deg);
            eyes.gameObject.SetActive(headSprite == null);
            if (progressText != null) progressText.text = $"사과  {model.ApplesEaten} / {model.TargetApples}";
            if (rulesText != null)
                rulesText.text = $"{model.Width} × {model.Height}    /    사과 {model.TargetApples}개를 모으세요    /    {game.MoveInterval:0.##}초마다 이동";
            if (progressFill != null)
            {
                // A plain rectangle avoids depending on imported sliced/fill sprites.
                progressFill.rectTransform.anchorMax = new Vector2((float)model.ApplesEaten / model.TargetApples, 1);
            }
        }

        // 그림에 포함된 광원 방향을 유지하기 위해 회전 대신 WASD별 원본을 선택합니다.
        private Sprite HeadSprite(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return headUpSprite;
            if (direction == Vector2Int.left) return headLeftSprite;
            if (direction == Vector2Int.down) return headDownSprite;
            return headRightSprite;
        }

        private void SetToken(Image image, Sprite sprite, Vector2Int cell, Color fallbackColor,
            float fallbackSize, Vector2Int direction)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.preserveAspect = sprite != null;
            Vector2 size = Vector2.one * (CellSize * fallbackSize);
            Vector2 offset = Vector2.zero;
            if (sprite != null)
            {
                // 원본 몸통은 28px, 머리는 혀 방향으로 8px 더 깁니다. 몸통 중심을 격자에 맞추고
                // 혀만 이동 방향으로 돌출시켜 네 방향 모두 몸통과 동일한 배율을 유지합니다.
                size = sprite.rect.size * SpriteScale;
                offset = (Vector2)direction * ((Mathf.Max(sprite.rect.width, sprite.rect.height) - 28f) * SpriteScale * 0.5f);
            }
            image.rectTransform.sizeDelta = size;
            // 기존 씬의 타일 자식 토큰도 지원하며, 새 씬은 토큰을 Grid 직속으로 배치합니다.
            image.rectTransform.anchoredPosition = (image.transform.parent == grid ? Position(cell.x, cell.y) : Vector2.zero) + offset;
        }

        private float CellSize => Mathf.Min(board.sizeDelta.x / columns, board.sizeDelta.y / rows);
        private Vector2 Position(int x, int y) => new Vector2((x - (columns - 1) * 0.5f) * CellSize, (y - (rows - 1) * 0.5f) * CellSize);

        private static RectTransform Rect(string label, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static Image Pixel(string label, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Rect(label, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
    }
}
