using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace GE.MiniGames.Snake
{
    public enum SnakeStepResult
    {
        Moved,
        AteApple,
        Won,
        HitWall,
        HitBody,
        AlreadyFinished
    }

    /// <summary>
    /// Grid rules only. The owning game controls time, input, presentation and results.
    /// Coordinates start at the bottom left; Body[0] is the head.
    /// </summary>
    public sealed class SnakeModel
    {
        private readonly List<Vector2Int> body = new List<Vector2Int>();
        private readonly ReadOnlyCollection<Vector2Int> bodyView;
        private readonly int initialLength;
        private readonly int? seed;
        private System.Random random;
        private Vector2Int pendingDirection;
        private bool hasPendingDirection;

        public IReadOnlyList<Vector2Int> Body => bodyView;
        public Vector2Int Direction { get; private set; }
        public Vector2Int Apple { get; private set; }
        public bool HasApple { get; private set; }
        public bool IsFinished { get; private set; }
        public int ApplesEaten { get; private set; }
        public int Width { get; }
        public int Height { get; }
        public int TargetApples { get; }

        public SnakeModel(int width, int height, int targetApples, int initialLength = 3, int? seed = null)
        {
            if (width < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "The board must be at least two cells wide.");
            if (height < 2 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(height), "The board must have a valid cell count and at least two rows.");
            if (initialLength < 1)
                throw new ArgumentOutOfRangeException(nameof(initialLength), "The snake must have at least one segment.");

            // Leave one column in front of even the longest allowed starting snake.
            this.initialLength = Math.Min(initialLength, width - 1);
            if (targetApples < 1 || targetApples > width * height - this.initialLength)
                throw new ArgumentOutOfRangeException(nameof(targetApples), "The goal must fit in the board's free cells.");

            Width = width;
            Height = height;
            TargetApples = targetApples;
            this.seed = seed;
            bodyView = body.AsReadOnly();
            Reset();
        }

        /// <summary>A supplied seed also makes retries reproducible.</summary>
        public void Reset()
        {
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            body.Clear();
            int headX = Math.Max(Math.Min(Width / 2, Width - 2), initialLength - 1);
            int headY = Height / 2;
            for (int i = 0; i < initialLength; i++)
                body.Add(new Vector2Int(headX - i, headY));

            Direction = Vector2Int.right;
            ApplesEaten = 0;
            IsFinished = false;
            HasApple = false;
            Apple = default;
            ClearPendingInput();
            SpawnApple();
        }

        /// <summary>
        /// Accept at most one perpendicular turn per movement tick. Repeated/current
        /// directions never consume that slot, and a quick pair of keys cannot reverse.
        /// </summary>
        public bool QueueDirection(Vector2Int direction)
        {
            if (IsFinished || hasPendingDirection || !IsCardinal(direction) ||
                direction == Direction || direction == -Direction)
                return false;

            pendingDirection = direction;
            hasPendingDirection = true;
            return true;
        }

        public void ClearPendingInput()
        {
            pendingDirection = default;
            hasPendingDirection = false;
        }

        public SnakeStepResult Step()
        {
            if (IsFinished)
                return SnakeStepResult.AlreadyFinished;

            if (hasPendingDirection)
                Direction = pendingDirection;
            ClearPendingInput();

            Vector2Int next = body[0] + Direction;
            if (next.x < 0 || next.x >= Width || next.y < 0 || next.y >= Height)
                return Finish(SnakeStepResult.HitWall);

            bool grows = HasApple && next == Apple;
            // The tail vacates during an ordinary move and is therefore a legal cell.
            int occupiedCount = grows ? body.Count : body.Count - 1;
            for (int i = 0; i < occupiedCount; i++)
            {
                if (body[i] == next)
                    return Finish(SnakeStepResult.HitBody);
            }

            body.Insert(0, next);
            if (!grows)
            {
                body.RemoveAt(body.Count - 1);
                return SnakeStepResult.Moved;
            }

            ApplesEaten++;
            HasApple = false;
            if (ApplesEaten >= TargetApples)
                return Finish(SnakeStepResult.Won);

            SpawnApple();
            return SnakeStepResult.AteApple;
        }

        private SnakeStepResult Finish(SnakeStepResult result)
        {
            IsFinished = true;
            ClearPendingInput();
            return result;
        }

        private void SpawnApple()
        {
            int freeCount = Width * Height - body.Count;
            if (freeCount <= 0)
            {
                HasApple = false;
                return;
            }

            // Pick a free-cell index, then visit cells once. No rejection loop that
            // becomes slow (or infinite) as the board fills up.
            int selected = random.Next(freeCount);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (body.Contains(cell))
                        continue;
                    if (selected-- != 0)
                        continue;

                    Apple = cell;
                    HasApple = true;
                    return;
                }
            }
        }

        private static bool IsCardinal(Vector2Int direction)
        {
            return direction == Vector2Int.up || direction == Vector2Int.down ||
                   direction == Vector2Int.left || direction == Vector2Int.right;
        }
    }
}
