using GE.MiniGames.FlappyBird;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 전체 진행 시스템을 생성하지 않고 실제 플래피 모델과 공통 입력 경로만 검증합니다.
    public sealed class FlappyTests
    {
        [Test]
        public void HeldSpace_OnlyFlapsOnPressEdge()
        {
            var owner = new GameObject("Flappy input test");
            try
            {
                var game = owner.AddComponent<FlappyGame>();
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                game.ProcessFrame(0.1f, GameButtons.Space);
                float firstVelocity = game.Model.Velocity;
                game.ProcessFrame(0.1f, GameButtons.Space);
                Assert.That(game.Model.Velocity, Is.LessThan(firstVelocity));
                game.ProcessFrame(0f, GameButtons.None);
                game.ProcessFrame(0f, GameButtons.Space);
                Assert.That(game.Model.Velocity, Is.EqualTo(game.Model.Rules.flapSpeed));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void PauseAndDisable_PreserveProgressAndIgnoreHeldInput()
        {
            var owner = new GameObject("Flappy pause test");
            try
            {
                var game = owner.AddComponent<FlappyGame>();
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                game.ProcessFrame(0.1f, GameButtons.Space);
                game.PauseGame(); float y = game.Model.BirdY;
                game.ProcessFrame(1f, GameButtons.Space);
                Assert.That(game.Model.BirdY, Is.EqualTo(y));
                owner.SetActive(false); owner.SetActive(true);
                game.ResumeGame(); game.ProcessFrame(0, GameButtons.Space);
                float velocity = game.Model.Velocity;
                game.ProcessFrame(0.1f, GameButtons.Space);
                Assert.That(game.Model.Velocity, Is.LessThan(velocity));
                game.ResetGame(); Assert.That(game.Progress, Is.Zero);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
                game.StopGame(); Assert.That(game.Model, Is.Null);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothScreenBoundaries_Fail(bool flap)
        {
            var model = new FlappyModel(new FlappyRules { randomSeed = 2 });
            for (int i = 0; i < 1000 && model.Result == FlappyStep.Running; i++)
            { if (flap) model.Flap(); model.Step(0.01f); }
            Assert.That(model.Result, Is.EqualTo(FlappyStep.BoundaryCollision));
            Assert.That(model.Passed, Is.Zero);
        }

        [TestCase(-2.8f)]
        [TestCase(2.8f)]
        public void EitherPipe_FailsWithoutAwardingPoint(float gap)
        {
            var model = new FlappyModel(new FlappyRules());
            model.Pipes[0].X = FlappyRules.BirdX;
            model.Pipes[0].GapY = gap;
            model.Step(0.01f);
            Assert.That(model.Result, Is.EqualTo(FlappyStep.PipeCollision));
            Assert.That(model.Passed, Is.Zero);
        }

        [Test]
        public void TrailingEdge_MustClearEntireBird_AndScoresOnlyOnce()
        {
            var model = new FlappyModel(new FlappyRules());
            model.Pipes[0].X = FlappyRules.BirdX - FlappyRules.BirdHalfWidth - model.Rules.pipeWidth / 2f + 0.05f;
            model.Step(0.001f); Assert.That(model.Passed, Is.Zero);
            model.Step(0.03f); Assert.That(model.Passed, Is.EqualTo(1));
            model.Step(0.1f); Assert.That(model.Passed, Is.EqualTo(1));
            Assert.That(model.Pipes[0].Counted, Is.True);
        }

        [Test]
        public void LongFrame_CannotTunnelThroughPipe()
        {
            var model = new FlappyModel(new FlappyRules { pipeSpeed = 6 });
            model.Pipes[0].X = FlappyRules.BirdX + 1f;
            model.Pipes[0].GapY = -2.8f;
            Assert.That(model.Step(0.4f), Is.EqualTo(FlappyStep.PipeCollision));
        }

        [Test]
        public void FifteenthPair_SendsSuccessExactlyOnce()
        {
            var owner = new GameObject("Flappy success test");
            try
            {
                var game = owner.AddComponent<FlappyGame>();
                int completions = 0;
                game.Completed += result => { completions++; Assert.That(result.Outcome, Is.EqualTo(MiniGameOutcome.Success)); };
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                for (int i = 0; i < 15; i++)
                {
                    game.Model.Pipes[i].X = -5f;
                    game.ProcessFrame(0.001f, GameButtons.None);
                    Assert.That(game.Progress, Is.EqualTo(i + 1));
                    Assert.That(game.State, Is.EqualTo(i == 14 ? MiniGameState.Succeeded : MiniGameState.Playing));
                }
                game.ProcessFrame(1f, GameButtons.Space);
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(game.Progress, Is.EqualTo(15));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void Failure_SendsOnceAndRetryRequiresRelease()
        {
            var owner = new GameObject("Flappy failure test");
            try
            {
                var game = owner.AddComponent<FlappyGame>();
                var view = owner.AddComponent<FlappyView>();
                view.Configure(game, null, null, null, null, null, null, null, null, null, null);
                int completions = 0;
                game.Completed += result => { completions++; Assert.That(result.Outcome, Is.EqualTo(MiniGameOutcome.Failure)); };
                view.ProcessStartInput(false); view.ProcessStartInput(true);
                game.ProcessFrame(0, GameButtons.None);
                for (int i = 0; i < 50; i++) game.ProcessFrame(0.1f, GameButtons.None);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
                Assert.That(completions, Is.EqualTo(1));
                view.ProcessStartInput(true); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
                view.ProcessStartInput(false); view.ProcessStartInput(true);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
                Assert.That(game.Progress, Is.Zero);
                Assert.That(game.RunId, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(0)]
        [TestCase(17)]
        [TestCase(42)]
        public void DefaultBalance_AllFifteenPairsAreReachable(int seed)
        {
            var model = new FlappyModel(new FlappyRules { randomSeed = seed });
            // 간단한 자동 조작으로 모든 기본 배치의 도달 가능성을 검사합니다. 실제 난이도 평가는 별도입니다.
            for (int frame = 0; frame < 6000 && model.Result == FlappyStep.Running; frame++)
            {
                float goal = 0;
                foreach (var pipe in model.Pipes)
                    if (!pipe.Counted) { goal = pipe.GapY; break; }
                if (model.BirdY < goal - 0.25f && model.Velocity <= 0) model.Flap();
                model.Step(1f / 60f);
            }
            Assert.That(model.Result, Is.EqualTo(FlappyStep.Success));
            Assert.That(model.Passed, Is.EqualTo(15));
        }
    }
}
