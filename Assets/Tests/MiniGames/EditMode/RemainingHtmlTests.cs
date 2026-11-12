using System;
using System.Collections.Generic;
using System.Reflection;
using GE.MiniGames.Baseball;
using GE.MiniGames.Fishing;
using GE.MiniGames.Gust;
using GE.MiniGames.Tomato;
using GE.MiniGames.DeadPixel;
using NUnit.Framework;

namespace GE.MiniGames.Tests
{
    // 난수 대신 경계 배치를 주입해 HTML의 타격·시간·획득·색상 규칙을 검증합니다. 런타임에는 테스트용 API를 추가하지 않습니다.
    [Category("RemainingHtml")]
    public sealed class RemainingHtmlTests
    {
        private static void Set(object model, string name, object value) => model.GetType().GetProperty(name).SetValue(model, value);
        private static void Field(object model, string name, object value) => model.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model,value);
        private static T Get<T>(object model,string name) => (T)model.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
        [TestCase(59d, true)] [TestCase(101d, true)] [TestCase(58.99d, false)] [TestCase(101.01d, false)]
        public void Baseball_HitZoneMatchesVisibleRadius(double x, bool hit)
        {
            var model = new BaseballModel(1); Set(model,"Active",true); Set(model,"BallX",x); model.Swing();
            Assert.That(model.Hits, Is.EqualTo(hit ? 1 : 0));
            Assert.That(model.Result, Is.EqualTo(hit ? BaseballStep.Running : BaseballStep.Mistimed));
        }
        [Test]
        public void Baseball_EarlyAndMissedFail_SevenHitsWinAndFreeze()
        {
            var early = new BaseballModel(2); early.Swing(); Assert.That(early.Result, Is.EqualTo(BaseballStep.Early));
            var missed = new BaseballModel(2); missed.Step(4); Assert.That(missed.Result, Is.EqualTo(BaseballStep.Missed));
            var model = new BaseballModel(2);
            for(int i=0;i<7;i++) { Set(model,"Active",true); Set(model,"BallX",80d); model.Swing(); }
            Assert.That(model.Result, Is.EqualTo(BaseballStep.Success)); model.Step(10); model.Swing(); Assert.That(model.Hits, Is.EqualTo(7));
            Assert.That(new BaseballModel(2).Hits, Is.Zero);
        }
        [Test]
        public void Fishing_CaptureGainDecayAndBoundary()
        {
            var model = new FishingModel(1); Field(model,"moveTimer",100d); Set(model,"FishY",185d); Field(model,"target",185d);
            model.Step(1,0); Assert.That(model.Progress, Is.EqualTo(25).Within(1e-6));
            Set(model,"FishY",185.01d); Field(model,"target",185.01d); model.Step(1,0); Assert.That(model.Progress, Is.EqualTo(15).Within(1e-6));
            model.Step(2,0); Assert.That(model.Progress, Is.Zero);
        }
        [Test]
        public void Fishing_CompleteCaptureBeatsDeadline_RetryResetsTimers()
        {
            var model = new FishingModel(2); Field(model,"moveTimer",100d); Field(model,"ticks",1199); Set(model,"Progress",99.8d);
            model.Step(1d/120,0); Assert.That(model.Result, Is.EqualTo(FishingStep.Success)); Assert.That(model.Progress, Is.EqualTo(100));
            var timed = new FishingModel(2); Field(timed,"ticks",1199); timed.Step(1d/120,0); Assert.That(timed.Result, Is.EqualTo(FishingStep.Timeout));
            var retry = new FishingModel(2); Assert.That(retry.TimeLeft, Is.EqualTo(10)); Assert.That(Get<double>(retry,"moveTimer"), Is.Zero);
        }
        [Test]
        public void Fishing_MovementClampsAndFrameRatesAgree()
        {
            var a=new FishingModel(6); var b=new FishingModel(6);
            for(int i=0;i<120;i++) a.Step(1d/60,1);
            for(int i=0;i<240;i++) b.Step(1d/120,1);
            Assert.That(a.CaptureY, Is.EqualTo(265)); Assert.That(a.FishY,Is.EqualTo(b.FishY).Within(1e-8));
            Assert.That(a.Progress,Is.EqualTo(b.Progress).Within(1e-8));
        }
        [Test]
        public void Gust_BothWallsFailImmediately_AndNoInputLoses()
        {
            var right = new GustModel(1); for(int i=0;i<7;i++) right.Push(); Assert.That(right.Result,Is.EqualTo(GustStep.WallHit));
            var left = new GustModel(1); left.Step(3); Assert.That(left.Result,Is.EqualTo(GustStep.WallHit));
            double x=left.PlayerX; left.Push(); left.Step(1); Assert.That(left.PlayerX,Is.EqualTo(x));
        }
        [Test]
        public void Gust_ControlledPushesSurviveSixSeconds()
        {
            var model=new GustModel(8);
            for(int i=0;i<720;i++) { if(model.PlayerX<200) model.Push(); model.Step(1d/120); }
            Assert.That(model.Result,Is.EqualTo(GustStep.Success)); Assert.That(model.TimeLeft,Is.Zero);
            Assert.That(new GustModel(8).PlayerX,Is.EqualTo(200));
        }
        private static List<FallingItem> Items(TomatoModel model) => Get<List<FallingItem>>(model,"items");
        [Test]
        public void Tomato_SevenCatchesWinAndOnlyCountEachItemOnce()
        {
            var model=new TomatoModel(1); Field(model,"spawnTimer",100d);
            for(int i=0;i<7;i++) { Items(model).Add(new FallingItem { X=200,Y=247,Speed=400 }); model.Step(.1,0); Assert.That(model.Catches,Is.EqualTo(i+1)); }
            Assert.That(model.Result,Is.EqualTo(TomatoStep.Success)); model.Step(10,1); Assert.That(model.Catches,Is.EqualTo(7));
            Assert.That(new TomatoModel(1).Items.Count,Is.Zero);
        }
        [Test]
        public void Tomato_RockCatchAndTomatoMissFail_ButRockMissIsAllowed()
        {
            var rock=new TomatoModel(1); Items(rock).Add(new FallingItem {X=200,Y=248,Speed=400,Rock=true}); rock.Step(.1,0);
            Assert.That(rock.Result,Is.EqualTo(TomatoStep.RockCaught));
            var missed=new TomatoModel(1); Items(missed).Add(new FallingItem {X=20,Y=313,Speed=180}); missed.Step(.1,0);
            Assert.That(missed.Result,Is.EqualTo(TomatoStep.Missed));
            var safe=new TomatoModel(1); Items(safe).Add(new FallingItem {X=20,Y=313,Speed=180,Rock=true}); safe.Step(.1,0);
            Assert.That(safe.Result,Is.EqualTo(TomatoStep.Running)); Assert.That(safe.Catches,Is.Zero);
        }
        [Test]
        public void Tomato_GracePeriodSpeedAndArrivalSpacingMatchHtml()
        {
            var model=new TomatoModel(4); model.Step(.99,0); Assert.That(model.Items.Count,Is.Zero);
            model.Step(.01,0); Assert.That(model.Items.Count,Is.EqualTo(1));
            double speed=model.Items[0].Speed; Assert.That(speed,Is.InRange(180,400));
            double nextDelay=Get<double>(model,"spawnTimer");
            Assert.That(nextDelay + 280d/400 - 280/speed,Is.EqualTo(1.1).Within(1e-8));
            var boundary=new TomatoModel(3); Field(boundary,"spawnTimer",100d); boundary.Step(1,-1); Assert.That(boundary.BasketX,Is.Zero);
            boundary.Step(1,1); Assert.That(boundary.BasketX,Is.EqualTo(352));
        }
        [Test]
        public void DeadPixel_ClampsCursorAndUsesOriginalHslLightness()
        {
            var model=new DeadPixelModel(3); for(int i=0;i<5;i++) model.Move(-1,-1);
            Assert.That(model.CursorX,Is.Zero); Assert.That(model.CursorY,Is.Zero);
            var b=model.BaseColor; var t=model.TargetColor;
            Assert.That((Math.Max(b.r,Math.Max(b.g,b.b))+Math.Min(b.r,Math.Min(b.g,b.b)))/2,Is.EqualTo(.5).Within(1e-6));
            Assert.That((Math.Max(t.r,Math.Max(t.g,t.b))+Math.Min(t.r,Math.Min(t.g,t.b)))/2,Is.EqualTo(.62).Within(1e-6));
        }
        [Test]
        public void DeadPixel_FifteenCorrectSelectionsWinWithoutResettingTime()
        {
            var model=new DeadPixelModel(7); model.Step(1);
            for(int i=0;i<15;i++)
            {
                Assert.That(model.CursorX,Is.EqualTo(1)); Assert.That(model.CursorY,Is.EqualTo(1));
                model.Move(model.TargetIndex%3-1,model.TargetIndex/3-1); model.Select();
            }
            Assert.That(model.Result,Is.EqualTo(DeadPixelStep.Success)); Assert.That(model.TimeLeft,Is.EqualTo(13));
            model.Select(); model.Step(100); Assert.That(model.Score,Is.EqualTo(15));
        }
        [Test]
        public void DeadPixel_WrongSelectionAndTimeoutFail_RetryResets()
        {
            var wrong=new DeadPixelModel(1); Set(wrong,"TargetIndex",0); wrong.Select(); Assert.That(wrong.Result,Is.EqualTo(DeadPixelStep.Wrong));
            var timeout=new DeadPixelModel(1); timeout.Step(14); Assert.That(timeout.Result,Is.EqualTo(DeadPixelStep.Timeout));
            Assert.That(new DeadPixelModel(1).TimeLeft,Is.EqualTo(14));
        }
    }
}
