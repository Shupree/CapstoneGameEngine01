using System;
using System.Collections.Generic;

namespace GE.MiniGames.Stack
{
    public enum StackStep { Running, Success, Missed }
    public readonly struct StackBlock
    {
        public readonly float X, Y, Width;
        public StackBlock(float x, float y, float width) { X = x; Y = y; Width = width; }
    }
    // 위치 확정·겹침 판정만 담당합니다. 떨어지는 연출 없이 입력 순간 바로 쌓는 원본 규칙입니다.
    public sealed class StackModel
    {
        private readonly Random random;
        private readonly List<StackBlock> blocks = new List<StackBlock>();
        private float speed;
        private int direction = 1;
        public IReadOnlyList<StackBlock> Blocks { get; }
        public StackBlock Current { get; private set; }
        public int Count => blocks.Count - 1;
        public StackStep Result { get; private set; }
        public StackModel(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random(); Blocks = blocks.AsReadOnly();
            blocks.Add(new StackBlock(165, 270, 70)); Spawn();
        }
        private void Spawn()
        {
            var top = blocks[blocks.Count - 1]; Current = new StackBlock(0, top.Y - 25, top.Width);
            speed = 180 + (float)random.NextDouble() * 150 + blocks.Count * 25; direction = 1;
        }
        public void Step(float dt)
        {
            if (Result != StackStep.Running || dt <= 0) return;
            float x = Current.X + speed * direction * dt;
            if (x <= 0) { x = 0; direction = 1; }
            if (x + Current.Width >= 400) { x = 400 - Current.Width; direction = -1; }
            Current = new StackBlock(x, Current.Y, Current.Width);
        }
        // 조금이라도 겹치면 원본처럼 최소 14px를 보장합니다. 경계만 맞닿은 경우는 실패입니다.
        public void Drop()
        {
            if (Result != StackStep.Running) return;
            var top = blocks[blocks.Count - 1];
            float left = Math.Max(Current.X, top.X), right = Math.Min(Current.X + Current.Width, top.X + top.Width);
            float width = right - left;
            if (width <= 0) { Result = StackStep.Missed; return; }
            if (width < 14) { width = 14; if (left + width > top.X + top.Width) left = top.X + top.Width - width; }
            blocks.Add(new StackBlock(left, Current.Y, width));
            if (Count >= 7) Result = StackStep.Success; else Spawn();
        }
    }
}
