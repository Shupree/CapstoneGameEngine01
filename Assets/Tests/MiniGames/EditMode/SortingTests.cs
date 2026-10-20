using GE.MiniGames.Sorting;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 기존 공통 입력 경로를 그대로 호출해 규칙·실행당 결과·재시도의 계약을 검증합니다.
    public sealed class SortingTests
    {
        [Test]
        public void ThirtyCorrect_StopsImmediately()
        {
            var model = new SortingModel(30, 8);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(model.Submit(SortingModel.RequiredChoice(model.Current)),
                    Is.EqualTo(i == 29 ? SortingStep.Success : SortingStep.Correct));
                Assert.That(model.Remaining, Is.EqualTo(29 - i));
            }
            Assert.That(model.Submit(SortingChoice.Left), Is.EqualTo(SortingStep.AlreadyFinished));
            Assert.That(model.Correct, Is.EqualTo(30));
        }

        [TestCase(SortingItem.Box, SortingChoice.Left)]
        [TestCase(SortingItem.Bottle, SortingChoice.Right)]
        [TestCase(SortingItem.Stone, SortingChoice.Discard)]
        [TestCase(SortingItem.Leaf, SortingChoice.Discard)]
        [TestCase(SortingItem.Bolt, SortingChoice.Discard)]
        public void AllFiveItems_HaveDefinedKeys(SortingItem item, SortingChoice expected)
        {
            Assert.That(SortingModel.RequiredChoice(item), Is.EqualTo(expected));
        }

        [Test]
        public void GroupProbabilities_AreThirds_AndDummiesAreUniform()
        {
            // 고정 시드의 충분한 표본으로 그룹 확률과 더미 내 조건부 확률을 각각 확인합니다.
            var model = new SortingModel(90000, 123);
            var counts = new int[5];
            while (!model.IsFinished)
            {
                counts[(int)model.Current]++;
                model.Submit(SortingModel.RequiredChoice(model.Current));
            }
            Assert.That(counts[0], Is.InRange(29000, 31000));
            Assert.That(counts[1], Is.InRange(29000, 31000));
            for (int i = 2; i < 5; i++) Assert.That(counts[i], Is.InRange(9500, 10500));
        }

        [Test]
        public void HeldAndSimultaneousKeys_DoNotConsumeExtraObjects()
        {
            var owner = new GameObject("Sorting input test");
            try
            {
                var game = owner.AddComponent<SortingGame>();
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                game.ProcessFrame(0, GameButtons.A | GameButtons.D | GameButtons.Space);
                Assert.That(game.Progress, Is.Zero);
                game.ProcessFrame(0, GameButtons.None);
                var key = KeyFor(game.Model.Current);
                game.ProcessFrame(0, key);
                for (int i = 0; i < 60; i++) game.ProcessFrame(0.1f, key);
                Assert.That(game.Progress, Is.EqualTo(1));
                Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void PauseResume_BlocksQueuedInput_AndKeepsCurrentObject()
        {
            var owner = new GameObject("Sorting pause test");
            try
            {
                var game = owner.AddComponent<SortingGame>();
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                var current = game.Model.Current;
                var key = KeyFor(current);
                game.ProcessFrame(0, GameButtons.Tab);
                game.ProcessFrame(1, GameButtons.Tab | key);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
                game.ProcessFrame(0, key);
                game.ProcessFrame(0, GameButtons.Tab | key);
                game.ProcessFrame(1, key);
                game.ProcessFrame(1, key);
                Assert.That(game.Progress, Is.Zero);
                Assert.That(game.Model.Current, Is.EqualTo(current));
                game.ProcessFrame(0, GameButtons.None);
                game.ProcessFrame(0, key);
                Assert.That(game.Progress, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void FailureAndRetry_ResetThirty_ThenSuccessNotifiesOnce_AndStopCleansModel()
        {
            var owner = new GameObject("Sorting lifecycle test");
            try
            {
                var game = owner.AddComponent<SortingGame>();
                int results = 0;
                game.Completed += result => results++;
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                for (int i = 0; i < 4; i++)
                { game.ProcessFrame(0, KeyFor(game.Model.Current)); game.ProcessFrame(0, GameButtons.None); }
                var wrong = KeyFor(game.Model.Current) == GameButtons.A ? GameButtons.D : GameButtons.A;
                game.ProcessFrame(0, wrong);
                Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
                for (int i = 0; i < 5; i++) game.ProcessFrame(0, wrong);
                Assert.That(results, Is.EqualTo(1));
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                Assert.That(game.Progress, Is.Zero);
                Assert.That(game.Model.Remaining, Is.EqualTo(30));
                Assert.That(game.RunId, Is.EqualTo(2));
                for (int i = 0; i < 30; i++)
                { game.ProcessFrame(0, KeyFor(game.Model.Current)); game.ProcessFrame(0, GameButtons.None); }
                Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded));
                Assert.That(results, Is.EqualTo(2));
                game.StopGame();
                Assert.That(game.Model, Is.Null);
                Assert.That(results, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static GameButtons KeyFor(SortingItem item)
        {
            var choice = SortingModel.RequiredChoice(item);
            return choice == SortingChoice.Left ? GameButtons.A : choice == SortingChoice.Right ? GameButtons.D : GameButtons.Space;
        }
    }
}
