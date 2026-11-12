using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.Fly
{
    // 기존 미니게임의 공통 상태·결과 계약을 사용하고, 누르고 있는 이동 입력만 로컬에서 관리합니다.
    [DisallowMultipleComponent]
    public sealed class FlyGame : MiniGameController
    {
        private GameButtons held, blocked;
        public FlyModel Model { get; private set; }
        public override string GameId => "MiniGame17";
        public override int Progress => Model?.Hits ?? 0;
        public override int Target => 5;
        public override string StartInstructions => "W A S D  파리채 이동\nSPACE  휘두르기 · 파리 5마리 잡기\n가까이 오래 머물면 파리가 도망갑니다.\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new FlyModel(); }
        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 기존 게임과 동일한 ZXC 성공 스킵. 출시 시 이 #if~#endif 블록만 삭제합니다.
            var keys = Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            { Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)"); return; }
#endif
            held = KeyboardInputEnabled ? ReadButtons() : GameButtons.None;
            blocked &= held; held &= ~blocked;
            if ((pressed & GameButtons.Space) != 0 && Model != null && Model.Swing()) { if (Model.Success) Finish(MiniGameOutcome.Success, "파리 5마리를 모두 잡았습니다!"); else NotifyChanged(); }
        }
        // 공통 Playing 상태에서만 진행하므로 정지 중 물리·타이머·파리 이동이 모두 유지됩니다.
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            int horizontal = ((held & GameButtons.D) != 0 ? 1 : 0) - ((held & GameButtons.A) != 0 ? 1 : 0);
            int vertical = ((held & GameButtons.S) != 0 ? 1 : 0) - ((held & GameButtons.W) != 0 ? 1 : 0);
            Model.Step(deltaTime, horizontal, vertical);
        }
        // 시작·정지·재개 때 누른 이동 키는 놓은 뒤 다시 눌러야 합니다. 기존 Invaders 입력 처리와 같습니다.
        protected override void OnClearInput()
        {
            blocked |= held;
            if (isActiveAndEnabled && Keyboard.current != null) blocked |= ReadButtons();
            held = GameButtons.None;
        }
        protected override void OnStopGame() { Model = null; }
    }
}