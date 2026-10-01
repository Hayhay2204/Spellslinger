using UnityEngine;

namespace SpellSlinger
{
    // Handles slowing down time, both while you're drawing a spell and when the boss eats a spell
    public class GameTime : MonoBehaviour
    {
        public static bool Targeting { get; set; }
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
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;
        }

        void OnDestroy()
        {
            Targeting = false;
            dramaticUntil = 0f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
