using UnityEngine;

namespace SpellSlinger
{
    // Makes the fire lights flicker
    public class Flicker : MonoBehaviour
    {
        Light l;
        float baseIntensity, seed;

        void Start() { l = GetComponent<Light>(); baseIntensity = l.intensity; seed = Random.value * 100f; }
        void Update() => l.intensity = baseIntensity * (0.75f + Mathf.PerlinNoise(seed, Time.time * 6f) * 0.5f);
    }
}
