using System.Linq;
using Caro.Core;
using NUnit.Framework;

namespace Caro.Tests
{
    public class WinTests
    {
        /// <summary>
        /// Alternates moves: X plays xMoves, O plays oMoves (O plays somewhere harmless).
        /// Stops as soon as the game ends. After the last X move, O has not moved yet (O to play).
        /// </summary>
        private static CaroGame Play(Cell[] xMoves, Cell[] oMoves)
        {
            var g = new CaroGame(Piece.X);
            for (int i = 0; i < xMoves.Length && !g.IsOver; i++)
            {
                Assert.AreEqual(PlaceResult.Ok, g.TryPlace(xMoves[i]), $"X plays {xMoves[i]}");
                if (g.IsOver || i >= oMoves.Length || i == xMoves.Length - 1) break; // after the last X move, leave the turn to O
                Assert.AreEqual(PlaceResult.Ok, g.TryPlace(oMoves[i]), $"O plays {oMoves[i]}");
            }
            return g;
        }

        private static Cell C(int x, int y) => new Cell(x, y);

        // O plays scattered on the top row so it never blocks X
        private static readonly Cell[] OFiller = { C(0, 14), C(2, 14), C(4, 14), C(6, 14), C(8, 14), C(10, 14) };

        [Test]
        public void Horizontal_Five_Wins()
        {
            var g = Play(new[] { C(3, 5), C(4, 5), C(5, 5), C(6, 5), C(7, 5) }, OFiller);
            Assert.IsTrue(g.IsOver);
            Assert.AreEqual(Piece.X, g.Winner);
            CollectionAssert.AreEquivalent(new[] { C(3, 5), C(4, 5), C(5, 5), C(6, 5), C(7, 5) }, g.WinningLine.ToArray());
        }

        [Test]
        public void Vertical_Five_Wins()
        {
            var g = Play(new[] { C(6, 2), C(6, 3), C(6, 4), C(6, 5), C(6, 6) }, OFiller);
            Assert.AreEqual(Piece.X, g.Winner);
            Assert.AreEqual(5, g.WinningLine.Count);
        }

        [Test]
        public void Diagonal_UpRight_Five_Wins()
        {
            var g = Play(new[] { C(2, 2), C(3, 3), C(4, 4), C(5, 5), C(6, 6) }, OFiller);
            Assert.AreEqual(Piece.X, g.Winner);
        }

        [Test]
        public void Diagonal_DownRight_Five_Wins()
        {
            var g = Play(new[] { C(2, 10), C(3, 9), C(4, 8), C(5, 7), C(6, 6) }, OFiller);
            Assert.AreEqual(Piece.X, g.Winner);
        }

        [Test]
        public void Four_InARow_DoesNotWin()
        {
            var g = Play(new[] { C(3, 5), C(4, 5), C(5, 5), C(6, 5) }, OFiller);
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(Piece.None, g.Winner);
            Assert.AreEqual(0, g.WinningLine.Count);
        }

        [Test]
        public void FiveWithGap_DoesNotWin_UntilGapFilled()
        {
            // X X _ X X  → not a win yet; filling the middle gap → win
            var g = Play(new[] { C(3, 5), C(4, 5), C(6, 5), C(7, 5) }, OFiller);
            Assert.IsFalse(g.IsOver);
            g.TryPlace(OFiller[4]);          // O's turn
            g.TryPlace(C(5, 5));             // X fills the gap
            Assert.AreEqual(Piece.X, g.Winner);
            CollectionAssert.AreEquivalent(new[] { C(3, 5), C(4, 5), C(5, 5), C(6, 5), C(7, 5) }, g.WinningLine.ToArray());
        }

        [Test]
        public void Six_InARow_Wins_NoOverlineRule()
        {
            // X X X _ X X → filling the gap makes six, still a win
            var g = Play(new[] { C(1, 5), C(2, 5), C(3, 5), C(5, 5), C(6, 5) }, OFiller);
            Assert.IsFalse(g.IsOver);
            g.TryPlace(OFiller[5]);
            g.TryPlace(C(4, 5));
            Assert.AreEqual(Piece.X, g.Winner);
            Assert.AreEqual(6, g.WinningLine.Count);
        }

        [Test]
        public void BlockedBothEnds_StillWins()
        {
            // O X X X X X O → still a win, there is no blocked-both-ends rule
            var g = new CaroGame(Piece.O);
            g.TryPlace(C(2, 7));             // O blocks the left end
            g.TryPlace(C(3, 7));             // X
            g.TryPlace(C(8, 7));             // O blocks the right end
            g.TryPlace(C(4, 7));
            g.TryPlace(C(0, 0));
            g.TryPlace(C(5, 7));
            g.TryPlace(C(0, 2));
            g.TryPlace(C(6, 7));
            g.TryPlace(C(0, 4));
            g.TryPlace(C(7, 7));
            Assert.AreEqual(Piece.X, g.Winner);
        }

        [Test]
        public void Win_AlongEdge_AndCorner()
        {
            var edge = Play(new[] { C(0, 0), C(0, 1), C(0, 2), C(0, 3), C(0, 4) }, OFiller);
            Assert.AreEqual(Piece.X, edge.Winner, "Column on the left edge");

            var corner = Play(new[] { C(14, 0), C(13, 1), C(12, 2), C(11, 3), C(10, 4) }, OFiller);
            Assert.AreEqual(Piece.X, corner.Winner, "Diagonal from the bottom-right corner");
        }

        [Test]
        public void O_CanWin()
        {
            var g = new CaroGame(Piece.X);
            Cell[] x = { C(0, 14), C(2, 14), C(4, 14), C(6, 14), C(8, 14) };
            Cell[] o = { C(5, 3), C(5, 4), C(5, 5), C(5, 6), C(5, 7) };
            for (int i = 0; i < 5; i++) { g.TryPlace(x[i]); g.TryPlace(o[i]); }
            Assert.AreEqual(Piece.O, g.Winner);
        }

        [Test]
        public void AfterWin_NoMoreMoves_AndEventFiresOnce()
        {
            var g = new CaroGame(Piece.X);
            int ended = 0; Piece winner = Piece.None;
            g.GameEnded += w => { ended++; winner = w; };
            Cell[] x = { C(3, 5), C(4, 5), C(5, 5), C(6, 5), C(7, 5) };
            for (int i = 0; i < 5; i++) { g.TryPlace(x[i]); if (!g.IsOver) g.TryPlace(OFiller[i]); }
            Assert.AreEqual(1, ended);
            Assert.AreEqual(Piece.X, winner);
            Assert.AreEqual(PlaceResult.GameOver, g.TryPlace(C(10, 10)));
            Assert.AreEqual(Piece.X, g.Turn, "Turn stays with the winner");
        }
    }
}
