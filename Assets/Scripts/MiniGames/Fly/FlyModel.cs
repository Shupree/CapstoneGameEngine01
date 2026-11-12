using System;
using UnityEngine;

namespace GE.MiniGames.Fly
{
    // HTML과 같은 400×300 좌상단 좌표계입니다. 입력·결과 통지는 공통 컨트롤러에 맡깁니다.
    public sealed class FlyModel
    {
        private readonly System.Random random;
        private Vector2 target;
        private float changeTimer, sitTimer, scareTimer;
        public Vector2 Position { get; private set; }
        public Vector2 Swatter { get; private set; } = new Vector2(200, 150);
        public bool Sitting { get; private set; }
        public int Hits { get; private set; }
        public bool Success => Hits >= 5;
        public FlyModel(int? seed = null)
        { random = seed.HasValue ? new System.Random(seed.Value) : new System.Random(); ResetFly(); }
        private float Range(float low, float high) => low + (float)random.NextDouble() * (high - low);
        private void ResetFly()
        { Position = new Vector2(Range(40, 360), Range(40, 260)); Sitting = false; scareTimer = sitTimer = 0; NewTarget(); }
        private void NewTarget()
        { target = new Vector2(Range(20, 380), Range(20, 280)); changeTimer = Range(.2f, .5f); }

        // 새 Space 입력당 한 번 판정합니다. 시간 제한·헛스윙 페널티는 원본처럼 없습니다.
        public bool Swing()
        {
            if (Success || Vector2.Distance(Position, Swatter) >= 25) return false;
            Hits++;
            if (!Success) ResetFly();
            return true;
        }

        // 대각 이동도 HTML처럼 축별 450px/s로 처리합니다. 파리는 목표를 넘겨 진동하지 않게 보정합니다.
        public void Step(float dt, int horizontal, int vertical)
        {
            if (Success || dt <= 0) return;
            Swatter = new Vector2(Mathf.Clamp(Swatter.x + Math.Sign(horizontal) * 450 * dt, 20, 380),
                Mathf.Clamp(Swatter.y + Math.Sign(vertical) * 450 * dt, 20, 280));
            if (!Sitting)
            {
                changeTimer -= dt;
                if (changeTimer <= 0)
                {
                    if (random.NextDouble() < .45) { Sitting = true; sitTimer = .8f; scareTimer = 0; }
                    else NewTarget();
                }
                Position = Vector2.MoveTowards(Position, target, 510 * dt);
                if (Vector2.Distance(Position, target) <= 5 && changeTimer > 0) changeTimer = 0;
            }
            else
            {
                sitTimer -= dt;
                scareTimer = Vector2.Distance(Position, Swatter) < 55 ? scareTimer + dt : 0;
                if (sitTimer <= 0 || scareTimer >= .3f) { Sitting = false; NewTarget(); }
            }
        }
    }
}
