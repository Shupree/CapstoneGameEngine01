using UnityEngine;

namespace GE.MiniGames.Dance
{
    // 시작·정지·재개·재시도·결과 1회 통지는 기존 공통 구현을 사용합니다. 메인 목숨이나 씬을 직접 조작하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class DanceGame : MiniGameController
    {
        [Tooltip("-1: 재시도마다 무작위 / 0 이상: 검증용 고정 순서")]
        [SerializeField] private int randomSeed = -1;
        public DanceModel Model { get; private set; }
        public override string GameId => "MiniGame08";
        public override int Progress => Model?.CompletedRounds ?? 0;
        public override int Target => DanceModel.Goal;
        public override string StartInstructions => "W A S D로 표시된 방향을 순서대로 입력!\n10라운드를 완료하면 성공\n틀린 입력 또는 시간 초과 시 실패\nTAB  일시정지 · 재개";

        protected override void OnResetGame() { Model = new DanceModel(randomSeed < 0 ? (int?)null : randomSeed); }
        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 기존 게임과 같은 ZXC 성공 스킵. 출시 시 이 #if~#endif 블록만 삭제합니다.
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            { Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)"); return; }
#endif
            if (Model == null) return;
            // 새 눌림만 받고 동시 입력은 기존 스네이크처럼 W→A→S→D 중 한 개만 처리합니다. Space는 플레이 중 무시합니다.
            if ((pressed & GameButtons.W) != 0) Model.Input(DanceDirection.Up);
            else if ((pressed & GameButtons.A) != 0) Model.Input(DanceDirection.Left);
            else if ((pressed & GameButtons.S) != 0) Model.Input(DanceDirection.Down);
            else if ((pressed & GameButtons.D) != 0) Model.Input(DanceDirection.Right);
            PublishResult();
        }
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            Model.Step(deltaTime); PublishResult();
        }

        // 원본의 자동 초기화 대신 공통 실패 화면으로 알립니다. Space 재시도로 1라운드를 새로 생성합니다.
        private void PublishResult()
        {
            if (Model.Result == DanceStep.Success) Finish(MiniGameOutcome.Success, "10라운드를 모두 완료했습니다!");
            else if (Model.Result == DanceStep.WrongKey) Finish(MiniGameOutcome.Failure, "방향 순서가 틀렸습니다.");
            else if (Model.Result == DanceStep.Timeout) Finish(MiniGameOutcome.Failure, "시간 초과!");
            else NotifyChanged();
        }
        // 전환 중 입력을 예약하지 않습니다. 길게 누른 키는 공통 키 해제 차단을 거쳐야 다시 입력됩니다.
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
    }
}
