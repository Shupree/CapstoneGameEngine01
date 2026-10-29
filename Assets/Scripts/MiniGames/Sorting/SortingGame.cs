using UnityEngine;

namespace GE.MiniGames.Sorting
{
    // MiniGame01·02와 같은 결과 계약을 사용합니다. MainGame 매니저를 직접 참조하거나 별도 목숨을 만들지 않습니다.
    [DisallowMultipleComponent]
    public sealed class SortingGame : MiniGameController
    {
        [SerializeField, Min(1)] private int targetCount = 30;
        [Tooltip("-1: 매 시도 무작위 / 0 이상: 테스트용 고정 출현 순서")]
        [SerializeField] private int randomSeed = -1;
        public SortingModel Model { get; private set; }
        public override string GameId => "MiniGame03";
        public override int Progress => Model?.Correct ?? 0;
        public override int Target => Model?.Target ?? Mathf.Max(1, targetCount);
        // 메인 안내 UI도 같은 문구를 사용하므로 새 그림의 분류 대상을 여기에서 함께 안내합니다.
        public override string StartInstructions =>
            $"A  곰     D  로봇\nSPACE  기타 장난감 3종\n{Target}개를 정확하게 분류하세요.\n오답은 실패! 메인 목숨 1개 차감 후 처음부터 재도전합니다.";

        // 재시도마다 새 묶음을 만들고 정답 수와 남은 수를 초기화합니다. 누적 목숨은 메인이 유지합니다.
        protected override void OnResetGame()
        {
            Model = new SortingModel(Mathf.Max(1, targetCount), randomSeed < 0 ? (int?)null : randomSeed);
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
            var choices = pressed & (GameButtons.A | GameButtons.D | GameButtons.Space);
            if (choices == GameButtons.None) return;
            // 여러 분류 키를 동시에 누르면 무시합니다. 세 키를 함께 눌러 정답을 자동 선택하는 편법을 막습니다.
            if (choices != GameButtons.A && choices != GameButtons.D && choices != GameButtons.Space) return;
            SortingChoice choice = choices == GameButtons.A ? SortingChoice.Left
                : choices == GameButtons.D ? SortingChoice.Right : SortingChoice.Discard;
            var result = Model.Submit(choice);
            if (result == SortingStep.Success)
                Finish(MiniGameOutcome.Success, $"{Target}개를 모두 정확하게 분류했습니다!");
            else if (result == SortingStep.Wrong)
                // 기존 메인의 OnMiniGameFailed가 실행당 한 번 목숨을 차감하고 로컬 Space 재시도를 허용합니다.
                Finish(MiniGameOutcome.Failure, "잘못 분류했습니다. 처음부터 다시 도전하세요.");
            else NotifyChanged();
        }

        // 입력 시에만 모델이 바뀌므로 정지 중 시간 누적·생성·물리 처리가 없습니다.
        protected override void OnGameTick(float deltaTime) { }
        protected override void OnClearInput() { }
        protected override void OnStopGame() { Model = null; }
    }
}
