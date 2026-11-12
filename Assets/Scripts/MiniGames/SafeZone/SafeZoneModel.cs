using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.SafeZone
{
    public enum SafeZoneStep { Running, Success, Hit }
    public sealed class ZoneAttack
    {
        public Rect Bounds { get; internal set; }
        public bool Striking { get; internal set; }
        internal double Timer = .6;
    }

    // 원본의 400×300 좌상단 좌표계와 축별 이동 속도를 사용합니다. 작은 고정 간격으로 충돌 누락을 막습니다.
    public sealed class SafeZoneModel
    {
        private readonly System.Random random;
        private readonly List<ZoneAttack> attacks = new List<ZoneAttack>();
        private double accumulator, spawnTimer = .5;
        private int ticks;
        public IReadOnlyList<ZoneAttack> Attacks { get; }
        public Vector2 Player { get; private set; } = new Vector2(200, 150);
        public double TimeLeft => Math.Max(0, 7 - ticks / 120.0);
        public SafeZoneStep Result { get; private set; }
        public SafeZoneModel(int? seed = null)
        { random = seed.HasValue ? new System.Random(seed.Value) : new System.Random(); Attacks = attacks.AsReadOnly(); }

        // 경고·타격 중인 영역과 겹치지 않는 패턴을 최대 10번 찾고, 없으면 이번 생성은 건너뜁니다.
        private void SpawnAttack()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bool horizontal = random.NextDouble() > .5;
                var bounds = horizontal ? new Rect(0, (float)random.NextDouble() * 220, 400, 80)
                    : new Rect((float)random.NextDouble() * 320, 0, 80, 300);
                if (attacks.Exists(attack => attack.Bounds.Overlaps(bounds))) continue;
                attacks.Add(new ZoneAttack { Bounds = bounds }); return;
            }
        }

        // 타격과 제한 시간이 같은 틱에 끝나면 원본처럼 피격을 먼저 판정합니다. 경고 중에는 피해가 없습니다.
        public void Step(double dt, int horizontal, int vertical)
        {
            if (Result != SafeZoneStep.Running || dt <= 0) return;
            accumulator += dt;
            const double step = 1.0 / 120;
            while (accumulator + 1e-10 >= step && Result == SafeZoneStep.Running)
            {
                accumulator -= step;
                Player = new Vector2(Mathf.Clamp(Player.x + Math.Sign(horizontal) * 180 * (float)step, 10, 390),
                    Mathf.Clamp(Player.y + Math.Sign(vertical) * 180 * (float)step, 10, 290));
                spawnTimer -= step;
                if (spawnTimer <= 1e-10)
                { SpawnAttack(); if (random.NextDouble() > .6) SpawnAttack(); spawnTimer += .8; }
                var body = new Rect(Player.x - 10, Player.y - 10, 20, 20);
                for (int i = attacks.Count - 1; i >= 0; i--)
                {
                    var attack = attacks[i]; attack.Timer -= step;
                    if (attack.Timer <= 1e-10)
                    {
                        if (attack.Striking) { attacks.RemoveAt(i); continue; }
                        attack.Striking = true; attack.Timer += .2;
                    }
                    if (attack.Striking && attack.Bounds.Overlaps(body)) Result = SafeZoneStep.Hit;
                }
                ticks++;
                if (ticks >= 840 && Result == SafeZoneStep.Running) Result = SafeZoneStep.Success;
            }
        }
    }
}
