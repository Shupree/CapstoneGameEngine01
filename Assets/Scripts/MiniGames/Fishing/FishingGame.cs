using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.Fishing
{
    // 공통 시작·정지·재시도와 결과 계약을 유지하며 이 미니게임의 입력만 모델에 전달합니다.
    [DisallowMultipleComponent]
    public sealed class FishingGame : MiniGameController
    {
        private GameButtons held, blocked;
        public FishingModel Model { get; private set; }
        public override string GameId => "MiniGame11";
        public override int Progress => Mathf.FloorToInt((float)(Model?.Progress ?? 0));
        public override int Target => 100;
        public override string StartInstructions => "W / S로 포획 영역 이동\n물고기를 영역 안에 유지하세요.\n10초 안에 포획 100% 달성하기\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new FishingModel(); }
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
            held = KeyboardInputEnabled ? ReadButtons() : GameButtons.None; blocked &= held; held &= ~blocked;
            // 새 눌림만 공격·선택으로 사용합니다. 방향 동시 입력은 기존 게임처럼 W→A→S→D 우선입니다.
            
            CheckResult();
        }
        // Playing에서만 호출되므로 정지 중에는 이동·시간·생성 간격 모두 멈춥니다.
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            int vertical = ((held & GameButtons.S) != 0 ? 1 : 0) - ((held & GameButtons.W) != 0 ? 1 : 0); Model.Step(deltaTime, vertical);
            CheckResult();
        }
        private void CheckResult()
        {
            if (Model.Result == FishingStep.Success) Finish(MiniGameOutcome.Success, "포획 성공!"); else if (Model.Result == FishingStep.Timeout) Finish(MiniGameOutcome.Failure, "시간 초과!");
        }
        // 상태 전환 때 누른 이동 키는 해제 후 다시 눌러야 합니다. 새 눌림 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnClearInput() { blocked |= held; if (isActiveAndEnabled && Keyboard.current != null) blocked |= ReadButtons(); held = GameButtons.None; }
        protected override void OnStopGame() { Model = null; }
    }
}