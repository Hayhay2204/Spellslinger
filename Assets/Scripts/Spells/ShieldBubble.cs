using UnityEngine;

namespace SpellSlinger
{
    // The Air Mages Wind Ward. Blocks damage, pushes enemies away and sends the bosses projectiles back
    // at it. Air mages are pacifists so this is the only way their magic does any damage
    public class ShieldBubble : MonoBehaviour
    {
        public float duration = 6f;
        public float radius = 2.3f;
        [Tooltip("How hard it shoves enemies that touch it")]
        public float repelForce = 14f;
        public float repelDamage = 8f;
        public Color burstColor = new(0.8f, 0.95f, 1f);

        Material mat;
        Color baseColor;
        float remaining, hitFlash;
        PlayerController player;

        public bool IsUp => remaining > 0f;
        public Vector3 Center => transform.position;

        // Puts the ward on the player, or refreshes it if they already have one
        public static void Activate(ShieldBubble prefab, PlayerController player)
        {
            var shield = player.Shield;
            if (shield == null)
            {
                shield = Instantiate(prefab, player.transform);
                shield.transform.localPosition = new Vector3(0f, 1f, 0f);
                shield.player = player;
                player.Shield = shield;
            }

            shield.remaining = shield.duration;
            shield.hitFlash = 1f;
            shield.gameObject.SetActive(true);
            FX.Burst(shield.transform.position, shield.burstColor, 50, 5f, 0.12f, 0.8f);
            SFX.Play2D(SFX.Shimmer, 0.9f);
        }

        void Awake()
        {
            var r = GetComponent<Renderer>();
            mat = r.material;
            baseColor = mat.GetColor("_BaseColor");
        }

        void OnDestroy() { if (mat) Destroy(mat); }

        public void Absorb()
        {
            hitFlash = 1f;
            SFX.Play2D(SFX.Shimmer, 0.4f, 0.3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            remaining -= dt;
            if (remaining <= 0f)
            {
                FX.Burst(transform.position, burstColor, 30, 3f, 0.1f, 0.6f);
                gameObject.SetActive(false);
                return;
            }

            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 3f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            float blink = remaining < 1.5f ? (Mathf.Sin(Time.time * 30f) > 0f ? 1f : 0.3f) : 1f;
            var c = baseColor;
            c.a = (baseColor.a * 0.6f + pulse * 0.04f + hitFlash * 0.3f) * blink;
            mat.SetColor("_BaseColor", c);
            transform.localScale = Vector3.one * radius * 2f * (1f + 0.02f * pulse);

            foreach (var t in Combatant.InRadius(transform.position, radius + 0.6f))
            {
                Vector3 away = t.AimPoint - player.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = player.transform.forward;
                t.Repel(away.normalized * repelForce, repelDamage);
            }
        }
    }
}
