using UnityEngine;

namespace SpellSlinger
{
    // The husks are the shadowy things the Magic Eater leaves behind. They float around where they spawned
    // and chase you if you get too close. The practice totems use this too with isDummy turned on
    public class Enemy : Combatant
    {
        public static event System.Action<Enemy> Killed;

        [Header("Stats")]
        public float speed = 3.4f;
        public float contactDamage = 10f;
        public float hoverHeight = 1.4f;
        public float aggroRange = 22f;
        public float leashRange = 45f;
        public int scoreValue = 1;
        [Tooltip("Turns it into a practice totem that doesnt move or attack and heals back up")]
        public bool isDummy;

        [Header("Visuals")]
        public Renderer bodyRenderer;
        [ColorUsage(false, true)] public Color emissionColor = new(0.6f, 0.15f, 1.3f);

        [Header("Animation")]
        [Tooltip("Optional. Uses states called Idle, Move, Attack, Hit and Death if the controller has them")]
        public Animator animator;
        [Tooltip("How long the body stays around to play its death animation")]
        public float deathTime = 1.2f;
        [Tooltip("How long its spawn animation is, it wont start moving until this is done")]
        public float spawnTime;

        public Vector3 Home { get; set; }

        Material[] mats;
        Color[] baseColors;
        string currentAnim;
        float actionUntil;
        Vector3 knockback;
        float attackReadyAt, lastRepel, flash, lastHitTime;
        float bobPhase;
        bool chasing;

        void Awake()
        {
            Health = maxHealth;
            // makes copies of the materials so when one husk flashes they dont all flash
            mats = bodyRenderer.materials;
            baseColors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].EnableKeyword("_EMISSION");
                baseColors[i] = mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
            }
            bobPhase = Random.value * 10f;
            Home = transform.position;
            actionUntil = Time.time + spawnTime;
        }

        void OnDestroy()
        {
            if (mats == null) return;
            foreach (var m in mats) if (m) Destroy(m);
        }

        // Crossfades to an animation state if the animator has one with that name
        void PlayAnim(string state, float actionLength = 0f)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            if (actionLength <= 0f && (currentAnim == state || Time.time < actionUntil)) return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return;
            animator.CrossFadeInFixedTime(hash, 0.15f);
            currentAnim = state;
            if (actionLength > 0f) actionUntil = Time.time + actionLength;
        }

        void Update()
        {
            if (IsDead) return; // just playing the death animation now
            if (Time.time < actionUntil && currentAnim == null) return; // still climbing out of the ground
            float dt = Time.deltaTime;
            float now = Time.time;
            UpdateVisuals(dt, now);

            if (isDummy)
            {
                // Heal the totem back up if nobody has hit it for a bit
                if (now - lastHitTime > 3f) Health = Mathf.MoveTowards(Health, maxHealth, maxHealth * dt);
                return;
            }

            var player = PlayerController.Instance;
            if (!player) return;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float playerDist = toPlayer.magnitude;
            float homeDist = Vector3.Distance(Flat(transform.position), Flat(Home));

            if (!chasing && playerDist < aggroRange && !player.IsDead) chasing = true;
            if (chasing && (homeDist > leashRange || player.IsDead)) chasing = false;

            Vector3 goal = chasing ? player.transform.position : Home + new Vector3(Mathf.Sin(now * 0.3f + bobPhase), 0f, Mathf.Cos(now * 0.27f + bobPhase)) * 4f;
            Vector3 toGoal = goal - transform.position;
            toGoal.y = 0f;
            float goalDist = toGoal.magnitude;
            Vector3 dir = goalDist > 0.001f ? toGoal / goalDist : transform.forward;

            Vector3 vel = Vector3.zero;
            float stopDist = chasing ? radius + 0.5f : 0.5f;
            if (!IsStunned && !IsRooted && goalDist > stopDist) vel = dir * speed * (chasing ? 1f : 0.4f);

            // Stops the husks from all bunching up in the same spot
            foreach (var other in All)
            {
                if (other == this || other is not Enemy) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                float min = radius + other.radius;
                if (m < min && m > 0.001f) vel += d / m * (min - m) * 6f;
            }

            PlayAnim(vel.sqrMagnitude > 0.25f ? "Move" : "Idle");
            vel += knockback;
            knockback *= Mathf.Exp(-5f * dt);

            Vector3 pos = transform.position + vel * dt;
            float bob = hoverHeight > 0.5f ? Mathf.Sin(now * 2f + bobPhase) * 0.2f : 0f; // only floating ones bob
            float targetY = World.HeightAt(pos) + hoverHeight + bob;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-4f * dt));
            transform.position = pos;
            if (!IsStunned) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * dt));

            if (chasing && !IsStunned && !player.IsDead && playerDist < radius + 0.9f && now >= attackReadyAt)
            {
                attackReadyAt = now + 1.1f;
                player.TakeDamage(contactDamage);
                knockback = -dir * 7f;
                PlayAnim("Attack", 0.6f);
            }
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        void UpdateVisuals(float dt, float now)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
            Color c = new Color(0.25f, 0.5f, 0.15f);
            Color e = IsRooted ? new Color(0.3f, 1.2f, 0.2f) : emissionColor;
            if (IsStunned) e += new Color(0.6f, 0.5f, 1.2f) * (0.5f + 0.5f * Mathf.Sin(now * 60f));
            for (int i = 0; i < mats.Length; i++)
            {
                Color b = IsRooted ? Color.Lerp(baseColors[i], c, 0.6f) : baseColors[i];
                mats[i].SetColor("_BaseColor", Color.Lerp(b, Color.white, flash));
                mats[i].SetColor("_EmissionColor", e + Color.white * flash * 3f);
            }
        }

        public override void TakeDamage(float amount, Vector3 impulse, Element element)
        {
            if (IsDead) return;
            Health -= amount;
            if (!isDummy) knockback += impulse;
            flash = 1f;
            lastHitTime = Time.time;
            chasing = true;
            if (Health > 0f) PlayAnim("Hit", 0.3f);
            GameHUD.Popup(transform.position + Vector3.up * (radius + 0.3f), Mathf.RoundToInt(amount).ToString(), Color.white);
            if (Health <= 0f)
            {
                if (isDummy) { Health = maxHealth; return; }
                Die();
            }
        }

        // Gets called when a husk touches the Wind Ward, pushes it away and does a bit of damage (max twice a second)
        public override void Repel(Vector3 impulse, float damage)
        {
            if (isDummy) return;
            knockback = impulse;
            if (Time.time - lastRepel > 0.5f)
            {
                lastRepel = Time.time;
                TakeDamage(damage, Vector3.zero, Element.Air);
            }
        }

        void Die()
        {
            IsDead = true;
            PlayAnim("Death", 10f);
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            FX.Burst(transform.position, new Color(0.6f, 0.2f, 1f), 45, 6f, 0.25f, 0.9f, -0.3f);
            FX.Burst(transform.position, new Color(0.1f, 0.05f, 0.2f), 20, 3f, 0.6f, 0.6f);
            SFX.PlayAt(SFX.Pop, transform.position, 0.8f);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) FX.Detach(ps);
            Killed?.Invoke(this);
            Destroy(gameObject, animator ? deathTime : 0f);
        }
    }
}
