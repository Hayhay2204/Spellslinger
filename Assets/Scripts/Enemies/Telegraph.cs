using System;
using UnityEngine;

namespace SpellSlinger
{
    // Glowing warning circle on the ground, when it fills up the attack goes off
    public class Telegraph : MonoBehaviour
    {
        [Tooltip("The outline showing how big the attack is")]
        public LineRenderer ring;
        [Tooltip("The circle that grows until the attack goes off")]
        public LineRenderer fill;

        float radius, delay, age;
        Action onFire;
        Material mat;
        Color color;

        public static Telegraph Create(Telegraph prefab, Vector3 groundPos, float radius, float delay, Color color, Action onFire)
        {
            var t = Instantiate(prefab, groundPos + Vector3.up * 0.15f, Quaternion.identity);
            t.radius = radius;
            t.delay = delay;
            t.onFire = onFire;
            t.color = color;
            // both lines share one copy of the material so the colour can change per attack
            t.mat = new Material(t.ring.sharedMaterial);
            t.ring.sharedMaterial = t.mat;
            t.fill.sharedMaterial = t.mat;
            SetCircle(t.ring, radius);
            return t;
        }

        static void SetCircle(LineRenderer lr, float r)
        {
            const int n = 48;
            lr.positionCount = n;
            Vector3 c = lr.transform.position;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                p.y = World.HeightAt(p) + 0.15f;
                lr.SetPosition(i, p);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / delay);
            SetCircle(fill, Mathf.Max(0.1f, radius * t));
            mat.SetColor("_BaseColor", color * (3f + Mathf.Sin(age * 20f)));
            if (age >= delay)
            {
                onFire?.Invoke();
                Destroy(gameObject);
            }
        }

        void OnDestroy() { if (mat) Destroy(mat); }
    }
}
