using Caro.Core;
using NUnit.Framework;

namespace Caro.Tests
{
    public class ExpansionTests
    {
        private static Cell C(int x, int y) => new Cell(x, y);

        [Test]
        public void CenterMove_DoesNotExpand()
        {
            var g = new CaroGame();
            g.TryPlace(C(7, 7));
            g.TryPlace(C(2, 12)); // 2 from the left edge and 2 from the top → margin still enough
            Assert.AreEqual(15, g.Board.Width);
            Assert.AreEqual(15, g.Board.Height);
        }

        [Test]
        public void NearLeftEdge_ExpandsLeft()
        {
            var g = new CaroGame();
            g.TryPlace(C(1, 7));  // 1 from the left edge → grows one column to the left
            Assert.AreEqual(-1, g.Board.MinX);
            Assert.AreEqual(16, g.Board.Width);
            Assert.AreEqual(15, g.Board.Height, "Height unchanged");
            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(C(-1, 3)), "Newly added cells must be playable");
        }

        [Test]
        public void OnRightAndTopEdge_ExpandsBothWays()
        {
            var g = new CaroGame();
            g.TryPlace(C(14, 14)); // top-right corner on the edge → grows 2 to the right and 2 up
            Assert.AreEqual(0, g.Board.MinX);
            Assert.AreEqual(0, g.Board.MinY);
            Assert.AreEqual(17, g.Board.Width);
            Assert.AreEqual(17, g.Board.Height);
        }

        [Test]
        public void OnBottomLeftCorner_ExpandsDownAndLeft()
        {
            var g = new CaroGame();
            g.TryPlace(C(0, 0));
            Assert.AreEqual(-2, g.Board.MinX);
            Assert.AreEqual(-2, g.Board.MinY);
            Assert.AreEqual(17, g.Board.Width);
            Assert.AreEqual(17, g.Board.Height);
        }

        [Test]
        public void Width_StopsAt20_HeightStaysIndependent()
        {
            var g = new CaroGame();
            for (int i = 0; i < 10; i++)
            {
                var r = g.TryPlace(C(g.Board.MinX, 2 + i)); // always play on the left edge, each move on a different row
                Assert.AreEqual(PlaceResult.Ok, r);
                Assert.LessOrEqual(g.Board.Width, Board.MaxSize);
            }
            Assert.AreEqual(20, g.Board.Width);
            Assert.AreEqual(15, g.Board.Height, "Height is independent and does not grow along");
            Assert.AreEqual(PlaceResult.OutOfBounds, g.TryPlace(C(g.Board.MinX - 1, 7)), "Cannot play outside once at max size");
        }

        [Test]
        public void ResizedEvent_FiresOnlyWhenSizeChanges()
        {
            var g = new CaroGame();
            int resized = 0;
            g.BoardResized += () => resized++;
            g.TryPlace(C(7, 7));
            Assert.AreEqual(0, resized);
            g.TryPlace(C(0, 7));
            Assert.AreEqual(1, resized);
        }
    }

    public class UndoTests
    {
        private static Cell C(int x, int y) => new Cell(x, y);

        [Test]
        public void Undo_EmptyGame_ReturnsFalse()
        {
            Assert.IsFalse(new CaroGame().Undo());
        }

        [Test]
        public void Undo_RemovesPiece_AndRestoresTurn()
        {
            var g = new CaroGame();
            g.TryPlace(C(7, 7)); // X
            g.TryPlace(C(8, 8)); // O
            Assert.IsTrue(g.Undo());
            Assert.AreEqual(Piece.None, g.Board.Get(C(8, 8)));
            Assert.AreEqual(Piece.O, g.Turn);
            Assert.AreEqual(1, g.History.Count);
            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(C(8, 8)), "The undone cell can be played again");
        }

        [Test]
        public void Undo_AfterWin_ReopensGame()
        {
            var g = new CaroGame();
            Cell[] x = { C(3, 5), C(4, 5), C(5, 5), C(6, 5), C(7, 5) };
            Cell[] o = { C(3, 9), C(5, 9), C(7, 9), C(9, 9) };
            for (int i = 0; i < 5; i++) { g.TryPlace(x[i]); if (i < 4) g.TryPlace(o[i]); }
            Assert.AreEqual(Piece.X, g.Winner);

            Assert.IsTrue(g.Undo());
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(Piece.None, g.Winner);
            Assert.AreEqual(0, g.WinningLine.Count);
            Assert.AreEqual(Piece.X, g.Turn, "Turn goes back to X to replay the winning move");
        }

        [Test]
        public void Undo_DoesNotShrinkBoard()
        {
            var g = new CaroGame();
            g.TryPlace(C(0, 7));
            Assert.AreEqual(17, g.Board.Width);
            g.Undo();
            Assert.AreEqual(17, g.Board.Width, "Board keeps its size after undo");
            Assert.AreEqual(-2, g.Board.MinX);
        }

        [Test]
        public void NewGame_IsBackTo15x15()
        {
            var g = new CaroGame();
            g.TryPlace(C(0, 0));
            Assert.AreEqual(17, g.Board.Width);
            var fresh = new CaroGame();
            Assert.AreEqual(15, fresh.Board.Width);
            Assert.AreEqual(15, fresh.Board.Height);
            Assert.AreEqual(0, fresh.Board.PieceCount);
        }
    }

    public class DrawTests
    {
        /// <summary>
        /// Pattern that fills the board without five in a row in any direction:
        /// the longest run in every direction is only 2.
        /// </summary>
        private static Piece Pattern(int x, int y) => (((x / 2) + y) % 2 == 0) ? Piece.X : Piece.O;

        [Test]
        public void Full20x20Board_WithoutFive_IsDraw()
        {
            var board = new Board(0, 0, Board.MaxSize, Board.MaxSize);
            var g = new CaroGame(Piece.X, board);

            var xs = new System.Collections.Generic.List<Cell>();
            var os = new System.Collections.Generic.List<Cell>();
            for (int y = 0; y < Board.MaxSize; y++)
            for (int x = 0; x < Board.MaxSize; x++)
                (Pattern(x, y) == Piece.X ? xs : os).Add(new Cell(x, y));
            Assert.AreEqual(xs.Count, os.Count, "The pattern must split X and O evenly");

            int ended = 0;
            g.GameEnded += _ => ended++;
            for (int i = 0; i < xs.Count; i++)
            {
                Assert.AreEqual(PlaceResult.Ok, g.TryPlace(xs[i]));
                Assert.IsFalse(g.IsOver, $"Must not be over yet at X move {i}");
                Assert.AreEqual(PlaceResult.Ok, g.TryPlace(os[i]));
            }

            Assert.IsTrue(g.IsOver);
            Assert.IsTrue(g.IsDraw);
            Assert.AreEqual(Piece.None, g.Winner);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(20, g.Board.Width);
            Assert.AreEqual(20, g.Board.Height);
        }

        [Test]
        public void NotDraw_WhileCellsRemain()
        {
            var board = new Board(0, 0, Board.MaxSize, Board.MaxSize);
            var g = new CaroGame(Piece.X, board);
            g.TryPlace(new Cell(0, 0));
            Assert.IsFalse(g.IsOver);
        }
    }
}
