using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Caro.Core;

namespace Caro.AI
{
    public enum Difficulty { Easy, Medium, Hard }

    /// <summary>
    /// Caro AI with three clearly separated levels:
    /// • Easy (beginner): makes mistakes — takes a ready win only ~80% of the time, blocks your four ~60%, rarely blocks an open three,
    ///   never builds a double threat on purpose; otherwise plays almost randomly next to existing pieces.
    /// • Medium (casual player): always wins/blocks fours and blocks open threes, looks one move ahead, sometimes picks the second-best move.
    /// • Hard (expert): searches continuous four-threat attacks (VCF) for itself and to defend against the opponent,
    ///   then runs an alpha-beta search several moves deep.
    /// No Unity dependency; safe to call from a background thread.
    /// </summary>
    public sealed class CaroAI
    {
        private const long Win = 1_000_000_000L;

        public Difficulty Level { get; }

        /// <summary>Maximum thinking time for Hard (milliseconds).</summary>
        public int TimeBudgetMs { get; set; } = 1400;

        /// <summary>Maximum alpha-beta depth for Hard.</summary>
        public int MaxDepth { get; set; } = 8;

        /// <summary>Maximum depth (attacking moves) of the continuous-four (VCF) search.</summary>
        public int VcfDepth { get; set; } = 10;

        // ----- Personality parameters -----
        private const double EasyTakeWin = 0.80;
        private const double EasyBlockFour = 0.60;
        private const double EasyBlockOpenThree = 0.20;
        private const double EasyAttackMove = 0.35;   // otherwise: random move next to a piece
        private const double MediumSecondBest = 0.25;
        private const double MediumTakeDouble = 0.50;

        private readonly Random _rng;
        private Stopwatch _clock;
        private bool _timeUp;
        private CancellationToken _cancel;
        private int _vcfNodes;
        private const int VcfNodeLimit = 30000;

