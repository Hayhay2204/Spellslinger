using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger.Gestures
{
    // This is where all the glyph shapes get made. To add a new glyph make a function for the shape,
    // add it in Build() and then add a spell with the same name in SpellBook.
    // Y goes up like it does in screen space
    public static class GestureLibrary
    {
        public const string Lightning = "Chain Lightning";
        public const string Fireball = "Fireball";
        public const string Boulder = "Boulder";
        public const string WindWard = "Wind Ward";
        public const string Vines = "Vines";

        // One version of each shape, used to draw the icons in the spellbook
        public static readonly Dictionary<string, List<Vector2>> Canonical = new();

        public static GestureRecognizer Build()
        {
            var r = new GestureRecognizer();
            Canonical.Clear();

            // Circle = Wind Ward. Added starting from every angle and going both directions
            for (int start = 0; start < 360; start += 45)
            {
                AddBothWays(r, WindWard, Circle(start, 1));
            }
            Canonical[WindWard] = Circle(90, -1);

            // Square = Boulder. Can start from any corner and go either direction
            var sq = new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };
            for (int s = 0; s < 4; s++)
            {
                var poly = new List<Vector2>();
                for (int i = 0; i <= 4; i++) poly.Add(sq[(s + i) % 4]);
                AddBothWays(r, Boulder, Densify(poly));
            }
            Canonical[Boulder] = Densify(new List<Vector2> { sq[0], sq[1], sq[2], sq[3], sq[0] });

            // Triangle = Fireball. Can start from any corner and go either direction
            var tri = new[] { new Vector2(0f, 1f), new Vector2(0.87f, -0.5f), new Vector2(-0.87f, -0.5f) };
            for (int s = 0; s < 3; s++)
            {
                var poly = new List<Vector2>();
                for (int i = 0; i <= 3; i++) poly.Add(tri[(s + i) % 3]);
                AddBothWays(r, Fireball, Densify(poly));
            }
            Canonical[Fireball] = Densify(new List<Vector2> { tri[0], tri[1], tri[2], tri[0] });

            // Zigzag = Lightning. Zigzags with 3 to 6 lines, plus some going downwards like a lightning bolt
            for (int segs = 3; segs <= 6; segs++)
            {
                AddBothWays(r, Lightning, Zigzag(segs, true, false));
                AddBothWays(r, Lightning, Zigzag(segs, false, false));
            }
            for (int segs = 3; segs <= 4; segs++)
            {
                AddBothWays(r, Lightning, Zigzag(segs, true, true));
                AddBothWays(r, Lightning, Zigzag(segs, false, true));
            }
            Canonical[Lightning] = Zigzag(4, false, false);

            // V = Vines (its meant to look like a little sprout)
            var v = Densify(new List<Vector2> { new(-1f, 1f), new(0f, -1f), new(1f, 1f) });
            AddBothWays(r, Vines, v);
            Canonical[Vines] = v;

            return r;
        }

        // The recognizer sometimes mixes up triangles and squares, so if it thinks its one of
        // those two count the corners to decide properly
        public static GestureResult Disambiguate(IList<Vector2> points, GestureResult result)
        {
            if (result.Name != Fireball && result.Name != Boulder) return result;
            int corners = GestureRecognizer.CountCorners(points);
            if (corners >= 4) result.Name = Boulder;
            else if (corners == 3) result.Name = Fireball;
            return result;
        }

        static void AddBothWays(GestureRecognizer r, string name, List<Vector2> pts)
        {
            r.AddTemplate(name, pts);
            var rev = new List<Vector2>(pts);
            rev.Reverse();
            r.AddTemplate(name, rev);
        }

        static List<Vector2> Circle(float startDeg, int direction)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 48; i++)
            {
                float a = (startDeg + direction * i * 7.5f) * Mathf.Deg2Rad;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            return pts;
        }

        static List<Vector2> Zigzag(int segments, bool startLow, bool vertical)
        {
            var corners = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float along = i;
                float across = (i % 2 == 0) == startLow ? 0f : 1f;
                // Stops the shape getting too thin, otherwise the recognizer scales it differently
                corners.Add(vertical ? new Vector2(across * segments * 0.45f, -along) : new Vector2(along, across * segments * 0.5f));
            }
            return Densify(corners);
        }

        // Adds extra points along the straight lines so they match up better with how the player draws
        static List<Vector2> Densify(List<Vector2> corners, int perEdge = 12)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < corners.Count - 1; i++)
                for (int k = 0; k < perEdge; k++)
                    pts.Add(Vector2.Lerp(corners[i], corners[i + 1], k / (float)perEdge));
            pts.Add(corners[corners.Count - 1]);
            return pts;
        }

        // Draws the glyph onto a small texture for the spellbook icon
        public static Texture2D RenderIcon(IList<Vector2> pts, Color color, int size = 96)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            float pad = size * 0.16f;
            float scale = (size - 2f * pad) / Mathf.Max(max.x - min.x, max.y - min.y);
            Vector2 offset = new Vector2(size, size) * 0.5f - (min + max) * 0.5f * scale;

            float thickness = size * 0.045f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1] * scale + offset, b = pts[i] * scale + offset;
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
                for (int s = 0; s <= steps; s++) Stamp(px, size, Vector2.Lerp(a, b, s / (float)steps), thickness, color);
            }
            // Put a dot where you start drawing so the player knows where to begin
            Stamp(px, size, pts[0] * scale + offset, thickness * 2.2f, Color.white);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void Stamp(Color[] px, int size, Vector2 c, float radius, Color color)
        {
            int r = Mathf.CeilToInt(radius + 1);
            for (int y = (int)c.y - r; y <= (int)c.y + r; y++)
            for (int x = (int)c.x - r; x <= (int)c.x + r; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float a = Mathf.Clamp01(radius + 0.5f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c));
                if (a <= 0f) continue;
                ref var dst = ref px[y * size + x];
                var src = new Color(color.r, color.g, color.b, a);
                if (src.a >= dst.a) dst = src;
            }
        }
    }
}
