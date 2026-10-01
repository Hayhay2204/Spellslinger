using UnityEngine;

namespace SpellSlinger
{
    public class FadeLight : MonoBehaviour
    {
        public float duration = 0.3f;
        Light l;
        float start, t;

        void Awake() { l = GetComponent<Light>(); start = l.intensity; }

        void Update()
        {
            t += Time.deltaTime;
            l.intensity = start * (1f - t / duration);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
