using System;
using TMPro;
using UnityEngine;

namespace SpellSlinger
{
    // The settings screen, used by both the main menu and the pause menu. Its just audio and
    // key bindings. Everything saves as soon as you change it
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Audio")]
        public UnityEngine.UI.Slider masterVolume;
        public TMP_Text masterVolumeValue;
        public UnityEngine.UI.Slider musicVolume;
        public TMP_Text musicVolumeValue;
        public UnityEngine.UI.Slider sfxVolume;
        public TMP_Text sfxVolumeValue;

        [Header("Key Bindings")]
        public UnityEngine.UI.Button resetKeys;

        public UnityEngine.UI.Button back;

        // Whoever opened the settings sets this so Back takes you to the right place
        public Action onBack;

        void Awake()
        {
            Bind(masterVolume, masterVolumeValue, v => GameSettings.MasterVolume = v);
            Bind(musicVolume, musicVolumeValue, v => GameSettings.MusicVolume = v);
            Bind(sfxVolume, sfxVolumeValue, v => GameSettings.SfxVolume = v);
            if (resetKeys) resetKeys.onClick.AddListener(Controls.ResetToDefaults);
            if (back) back.onClick.AddListener(() => onBack?.Invoke());
        }

        // Fill everything in with the saved values whenever the menu opens
        void OnEnable()
        {
            Show(masterVolume, masterVolumeValue, GameSettings.MasterVolume);
            Show(musicVolume, musicVolumeValue, GameSettings.MusicVolume);
            Show(sfxVolume, sfxVolumeValue, GameSettings.SfxVolume);
        }

        void Update()
        {
            // Esc works as a back button too, unless a key rebind is listening for a key
            if (Controls.EscapeUnused)
            {
                Controls.UseEscape();
                onBack?.Invoke();
            }
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        static void Bind(UnityEngine.UI.Slider slider, TMP_Text label, Action<float> save)
        {
            if (!slider) return;
            slider.onValueChanged.AddListener(v =>
            {
                save(v);
                if (label) label.text = Percent(v);
            });
        }

        static void Show(UnityEngine.UI.Slider slider, TMP_Text label, float value)
        {
            if (!slider) return;
            slider.SetValueWithoutNotify(value);
            if (label) label.text = Percent(value);
        }
    }
}
