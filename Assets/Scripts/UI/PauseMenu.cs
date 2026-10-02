using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SpellSlinger
{
    // Esc pauses the game. Runs after everything else so if Esc was already used to cancel a spell,
    // close the map or leave a conversation it doesnt pause as well
    [DefaultExecutionOrder(100)]
    public class PauseMenu : MonoBehaviour
    {
        public GameObject pausePanel;
        public SettingsMenu settings;
        public UnityEngine.UI.Button resume;
        public UnityEngine.UI.Button openSettings;
        public UnityEngine.UI.Button mainMenu;
        public UnityEngine.UI.Button quit;
        public string mainMenuScene = "MainMenu";

        public static bool IsOpen { get; private set; }

        void Awake()
        {
            resume.onClick.AddListener(Close);
            openSettings.onClick.AddListener(ShowSettings);
            mainMenu.onClick.AddListener(GoToMainMenu);
            quit.onClick.AddListener(Quit);
            settings.onBack = ShowPause;
            pausePanel.SetActive(false);
            settings.gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsOpen || EndingScreen.Showing || !Controls.EscapeUnused) return;
            Controls.UseEscape();
            Open();
        }

        // Esc while paused closes it (the settings menu handles its own Esc as a back button)
        void LateUpdate()
        {
            if (IsOpen && pausePanel.activeSelf && Controls.EscapeUnused)
            {
                Controls.UseEscape();
                Close();
            }
        }

        public void Open()
        {
            IsOpen = true;
            GameTime.Paused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowPause();
        }

        public void Close()
        {
            IsOpen = false;
            GameTime.Paused = false;
            pausePanel.SetActive(false);
            settings.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ShowPause()
        {
            settings.gameObject.SetActive(false);
            pausePanel.SetActive(true);
            Select(resume.gameObject);
        }

        void ShowSettings()
        {
            pausePanel.SetActive(false);
            settings.gameObject.SetActive(true);
            Select(settings.back ? settings.back.gameObject : null);
        }

        void GoToMainMenu()
        {
            IsOpen = false;
            GameTime.Paused = false;
            SceneManager.LoadScene(mainMenuScene);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Select(GameObject go)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(go);
        }

        void OnDestroy()
        {
            IsOpen = false;
            GameTime.Paused = false;
        }
    }
}
