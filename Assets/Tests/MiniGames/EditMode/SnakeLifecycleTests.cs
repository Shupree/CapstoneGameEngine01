using System.Collections.Generic;
using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    /// <summary>Drives the production frame path, including input edges and transition frames.</summary>
    public sealed class SnakeLifecycleTests
    {
        private GameObject owner;
        private SnakeGame game;
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            owner = new GameObject("Snake lifecycle test");
            game = owner.AddComponent<SnakeGame>();
            game.ResetGame();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void StartPauseResumeResetStop_RespectLifecycleBoundaries()
        {
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            game.ResumeGame();
            game.PauseGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));

            BeginRun();
            int run = game.RunId;
            SnakeModel activeModel = game.Model;
            game.StartGame();
            Assert.That(game.RunId, Is.EqualTo(run));
            Assert.That(game.Model, Is.SameAs(activeModel));

            game.PauseGame();
            game.StartGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            Assert.That(game.RunId, Is.EqualTo(run));
            game.ResumeGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));

            game.ResetGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Assert.That(game.Model, Is.Not.SameAs(activeModel));
            Assert.That(game.Progress, Is.Zero);
            game.StopGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Stopped));
            Assert.That(game.Model, Is.Null);
            game.ResumeGame();
            Frame(10f, GameButtons.Tab | GameButtons.W);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Stopped));
            Assert.That(game.Model, Is.Null);

            BeginRun();
            Assert.That(game.RunId, Is.EqualTo(run + 1));
            Assert.That(game.Model, Is.Not.Null);
        }

        [Test]
        public void Tab_UsesPressEdgesAndNeverStartsReadyOrTerminalGame()
        {
            Frame(1f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Frame(0f);
            BeginRun();
            Frame(0f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            for (int i = 0; i < 20; i++) Frame(1f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));

            Frame(0f);
            Frame(0f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            for (int i = 0; i < 20; i++) Frame(0f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Frame(0f);

            RunIntoWall();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Frame(1f, GameButtons.Tab);
            Frame(0f);
            Frame(1f, GameButtons.Tab);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
        }

        [Test]
        public void Pause_PreservesFractionalTimerAndClearsQueuedTurn()
        {
            BeginRun();
            Vector2Int head = game.Model.Body[0];
            Frame(0.1f);
            Frame(0.1f);
            Frame(0.01f, GameButtons.W);
            Assert.That(game.Model.Body[0], Is.EqualTo(head));
            game.PauseGame();
            for (int i = 0; i < 50; i++) Frame(20f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head));

            game.ResumeGame();
            Frame(20f); // First transition frame cannot charge stopped time.
            Assert.That(game.Model.Body[0], Is.EqualTo(head));
            Frame(0.04f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head + Vector2Int.right));
            Assert.That(game.Model.Direction, Is.EqualTo(Vector2Int.right));
        }

        [Test]
        public void HeldDuringPause_IsBlockedUntilReleasedAfterResume()
        {
            BeginRun();
            Vector2Int head = game.Model.Body[0];
            game.PauseGame();
            Frame(2f, GameButtons.W);
            game.ResumeGame();
            Frame(0f, GameButtons.W);
            Frame(0.1f, GameButtons.W);
            Frame(0.1f, GameButtons.W);
            Frame(0.05f, GameButtons.W);
            Assert.That(game.Model.Body[0], Is.EqualTo(head + Vector2Int.right));

            Frame(0f);
            Frame(0f, GameButtons.W);
            AdvanceOneMove();
            Assert.That(game.Model.Body[0], Is.EqualTo(head + Vector2Int.right + Vector2Int.up));
        }

        [Test]
        public void AStalledFrame_DoesNotMoveSeveralCellsOrAccumulateCatchup()
        {
            BeginRun();
            Vector2Int head = game.Model.Body[0];
            Frame(20f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head));
            Frame(20f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head));
            Frame(20f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head + Vector2Int.right));
            Frame(0f);
            Assert.That(game.Model.Body[0], Is.EqualTo(head + Vector2Int.right));
        }

        [Test]
        public void WallFailure_ReportsExactlyOncePerRunAndStopsBoard()
        {
            var results = new List<MiniGameResult>();
            game.Completed += results.Add;
            BeginRun();
            RunIntoWall();
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].GameId, Is.EqualTo("MiniGame01"));
            Assert.That(results[0].Outcome, Is.EqualTo(MiniGameOutcome.Failure));
            Assert.That(results[0].RunId, Is.EqualTo(game.RunId));
            Assert.That(results[0].Reason, Is.Not.Empty);
            Vector2Int head = game.Model.Body[0];
            for (int i = 0; i < 100; i++) Frame(10f, GameButtons.W | GameButtons.Space);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(game.Model.Body[0], Is.EqualTo(head));

            BeginRun();
            RunIntoWall();
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].RunId, Is.EqualTo(results[0].RunId + 1));
        }

        [Test]
        public void TerminalStateObserverDestroyingGame_DoesNotLoseCompletedResult()
        {
            var results = new List<MiniGameResult>();
            game.Completed += results.Add;
            game.StateChanged += state =>
            {
                if (state != MiniGameState.Failed) return;
                InvokeLifecycleHook("OnDestroy"); // Ensure cleanup also runs without a PlayMode Awake.
                Object.DestroyImmediate(owner);
                owner = null;
            };
            BeginRun();
            int run = game.RunId;
            for (int i = 0; i < 20 && owner != null; i++) AdvanceOneMove();
            Assert.That(owner, Is.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].RunId, Is.EqualTo(run));
            Assert.That(results[0].Outcome, Is.EqualTo(MiniGameOutcome.Failure));
            Assert.That(results[0].GameId, Is.EqualTo("MiniGame01"));
        }

        [Test]
        public void FiveApples_ReportSuccessOnceAndResetRestoresEntireRun()
        {
            var results = new List<MiniGameResult>();
            game.Completed += results.Add;
            BeginRun();
            DriveToSuccess();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded));
            Assert.That(game.Progress, Is.EqualTo(5));
            Assert.That(game.Model.Body.Count, Is.EqualTo(8));
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Outcome, Is.EqualTo(MiniGameOutcome.Success));
            Assert.That(results[0].Progress, Is.EqualTo(5));
            Assert.That(results[0].Target, Is.EqualTo(5));
            Vector2Int finalHead = game.Model.Body[0];
            Frame(0f, GameButtons.Tab);
            for (int i = 0; i < 10; i++) Frame(10f, GameButtons.W);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded));
            Assert.That(game.Model.Body[0], Is.EqualTo(finalHead));
            Assert.That(results.Count, Is.EqualTo(1));

            game.ResetGame();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            Assert.That(game.Progress, Is.Zero);
            Assert.That(game.ResultReason, Is.Empty);
            Assert.That(game.Model.Body.Count, Is.EqualTo(3));
            Assert.That(game.Model.Body[0], Is.EqualTo(new Vector2Int(5, 5)));
            Assert.That(game.Model.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(game.Model.IsFinished, Is.False);
            Assert.That(game.Model.HasApple, Is.True);

            BeginRun();
            Vector2Int initialHead = game.Model.Body[0];
            Frame(0.1f);
            Frame(0.1f);
            Assert.That(game.Model.Body[0], Is.EqualTo(initialHead), "Retry must reset fractional movement time.");
            Frame(0.05f);
            Assert.That(game.Model.Body[0], Is.EqualTo(initialHead + Vector2Int.right));
            Assert.That(results.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisableEnableHooks_KeepBoardAndTimerButDiscardInput()
        {
            BeginRun();
            SnakeModel model = game.Model;
            Vector2Int head = model.Body[0];
            Frame(0.1f);
            Frame(0.1f, GameButtons.W);
            owner.SetActive(false);
            InvokeLifecycleHook("OnDisable");
            Assert.That(game.Model, Is.SameAs(model));
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            owner.SetActive(true);
            InvokeLifecycleHook("OnEnable");
            Frame(20f, GameButtons.W);
            Assert.That(model.Body[0], Is.EqualTo(head));
            Frame(0.05f, GameButtons.W);
            Assert.That(model.Body[0], Is.EqualTo(head + Vector2Int.right));

            game.StopGame();
            InvokeLifecycleHook("OnDisable");
            InvokeLifecycleHook("OnEnable");
            Assert.That(game.State, Is.EqualTo(MiniGameState.Stopped));
            Assert.That(game.Model, Is.Null);
        }

        [Test]
        public void LifecycleAndTerminalResult_DoNotChangeGlobalTimeScale()
        {
            Time.timeScale = 0.37f;
            BeginRun();
            AssertTimeScale();
            game.PauseGame();
            AssertTimeScale();
            game.ResumeGame();
            AssertTimeScale();
            Frame(0f);
            RunIntoWall();
            AssertTimeScale();
            game.ResetGame();
            AssertTimeScale();
            game.StopGame();
            AssertTimeScale();
            Object.DestroyImmediate(owner);
            owner = null;
            AssertTimeScale();
        }

        private void BeginRun()
        {
            game.StartGame();
            Frame(0f); // Consume lifecycle transition without advancing simulation.
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
        }

        private void Frame(float seconds, GameButtons held = GameButtons.None)
        {
            game.ProcessFrame(seconds, held);
        }

        private void AdvanceOneMove(GameButtons direction = GameButtons.None)
        {
            Frame(0.1f, direction);
            Frame(0.1f);
            Frame(0.05f);
        }

        private void RunIntoWall()
        {
            for (int i = 0; i < 20 && game.State == MiniGameState.Playing; i++) AdvanceOneMove();
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
        }

        private void DriveToSuccess()
        {
            // A Hamiltonian cycle through the default 10x10 board starts aligned with
            // the initial right-facing body. Every apple is visited within 100 moves.
            for (int i = 0; i < 600 && game.State == MiniGameState.Playing; i++)
            {
                Vector2Int head = game.Model.Body[0];
                GameButtons next;
                if (head.x == 0) next = head.y < 9 ? GameButtons.W : GameButtons.D;
                else if (head.y == 0) next = GameButtons.A;
                else if ((head.y & 1) == 1) next = head.x < 9 ? GameButtons.D : GameButtons.S;
                else next = head.x > 1 ? GameButtons.A : GameButtons.S;
                AdvanceOneMove(next);
            }
        }

        private void InvokeLifecycleHook(string name)
        {
            // EditMode does not guarantee normal MonoBehaviour activation callbacks.
            // Call the production hook explicitly; PlayMode verifies Unity dispatch.
            MethodInfo method = typeof(MiniGameController).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(game, null);
        }

        private static void AssertTimeScale()
        {
            Assert.That(Time.timeScale, Is.EqualTo(0.37f).Within(0.00001f));
        }
    }
}
