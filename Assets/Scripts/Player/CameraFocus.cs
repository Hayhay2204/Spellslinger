using System;
using UnityEngine;

namespace SpellSlinger
{
    // Little cutscene moment for when the boss eats your magic. Swings the camera round to look at it,
    // zooms in, cuts all the sound, then brings it all back in at the "gulp"
    public class CameraFocus : MonoBehaviour
    {
        [Tooltip("How far it zooms in (field of view) at the peak")]
        public float zoomFov = 40f;
        public float easeIn = 0.4f;
        public float easeOut = 0.6f;

        static CameraFocus instance;

        Camera cam;
        float baseFov, startTime, length, gulpAt;
        Vector3 focusPoint;
        Action onGulp;
        bool gulped, playing;

        public static bool Playing => instance && instance.playing;

        // seconds and gulpDelay are real time so the slow motion doesnt stretch them out
        public static void Play(Vector3 point, float seconds, float gulpDelay, Action gulp)
        {
            var cam = Camera.main;
            if (!cam) { gulp?.Invoke(); return; }
            // TryGetComponent instead of ?? because in the editor GetComponent gives back a fake null that ?? doesnt catch
            if (!instance && !cam.TryGetComponent(out instance)) instance = cam.gameObject.AddComponent<CameraFocus>();
            instance.Begin(cam, point, seconds, gulpDelay, gulp);
        }

        void Begin(Camera c, Vector3 point, float seconds, float gulpDelay, Action gulp)
        {
            if (playing) FinishGulp(); // if one was already going make sure its effects still happen
            cam = c;
            if (!playing) baseFov = cam.fieldOfView;
            focusPoint = point;
            length = seconds;
            gulpAt = gulpDelay;
            onGulp = gulp;
            gulped = false;
            playing = true;
            startTime = Time.unscaledTime;
        }

        void LateUpdate()
        {
            if (!playing) return;
            float t = Time.unscaledTime - startTime;

            if (!gulped)
            {
                // everything goes quiet while it breathes in
                AudioListener.volume = GameSettings.MasterVolume * (1f - Mathf.Clamp01(t / 0.3f));
                if (t >= gulpAt) FinishGulp();
            }

            if (t >= length)
            {
                cam.fieldOfView = baseFov;
                AudioListener.volume = GameSettings.MasterVolume;
                playing = false;
                return;
            }

            float weight = Mathf.SmoothStep(0f, 1f, t / easeIn) * Mathf.SmoothStep(0f, 1f, (length - t) / easeOut);
            Vector3 dir = focusPoint - transform.position;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), weight);
            cam.fieldOfView = Mathf.Lerp(baseFov, zoomFov, weight);
        }

        void FinishGulp()
        {
            gulped = true;
            AudioListener.volume = GameSettings.MasterVolume;
            var g = onGulp;
            onGulp = null;
            g?.Invoke();
        }

        void OnDisable()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (cam && playing) cam.fieldOfView = baseFov;
            playing = false;
        }
    }
}
