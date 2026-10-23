using System.Collections.Generic;

namespace Caro.Core
{
    /// <summary>
    /// Win detection: five or more pieces in a row horizontally, vertically or diagonally.
    /// Lines blocked at both ends still count.
    /// </summary>
    public static class WinChecker
    {
        public const int WinLength = 5;

        // Horizontal, vertical, rising diagonal (↗), falling diagonal (↘)
        private static readonly (int dx, int dy)[] Directions = { (1, 0), (0, 1), (1, 1), (1, -1) };

        /// <summary>
        /// Returns the winning line through the cell just played (ordered along the line), or null if none.
        /// If one move completes several lines at once, the longest one is returned.
        /// </summary>
        public static List<Cell> FindWinningLine(Board board, Cell last)
        {
            var piece = board.Get(last);
            if (piece == Piece.None) return null;

            List<Cell> best = null;
            foreach (var (dx, dy) in Directions)
            {
                var line = new List<Cell>();
                // Walk back to the start of the run
                var start = last;
                while (board.Get(new Cell(start.X - dx, start.Y - dy)) == piece)
                    start = new Cell(start.X - dx, start.Y - dy);
                // Walk forward to the end of the run
                for (var c = start; board.Get(c) == piece; c = new Cell(c.X + dx, c.Y + dy))
                    line.Add(c);

                if (line.Count >= WinLength && (best == null || line.Count > best.Count))
                    best = line;
            }
            return best;
        }
    }
}
