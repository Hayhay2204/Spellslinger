using UnityEngine;

namespace SpellSlinger
{
    // Plays the dream dialogue when the game starts
    public class IntroDialogue : MonoBehaviour
    {
        public DialogueSystem dialogue;
        public string speaker = "A voice in your dreams";
        public Color speakerColor = new(0.7f, 0.6f, 1f);
        [TextArea(2, 4)] public string[] lines;
        public bool skip;

        void Start()
        {
            if (!skip && dialogue && lines != null && lines.Length > 0)
                dialogue.Say(speaker, "", speakerColor, lines);
        }
    }
}
