using System.Reflection;
using GE.MiniGames.Snake;
using NUnit.Framework;
using UnityEngine;

namespace GE.MiniGames.Tests
{
    public sealed class SnakeTestUITests
    {
        private GameObject gameObject;
        private GameObject uiObject;
        private SnakeGame game;
        private MiniGameTestUI presenter;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Snake presenter test game");
            game = gameObject.AddComponent<SnakeGame>();
            game.ResetGame();
            uiObject = new GameObject("Snake presenter test UI");
            uiObject.SetActive(false);
            presenter = uiObject.AddComponent<MiniGameTestUI>();
            presenter.Configure(game, null, null, null, null, "Test instructions");
            uiObject.SetActive(true);
            // Normalize subscription whether this editor dispatched activation or not.
            InvokePresenterHook("OnDisable");
            InvokePresenterHook("OnEnable");
            game.StartGame();
            game.ProcessFrame(0f, GameButtons.None);
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null) InvokePresenterHook("OnDisable");
            if (uiObject != null) Object.DestroyImmediate(uiObject);
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SpaceHeldBeforeFailure_CannotRetryUntilReleasedAndPressedAgain()
        {
            presenter.ProcessSpace(false);
            presenter.ProcessSpace(true);
            int run = game.RunId;
            FailRun();
            presenter.ProcessSpace(true, true);
            for (int i = 0; i < 20; i++) presenter.ProcessSpace(true);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Assert.That(game.RunId, Is.EqualTo(run));

            presenter.ProcessSpace(false);
            presenter.ProcessSpace(true);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.RunId, Is.EqualTo(run + 1));
            Assert.That(game.Progress, Is.Zero);
            Assert.That(game.Model.Body.Count, Is.EqualTo(3));
        }

        [Test]
        public void SpacePressedOnResultTransition_CannotInstantlyRetry()
        {
            presenter.ProcessSpace(false);
            int run = game.RunId;
            FailRun();
            // Even if a release has already been observed in this transition frame,
            // its later key-down must not dismiss a result before it can be seen.
            presenter.ProcessSpace(false, true);
            presenter.ProcessSpace(true, true);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            presenter.ProcessSpace(true);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            Assert.That(game.RunId, Is.EqualTo(run));

            presenter.ProcessSpace(false);
            presenter.ProcessSpace(true);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.RunId, Is.EqualTo(run + 1));
        }

        private void FailRun()
        {
            for (int i = 0; i < 100 && game.State == MiniGameState.Playing; i++)
                game.ProcessFrame(0.1f, GameButtons.None);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
        }

        private void InvokePresenterHook(string name)
        {
            MethodInfo method = typeof(MiniGameTestUI).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, null);
        }
    }
}
