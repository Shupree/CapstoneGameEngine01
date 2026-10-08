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
            // Fixed tie-break order for keys pressed in the same rendered frame.
            if ((pressed & GameButtons.W) != 0) Model.QueueDirection(Vector2Int.up);
            else if ((pressed & GameButtons.A) != 0) Model.QueueDirection(Vector2Int.left);
            else if ((pressed & GameButtons.S) != 0) Model.QueueDirection(Vector2Int.down);
            else if ((pressed & GameButtons.D) != 0) Model.QueueDirection(Vector2Int.right);
        }

        protected override void OnGameTick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed + 0.000001f < moveInterval) return;
            // At most one move per rendered frame: no catch-up bursts after a stall.
            elapsed = Mathf.Min(elapsed - moveInterval, moveInterval);
            SnakeStepResult result = Model.Step();
            if (result == SnakeStepResult.Won) Finish(MiniGameOutcome.Success, "사과 목표를 달성했습니다!");
            else if (result == SnakeStepResult.HitWall) Finish(MiniGameOutcome.Failure, "벽에 부딪혔습니다.");
            else if (result == SnakeStepResult.HitBody) Finish(MiniGameOutcome.Failure, "몸에 부딪혔습니다.");
            else NotifyChanged();
        }

        protected override void OnClearInput() { Model?.ClearPendingInput(); }
        protected override void OnStopGame() { elapsed = 0f; Model = null; }
    }
}
