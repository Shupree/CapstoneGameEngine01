using System;
using System.Collections.Generic;

namespace GE.MiniGames.Tomato
{
    public enum TomatoStep { Running, Success, RockCaught, Missed }
    public sealed class FallingItem
    {
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public double Speed { get; internal set; }
        public bool Rock { get; internal set; }
    }
    // 바구니는 48×20px, 아이템 반지름은 12px입니다. x 판정은 원본처럼 중심에 좌우 5px 여유를 줍니다.
    public sealed class TomatoModel
    {
        private readonly Random random;
        private readonly List<FallingItem> items = new List<FallingItem>();
        private double accumulator, spawnTimer = 1;
        public IReadOnlyList<FallingItem> Items { get; }
        public double BasketX { get; private set; } = 176;
        public int Catches { get; private set; }
        public TomatoStep Result { get; private set; }
        public TomatoModel(int? seed = null)
        { random = seed.HasValue ? new Random(seed.Value) : new Random(); Items = items.AsReadOnly(); }
        private void Spawn()
        {
            bool rock = random.NextDouble() < .35; double speed = 180 + random.NextDouble() * 220;
            items.Add(new FallingItem { X = 20 + random.NextDouble() * 360, Y = -20, Speed = speed, Rock = rock });
            // 원본의 도착 시간 간격 보정: 다음 아이템이 최고 속도여도 약 1.1초 여유를 확보합니다.
            spawnTimer += 280 / speed - 280.0 / 400 + 1.1;
        }
        public void Step(double dt, int horizontal)
        {
            if (Result != TomatoStep.Running || dt <= 0) return;
            accumulator += dt; const double step = 1.0 / 120;
            // 작은 간격으로 이동·충돌을 함께 처리해 프레임이 느려도 바구니를 관통하지 않습니다.
            while (accumulator + 1e-10 >= step && Result == TomatoStep.Running)
            {
                accumulator -= step;
                BasketX = Math.Max(0, Math.Min(352, BasketX + Math.Sign(horizontal) * 450 * step));
                spawnTimer -= step; if (spawnTimer <= 1e-10) Spawn();
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i]; item.Y += item.Speed * step;
                    if (item.Y + 12 >= 260 && item.Y - 12 <= 280 && item.X >= BasketX - 5 && item.X <= BasketX + 53)
                    {
                        items.RemoveAt(i);
                        if (item.Rock) Result = TomatoStep.RockCaught;
                        else if (++Catches >= 7) Result = TomatoStep.Success;
                        if (Result != TomatoStep.Running) break;
                    }
                    else if (item.Y - 12 > 300)
                    {
                        items.RemoveAt(i);
                        if (!item.Rock) { Result = TomatoStep.Missed; break; }
                    }
                }
            }
        }
    }
}