        public CaroAI(Difficulty level, int? seed = null)
        {
            Level = level;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Picks a move for side `me` on the given board. The board must have at least one empty cell.</summary>
        public Cell ChooseMove(Board board, Piece me, CancellationToken cancel = default)
        {
            if (me == Piece.None) throw new ArgumentException("The AI must play X or O.");
            _cancel = cancel;
            var g = new SearchGrid(board);
            int s = SearchGrid.ToInt(me);

            if (g.Count == 0) return g.ToCell(g.W / 2, g.H / 2);

            var moves = RankMoves(g, s);
            if (moves.Count == 0) return FirstEmpty(g);

            switch (Level)
            {
                case Difficulty.Easy: return PickEasy(g, moves);
                case Difficulty.Medium: return PickMedium(g, moves);
                default: return PickHard(g, s, moves);
            }
        }

        // ================= Easy =================

        private Cell PickEasy(SearchGrid g, List<Ranked> moves)
        {
            if (Chance(EasyTakeWin) && TryFind(moves, m => m.Mine.Five, out var win)) return g.ToCell(win.X, win.Y);
            if (Chance(EasyBlockFour) && TryFind(moves, m => m.Theirs.Five, out var block)) return g.ToCell(block.X, block.Y);
            if (Chance(EasyBlockOpenThree) && TryFind(moves, m => m.Theirs.OpenFours > 0, out var b3)) return g.ToCell(b3.X, b3.Y);

            // Never builds a double threat; never deliberately plays the immediate win (handled above)
            var pool = moves.FindAll(m => !m.Mine.Five && !IsDouble(m.Mine));
            if (pool.Count == 0) pool = moves;

            if (Chance(EasyAttackMove))
            {
                // Random pick among a few decent "attacking" moves — looks only at its own shapes, ignores defence
                pool.Sort((a, b) => b.Mine.Score.CompareTo(a.Mine.Score));
                var m = pool[_rng.Next(Math.Min(10, pool.Count))];
                return g.ToCell(m.X, m.Y);
            }
            var near = pool.FindAll(m => g.HasNeighbor(m.X, m.Y, 1));
            var pick = near.Count > 0 ? near[_rng.Next(near.Count)] : pool[_rng.Next(pool.Count)];
            return g.ToCell(pick.X, pick.Y);
        }

        // ================= Medium =================

        private Cell PickMedium(SearchGrid g, List<Ranked> moves)
        {
            if (TryFind(moves, m => m.Mine.Five, out var win)) return g.ToCell(win.X, win.Y);
            if (TryFind(moves, m => m.Theirs.Five, out var block)) return g.ToCell(block.X, block.Y);
            // We can make an open four → guaranteed win, play it
            if (TryFind(moves, m => m.Mine.OpenFours > 0, out var open4)) return g.ToCell(open4.X, open4.Y);
            // Opponent is about to get an open four (has an open three) or a double threat → block the most dangerous cell
            var danger = moves.FindAll(m => m.Theirs.OpenFours > 0 || IsDouble(m.Theirs));
            if (danger.Count > 0) return g.ToCell(danger[0].X, danger[0].Y); // already sorted by score

            // Our own double threat: only noticed half of the time
            var pool = moves;
            if (IsDouble(moves[0].Mine) && !Chance(MediumTakeDouble))
            {
                var rest = moves.FindAll(m => !IsDouble(m.Mine));
                if (rest.Count > 0) pool = rest;
            }
            // Sometimes picks the second/third best move
            int k = Chance(MediumSecondBest) ? Math.Min(3, pool.Count) : 1;
            var pick = pool[_rng.Next(k)];
            return g.ToCell(pick.X, pick.Y);
        }

        // ================= Hard =================

        private Cell PickHard(SearchGrid g, int me, List<Ranked> rootMoves)
        {
            _clock = Stopwatch.StartNew();
            _timeUp = false;
            int opp = SearchGrid.Other(me);

            if (TryFind(rootMoves, m => m.Mine.Five, out var win)) return g.ToCell(win.X, win.Y);
            if (TryFind(rootMoves, m => m.Theirs.Five, out var block)) return g.ToCell(block.X, block.Y);

            // 1) Do we have a continuous-four sequence that wins?
            _vcfNodes = 0;
            if (Vcf(g, me, VcfDepth, out int vx, out int vy)) return g.ToCell(vx, vy);

            // 2) Drop moves that let the opponent win with a continuous-four sequence right after
            var root = rootMoves.GetRange(0, Math.Min(16, rootMoves.Count));
            var safe = new List<Ranked>();
            foreach (var m in root)
            {
                if (_clock.ElapsedMilliseconds > TimeBudgetMs / 2) { safe.Add(m); continue; }
                g.Place(m.X, m.Y, me);
                _vcfNodes = 0;
                bool oppWins = Vcf(g, opp, VcfDepth - 2, out _, out _);
                g.Remove(m.X, m.Y);
                if (!oppWins) safe.Add(m);
            }
            if (safe.Count == 0) safe = root; // every move loses: still resist with the best one
            var best = safe[0];

            // 3) Iterative-deepening alpha-beta within the time budget
            var order = safe;
            for (int depth = 2; depth <= MaxDepth; depth += 2)
            {
                long alpha = -Win * 2, beta = Win * 2;
                Ranked depthBest = order[0];
                long depthBestVal = long.MinValue;
                var scored = new List<(Ranked m, long v)>();

                foreach (var m in order)
                {
                    g.Place(m.X, m.Y, me);
                    long v = -Search(g, depth - 1, -beta, -alpha, opp, 1);
                    g.Remove(m.X, m.Y);
                    if (_timeUp) break;
                    scored.Add((m, v));
                    if (v > depthBestVal) { depthBestVal = v; depthBest = m; }
                    if (v > alpha) alpha = v;
                }
                if (_timeUp) break; // this depth did not finish → keep the previous depth's result

                best = depthBest;
                if (depthBestVal >= Win / 2) break; // a forced win has been found

                scored.Sort((a, b) => b.v.CompareTo(a.v));
                order = scored.ConvertAll(t => t.m);
            }
            return g.ToCell(best.X, best.Y);
        }

        private long Search(SearchGrid g, int depth, long alpha, long beta, int toMove, int ply)
        {
            if ((ply & 3) == 0 && (_clock.ElapsedMilliseconds > TimeBudgetMs || _cancel.IsCancellationRequested))
                _timeUp = true;
            if (_timeUp) return 0;

            if (depth <= 0) return Patterns.Evaluate(g, toMove);

            var moves = RankMoves(g, toMove);
            if (moves.Count == 0) return 0; // no cells left: draw

            foreach (var m in moves) if (m.Mine.Five) return Win - ply;

            var forced = moves.FindAll(m => m.Theirs.Five);
            if (forced.Count >= 2) return -(Win - ply - 1);

            List<Ranked> list;
            if (forced.Count == 1) list = forced;
            else
            {
                // We have an open four → forced win in two moves
                foreach (var m in moves) if (m.Mine.OpenFours > 0) return Win - ply - 2;
                list = moves.GetRange(0, Math.Min(ply <= 2 ? 12 : 8, moves.Count));
            }

            long best = -Win * 2;
            foreach (var m in list)
            {
                g.Place(m.X, m.Y, toMove);
                long v = -Search(g, depth - 1, -beta, -alpha, SearchGrid.Other(toMove), ply + 1);
                g.Remove(m.X, m.Y);
                if (_timeUp) return 0;
                if (v > best) best = v;
                if (v > alpha) alpha = v;
                if (alpha >= beta) break;
            }
            return best;
        }

        /// <summary>
        /// Victory by Continuous Fours (VCF) for `attacker`: every move makes a four the opponent must block,
        /// until a five or two fours at once are made. Returns the first move of the sequence.
        /// </summary>
        private bool Vcf(SearchGrid g, int attacker, int depth, out int fx, out int fy)
        {
            fx = fy = -1;
            if (depth <= 0 || ++_vcfNodes > VcfNodeLimit || _cancel.IsCancellationRequested) return false;
            // The VCF search may use at most 60% of the thinking time (the rest is for alpha-beta)
            if ((_vcfNodes & 63) == 0 && _clock != null && _clock.ElapsedMilliseconds > TimeBudgetMs * 6 / 10) { _vcfNodes = VcfNodeLimit + 1; return false; }
            int defender = SearchGrid.Other(attacker);

            // Opponent threatens to win → the sequence can only continue if we win immediately
            bool defenderThreat = HasFiveCell(g, defender);

            var fours = new List<(int x, int y, int score)>();
            for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                if (g.At(x, y) != SearchGrid.Empty || !g.HasNeighbor(x, y, 2)) continue;
                var info = Patterns.AnalyzeMove(g, x, y, attacker);
                if (info.Five) { fx = x; fy = y; return true; }
                if (!defenderThreat && info.Fours > 0) fours.Add((x, y, info.Score));
            }
            if (defenderThreat) return false;
            fours.Sort((a, b) => b.score.CompareTo(a.score));

            foreach (var (x, y, _) in fours)
            {
                g.Place(x, y, attacker);
                var blocks = FiveCellsAround(g, x, y, attacker);
                bool ok = false;
                if (blocks.Count >= 2) ok = true; // two cells to block → win
                else if (blocks.Count == 1)
                {
                    var (bx, by) = blocks[0];
                    g.Place(bx, by, defender);
                    // If the forced block makes five for the opponent, the sequence fails
                    if (!g.MakesFive(bx, by, defender)) ok = Vcf(g, attacker, depth - 1, out _, out _);
                    g.Remove(bx, by);
                }
                g.Remove(x, y);
                if (ok) { fx = x; fy = y; return true; }
                if (_vcfNodes > VcfNodeLimit) return false;
            }
            return false;
        }

