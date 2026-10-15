using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.FlappyBird
{
    // 화면과 무관한 게임 단위입니다. 에셋을 교체해도 판정 크기와 난이도는 유지합니다.
    [Serializable]
    public sealed class FlappyRules
    {
        public float gravity = 14f;
        public float flapSpeed = 4.8f;
        public float pipeSpeed = 2.8f;
        public float pipeSpacing = 3.8f;
        public float gapHeight = 3.2f;
        public float pipeWidth = 0.9f;
        public float firstPipeX = 6f;
        public int randomSeed = -1;
        public const int Goal = 15;
        public const float Width = 16f, Height = 9f, BirdX = -3.5f;
        public const float BirdHalfWidth = 0.3f, BirdHalfHeight = 0.24f;

        // Inspector 입력을 안전한 범위로 한정하고 실행별 복사본을 사용합니다.
        public FlappyRules ValidatedCopy()
        {
            var copy = (FlappyRules)MemberwiseClone();
            copy.gravity = Mathf.Clamp(gravity, 6f, 24f);
            copy.flapSpeed = Mathf.Clamp(flapSpeed, 3f, 7f);
            copy.pipeSpeed = Mathf.Clamp(pipeSpeed, 1f, 6f);
            copy.pipeWidth = Mathf.Clamp(pipeWidth, 0.5f, 1.5f);
            copy.pipeSpacing = Mathf.Clamp(pipeSpacing, 3f, 6f);
            copy.gapHeight = Mathf.Clamp(gapHeight, 2.4f, 4.5f);
            copy.firstPipeX = Mathf.Clamp(firstPipeX, 4f, 8f);
            return copy;
        }
    }

    public enum FlappyStep { Running, Success, PipeCollision, BoundaryCollision }

    public sealed class FlappyPipe
    {
        public float X { get; internal set; }
        public float GapY { get; internal set; }
        public bool Counted { get; internal set; }
    }

    // 공통 컨트롤러가 시간과 눌림 순간을 전달하며, 이 모델은 로컬 규칙만 판정합니다.
    public sealed class FlappyModel
    {
        private readonly List<FlappyPipe> pipes = new List<FlappyPipe>();
        public IReadOnlyList<FlappyPipe> Pipes { get; }
        public FlappyRules Rules { get; }
        public float BirdY { get; private set; }
        public float Velocity { get; private set; }
        public int Passed { get; private set; }
        public FlappyStep Result { get; private set; }

        public FlappyModel(FlappyRules rules)
        {
            Rules = rules.ValidatedCopy();
            Pipes = pipes.AsReadOnly();
            var random = Rules.randomSeed < 0 ? new System.Random() : new System.Random(Rules.randomSeed);
            float gap = 0f;
            for (int i = 0; i < FlappyRules.Goal; i++)
            {
                // 첫 파이프는 중앙에 두고 다음 높이 변화는 제한해 갑작스러운 난이도 급증을 피합니다.
                if (i > 0) gap = Mathf.Clamp(gap + (float)(random.NextDouble() * 1.8 - 0.9), -1.6f, 1.6f);
                pipes.Add(new FlappyPipe { X = Rules.firstPipeX + i * Rules.pipeSpacing, GapY = gap });
            }
            Velocity = Rules.flapSpeed;
        }

        // 속도를 누적하지 않고 교체하므로 빠른 연타에서도 한 번의 상승력이 일정합니다.
        public void Flap() { if (Result == FlappyStep.Running) Velocity = Rules.flapSpeed; }

        public FlappyStep Step(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            // 작은 구간으로 나누고 이동 경로를 검사해 낮은 프레임에서도 파이프를 뚫지 않습니다.
            while (seconds > 0f && Result == FlappyStep.Running)
            {
                float dt = Mathf.Min(seconds, 1f / 120f);
                seconds -= dt;
                Advance(dt);
            }
            return Result;
        }

        private void Advance(float dt)
        {
            float oldY = BirdY;
            float oldVelocity = Velocity;
            BirdY += Velocity * dt - 0.5f * Rules.gravity * dt * dt;
            Velocity -= Rules.gravity * dt;
            float peak = oldVelocity > 0f && Velocity < 0f
                ? oldY + oldVelocity * oldVelocity / (2f * Rules.gravity) : Mathf.Max(oldY, BirdY);
            if (Mathf.Min(oldY, BirdY) - FlappyRules.BirdHalfHeight <= -FlappyRules.Height / 2f ||
                peak + FlappyRules.BirdHalfHeight >= FlappyRules.Height / 2f)
            { Result = FlappyStep.BoundaryCollision; return; }

            foreach (var pipe in pipes)
            {
                float oldX = pipe.X;
                pipe.X -= Rules.pipeSpeed * dt;
                float extent = Rules.pipeWidth / 2f + FlappyRules.BirdHalfWidth;
                // 새와 파이프의 상대 이동에서 겹치는 시간 구간만 검사합니다. 접촉도 충돌입니다.
                float enter = Mathf.Clamp01((oldX - FlappyRules.BirdX - extent) / (Rules.pipeSpeed * dt));
                float leave = Mathf.Clamp01((oldX - FlappyRules.BirdX + extent) / (Rules.pipeSpeed * dt));
                if (oldX + extent >= FlappyRules.BirdX && pipe.X - extent <= FlappyRules.BirdX)
                {
                    float y1 = oldY + oldVelocity * dt * enter - 0.5f * Rules.gravity * dt * dt * enter * enter;
                    float y2 = oldY + oldVelocity * dt * leave - 0.5f * Rules.gravity * dt * dt * leave * leave;
                    float maxY = Mathf.Max(y1, y2);
                    float apexT = oldVelocity / Rules.gravity / dt;
                    if (apexT >= enter && apexT <= leave) maxY = Mathf.Max(maxY, oldY + oldVelocity * oldVelocity / (2f * Rules.gravity));
                    if (Mathf.Min(y1, y2) - FlappyRules.BirdHalfHeight <= pipe.GapY - Rules.gapHeight / 2f ||
                        maxY + FlappyRules.BirdHalfHeight >= pipe.GapY + Rules.gapHeight / 2f)
                    { Result = FlappyStep.PipeCollision; return; }
                }
            }
            // 모든 충돌 검사를 먼저 끝냅니다. 같은 구간의 충돌보다 성공 판정이 앞서지 않게 합니다.
            foreach (var pipe in pipes)
                if (!pipe.Counted && pipe.X + Rules.pipeWidth / 2f < FlappyRules.BirdX - FlappyRules.BirdHalfWidth)
                { pipe.Counted = true; Passed++; }
            if (Passed >= FlappyRules.Goal) Result = FlappyStep.Success;
        }
    }
}
