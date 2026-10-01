using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // Druid vines. Makes a patch of vines that roots and damages anything standing in it.
    // If you cast it on something flying, giant vines grow up from the ground and grab it.
    // Anything immune to druid magic cant be grabbed, the vines just wither away
    public class VineField : MonoBehaviour
    {
        [Header("Stats")]
        public float radius = 3.5f;
        public float duration = 3.5f;
        public float impactDamage = 20f;
        public float tickDamage = 6f;
        [Tooltip("Targets higher than this get grabbed by the giant vines")]
        public float grabHeight = 3f;

        [Header("Look")]
        public GameObject stemPrefab;
        public GameObject thornPrefab;
        public int groundVines = 12;
        public int giantVines = 4;
        public Color burstColor = new(0.4f, 1f, 0.3f);

        readonly List<Transform> vines = new();
        Combatant seized;
        float age, nextTick;

        public static VineField Create(VineField prefab, Vector3 groundPos, Combatant target)
        {
            var field = Instantiate(prefab, groundPos, Quaternion.identity);

            // Vines on the ground
            for (int i = 0; i < field.groundVines; i++)
            {
                Vector2 r = Random.insideUnitCircle * field.radius;
                var pivot = field.NewPivot(new Vector3(r.x, 0f, r.y));
                pivot.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                float h = Random.Range(1.5f, 3.2f);
                field.Piece(field.stemPrefab, pivot, Vector3.up * h * 0.5f, new Vector3(0.18f, h * 0.5f, 0.18f), Quaternion.identity);
                field.Piece(field.thornPrefab, pivot, Vector3.up * h, Vector3.one * 0.25f, Quaternion.identity);
            }

            // Big vines that climb up to the target if its in the air
            float reach = target != null ? target.AimPoint.y - groundPos.y : 0f;
            bool withered = false;
            if (target != null && reach > field.grabHeight)
            {
                float wrapRadius = Mathf.Max(0.8f, target.radius * 0.9f);
                for (int v = 0; v < field.giantVines; v++)
                    field.BuildSpiral(field.NewPivot(Vector3.zero), v * 360f / field.giantVines, wrapRadius + 2.5f, wrapRadius, reach + target.radius * 0.5f);

                if (target.Resists(Element.Nature))
                {
                    // It cant be held, so the vines reach it and then wither straight away
                    target.TakeDamage(field.impactDamage, Vector3.zero, Element.Nature);
                    field.duration = 0.9f;
                    withered = true;
                }
                else field.seized = target;
            }

            FX.Burst(groundPos + Vector3.up * 0.3f, field.burstColor, 50, 5f, 0.15f, 0.9f, 1f);
            FX.Burst(groundPos, new Color(0.45f, 0.3f, 0.15f), 25, 4f, 0.4f, 0.7f, 2f);
            SFX.PlayAt(SFX.Frost, groundPos, 0.8f);

            field.Seize(field.impactDamage);
            foreach (var t in Combatant.InRadius(groundPos + Vector3.up, field.radius))
            {
                if (t == field.seized || (withered && t == target)) continue;
                t.Root(field.duration);
                t.TakeDamage(15f, Vector3.zero, Element.Nature);
            }
            return field;
        }

        Transform NewPivot(Vector3 localPos)
        {
            var pivot = new GameObject("Vine").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = localPos;
            pivot.localScale = new Vector3(1f, 0.01f, 1f);
            vines.Add(pivot);
            return pivot;
        }

        void Piece(GameObject prefab, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rot)
        {
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
        }

        // Makes a vine out of stem pieces that spirals up around the target
        void BuildSpiral(Transform pivot, float startAngle, float baseRadius, float topRadius, float height)
        {
            int segments = Mathf.Clamp(Mathf.CeilToInt(height / 1.2f), 6, 30);
            Vector3 Point(int k)
            {
                float t = k / (float)segments;
                float a = (startAngle + t * 300f) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(baseRadius, topRadius, Mathf.SmoothStep(0f, 1f, t));
                return new Vector3(Mathf.Cos(a) * r, t * height, Mathf.Sin(a) * r);
            }
            for (int k = 0; k < segments; k++)
            {
                Vector3 a = Point(k), b = Point(k + 1);
                float thickness = Mathf.Lerp(0.7f, 0.3f, k / (float)segments);
                Piece(stemPrefab, pivot, (a + b) * 0.5f, new Vector3(thickness, Vector3.Distance(a, b) * 0.55f, thickness), Quaternion.FromToRotation(Vector3.up, b - a));
                if (k % 4 == 3) Piece(thornPrefab, pivot, b, Vector3.one * 0.35f, Quaternion.identity);
            }
        }

        void Seize(float damage)
        {
            if (seized == null || seized.IsDead) return;
            seized.Root(0.8f);
            seized.TakeDamage(damage, Vector3.zero, Element.Nature);
        }

        void Update()
        {
            age += Time.deltaTime;
            float grow = Mathf.Clamp01(age / 0.35f) * Mathf.Clamp01((duration - age) / 0.4f);
            for (int i = 0; i < vines.Count; i++)
                vines[i].localScale = new Vector3(1f, Mathf.Max(0.01f, grow), 1f);

            if (age >= nextTick && age < duration)
            {
                nextTick = age + 0.5f;
                Seize(tickDamage);
                foreach (var t in Combatant.InRadius(transform.position + Vector3.up, radius))
                {
                    if (t == seized) continue;
                    t.Root(0.6f);
                    t.TakeDamage(tickDamage, Vector3.zero, Element.Nature);
                }
            }
            if (age >= duration) Destroy(gameObject);
        }
    }
}
