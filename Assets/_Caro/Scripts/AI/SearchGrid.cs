using Caro.Core;

namespace Caro.AI
{
    /// <summary>
    /// Compact copy of the board so the AI can try moves quickly (place/remove in place).
    /// Grid coordinates are (0..W-1, 0..H-1); cells outside the board count as walls.
    /// Cell values: 0 = empty, 1 = X, 2 = O.
    /// </summary>
    internal sealed class SearchGrid
    {
        public const int Empty = 0, Wall = -1;

        public static readonly int[] DX = { 1, 0, 1, 1 };
        public static readonly int[] DY = { 0, 1, 1, -1 };

        public readonly int W, H, MinX, MinY;
        private readonly sbyte[] _c;
        public int Count { get; private set; }

        public SearchGrid(Board b)
        {
            W = b.Width; H = b.Height; MinX = b.MinX; MinY = b.MinY;
            _c = new sbyte[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var p = b.Get(new Cell(MinX + x, MinY + y));
                if (p != Piece.None) { _c[y * W + x] = (sbyte)ToInt(p); Count++; }
            }
        }

        public static int ToInt(Piece p) => p == Piece.X ? 1 : p == Piece.O ? 2 : 0;
        public static int Other(int s) => 3 - s;

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public int At(int x, int y) => In(x, y) ? _c[y * W + x] : Wall;

        public void Place(int x, int y, int s) { _c[y * W + x] = (sbyte)s; Count++; }
        public void Remove(int x, int y) { _c[y * W + x] = 0; Count--; }

        public Cell ToCell(int x, int y) => new Cell(MinX + x, MinY + y);

        /// <summary>Whether any piece lies within radius r (square distance) of the cell.</summary>
        public bool HasNeighbor(int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int v = At(x + dx, y + dy);
                if (v > 0) return true;
            }
            return false;
        }

        /// <summary>Whether the s piece at (x,y) is part of a run of five or more.</summary>
        public bool MakesFive(int x, int y, int s)
        {
            for (int d = 0; d < 4; d++)
            {
                int n = 1;
                for (int k = 1; At(x + DX[d] * k, y + DY[d] * k) == s; k++) n++;
                for (int k = 1; At(x - DX[d] * k, y - DY[d] * k) == s; k++) n++;
                if (n >= 5) return true;
            }
            return false;
        }
    }
}
