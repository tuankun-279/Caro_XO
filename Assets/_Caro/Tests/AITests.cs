using System;
using Caro.AI;
using Caro.Core;
using NUnit.Framework;

namespace Caro.Tests
{
    public class AITests
    {
        private static Cell C(int x, int y) => new Cell(x, y);

        private static readonly Difficulty[] AllLevels = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
        private static readonly Difficulty[] StrongLevels = { Difficulty.Medium, Difficulty.Hard };

        /// <summary>Builds a position: the first player and the other side alternate through the list.</summary>
        private static CaroGame Setup(Cell[] xs, Cell[] os, Piece first = Piece.X)
        {
            var g = new CaroGame(first);
            var a = first == Piece.X ? xs : os;
            var b = first == Piece.X ? os : xs;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                if (i < a.Length) Assert.AreEqual(PlaceResult.Ok, g.TryPlace(a[i]));
                if (i < b.Length) Assert.AreEqual(PlaceResult.Ok, g.TryPlace(b[i]));
            }
            return g;
        }

        private static CaroAI MakeAI(Difficulty d, int seed = 1) =>
            new CaroAI(d, seed) { TimeBudgetMs = 300, MaxDepth = 4, VcfDepth = 8 };

        // ---------- All levels ----------

        [Test]
        public void EmptyBoard_PlaysCenter([ValueSource(nameof(AllLevels))] Difficulty d)
        {
            Assert.AreEqual(C(7, 7), MakeAI(d).ChooseMove(new CaroGame().Board, Piece.X));
        }

        [Test]
        public void AlwaysLegal_InRandomPositions([ValueSource(nameof(AllLevels))] Difficulty d)
        {
            var rng = new Random(42);
            for (int round = 0; round < 15; round++)
            {
                var g = new CaroGame();
                int moves = rng.Next(4, 30);
                for (int i = 0; i < moves && !g.IsOver; i++)
                    g.TryPlace(C(rng.Next(g.Board.MinX, g.Board.MaxX + 1), rng.Next(g.Board.MinY, g.Board.MaxY + 1)));
                if (g.IsOver) continue;

                var move = MakeAI(d, round).ChooseMove(g.Board, g.Turn);
                Assert.IsTrue(g.Board.InBounds(move), $"Off the board: {move}");
                Assert.IsTrue(g.Board.IsEmpty(move), $"Cell already taken: {move}");
            }
        }

        // ---------- Medium & Hard: never blunder ----------

        [Test]
        public void TakesImmediateWin([ValueSource(nameof(StrongLevels))] Difficulty d)
        {
            var g = Setup(new[] { C(3, 7), C(4, 7), C(5, 7), C(6, 7) }, new[] { C(2, 7), C(10, 2), C(10, 4), C(12, 12) });
            Assert.AreEqual(C(7, 7), MakeAI(d).ChooseMove(g.Board, Piece.X));
        }

        [Test]
        public void TakesWinOverBlocking([ValueSource(nameof(StrongLevels))] Difficulty d)
        {
            var g = Setup(
                new[] { C(3, 3), C(4, 3), C(5, 3), C(6, 3), C(12, 12) },
                new[] { C(3, 10), C(4, 10), C(5, 10), C(6, 10) });
            var move = MakeAI(d).ChooseMove(g.Board, Piece.O);
            g.TryPlace(move);
            Assert.AreEqual(Piece.O, g.Winner, $"AI played {move} instead of winning");
        }

        [Test]
        public void BlocksOpponentFour([ValueSource(nameof(StrongLevels))] Difficulty d)
        {
            var g = Setup(new[] { C(8, 4), C(8, 5), C(8, 6), C(8, 7) }, new[] { C(8, 3), C(2, 2), C(2, 12) });
            Assert.AreEqual(C(8, 8), MakeAI(d).ChooseMove(g.Board, Piece.O));
        }

        [Test]
        public void BlocksBrokenFour([ValueSource(nameof(StrongLevels))] Difficulty d)
        {
            var g = Setup(new[] { C(4, 6), C(5, 6), C(7, 6), C(8, 6) }, new[] { C(1, 1), C(1, 13), C(13, 1) });
            Assert.AreEqual(C(6, 6), MakeAI(d).ChooseMove(g.Board, Piece.O));
        }

