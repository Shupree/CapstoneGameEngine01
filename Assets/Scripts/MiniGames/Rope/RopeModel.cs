using System;

namespace GE.MiniGames.Rope
{
    public enum RopeStep { Running, Success, Tilted, BallDropped }

    // HTML의 60fps 관성·바람 수치를 고정 간격으로 계산해 화면 프레임률과 난도를 분리합니다.
    public sealed class RopeModel
    {
        private readonly Random random;
        private double accumulator, velocity, wind, ballVelocity;
        private int ticks;
        public double Angle { get; private set; }
        public double BallPosition { get; private set; }
        public double TimeLeft => Math.Max(0, 5 - ticks / 60.0);
        public RopeStep Result { get; private set; }
        public RopeModel(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            Angle = (random.NextDouble() > .5 ? 1 : -1) * (10 + random.NextDouble() * 5);
            BallPosition = (random.NextDouble() - .5) * 40;
        }

        // 실패를 시간 만료보다 먼저 판정합니다. 정지 중에는 컨트롤러가 이 함수를 호출하지 않습니다.
        public void Step(double dt, int direction)
        {
            if (Result != RopeStep.Running || dt <= 0) return;
            accumulator += dt;
            const double step = 1.0 / 60;
            while (accumulator + 1e-10 >= step && Result == RopeStep.Running)
            {
                accumulator -= step;
                wind = random.NextDouble() < .03 ? (random.NextDouble() - .5) * 100 : wind * .88;
                velocity = (velocity + (Angle + wind + Math.Sign(direction) * 55) * step) * .96;
                Angle += velocity * step;
                ballVelocity = (ballVelocity + Angle * 12 * step) * .98;
                BallPosition += ballVelocity * step;
                if (Math.Abs(Angle) >= 48) Result = RopeStep.Tilted;
                else if (Math.Abs(BallPosition) > 100) Result = RopeStep.BallDropped;
                else if (++ticks >= 300) Result = RopeStep.Success;
            }
        }
    }
}
