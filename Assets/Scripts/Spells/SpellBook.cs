using System;
using System.Collections.Generic;
using SpellSlinger.Gestures;
using UnityEngine;

namespace SpellSlinger
{
    public struct CastContext
    {
        public PlayerController Player;
        public SpellCaster Caster;
        public Wand Wand;
        public Vector3 Origin;   // where the wand tip is
        public Vector3 Target;   // where the cursor was pointing when you cast
        public Ray AimRay;       // ray from the camera through the cursor
    }

    public class SpellDef
    {
        public string Name;
        public Element Element;
        public string Sect;
        public string Glyph;
        public string Description;
        public float ManaCost;
        public Color Color;
        public Action<CastContext> Cast;
        public Texture2D Icon;

        public bool Known => PlayerMagic.Knows(Element);
    }

    // One spell for each sect. The names have to match the ones in GestureLibrary
    public static class SpellBook
    {
        public static readonly List<SpellDef> All = new()
        {
            new SpellDef
            {
                Name = GestureLibrary.Lightning, Element = Element.Storm, Sect = "Stormcallers", Glyph = "Zigzag",
                Description = "Chains between foes", ManaCost = 20f, Color = new Color(0.75f, 0.6f, 1f), Cast = Spells.Lightning,
            },
            new SpellDef
            {
                Name = GestureLibrary.Fireball, Element = Element.Fire, Sect = "Pyromancers", Glyph = "Triangle",
                Description = "Explodes on impact", ManaCost = 20f, Color = new Color(1f, 0.45f, 0.12f), Cast = Spells.Fireball,
            },
            new SpellDef
            {
                Name = GestureLibrary.Boulder, Element = Element.Earth, Sect = "Earth Mages", Glyph = "Square",
                Description = "Heavy, crushing throw", ManaCost = 25f, Color = new Color(0.85f, 0.65f, 0.35f), Cast = Spells.Boulder,
            },
            new SpellDef
            {
                Name = GestureLibrary.WindWard, Element = Element.Air, Sect = "Air Mages", Glyph = "Circle",
                Description = "Blocks, repels, reflects", ManaCost = 30f, Color = new Color(0.75f, 0.95f, 1f), Cast = Spells.WindWard,
            },
            new SpellDef
            {
                Name = GestureLibrary.Vines, Element = Element.Nature, Sect = "Druids", Glyph = "V",
                Description = "Roots and strangles", ManaCost = 20f, Color = new Color(0.4f, 1f, 0.35f), Cast = Spells.Vines,
            },
        };

        public static SpellDef Get(string name) => All.Find(s => s.Name == name);
        public static SpellDef Get(Element element) => All.Find(s => s.Element == element);
    }

    // Keeps track of which magic the player has learned. You start with Storm since thats your sect
    public static class PlayerMagic
    {
        static readonly HashSet<Element> known = new() { Element.Storm };
        public static event Action<Element> Learned;

        public static int Count => known.Count;
        public static bool Knows(Element e) => known.Contains(e);

        public static void Learn(Element e)
        {
            if (known.Add(e)) Learned?.Invoke(e);
        }

        public static void LearnAll()
        {
            foreach (Element e in Enum.GetValues(typeof(Element))) Learn(e);
        }

        // Resets this when you press play in case domain reload is turned off
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            known.Clear();
            known.Add(Element.Storm);
            Learned = null;
        }
    }
}