        [Test]
        public void BlocksOpenThree([ValueSource(nameof(StrongLevels))] Difficulty d)
        {
            var g = Setup(new[] { C(5, 5), C(6, 6), C(7, 7) }, new[] { C(1, 12), C(13, 2) });
            for (int seed = 1; seed <= 10; seed++)
            {
                var move = MakeAI(d, seed).ChooseMove(g.Board, Piece.O);
                CollectionAssert.Contains(new[] { C(4, 4), C(8, 8), C(3, 3), C(9, 9) }, move, $"seed {seed}: AI played {move}");
            }
        }

        [Test]
        public void Hard_FindsDoubleFourWin()
        {
            // X at (8,5) makes a horizontal four and a vertical four at once → O cannot block both
            var g = Setup(
                new[] { C(5, 5), C(6, 5), C(7, 5), C(8, 6), C(8, 7), C(8, 8) },
                new[] { C(4, 5), C(8, 9), C(1, 13), C(13, 1), C(1, 1), C(13, 13) });
            Assert.AreEqual(Piece.X, g.Turn);
            Assert.AreEqual(C(8, 5), MakeAI(Difficulty.Hard).ChooseMove(g.Board, Piece.X));
        }

        // ---------- Easy: makes mistakes (measured as rates) ----------

        private static int CountOver(int runs, Func<int, bool> trial)
        {
            int n = 0;
            for (int i = 0; i < runs; i++) if (trial(i)) n++;
            return n;
        }

        [Test]
        public void Easy_SometimesMissesWin()
        {
            var g = Setup(new[] { C(3, 7), C(4, 7), C(5, 7), C(6, 7) }, new[] { C(2, 7), C(10, 2), C(10, 4), C(12, 12) });
            int took = CountOver(200, s => MakeAI(Difficulty.Easy, s).ChooseMove(g.Board, Piece.X) == C(7, 7));
            TestContext.WriteLine($"Easy took the win {took}/200 times");
            Assert.That(took, Is.InRange(130, 190), "Easy should take the win about 80% of the time");
        }

        [Test]
        public void Easy_SometimesMissesBlock()
        {
            var g = Setup(new[] { C(8, 4), C(8, 5), C(8, 6), C(8, 7) }, new[] { C(8, 3), C(2, 2), C(2, 12) });
            int blocked = CountOver(200, s => MakeAI(Difficulty.Easy, s).ChooseMove(g.Board, Piece.O) == C(8, 8));
            TestContext.WriteLine($"Easy blocked the four {blocked}/200 times");
            Assert.That(blocked, Is.InRange(90, 160), "Easy should block a four about 60% of the time");
        }

        [Test]
        public void Easy_RarelyBlocksOpenThree()
        {
            var g = Setup(new[] { C(5, 5), C(6, 6), C(7, 7) }, new[] { C(1, 12), C(13, 2) });
            int blocked = CountOver(200, s =>
            {
                var m = MakeAI(Difficulty.Easy, s).ChooseMove(g.Board, Piece.O);
                return m == C(4, 4) || m == C(8, 8);
            });
            TestContext.WriteLine($"Easy blocked the open three {blocked}/200 times");
            Assert.Less(blocked, 100, "Easy should often miss open threes");
        }

        // ---------- Matches: the gap between levels ----------

        /// <summary>Simulated "novice": only takes immediate wins and blocks fours; otherwise plays randomly next to a piece.</summary>
        private sealed class NoviceBot
        {
            private readonly Random _rng;
            public NoviceBot(int seed) { _rng = new Random(seed); }

            public Cell Choose(CaroGame g)
            {
                var b = g.Board;
                var me = g.Turn; var opp = me.Opponent();
                var near = new System.Collections.Generic.List<Cell>();
                Cell? block = null;
                for (int y = b.MinY; y <= b.MaxY; y++)
                for (int x = b.MinX; x <= b.MaxX; x++)
                {
                    var c = new Cell(x, y);
                    if (!b.IsEmpty(c)) continue;
                    if (WouldFive(b, c, me)) return c;
                    if (block == null && WouldFive(b, c, opp)) block = c;
                    if (HasNeighbor(b, c)) near.Add(c);
                }
                if (block.HasValue) return block.Value;
                return near.Count > 0 ? near[_rng.Next(near.Count)] : new Cell(7, 7);
            }