        /// <summary>Empty cells on the four lines through (x,y) where `s` would make five.</summary>
        private static List<(int, int)> FiveCellsAround(SearchGrid g, int x, int y, int s)
        {
            var result = new List<(int, int)>(2);
            for (int d = 0; d < 4; d++)
            for (int k = -4; k <= 4; k++)
            {
                int cx = x + SearchGrid.DX[d] * k, cy = y + SearchGrid.DY[d] * k;
                if (g.At(cx, cy) != SearchGrid.Empty) continue;
                g.Place(cx, cy, s);
                bool five = g.MakesFive(cx, cy, s);
                g.Remove(cx, cy);
                if (five && !result.Contains((cx, cy))) result.Add((cx, cy));
            }
            return result;
        }

        /// <summary>Whether side `s` has any cell that makes five.</summary>
        private static bool HasFiveCell(SearchGrid g, int s)
        {
            for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                if (g.At(x, y) != SearchGrid.Empty || !g.HasNeighbor(x, y, 1)) continue;
                g.Place(x, y, s);
                bool five = g.MakesFive(x, y, s);
                g.Remove(x, y);
                if (five) return true;
            }
            return false;
        }

        // ================= Chung =================

        private struct Ranked
        {
            public int X, Y;
            public long Value;
            public Patterns.MoveInfo Mine, Theirs;
        }

        private bool Chance(double p) => _rng.NextDouble() < p;

        private static bool IsDouble(Patterns.MoveInfo i) =>
            i.Fours >= 2 || (i.Fours >= 1 && i.OpenThrees >= 1) || i.OpenThrees >= 2;

        private static bool TryFind(List<Ranked> moves, Predicate<Ranked> pred, out Ranked found)
        {
            foreach (var m in moves) if (pred(m)) { found = m; return true; }
            found = default;
            return false;
        }

        /// <summary>Empty cells within 2 of a piece, sorted by attack + defence score, highest first.</summary>
        private static List<Ranked> RankMoves(SearchGrid g, int me)
        {
            var list = new List<Ranked>(64);
            for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                if (g.At(x, y) != SearchGrid.Empty || !g.HasNeighbor(x, y, 2)) continue;
                long v = Patterns.MoveValue(g, x, y, me, out var mine, out var theirs);
                list.Add(new Ranked { X = x, Y = y, Value = v, Mine = mine, Theirs = theirs });
            }
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        private static Cell FirstEmpty(SearchGrid g)
        {
            for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
                if (g.At(x, y) == SearchGrid.Empty) return g.ToCell(x, y);
            throw new InvalidOperationException("The board is full.");
        }
    }
}
