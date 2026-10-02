using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpellSlinger
{
    // All the player settings from the settings menu. They get saved in PlayerPrefs and applied
    // when the game starts
    public static class GameSettings
    {
        public static event Action Changed;

        public static float MasterVolume { get => GetFloat("masterVolume", 1f); set { SetFloat("masterVolume", value); AudioListener.volume = value; } }
        public static float MusicVolume { get => GetFloat("musicVolume", 0.8f); set => SetFloat("musicVolume", value); }
        public static float SfxVolume { get => GetFloat("sfxVolume", 1f); set => SetFloat("sfxVolume", value); }
        public static float MouseSensitivity { get => GetFloat("mouseSensitivity", 1f); set => SetFloat("mouseSensitivity", value); }
        public static float DrawSensitivity { get => GetFloat("drawSensitivity", 1f); set => SetFloat("drawSensitivity", value); }
        public static bool InvertY { get => PlayerPrefs.GetInt("invertY", 0) == 1; set => SetInt("invertY", value ? 1 : 0); }

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set { SetInt("quality", value); QualitySettings.SetQualityLevel(value, true); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt("fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            set { SetInt("fullscreen", value ? 1 : 0); ApplyResolution(); }
        }

        // Saved as "1920x1080" so it still works if the list of resolutions changes
        public static string Resolution
        {
            get => PlayerPrefs.GetString("resolution", $"{Screen.currentResolution.width}x{Screen.currentResolution.height}");
            set { PlayerPrefs.SetString("resolution", value); PlayerPrefs.Save(); ApplyResolution(); Changed?.Invoke(); }
        }

        // Every resolution the screen supports, without the duplicates for different refresh rates
        public static List<string> Resolutions =>
            Screen.resolutions.Select(r => $"{r.width}x{r.height}").Distinct().Reverse().ToList();

        static void ApplyResolution()
        {
            var parts = Resolution.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h)) return;
            Screen.SetResolution(w, h, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.SetQualityLevel(Quality, true);
#if !UNITY_EDITOR
            ApplyResolution(); // the editor game view picks its own size so only do this in builds
#endif
        }

        static float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
