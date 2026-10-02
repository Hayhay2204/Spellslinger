using System;
using UnityEngine;

namespace SpellSlinger
{
    // Makes all the sound effects through code so I didnt need to import any audio yet
    public static class SFX
    {
        const int Rate = 44100;
        static AudioSource source2D;
        static readonly System.Random rng = new(1234);

        static AudioClip whoosh, boom, zap, shimmer, fizzle, hurt, pop, chime, frost;

        public static AudioClip Whoosh => whoosh ??= Make("Whoosh", 0.35f, (t, n) => Noise() * Mathf.Sin(n * Mathf.PI) * 0.5f);
        public static AudioClip Boom => boom ??= MakeFiltered("Boom", 1.1f, 0.08f, (t, n) => (Noise() + Mathf.Sin(t * 2 * Mathf.PI * 55f)) * Mathf.Exp(-t * 4.5f));
        public static AudioClip Zap => zap ??= Make("Zap", 0.45f, (t, n) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * (90f + 400f * Noise01()))) * Noise01() * Mathf.Exp(-t * 7f) * 0.6f);
        public static AudioClip Shimmer => shimmer ??= Make("Shimmer", 0.9f, (t, n) =>
            (Mathf.Sin(t * 2 * Mathf.PI * 660f) + Mathf.Sin(t * 2 * Mathf.PI * 990f) * 0.6f + Mathf.Sin(t * 2 * Mathf.PI * 1320f * (1f + 0.01f * Mathf.Sin(t * 30f))) * 0.4f)
            * Mathf.Sin(n * Mathf.PI) * 0.25f);
        public static AudioClip Fizzle => fizzle ??= MakeFiltered("Fizzle", 0.4f, 0.3f, (t, n) => Noise() * Mathf.Exp(-t * 9f) * 0.5f);
        public static AudioClip Hurt => hurt ??= Make("Hurt", 0.25f, (t, n) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * Mathf.Lerp(220f, 90f, n))) * (1f - n) * 0.3f);
        public static AudioClip Pop => pop ??= Make("Pop", 0.35f, (t, n) => (Mathf.Sin(t * 2 * Mathf.PI * Mathf.Lerp(700f, 80f, n)) * 0.7f + Noise() * 0.3f) * (1f - n) * 0.6f);
        public static AudioClip Chime => chime ??= Make("Chime", 1.2f, (t, n) => (Mathf.Sin(t * 2 * Mathf.PI * 523f) + Mathf.Sin(t * 2 * Mathf.PI * 784f) * 0.5f) * Mathf.Exp(-t * 3f) * 0.3f);
        public static AudioClip Frost => frost ??= MakeFiltered("Frost", 0.4f, 0.6f, (t, n) => (Noise() * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 1800f) * 0.2f) * Mathf.Exp(-t * 8f));

        public static void Play2D(AudioClip clip, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (source2D == null)
            {
                var cam = Camera.main;
                if (!cam) return;
                source2D = cam.gameObject.AddComponent<AudioSource>();
                source2D.playOnAwake = false;
                source2D.spatialBlend = 0f;
            }
            source2D.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            source2D.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            var go = new GameObject("SFX");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume * GameSettings.SfxVolume;
            src.spatialBlend = 0.8f;
            src.minDistance = 4f;
            src.maxDistance = 60f;
            src.pitch = 1f + UnityEngine.Random.Range(-0.1f, 0.1f);
            src.Play();
            UnityEngine.Object.Destroy(go, clip.length + 0.1f);
        }

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Noise01() => (float)rng.NextDouble();

        // f gets the time in seconds and how far through the sound it is (0 to 1) and returns the sample
        static AudioClip Make(string name, float duration, Func<float, float, float> f)
        {
            int count = Mathf.CeilToInt(duration * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i / (float)count), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Same as Make but muffles the sound a bit, a lower alpha makes it more muffled
        static AudioClip MakeFiltered(string name, float duration, float alpha, Func<float, float, float> f)
        {
            float y = 0f;
            return Make(name, duration, (t, n) => y += alpha * (f(t, n) - y));
        }
    }
}
