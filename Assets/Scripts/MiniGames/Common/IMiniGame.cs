using System;

namespace GE.MiniGames
{
    public enum MiniGameState { Ready, Playing, Paused, Succeeded, Failed, Stopped }
    public enum MiniGameOutcome { Success, Failure }

    /// <summary>종료 순간의 결과 사본입니다. GameId와 RunId로 다른 씬/이전 재시도의 결과를 구분합니다.</summary>
    public readonly struct MiniGameResult
    {
        public readonly string GameId;
        public readonly int RunId;
        public readonly MiniGameOutcome Outcome;
        public readonly int Progress;
        public readonly int Target;
        public readonly string Reason;

        public MiniGameResult(string gameId, int runId, MiniGameOutcome outcome,
            int progress, int target, string reason)
        {
            GameId = gameId;
            RunId = runId;
            Outcome = outcome;
            Progress = progress;
            Target = target;
            Reason = reason;
        }
    }

    /// <summary>외부 진행 시스템의 공통 제어 계약입니다. Completed는 실행당 성공 또는 실패를 한 번 전달합니다.</summary>
    public interface IMiniGame
    {
        MiniGameState State { get; }
        event Action<MiniGameResult> Completed;
        void StartGame();
        void PauseGame();
        void ResumeGame();
        void ResetGame();
        void StopGame();
    }
}
