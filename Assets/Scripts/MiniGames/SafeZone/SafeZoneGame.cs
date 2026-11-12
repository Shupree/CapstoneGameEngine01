using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.SafeZone
{
    // 기존 미니게임의 공통 상태·결과 계약을 사용하고, 누르고 있는 이동 입력만 로컬에서 관리합니다.
    [DisallowMultipleComponent]
    public sealed class SafeZoneGame : MiniGameController
    {
        private GameButtons held, blocked;
        public SafeZoneModel Model { get; private set; }
        public override string GameId => "MiniGame12";
        public override int Progress => Mathf.FloorToInt((float)(7 - (Model?.TimeLeft ?? 7)));
        public override int Target => 7;
        public override string StartInstructions => "W A S D로 붉은 영역을 피하세요.\n0.6초 경고 뒤 타격이 발생합니다.\n7초 동안 생존하면 성공!\nTAB  일시정지 · 재개";
        protected override void OnResetGame() { Model = new SafeZoneModel(); }
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
            
        }
        // 공통 Playing 상태에서만 진행하므로 정지 중 플레이어·공격 영역·생존 시간이 모두 유지됩니다.
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            int horizontal = ((held & GameButtons.D) != 0 ? 1 : 0) - ((held & GameButtons.A) != 0 ? 1 : 0);
            int vertical = ((held & GameButtons.S) != 0 ? 1 : 0) - ((held & GameButtons.W) != 0 ? 1 : 0);
            Model.Step(deltaTime, horizontal, vertical);
            if (Model.Result == SafeZoneStep.Success) Finish(MiniGameOutcome.Success, "7초 동안 생존했습니다!");
            else if (Model.Result == SafeZoneStep.Hit) Finish(MiniGameOutcome.Failure, "타격에 맞았습니다.");
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