using UnityEngine;

namespace SpellSlinger
{
    // Handles slowing down time, both while you're drawing a spell and when the boss eats a spell
    public class GameTime : MonoBehaviour
    {
        public static bool Targeting { get; set; }
        // Set by the pause menu, stops time completely
        public static bool Paused { get; set; }
        public static float TargetingScale { get; set; } = 0.3f;
        static float dramaticScale = 1f, dramaticUntil;

        public static void Dramatic(float scale, float realSeconds)
        {
            dramaticScale = scale;
            dramaticUntil = Time.unscaledTime + realSeconds;
        }

        void Update()
        {
            float scale = 1f;
            if (Targeting) scale = Mathf.Min(scale, TargetingScale);
            if (Time.unscaledTime < dramaticUntil) scale = Mathf.Min(scale, dramaticScale);
            if (Paused) scale = 0f;
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * Mathf.Max(scale, 0.01f); // fixed delta cant be 0
        }

        void OnDestroy()
        {
            Targeting = false;
            Paused = false;
            dramaticUntil = 0f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
