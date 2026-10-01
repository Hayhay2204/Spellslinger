using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // What each spell actually does. They all get a CastContext from the SpellCaster,
    // and the prefabs for each spell are set on the SpellCaster in the inspector
    public static class Spells
    {
        public const int IgnoreRaycastLayer = 2;

        // Storm (your sect), lightning that jumps between enemies
        public static void Lightning(CastContext c)
        {
            var prefab = c.Caster.lightningPrefab;
            var points = new List<Vector3> { c.Origin };
            var hitSet = new HashSet<Combatant>();

            Combatant first = null;
            Vector3 end = c.Target;
            if (Physics.SphereCast(c.AimRay, 0.6f, out var hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                first = hit.collider.GetComponentInParent<Combatant>();
                end = hit.point;
            }
            if (first == null) first = Combatant.AimAssist(c.AimRay, 8f, 70f);

            if (first != null)
            {
                Combatant current = first;
                float damage = prefab.damage;
                for (int i = 0; i <= prefab.chains && current != null; i++)
                {
                    hitSet.Add(current);
                    points.Add(current.AimPoint);
                    current.TakeDamage(damage, Vector3.zero, Element.Storm);
                    current.Stun(prefab.stunTime, Element.Storm);
                    FX.Burst(current.AimPoint, prefab.hitColor, 18, 6f, 0.1f, 0.35f);
                    damage *= prefab.damageFalloff;
                    current = Combatant.Nearest(current.AimPoint, prefab.chainRange, hitSet);
                }
            }
            else
            {
                points.Add(end);
                FX.Burst(end, prefab.hitColor, 20, 5f, 0.1f, 0.4f, 1f);
            }

            LightningArc.Create(prefab, points);
            FX.Flash(points[points.Count - 1], prefab.hitColor, 10f, 16f, 0.25f);
            SFX.Play2D(SFX.Zap, 0.9f);
            CameraShake.Add(0.2f);
        }

        // Pyromancers, fireball that explodes when it hits something
        public static void Fireball(CastContext c)
        {
            Projectile.Launch(c.Caster.fireballPrefab, c.Origin, c.Target);
            SFX.Play2D(SFX.Whoosh, 0.8f);
        }

        // Earth Mages, throws a boulder that arcs onto the target
        public static void Boulder(CastContext c)
        {
            Projectile.Launch(c.Caster.boulderPrefab, c.Origin, c.Target);
            SFX.Play2D(SFX.Whoosh, 0.6f);
        }

        // Air Mages, a shield that bounces stuff back
        public static void WindWard(CastContext c) => ShieldBubble.Activate(c.Caster.windWardPrefab, c.Player);

        // Druids, vines come out of the ground
        public static void Vines(CastContext c)
        {
            // Grab whatever the cursor is on, even if its flying
            Combatant target = null;
            if (Physics.SphereCast(c.AimRay, 0.8f, out var hit, 150f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<Combatant>();
            if (target == null) target = Combatant.AimAssist(c.AimRay, 6f, 90f);

            Vector3 spot = target != null ? target.AimPoint : c.Target;
            spot.y = World.HeightAt(spot);
            var field = VineField.Create(c.Caster.vinesPrefab, spot, target);
            FX.Burst(c.Origin, field.burstColor, 15, 2f, 0.08f, 0.5f);
        }

        public static void ShakeByDistance(Vector3 pos, float strength)
        {
            var player = PlayerController.Instance;
            if (!player) return;
            float d = Vector3.Distance(player.transform.position, pos);
            CameraShake.Add(strength * Mathf.Clamp01(1f - d / 30f));
        }
    }
}
