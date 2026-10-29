using System;
using System.Linq;
using GE.MiniGames.Dino;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 시간 간격과 종류의 추첨을 물리/프레임률에 독립적으로 검증합니다.
    public sealed class DinoCourseTests
    {
        [Test]
        public void Defaults_MatchReferenceJumpAndDuration()
        {
            var rules = new DinoRules();
            Assert.That(rules.jumpSpeed * rules.jumpSpeed / (2 * rules.gravity), Is.EqualTo(100));
            Assert.That(rules.FlightTime, Is.EqualTo(2f / 3).Within(.00001));
            Assert.That(rules.goalDistance / rules.speed, Is.EqualTo(26.75585f).Within(.001));
            Assert.That(rules.GroundY, Is.EqualTo(64));
        }

        [Test]
        public void Intervals_RespectMixtureAndAllowShortStreaks()
        {
            var random = new System.Random(104);
            var rules = new DinoRules();
            int[] counts = new int[3]; int consecutive = 0; bool previousShort = false;
            for (int i = 0; i < 20000; i++)
            {
                float interval = DinoCourse.SampleInterval(random, rules);
                int bucket = interval <= 1.05f ? 0 : interval <= 1.65f ? 1 : 2;
                Vector2 range = bucket == 0 ? rules.shortInterval : bucket == 1 ? rules.mediumInterval : rules.longInterval;
                Assert.That(interval, Is.InRange(range.x, range.y));
                counts[bucket]++; if (previousShort && bucket == 0) consecutive++; previousShort = bucket == 0;
            }
            Assert.That(counts[0] / 20000f, Is.EqualTo(.4f).Within(.02));
            Assert.That(counts[1] / 20000f, Is.EqualTo(.4f).Within(.02));
            Assert.That(counts[2] / 20000f, Is.EqualTo(.2f).Within(.02));
            Assert.That(consecutive, Is.GreaterThan(2000));
        }

        [Test]
        public void Course_UsesFrontEdgesUniformKindsAndFinishClearance()
        {
            int[] kinds = new int[4], cactus = new int[3], rocks = new int[2];
            for (int seed = 0; seed < 400; seed++)
            {
                var rules = new DinoRules { randomSeed = seed };
                var course = DinoCourse.Build(rules);
                Assert.That(course[0].Kind, Is.EqualTo(DinoObstacleKind.Cactus));
                Assert.That((course[0].CourseX - rules.DinosaurFront) / rules.speed, Is.EqualTo(2.5f).Within(.00001));
                for (int i = 1; i < course.Count; i++)
                {
                    var obstacle = course[i]; kinds[(int)obstacle.Kind]++;
                    if (obstacle.Kind == DinoObstacleKind.Cactus) cactus[obstacle.Variant]++;
                    if (obstacle.Kind == DinoObstacleKind.Rock) rocks[obstacle.Variant]++;
                    float interval = (obstacle.CourseX - course[i-1].CourseX) / rules.speed;
                    Assert.That(interval, Is.InRange(.84999f, 2.40001f));
                    Assert.That(obstacle.CourseX + rules.Size(obstacle.Kind, obstacle.Variant).x,
                        Is.LessThanOrEqualTo(rules.DinosaurFront + rules.goalDistance - rules.finishClearance));
                }
            }
            foreach (int count in kinds) Assert.That((float)count / kinds.Sum(), Is.EqualTo(.25f).Within(.025));
            foreach (int count in cactus) Assert.That((float)count / cactus.Sum(), Is.EqualTo(1f/3).Within(.04));
            foreach (int count in rocks) Assert.That((float)count / rocks.Sum(), Is.EqualTo(.5f).Within(.04));
        }
    }
}
