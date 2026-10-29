using System.Linq;
using GE.MiniGames.Invaders;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    // 메인 시스템 없이 실제 로컬 규칙의 경계·시간·충돌 순서를 검증합니다.
    public sealed class InvadersTests
    {
        private static InvadersModel NewGame(int lives = 1)
        { return new InvadersModel(new InvadersRules { randomSeed = 17, startingLives = lives, enemyBulletSpeed = 1 }); }

        [Test]
        public void InitialLayoutAndRetry_ExactlyMatchSpecification()
        {
            var model = NewGame(); Assert.That(model.Lives, Is.EqualTo(1)); Assert.That(model.Enemies.Count, Is.EqualTo(18));
            Assert.That(model.Direction, Is.EqualTo(1)); Assert.That(model.FormationSpeed, Is.EqualTo(32));
            foreach (var enemy in model.Enemies) Assert.That(enemy.Position, Is.EqualTo(new Vector2(100 + enemy.Column * 80, 76 + enemy.Row * 56)));
            Assert.That(model.Shields.Count, Is.EqualTo(24));
            foreach (int center in new[] { 180, 420 })
            {
                var pieces = model.Shields.Where(p => Mathf.Abs(p.Position.x - center) < 25).ToArray();
                Assert.That(pieces.Length, Is.EqualTo(12));
                // 50% 축소 후에도 12조각 형태와 상단 Y=370, 중심 X를 유지합니다.
                Assert.That(InvadersRules.ShieldPieceSize, Is.EqualTo(5));
                Assert.That(pieces.Count(p => p.Position.y == 372.5f), Is.EqualTo(3));
                Assert.That(pieces.Count(p => p.Position.y == 377.5f), Is.EqualTo(5));
                Assert.That(pieces.Count(p => p.Position.y == 382.5f), Is.EqualTo(4));
                Assert.That(pieces.Max(p => p.Position.x) - pieces.Min(p => p.Position.x) + InvadersRules.ShieldPieceSize, Is.EqualTo(25));
                Assert.That(pieces.Max(p => p.Position.y) - pieces.Min(p => p.Position.y) + InvadersRules.ShieldPieceSize, Is.EqualTo(15));
                Assert.That(pieces.Any(p => p.Position == new Vector2(center, 382.5f)), Is.False);
            }
            Kill(model, model.Enemies[0]); model.Step(.1f, 1, true);
            var retry = NewGame(); Assert.That(retry.Kills, Is.Zero); Assert.That(retry.PlayerX, Is.EqualTo(300));
            Assert.That(retry.Shields.All(p => p.Alive), Is.True); Assert.That(retry.Bullets.Count, Is.Zero);
        }

        [Test]
        public void Movement_ClampsOuterEdges_AndCanFireAtSameTime()
        {
            var model = NewGame(); model.Step(1, -1, true); model.Step(.5f, -1);
            Assert.That(model.PlayerX - InvadersRules.PlayerWidth / 2, Is.EqualTo(InvadersRules.Left));
            Assert.That(model.PlayerShots, Is.GreaterThan(1));
            model.Step(2.5f, 1);
            Assert.That(model.PlayerX + InvadersRules.PlayerWidth / 2, Is.EqualTo(InvadersRules.Right));
        }

        [Test]
        public void FireCooldown_HoldAndRapidPressesHaveSameRate()
        {
            var held = NewGame(); held.Step(0, 0, true); Assert.That(held.PlayerShots, Is.EqualTo(1));
            held.Step(.399f, 0, true); Assert.That(held.PlayerShots, Is.EqualTo(1));
            held.Step(.001f, 0, true); Assert.That(held.PlayerShots, Is.EqualTo(2));
            var tapped = NewGame(); tapped.Step(0, 0, true);
            for (int i = 0; i < 19; i++) { tapped.Step(.01f); tapped.Step(.01f, 0, true); }
            Assert.That(tapped.PlayerShots, Is.EqualTo(1));
            tapped.Step(.02f); tapped.Step(0, 0, true); Assert.That(tapped.PlayerShots, Is.EqualTo(2));
            tapped.Step(.5f); tapped.Step(0, 0, true); Assert.That(tapped.PlayerShots, Is.EqualTo(3));
        }

        [Test]
        public void Formation_BouncesUsingLivingEdges_DropsOnce_LeavesHoles()
        {
            var model = NewGame();
            foreach (var enemy in model.Enemies.Where(e => e.Column == 5)) Kill(model, enemy);
            float firstX = model.Enemies[0].Position.x, deadX = model.Enemies[5].Position.x;
            model.Step(2);
            Assert.That(model.Direction, Is.EqualTo(1), "제거한 오른쪽 열은 경계 계산에서 제외합니다.");
            Assert.That(model.Enemies[0].Position.x - firstX, Is.EqualTo(38 * 2).Within(.02));
            Assert.That(model.Enemies[5].Position.x, Is.EqualTo(deadX));
            float maxX = model.Enemies.Where(e => e.Alive).Max(e => e.Position.x);
            foreach (var enemy in model.Enemies.Where(e => e.Alive)) enemy.Position += Vector2.right * (InvadersRules.Right - 16 - maxX - 1);
            float topY = model.Enemies[0].Position.y;
            model.Step(.1f); Assert.That(model.Direction, Is.EqualTo(-1));
            Assert.That(model.Enemies[0].Position.y, Is.EqualTo(topY + 22));
            Assert.That(model.Enemies.Where(e => e.Alive).Max(e => e.Position.x) + 16, Is.LessThanOrEqualTo(InvadersRules.Right));
            Assert.That(model.Enemies[1].Position.x - model.Enemies[0].Position.x, Is.EqualTo(80).Within(.01));
            float minX = model.Enemies.Where(e => e.Alive).Min(e => e.Position.x);
            foreach (var enemy in model.Enemies.Where(e => e.Alive)) enemy.Position += Vector2.right * (InvadersRules.Left + 16 - minX + 1);
            model.Step(.1f); Assert.That(model.Direction, Is.EqualTo(1)); Assert.That(model.Enemies[0].Position.y, Is.EqualTo(topY + 44));
        }

        [Test]
        public void KillSpeed_AndLastEnemySuccessFreezeAllSimulation()
        {
            var model = NewGame();
            for (int i = 0; i < 9; i++) Kill(model, model.Enemies[i]);
            Assert.That(model.FormationSpeed, Is.EqualTo(50));
            for (int i = 9; i < 17; i++) Kill(model, model.Enemies[i]);
            Assert.That(model.FormationSpeed, Is.EqualTo(66));
            Assert.That(model.Result, Is.EqualTo(InvadersStep.Running));
            Kill(model, model.Enemies[17]); Assert.That(model.Result, Is.EqualTo(InvadersStep.Success));
            Assert.That(model.Kills, Is.EqualTo(18)); double elapsed = model.Elapsed; int shots = model.PlayerShots;
            model.Step(10, 1, true); Assert.That(model.Elapsed, Is.EqualTo(elapsed)); Assert.That(model.PlayerShots, Is.EqualTo(shots));
        }

        [Test]
        public void EnemyFire_GraceAndGlobalIntervalStayConstantAfterKills()
        {
            var model = NewGame(); model.Step(2.499f); Assert.That(model.EnemyShots, Is.Zero);
            model.Step(.001f); Assert.That(model.EnemyShots, Is.EqualTo(1));
            for (int i = 0; i < 17; i++) Kill(model, model.Enemies[i]);
            // 적 처치 처리에 흐른 시간까지 포함해 새 0.55초 간격의 직전/직후를 검사합니다.
            model.Step(.548f); Assert.That(model.EnemyShots, Is.EqualTo(1));
            model.Step(.002f); Assert.That(model.EnemyShots, Is.EqualTo(2));
            var shot = model.Bullets.First(b => !b.FromPlayer); float y = shot.Position.y, x = shot.Position.x;
            model.Step(.1f, 1); Assert.That(shot.Position.x, Is.EqualTo(x)); Assert.That(shot.Position.y - y, Is.EqualTo(.1f).Within(.001));
        }

        [Test]
        public void EnemyShooter_IsLowestLivingEnemyPerNonEmptyColumn_UniformlyChosen()
        {
            var model = NewGame();
            foreach (var enemy in model.Enemies.Where(e => e.Column == 5 || (e.Column == 0 && e.Row > 0))) enemy.Alive = false;
            var counts = new int[6];
            for (int i = 0; i < 10000; i++)
            {
                var shooter = model.SelectShooter(); counts[shooter.Column]++;
                Assert.That(shooter.Row, Is.EqualTo(shooter.Column == 0 ? 0 : 2));
            }
            Assert.That(counts[5], Is.Zero);
            foreach (int count in counts.Take(5)) Assert.That(count, Is.InRange(1800, 2200));
        }

        [TestCase(true, 410, 382.5f)]
        [TestCase(false, 350, 372.5f)]
        public void Shields_FirstContactOnly_AndNextBulletPassesHole(bool player, float startY, float hitY)
        {
            var model = new InvadersModel(new InvadersRules { playerBulletSpeed = 1200, enemyBulletSpeed = 1200 });
            model.SpawnBullet(player, new Vector2(175, startY)); model.Step(.1f);
            Assert.That(model.Shields.Count(p => !p.Alive), Is.EqualTo(1)); Assert.That(model.Kills, Is.Zero);
            Assert.That(model.Shields.Single(p => !p.Alive).Position, Is.EqualTo(new Vector2(175, hitY)));
            Assert.That(model.Bullets.Count, Is.Zero);
            model.SpawnBullet(player, new Vector2(175, startY)); model.Step(.1f);
            Assert.That(model.Shields.Count(p => !p.Alive), Is.EqualTo(2));
            Assert.That(model.Shields.Any(p => p.Position == new Vector2(175, 377.5f) && !p.Alive), Is.True);
        }

        [Test]
        public void DescendingEnemy_CrushesOnlyOverlappingPieces_WithoutTakingDamage()
        {
            var model = NewGame(); var enemy = model.Enemies[0]; enemy.Position = new Vector2(180, 380);
            model.Step(.001f);
            Assert.That(enemy.Alive, Is.True); Assert.That(model.Kills, Is.Zero);
            Assert.That(model.Shields.Any(p => p.Position.x < 250 && !p.Alive), Is.True);
            Assert.That(model.Shields.Where(p => p.Position.x > 300).All(p => p.Alive), Is.True);
        }

        [Test]
        public void DefaultOneLife_FirstHitFails()
        {
            var model = NewGame(); model.SpawnBullet(false, new Vector2(300, 440)); model.Step(.001f);
            Assert.That(model.Lives, Is.Zero); Assert.That(model.Result, Is.EqualTo(InvadersStep.ShotDown));
            Assert.That(model.Bullets.Count, Is.Zero);
        }

        [Test]
        public void Invulnerability_RemovesAllHitsWithoutDoubleDamage_ExpiresAfterOnePointTwoSeconds()
        {
            var model = NewGame(3);
            for (int i = 0; i < 3; i++) model.SpawnBullet(false, new Vector2(300, 440));
            model.Step(.001f); Assert.That(model.Lives, Is.EqualTo(2)); Assert.That(model.Bullets.Count, Is.Zero);
            model.Step(1.19f); model.SpawnBullet(false, new Vector2(300, 440)); model.Step(.001f);
            Assert.That(model.Lives, Is.EqualTo(2)); Assert.That(model.Bullets.Count, Is.Zero);
            model.Step(.02f); model.SpawnBullet(false, new Vector2(300, 440)); model.Step(.001f);
            Assert.That(model.Lives, Is.EqualTo(1));
        }

        [Test]
        public void DefenseLine_ContactFailsRegardlessOfRemainingLives()
        {
            var model = NewGame(3); model.Enemies[0].Position = new Vector2(100, InvadersRules.DefenseY - 12);
            model.Step(.001f); Assert.That(model.Result, Is.EqualTo(InvadersStep.DefenseBreached)); Assert.That(model.Lives, Is.EqualTo(3));
        }

        [Test]
        public void CommonController_ReportsSuccessOnce_AndResetsLocalState()
        {
            var owner = new GameObject("Invaders lifecycle test");
            try
            {
                var game = owner.AddComponent<InvadersGame>(); game.KeyboardInputEnabled = false;
                int count = 0; MiniGameResult last = default; game.Completed += result => { count++; last = result; };
                game.StartGame(); game.ProcessFrame(0, GameButtons.None);
                foreach (var enemy in game.Model.Enemies) Kill(game.Model, enemy);
                game.ProcessFrame(0, GameButtons.None); game.ProcessFrame(1, GameButtons.None);
                Assert.That(count, Is.EqualTo(1)); Assert.That(last.Outcome, Is.EqualTo(MiniGameOutcome.Success));
                Assert.That(last.GameId, Is.EqualTo("MiniGame06")); Assert.That(last.Progress, Is.EqualTo(18));
                game.StartGame(); Assert.That(game.RunId, Is.EqualTo(2)); Assert.That(game.Progress, Is.Zero);
                Assert.That(game.Model.Lives, Is.EqualTo(1)); Assert.That(game.Model.FormationSpeed, Is.EqualTo(32));
                Assert.That(game.Model.Shields.All(p => p.Alive), Is.True);
                game.PauseGame(); double time = game.Model.Elapsed; game.ProcessFrame(1, GameButtons.Space);
                Assert.That(game.Model.Elapsed, Is.EqualTo(time)); game.StopGame(); Assert.That(game.Model, Is.Null);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static void Kill(InvadersModel model, Invader enemy)
        {
            model.SpawnBullet(true, enemy.Position); model.Step(.0001f);
            Assert.That(enemy.Alive, Is.False);
        }
    }
}
