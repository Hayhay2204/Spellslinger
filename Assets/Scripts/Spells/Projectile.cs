using UnityEngine;

namespace SpellSlinger
{
    // Used for the fireball and the boulder. Moves in a straight line (or an arc if it has gravity)
    // and checks ahead every frame so fast projectiles dont go straight through things
    public class Projectile : MonoBehaviour
    {
        public enum HitSound { None, Boom, Frost, Zap }

        [Header("Flight")]
        public float speed = 26f;
        [Tooltip("Above 0 makes it arc down onto the target instead of flying straight")]
        public float gravity;
        public float radius = 0.2f;
        public float lifetime = 5f;

        [Header("Hit")]
        public Element element = Element.Fire;
        public float damage = 45f;
        [Tooltip("Everything inside this radius gets hit")]
        public float blastRadius = 4f;
        [Tooltip("Things on the edge of the blast take half damage")]
        public bool damageFalloff = true;
        public float knockback = 9f;
        public float upwardKnockback = 3f;
        public float stunTime;

        [Header("Effects")]
        public Color hitColor = new(1f, 0.45f, 0.08f);
        public HitSound hitSound = HitSound.Boom;
        public float screenShake = 0.5f;
        public bool flashLight = true;

        Vector3 velocity;
        bool done;

        // Spawns the projectile at the wand and fires it at the target
        public static Projectile Launch(Projectile prefab, Vector3 from, Vector3 target)
        {
            var p = Instantiate(prefab, from, Quaternion.identity);
            Vector3 to = target - from;
            if (p.gravity > 0f)
            {
                // Aim straight at the target then add enough upwards speed so it arcs down onto the target
                float flightTime = Mathf.Clamp(to.magnitude / p.speed, 0.05f, 3f);
                p.velocity = to.normalized * p.speed + Vector3.up * (0.5f * p.gravity * flightTime);
            }
            else p.velocity = to.normalized * p.speed;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            lifetime -= dt;
            velocity += Vector3.down * gravity * dt;
            Vector3 step = velocity * dt;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);

            if (Physics.SphereCast(transform.position, radius, velocity.normalized, out var hit, step.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * radius;
                Impact(transform.position);
                return;
            }

            transform.position += step;
            if (lifetime <= 0f) Impact(transform.position);
        }

        void Impact(Vector3 pos)
        {
            if (done) return;
            done = true;

            foreach (var t in Combatant.InRadius(pos, blastRadius))
            {
                Vector3 away = (t.AimPoint - pos).normalized;
                float falloff = damageFalloff ? 1f - 0.5f * Mathf.Clamp01(Vector3.Distance(t.AimPoint, pos) / (blastRadius + t.radius)) : 1f;
                t.TakeDamage(damage * falloff, away * knockback + Vector3.up * upwardKnockback, element);
                if (stunTime > 0f) t.Stun(stunTime, element);
            }

            float size = Mathf.Max(1f, blastRadius / 2f);
            FX.Burst(pos, hitColor, Mathf.RoundToInt(30 * size), 5f * size, 0.35f * size, 0.8f, -0.2f);
            FX.Burst(pos, Color.Lerp(hitColor, Color.white, 0.5f), Mathf.RoundToInt(12 * size), 2f * size, 0.7f * size, 0.4f);
            if (flashLight) FX.Flash(pos, hitColor, 12f, blastRadius * 3.5f, 0.5f);
            switch (hitSound)
            {
                case HitSound.Boom: SFX.PlayAt(SFX.Boom, pos, 1f); break;
                case HitSound.Frost: SFX.PlayAt(SFX.Frost, pos, 1f); break;
                case HitSound.Zap: SFX.PlayAt(SFX.Zap, pos, 1f); break;
            }
            Spells.ShakeByDistance(pos, screenShake);

            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) FX.Detach(ps);
            Destroy(gameObject);
        }
    }
}
