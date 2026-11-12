using System;

namespace GE.MiniGames.Fishing
{
    public enum FishingStep { Running, Success, Timeout }
    // 포획 판정은 물고기 중심과 높이 70px 영역의 중심 차이로 계산합니다.
    public sealed class FishingModel
    {
        private readonly Random random;
        private double accumulator, moveTimer, target = 150, speed;
        private int ticks;
        public double CaptureY { get; private set; } = 150;
        public double FishY { get; private set; } = 150;
        public double Progress { get; private set; }
        public double TimeLeft => Math.Max(0, 10 - ticks / 120.0);
        public bool Captured => Math.Abs(FishY - CaptureY) <= 35;
        public FishingStep Result { get; private set; }
        public FishingModel(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }
        // 120Hz로 이동·획득량을 계산합니다. 재시도는 새 모델로 물고기 목표와 이동 타이머까지 초기화합니다.
        public void Step(double dt, int vertical)
        {
            if (Result != FishingStep.Running || dt <= 0) return;
            accumulator += dt; const double step = 1.0 / 120;
            while (accumulator + 1e-10 >= step && Result == FishingStep.Running)
            {
                accumulator -= step; ticks++;
                CaptureY = Math.Max(35, Math.Min(265, CaptureY + Math.Sign(vertical) * 200 * step));
                moveTimer -= step;
                if (moveTimer <= 1e-10)
                { target = 20 + random.NextDouble() * 260; speed = 80 + random.NextDouble() * 250; moveTimer = .4 + random.NextDouble() * .9; }
                FishY += Math.Sign(target - FishY) * Math.Min(speed * step, Math.Abs(target - FishY));
                Progress = Math.Max(0, Math.Min(100, Progress + (Captured ? 25 : -10) * step));
                // 제한 시간과 포획 완료가 겹치면 원본처럼 포획 성공을 우선합니다.
                if (Progress >= 100 - 1e-9) { Progress = 100; Result = FishingStep.Success; }
                else if (ticks >= 1200) Result = FishingStep.Timeout;
            }
        }
    }
}
