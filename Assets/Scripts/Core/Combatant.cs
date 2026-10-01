using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // The types of magic. Storm is the players sect that got destroyed
    public enum Element { Storm, Fire, Earth, Air, Nature }

    // Base class for anything spells can hit (husks, practice totems and the boss)
    public abstract class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new();

        public float maxHealth = 60f;
        public float radius = 0.5f;

        public float Health { get; protected set; }
        public bool IsDead { get; protected set; }
        public bool IsRooted => Time.time < rootedUntil;
        public bool IsStunned => Time.time < stunnedUntil;
        public virtual Vector3 AimPoint => transform.position;

        protected float rootedUntil, stunnedUntil;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public abstract void TakeDamage(float amount, Vector3 impulse, Element element);

        // True if this type of magic doesnt affect it at all (the boss uses this for the magic its eaten)
        public virtual bool Resists(Element element) => false;

        // Roots only come from druid magic, so anything immune to nature cant be rooted
        public void Root(float seconds)
        {
            if (Resists(Element.Nature)) return;
            rootedUntil = Mathf.Max(rootedUntil, Time.time + seconds);
        }

        public void Stun(float seconds, Element source)
        {
            if (Resists(source)) return;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        }

        public virtual void Repel(Vector3 impulse, float damage) { }

        // Helper functions the spells use to find targets

        public static List<Combatant> InRadius(Vector3 pos, float r)
        {
            var result = new List<Combatant>();
            foreach (var c in All)
            {
                float reach = r + c.radius;
                if (!c.IsDead && (c.AimPoint - pos).sqrMagnitude <= reach * reach) result.Add(c);
            }
            return result;
        }

        public static Combatant Nearest(Vector3 pos, float maxDist, ICollection<Combatant> exclude)
        {
            Combatant best = null;
            float bestSqr = maxDist * maxDist;
            foreach (var c in All)
            {
                if (c.IsDead || (exclude != null && exclude.Contains(c))) continue;
                float d = (c.AimPoint - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        // Finds whatever is closest to where you're aiming so you dont have to be perfectly accurate
        public static Combatant AimAssist(Ray ray, float maxAngle, float maxDist)
        {
            Combatant best = null;
            float bestAngle = maxAngle;
            foreach (var c in All)
            {
                if (c.IsDead) continue;
                Vector3 to = c.AimPoint - ray.origin;
                if (to.magnitude > maxDist + c.radius) continue;
                float a = Vector3.Angle(ray.direction, to);
                if (a < bestAngle) { bestAngle = a; best = c; }
            }
            return best;
        }
    }
}
