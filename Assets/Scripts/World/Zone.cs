using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // An area of the world. Shows its name when you walk in and has a label on the map
    public class Zone : MonoBehaviour
    {
        public static readonly List<Zone> All = new();

        public string zoneName = "Unnamed Zone";
        public string subtitle;
        public float radius = 50f;
        public Color mapColor = Color.white;
        [Tooltip("Turn this on for areas that are up in the air, like Zephyr Isle")]
        public bool floating;
        public bool showOnMap = true;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = mapColor;
            const int segments = 64;
            Vector3 prev = transform.position + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
