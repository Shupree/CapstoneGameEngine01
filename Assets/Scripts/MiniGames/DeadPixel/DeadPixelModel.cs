using System;
using UnityEngine;

namespace GE.MiniGames.DeadPixel
{
    public enum DeadPixelStep { Running, Success, Wrong, Timeout }
    // MiniGame18의 HTML은 두 그림 비교가 아니라 3×3에서 명도가 다른 한 칸을 찾는 규칙입니다.
    public sealed class DeadPixelModel
    {
        private readonly System.Random random;
        public int CursorX { get; private set; } = 1;
        public int CursorY { get; private set; } = 1;
        public int TargetIndex { get; private set; }
        public int Score { get; private set; }
        public double TimeLeft { get; private set; } = 14;
        public Color BaseColor { get; private set; }
        public Color TargetColor { get; private set; }
        public DeadPixelStep Result { get; private set; }
        public DeadPixelModel(int? seed = null) { random = seed.HasValue ? new System.Random(seed.Value) : new System.Random(); NextPixel(); }
        private void NextPixel()
        {
            CursorX = CursorY = 1; TargetIndex = random.Next(9); float hue = random.Next(360) / 360f;
            BaseColor = Hsl(hue, .5f); TargetColor = Hsl(hue, .62f);
        }
        // HTML HSL(채도 60%)을 HSV로 변환해 원본과 같은 색 차이를 표시합니다.
        private static Color Hsl(float hue, float lightness)
        { float value = lightness + .6f * Mathf.Min(lightness, 1 - lightness); return Color.HSVToRGB(hue, 2 * (1 - lightness / value), value); }
        public void Move(int horizontal, int vertical)
        {
            if (Result != DeadPixelStep.Running) return;
            CursorX = Math.Max(0, Math.Min(2, CursorX + Math.Sign(horizontal)));
            CursorY = Math.Max(0, Math.Min(2, CursorY + Math.Sign(vertical)));
        }
        public void Select()
        {
            if (Result != DeadPixelStep.Running) return;
            if (CursorY * 3 + CursorX != TargetIndex) { Result = DeadPixelStep.Wrong; return; }
            if (++Score >= 15) Result = DeadPixelStep.Success; else NextPixel();
        }
        // 다음 문제로 넘어가도 14초 전체 제한 시간을 초기화하지 않습니다.
        public void Step(double dt)
        {
            if (Result != DeadPixelStep.Running || dt <= 0) return;
            TimeLeft = Math.Max(0, TimeLeft - dt);
            if (TimeLeft <= 1e-9) { TimeLeft = 0; Result = DeadPixelStep.Timeout; }
        }
    }
}
