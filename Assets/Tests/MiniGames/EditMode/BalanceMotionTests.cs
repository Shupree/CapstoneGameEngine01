using System.Reflection;
using GE.MiniGames.Rope;
using GE.MiniGames.Sorting;
using GE.MiniGames.FlappyBird;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 낙하 중 성공 방지와 이동 중 정지·중복 입력, 보정된 날갯짓을 실제 모델/컨트롤러로 확인합니다.
    public sealed class BalanceMotionTests
    {
        [Test]
        public void Rope_FallsBeforeFailing_AndCannotWinDuringFall()
        {
            var model = new RopeModel(1);
            typeof(RopeModel).GetProperty("BallPosition").SetValue(model,101d);
            typeof(RopeModel).GetField("ticks",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(model,299);
            model.Step(1d/60,0); Assert.That(model.Falling,Is.True);
            double y=model.FallY; model.Step(.2,0);
            Assert.That(model.Result,Is.EqualTo(RopeStep.Running)); Assert.That(model.FallY,Is.LessThan(y));
            model.Step(1,0); Assert.That(model.Result,Is.EqualTo(RopeStep.BallDropped));
        }
        [TestCase(SortingChoice.Left)] [TestCase(SortingChoice.Right)] [TestCase(SortingChoice.Discard)]
        public void Sorting_MovesChosenDirection_AndPauseFreezesPendingJudgment(SortingChoice choice)
        {
            var owner=new GameObject("Sorting motion test");
            try
            {
                var game=owner.AddComponent<SortingGame>(); game.KeyboardInputEnabled=false;
                game.StartGame(); game.ProcessFrame(0,GameButtons.None);
                var key=choice==SortingChoice.Left ? GameButtons.A : choice==SortingChoice.Right ? GameButtons.D : GameButtons.Space;
                bool correct=SortingModel.RequiredChoice(game.Model.Current)==choice;
                game.ProcessFrame(.05f,key); Assert.That(game.MovingChoice,Is.EqualTo(choice));
                Assert.That(game.Progress,Is.Zero); Assert.That(game.SortProgress,Is.GreaterThan(0));
                game.PauseGame(); float fraction=game.SortProgress; game.ProcessFrame(.1f,GameButtons.Space);
                Assert.That(game.SortProgress,Is.EqualTo(fraction));
                game.ResumeGame(); game.ProcessFrame(0,GameButtons.None);
                game.ProcessFrame(.1f,GameButtons.None); game.ProcessFrame(.1f,GameButtons.None);
                Assert.That(game.Progress,Is.EqualTo(correct?1:0));
                Assert.That(game.State,Is.EqualTo(correct?MiniGameState.Playing:MiniGameState.Failed));
                game.ResetGame(); Assert.That(game.SortProgress,Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test]
        public void Flappy_UsesEffectiveImpulseAndMovementWithoutCompounding()
        {
            var rules=new FlappyRules(); var model=new FlappyModel(rules);
            Assert.That(model.Velocity,Is.EqualTo(4.8f*1.15f).Within(.0001));
            Assert.That(model.Rules.Gravity,Is.EqualTo(18.2f*.9f).Within(.0001));
            float x=model.Pipes[0].X; model.Step(.1f);
            Assert.That(x-model.Pipes[0].X,Is.EqualTo(3.64f*1.2f*.1f).Within(.0001));
            model.Flap(); Assert.That(model.Velocity,Is.EqualTo(new FlappyModel(model.Rules).Velocity));
        }
    }
}
