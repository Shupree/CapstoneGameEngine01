using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.DeadPixel
{
    // 공통 시작·정지·재시도와 결과 계약을 유지하며 이 미니게임의 입력만 모델에 전달합니다.
    [DisallowMultipleComponent]
    public sealed class DeadPixelGame : MiniGameController
    {
        
        public DeadPixelModel Model { get; private set; }
        public override string GameId => "MiniGame18";
        public override int Progress => Model?.Score ?? 0;
        public override int Target => 15;
        public override string StartInstructions => "W A S D로 선택 칸 이동 · SPACE로 확인\n색이 다른 한 칸을 찾으세요.\n14초 안에 15번 찾기 · 오답이면 실패\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new DeadPixelModel(); }
        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 기존 게임과 동일한 ZXC 성공 스킵. 출시 시 이 #if~#endif 블록만 삭제합니다.
            var keys = Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            { Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)"); return; }
#endif
            if (Model == null) return;
            
            // 새 눌림만 공격·선택으로 사용합니다. 방향 동시 입력은 기존 게임처럼 W→A→S→D 우선입니다.
            if ((pressed & GameButtons.W) != 0) Model.Move(0, -1); else if ((pressed & GameButtons.A) != 0) Model.Move(-1, 0); else if ((pressed & GameButtons.S) != 0) Model.Move(0, 1); else if ((pressed & GameButtons.D) != 0) Model.Move(1, 0); if ((pressed & GameButtons.Space) != 0) Model.Select();
            CheckResult();
        }
        // Playing에서만 호출되므로 정지 중에는 이동·시간·생성 간격 모두 멈춥니다.
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            Model.Step(deltaTime);
            CheckResult();
        }
        private void CheckResult()
        {
            if (Model.Result == DeadPixelStep.Success) Finish(MiniGameOutcome.Success, "15개 모두 찾았습니다!"); else if (Model.Result == DeadPixelStep.Wrong) Finish(MiniGameOutcome.Failure, "틀렸습니다!"); else if (Model.Result == DeadPixelStep.Timeout) Finish(MiniGameOutcome.Failure, "시간 초과!");
        }
        // 상태 전환 때 누른 이동 키는 해제 후 다시 눌러야 합니다. 새 눌림 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnClearInput() {  }
        protected override void OnStopGame() { Model = null; }
    }
}