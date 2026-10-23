using System.Collections.Generic;
using UnityEngine;

namespace Caro.Game
{
    /// <summary>Pen timing (seconds), shared by the drawing animation and the sounds so strokes match the audio.</summary>
    public static class PenTiming
    {
        /// <summary>Duration of one X stroke.</summary>
        public const float XStroke = 0.11f;
        /// <summary>Delay before the second X stroke starts.</summary>
        public const float XSecondStart = 0.17f;
        /// <summary>Duration of the full O loop.</summary>
        public const float OStroke = 0.36f;

        public static float Duration(Caro.Core.Piece p) =>
            p == Caro.Core.Piece.X ? XSecondStart + XStroke : OStroke;
    }

    /// <summary>
    /// Builds hand-drawn ink X/O sprites in code (no image files).
    /// Each piece has a few slightly different variants so the board looks naturally hand-drawn.
    /// Texture layout: A = ink coverage, R = drawing order of that pixel (0..1), so the
    /// "Caro/InkReveal" shader can draw the piece along the pen path: X as two strokes, O as one loop.
    /// </summary>
    internal static class InkSprites
    {
        private const int Size = 160;

        public static Sprite Pixel()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public static Sprite[] MakeX(int variants, int seed)
        {
            var rng = new System.Random(seed);
            var result = new Sprite[variants];
            for (int v = 0; v < variants; v++)
            {
                // Stroke 1: top-left → bottom-right (order 0..0.5); stroke 2: top-right → bottom-left (0.5..1)
                var strokes = new List<Stroke>
                {
                    Curve(J(rng, -0.32f, 0.33f), J(rng, 0.33f, -0.31f), Bend(rng), 0.074f, rng, 0f, 0.5f),
                    Curve(J(rng, 0.31f, 0.32f), J(rng, -0.32f, -0.33f), Bend(rng), 0.070f, rng, 0.5f, 1f),
                };
                result[v] = Rasterize(strokes, $"InkX{v}");
            }
            return result;
        }

        public static Sprite[] MakeO(int variants, int seed)
        {
            var rng = new System.Random(seed);
            var result = new Sprite[variants];
            for (int v = 0; v < variants; v++)
            {
                // Start near the top and go counter-clockwise, like handwriting
                float start = Mathf.PI * 0.5f + R(rng, 0.6f);
                float p1 = (float)(rng.NextDouble() * 6.28), p2 = (float)(rng.NextDouble() * 6.28);
                float cx = R(rng, 0.015f), cy = R(rng, 0.015f);
                var pts = new List<Vector2>();
                var rad = new List<float>();
                const int seg = 56;
                float sweep = Mathf.PI * 2f + 0.45f; // the loop overlaps slightly where the pen lifts
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg;
                    float a = start + sweep * t;
                    float r = 0.335f * (1f + 0.05f * Mathf.Sin(2f * a + p1) + 0.025f * Mathf.Sin(3f * a + p2)) - 0.012f * t;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r * 0.96f));
                    rad.Add(0.068f * (0.55f + 0.45f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f))));
                }
                result[v] = Rasterize(new List<Stroke> { new Stroke(pts, rad, 0f, 1f) }, $"InkO{v}");
            }
            return result;
        }

        // ---------- Pen strokes ----------

        private sealed class Stroke
        {
            public readonly List<Vector2> P;
            public readonly List<float> R;
            /// <summary>Drawing order (0..1) at each point, proportional to arc length.</summary>
            public readonly float[] Order;

            public Stroke(List<Vector2> p, List<float> r, float from, float to)
            {
                P = p; R = r;
                Order = new float[p.Count];
                float total = 0f;
                for (int i = 1; i < p.Count; i++) total += (p[i] - p[i - 1]).magnitude;
                float acc = 0f;
                for (int i = 0; i < p.Count; i++)
                {
                    if (i > 0) acc += (p[i] - p[i - 1]).magnitude;
                    Order[i] = Mathf.Lerp(from, to, total > 0f ? acc / total : 0f);
                }
            }
        }

        private static float R(System.Random rng, float amp) => (float)(rng.NextDouble() * 2 - 1) * amp;
        private static Vector2 J(System.Random rng, float x, float y) => new Vector2(x + R(rng, 0.03f), y + R(rng, 0.03f));
        private static float Bend(System.Random rng) => R(rng, 0.06f);

        /// <summary>A gently curved stroke from a to b, thin at the ends and thicker in the middle like a ballpoint pen.</summary>
        private static Stroke Curve(Vector2 a, Vector2 b, float bend, float width, System.Random rng, float from, float to)
        {
            var mid = (a + b) / 2f;
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized;
            var ctrl = mid + normal * bend;
            var pts = new List<Vector2>();
            var rad = new List<float>();
            const int seg = 18;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * ctrl + t * t * b;
                pts.Add(p);
                rad.Add(width * (0.6f + 0.4f * Mathf.Sin(Mathf.PI * t)) * (1f + R(rng, 0.06f)));
            }
            return new Stroke(pts, rad, from, to);
        }

        /// <summary>
        /// Rasterizes the strokes: A = coverage (anti-aliased), R = drawing order.
        /// Where strokes overlap the earliest order wins, so already-drawn ink never "disappears" when a later stroke crosses it.
        /// </summary>
        private static Sprite Rasterize(List<Stroke> strokes, string name)
        {
            // linear = true: the drawing order is data, not colour, so it must not be gamma-converted
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            var px = new Color32[Size * Size];
            float scale = Size; // stroke coordinates lie in [-0.5, 0.5]
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var q = new Vector2((x + 0.5f) / scale - 0.5f, (y + 0.5f) / scale - 0.5f);
                float best = float.MaxValue, bestOrder = 1f, insideOrder = float.MaxValue;
                foreach (var s in strokes)
                {
                    for (int i = 0; i < s.P.Count - 1; i++)
                    {
                        var a = s.P[i]; var b = s.P[i + 1];
                        var ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        float d = (q - (a + ab * t)).magnitude - Mathf.Lerp(s.R[i], s.R[i + 1], t);
                        float order = Mathf.Lerp(s.Order[i], s.Order[i + 1], t);
                        if (d < best) { best = d; bestOrder = order; }
                        if (d < 0.5f / scale && order < insideOrder) insideOrder = order;
                    }
                }
                float alpha = Mathf.Clamp01(0.5f - best * scale); // ~1 pixel anti-aliasing
                float ord = insideOrder < float.MaxValue ? insideOrder : bestOrder;
                px[y * Size + x] = new Color32((byte)Mathf.RoundToInt(ord * 255f), 255, 255, (byte)(alpha * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }
}
