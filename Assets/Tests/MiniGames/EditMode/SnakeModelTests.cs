using System;
using System.Collections.Generic;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    public sealed class SnakeModelTests
    {
        [Test]
        public void DefaultBoardStartsCenteredFacingRightWithAnUnoccupiedApple()
        {
            var model = new SnakeModel(10, 10, 5, seed: 7);

            Assert.That(model.Body.Count, Is.EqualTo(3));
            Assert.That(model.Body[0], Is.EqualTo(new Vector2Int(5, 5)));
            Assert.That(model.Body[1], Is.EqualTo(new Vector2Int(4, 5)));
            Assert.That(model.Body[2], Is.EqualTo(new Vector2Int(3, 5)));
            Assert.That(model.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(model.ApplesEaten, Is.Zero);
            Assert.That(model.IsFinished, Is.False);
            AssertAppleIsFree(model);
        }

        [Test]
        public void InvalidConfigurationIsRejectedAndLongStartingSnakeIsClamped()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(1, 10, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(10, 1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(int.MaxValue, 2, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(10, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(10, 10, 98));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeModel(10, 10, 5, 0));
            var clamped = new SnakeModel(4, 4, 5, 99, 7);
            Assert.That(clamped.Body.Count, Is.EqualTo(3));
            Assert.That(clamped.Body[0].x, Is.EqualTo(2));
        }

        [Test]
        public void OnlyOneTurnCanBeQueuedPerTickAndFastInputsCannotReverse()
        {
            var model = new SnakeModel(10, 10, 5, seed: 7);
            Assert.That(model.QueueDirection(Vector2Int.left), Is.False);
            Assert.That(model.QueueDirection(Vector2Int.right), Is.False);
            Assert.That(model.QueueDirection(Vector2Int.zero), Is.False);
            Assert.That(model.QueueDirection(new Vector2Int(1, 1)), Is.False);
            Assert.That(model.QueueDirection(Vector2Int.up), Is.True);
            Assert.That(model.QueueDirection(Vector2Int.left), Is.False);
            Assert.That(model.QueueDirection(Vector2Int.down), Is.False);

            model.Step();
            Assert.That(model.Body[0], Is.EqualTo(new Vector2Int(5, 6)));
            Assert.That(model.Direction, Is.EqualTo(Vector2Int.up));
            Assert.That(model.QueueDirection(Vector2Int.left), Is.True);
        }

        [Test]
        public void ClearPendingInputDiscardsAnUnexecutedTurn()
        {
            var model = new SnakeModel(10, 10, 5, seed: 7);
            model.QueueDirection(Vector2Int.up);
            model.ClearPendingInput();
            model.Step();
            Assert.That(model.Body[0], Is.EqualTo(new Vector2Int(6, 5)));
            Assert.That(model.Direction, Is.EqualTo(Vector2Int.right));
        }

        [Test]
        public void WallHitEndsImmediatelyAndFurtherStepsDoNothing()
        {
            var model = new SnakeModel(10, 10, 5, seed: 7);
            for (int i = 0; i < 4; i++)
                Assert.That(model.Step(), Is.Not.EqualTo(SnakeStepResult.HitWall));
            var before = new List<Vector2Int>(model.Body);

            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.HitWall));
            Assert.That(model.IsFinished, Is.True);
            Assert.That(model.Body, Is.EqualTo(before));
            Assert.That(model.QueueDirection(Vector2Int.up), Is.False);
            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.AlreadyFinished));
            Assert.That(model.Body, Is.EqualTo(before));
        }

        [Test]
        public void SelfCollisionEndsWithoutMovingTheBody()
        {
            var model = FindModel(5, m => m.Apple != new Vector2Int(5, 6) &&
                m.Apple != new Vector2Int(4, 6) && m.Apple != new Vector2Int(4, 5));
            TurnAndStep(model, Vector2Int.up);
            TurnAndStep(model, Vector2Int.left);
            var before = new List<Vector2Int>(model.Body);

            model.QueueDirection(Vector2Int.down);
            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.HitBody));
            Assert.That(model.IsFinished, Is.True);
            Assert.That(model.Body, Is.EqualTo(before));
        }

        [Test]
        public void VacatingTailCanBeEnteredOnANonGrowthTick()
        {
            var model = FindModel(4, m => m.Apple != new Vector2Int(5, 6) &&
                m.Apple != new Vector2Int(4, 6) && m.Apple != new Vector2Int(4, 5));
            TurnAndStep(model, Vector2Int.up);
            TurnAndStep(model, Vector2Int.left);
            Vector2Int previousTail = model.Body[model.Body.Count - 1];

            model.QueueDirection(Vector2Int.down);
            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.Moved));
            Assert.That(model.Body[0], Is.EqualTo(previousTail));
            Assert.That(model.IsFinished, Is.False);
        }

        [Test]
        public void AppleAddsOneSegmentAndExactlyOnePoint()
        {
            var model = FindModel(3, m => m.Apple == m.Body[0] + Vector2Int.right);
            Vector2Int previousTail = model.Body[model.Body.Count - 1];

            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.AteApple));
            Assert.That(model.Body.Count, Is.EqualTo(4));
            Assert.That(model.Body[3], Is.EqualTo(previousTail));
            Assert.That(model.ApplesEaten, Is.EqualTo(1));
            Assert.That(model.IsFinished, Is.False);
            AssertAppleIsFree(model);
        }

        [Test]
        public void ResetClearsGrowthFinishAndPendingInputAndRecreatesSeededStart()
        {
            var model = FindModel(3, m => m.Apple == m.Body[0] + Vector2Int.right);
            Vector2Int initialApple = model.Apple;
            var initialBody = new List<Vector2Int>(model.Body);
            model.Step();
            while (!model.IsFinished)
                model.Step();
            model.Reset();
            model.QueueDirection(Vector2Int.up);
            model.Reset();

            Assert.That(model.Body, Is.EqualTo(initialBody));
            Assert.That(model.ApplesEaten, Is.Zero);
            Assert.That(model.IsFinished, Is.False);
            Assert.That(model.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(model.Apple, Is.EqualTo(initialApple));
            Assert.That(model.HasApple, Is.True);
            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.AteApple));
            Assert.That(model.Body[0], Is.EqualTo(new Vector2Int(6, 5)));
        }

        [TestCase(5)]
        [TestCase(15)]
        public void SafeCycleReachesGoalAndHandlesCompletelyFullBoard(int target)
        {
            // A Hamiltonian cycle visits every cell once. Following it guarantees
            // a legal solution even when all 16 cells eventually contain the body.
            var cycle = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0),
                new Vector2Int(3, 1), new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(1, 2),
                new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(3, 3), new Vector2Int(2, 3),
                new Vector2Int(1, 3), new Vector2Int(0, 3), new Vector2Int(0, 2), new Vector2Int(0, 1)
            };

            for (int seed = 0; seed < 32; seed++)
            {
                var model = new SnakeModel(4, 4, target, 1, seed);
                int index = Array.IndexOf(cycle, model.Body[0]);
                SnakeStepResult result = SnakeStepResult.Moved;
                for (int step = 0; step < cycle.Length * target && !model.IsFinished; step++)
                {
                    AssertAppleIsFree(model);
                    int next = (index + 1) % cycle.Length;
                    model.QueueDirection(cycle[next] - cycle[index]);
                    result = model.Step();
                    Assert.That(result, Is.Not.EqualTo(SnakeStepResult.HitWall));
                    Assert.That(result, Is.Not.EqualTo(SnakeStepResult.HitBody));
                    index = next;
                }

                Assert.That(result, Is.EqualTo(SnakeStepResult.Won), $"Seed {seed}");
                Assert.That(model.IsFinished, Is.True);
                Assert.That(model.ApplesEaten, Is.EqualTo(target));
                Assert.That(model.Body.Count, Is.EqualTo(1 + target));
                Assert.That(model.HasApple, Is.False, "A win must not spawn another apple.");
                var body = new List<Vector2Int>(model.Body);
                Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.AlreadyFinished));
                Assert.That(model.Body, Is.EqualTo(body));
            }
        }

        [Test]
        public void ApplesNeverSpawnOnTheInitialBodyAcrossSeeds()
        {
            for (int seed = 0; seed < 256; seed++)
                AssertAppleIsFree(new SnakeModel(10, 10, 5, seed: seed));
        }

        private static SnakeModel FindModel(int length, Predicate<SnakeModel> matches)
        {
            // Find a reproducible initial apple without any mutation-only test API.
            for (int seed = 0; seed < 10000; seed++)
            {
                var model = new SnakeModel(10, 10, 5, length, seed);
                if (matches(model))
                    return model;
            }
            Assert.Fail("No matching deterministic seed was found.");
            return null;
        }

        private static void TurnAndStep(SnakeModel model, Vector2Int direction)
        {
            Assert.That(model.QueueDirection(direction), Is.True);
            Assert.That(model.Step(), Is.EqualTo(SnakeStepResult.Moved));
        }

        private static void AssertAppleIsFree(SnakeModel model)
        {
            Assert.That(model.HasApple, Is.True);
            Assert.That(model.Apple.x, Is.InRange(0, model.Width - 1));
            Assert.That(model.Apple.y, Is.InRange(0, model.Height - 1));
            Assert.That(model.Body, Has.No.Member(model.Apple));
        }
    }
}
