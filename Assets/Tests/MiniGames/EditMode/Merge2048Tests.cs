using System;
using System.Linq;
using System.Reflection;
using GE.MiniGames.Merge2048;
using NUnit.Framework;

namespace GE.MiniGames.Tests
{
    // 2048의 병합 경계와 생성 규칙을 검사합니다. 고정 보드 주입은 검증 코드에만 두고 런타임 API는 늘리지 않습니다.
    public sealed class Merge2048Tests
    {
        private static Merge2048Model Board(params int[] values)
        {
            var model = new Merge2048Model(19);
            var cells = (int[])typeof(Merge2048Model).GetField("cells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
            Array.Clear(cells, 0, cells.Length); Array.Copy(values, cells, values.Length); return model;
        }
        [Test]
        public void Start_OnlyTwoTwos_RepeatableAndVariedAcrossSeeds()
        {
            bool varied = false; var first = new Merge2048Model(0).Tiles;
            for (int seed = 0; seed < 64; seed++)
            {
                var model = new Merge2048Model(seed);
                Assert.That(model.Tiles.Count(x => x == 2), Is.EqualTo(2)); Assert.That(model.Tiles.Sum(), Is.EqualTo(4));
                Assert.That(model.Tiles, Is.EqualTo(new Merge2048Model(seed).Tiles));
                Assert.That(model.MoveCount, Is.Zero); Assert.That(model.Result, Is.EqualTo(MergeStep.Running));
                varied |= !first.SequenceEqual(model.Tiles);
            }
            Assert.That(varied, Is.True);
        }
        [TestCase(MergeDirection.Left, 4, 7, 4)]
        [TestCase(MergeDirection.Right, 4, 7, 7)]
        [TestCase(MergeDirection.Up, 5, 13, 1)]
        [TestCase(MergeDirection.Down, 1, 9, 13)]
        public void FourDirections_CompressAndMergeTowardPressedEdge(MergeDirection direction, int a, int b, int destination)
        {
            var values = new int[16]; values[a] = values[b] = 2; var model = Board(values);
            Assert.That(model.Move(direction), Is.True); Assert.That(model.Tiles[destination], Is.EqualTo(4));
            Assert.That(model.Tiles.Count(x => x == 2), Is.EqualTo(1)); Assert.That(model.Tiles.Sum(), Is.EqualTo(6));
        }
        [TestCase(2, 2, 2, 2, 4, 4, 0, 0)]
        [TestCase(2, 2, 4, 0, 4, 4, 0, 0)]
        [TestCase(2, 2, 2, 0, 4, 2, 0, 0)]
        [TestCase(2, 0, 2, 2, 4, 2, 0, 0)]
        [TestCase(4, 4, 8, 8, 8, 16, 0, 0)]
        public void RowRules_ConsumeEachPairOnce(int a, int b, int c, int d, int x, int y, int z, int w)
        {
            var model = Board(a, b, c, d); model.Move(MergeDirection.Left);
            int[] expected = new int[16]; expected[0] = x; expected[1] = y; expected[2] = z; expected[3] = w;
            int spawned = 0;
            for (int i = 0; i < 16; i++)
            {
                if (model.Tiles[i] == expected[i]) continue;
                Assert.That(expected[i], Is.Zero); Assert.That(model.Tiles[i], Is.EqualTo(2)); spawned++;
            }
            Assert.That(spawned, Is.EqualTo(1)); Assert.That(model.MoveCount, Is.EqualTo(1));
        }
        [Test]
        public void IneffectiveMove_DoesNotSpawnCountOrConsumeRandomness()
        {
            var model = Board(2); var original = model.Tiles.ToArray(); var control = Board(2);
            for (int i = 0; i < 8; i++) Assert.That(model.Move(MergeDirection.Left), Is.False);
            Assert.That(model.Tiles, Is.EqualTo(original)); Assert.That(model.MoveCount, Is.Zero);
            model.Move(MergeDirection.Down); control.Move(MergeDirection.Down);
            Assert.That(model.Tiles, Is.EqualTo(control.Tiles)); Assert.That(model.Tiles.Sum(), Is.EqualTo(4));
        }
        [Test]
        public void FullBoard_WithHorizontalMerge_IsNotLostByAnIneffectiveUpMove()
        {
            var model = Board(2,4,8,16, 4,8,16,2, 8,16,2,8, 16,2,4,4);
            Assert.That(model.Move(MergeDirection.Up), Is.False); Assert.That(model.Result, Is.EqualTo(MergeStep.Running));
            Assert.That(model.Move(MergeDirection.Left), Is.True);
        }
        [Test]
        public void FullBlockedBoard_FailsAndStopsAcceptingMoves()
        {
            var model = Board(2,4,2,4, 4,2,4,2, 2,4,2,4, 4,2,4,2); var original = model.Tiles.ToArray();
            Assert.That(model.Move(MergeDirection.Left), Is.False); Assert.That(model.Result, Is.EqualTo(MergeStep.NoMoves));
            Assert.That(model.Move(MergeDirection.Up), Is.False); Assert.That(model.Tiles, Is.EqualTo(original));
        }
        [Test]
        public void SpawnThatFillsLastCell_ImmediatelyDetectsFailure()
        {
            var model = Board(2,4,8,16, 4,8,16,2, 8,16,2,4, 4,8,16,0);
            Assert.That(model.Move(MergeDirection.Right), Is.True);
            Assert.That(model.Tiles[12], Is.EqualTo(2)); Assert.That(model.Result, Is.EqualTo(MergeStep.NoMoves));
            Assert.That(model.MoveCount, Is.EqualTo(1));
        }
        [Test]
        public void Goal128_WinsBeforeSpawning_AndFreezesBoard()
        {
            var model = Board(64, 64); model.Move(MergeDirection.Left);
            Assert.That(model.Result, Is.EqualTo(MergeStep.Success)); Assert.That(model.HighestTile, Is.EqualTo(128));
            Assert.That(model.Tiles.Count(x => x != 0), Is.EqualTo(1)); Assert.That(model.Tiles.Sum(), Is.EqualTo(128));
            var original = model.Tiles.ToArray(); Assert.That(model.Move(MergeDirection.Right), Is.False);
            Assert.That(model.Tiles, Is.EqualTo(original)); Assert.That(model.MoveCount, Is.EqualTo(1));
        }
        [Test]
        public void RandomLegalGames_ConserveMergeValuesAndSpawnOnlyOneTwo()
        {
            var input = new Random(47);
            for (int seed = 0; seed < 20; seed++)
            {
                var model = new Merge2048Model(seed);
                for (int turn = 0; turn < 200 && model.Result == MergeStep.Running; turn++)
                {
                    int sum = model.Tiles.Sum(), moves = model.MoveCount;
                    bool changed = model.Move((MergeDirection)input.Next(4));
                    Assert.That(model.Tiles.Sum(), Is.EqualTo(sum + (changed && model.Result != MergeStep.Success ? 2 : 0)));
                    Assert.That(model.MoveCount, Is.EqualTo(moves + (changed ? 1 : 0)));
                    Assert.That(model.Tiles.All(x => x == 0 || x >= 2 && (x & (x - 1)) == 0), Is.True);
                }
            }
        }
    }
}