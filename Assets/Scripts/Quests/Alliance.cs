using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // Which sects have agreed to fight the Magic Eater with you. The boss cant eat the magic
    // of a sect thats on your side, and if all four join it cant eat anything new at all
    public static class Alliance
    {
        public const int SectCount = 4;
        static readonly HashSet<Element> pledged = new();

        public static event Action<Element> Pledged;

        public static int Count => pledged.Count;
        public static bool AllUnited => pledged.Count >= SectCount;
        public static bool Has(Element e) => pledged.Contains(e);
        public static bool Protects(Element e) => pledged.Contains(e);

        public static void Pledge(Element e)
        {
            if (e == Element.Storm || !pledged.Add(e)) return;
            Pledged?.Invoke(e);
        }

        public static void ResetProgress() => pledged.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pledged.Clear();
            Pledged = null;
        }
    }
}
