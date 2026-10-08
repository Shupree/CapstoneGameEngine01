using System;

namespace GE.MiniGames
{
    public enum MiniGameState { Ready, Playing, Paused, Succeeded, Failed, Stopped }
    public enum MiniGameOutcome { Success, Failure }

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
