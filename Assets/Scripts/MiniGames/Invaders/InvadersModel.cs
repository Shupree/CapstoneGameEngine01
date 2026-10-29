using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.Invaders
{
    public enum InvadersStep { Running, Success, ShotDown, DefenseBreached }

    // 좌상단 원점의 픽셀 좌표입니다. 화면 배율은 View에서만 적용하여 판정과 표시를 일치시킵니다.
    [Serializable]
    public sealed class InvadersRules
    {
        public float playerSpeed = 220f;
        public float playerBulletSpeed = 420f;
        public float enemyBulletSpeed = 180f;
        [Min(1)] public int startingLives = 1;
        public int randomSeed = -1;
        public const float Width = 600, Height = 480, Left = 20, Right = 580;
        public const float PlayerY = 440, DefenseY = 420;
        public const float PlayerWidth = 32, PlayerHeight = 24, EnemyWidth = 32, EnemyHeight = 24;
        public const float BulletWidth = 4, BulletHeight = 10;
        // 밸런스 조정: 적 발사 간격을 기존 1.1초의 절반으로 줄이고 최초 발사 유예는 유지합니다.
        public const float PlayerInterval = .4f, EnemyGrace = 2.5f, EnemyInterval = .55f, Invulnerability = 1.2f;
        // 조각 크기는 원래 10×10px로 복구하고, 방어막마다 6개를 한 줄로 배치합니다. 표시와 충돌이 같은 크기를 사용합니다.
        public const float ShieldPieceSize = 10f;
        public const int ShieldPiecesPerShield = 6;
        public const int Goal = 18;
        public InvadersRules Copy()
        {
            var rules = (InvadersRules)MemberwiseClone();
            rules.playerSpeed = Mathf.Clamp(playerSpeed, 1, 600);
            rules.playerBulletSpeed = Mathf.Clamp(playerBulletSpeed, 1, 1200);
            rules.enemyBulletSpeed = Mathf.Clamp(enemyBulletSpeed, 1, 1200);
            rules.startingLives = Mathf.Max(1, startingLives);
            return rules;
        }
    }

    public sealed class Invader
    {
        public int Column { get; internal set; }
        public int Row { get; internal set; }
        public Vector2 Position { get; internal set; }
        internal Vector2 PreviousPosition;
        public bool Alive { get; internal set; } = true;
    }
    public sealed class ShieldPiece
    {
        public Vector2 Position { get; internal set; }
        public bool Alive { get; internal set; } = true;
    }
    public sealed class InvadersBullet
    {
        public Vector2 Position { get; internal set; }
        public bool FromPlayer { get; internal set; }
    }

    // 개별 게임의 시간·편대·탄환·내부 목숨만 담당합니다. 외부 목숨과 씬 전환은 공통 결과 계약에 맡깁니다.
    public sealed class InvadersModel
    {
        private readonly List<Invader> enemies = new List<Invader>();
        private readonly List<ShieldPiece> shields = new List<ShieldPiece>();
        private readonly List<InvadersBullet> bullets = new List<InvadersBullet>();
        private readonly System.Random random;
        private readonly List<Invader> firingCandidates = new List<Invader>(6);
        private double nextPlayerShot, nextEnemyShot = InvadersRules.EnemyGrace, invulnerableUntil;
        private float previousPlayerX;
        public InvadersRules Rules { get; }
        public IReadOnlyList<Invader> Enemies { get; }
        public IReadOnlyList<ShieldPiece> Shields { get; }
        public IReadOnlyList<InvadersBullet> Bullets { get; }
        public float PlayerX { get; private set; } = 300;
        public int Lives { get; private set; }
        public int Kills { get; private set; }
        public int Direction { get; private set; } = 1;
        public float FormationSpeed => 32 + Kills * 2;
        public double Elapsed { get; private set; }
        public bool Invulnerable => Elapsed < invulnerableUntil;
        public int PlayerShots { get; private set; }
        public int EnemyShots { get; private set; }
        public InvadersStep Result { get; private set; }

        public InvadersModel(InvadersRules rules)
        {
            Rules = rules.Copy(); Lives = Rules.startingLives;
            random = Rules.randomSeed < 0 ? new System.Random() : new System.Random(Rules.randomSeed);
            Enemies = enemies.AsReadOnly(); Shields = shields.AsReadOnly(); Bullets = bullets.AsReadOnly();
            for (int row = 0; row < 3; row++) for (int column = 0; column < 6; column++)
                enemies.Add(new Invader { Row = row, Column = column, Position = new Vector2(100 + column * 80, 76 + row * 56) });
            // 기존 중심 X와 상단 Y를 유지하며 60×10px의 얇은 방어막을 만듭니다. 재시도 때도 같은 배치로 복구합니다.
            foreach (int center in new[] { 180, 420 })
                for (int column = 0; column < InvadersRules.ShieldPiecesPerShield; column++)
                    shields.Add(new ShieldPiece { Position = new Vector2(
                        center + (column - (InvadersRules.ShieldPiecesPerShield - 1) / 2f) * InvadersRules.ShieldPieceSize,
                        370 + InvadersRules.ShieldPieceSize / 2) });
        }

        public InvadersStep Step(float deltaTime, int horizontal = 0, bool firing = false)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Result != InvadersStep.Running) return Result;
            TryPlayerShot(firing);
            // 긴 프레임도 잘게 나누고 탄환 경로를 검사합니다. 발사 간격은 키 해제와 무관하게 유지됩니다.
            double remaining = deltaTime;
            while (remaining > 0.00000001 && Result == InvadersStep.Running)
            {
                float dt = (float)Math.Min(remaining, 1.0 / 120);
                remaining -= dt; Advance(dt, Mathf.Clamp(horizontal, -1, 1));
                if (Result == InvadersStep.Running) TryPlayerShot(firing);
            }
            return Result;
        }

        private void TryPlayerShot(bool firing)
        {
            if (!firing || Elapsed + .000001 < nextPlayerShot) return;
            SpawnBullet(true, new Vector2(PlayerX, InvadersRules.PlayerY - InvadersRules.PlayerHeight / 2 - InvadersRules.BulletHeight / 2));
            nextPlayerShot = Elapsed + InvadersRules.PlayerInterval; PlayerShots++;
        }

        private void Advance(float dt, int horizontal)
        {
            Elapsed += dt; previousPlayerX = PlayerX;
            PlayerX = Mathf.Clamp(PlayerX + horizontal * Rules.playerSpeed * dt,
                InvadersRules.Left + InvadersRules.PlayerWidth / 2, InvadersRules.Right - InvadersRules.PlayerWidth / 2);
            MoveFormation(dt);
            if (Result != InvadersStep.Running) return;
            CrushShields(); MoveBullets(dt);
            if (Result != InvadersStep.Running || Elapsed + .000001 < nextEnemyShot) return;
            var shooter = SelectShooter();
            if (shooter != null)
            {
                SpawnBullet(false, shooter.Position + Vector2.up * (InvadersRules.EnemyHeight / 2 + InvadersRules.BulletHeight / 2));
                EnemyShots++;
            }
            nextEnemyShot += InvadersRules.EnemyInterval;
        }

        private void MoveFormation(float dt)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var enemy in enemies)
            {
                enemy.PreviousPosition = enemy.Position;
                if (!enemy.Alive) continue;
                enemy.Position += Vector2.right * (Direction * FormationSpeed * dt);
                left = Mathf.Min(left, enemy.Position.x - InvadersRules.EnemyWidth / 2);
                right = Mathf.Max(right, enemy.Position.x + InvadersRules.EnemyWidth / 2);
            }
            bool bounce = Direction > 0 ? right >= InvadersRules.Right : left <= InvadersRules.Left;
            float correction = Direction > 0 ? InvadersRules.Right - right : InvadersRules.Left - left;
            if (bounce) Direction = -Direction;
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive) continue;
                if (bounce) enemy.Position += new Vector2(correction, 22);
                if (enemy.Position.y + InvadersRules.EnemyHeight / 2 >= InvadersRules.DefenseY)
                    Result = InvadersStep.DefenseBreached;
            }
        }

        private void CrushShields()
        {
            foreach (var piece in shields)
            {
                if (!piece.Alive) continue;
                foreach (var enemy in enemies)
                    if (enemy.Alive && Mathf.Abs(piece.Position.x - enemy.Position.x) <= InvadersRules.EnemyWidth / 2 + InvadersRules.ShieldPieceSize / 2 &&
                        Mathf.Abs(piece.Position.y - enemy.Position.y) <= InvadersRules.EnemyHeight / 2 + InvadersRules.ShieldPieceSize / 2)
                    { piece.Alive = false; break; }
            }
        }

        // 각 열의 맨 아래 생존 기체만 후보로 만들고, 후보 수를 기준으로 균등 추첨합니다.
        internal Invader SelectShooter()
        {
            firingCandidates.Clear();
            for (int column = 0; column < 6; column++)
            {
                Invader lowest = null;
                foreach (var enemy in enemies)
                    if (enemy.Alive && enemy.Column == column && (lowest == null || enemy.Position.y > lowest.Position.y)) lowest = enemy;
                if (lowest != null) firingCandidates.Add(lowest);
            }
            return firingCandidates.Count == 0 ? null : firingCandidates[random.Next(firingCandidates.Count)];
        }

        internal void SpawnBullet(bool fromPlayer, Vector2 position)
        { bullets.Add(new InvadersBullet { FromPlayer = fromPlayer, Position = position }); }

        private void MoveBullets(float dt)
        {
            for (int i = 0; i < bullets.Count && Result == InvadersStep.Running;)
            {
                var bullet = bullets[i]; var start = bullet.Position;
                var end = start + Vector2.up * ((bullet.FromPlayer ? -Rules.playerBulletSpeed : Rules.enemyBulletSpeed) * dt);
                float nearest = float.PositiveInfinity; ShieldPiece shieldHit = null; Invader enemyHit = null; bool playerHit = false;
                foreach (var piece in shields)
                {
                    if (!piece.Alive) continue;
                    float t = HitTime(start, end, piece.Position, piece.Position, Vector2.one * InvadersRules.ShieldPieceSize);
                    if (t < nearest) { nearest = t; shieldHit = piece; }
                }
                if (bullet.FromPlayer)
                {
                    foreach (var enemy in enemies)
                    {
                        if (!enemy.Alive) continue;
                        float t = HitTime(start, end, enemy.PreviousPosition, enemy.Position, new Vector2(InvadersRules.EnemyWidth, InvadersRules.EnemyHeight));
                        if (t < nearest) { nearest = t; enemyHit = enemy; shieldHit = null; }
                    }
                }
                else
                {
                    float t = HitTime(start, end, new Vector2(previousPlayerX, InvadersRules.PlayerY),
                        new Vector2(PlayerX, InvadersRules.PlayerY), new Vector2(InvadersRules.PlayerWidth, InvadersRules.PlayerHeight));
                    if (t < nearest) { nearest = t; playerHit = true; shieldHit = null; }
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    bullets.RemoveAt(i);
                    if (shieldHit != null) shieldHit.Alive = false;
                    else if (enemyHit != null) { enemyHit.Alive = false; Kills++; if (Kills == InvadersRules.Goal) Result = InvadersStep.Success; }
                    else if (playerHit && !Invulnerable)
                    {
                        Lives--; invulnerableUntil = Elapsed + InvadersRules.Invulnerability;
                        if (Lives == 0) Result = InvadersStep.ShotDown;
                    }
                    continue;
                }
                bullet.Position = end;
                if (end.y < -InvadersRules.BulletHeight || end.y > InvadersRules.Height + InvadersRules.BulletHeight) bullets.RemoveAt(i);
                else i++;
            }
        }

        // 상대 이동 선분과 확장 사각형으로 최초 접촉 시점을 구합니다. 한 발은 가장 가까운 대상 하나만 파괴합니다.
        private static float HitTime(Vector2 start, Vector2 end, Vector2 oldTarget, Vector2 target, Vector2 size)
        {
            Vector2 from = start - oldTarget, delta = (end - target) - from;
            Vector2 half = (size + new Vector2(InvadersRules.BulletWidth, InvadersRules.BulletHeight)) / 2;
            float enter = 0, leave = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .000001f) { if (Mathf.Abs(from[axis]) > half[axis]) return float.PositiveInfinity; continue; }
                float a = (-half[axis] - from[axis]) / delta[axis], b = (half[axis] - from[axis]) / delta[axis];
                enter = Mathf.Max(enter, Mathf.Min(a, b)); leave = Mathf.Min(leave, Mathf.Max(a, b));
                if (enter > leave) return float.PositiveInfinity;
            }
            return enter;
        }
    }
}
