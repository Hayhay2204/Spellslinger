using UnityEngine;

namespace SpellSlinger
{
    // Shakes the camera, more trauma means more shake
    public class CameraShake : MonoBehaviour
    {
        static CameraShake instance;
        float trauma;
        Vector3 basePos;

        public static void Add(float amount)
        {
            if (instance) instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        void Awake() { instance = this; basePos = transform.localPosition; }

        void LateUpdate()
        {
            trauma = Mathf.MoveTowards(trauma, 0f, Time.unscaledDeltaTime * 1.5f);
            float s = trauma * trauma * 0.15f;
            float t = Time.unscaledTime * 25f;
            transform.localPosition = basePos + new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * s;
        }
    }
}
