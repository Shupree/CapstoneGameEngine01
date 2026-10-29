using System;
using System.Collections.Generic;

namespace GE.MiniGames.Bomb
{
    public enum BombColor { Red, Blue, Yellow, Green }
    public enum BombStep { Running, Success, WrongWire, Timeout }

    // HTML의 네 색상·독립 셔플·4.7초 규칙만 담당합니다. 시간 공급과 외부 결과 통지는 공통 컨트롤러의 책임입니다.
    public sealed class BombModel
    {
        public const int WireCount = 4;
        public const double TimeLimit = 4.7;
        private readonly BombColor[] wires = { BombColor.Red, BombColor.Blue, BombColor.Yellow, BombColor.Green };
        private readonly BombColor[] sequence = { BombColor.Red, BombColor.Blue, BombColor.Yellow, BombColor.Green };
        private readonly bool[] cut = new bool[WireCount];
        public IReadOnlyList<BombColor> Wires { get; }
        public IReadOnlyList<BombColor> Sequence { get; }
        public IReadOnlyList<bool> CutWires { get; }
        public int SelectedIndex { get; private set; }
        public int CutCount { get; private set; }
        public double TimeLeft { get; private set; } = TimeLimit;
        public BombStep Result { get; private set; }

        // 재시도마다 새 모델을 생성해 선택·절단·시간을 복구합니다. 지정 시드는 로컬 검증용입니다.
        public BombModel(int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            Shuffle(wires, random); Shuffle(sequence, random);
            Wires = Array.AsReadOnly(wires); Sequence = Array.AsReadOnly(sequence); CutWires = Array.AsReadOnly(cut);
        }

        private static void Shuffle(BombColor[] colors, Random random)
        {
            for (int i = colors.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var value = colors[i]; colors[i] = colors[j]; colors[j] = value;
            }
        }

        // 좌우 끝에서 멈추며 이미 자른 전선도 선택할 수 있습니다. 키 반복 방지는 공통 새 눌림 입력을 사용합니다.
        public void Move(int direction)
        {
            if (Result != BombStep.Running) return;
            SelectedIndex = Math.Max(0, Math.Min(WireCount - 1, SelectedIndex + Math.Sign(direction)));
        }

        // 절단된 선은 무시하고, 마지막 올바른 절단에서 즉시 성공합니다. 종료 이후 입력으로 결과를 덮어쓰지 않습니다.
        public BombStep Cut()
        {
            if (Result != BombStep.Running || cut[SelectedIndex]) return Result;
            if (wires[SelectedIndex] != sequence[CutCount]) return Result = BombStep.WrongWire;
            cut[SelectedIndex] = true;
            if (++CutCount == WireCount) Result = BombStep.Success;
            return Result;
        }

        // Playing일 때만 호출하므로 시작 대기·일시정지·결과 화면에서 제한 시간이 줄지 않습니다.
        public BombStep Step(double deltaTime)
        {
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Result != BombStep.Running) return Result;
            TimeLeft = Math.Max(0, TimeLeft - deltaTime);
            if (TimeLeft <= 0) Result = BombStep.Timeout;
            return Result;
        }
    }
}
