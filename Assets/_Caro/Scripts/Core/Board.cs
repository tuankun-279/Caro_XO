using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Caro.Tests")]

namespace Caro.Core
{
    public enum Piece { None, X, O }

    public static class PieceExtensions
    {
        public static Piece Opponent(this Piece p) =>
            p == Piece.X ? Piece.O : p == Piece.O ? Piece.X : Piece.None;
    }

    /// <summary>A cell coordinate on the board. X grows to the right, Y grows upward.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y) { X = x; Y = y; }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
    }

    /// <summary>
    /// A board with movable bounds: starts at 15×15 and grows when a piece is played near an edge,
    /// up to 20×20. Cell coordinates never change when the board grows; only the bounds
    /// (MinX, MinY, Width, Height) do, so MinX/MinY can become negative after growing left/down.
    /// </summary>
    public class Board
    {
        public const int StartSize = 15;
        public const int MaxSize = 20;

        /// <summary>Minimum number of empty rows/columns to keep between a piece and the board edge.</summary>
        public const int EdgeMargin = 2;

        private readonly Dictionary<Cell, Piece> _cells = new Dictionary<Cell, Piece>();

        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public int MaxX => MinX + Width - 1;
        public int MaxY => MinY + Height - 1;

        /// <summary>Number of pieces currently on the board.</summary>
        public int PieceCount => _cells.Count;

        /// <summary>Total number of cells at the current board size.</summary>
        public int Capacity => Width * Height;

        public bool IsFull => PieceCount >= Capacity;

        public bool IsAtMaxSize => Width >= MaxSize && Height >= MaxSize;

        public Board() : this(0, 0, StartSize, StartSize) { }

        /// <summary>Creates a board with arbitrary bounds (for tests).</summary>
        internal Board(int minX, int minY, int width, int height)
        {
            MinX = minX; MinY = minY; Width = width; Height = height;
        }

        /// <summary>Independent copy, so the AI can think on a background thread without touching the live board.</summary>
        public Board Clone()
        {
            var copy = new Board(MinX, MinY, Width, Height);
            foreach (var kv in _cells) copy._cells[kv.Key] = kv.Value;
            return copy;
        }

        public bool InBounds(Cell c) => c.X >= MinX && c.X <= MaxX && c.Y >= MinY && c.Y <= MaxY;

        public Piece Get(Cell c) => _cells.TryGetValue(c, out var p) ? p : Piece.None;

        public bool IsEmpty(Cell c) => Get(c) == Piece.None;

        internal void Set(Cell c, Piece p)
        {
            if (p == Piece.None) _cells.Remove(c);
            else _cells[c] = p;
        }

        /// <summary>
        /// Grows the board so at least EdgeMargin empty rows/columns remain between cell c and each edge,
        /// never exceeding MaxSize in either dimension. Returns true if the size changed.
        /// </summary>
        internal bool ExpandAround(Cell c)
        {
            int oldMinX = MinX, oldMinY = MinY, oldW = Width, oldH = Height;

            // Left
            int grow = Math.Min(EdgeMargin - (c.X - MinX), MaxSize - Width);
            if (grow > 0) { MinX -= grow; Width += grow; }
            // Right
            grow = Math.Min(EdgeMargin - (MaxX - c.X), MaxSize - Width);
            if (grow > 0) Width += grow;
            // Bottom
            grow = Math.Min(EdgeMargin - (c.Y - MinY), MaxSize - Height);
            if (grow > 0) { MinY -= grow; Height += grow; }
            // Top
            grow = Math.Min(EdgeMargin - (MaxY - c.Y), MaxSize - Height);
            if (grow > 0) Height += grow;

            return MinX != oldMinX || MinY != oldMinY || Width != oldW || Height != oldH;
        }
    }
}
