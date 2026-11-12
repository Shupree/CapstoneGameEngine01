using System.Reflection;
using System.Collections.Generic;
using GE.MiniGames.SafeZone;
using GE.MiniGames.Stack;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 원본의 피격 경계·경고 유예·최소 너비 보정을 고정 배치로 검증합니다.
    [Category("HtmlMiniGames")]
    public sealed class SafeZoneStackTests
    {
        private static void Field(object model, string name, object value)
        { model.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, value); }
        private static void Property(object model, string name, object value)
        { model.GetType().GetProperty(name).SetValue(model, value); }
        [Test]
        public void SafeZone_WarningIsHarmlessThenStrikeFailsAndFreezes()
        {
            var model = new SafeZoneModel(1); Field(model, "spawnTimer", 100d);
            var attacks = (List<ZoneAttack>)typeof(SafeZoneModel).GetField("attacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
            attacks.Add(new ZoneAttack { Bounds = new Rect(160, 0, 80, 300) });
            model.Step(.59, 0, 0); Assert.That(model.Result, Is.EqualTo(SafeZoneStep.Running));
            model.Step(.01, 0, 0); Assert.That(model.Result, Is.EqualTo(SafeZoneStep.Hit));
            double left = model.TimeLeft; model.Step(2, 1, 1); Assert.That(model.TimeLeft, Is.EqualTo(left));
        }
        [Test]
        public void SafeZone_CanEscapeWarning_AndStrikeExpires()
        {
            var model = new SafeZoneModel(2); Field(model, "spawnTimer", 100d);
            var attacks = (List<ZoneAttack>)typeof(SafeZoneModel).GetField("attacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
            attacks.Add(new ZoneAttack { Bounds = new Rect(160, 0, 80, 300) });
            model.Step(.6, 1, 0); Assert.That(model.Result, Is.EqualTo(SafeZoneStep.Running));
            Assert.That(model.Attacks[0].Striking, Is.True);
            model.Step(.2, 0, 0); Assert.That(model.Attacks.Count, Is.Zero);
        }
        [Test]
        public void SafeZone_SurvivalAndMovementBoundaryReset()
        {
            var model = new SafeZoneModel(1); Field(model, "spawnTimer", 100d);
            model.Step(7, -1, -1); Assert.That(model.Result, Is.EqualTo(SafeZoneStep.Success));
            Assert.That(model.Player, Is.EqualTo(new Vector2(10, 10))); Assert.That(model.TimeLeft, Is.Zero);
            Assert.That(new SafeZoneModel(1).Player, Is.EqualTo(new Vector2(200, 150)));
        }
        [Test]
        public void SafeZone_GeneratedAttacksNeverOverlap()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var model = new SafeZoneModel(seed);
                for (int tick = 0; tick < 840 && model.Result == SafeZoneStep.Running; tick++)
                {
                    model.Step(1.0 / 120, tick % 2 == 0 ? 1 : -1, 0);
                    Assert.That(model.Attacks.Count, Is.LessThanOrEqualTo(5));
                    for (int i = 0; i < model.Attacks.Count; i++) for (int j = i + 1; j < model.Attacks.Count; j++)
                        Assert.That(model.Attacks[i].Bounds.Overlaps(model.Attacks[j].Bounds), Is.False);
                }
            }
        }
        [Test]
        public void Stack_OnePixelOverlapBecomesFourteen_AndTouchingFails()
        {
            var model = new StackModel(1);
            Property(model, "Current", new StackBlock(234, 245, 70)); model.Drop();
            Assert.That(model.Count, Is.EqualTo(1)); Assert.That(model.Blocks[1].Width, Is.EqualTo(14));
            Assert.That(model.Blocks[1].X, Is.EqualTo(221));
            Property(model, "Current", new StackBlock(235, 220, 14)); model.Drop();
            Assert.That(model.Result, Is.EqualTo(StackStep.Missed));
        }
        [Test]
        public void Stack_SevenPerfectDropsWinAndFreeze_RetryStartsFromBase()
        {
            var model = new StackModel(4);
            for (int i = 0; i < 7; i++)
            {
                var top = model.Blocks[model.Blocks.Count - 1];
                Property(model, "Current", new StackBlock(top.X, top.Y - 25, top.Width)); model.Drop();
            }
            Assert.That(model.Result, Is.EqualTo(StackStep.Success)); Assert.That(model.Count, Is.EqualTo(7));
            var current = model.Current; model.Step(1); model.Drop(); Assert.That(model.Count, Is.EqualTo(7));
            Assert.That(model.Current, Is.EqualTo(current)); Assert.That(new StackModel(4).Count, Is.Zero);
        }
        [Test]
        public void Stack_BouncesWithoutLeavingField()
        {
            var model = new StackModel(9); bool reachedRight = false;
            for (int i = 0; i < 200; i++)
            {
                model.Step(.1f); Assert.That(model.Current.X, Is.InRange(0, 330));
                reachedRight |= model.Current.X == 330;
            }
            Assert.That(reachedRight, Is.True);
        }
    }
}
