using System.Linq;
using GE.MiniGames.Cross;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 공통 입력/결과 계약과 HTML의 격자·속도·차선 규칙을 검증하며 기존 씬이나 데이터는 사용하지 않습니다.
    public sealed class CrossTests
    {
        [Test]
        public void SeededLanes_MatchPrototypeRangesAndRestartLayout()
        {
            var first = new CrossModel(42); var second = new CrossModel(42);
            Assert.That(first.PlayerColumn, Is.EqualTo(5)); Assert.That(first.PlayerRow, Is.EqualTo(11));
            for (int row = 1; row <= 10; row++)
            {
                var cars = first.Cars.Where(c => c.Row == row).ToArray();
                Assert.That(cars.Length, Is.InRange(1, 2));
                foreach (var car in cars)
                {
                    Assert.That(car.Speed, Is.InRange(.96f / .01666f, 2.56f / .01666f));
                    Assert.That(car.Direction, Is.EqualTo(cars[0].Direction));
                    Assert.That(car.Speed, Is.EqualTo(cars[0].Speed));
                }
            }
            Assert.That(first.Cars.Select(c => c.X), Is.EqualTo(second.Cars.Select(c => c.X)));
        }

        [Test]
        public void BoundsAndDiagonalInput_CannotLeaveStartingZone()
        {
            var model = new CrossModel(0);
            model.Move(0, 1); model.Move(1, -1);
            Assert.That(model.PlayerRow, Is.EqualTo(11)); Assert.That(model.PlayerColumn, Is.EqualTo(5));
            for (int i = 0; i < 20; i++) model.Move(-1, 0);
            Assert.That(model.PlayerColumn, Is.Zero);
            for (int i = 0; i < 20; i++) model.Move(1, 0);
            Assert.That(model.PlayerColumn, Is.EqualTo(9)); Assert.That(model.Result, Is.EqualTo(CrossStep.Running));
        }

        [Test]
        public void TenthLaneIsNotGoal_UntilSafeZoneEntered()
        {
            var model = new CrossModel(0); ClearColumn(model);
            for (int i = 0; i < 10; i++) model.Move(0, -1);
            Assert.That(model.PlayerRow, Is.EqualTo(1)); Assert.That(model.CrossedLanes, Is.EqualTo(9));
            Assert.That(model.Result, Is.EqualTo(CrossStep.Running));
            Assert.That(model.Move(0, -1), Is.EqualTo(CrossStep.Success));
            var positions = model.Cars.Select(c => c.X).ToArray();
            model.Step(10); model.Move(0, 1);
            Assert.That(model.PlayerRow, Is.Zero); Assert.That(model.CrossedLanes, Is.EqualTo(10));
            Assert.That(model.Cars.Select(c => c.X), Is.EqualTo(positions));
        }

        [Test]
        public void EnteringOccupiedCell_FailsImmediatelyAndFreezes()
        {
            var model = new CrossModel(1); ClearColumn(model);
            model.Cars.First(c => c.Row == 10).X = model.PlayerX;
            Assert.That(model.Move(0, -1), Is.EqualTo(CrossStep.Collision));
            float x = model.Cars[0].X;
            model.Step(1); model.Move(1, 0);
            Assert.That(model.PlayerColumn, Is.EqualTo(5)); Assert.That(model.Cars[0].X, Is.EqualTo(x));
        }

        [Test]
        public void RectangleEdges_OnlyOverlapCountsAsCollision()
        {
            var model = new CrossModel(0); ClearColumn(model); model.Move(0, -1);
            var car = model.Cars.First(c => c.Row == 10);
            car.X = model.PlayerX - CrossModel.CarWidth;
            Assert.That(model.HasCollision(), Is.False);
            car.X += .01f; Assert.That(model.HasCollision(), Is.True);
        }

        [Test]
        public void LongFrame_CannotSkipVehicleCollision()
        {
            var model = new CrossModel(0); ClearColumn(model); model.Move(0, -1);
            var car = model.Cars.First(c => c.Row == 10);
            car.X = car.Direction == 1 ? model.PlayerX - CrossModel.CarWidth - 1 : model.PlayerX + CrossModel.PlayerSize + 1;
            Assert.That(model.Step(1), Is.EqualTo(CrossStep.Collision));
        }

        [Test]
        public void Wrapping_DoesNotSweepAcrossWholeRoad()
        {
            var model = new CrossModel(0); ClearColumn(model); model.Move(0, -1);
            foreach (var car in model.Cars.Where(c => c.Row == 10))
                car.X = car.Direction == 1 ? CrossModel.Width - .01f : -CrossModel.CarWidth + .01f;
            Assert.That(model.Step(.01f), Is.EqualTo(CrossStep.Running));
            foreach (var car in model.Cars.Where(c => c.Row == 10))
                Assert.That(car.X, car.Direction == 1 ? Is.LessThan(0) : Is.GreaterThan(490));
        }

        [Test]
        public void VehicleSpeed_IsIndependentOfFrameRate()
        {
            var fast = new CrossModel(10); var slow = new CrossModel(10);
            // 래핑이 발생하지 않는 구간에서 같은 0.5초의 이동량을 비교합니다.
            foreach (var car in fast.Cars.Concat(slow.Cars)) car.X = 200;
            for (int i = 0; i < 15; i++) slow.Step(1f / 30);
            for (int i = 0; i < 72; i++) fast.Step(1f / 144);
            for (int i = 0; i < fast.Cars.Count; i++) Assert.That(fast.Cars[i].X, Is.EqualTo(slow.Cars[i].X).Within(.01f));
        }

        [Test]
        public void CommonInput_HeldKeysAndPauseDoNotRepeatMovement()
        {
            var owner = new GameObject("Cross input test");
            try
            {
                var game = owner.AddComponent<CrossGame>(); game.KeyboardInputEnabled = false;
                game.StartGame(); ClearColumn(game.Model); game.ProcessFrame(0, GameButtons.None);
                game.ProcessFrame(0, GameButtons.Space | GameButtons.W);
                Assert.That(game.Model.PlayerRow, Is.EqualTo(10));
                game.ProcessFrame(0, GameButtons.Space | GameButtons.W);
                Assert.That(game.Model.PlayerRow, Is.EqualTo(10));
                game.PauseGame(); float x = game.Model.Cars[0].X;
                game.ProcessFrame(1, GameButtons.W);
                Assert.That(game.Model.Cars[0].X, Is.EqualTo(x));
                game.ResumeGame(); game.ProcessFrame(0, GameButtons.W); game.ProcessFrame(0, GameButtons.W);
                Assert.That(game.Model.PlayerRow, Is.EqualTo(10));
                game.ProcessFrame(0, GameButtons.None); game.ProcessFrame(0, GameButtons.W);
                Assert.That(game.Model.PlayerRow, Is.EqualTo(9));
                game.StopGame(); Assert.That(game.Model, Is.Null);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void CommonResults_FailOnceThenRetryAndSucceedOnce()
        {
            var owner = new GameObject("Cross result test");
            try
            {
                var game = owner.AddComponent<CrossGame>(); game.KeyboardInputEnabled = false;
                int results = 0; MiniGameResult last = default;
                game.Completed += result => { results++; last = result; };
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                game.Model.Cars.First(c => c.Row == 10).X = game.Model.PlayerX;
                game.ProcessFrame(0, GameButtons.W); game.ProcessFrame(1, GameButtons.W);
                Assert.That(results, Is.EqualTo(1)); Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Failure));
                game.StartGame(); ClearColumn(game.Model); game.ProcessFrame(0, GameButtons.None);
                Assert.That(game.Progress, Is.Zero); Assert.That(game.RunId, Is.EqualTo(2));
                for (int i = 0; i < 11; i++)
                { game.ProcessFrame(0, GameButtons.W); game.ProcessFrame(0, GameButtons.None); }
                game.ProcessFrame(1, GameButtons.W);
                Assert.That(results, Is.EqualTo(2)); Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success));
                Assert.That(last.GameId, Is.EqualTo("MiniGame05")); Assert.That(last.Progress, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // 테스트만 차량을 왼쪽으로 옮겨 무시간 입력 경로의 충돌 여부를 결정적으로 제어합니다.
        private static void ClearColumn(CrossModel model) { foreach (var car in model.Cars) car.X = 0; }
    }
}
