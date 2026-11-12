using UnityEngine;

namespace GE.MiniGames.Stack
{
    // 기존 공통 상태·목숨 결과 계약을 재사용합니다. 이 게임은 제한 시간과 별도 물리를 사용하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class StackGame : MiniGameController
    {
        [Tooltip("-1: 재시도마다 무작위 / 0 이상: 검증용 고정 배치")]
        [SerializeField] private int randomSeed = -1;
        public StackModel Model { get; private set; }
        public override string GameId => "MiniGame14";
        public override int Progress => Model?.Count ?? 0;
        public override int Target => 7;
        public override string StartInstructions => "W A S D 또는 SPACE로 블록 쌓기\n아래 블록과 겹치는 순간 누르세요.\n7개를 쌓으면 성공!\nTAB  일시정지 · 재개";

        protected override void OnResetGame() { Model = new StackModel(randomSeed < 0 ? (int?)null : randomSeed); }
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
            // 새 눌림당 한 번만 쌓습니다. 동시에 여러 조작 키를 눌러도 한 블록만 확정합니다.
            if ((pressed & (GameButtons.W | GameButtons.A | GameButtons.S | GameButtons.D | GameButtons.Space)) == 0) return;
            Model.Drop();
            if (Model.Result == StackStep.Success) Finish(MiniGameOutcome.Success, "블록 7개를 쌓았습니다!");
            else if (Model.Result == StackStep.Missed) Finish(MiniGameOutcome.Failure, "블록이 빗나갔습니다!");
            else NotifyChanged();
        }

        // Playing에서만 움직여 정지·결과 화면에서는 블록 위치를 유지합니다. 입력 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnGameTick(float deltaTime) { Model?.Step(deltaTime); }
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
    }
}
