using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // The chain lightning spell. Also just draws a lightning bolt when the boss uses it
    [RequireComponent(typeof(LineRenderer))]
    public class LightningArc : MonoBehaviour
    {
        [Header("Look")]
        public float width = 0.09f;
        public float life = 0.3f;

        [Header("Spell (only used when the player casts it)")]
        public float damage = 34f;
        [Tooltip("How many extra enemies it jumps to after the first one")]
        public int chains = 3;
        public float chainRange = 10f;
        [Tooltip("Damage gets multiplied by this every jump")]
        public float damageFalloff = 0.75f;
        public float stunTime = 0.6f;
        public Color hitColor = new(0.8f, 0.7f, 1f);

        List<Vector3> points;
        LineRenderer line;
        float age, nextJitter, widthScale = 1f;

        // Draws a bolt through the points. widthScale makes it thicker, the boss uses this for its strikes
        public static LightningArc Create(LightningArc prefab, List<Vector3> points, float widthScale = 1f)
        {
            var arc = Instantiate(prefab);
            arc.points = points;
            arc.widthScale = widthScale;
            arc.line = arc.GetComponent<LineRenderer>();
            arc.line.useWorldSpace = true;
            arc.Rebuild();
            return arc;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            line.widthMultiplier = width * widthScale * (1f - age / life);
            if (Time.time >= nextJitter) Rebuild();
        }

        void Rebuild()
        {
            nextJitter = Time.time + 0.035f;
            var jagged = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float len = Vector3.Distance(a, b);
                int segs = Mathf.Max(4, Mathf.CeilToInt(len / 0.7f));
                for (int s = 0; s < segs; s++)
                {
                    float jitter = s == 0 ? 0f : Mathf.Min(0.35f, len * 0.05f) * widthScale;
                    jagged.Add(Vector3.Lerp(a, b, s / (float)segs) + Random.insideUnitSphere * jitter);
                }
            }
            jagged.Add(points[points.Count - 1]);
            line.positionCount = jagged.Count;
            line.SetPositions(jagged.ToArray());
        }
    }
}
