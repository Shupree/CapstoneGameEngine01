using System;

namespace GE.MiniGames.Sorting
{
    public enum SortingItem { Box, Bottle, Stone, Leaf, Bolt }
    public enum SortingChoice { Left, Right, Discard }
    public enum SortingStep { Correct, Wrong, Success, AlreadyFinished }

    // 규칙과 출현 순서만 관리합니다. 목숨 차감·시간·씬 전환은 기존 컨트롤러와 메인 시스템의 책임입니다.
    public sealed class SortingModel
    {
        private readonly SortingItem[] items;
        public int Correct { get; private set; }
        public int Target => items.Length;
        public int Remaining => Target - Correct;
        public bool IsFinished { get; private set; }
        public SortingItem Current => items[Math.Min(Correct, Target - 1)];

        // 표시 계층은 현재 물건을 0으로 하는 상대 순서만 조회합니다. 추첨이나 정답 수를
        // 변경하지 않으며 마지막 물건 뒤를 조회하면 false를 반환해 미리보기를 숨길 수 있습니다.
        public bool TryPeek(int offset, out SortingItem item)
        {
            item = default;
            if (offset < 0 || offset >= Remaining) return false;
            item = items[Correct + offset];
            return true;
        }

        public SortingModel(int target = 30, int? seed = null)
        {
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target));
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            items = new SortingItem[target];
            // 그룹을 먼저 균등 추첨한 뒤 더미만 세 종류 중 추첨해 Space 비중이 3/5로 치우치지 않게 합니다.
            for (int i = 0; i < items.Length; i++)
            {
                int group = random.Next(3);
                items[i] = group == 0 ? SortingItem.Box : group == 1 ? SortingItem.Bottle
                    : (SortingItem)((int)SortingItem.Stone + random.Next(3));
            }
        }

        public static SortingChoice RequiredChoice(SortingItem item)
        {
            return item == SortingItem.Box ? SortingChoice.Left
                : item == SortingItem.Bottle ? SortingChoice.Right : SortingChoice.Discard;
        }

        // 한 호출은 맨 앞 오브젝트 하나만 처리합니다. 오답으로 종료되면 새 모델을 만들 때까지 진행할 수 없습니다.
        public SortingStep Submit(SortingChoice choice)
        {
            if (IsFinished) return SortingStep.AlreadyFinished;
            if (choice != RequiredChoice(Current))
            {
                IsFinished = true;
                return SortingStep.Wrong;
            }
            Correct++;
            if (Correct == Target)
            {
                IsFinished = true;
                return SortingStep.Success;
            }
            return SortingStep.Correct;
        }
    }
}
