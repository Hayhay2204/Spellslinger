using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellSlinger
{
    // Shows the ending after the Magic Eater dies. Which one you get depends on whether you
    // got all the sects to work together or did it alone
    public class EndingScreen : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text title;
        public TMP_Text body;
        public UnityEngine.UI.Button mainMenu;
        [Tooltip("Seconds after the boss dies before the ending shows, so the death plays out first")]
        public float delay = 5f;
        public string mainMenuScene = "MainMenu";

        public static bool Showing { get; private set; }

        void Awake()
        {
            panel.SetActive(false);
            mainMenu.onClick.AddListener(() =>
            {
                Showing = false;
                GameTime.Paused = false;
                SceneManager.LoadScene(mainMenuScene);
            });
        }

        void OnEnable() => MagicEater.Defeated += OnBossDefeated;
        void OnDisable() => MagicEater.Defeated -= OnBossDefeated;
        void OnDestroy() => Showing = false;

        void OnBossDefeated() => StartCoroutine(ShowAfterDelay());

        IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(delay);
            Showing = true;
            GameTime.Paused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Alliance.AllUnited)
            {
                title.text = "THE SECTS UNITED";
                body.text =
                    "Fire and stone, wind and root, and your storm, all at once. The Magic Eater tried to swallow them and choked on something it could never understand.\n\n" +
                    "The Pyromancers still think the strong should rule. The Earth Guild still wants to build on everything. But for one day they stood together, " +
                    "and the dream from the start finally came true. Magic, whole again.";
            }
            else
            {
                title.text = "THE LAST STORMCALLER";
                string allies = Alliance.Count switch
                {
                    0 => "You did it alone.",
                    1 => "One sect stood with you. The rest watched from a distance.",
                    _ => $"{Alliance.Count} sects stood with you. The others watched from a distance.",
                };
                body.text =
                    $"You learned what the sects would teach and turned it all against the beast. It ate what it could, but it couldnt eat you. {allies}\n\n" +
                    "The sects are safe, but theyre still divided, each still sure their way is the only way. " +
                    "Maybe one day someone will show them what you learned. Magic was never meant to be split apart.";
            }
            panel.SetActive(true);
        }
    }
}
