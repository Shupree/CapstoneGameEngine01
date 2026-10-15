using UnityEngine;

namespace GE.MiniGames.FlappyBird
{
    // 외부 시스템은 기존 Completed 계약만 구독합니다. 씬 전환과 목숨을 직접 다루지 않습니다.
    [DisallowMultipleComponent]
    public sealed class FlappyGame : MiniGameController
    {
        [SerializeField] private FlappyRules rules = new FlappyRules();
        public FlappyModel Model { get; private set; }
        public override string GameId => "MiniGame02";
        public override int Progress => Model?.Passed ?? 0;
        public override int Target => FlappyRules.Goal;
        public override string StartInstructions => "Spacebar를 누를 때마다 위로 날아갑니다.\n파이프 15쌍을 통과하세요.\n파이프와 위·아래 경계에 닿으면 실패합니다.";

        protected override void OnResetGame() { Model = new FlappyModel(rules); }
        internal override void OnGameInput(GameButtons pressed)
        {
            // 공통 API가 눌림 순간만 전달하므로 키 유지로 연속 날갯짓하지 않습니다.
            if ((pressed & GameButtons.Space) != 0) Model.Flap();
        }
        protected override void OnGameTick(float deltaTime)
        {
            var result = Model.Step(deltaTime);
            if (result == FlappyStep.Success) Finish(MiniGameOutcome.Success, "파이프 15쌍을 통과했습니다!");
            else if (result == FlappyStep.PipeCollision) Finish(MiniGameOutcome.Failure, "파이프에 부딪혔습니다.");
            else if (result == FlappyStep.BoundaryCollision) Finish(MiniGameOutcome.Failure, "화면 경계에 부딪혔습니다.");
            else NotifyChanged();
        }
        // 입력 예약과 별도 물리를 사용하지 않아 공통 정지·숨김 시 추가 진행이 없습니다.
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
        private void OnValidate() { rules = (rules ?? new FlappyRules()).ValidatedCopy(); }
    }
}
