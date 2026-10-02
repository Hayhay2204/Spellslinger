using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpellSlinger
{
    // Everything the player can rebind in the settings menu
    public enum GameAction { MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Sprint, ReadyWand, Talk, Map, Respawn }

    // All the keyboard controls go through here so they can be rebound. Bindings are saved in PlayerPrefs.
    // Drawing and casting stay on the mouse buttons
    public static class Controls
    {
        public static event Action Changed;

        static readonly Dictionary<GameAction, Key> Defaults = new()
        {
            { GameAction.MoveForward, Key.W },
            { GameAction.MoveBack, Key.S },
            { GameAction.MoveLeft, Key.A },
            { GameAction.MoveRight, Key.D },
            { GameAction.Jump, Key.Space },
            { GameAction.Sprint, Key.LeftShift },
            { GameAction.ReadyWand, Key.Q },
            { GameAction.Talk, Key.E },
            { GameAction.Map, Key.M },
            { GameAction.Respawn, Key.R },
        };

        static readonly Dictionary<GameAction, Key> keys = new();
        static int escapeUsedFrame = -1;

        public static IEnumerable<GameAction> AllActions => Defaults.Keys;

        public static Key Get(GameAction action)
        {
            if (!keys.TryGetValue(action, out var key))
            {
                key = (Key)PlayerPrefs.GetInt("key_" + action, (int)Defaults[action]);
                keys[action] = key;
            }
            return key;
        }

        // Binds a key. If another action already used it they swap so nothing ends up with two jobs
        public static void Set(GameAction action, Key key)
        {
            Key old = Get(action);
            foreach (var other in AllActions)
                if (other != action && Get(other) == key) Save(other, old);
            Save(action, key);
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            foreach (var pair in Defaults) Save(pair.Key, pair.Value);
            Changed?.Invoke();
        }

        static void Save(GameAction action, Key key)
        {
            keys[action] = key;
            PlayerPrefs.SetInt("key_" + action, (int)key);
            PlayerPrefs.Save();
        }

        public static bool Pressed(GameAction action) => Control(action)?.wasPressedThisFrame ?? false;
        public static bool Held(GameAction action) => Control(action)?.isPressed ?? false;

        static KeyControl Control(GameAction action)
        {
            var kb = Keyboard.current;
            var key = Get(action);
            return kb == null || key == Key.None ? null : kb[key];
        }

        // The name to show for a key, like "Space" or "Left Shift"
        public static string KeyName(GameAction action)
        {
            var key = Get(action);
            var kb = Keyboard.current;
            string name = kb != null && key != Key.None ? kb[key].displayName : key.ToString();
            return string.IsNullOrEmpty(name) ? key.ToString() : name;
        }

        public static string ActionName(GameAction action) => action switch
        {
            GameAction.MoveForward => "Move Forward",
            GameAction.MoveBack => "Move Back",
            GameAction.MoveLeft => "Move Left",
            GameAction.MoveRight => "Move Right",
            GameAction.ReadyWand => "Ready Wand",
            _ => action.ToString(),
        };

        // Lots of things use Esc (cancelling a spell, closing the map, leaving a conversation, pausing).
        // Whatever uses it first calls UseEscape so the pause menu doesnt also open on the same press
        public static void UseEscape() => escapeUsedFrame = Time.frameCount;

        public static bool EscapePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public static bool EscapeUnused => EscapePressed && escapeUsedFrame != Time.frameCount;
    }
}
