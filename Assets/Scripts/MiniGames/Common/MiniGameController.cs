using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: InternalsVisibleTo("GE.MiniGames.Tests")]

namespace GE.MiniGames
{
    [Flags]
    internal enum GameButtons { None = 0, W = 1, A = 2, S = 4, D = 8, Space = 16, Tab = 32 }

    /// <summary>Scene-local lifecycle. Never changes Time.timeScale or creates a global manager.</summary>
    public abstract class MiniGameController : MonoBehaviour, IMiniGame
    {
        [SerializeField] private bool keyboardInputEnabled = true;
        private GameButtons previousButtons;
        private GameButtons blockedButtons;
        private bool resultSent;
        private bool initialized;
        private bool skipNextTick;

        public MiniGameState State { get; private set; } = MiniGameState.Ready;
        public int RunId { get; private set; }
        public string ResultReason { get; private set; } = string.Empty;
        public abstract string GameId { get; }
        public abstract int Progress { get; }
        public abstract int Target { get; }
        public virtual string StartInstructions => string.Empty;
        public bool KeyboardInputEnabled { get => keyboardInputEnabled; set { keyboardInputEnabled = value; FlushInput(); } }
        public event Action<MiniGameResult> Completed;
        public event Action<MiniGameState> StateChanged;
        public event Action Changed;

        protected virtual void Awake() { EnsureInitialized(); }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            ResetGame();
        }

        protected virtual void Update()
        {
            ProcessFrame(Time.deltaTime, keyboardInputEnabled ? ReadButtons() : GameButtons.None);
        }

        internal static GameButtons ReadButtons()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return GameButtons.None;
            GameButtons held = GameButtons.None;
            if (keyboard.wKey.isPressed) held |= GameButtons.W;
            if (keyboard.aKey.isPressed) held |= GameButtons.A;
            if (keyboard.sKey.isPressed) held |= GameButtons.S;
            if (keyboard.dKey.isPressed) held |= GameButtons.D;
            if (keyboard.spaceKey.isPressed) held |= GameButtons.Space;
            if (keyboard.tabKey.isPressed) held |= GameButtons.Tab;
            return held;
        }

        // The test runner drives this exact path, including debounce and transition frames.
        internal void ProcessFrame(float deltaTime, GameButtons held)
        {
            blockedButtons &= held;
            GameButtons pressed = held & ~previousButtons & ~blockedButtons;
            previousButtons = held;
            if ((pressed & GameButtons.Tab) != 0)
            {
                if (State == MiniGameState.Playing) { PauseGame(); return; }
                if (State == MiniGameState.Paused) { ResumeGame(); return; }
            }
            if (State != MiniGameState.Playing) return;
            if (skipNextTick) { skipNextTick = false; return; }
            OnGameInput(pressed);
            if (State == MiniGameState.Playing)
                OnGameTick(Mathf.Clamp(deltaTime, 0f, 0.1f));
        }

        public void StartGame()
        {
            EnsureInitialized();
            if (State == MiniGameState.Playing || State == MiniGameState.Paused) return;
            OnResetGame();
            RunId++;
            resultSent = false;
            ResultReason = string.Empty;
            FlushInput();
            SetState(MiniGameState.Playing);
        }

        public void PauseGame()
        {
            if (State != MiniGameState.Playing) return;
            FlushInput();
            SetState(MiniGameState.Paused);
        }

        public void ResumeGame()
        {
            if (State != MiniGameState.Paused) return;
            FlushInput();
            SetState(MiniGameState.Playing);
        }

        public void ResetGame()
        {
            initialized = true;
            StopAllCoroutines();
            CancelInvoke();
            OnResetGame();
            resultSent = false;
            ResultReason = string.Empty;
            FlushInput();
            SetState(MiniGameState.Ready);
        }

        public void StopGame()
        {
            StopAllCoroutines();
            CancelInvoke();
            OnStopGame();
            FlushInput();
            SetState(MiniGameState.Stopped);
        }

        protected void Finish(MiniGameOutcome outcome, string reason)
        {
            if (State != MiniGameState.Playing || resultSent) return;
            resultSent = true;
            ResultReason = reason;
            // Capture this run before observers have an opportunity to reset/unload the scene.
            var result = new MiniGameResult(GameId, RunId, outcome, Progress, Target, reason);
            var completed = Completed;
            FlushInput();
            SetState(outcome == MiniGameOutcome.Success ? MiniGameState.Succeeded : MiniGameState.Failed);
            completed?.Invoke(result);
        }

        private void SetState(MiniGameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
            NotifyChanged();
        }

        protected void NotifyChanged() { Changed?.Invoke(); }

        private void FlushInput()
        {
            blockedButtons |= previousButtons | ReadButtons();
            OnClearInput();
            skipNextTick = true;
        }

        protected virtual void OnEnable() { FlushInput(); }
        // Root deactivation (the existing Shift manager) retains the board and fractional timer.
        protected virtual void OnDisable()
        {
            FlushInput();
            StopAllCoroutines();
            CancelInvoke();
        }
        protected virtual void OnDestroy()
        {
            OnStopGame();
            Completed = null;
            StateChanged = null;
            Changed = null;
        }
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) PauseGame();
        }

        internal abstract void OnGameInput(GameButtons pressed);
        protected abstract void OnGameTick(float deltaTime);
        protected abstract void OnResetGame();
        protected abstract void OnStopGame();
        protected abstract void OnClearInput();
    }
}
