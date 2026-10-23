using System;
using System.Collections.Generic;

namespace Caro.Core
{
    public enum PlaceResult
    {
        Ok,
        OutOfBounds,
        Occupied,
        GameOver
    }

    /// <summary>
    /// Rules of one game of caro: turns, placing pieces, growing the board, win/draw detection and undo.
    /// Has no Unity dependency, so it can be tested on its own.
    /// </summary>
    public class CaroGame
    {
        private readonly List<Cell> _history = new List<Cell>();
        private List<Cell> _winningLine;

        public Board Board { get; }
        public Piece FirstPlayer { get; }
        public Piece Turn { get; private set; }
        public bool IsOver { get; private set; }

        /// <summary>The winner; Piece.None while nobody has won, or on a draw.</summary>
        public Piece Winner { get; private set; } = Piece.None;

        public bool IsDraw => IsOver && Winner == Piece.None;

        /// <summary>Cells of the winning line; empty while nobody has won.</summary>
        public IReadOnlyList<Cell> WinningLine => (IReadOnlyList<Cell>)_winningLine ?? Array.Empty<Cell>();

        public IReadOnlyList<Cell> History => _history;

        /// <summary>Raised after every legal move: the cell played and the piece placed.</summary>
        public event Action<Cell, Piece> PiecePlaced;

        /// <summary>Raised when the board has just grown.</summary>
        public event Action BoardResized;

        /// <summary>Raised when the game ends: the winner (Piece.None = draw).</summary>
        public event Action<Piece> GameEnded;

        /// <summary>Raised when a move is taken back: the cell that was cleared.</summary>
        public event Action<Cell> MoveUndone;

        public CaroGame(Piece firstPlayer = Piece.X) : this(firstPlayer, new Board()) { }

        /// <summary>Creates a game on a given board (for tests).</summary>
        internal CaroGame(Piece firstPlayer, Board board)
        {
            if (firstPlayer == Piece.None) throw new ArgumentException("The first player must be X or O.");
            FirstPlayer = firstPlayer;
            Turn = firstPlayer;
            Board = board;
        }

        public PlaceResult TryPlace(Cell cell)
        {
            if (IsOver) return PlaceResult.GameOver;
            if (!Board.InBounds(cell)) return PlaceResult.OutOfBounds;
            if (!Board.IsEmpty(cell)) return PlaceResult.Occupied;

            var piece = Turn;
            Board.Set(cell, piece);
            _history.Add(cell);
            bool resized = Board.ExpandAround(cell);

            _winningLine = WinChecker.FindWinningLine(Board, cell);
            if (_winningLine != null)
            {
                Winner = piece;
                IsOver = true;
            }
            else if (Board.IsFull)
            {
                // Board full: only possible at the full 20×20, since any piece near an edge grows the board while it still can.
                IsOver = true;
            }
            else
            {
                Turn = piece.Opponent();
            }

            PiecePlaced?.Invoke(cell, piece);
            if (resized) BoardResized?.Invoke();
            if (IsOver) GameEnded?.Invoke(Winner);
            return PlaceResult.Ok;
        }

        /// <summary>
        /// Takes back the last move and gives the turn back to whoever played it. Reopens a finished game.
        /// The board keeps its size (it never shrinks). Returns false if there is no move to undo.
        /// </summary>
        public bool Undo()
        {
            if (_history.Count == 0) return false;

            var cell = _history[_history.Count - 1];
            var piece = Board.Get(cell);
            _history.RemoveAt(_history.Count - 1);
            Board.Set(cell, Piece.None);

            IsOver = false;
            Winner = Piece.None;
            _winningLine = null;
            Turn = piece;

            MoveUndone?.Invoke(cell);
            return true;
        }
    }
}
