using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.Gust
{
    // 공통 시작·정지·재시도와 결과 계약을 유지하며 이 미니게임의 입력만 모델에 전달합니다.
    [DisallowMultipleComponent]
    public sealed class GustGame : MiniGameController
    {
        
        public GustModel Model { get; private set; }
        public override string GameId => "MiniGame16";
        public override int Progress => Mathf.FloorToInt((float)(6 - (Model?.TimeLeft ?? 6)));
        public override int Target => 6;
        public override string StartInstructions => "SPACE를 연타해 강풍 버티기\n누를 때마다 오른쪽으로 이동합니다.\n양쪽 벽을 피해 6초 생존하세요.\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new GustModel(); }
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
            if ((pressed & GameButtons.Space) != 0) Model.Push();
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
            if (Model.Result == GustStep.Success) Finish(MiniGameOutcome.Success, "6초 동안 버텼습니다!"); else if (Model.Result == GustStep.WallHit) Finish(MiniGameOutcome.Failure, "벽에 부딪혔습니다!");
        }
        // 상태 전환 때 누른 이동 키는 해제 후 다시 눌러야 합니다. 새 눌림 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnClearInput() {  }
        protected override void OnStopGame() { Model = null; }
    }
}