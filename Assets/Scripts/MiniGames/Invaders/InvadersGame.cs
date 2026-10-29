using UnityEngine;
using UnityEngine.InputSystem;

namespace GE.MiniGames.Invaders
{
    // MiniGame05와 같은 공통 수명·결과 계약을 사용하며, 연속 이동/발사 입력만 로컬로 확장합니다.
    [DisallowMultipleComponent]
    public sealed class InvadersGame : MiniGameController
    {
        [SerializeField] private InvadersRules rules = new InvadersRules();
        private GameButtons held, blocked;
        public InvadersModel Model { get; private set; }
        public override string GameId => "MiniGame06";
        public override int Progress => Model?.Kills ?? 0;
        public override int Target => InvadersRules.Goal;
        public override string StartInstructions => "적 18기 모두 처치하기\nA / D  좌우 이동    SPACE  발사 (누르면 연사)\n내부 목숨 1개 · 방어선을 지키세요\nTAB  일시정지 · 재개";

        protected override void OnResetGame() { Model = new InvadersModel(rules); }
        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 기존 게임과 동일한 ZXC 성공 스킵. 출시 시 이 #if~#endif 블록을 삭제합니다.
            var keys = Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            { Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)"); return; }
#endif
            held = KeyboardInputEnabled ? ReadButtons() : GameButtons.None;
            blocked &= held;
            held &= ~blocked;
        }

        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            int horizontal = ((held & GameButtons.D) != 0 ? 1 : 0) - ((held & GameButtons.A) != 0 ? 1 : 0);
            var result = Model.Step(deltaTime, horizontal, (held & GameButtons.Space) != 0);
            if (result == InvadersStep.Success) Finish(MiniGameOutcome.Success, "적 18기를 모두 처치했습니다!");
            else if (result == InvadersStep.DefenseBreached) Finish(MiniGameOutcome.Failure, "방어선 돌파");
            else if (result == InvadersStep.ShotDown) Finish(MiniGameOutcome.Failure, "기체 격추");
        }

        // 시작·정지·재개 시 누른 키를 해제할 때까지 차단합니다. 공통 Start/Tab/결과 입력과 중복되지 않게 합니다.
        protected override void OnClearInput()
        {
            blocked |= held;
            if (isActiveAndEnabled && Keyboard.current != null) blocked |= ReadButtons();
            held = GameButtons.None;
        }
        protected override void OnStopGame() { Model = null; }
    }
}
