using System.Reflection;
using GE.MiniGames.Rope;
using GE.MiniGames.Fly;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 경계 배치만 테스트에서 주입해 성공·실패와 HTML 수치의 시간/판정 계약을 검사합니다.
    [Category("HtmlMiniGames")]
    public sealed class RopeFlyTests
    {
        private static void Set(object model, string property, object value)
        { model.GetType().GetProperty(property).SetValue(model, value); }
        [Test]
        public void Rope_FixedStepMatchesAcrossFrameRates()
        {
            var a = new RopeModel(23); var b = new RopeModel(23);
            Assert.That(System.Math.Abs(a.Angle), Is.InRange(10, 15));
            Assert.That(a.BallPosition, Is.InRange(-20, 20));
            for (int i = 0; i < 60; i++) a.Step(1.0 / 60, -1);
            for (int i = 0; i < 120; i++) b.Step(1.0 / 120, -1);
            Assert.That(a.Angle, Is.EqualTo(b.Angle).Within(1e-8));
            Assert.That(a.BallPosition, Is.EqualTo(b.BallPosition).Within(1e-8));
            Assert.That(a.TimeLeft, Is.EqualTo(b.TimeLeft));
        }
        [Test]
        public void Rope_BothFailureReasonsFreezeSimulation()
        {
            var angle = new RopeModel(1); Set(angle, "Angle", 49d); angle.Step(1.0 / 60, 0);
            Assert.That(angle.Result, Is.EqualTo(RopeStep.Tilted));
            var ball = new RopeModel(1); Set(ball, "BallPosition", 101d); ball.Step(1.0 / 60, 0);
            Assert.That(ball.Result, Is.EqualTo(RopeStep.BallDropped));
            double position = ball.BallPosition; ball.Step(10, -1); Assert.That(ball.BallPosition, Is.EqualTo(position));
        }
        [Test]
        public void Rope_SurvivingFinalTickSucceedsAndRetryResets()
        {
            var model = new RopeModel(2);
            typeof(RopeModel).GetField("ticks", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, 299);
            model.Step(1.0 / 60, 0); Assert.That(model.Result, Is.EqualTo(RopeStep.Success));
            Assert.That(model.TimeLeft, Is.Zero); model.Step(1, 1); Assert.That(model.TimeLeft, Is.Zero);
            Assert.That(new RopeModel(2).TimeLeft, Is.EqualTo(5));
        }
        [Test]
        public void Fly_SwingBoundaryAndFiveHitsFreezeUntilRetry()
        {
            var model = new FlyModel(4); Set(model, "Position", model.Swatter + Vector2.right * 25);
            Assert.That(model.Swing(), Is.False);
            for (int i = 0; i < 5; i++) { Set(model, "Position", model.Swatter + Vector2.right * 24.9f); Assert.That(model.Swing(), Is.True); }
            Assert.That(model.Success, Is.True); Assert.That(model.Hits, Is.EqualTo(5));
            var old = model.Swatter; model.Step(1, 1, 1); Assert.That(model.Swatter, Is.EqualTo(old));
            Assert.That(model.Swing(), Is.False); Assert.That(new FlyModel(4).Hits, Is.Zero);
        }
        [Test]
        public void Fly_MovementClampsAndTargetsRemainInsideField()
        {
            var model = new FlyModel(5);
            for (int i = 0; i < 1000; i++)
            {
                model.Step(.1f, 1, -1);
                Assert.That(model.Position.x, Is.InRange(20, 380)); Assert.That(model.Position.y, Is.InRange(20, 280));
            }
            Assert.That(model.Swatter, Is.EqualTo(new Vector2(380, 20))); Assert.That(model.Success, Is.False);
        }
        [Test]
        public void Fly_NearbySwatterScaresAfterPointThreeSeconds()
        {
            var model = new FlyModel(1); Set(model, "Position", model.Swatter); Set(model, "Sitting", true);
            typeof(FlyModel).GetField("sitTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, .8f);
            model.Step(.2f, 0, 0); Assert.That(model.Sitting, Is.True);
            model.Step(.1f, 0, 0); Assert.That(model.Sitting, Is.False);
        }
    }
}
