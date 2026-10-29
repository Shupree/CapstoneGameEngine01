using System;
using UnityEngine;

namespace GE.MiniGames.Dino
{
    public enum DinoObstacleKind { Cactus, Rock, LowBird, HighBird }
    public enum DinoStep { Running, Success, Collision }

    // 모든 수치는 720×330 기준 논리 픽셀입니다. 물리 계층에서만 동일한 비율로 월드 단위로 변환합니다.
    [Serializable]
    public sealed class DinoRules
    {
        public Vector2 viewport = new Vector2(720, 330);
        public Vector2 dinosaurSize = new Vector2(48, 56);
        public float dinosaurLeft = 96, groundFromTop = 266;
        [Min(1)] public float speed = 299;
        [Min(1)] public float jumpSpeed = 600, gravity = 1800;
        [Min(1)] public float goalDistance = 8000;
        // 씬의 기준 거리 8000을 유지하고 실제 목표만 1.5배로 계산해 재시도 시 중복 적용을 막습니다.
        public float GoalDistance => goalDistance * 1.5f;
        // 진행률에 따라 출발 속도에서 최대 30%까지 서서히 빨라집니다. 재시도는 거리 0부터 다시 시작합니다.
        public float MaximumSpeed => speed * 1.3f;
        public float SpeedAtDistance(float distance) => Mathf.Lerp(speed, MaximumSpeed, Mathf.Clamp01(distance / GoalDistance));
        [Min(0)] public float firstArrivalSeconds = 2.5f;
        public Vector2[] cactusSizes = { new Vector2(26, 40), new Vector2(32, 48), new Vector2(38, 56) };
        public Vector2[] rockSizes = { new Vector2(50, 28), new Vector2(62, 32) };
        public Vector2 birdSize = new Vector2(40, 28);
        public float lowBirdBottom = 20, highBirdBottom = 80;
        public Vector2 shortInterval = new Vector2(0.85f, 1.05f);
        public Vector2 mediumInterval = new Vector2(1.25f, 1.65f);
        public Vector2 longInterval = new Vector2(1.9f, 2.4f);
        public Vector3 intervalWeights = new Vector3(0.4f, 0.4f, 0.2f);
        [Min(0)] public float landingBuffer = 0.08f, finishClearance = 350;
        [Min(0)] public float collisionInset = 1;
        [Min(1)] public float animationFps = 8;
        public int randomSeed = -1;
        public float FlightTime => 2f * jumpSpeed / gravity;
        public float GroundY => viewport.y - groundFromTop;
        public float DinosaurFront => dinosaurLeft + dinosaurSize.x;

        // 플레이 도중 Inspector 변경이 한 판의 판정에 섞이지 않도록 실행별 사본을 만듭니다.
        public DinoRules ValidatedCopy()
        {
            var copy = (DinoRules)MemberwiseClone();
            copy.speed = Mathf.Max(1, speed); copy.gravity = Mathf.Max(1, gravity);
            copy.jumpSpeed = Mathf.Max(1, jumpSpeed); copy.goalDistance = Mathf.Max(1, goalDistance);
            copy.cactusSizes = (Vector2[])cactusSizes.Clone(); copy.rockSizes = (Vector2[])rockSizes.Clone();
            return copy;
        }

        public Vector2 Size(DinoObstacleKind kind, int variant) => kind == DinoObstacleKind.Cactus ? cactusSizes[variant]
            : kind == DinoObstacleKind.Rock ? rockSizes[variant] : birdSize;
        public float Bottom(DinoObstacleKind kind) => kind == DinoObstacleKind.LowBird ? lowBirdBottom
            : kind == DinoObstacleKind.HighBird ? highBirdBottom : 0;
    }
}
