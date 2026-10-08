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

        // Also used by the editor builder: the saved scene has all visual objects and references.
        public void BuildGrid(int width, int height)
        {
            if (grid != null)
            {
                grid.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(grid.gameObject); else DestroyImmediate(grid.gameObject);
            }
            pixels.Clear();
            columns = width; rows = height;
            grid = Rect("Grid", board, Vector2.zero, board.sizeDelta);
            float cell = Mathf.Min(board.sizeDelta.x / width, board.sizeDelta.y / height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 position = Position(x, y);
                Image tile = Pixel($"Cell {x},{y}", grid, position, Vector2.one * (cell - 2),
                    (x + y) % 2 == 0 ? new Color32(27, 48, 46, 255) : new Color32(30, 53, 49, 255));
                Image token = Pixel("Token", tile.rectTransform, Vector2.zero, Vector2.one * (cell * 0.78f), BodyColor);
                token.enabled = false;
                pixels.Add(token);
            }
            eyes = Rect("Head eyes", grid, Vector2.zero, Vector2.one * cell);
            Pixel("Eye upper", eyes, new Vector2(cell * 0.17f, cell * 0.18f), Vector2.one * (cell * 0.10f), new Color32(22, 43, 40, 255));
            Pixel("Eye lower", eyes, new Vector2(cell * 0.17f, -cell * 0.18f), Vector2.one * (cell * 0.10f), new Color32(22, 43, 40, 255));
            stem = Pixel("Apple leaf", grid, Vector2.zero, new Vector2(cell * 0.18f, cell * 0.12f), BodyColor).rectTransform;
            eyes.gameObject.SetActive(false); stem.gameObject.SetActive(false);
        }

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
            for (int i = model.Body.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = model.Body[i];
                var pixel = pixels[cell.y * columns + cell.x];
                pixel.enabled = true;
                pixel.color = i == 0 ? HeadColor : BodyColor;
                pixel.rectTransform.sizeDelta = Vector2.one * (CellSize * 0.78f);
            }
            if (model.HasApple)
            {
                var apple = pixels[model.Apple.y * columns + model.Apple.x];
                apple.enabled = true; apple.color = AppleColor;
                apple.rectTransform.sizeDelta = Vector2.one * (CellSize * 0.56f);
                stem.anchoredPosition = Position(model.Apple.x, model.Apple.y) + new Vector2(CellSize * 0.08f, CellSize * 0.32f);
            }
            stem.gameObject.SetActive(model.HasApple);
            Vector2Int head = model.Body[0];
            eyes.anchoredPosition = Position(head.x, head.y);
            eyes.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(model.Direction.y, model.Direction.x) * Mathf.Rad2Deg);
            eyes.gameObject.SetActive(true);
            if (progressText != null) progressText.text = $"사과  {model.ApplesEaten} / {model.TargetApples}";
            if (rulesText != null)
                rulesText.text = $"{model.Width} × {model.Height}    /    사과 {model.TargetApples}개를 모으세요    /    {game.MoveInterval:0.##}초마다 이동";
            if (progressFill != null)
            {
                // A plain rectangle avoids depending on imported sliced/fill sprites.
                progressFill.rectTransform.anchorMax = new Vector2((float)model.ApplesEaten / model.TargetApples, 1);
            }
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
