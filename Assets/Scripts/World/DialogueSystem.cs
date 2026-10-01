using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // Finds the closest NPC you can talk to and handles the conversations
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        public PlayerController player;
        public SpellCaster caster;

        public NPC InRange { get; private set; }
        public bool Active => lines != null;
        public string Speaker { get; private set; }
        public string SpeakerTitle { get; private set; }
        public Color SpeakerColor { get; private set; }
        public string CurrentLine => Active ? lines[index] : null;
        public float LineStartTime { get; private set; }

        string[] lines;
        int index;
        NPC talkingTo;

        void Awake() => Instance = this;

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || !player) return;

            if (Active)
            {
                bool advance = kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame;
                if (kb.escapeKey.wasPressedThisFrame) End();
                else if (advance)
                {
                    // First press shows the whole line, second press goes to the next one
                    if (!LineFullyShown()) LineStartTime = -99f;
                    else if (++index >= lines.Length) Finish();
                    else LineStartTime = Time.unscaledTime;
                }
                return;
            }

            InRange = null;
            if (player.IsDead || caster.IsTargeting) return;
            float best = 3.5f * 3.5f;
            foreach (var npc in NPC.All)
            {
                float d = (npc.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; InRange = npc; }
            }
            if (InRange && kb.eKey.wasPressedThisFrame) Begin(InRange);
        }

        public int VisibleChars => Mathf.FloorToInt((Time.unscaledTime - LineStartTime) * 55f);
        bool LineFullyShown() => VisibleChars >= CurrentLine.Length;

        public void Begin(NPC npc) => Say(npc.displayName, npc.title, npc.color, npc.CurrentLines, npc);

        // Starts a conversation that doesnt need an NPC, like the dream at the start
        public void Say(string speaker, string title, Color color, string[] text, NPC npc = null)
        {
            if (text == null || text.Length == 0) return;
            talkingTo = npc;
            Speaker = speaker;
            SpeakerTitle = title;
            SpeakerColor = color;
            lines = text;
            index = 0;
            LineStartTime = Time.unscaledTime;
            player.MovementLocked = true;
            caster.Blocked = true;
        }

        void Finish()
        {
            var npc = talkingTo;
            End();
            if (npc && npc.teaches && !PlayerMagic.Knows(npc.teachesElement))
            {
                PlayerMagic.Learn(npc.teachesElement);
                npc.PlayTeach();
                var spell = SpellBook.Get(npc.teachesElement);
                caster.ShowMessage($"Learned {spell.Name}!  Draw a {spell.Glyph}", spell.Color);
                FX.Burst(player.transform.position + Vector3.up, spell.Color, 80, 5f, 0.15f, 1.2f, -0.5f);
                SFX.Play2D(SFX.Chime, 1f, 0f);
            }
        }

        void End()
        {
            lines = null;
            talkingTo = null;
            player.MovementLocked = false;
            caster.Blocked = false;
        }
    }
}
