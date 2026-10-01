using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger.Gestures
{
    public struct GestureResult
    {
        public string Name;
        public float Score; // 0 to 1, higher means its a better match
    }

    // This is based on the $1 Unistroke Recognizer (Wobbrock et al.) with a few changes.
    // Normally it doesnt care which way a shape is rotated, but then a V and an upside down V would be the
    // same spell. So it saves the angle each shape starts at and only compares it to templates that start
    // at a similar angle. There are a few versions of each template so it doesnt matter which corner you
    // start from or which way you draw it
    public class GestureRecognizer
    {
        const int SampleCount = 64;
        const float SquareSize = 250f;
        static readonly float HalfDiagonal = 0.5f * Mathf.Sqrt(2f * SquareSize * SquareSize);
        const float AngleRange = 45f * Mathf.Deg2Rad;
        const float MaxOrientationDiff = 50f;
        const float AnglePrecision = 2f * Mathf.Deg2Rad;
        static readonly float Phi = 0.5f * (-1f + Mathf.Sqrt(5f));

        readonly List<(string name, Vector2[] points, float angle)> templates = new();

        public int TemplateCount => templates.Count;

        public void AddTemplate(string name, IList<Vector2> points)
        {
            var pts = Normalize(points, out float angle);
            templates.Add((name, pts, angle));
        }

        public GestureResult Recognize(IList<Vector2> points)
        {
            if (points.Count < 5 || templates.Count == 0) return default;

            var candidate = Normalize(points, out float candidateAngle);
            float best = float.MaxValue;
            string bestName = null;
            foreach (var (name, tpl, angle) in templates)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(candidateAngle, angle)) > MaxOrientationDiff) continue;
                float d = DistanceAtBestAngle(candidate, tpl, -AngleRange, AngleRange, AnglePrecision);
                if (d < best) { best = d; bestName = name; }
            }
            return new GestureResult { Name = bestName, Score = 1f - best / HalfDiagonal };
        }

        // indicativeAngle is the angle (in degrees) from the middle of the shape to the first point
        static Vector2[] Normalize(IList<Vector2> points, out float indicativeAngle)
        {
            var pts = Resample(points, SampleCount);
            var c = Centroid(pts);
            float radians = Mathf.Atan2(pts[0].y - c.y, pts[0].x - c.x);
            indicativeAngle = radians * Mathf.Rad2Deg;
            return TranslateToOrigin(ScaleToSquare(RotateBy(pts, c, -radians)));
        }

        static Vector2[] RotateBy(Vector2[] pts, Vector2 c, float radians)
        {
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            var result = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var d = pts[i] - c;
                result[i] = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + c;
            }
            return result;
        }

        static Vector2[] Resample(IList<Vector2> input, int n)
        {
            var pts = new List<Vector2>(input);
            float interval = PathLength(pts) / (n - 1);
            float accumulated = 0f;
            var result = new List<Vector2>(n) { pts[0] };

            for (int i = 1; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i - 1], pts[i]);
                if (d > 0f && accumulated + d >= interval)
                {
                    var q = pts[i - 1] + (interval - accumulated) / d * (pts[i] - pts[i - 1]);
                    result.Add(q);
                    pts.Insert(i, q);
                    accumulated = 0f;
                }
                else accumulated += d;
            }

            while (result.Count < n) result.Add(pts[pts.Count - 1]);
            if (result.Count > n) result.RemoveRange(n, result.Count - n);
            return result.ToArray();
        }

        static Vector2[] ScaleToSquare(Vector2[] pts)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            float w = Mathf.Max(max.x - min.x, 1e-5f), h = Mathf.Max(max.y - min.y, 1e-5f);
            float longest = Mathf.Max(w, h);

            // Really thin shapes get stretched way too much, so those get scaled evenly instead
            bool uniform = Mathf.Min(w, h) / longest < 0.3f;
            float sx = SquareSize / (uniform ? longest : w);
            float sy = SquareSize / (uniform ? longest : h);

            var result = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                result[i] = new Vector2((pts[i].x - min.x) * sx, (pts[i].y - min.y) * sy);
            return result;
        }

        static Vector2[] TranslateToOrigin(Vector2[] pts)
        {
            var c = Centroid(pts);
            var result = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) result[i] = pts[i] - c;
            return result;
        }

        static float DistanceAtBestAngle(Vector2[] pts, Vector2[] tpl, float a, float b, float threshold)
        {
            float x1 = Phi * a + (1f - Phi) * b, f1 = DistanceAtAngle(pts, tpl, x1);
            float x2 = (1f - Phi) * a + Phi * b, f2 = DistanceAtAngle(pts, tpl, x2);
            while (Mathf.Abs(b - a) > threshold)
            {
                if (f1 < f2)
                {
                    b = x2; x2 = x1; f2 = f1;
                    x1 = Phi * a + (1f - Phi) * b; f1 = DistanceAtAngle(pts, tpl, x1);
                }
                else
                {
                    a = x1; x1 = x2; f1 = f2;
                    x2 = (1f - Phi) * a + Phi * b; f2 = DistanceAtAngle(pts, tpl, x2);
                }
            }
            return Mathf.Min(f1, f2);
        }

        static float DistanceAtAngle(Vector2[] pts, Vector2[] tpl, float radians)
        {
            // The points are already centred so it can just rotate around 0,0
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            float sum = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = pts[i];
                var r = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
                sum += Vector2.Distance(r, tpl[i]);
            }
            return sum / pts.Length;
        }

        static Vector2 Centroid(IList<Vector2> pts)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in pts) sum += p;
            return sum / pts.Count;
        }

        // Counts the sharp corners in a drawing. Used to tell triangles and squares apart since the
        // recognizer sometimes mixes them up when they're drawn sloppy.
        // If the shape is closed (ends near where it started) it counts around the whole loop
        public static int CountCorners(IList<Vector2> input, float minTurn = 50f)
        {
            if (input.Count < 6) return 0;
            var pts = Resample(input, SampleCount);
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            float size = Mathf.Max(max.x - min.x, max.y - min.y, 1e-5f);
            bool closed = Vector2.Distance(pts[0], pts[pts.Length - 1]) < size * 0.25f;

            int n = closed ? pts.Length - 1 : pts.Length; // last point of a closed shape is the same as the first
            const int w = 4; // how far either side to look when measuring the turn
            var turn = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i < w || i >= n - w)) continue;
                Vector2 before = pts[i] - pts[(i - w + n) % n];
                Vector2 after = pts[(i + w) % n] - pts[i];
                if (before.sqrMagnitude < 1e-6f || after.sqrMagnitude < 1e-6f) continue;
                turn[i] = Vector2.Angle(before, after);
            }

            // a corner is the sharpest point in its neighbourhood that turns more than minTurn
            int corners = 0;
            for (int i = 0; i < n; i++)
            {
                if (turn[i] < minTurn) continue;
                bool peak = true;
                for (int k = -w; k <= w && peak; k++)
                {
                    if (k == 0) continue;
                    int j = closed ? (i + k + n) % n : i + k;
                    if (j < 0 || j >= n) continue;
                    if (turn[j] > turn[i] || (turn[j] == turn[i] && k < 0)) peak = false;
                }
                if (peak) corners++;
            }
            // an open shape that starts on a corner doesnt get that corner counted, so add it back
            return closed ? corners : corners + 1;
        }

        public static float PathLength(IList<Vector2> pts)
        {
            float d = 0f;
            for (int i = 1; i < pts.Count; i++) d += Vector2.Distance(pts[i - 1], pts[i]);
            return d;
        }
    }
}
