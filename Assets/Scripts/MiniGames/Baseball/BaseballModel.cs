using System;

namespace GE.MiniGames.Baseball
{
    public enum BaseballStep { Running, Success, Early, Mistimed, Missed }
    // HTML의 투구 간격·속도·공 중심 기준 타격 범위를 그대로 보관합니다.
    public sealed class BaseballModel
    {
        private readonly Random random;
        private double spawnTimer, speed;
        public double BallX { get; private set; }
        public bool Active { get; private set; }
        public int Hits { get; private set; }
        public BaseballStep Result { get; private set; }
        public BaseballModel(int? seed = null)
        { random = seed.HasValue ? new Random(seed.Value) : new Random(); PreparePitch(); }
        private void PreparePitch()
        { BallX = 450; speed = 300 + random.NextDouble() * 350; Active = false; spawnTimer = .3 + random.NextDouble() * 1.5; }
        // 한 번의 새 Space 입력만 받습니다. 대기 중 휘두르거나 타격존 밖이면 즉시 실패합니다.
        public void Swing()
        {
            if (Result != BaseballStep.Running) return;
            if (!Active) { Result = BaseballStep.Early; return; }
            if (Math.Abs(BallX - 80) > 21) { Result = BaseballStep.Mistimed; return; }
            if (++Hits >= 7) Result = BaseballStep.Success; else PreparePitch();
        }
        // 투구 대기가 끝난 프레임의 남은 시간도 이동에 반영해 프레임률에 따른 지연을 없앱니다.
        public void Step(double dt)
        {
            if (Result != BaseballStep.Running || dt <= 0) return;
            if (!Active)
            {
                spawnTimer -= dt;
                if (spawnTimer > 0) return;
                dt = -spawnTimer; Active = true;
            }
            BallX -= speed * dt;
            if (BallX < 0) Result = BaseballStep.Missed;
        }
    }
}
