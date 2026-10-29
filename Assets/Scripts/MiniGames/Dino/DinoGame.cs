using UnityEngine;

namespace GE.MiniGames.Dino
{
    // 목숨·씬 전환·정지·재시도 입력·결과 중복 방지는 기존 공통 구현을 그대로 사용합니다.
    [DisallowMultipleComponent]
    public sealed class DinoGame : MiniGameController
    {
        [SerializeField] private DinoRules rules = new DinoRules();
        [SerializeField] private DinoArt art;
        public DinoModel Model { get; private set; }
        public DinoRules Settings => rules;
        public DinoArt Art => art;
        public override string GameId => "MiniGame04";
        public override int Progress => Mathf.FloorToInt(Model?.Distance ?? 0);
        public override int Target => Mathf.CeilToInt(Model?.Rules.goalDistance ?? rules.goalDistance);
        public override string StartInstructions => "목표지점까지 도달하기\nSpace 점프  /  Tab 일시정지 · 재개\n낮은 새는 점프 / 높은 새는 뛰지 않고 통과";

        public void Configure(DinoRules settings, DinoArt appearance) { rules = settings; art = appearance; }
        protected override void OnResetGame()
        {
            Model?.Dispose(); Model = null;
            // 에디터의 씬 제작 단계에서는 물리 씬을 생성하지 않습니다.
            if (Application.isPlaying && art != null) Model = new DinoModel(rules, art);
        }
        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 개발용 Z+X+C 성공 스킵. 출시 시 이 #if~#endif 블록만 삭제하면 됩니다.
            // 공통 성공 이벤트를 사용하며, 새 눌림만 받아 다음 게임의 연속 스킵을 막습니다.
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            {
                Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)");
                return;
            }
#endif
            if ((pressed & GameButtons.Space) != 0) Model?.Jump();
        }
        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            var result = Model.Step(deltaTime);
            // 공통 결과 UI를 재사용하되 거리형 게임의 실패 원인을 명시합니다.
            if (result == DinoStep.Collision) Finish(MiniGameOutcome.Failure, "실패: 장애물에 부딪혔습니다.");
            else if (result == DinoStep.Success) Finish(MiniGameOutcome.Success, "목표지점에 도착했습니다!");
            else NotifyChanged();
        }
        // 점프 예약을 보관하지 않으므로 상태 전환 때 추가 입력을 실행할 일이 없습니다.
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model?.Dispose(); Model = null; }
    }
}