            private static bool HasNeighbor(Board b, Cell c)
            {
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && b.Get(new Cell(c.X + dx, c.Y + dy)) != Piece.None) return true;
                return false;
            }

            private static bool WouldFive(Board b, Cell c, Piece p)
            {
                int[] dx = { 1, 0, 1, 1 }, dy = { 0, 1, 1, -1 };
                for (int d = 0; d < 4; d++)
                {
                    int n = 1;
                    for (int k = 1; b.Get(new Cell(c.X + dx[d] * k, c.Y + dy[d] * k)) == p; k++) n++;
                    for (int k = 1; b.Get(new Cell(c.X - dx[d] * k, c.Y - dy[d] * k)) == p; k++) n++;
                    if (n >= 5) return true;
                }
                return false;
            }
        }

        /// <summary>Plays one game (board can grow). Returns true if `a` wins, false if it loses, null on a draw.</summary>
        private static bool? Match(Func<CaroGame, Cell> a, Func<CaroGame, Cell> b, bool aIsX)
        {
            var g = new CaroGame(Piece.X);
            for (int i = 0; i < 400 && !g.IsOver; i++)
            {
                bool aTurn = (g.Turn == Piece.X) == aIsX;
                var move = aTurn ? a(g) : b(g);
                Assert.AreEqual(PlaceResult.Ok, g.TryPlace(move), $"Illegal move {move}");
            }
            if (g.Winner == Piece.None) return null;
            return (g.Winner == Piece.X) == aIsX;
        }

        private static (int w, int l, int d) Series(int games, Func<int, Func<CaroGame, Cell>> makeA, Func<int, Func<CaroGame, Cell>> makeB)
        {
            int w = 0, l = 0, dr = 0;
            for (int i = 0; i < games; i++)
            {
                var r = Match(makeA(i), makeB(i), i % 2 == 0);
                if (r == true) w++; else if (r == false) l++; else dr++;
            }
            return (w, l, dr);
        }

        private static Func<int, Func<CaroGame, Cell>> Bot(Difficulty d, int budget = 150) =>
            seed => { var ai = new CaroAI(d, 1000 + seed * 7 + (int)d) { TimeBudgetMs = budget, MaxDepth = 4, VcfDepth = 8 }; return g => ai.ChooseMove(g.Board, g.Turn); };

        private static Func<int, Func<CaroGame, Cell>> Novice =>
            seed => { var n = new NoviceBot(5000 + seed); return n.Choose; };

        [Test, Explicit("Slow: tournament between levels"), Category("Tournament")]
        public void Medium_CrushesEasy()
        {
            var (w, l, d) = Series(40, Bot(Difficulty.Medium), Bot(Difficulty.Easy));
            TestContext.WriteLine($"Medium vs Easy: {w}W {l}L {d}D / 40");
            Assert.GreaterOrEqual(w, 38, "Medium must beat Easy ≥ 95%");
        }

        [Test, Explicit("Slow: tournament between levels"), Category("Tournament")]
        public void Hard_CrushesMedium()
        {
            // In game Hard thinks up to 1.4 s; 400 ms here keeps the test faster while staying realistic
            var (w, l, d) = Series(12, Bot(Difficulty.Hard, 400), Bot(Difficulty.Medium));
            TestContext.WriteLine($"Hard vs Medium: {w}W {l}L {d}D / 12");
            Assert.GreaterOrEqual(w, 11, "Hard must beat Medium ≥ 90%");
        }

        [Test, Explicit("Slow: tournament between levels"), Category("Tournament")]
        public void Easy_IsBeatableByNovice()
        {
            var (w, l, d) = Series(60, Bot(Difficulty.Easy), Novice);
            TestContext.WriteLine($"Easy vs Novice: {w}W {l}L {d}D / 60");
            Assert.That(w, Is.InRange(18, 45), "Easy should win about half of its games against a novice");
        }
    }
}
