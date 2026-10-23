using Caro.Core;
using NUnit.Framework;

namespace Caro.Tests
{
    public class PlacementTests
    {
        [Test]
        public void NewGame_Is15x15_AndXGoesFirst()
        {
            var g = new CaroGame();
            Assert.AreEqual(15, g.Board.Width);
            Assert.AreEqual(15, g.Board.Height);
            Assert.AreEqual(Piece.X, g.Turn);
            Assert.AreEqual(0, g.Board.PieceCount);
        }

        [Test]
        public void FirstPlayerCanBeO()
        {
            var g = new CaroGame(Piece.O);
            Assert.AreEqual(Piece.O, g.Turn);
            g.TryPlace(new Cell(7, 7));
            Assert.AreEqual(Piece.O, g.Board.Get(new Cell(7, 7)));
            Assert.AreEqual(Piece.X, g.Turn);
        }

        [Test]
        public void Place_PutsPiece_AndSwitchesTurn()
        {
            var g = new CaroGame();
            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(new Cell(7, 7)));
            Assert.AreEqual(Piece.X, g.Board.Get(new Cell(7, 7)));
            Assert.AreEqual(Piece.O, g.Turn);

            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(new Cell(8, 7)));
            Assert.AreEqual(Piece.O, g.Board.Get(new Cell(8, 7)));
            Assert.AreEqual(Piece.X, g.Turn);
            Assert.AreEqual(2, g.History.Count);
        }

        [Test]
        public void OccupiedCell_IsRejected_AndTurnDoesNotChange()
        {
            var g = new CaroGame();
            g.TryPlace(new Cell(3, 3));
            Assert.AreEqual(PlaceResult.Occupied, g.TryPlace(new Cell(3, 3)));
            Assert.AreEqual(Piece.X, g.Board.Get(new Cell(3, 3)));
            Assert.AreEqual(Piece.O, g.Turn);
            Assert.AreEqual(1, g.History.Count);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(15, 0)]
        [TestCase(0, 15)]
        public void OutsideBoard_IsRejected(int x, int y)
        {
            var g = new CaroGame();
            Assert.AreEqual(PlaceResult.OutOfBounds, g.TryPlace(new Cell(x, y)));
            Assert.AreEqual(0, g.Board.PieceCount);
        }

        [Test]
        public void Corners_AreInsideBoard()
        {
            var g = new CaroGame();
            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(new Cell(0, 0)));
            Assert.AreEqual(PlaceResult.Ok, g.TryPlace(new Cell(14, 14)));
        }

        [Test]
        public void PiecePlacedEvent_Fires()
        {
            var g = new CaroGame();
            Cell got = default; Piece gotPiece = Piece.None;
            g.PiecePlaced += (c, p) => { got = c; gotPiece = p; };
            g.TryPlace(new Cell(5, 6));
            Assert.AreEqual(new Cell(5, 6), got);
            Assert.AreEqual(Piece.X, gotPiece);
        }
    }
}
