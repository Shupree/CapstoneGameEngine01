using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: InternalsVisibleTo("GE.MiniGames.Tests")]

namespace GE.MiniGames
{
    [Flags]
    internal enum GameButtons { None = 0, W = 1, A = 2, S = 4, D = 8, Space = 16, Tab = 32 }

    /// <summary>해당 게임의 상태·입력·결과만 관리합니다. 전역 시간 변경과 다음 씬 로드는 메인에 맡깁니다.</summary>
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

        // 실제 플레이와 테스트가 같은 입력 경로를 사용합니다. 눌림 순간만 감지해 Tab 반복 전환을 막습니다.
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

        /// <summary>최초 시작과 결과 화면의 재시도에 공통 사용합니다. 실행 번호는 이전 결과 구분을 위해 증가합니다.</summary>
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

        // Tab 정지는 이 컨트롤러의 진행만 멈춥니다. 보드와 남은 이동 시간은 유지합니다.
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

        /// <summary>외부에서 시작 대기로 돌릴 때 사용합니다. 즉시 플레이하려면 StartGame을 호출합니다.</summary>
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

        /// <summary>전환/종료 전 로컬 자원을 정리합니다. 씬 언로드와 메인 진행 처리는 호출자가 담당합니다.</summary>
        public void StopGame()
        {
            StopAllCoroutines();
            CancelInvoke();
            OnStopGame();
            FlushInput();
            SetState(MiniGameState.Stopped);
        }

        // 성공·실패를 즉시 멈추고 실행당 한 번 알립니다. 메인은 성공만 OnMiniGameCleared로 연결합니다.
        protected void Finish(MiniGameOutcome outcome, string reason)
        {
            if (State != MiniGameState.Playing || resultSent) return;
            resultSent = true;
            ResultReason = reason;
            // 구독자가 초기화/씬 전환을 해도 이번 실행의 결과가 유지되도록 통지 전에 값을 확정합니다.
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
            // 전환 중 누른 키는 놓을 때까지 차단하고 첫 틱을 건너뛰어, 재개 직후 입력/시간이 몰리지 않게 합니다.
            blockedButtons |= previousButtons | ReadButtons();
            OnClearInput();
            skipNextTick = true;
        }

        protected virtual void OnEnable() { FlushInput(); }
        // 기존 Shift 숨김으로 비활성화되어도 보드와 이동 주기의 남은 시간은 유지합니다.
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
