using System;

namespace GE.MiniGames.Gust
{
    public enum GustStep { Running, Success, WallHit }
    // 바람은 왼쪽으로 밀고 Space의 새 눌림은 오른쪽으로 28px 이동시킵니다.
    public sealed class GustModel
    {
        private readonly Random random;
        private double accumulator, windTimer;
        private int ticks;
        public double PlayerX { get; private set; } = 200;
        public double WindForce { get; private set; } = 150;
        public double TimeLeft => Math.Max(0, 6 - ticks / 120.0);
        public GustStep Result { get; private set; }
        public GustModel(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public void Push()
        {
            if (Result != GustStep.Running) return;
            PlayerX += 28;
            // 입력 순간 벽을 넘은 뒤 다음 바람 틱으로 복귀해도 피격을 취소하지 않습니다.
            CheckWall();
        }
        private void CheckWall() { if (PlayerX <= 15 || PlayerX >= 385) Result = GustStep.WallHit; }
        public void Step(double dt)
        {
            if (Result != GustStep.Running || dt <= 0) return;
            accumulator += dt; const double step = 1.0 / 120;
            while (accumulator + 1e-10 >= step && Result == GustStep.Running)
            {
                accumulator -= step; ticks++;
                windTimer -= step;
                if (windTimer <= 1e-10) { WindForce = 120 + random.NextDouble() * 250; windTimer = .2 + random.NextDouble() * .5; }
                PlayerX -= WindForce * step; CheckWall();
                if (ticks >= 720 && Result == GustStep.Running) Result = GustStep.Success;
            }
        }
    }
}
