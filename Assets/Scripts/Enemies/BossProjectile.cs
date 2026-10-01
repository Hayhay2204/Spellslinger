using UnityEngine;

namespace SpellSlinger
{
    // Projectile the boss shoots. If it hits the Wind Ward it gets sent back at the boss as Air damage,
    // unless the boss has eaten air magic, then it goes straight through the ward
    public class BossProjectile : MonoBehaviour
    {
        public float speed = 20f;
        [Tooltip("Above 0 makes it arc like a thrown rock")]
        public float gravity;
        public float radius = 0.5f;
        public float damage = 12f;
        [Tooltip("0 means it only hurts on a direct hit")]
        public float blastRadius;
        [Tooltip("How fast it turns to follow the player")]
        public float homing;
        public float lifetime = 7f;
        public Color hitColor = new(0.55f, 0.15f, 1f);

        MagicEater owner;
        Vector3 velocity;
        bool reflected;

        bool PiercesWard => owner && owner.IsImmune(Element.Air);

        public static BossProjectile Launch(BossProjectile prefab, MagicEater owner, Vector3 pos, Vector3 velocity)
        {
            var p = Instantiate(prefab, pos, Quaternion.LookRotation(velocity));
            p.owner = owner;
            p.velocity = velocity;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            lifetime -= dt;
            var player = PlayerController.Instance;
            Vector3 pos = transform.position;

            if (!reflected && homing > 0f && player)
            {
                Vector3 want = (player.transform.position + Vector3.up - pos).normalized * velocity.magnitude;
                velocity = Vector3.RotateTowards(velocity, want, homing * dt, 0f);
            }
            velocity += Vector3.down * gravity * dt;
            pos += velocity * dt;
            transform.position = pos;

            if (!reflected && player)
            {
                // If the player has the Wind Ward up, bounce it back at the boss
                if (!PiercesWard && player.ShieldActive && Vector3.Distance(pos, player.Shield.Center) < player.Shield.radius + radius)
                {
                    reflected = true;
                    gravity = 0f;
                    if (owner) velocity = (owner.AimPoint - pos).normalized * Mathf.Max(28f, velocity.magnitude * 1.4f);
                    player.Shield.Absorb();
                    GameHUD.Popup(pos, "REFLECTED", SpellBook.Get(Element.Air).Color);
                    FX.Burst(pos, new Color(0.8f, 0.95f, 1f), 30, 6f, 0.15f, 0.5f);
                    return;
                }
                if (Vector3.Distance(pos, player.transform.position + Vector3.up) < radius + 0.7f) { Explode(pos); return; }
            }

            if (reflected && owner && Vector3.Distance(pos, owner.AimPoint) < owner.radius + radius)
            {
                owner.TakeDamage(damage * 2f, Vector3.zero, Element.Air);
                FX.Burst(pos, new Color(0.8f, 0.95f, 1f), 50, 8f, 0.3f, 0.7f);
                Cleanup();
                return;
            }

            if (pos.y < World.HeightAt(pos) || lifetime <= 0f) Explode(pos);
        }

        void Explode(Vector3 pos)
        {
            var player = PlayerController.Instance;
            float reach = Mathf.Max(blastRadius, radius + 0.7f);
            if (!reflected && player && Vector3.Distance(pos, player.transform.position + Vector3.up) < reach)
                player.TakeDamage(damage, PiercesWard);
            FX.Burst(pos, hitColor, blastRadius > 0f ? 60 : 20, blastRadius > 0f ? 9f : 4f, blastRadius > 0f ? 0.7f : 0.3f, 0.7f);
            if (blastRadius > 0f)
            {
                FX.Flash(pos, hitColor, 10f, blastRadius * 3f, 0.4f);
                SFX.PlayAt(SFX.Boom, pos, 0.8f);
                Spells.ShakeByDistance(pos, 0.4f);
            }
            Cleanup();
        }

        void Cleanup()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) FX.Detach(ps);
            Destroy(gameObject);
        }
    }
}
