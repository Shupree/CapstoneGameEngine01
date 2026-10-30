using System;
using System.Collections.Generic;

namespace GE.MiniGames.Merge2048
{
    public enum MergeDirection { Up, Left, Down, Right }
    public enum MergeStep { Running, Success, NoMoves }

    // 좌상단부터 행 우선으로 4×4 보드를 보관합니다. 입력·일시정지·외부 결과 통지는 공통 컨트롤러의 책임입니다.
    public sealed class Merge2048Model
    {
        public const int Size = 4, Goal = 128;
        private readonly int[] cells = new int[Size * Size];
        private readonly Random random;
        public IReadOnlyList<int> Tiles { get; }
        public int MoveCount { get; private set; }
        public MergeStep Result { get; private set; }
        public int HighestTile
        {
            get { int highest = 0; foreach (int value in cells) highest = Math.Max(highest, value); return highest; }
        }

        // 시작·재시도마다 서로 다른 빈칸 두 곳에 2만 생성합니다. 고정 시드는 로컬 검증용입니다.
        public Merge2048Model(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            Tiles = Array.AsReadOnly(cells);
            SpawnTile(); SpawnTile();
        }

        // 이동 방향의 앞쪽부터 압축→인접 쌍 병합→압축합니다. 병합한 쌍을 건너뛰어 새 타일의 연쇄 병합을 막습니다.
        public bool Move(MergeDirection direction)
        {
            if ((int)direction < 0 || (int)direction > 3) throw new ArgumentOutOfRangeException(nameof(direction));
            if (Result != MergeStep.Running) return false;
            bool changed = false;
            var packed = new int[Size]; var merged = new int[Size];
            for (int line = 0; line < Size; line++)
            {
                int count = 0, written = 0;
                Array.Clear(merged, 0, Size);
                for (int offset = 0; offset < Size; offset++)
                {
                    int value = cells[Index(direction, line, offset)];
                    if (value != 0) packed[count++] = value;
                }
                for (int i = 0; i < count; i++)
                {
                    int value = packed[i];
                    if (i + 1 < count && packed[i + 1] == value) { value *= 2; i++; }
                    merged[written++] = value;
                }
                for (int offset = 0; offset < Size; offset++)
                {
                    int index = Index(direction, line, offset);
                    changed |= cells[index] != merged[offset]; cells[index] = merged[offset];
                }
            }
            if (changed) MoveCount++;
            // 128을 만든 순간 종료하며 성공 이동 뒤에는 새 타일을 추가하지 않습니다.
            if (HighestTile >= Goal) { Result = MergeStep.Success; return changed; }
            if (changed) SpawnTile();
            // 가득 차도 인접한 같은 숫자가 있으면 계속할 수 있습니다. 무효 입력으로는 생성/횟수 증가가 없습니다.
            if (!HasAvailableMove()) Result = MergeStep.NoMoves;
            return changed;
        }

        private static int Index(MergeDirection direction, int line, int offset)
        {
            switch (direction)
            {
                case MergeDirection.Left: return line * Size + offset;
                case MergeDirection.Right: return line * Size + Size - 1 - offset;
                case MergeDirection.Up: return offset * Size + line;
                default: return (Size - 1 - offset) * Size + line;
            }
        }

        // 모든 빈칸 중 하나를 균등 추첨합니다. 4 타일은 생성하지 않으며 기존 타일을 덮어쓰지 않습니다.
        private void SpawnTile()
        {
            int empty = 0; foreach (int value in cells) if (value == 0) empty++;
            if (empty == 0) return;
            int chosen = random.Next(empty);
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == 0 && chosen-- == 0) { cells[i] = 2; return; }
        }

        private bool HasAvailableMove()
        {
            for (int row = 0; row < Size; row++) for (int column = 0; column < Size; column++)
            {
                int index = row * Size + column, value = cells[index];
                if (value == 0 || column + 1 < Size && value == cells[index + 1]
                    || row + 1 < Size && value == cells[index + Size]) return true;
            }
            return false;
        }
    }
}
