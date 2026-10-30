using UnityEngine;

namespace GE.MiniGames.Merge2048
{
    // 기존 공통 상태·목숨 결과 계약을 재사용합니다. 이 게임은 제한 시간과 별도 물리를 사용하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class Merge2048Game : MiniGameController
    {
        [Tooltip("-1: 재시도마다 무작위 / 0 이상: 검증용 고정 배치")]
        [SerializeField] private int randomSeed = -1;
        public Merge2048Model Model { get; private set; }
        public override string GameId => "MiniGame09";
        public override int Progress => Model?.HighestTile ?? 0;
        public override int Target => Merge2048Model.Goal;
        public override string StartInstructions => "W A S D로 타일을 밀어 같은 숫자를 합치세요.\n128 타일을 만들면 성공!\n더 이상 움직일 수 없으면 실패\nTAB  일시정지 · 재개";

        protected override void OnResetGame() { Model = new Merge2048Model(randomSeed < 0 ? (int?)null : randomSeed); }
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
            // 기존 스네이크처럼 새 눌림당 한 방향만 처리하며 동시 입력은 W→A→S→D 우선입니다. Space는 플레이 중 무시합니다.
            if ((pressed & GameButtons.W) != 0) Model.Move(MergeDirection.Up);
            else if ((pressed & GameButtons.A) != 0) Model.Move(MergeDirection.Left);
            else if ((pressed & GameButtons.S) != 0) Model.Move(MergeDirection.Down);
            else if ((pressed & GameButtons.D) != 0) Model.Move(MergeDirection.Right);
            else return;
            if (Model.Result == MergeStep.Success) Finish(MiniGameOutcome.Success, "128 타일을 만들었습니다!");
            else if (Model.Result == MergeStep.NoMoves) Finish(MiniGameOutcome.Failure, "더 이상 움직일 수 없습니다.");
            else NotifyChanged();
        }

        // 입력이 없는 동안 보드를 바꾸지 않습니다. 정지·재개·재시도 키 차단은 공통 컨트롤러가 담당합니다.
        protected override void OnGameTick(float deltaTime) { }
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
    }
}
