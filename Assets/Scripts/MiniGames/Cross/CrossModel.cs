using System;
using System.Collections.Generic;

namespace GE.MiniGames.Cross
{
    public enum CrossStep { Running, Collision, Success }

    // 좌표는 HTML과 동일한 좌상단 원점의 픽셀 단위입니다. View에서만 Unity UI 좌표로 바꿉니다.
    public sealed class CrossCar
    {
        public int Row { get; }
        public float Speed { get; }
        public int Direction { get; }
        public float X { get; internal set; }

        internal CrossCar(int row, float x, float speed, int direction)
        { Row = row; X = x; Speed = speed; Direction = direction; }
    }

    // 차량 생성·격자 이동·충돌만 담당합니다. 시간 공급, 정지, 결과 통지는 공통 컨트롤러의 책임입니다.
    public sealed class CrossModel
    {
        public const int CellSize = 50, Columns = 10, Rows = 12, RoadRows = 10;
        public const int Width = Columns * CellSize, Height = Rows * CellSize;
        public const float PlayerSize = 36, CarWidth = 75, CarHeight = 36;
        private readonly List<CrossCar> cars = new List<CrossCar>();
        public IReadOnlyList<CrossCar> Cars => cars;
        public int PlayerColumn { get; private set; } = Columns / 2;
        public int PlayerRow { get; private set; } = Rows - 1;
        // 차선에 진입하는 것이 아니라 그 차선을 벗어나야 통과로 셉니다. 성공은 반드시 0행 도착입니다.
        public int CrossedLanes => Math.Max(0, RoadRows - PlayerRow);
        public float PlayerX => PlayerColumn * CellSize + (CellSize - PlayerSize) / 2;
        public float PlayerY => PlayerRow * CellSize + (CellSize - PlayerSize) / 2;
        public CrossStep Result { get; private set; }

        public CrossModel(int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            for (int row = 1; row <= RoadRows; row++)
            {
                int direction = random.NextDouble() > .5 ? 1 : -1;
                // HTML의 (1.2~3.2)*0.8 px/16.66ms를 Unity의 초 단위 속도로 변환합니다.
                float speed = (float)((random.NextDouble() * 2 + 1.2) * .8 / .01666);
                // HTML 주석과 달리 실제 코드의 차량 수는 차선당 1~2대입니다.
                int count = random.Next(1, 3);
                for (int i = 0; i < count; i++)
                    cars.Add(new CrossCar(row, i * (Width / (float)count) + (float)random.NextDouble() * CellSize, speed, direction));
            }
        }

        // 한 입력에 상하좌우 한 칸만 이동합니다. 경계를 넘거나 종료 후 들어온 입력은 무시합니다.
        public CrossStep Move(int dx, int dy)
        {
            if (Result != CrossStep.Running || Math.Abs(dx) + Math.Abs(dy) != 1) return Result;
            int x = PlayerColumn + dx, y = PlayerRow + dy;
            if (x < 0 || x >= Columns || y < 0 || y >= Rows) return Result;
            PlayerColumn = x; PlayerRow = y;
            if (PlayerRow == 0) Result = CrossStep.Success;
            else if (HasCollision()) Result = CrossStep.Collision;
            return Result;
        }

        public bool HasCollision()
        {
            foreach (var car in cars)
                if (Overlaps(car, car.X, car.X)) return true;
            return false;
        }

        // 이동 경로까지 판정하여 낮은 프레임률의 통과 누락을 막습니다. 화면 반대편 래핑은 순간이동이므로
        // 출발점~도착점을 가로지르는 충돌로 취급하지 않고, 화면 밖으로 나간 뒤에만 위치를 재설정합니다.
        public CrossStep Step(float deltaTime)
        {
            if (Result != CrossStep.Running) return Result;
            if (HasCollision()) return Result = CrossStep.Collision;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return Result;
            while (deltaTime > 0)
            {
                float dt = Math.Min(deltaTime, 1f / 120f);
                bool collision = false;
                foreach (var car in cars)
                {
                    float before = car.X;
                    car.X += car.Speed * car.Direction * dt;
                    collision |= Overlaps(car, before, car.X);
                    if (car.Direction == 1 && car.X > Width) car.X = -CarWidth;
                    else if (car.Direction == -1 && car.X + CarWidth < 0) car.X = Width;
                }
                if (collision) return Result = CrossStep.Collision;
                deltaTime -= dt;
            }
            return Result;
        }

        // 같은 차선은 Y 범위가 반드시 겹치므로 X의 AABB만 검사합니다. 가장자리만 맞닿으면 HTML처럼 통과합니다.
        private bool Overlaps(CrossCar car, float before, float after)
        {
            return car.Row == PlayerRow && PlayerX < Math.Max(before, after) + CarWidth
                && PlayerX + PlayerSize > Math.Min(before, after);
        }
    }
}
