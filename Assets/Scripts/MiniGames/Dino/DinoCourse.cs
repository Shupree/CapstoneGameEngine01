using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.Dino
{
    // CourseX는 출발 기준 장애물의 왼쪽 끝입니다. 폭을 더하지 않아 앞쪽 끝 사이의 시간 분포를 유지합니다.
    public sealed class DinoObstacle
    {
        public DinoObstacleKind Kind;
        public int Variant;
        public float CourseX;
        public bool NeedsJump => Kind != DinoObstacleKind.HighBird;
    }

    public static class DinoCourse
    {
        public static List<DinoObstacle> Build(DinoRules rules)
        {
            var random = rules.randomSeed < 0 ? new System.Random() : new System.Random(rules.randomSeed);
            var result = new List<DinoObstacle>();
            var current = Pick(random, rules, DinoObstacleKind.Cactus);
            current.CourseX = rules.DinosaurFront + rules.speed * rules.firstArrivalSeconds;
            while (current.CourseX + rules.Size(current.Kind, current.Variant).x <=
                rules.DinosaurFront + rules.goalDistance - Mathf.Max(rules.finishClearance, rules.speed * (rules.FlightTime + rules.landingBuffer)))
            {
                result.Add(current);
                var next = Pick(random, rules, (DinoObstacleKind)random.Next(4));
                float interval = SampleInterval(random, rules);
                // 값 조정으로 체공 시간이 길어졌을 때 해당 조합에 필요한 간격만 보정합니다.
                next.CourseX = current.CourseX + rules.speed * Mathf.Max(interval, MinimumInterval(rules, current, next));
                current = next;
            }
            return result;
        }

        private static DinoObstacle Pick(System.Random random, DinoRules rules, DinoObstacleKind kind) =>
            new DinoObstacle { Kind = kind, Variant = random.Next(kind == DinoObstacleKind.Cactus ? rules.cactusSizes.Length
                : kind == DinoObstacleKind.Rock ? rules.rockSizes.Length : 1) };

        // 매번 독립 추첨하므로 짧은 간격도 연속해서 나옵니다. 종류 추첨과 별도로 진행합니다.
        public static float SampleInterval(System.Random random, DinoRules rules)
        {
            float weight = (float)random.NextDouble() * (rules.intervalWeights.x + rules.intervalWeights.y + rules.intervalWeights.z);
            Vector2 range = weight < rules.intervalWeights.x ? rules.shortInterval
                : weight < rules.intervalWeights.x + rules.intervalWeights.y ? rules.mediumInterval : rules.longInterval;
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        // 충돌 구간의 중앙이 점프 최고점과 만나도록 한 기준입니다. 실제 실루엣 판정은 Unity 검증으로 확인합니다.
        public static float JumpLead(DinoRules rules, DinoObstacle obstacle) => Mathf.Max(0,
            (rules.FlightTime - (rules.dinosaurSize.x + rules.Size(obstacle.Kind, obstacle.Variant).x) / rules.speed) * 0.5f);

        public static float MinimumInterval(DinoRules rules, DinoObstacle previous, DinoObstacle next)
        {
            if (previous.NeedsJump && next.NeedsJump)
                return rules.FlightTime + rules.landingBuffer + JumpLead(rules, next) - JumpLead(rules, previous);
            float headClearance = Mathf.Max(0, rules.highBirdBottom - rules.dinosaurSize.y);
            float discriminant = Mathf.Max(0, rules.jumpSpeed * rules.jumpSpeed - 2 * rules.gravity * headClearance);
            float rising = (rules.jumpSpeed - Mathf.Sqrt(discriminant)) / rules.gravity;
            float falling = (rules.jumpSpeed + Mathf.Sqrt(discriminant)) / rules.gravity;
            if (previous.NeedsJump) return falling - JumpLead(rules, previous) + rules.landingBuffer;
            if (next.NeedsJump) return (rules.dinosaurSize.x + rules.birdSize.x) / rules.speed
                + JumpLead(rules, next) - rising + rules.landingBuffer;
            return 0;
        }
    }
}
