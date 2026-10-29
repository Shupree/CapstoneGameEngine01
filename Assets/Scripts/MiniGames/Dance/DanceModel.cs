using System;
using System.Collections.Generic;

namespace GE.MiniGames.Dance
{
    public enum DanceDirection { Up, Left, Down, Right }
    public enum DanceStep { Running, Success, WrongKey, Timeout }

    // HTML의 순서·라운드·제한 시간만 관리합니다. 실패 후 재시도는 다른 미니게임과 같은 공통 수명 계약에 맡깁니다.
    public sealed class DanceModel
    {
        public const int Goal = 10;
        public const double SecondsPerKey = 1.2, RoundDelay = .4;
        private readonly Random random;
        private double transitionLeft;
        public IReadOnlyList<DanceDirection> Sequence { get; private set; }
        public int Round { get; private set; } = 1;
        public int CompletedRounds { get; private set; }
        public int InputIndex { get; private set; }
        public double TimeLimit => Sequence.Count * SecondsPerKey;
        public double TimeLeft { get; private set; }
        public bool IsTransitioning => transitionLeft > 0;
        public DanceStep Result { get; private set; }

        // 새 시도는 1라운드부터 시작합니다. 시드를 지정하면 검증 시 같은 순서를 재현할 수 있습니다.
        public DanceModel(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            GenerateSequence();
        }
        public static int SequenceLength(int round)
        {
            if (round < 1 || round > Goal) throw new ArgumentOutOfRangeException(nameof(round));
            return round <= 3 ? 4 : round <= 6 ? 6 : round <= 9 ? 8 : 10;
        }
        private void GenerateSequence()
        {
            var sequence = new DanceDirection[SequenceLength(Round)];
            // 같은 방향이 연속으로 나올 수 있으며, 각 칸은 독립적으로 뽑습니다.
            for (int i = 0; i < sequence.Length; i++) sequence[i] = (DanceDirection)random.Next(4);
            Sequence = Array.AsReadOnly(sequence); InputIndex = 0; TimeLeft = TimeLimit;
        }

        // 완료된 줄의 추가 입력을 잠가 중복 라운드 증가나 다음 줄의 첫 입력 소비를 막습니다.
        public DanceStep Input(DanceDirection direction)
        {
            if (Result != DanceStep.Running || IsTransitioning) return Result;
            if (direction != Sequence[InputIndex]) return Result = DanceStep.WrongKey;
            if (++InputIndex == Sequence.Count)
            {
                CompletedRounds++;
                if (CompletedRounds == Goal) Result = DanceStep.Success;
                else transitionLeft = RoundDelay;
            }
            return Result;
        }

        // 대기·일시정지에서는 공통 컨트롤러가 호출하지 않습니다. 라운드 사이 0.4초도 같은 모델 시간으로 정지됩니다.
        public DanceStep Step(double deltaTime)
        {
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Result != DanceStep.Running) return Result;
            if (IsTransitioning)
            {
                transitionLeft = Math.Max(0, transitionLeft - deltaTime);
                // 새 줄에는 전체 제한 시간을 부여하며 이전 프레임의 초과 시간이나 입력을 넘기지 않습니다.
                if (!IsTransitioning) { Round++; GenerateSequence(); }
                return Result;
            }
            TimeLeft = Math.Max(0, TimeLeft - deltaTime);
            if (TimeLeft <= 0) Result = DanceStep.Timeout;
            return Result;
        }
    }
}
