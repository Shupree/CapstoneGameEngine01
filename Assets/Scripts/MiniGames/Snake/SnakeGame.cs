using UnityEngine;

namespace GE.MiniGames.Snake
{
    [DisallowMultipleComponent]
    public sealed class SnakeGame : MiniGameController
    {
        [Header("Snake rules")]
        [SerializeField, Range(4, 20)] private int boardWidth = 10;
        [SerializeField, Range(4, 20)] private int boardHeight = 10;
        [SerializeField, Min(0.05f)] private float moveInterval = 0.25f;
        [SerializeField, Min(1)] private int targetApples = 5;
        [SerializeField, Min(1)] private int initialLength = 3;
        [Tooltip("-1: random each attempt; 0 or more: repeatable testing seed.")]
        [SerializeField] private int randomSeed = -1;

        private float elapsed;
        public SnakeModel Model { get; private set; }
        public override string GameId => "MiniGame01";
        public override int Progress => Model?.ApplesEaten ?? 0;
        public override int Target => Model?.TargetApples ?? targetApples;
        public float MoveInterval => moveInterval;
        public override string StartInstructions => $"WASD  방향 전환\n사과 {Target}개를 먹으면 성공!\n벽과 자신의 몸을 피하세요.";

        // 시작/재시도 시 모델을 새로 만들어 몸·사과·점수를 초기화하고, 이전 이동 타이머도 제거합니다.
        protected override void OnResetGame()
        {
            boardWidth = Mathf.Clamp(boardWidth, 4, 20);
            boardHeight = Mathf.Clamp(boardHeight, 4, 20);
            initialLength = Mathf.Clamp(initialLength, 1, boardWidth - 1);
            targetApples = Mathf.Clamp(targetApples, 1, boardWidth * boardHeight - initialLength);
            moveInterval = Mathf.Max(0.05f, moveInterval);
            Model = new SnakeModel(boardWidth, boardHeight, targetApples, initialLength,
                randomSeed < 0 ? (int?)null : randomSeed);
            elapsed = 0f;
        }

        internal override void OnGameInput(GameButtons pressed)
        {
            // 같은 프레임에 여러 방향을 누르면 W→A→S→D 우선순위로 한 방향만 예약합니다.
            if ((pressed & GameButtons.W) != 0) Model.QueueDirection(Vector2Int.up);
            else if ((pressed & GameButtons.A) != 0) Model.QueueDirection(Vector2Int.left);
            else if ((pressed & GameButtons.S) != 0) Model.QueueDirection(Vector2Int.down);
            else if ((pressed & GameButtons.D) != 0) Model.QueueDirection(Vector2Int.right);
        }

        protected override void OnGameTick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed + 0.000001f < moveInterval) return;
            // 프레임당 최대 한 칸만 이동해 일시적인 지연 뒤 여러 칸이 한꺼번에 진행되지 않게 합니다.
            elapsed = Mathf.Min(elapsed - moveInterval, moveInterval);
            SnakeStepResult result = Model.Step();
            // 규칙 판정을 공통 결과로 변환합니다. 실패·재시도는 로컬 처리, 성공의 다음 씬 전환은 메인 책임입니다.
            if (result == SnakeStepResult.Won) Finish(MiniGameOutcome.Success, "사과 목표를 달성했습니다!");
            else if (result == SnakeStepResult.HitWall) Finish(MiniGameOutcome.Failure, "벽에 부딪혔습니다.");
            else if (result == SnakeStepResult.HitBody) Finish(MiniGameOutcome.Failure, "몸에 부딪혔습니다.");
            else NotifyChanged();
        }

        protected override void OnClearInput() { Model?.ClearPendingInput(); }
        protected override void OnStopGame() { elapsed = 0f; Model = null; }
    }
}
