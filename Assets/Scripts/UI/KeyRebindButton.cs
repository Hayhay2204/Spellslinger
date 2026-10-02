using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // One row in the key bindings list. Click it, then press the key you want. Esc cancels
    [DefaultExecutionOrder(-10)] // gets first go at Esc so it cancels the rebind instead of closing the menu
    public class KeyRebindButton : MonoBehaviour
    {
        public GameAction action;
        public UnityEngine.UI.Button button;
        public TMP_Text keyLabel;

        bool listening;

        void Awake()
        {
            if (button) button.onClick.AddListener(StartListening);
        }

        void OnEnable()
        {
            Controls.Changed += Refresh;
            listening = false;
            Refresh();
        }

        void OnDisable() => Controls.Changed -= Refresh;

        void StartListening()
        {
            listening = true;
            if (keyLabel) keyLabel.text = "Press a key...";
            // deselect it so pressing Space or Enter to bind doesnt click the button again
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        void Update()
        {
            if (!listening || Keyboard.current == null) return;

            if (Controls.EscapePressed)
            {
                Controls.UseEscape(); // so it doesnt also close the settings menu
                listening = false;
                Refresh();
                return;
            }

            foreach (var key in Keyboard.current.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame) continue;
                listening = false;
                Controls.Set(action, key.keyCode);
                return;
            }
        }

        void Refresh()
        {
            if (keyLabel) keyLabel.text = Controls.KeyName(action);
        }
    }
}
