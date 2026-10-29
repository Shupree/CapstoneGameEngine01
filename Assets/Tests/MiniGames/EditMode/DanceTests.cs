using System;
using System.Linq;
using GE.MiniGames.Dance;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // HTML의 난이도 표와 공통 상태 연결을 검사하며 다른 게임/메인 시스템을 생성하지 않습니다.
    public sealed class DanceTests
    {
        [Test]
        public void AllRounds_UseExpectedLengthAndTime_AndWinAtTen()
        {
            var model = new DanceModel(1);
            int[] lengths = { 4, 4, 4, 6, 6, 6, 8, 8, 8, 10 };
            for (int round = 1; round <= 10; round++)
            {
                Assert.That(model.Round, Is.EqualTo(round)); Assert.That(model.Sequence.Count, Is.EqualTo(lengths[round - 1]));
                Assert.That(model.TimeLeft, Is.EqualTo(lengths[round - 1] * 1.2).Within(.000001));
                foreach (var key in model.Sequence) model.Input(key);
                Assert.That(model.CompletedRounds, Is.EqualTo(round));
                if (round < 10) { Assert.That(model.IsTransitioning, Is.True); model.Step(.4); }
            }
            Assert.That(model.Result, Is.EqualTo(DanceStep.Success));
            model.Input(DanceDirection.Down); model.Step(100);
            Assert.That(model.CompletedRounds, Is.EqualTo(10)); Assert.That(model.Result, Is.EqualTo(DanceStep.Success));
        }
        [Test]
        public void CompletedLine_LocksExtraInput_AndResetsTimerOnlyAfterDelay()
        {
            var model = new DanceModel(2); model.Step(1);
            foreach (var key in model.Sequence) model.Input(key);
            double time = model.TimeLeft;
            model.Input((DanceDirection)(((int)model.Sequence[0] + 1) % 4)); model.Step(.399);
            Assert.That(model.Result, Is.EqualTo(DanceStep.Running)); Assert.That(model.Round, Is.EqualTo(1));
            Assert.That(model.CompletedRounds, Is.EqualTo(1)); Assert.That(model.TimeLeft, Is.EqualTo(time));
            model.Step(.002);
            Assert.That(model.Round, Is.EqualTo(2)); Assert.That(model.InputIndex, Is.Zero); Assert.That(model.TimeLeft, Is.EqualTo(4.8));
        }
        [Test]
        public void WrongInput_FailsAndCannotBeOverwritten()
        {
            var model = new DanceModel(3);
            model.Input((DanceDirection)(((int)model.Sequence[0] + 1) % 4));
            model.Input(model.Sequence[0]); model.Step(100);
            Assert.That(model.Result, Is.EqualTo(DanceStep.WrongKey)); Assert.That(model.InputIndex, Is.Zero);
            Assert.That(model.CompletedRounds, Is.Zero);
        }
        [Test]
        public void Timeout_BoundaryAndNewAttempt()
        {
            var model = new DanceModel(4); model.Input(model.Sequence[0]); model.Step(4.799);
            Assert.That(model.Result, Is.EqualTo(DanceStep.Running)); model.Step(.002);
            Assert.That(model.TimeLeft, Is.Zero); Assert.That(model.Result, Is.EqualTo(DanceStep.Timeout));
            model.Input(model.Sequence[1]); Assert.That(model.InputIndex, Is.EqualTo(1));
            var retry = new DanceModel(4);
            Assert.That(retry.Round, Is.EqualTo(1)); Assert.That(retry.CompletedRounds, Is.Zero);
            Assert.That(retry.InputIndex, Is.Zero); Assert.That(retry.TimeLeft, Is.EqualTo(4.8)); Assert.That(retry.IsTransitioning, Is.False);
        }
        [Test]
        public void RandomSequence_IsRepeatable_AndAllowsRepeatedDirections()
        {
            bool repeated = false; bool varied = false;
            var first = new DanceModel(0).Sequence;
            for (int seed = 0; seed < 64; seed++)
            {
                var model = new DanceModel(seed);
                Assert.That(model.Sequence, Is.EqualTo(new DanceModel(seed).Sequence));
                Assert.That(model.Sequence.All(x => (int)x >= 0 && (int)x < 4), Is.True);
                repeated |= model.Sequence.Zip(model.Sequence.Skip(1), (a, b) => a == b).Any(x => x);
                varied |= !model.Sequence.SequenceEqual(first);
            }
            Assert.That(repeated && varied, Is.True);
        }
        [Test]
        public void InvalidTimeAndRound_AreRejected()
        {
            var model = new DanceModel(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => DanceModel.SequenceLength(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DanceModel.SequenceLength(11));
        }
        [Test]
        public void Controller_PauseFreezesRoundDelay_AndRetryUsesCommonFailure()
        {
            var root = new GameObject("Dance lifecycle test");
            try
            {
                var game = root.AddComponent<DanceGame>(); game.KeyboardInputEnabled = false;
                int completed = 0; game.Completed += _ => completed++;
                game.StartGame(); foreach (var key in game.Model.Sequence) game.Model.Input(key);
                game.PauseGame(); for (int i = 0; i < 60; i++) game.ProcessFrame(.1f, GameButtons.None);
                Assert.That(game.Model.Round, Is.EqualTo(1)); Assert.That(game.Model.IsTransitioning, Is.True);
                game.ResumeGame(); for (int i = 0; i < 6; i++) game.ProcessFrame(.1f, GameButtons.None);
                Assert.That(game.Model.Round, Is.EqualTo(2));
                for (int i = 0; i < 60; i++) game.ProcessFrame(.1f, GameButtons.None);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(completed, Is.EqualTo(1));
                game.StartGame(); Assert.That(game.Model.Round, Is.EqualTo(1)); Assert.That(game.Progress, Is.Zero);
                game.StopGame(); Assert.That(game.Model, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}