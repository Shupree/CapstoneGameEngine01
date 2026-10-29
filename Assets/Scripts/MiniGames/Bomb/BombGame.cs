using UnityEngine;

namespace GE.MiniGames.Bomb
{
    // 기존 공통 컨트롤러가 시작·정지·재개·재시도와 결과 1회 통지를 처리합니다. 메인 목숨/씬 전환은 호출하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class BombGame : MiniGameController
    {
        [Tooltip("-1: 재시도마다 무작위 / 0 이상: 검증용 고정 배치")]
        [SerializeField] private int randomSeed = -1;
        public BombModel Model { get; private set; }
        public override string GameId => "MiniGame07";
        public override int Progress => Model?.CutCount ?? 0;
        public override int Target => BombModel.WireCount;
        public override string StartInstructions => "4.7초 안에 표시된 색상 순서대로 절단!\nA / D  전선 선택    SPACE  절단\n잘못된 순서 또는 시간 초과 시 실패\nTAB  일시정지 · 재개";

        protected override void OnResetGame() { Model = new BombModel(randomSeed < 0 ? (int?)null : randomSeed); }
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
            // 동시 좌우 입력은 A 우선, 이동과 절단이 겹치면 이동 후 절단합니다. 길게 누르기는 공통 API가 걸러냅니다.
            if ((pressed & GameButtons.A) != 0) Model.Move(-1);
            else if ((pressed & GameButtons.D) != 0) Model.Move(1);
            if ((pressed & GameButtons.Space) != 0) Model.Cut();
            PublishResult();
        }

        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            Model.Step(deltaTime); PublishResult();
        }

        // HTML의 성공·순서 오류·시간 초과를 기존 Completed 계약으로 연결합니다.
        private void PublishResult()
        {
            if (Model.Result == BombStep.Success) Finish(MiniGameOutcome.Success, "폭탄 해체 성공!");
            else if (Model.Result == BombStep.WrongWire) Finish(MiniGameOutcome.Failure, "순서가 틀렸습니다.");
            else if (Model.Result == BombStep.Timeout) Finish(MiniGameOutcome.Failure, "시간 초과!");
            else NotifyChanged();
        }

        // 예약 입력 없이 공통 키 해제 차단을 사용해 시작 Space가 첫 절단으로 이어지지 않게 합니다.
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
    }
}
