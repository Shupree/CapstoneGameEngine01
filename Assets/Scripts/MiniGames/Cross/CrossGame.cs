using UnityEngine;

namespace GE.MiniGames.Cross
{
    // MiniGame01~04의 공통 계약을 그대로 사용합니다. 메인 매니저나 다른 씬의 데이터는 수정하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class CrossGame : MiniGameController
    {
        [Tooltip("-1: 재시도마다 무작위 / 0 이상: 검증용 고정 배치")]
        [SerializeField] private int randomSeed = -1;
        public CrossModel Model { get; private set; }
        public override string GameId => "MiniGame05";
        public override int Progress => Model?.CrossedLanes ?? 0;
        public override int Target => CrossModel.RoadRows;
        // 공통 시작 화면은 이 안내를 읽으므로 기존 씬의 문자열을 다시 저장할 필요가 없습니다.
        public override string StartInstructions =>
            "W / SPACE  앞으로    A / D  좌우    S  뒤로\n차량을 피해 25개 차선을 건너세요.\n5 / 15: 안전지대\nTAB  일시정지 · 재개";

        // 시도마다 출발 위치와 모든 차선을 초기화합니다. 목숨은 기존 메인 시스템에서 관리합니다.
        protected override void OnResetGame() { Model = new CrossModel(randomSeed < 0 ? (int?)null : randomSeed); }

        internal override void OnGameInput(GameButtons pressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEV_CLEAR_CHEAT: 기존 네 게임과 같은 개발용 성공 처리이며 일반 빌드에서는 제외됩니다.
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (KeyboardInputEnabled && keys != null && keys.zKey.isPressed && keys.xKey.isPressed && keys.cKey.isPressed
                && (keys.zKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame || keys.cKey.wasPressedThisFrame))
            { Finish(MiniGameOutcome.Success, "DEV CLEAR (ZXC)"); return; }
#endif
            if (Model == null) return;
            // 공통 API의 새 눌림만 사용합니다. 동시 입력은 전진→좌→후진→우 순서로 한 칸만 처리합니다.
            if ((pressed & (GameButtons.W | GameButtons.Space)) != 0) Model.Move(0, -1);
            else if ((pressed & GameButtons.A) != 0) Model.Move(-1, 0);
            else if ((pressed & GameButtons.S) != 0) Model.Move(0, 1);
            else if ((pressed & GameButtons.D) != 0) Model.Move(1, 0);
            PublishResult();
        }

        protected override void OnGameTick(float deltaTime)
        {
            if (Model == null) return;
            Model.Step(deltaTime);
            PublishResult();
        }

        // 공통 Finish가 실행당 한 번만 결과를 알리므로 외부 성공 집계/목숨 차감과 그대로 연결됩니다.
        private void PublishResult()
        {
            if (Model.Result == CrossStep.Success) Finish(MiniGameOutcome.Success, "모든 차선을 건너 안전지대에 도착했습니다!");
            else if (Model.Result == CrossStep.Collision) Finish(MiniGameOutcome.Failure, "차량에 부딪혔습니다. 다시 도전하세요.");
        }

        protected override void OnClearInput() { } // 예약 입력 없이 공통 컨트롤러의 전환 차단을 사용합니다.
        protected override void OnStopGame() { Model = null; }
    }
}
