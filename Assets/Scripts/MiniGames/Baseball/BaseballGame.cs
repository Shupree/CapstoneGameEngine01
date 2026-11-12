using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.Baseball
{
    // 공통 시작·정지·재시도와 결과 계약을 유지하며 이 미니게임의 입력만 모델에 전달합니다.
    [DisallowMultipleComponent]
    public sealed class BaseballGame : MiniGameController
    {
        
        public BaseballModel Model { get; private set; }
        public override string GameId => "MiniGame13";
        public override int Progress => Model?.Hits ?? 0;
        public override int Target => 7;
        public override string StartInstructions => "SPACE로 타격하기\n초록 타격존 안에 공 중심이 오면 누르세요.\n7번 연속 타격하면 성공!\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new BaseballModel(); }
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
            if ((pressed & GameButtons.Space) != 0) Model.Swing();
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
            if (Model.Result == BaseballStep.Success) Finish(MiniGameOutcome.Success, "7번 모두 타격했습니다!"); else if (Model.Result == BaseballStep.Early) Finish(MiniGameOutcome.Failure, "너무 일찍 휘둘렀습니다!"); else if (Model.Result == BaseballStep.Mistimed) Finish(MiniGameOutcome.Failure, "타이밍이 빗나갔습니다!"); else if (Model.Result == BaseballStep.Missed) Finish(MiniGameOutcome.Failure, "공을 놓쳤습니다!");
        }
        // 상태 전환 때 누른 이동 키는 해제 후 다시 눌러야 합니다. 새 눌림 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnClearInput() {  }
        protected override void OnStopGame() { Model = null; }
    }
}