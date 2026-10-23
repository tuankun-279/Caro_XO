namespace Caro.AI
{
    /// <summary>
    /// Shape scoring: which shapes (five, open four, four, open three…) side s would make by playing an empty cell,
    /// plus a whole-board evaluation used by the Hard level.
    /// </summary>
    internal static class Patterns
    {
        public const int Five = 10_000_000;
        public const int OpenFour = 500_000;
        public const int Four = 50_000;
        public const int OpenThree = 20_000;
        public const int BrokenThree = 14_000;
        public const int Three = 1_500;
        public const int OpenTwo = 800;
        public const int Two = 100;
        public const int One = 10;

        public const int DoubleThreatBonus = 400_000; // two fours, or a four + an open three
        public const int DoubleThreeBonus = 150_000;  // two open threes

        /// <summary>Result of analysing one cell.</summary>
        public struct MoveInfo
        {
            public int Score;
            public bool Five;
            public int Fours;      // directions that make a four (open, blocked on one side, or broken)
            public int OpenFours;
            public int OpenThrees; // includes broken threes open on both ends
        }

        /// <summary>Which shapes side s makes by playing the empty cell (x,y).</summary>
        public static MoveInfo AnalyzeMove(SearchGrid g, int x, int y, int s)
        {
            var info = new MoveInfo();
            for (int d = 0; d < 4; d++)
            {
                int dx = SearchGrid.DX[d], dy = SearchGrid.DY[d];
                Side(g, x, y, dx, dy, s, out int n1, out bool open1, out int gap1);
                Side(g, x, y, -dx, -dy, s, out int n2, out bool open2, out int gap2);

                int count = 1 + n1 + n2;
                int opens = (open1 ? 1 : 0) + (open2 ? 1 : 0);
                int bestGap = System.Math.Max(open1 ? gap1 : 0, open2 ? gap2 : 0);

                if (count >= 5) { info.Five = true; info.Score += Five; continue; }

                if (count == 4)
                {
                    if (opens == 2) { info.OpenFours++; info.Fours++; info.Score += OpenFour; }
                    else if (opens == 1) { info.Fours++; info.Score += Four; }
                    continue;
                }

                // Broken four: XX_XX, XXX_X … → filling the gap makes five
                if (bestGap > 0 && count + bestGap >= 4) { info.Fours++; info.Score += Four; continue; }

                if (count == 3)
                {
                    if (opens == 2) { info.OpenThrees++; info.Score += OpenThree; }
                    else if (opens == 1) info.Score += Three;
                    continue;
                }

                // Broken three open on both ends: X_XX, XX_X
                if (bestGap > 0 && count + bestGap == 3 && opens == 2)
                {
                    info.OpenThrees++; info.Score += BrokenThree; continue;
                }

                if (count == 2) info.Score += opens == 2 ? OpenTwo : opens == 1 ? Two : 0;
                else if (opens == 2) info.Score += One;
            }

            if (!info.Five)
            {
                if (info.Fours >= 2 || (info.Fours >= 1 && info.OpenThrees >= 1)) info.Score += DoubleThreatBonus;
                else if (info.OpenThrees >= 2) info.Score += DoubleThreeBonus;
            }
            return info;
        }

        /// <summary>
        /// Walks from (x,y) in one direction: n = adjacent pieces of s; open = the next cell is empty;
        /// gap = run of s pieces right after exactly one empty cell (to detect broken shapes).
        /// </summary>
        private static void Side(SearchGrid g, int x, int y, int dx, int dy, int s, out int n, out bool open, out int gap)
        {
            n = 0; gap = 0;
            int k = 1;
            while (g.At(x + dx * k, y + dy * k) == s) { n++; k++; }
            open = g.At(x + dx * k, y + dy * k) == SearchGrid.Empty;
            if (open)
            {
                int j = k + 1;
                while (gap < 4 && g.At(x + dx * j, y + dy * j) == s) { gap++; j++; }
            }
        }

        /// <summary>
        /// Value of a move: attack (our shapes) + defence (the opponent's shapes if they played there).
        /// </summary>
        public static long MoveValue(SearchGrid g, int x, int y, int me, out MoveInfo mine, out MoveInfo theirs)
        {
            mine = AnalyzeMove(g, x, y, me);
            theirs = AnalyzeMove(g, x, y, SearchGrid.Other(me));
            // Slightly prefer cells near the centre when everything else is equal
            int cx = g.W / 2, cy = g.H / 2;
            int center = 8 - System.Math.Max(System.Math.Abs(x - cx), System.Math.Abs(y - cy));
            return (long)mine.Score * 11 / 10 + theirs.Score + System.Math.Max(0, center);
        }

        /// <summary>
        /// Evaluates the whole board from the side to move (toMove): scans every run of pieces.
        /// </summary>
        public static long Evaluate(SearchGrid g, int toMove)
        {
            long s1 = 0, s2 = 0;
            int fours1 = 0, fours2 = 0, open4_1 = 0, open4_2 = 0, open3_1 = 0, open3_2 = 0;

            for (int d = 0; d < 4; d++)
            {
                int dx = SearchGrid.DX[d], dy = SearchGrid.DY[d];
                for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    int c = g.At(x, y);
                    if (c <= 0) continue;
                    if (g.At(x - dx, y - dy) == c) continue; // not the start of a run

                    int len = 1;
                    while (g.At(x + dx * len, y + dy * len) == c) len++;
                    int opens = (g.At(x - dx, y - dy) == 0 ? 1 : 0) + (g.At(x + dx * len, y + dy * len) == 0 ? 1 : 0);

                    long v = 0;
                    bool isFour = false, isOpen4 = false, isOpen3 = false;
                    if (len >= 5) v = Five;
                    else if (len == 4) { if (opens > 0) { isFour = true; isOpen4 = opens == 2; v = isOpen4 ? OpenFour : Four; } }
                    else if (len == 3) { if (opens == 2) { isOpen3 = true; v = OpenThree; } else if (opens == 1) v = Three; }
                    else if (len == 2) v = opens == 2 ? OpenTwo : opens == 1 ? Two : 0;
                    else v = opens == 2 ? One : 0;

                    if (c == 1) { s1 += v; if (isFour) fours1++; if (isOpen4) open4_1++; if (isOpen3) open3_1++; }
                    else { s2 += v; if (isFour) fours2++; if (isOpen4) open4_2++; if (isOpen3) open3_2++; }
                }
            }

            int opp = SearchGrid.Other(toMove);
            long mine = toMove == 1 ? s1 : s2, theirs = toMove == 1 ? s2 : s1;
            int myFours = toMove == 1 ? fours1 : fours2, theirFours = toMove == 1 ? fours2 : fours1;
            int theirOpen4 = toMove == 1 ? open4_2 : open4_1;
            int myOpen3 = toMove == 1 ? open3_1 : open3_2, theirOpen3 = toMove == 1 ? open3_2 : open3_1;

            // Side to move has a four → wins next move
            if (myFours > 0) return Five;
            // Opponent has an open four or two fours → cannot block in time
            if (theirOpen4 > 0 || theirFours >= 2) return -Five / 2;
            // We have an open three and the opponent has no four → moving first makes an open four
            if (myOpen3 > 0 && theirFours == 0) mine += OpenFour / 2;
            if (theirOpen3 >= 2 && myOpen3 == 0) theirs += DoubleThreeBonus / 2;

            return mine - theirs * 11 / 10;
        }
    }
}
