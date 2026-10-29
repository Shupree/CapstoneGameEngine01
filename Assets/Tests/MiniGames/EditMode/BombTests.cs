using System;
using System.Linq;
using GE.MiniGames.Bomb;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // HTML 규칙과 공통 수명 계약의 경계만 검사합니다. 메인게임이나 다른 미니게임을 실행하지 않습니다.
    public sealed class BombTests
    {
        [Test]
        public void Shuffle_PermutationsAreIndependentAndRepeatableBySeed()
        {
            bool different = false;
            for (int seed = 0; seed < 64; seed++)
            {
                var model = new BombModel(seed); var same = new BombModel(seed);
                Assert.That(model.Wires.Distinct().Count(), Is.EqualTo(4));
                Assert.That(model.Sequence.Distinct().Count(), Is.EqualTo(4));
                Assert.That(same.Wires, Is.EqualTo(model.Wires)); Assert.That(same.Sequence, Is.EqualTo(model.Sequence));
                different |= !model.Wires.SequenceEqual(model.Sequence);
            }
            Assert.That(different, Is.True);
        }
        [Test]
        public void Selection_ClampsAndCutWiresRemainSelectable()
        {
            var model = new BombModel(7);
            model.Move(-1); Assert.That(model.SelectedIndex, Is.Zero);
            for (int i = 0; i < 8; i++) model.Move(1);
            Assert.That(model.SelectedIndex, Is.EqualTo(3));
            Select(model, model.Sequence[0]); model.Cut();
            model.Move(-1); model.Move(1);
            Assert.That(model.SelectedIndex, Is.InRange(0, 3));
        }
        [Test]
        public void CorrectSequence_WinsExactlyOnce_AndFreezesAfterFinish()
        {
            var model = new BombModel(9);
            foreach (var color in model.Sequence) { Select(model, color); model.Cut(); }
            Assert.That(model.Result, Is.EqualTo(BombStep.Success)); Assert.That(model.CutCount, Is.EqualTo(4));
            double time = model.TimeLeft; int selected = model.SelectedIndex;
            model.Step(10); model.Cut(); model.Move(-1);
            Assert.That(model.Result, Is.EqualTo(BombStep.Success)); Assert.That(model.TimeLeft, Is.EqualTo(time));
            Assert.That(model.CutCount, Is.EqualTo(4)); Assert.That(model.SelectedIndex, Is.EqualTo(selected));
        }
        [Test]
        public void Recut_IsIgnored_AndWrongWireFailsImmediately()
        {
            var model = new BombModel(12);
            Select(model, model.Sequence[0]); model.Cut(); model.Cut();
            Assert.That(model.CutCount, Is.EqualTo(1)); Assert.That(model.Result, Is.EqualTo(BombStep.Running));
            Select(model, model.Sequence[2]); model.Cut();
            Assert.That(model.Result, Is.EqualTo(BombStep.WrongWire)); Assert.That(model.CutCount, Is.EqualTo(1));
        }
        [Test]
        public void Deadline_ExpiresAtLimit_AndCannotBeRescuedByLaterCut()
        {
            var model = new BombModel(1); model.Step(4.699);
            Assert.That(model.Result, Is.EqualTo(BombStep.Running)); model.Step(.002);
            Assert.That(model.Result, Is.EqualTo(BombStep.Timeout)); Assert.That(model.TimeLeft, Is.Zero);
            Select(model, model.Sequence[0]); model.Cut(); Assert.That(model.CutCount, Is.Zero);
        }
        [Test]
        public void InvalidDelta_IsRejected_AndFreshAttemptRestoresEverything()
        {
            var model = new BombModel(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Step(double.PositiveInfinity));
            Select(model, model.Sequence[0]); model.Cut(); model.Step(1);
            var retry = new BombModel(2);
            Assert.That(retry.CutCount, Is.Zero); Assert.That(retry.SelectedIndex, Is.Zero);
            Assert.That(retry.TimeLeft, Is.EqualTo(4.7)); Assert.That(retry.CutWires.All(x => !x), Is.True);
        }
        [Test]
        public void Controller_PauseFreezesTimer_AndRetryUsesExistingContract()
        {
            var root = new GameObject("Bomb lifecycle test");
            try
            {
                var game = root.AddComponent<BombGame>(); int completed = 0; game.Completed += _ => completed++;
                game.KeyboardInputEnabled = false; game.StartGame(); game.ProcessFrame(.1f, GameButtons.None);
                game.PauseGame(); double time = game.Model.TimeLeft;
                for (int i = 0; i < 60; i++) game.ProcessFrame(.1f, GameButtons.Space);
                Assert.That(game.Model.TimeLeft, Is.EqualTo(time)); Assert.That(game.Progress, Is.Zero);
                game.ResumeGame(); game.ProcessFrame(.1f, GameButtons.None);
                for (int i = 0; i < 60; i++) game.ProcessFrame(.1f, GameButtons.None);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Assert.That(completed, Is.EqualTo(1));
                Assert.That(game.ResultReason, Is.EqualTo("시간 초과!"));
                game.StartGame(); Assert.That(game.RunId, Is.EqualTo(2)); Assert.That(game.Model.TimeLeft, Is.EqualTo(4.7));
                game.StopGame(); Assert.That(game.Model, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // 선택 이동도 공개 모델 API를 사용해 배열 내부를 변경하지 않습니다.
        private static void Select(BombModel model, BombColor color)
        {
            int target = model.Wires.ToList().IndexOf(color);
            for (int i = 0; i < 4 && model.SelectedIndex != target; i++) model.Move(Math.Sign(target - model.SelectedIndex));
        }
    }
}