using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.FlappyBird
{
    // 화면과 무관한 게임 단위입니다. 원본 그림의 판정 크기는 모델 생성 직후 표시 계층에서 전달합니다.
    [Serializable]
    public sealed class FlappyRules
    {
        public float gravity = 14f;
        public float flapSpeed = 4.8f;
        public float pipeSpeed = 2.8f;
        public float pipeSpacing = 3.8f;
        // 씬에는 기존 기준 간격을 보관하고 생성 시에만 두 배를 적용합니다. 재시도/OnValidate에서 누적되지 않습니다.
        public float PipeSpacing => pipeSpacing * 2f;
        public float gapHeight = 3.2f;
        public float pipeWidth = 0.9f;
        public float firstPipeX = 6f;
        public int randomSeed = -1;
        public const int Goal = 15;
        public const float Width = 16f, Height = 9f, BirdX = -3.5f;
        public const float BirdHalfWidth = 0.3f, BirdHalfHeight = 0.24f;
        // 스프라이트 유무와 관계없이 표시와 판정의 가로·세로를 모두 30% 줄입니다.
        public const float BirdSizeScale = 0.7f;

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
        // 위·아래 해초는 서로 다른 원본을 사용할 수 있으므로 판정 폭도 각각 보관합니다.
        public float LowerWidth { get; internal set; }
        public float UpperWidth { get; internal set; }
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
        public Vector2 BirdHalfSize { get; internal set; } = new Vector2(FlappyRules.BirdHalfWidth, FlappyRules.BirdHalfHeight) * FlappyRules.BirdSizeScale;

        public FlappyModel(FlappyRules rules)
        {
            Rules = rules.ValidatedCopy();
            Pipes = pipes.AsReadOnly();
            var random = Rules.randomSeed < 0 ? new System.Random() : new System.Random(Rules.randomSeed);
            float gap = 0f;
            // 통과 공간과 화면 끝 사이에 최소 0.25만 남기고, 가능한 높이 범위 전체를 사용합니다.
            float maxGapOffset = (FlappyRules.Height - Rules.gapHeight) * 0.5f - 0.25f;
            for (int i = 0; i < FlappyRules.Goal; i++)
            {
                // 첫 쌍은 중앙, 이후는 이전 높이와 독립적으로 추첨해 비슷한 높이로 몰리는 경향을 줄입니다.
                if (i > 0) gap = Mathf.Lerp(-maxGapOffset, maxGapOffset, (float)random.NextDouble());
                pipes.Add(new FlappyPipe { X = Rules.firstPipeX + i * Rules.PipeSpacing, GapY = gap,
                    LowerWidth = Rules.pipeWidth, UpperWidth = Rules.pipeWidth });
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
            if (Mathf.Min(oldY, BirdY) - BirdHalfSize.y <= -FlappyRules.Height / 2f ||
                peak + BirdHalfSize.y >= FlappyRules.Height / 2f)
            { Result = FlappyStep.BoundaryCollision; return; }

            foreach (var pipe in pipes)
            {
                float oldX = pipe.X;
                pipe.X -= Rules.pipeSpeed * dt;
                if (HitsPipe(pipe, pipe.LowerWidth, false, oldX, oldY, oldVelocity, dt) ||
                    HitsPipe(pipe, pipe.UpperWidth, true, oldX, oldY, oldVelocity, dt))
                { Result = FlappyStep.PipeCollision; return; }
            }
            // 모든 충돌 검사를 먼저 끝냅니다. 같은 구간의 충돌보다 성공 판정이 앞서지 않게 합니다.
            foreach (var pipe in pipes)
                if (!pipe.Counted && pipe.X + Mathf.Max(pipe.LowerWidth, pipe.UpperWidth) / 2f < FlappyRules.BirdX - BirdHalfSize.x)
                { pipe.Counted = true; Passed++; }
            if (Passed >= FlappyRules.Goal) Result = FlappyStep.Success;
        }

        // 원본마다 다른 폭으로 접촉 구간을 계산하되 기존 연속 충돌 검사와 최고점 검사를 유지합니다.
        private bool HitsPipe(FlappyPipe pipe, float width, bool upper, float oldX, float oldY, float oldVelocity, float dt)
        {
            float extent = width / 2f + BirdHalfSize.x;
            if (oldX + extent < FlappyRules.BirdX || pipe.X - extent > FlappyRules.BirdX) return false;
            float enter = Mathf.Clamp01((oldX - FlappyRules.BirdX - extent) / (Rules.pipeSpeed * dt));
            float leave = Mathf.Clamp01((oldX - FlappyRules.BirdX + extent) / (Rules.pipeSpeed * dt));
            float y1 = oldY + oldVelocity * dt * enter - 0.5f * Rules.gravity * dt * dt * enter * enter;
            float y2 = oldY + oldVelocity * dt * leave - 0.5f * Rules.gravity * dt * dt * leave * leave;
            if (!upper) return Mathf.Min(y1, y2) - BirdHalfSize.y <= pipe.GapY - Rules.gapHeight / 2f;
            float maxY = Mathf.Max(y1, y2);
            float apexT = oldVelocity / Rules.gravity / dt;
            if (apexT >= enter && apexT <= leave) maxY = Mathf.Max(maxY, oldY + oldVelocity * oldVelocity / (2f * Rules.gravity));
            return maxY + BirdHalfSize.y >= pipe.GapY + Rules.gapHeight / 2f;
        }
    }
}
