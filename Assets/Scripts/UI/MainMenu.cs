using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SpellSlinger
{
    // The title screen
    public class MainMenu : MonoBehaviour
    {
        public GameObject mainPanel;
        public SettingsMenu settings;
        public UnityEngine.UI.Button newGame;
        public UnityEngine.UI.Button openSettings;
        public UnityEngine.UI.Button quit;
        public string gameScene = "Overworld";

        [Tooltip("Optional. Slowly spins around this so the background isnt just sitting still")]
        public Transform cameraPivot;
        public float spinSpeed = 4f;

        void Awake()
        {
            newGame.onClick.AddListener(StartNewGame);
            openSettings.onClick.AddListener(ShowSettings);
            quit.onClick.AddListener(Quit);
            settings.onBack = ShowMain;
        }

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowMain();
        }

        void Update()
        {
            if (cameraPivot) cameraPivot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        void StartNewGame()
        {
            PlayerMagic.ResetProgress();
            Alliance.ResetProgress();
            SceneManager.LoadScene(gameScene);
        }

        void ShowMain()
        {
            settings.gameObject.SetActive(false);
            mainPanel.SetActive(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(newGame.gameObject);
        }

        void ShowSettings()
        {
            mainPanel.SetActive(false);
            settings.gameObject.SetActive(true);
            if (EventSystem.current && settings.back) EventSystem.current.SetSelectedGameObject(settings.back.gameObject);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
