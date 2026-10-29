using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.Dino
{
    // 상태 전환은 공통 컨트롤러, 논리 좌표와 충돌은 이 모델이 담당합니다. 입력 예약이나 별도 타이머는 없습니다.
    public sealed class DinoModel : IDisposable
    {
        public DinoRules Rules { get; }
        public IReadOnlyList<DinoObstacle> Course { get; }
        public DinoPhysics Physics { get; }
        public float Distance { get; private set; }
        public float Height { get; private set; }
        public float Velocity { get; private set; }
        public double Elapsed { get; private set; }
        public DinoStep Result { get; private set; }
        public bool Grounded => Height == 0;
        public float Progress01 => Mathf.Clamp01(Distance / Rules.goalDistance);
        public float FlagX => Rules.dinosaurLeft + Rules.dinosaurSize.x * 0.5f + Rules.goalDistance - Distance;
        public int AnimationFrame => (int)(Elapsed * Rules.animationFps);
        private readonly DinoArt art;
        private readonly DinoPhysics.Actor dinosaur;
        private readonly DinoPhysics.Actor[] obstacles;
        private bool disposed;

        public DinoModel(DinoRules rules, DinoArt art, IReadOnlyList<DinoObstacle> course = null)
        {
            Rules = rules.ValidatedCopy(); this.art = art;
            Course = course ?? DinoCourse.Build(Rules);
            Physics = new DinoPhysics();
            var poses = new DinoFrame[art.dinosaurRun.Length + 1];
            Array.Copy(art.dinosaurRun, poses, art.dinosaurRun.Length); poses[poses.Length - 1] = art.dinosaurJump;
            dinosaur = Physics.Create("Dinosaur collider", poses, Rules.dinosaurSize, Rules.collisionInset);
            obstacles = new DinoPhysics.Actor[Course.Count];
            dinosaur.Place(new Vector2(Rules.dinosaurLeft, Rules.GroundY));
            Physics.Simulate(1f / 240f);
        }

        // 공중 입력은 즉시 버립니다. 눌림 순간 필터와 정지 중 입력 폐기는 MiniGameController가 처리합니다.
        public void Jump()
        {
            if (!disposed && Result == DinoStep.Running && Grounded) Velocity = Rules.jumpSpeed;
        }
        public float ObstacleX(int index) => Course[index].CourseX - Distance;

        public DinoStep Step(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (disposed || Result != DinoStep.Running) return Result;
            while (seconds > 0 && Result == DinoStep.Running)
            {
                float dt = Mathf.Min(seconds, 1f / 240f, (Rules.goalDistance - Distance) / Rules.speed);
                if (dt <= 0) { Distance = Rules.goalDistance; Result = DinoStep.Success; break; }
                seconds -= dt; Elapsed += dt;
                Distance = Mathf.Min(Rules.goalDistance, (float)(Elapsed * Rules.speed));
                if (Velocity != 0 || Height > 0)
                {
                    Height += Velocity * dt - Rules.gravity * dt * dt * 0.5f;
                    Velocity -= Rules.gravity * dt;
                    if (Height <= 0) { Height = 0; Velocity = 0; }
                }
                dinosaur.SetFrame(Grounded ? AnimationFrame % art.dinosaurRun.Length : art.dinosaurRun.Length);
                dinosaur.Place(new Vector2(Rules.dinosaurLeft, Rules.GroundY + Height));
                for (int i = 0; i < Course.Count; i++)
                {
                    var obstacle = Course[i];
                    float x = ObstacleX(i);
                    var size = Rules.Size(obstacle.Kind, obstacle.Variant);
                    bool visible = x <= Rules.viewport.x && x + size.x >= 0;
                    if (visible && obstacles[i] == null)
                        obstacles[i] = Physics.Create(obstacle.Kind + " collider", art.Frames(obstacle.Kind, obstacle.Variant), size, Rules.collisionInset);
                    if (obstacles[i] == null) continue;
                    obstacles[i].SetActive(visible);
                    if (!visible) continue;
                    obstacles[i].SetFrame(AnimationFrame);
                    obstacles[i].Place(new Vector2(x, Rules.GroundY + Rules.Bottom(obstacle.Kind)));
                }
                Physics.Simulate(dt);
                for (int i = 0; i < obstacles.Length; i++)
                    if (obstacles[i] != null && ObstacleX(i) <= Rules.DinosaurFront &&
                        ObstacleX(i) + Rules.Size(Course[i].Kind, Course[i].Variant).x >= Rules.dinosaurLeft && dinosaur.Touches(obstacles[i]))
                    { Result = DinoStep.Collision; break; }
                if (Result == DinoStep.Running && Distance >= Rules.goalDistance)
                { Distance = Rules.goalDistance; Result = DinoStep.Success; }
            }
            return Result;
        }

        public void Dispose() { if (disposed) return; disposed = true; Physics.Dispose(); }
    }
}
