using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpellSlinger
{
    // The final boss. Every time it loses 25% of its health it eats whatever magic did the most damage to it,
    // then its immune to that magic and can use a stronger version of it against you.
    // It already ate the Storm magic from the players sect before the game starts
    public class MagicEater : Combatant
    {
        public static MagicEater Instance { get; private set; }
        public static event Action<Element> Devoured;
        public static event Action Defeated;

        public float activationRange = 105f;
        public float leashRange = 190f;
        public float hoverHeight = 13f;

        [Header("Body parts")]
        public Transform head;
        public Transform[] wings;
        [Tooltip("The parts using the dark skin material, these flash white when it gets hit")]
        public Renderer[] hideRenderers;
        [Tooltip("The parts that glow the colour of the last magic it ate")]
        public Renderer[] coreRenderers;
        public Light coreLight;

        [Header("Model")]
        [Tooltip("Optional. Uses states called Idle, TakeOff, Fly and Attack")]
        public Animator animator;
        [Tooltip("Palette textures for each magic it has eaten, in the same order as the Element list (Storm, Fire, Earth, Air, Nature)")]
        public Texture2D[] palettes;
        [Tooltip("How high off the ground it sits while asleep")]
        public float restHeight = 6f;
        [Tooltip("Where spells aim at, relative to its position (the middle of its body)")]
        public Vector3 aimOffset = new(0f, 1f, 0f);
        [Tooltip("How strongly it glows the colour of the last magic it ate")]
        public float eatenGlow = 0.35f;

        [Header("Hitbox")]
        [Tooltip("How often the hitbox gets rebuilt from the animated model so it always matches what you see")]
        public float hitboxRefresh = 0.2f;

        [Header("Attack prefabs")]
        public BossProjectile voidOrbPrefab;
        public BossProjectile fireballPrefab;
        public BossProjectile boulderPrefab;
        public LightningArc lightningPrefab;
        public Telegraph telegraphPrefab;

        public bool IsAwake { get; private set; }
        public IReadOnlyList<Element> EatenOrder => eatenOrder;
        public Color CoreColor { get; private set; }
        public bool IsImmune(Element e) => immunities.Contains(e);

        // Magic its eaten doesnt work on it at all, so it cant be rooted once its eaten druid magic etc
        public override bool Resists(Element element) => IsImmune(element);

        static readonly float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };

        readonly HashSet<Element> immunities = new();
        readonly List<Element> eatenOrder = new();
        readonly Dictionary<Element, float> phaseDamage = new();
        int phase;
        float nextAttack;
        float orbitAngle;
        Vector3 home;
        Material coreMat, bodyMat;
        Color glow;
        SkinnedMeshRenderer skin;
        Transform[] hitBones;
        float nextHitbox;
        float roar, hitFlash;

        public override Vector3 AimPoint => transform.position + transform.rotation * aimOffset;

        void Awake()
        {
            Instance = this;
            home = transform.position;
            bodyMat = SharedInstance(hideRenderers);
            coreMat = SharedInstance(coreRenderers);
            if (bodyMat) bodyMat.EnableKeyword("_EMISSION");
            SetUpHitbox();
            ResetFight();
            PlayerController.Respawned += ResetFight;
        }

        void OnDestroy()
        {
            PlayerController.Respawned -= ResetFight;
            if (bodyMat) Destroy(bodyMat);
            if (coreMat) Destroy(coreMat);
        }

        // Gives these renderers their own copy of the material so I can change the colour without changing the asset
        static Material SharedInstance(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return null;
            var mat = new Material(renderers[0].sharedMaterial);
            foreach (var r in renderers) r.sharedMaterial = mat;
            return mat;
        }

        public Vector3 Home => home;

        void ResetFight()
        {
            if (IsDead) return;
            Health = maxHealth;
            immunities.Clear();
            eatenOrder.Clear();
            phaseDamage.Clear();
            phase = 0;
            IsAwake = false;
            // It already ate the Storm Spire at the start of the story
            immunities.Add(Element.Storm);
            eatenOrder.Add(Element.Storm);
            CoreColor = SpellBook.Get(Element.Storm).Color;
            SetPalette(Element.Storm);
            PlayAnim("Idle");
            transform.position = home;
        }

        // Swaps the dragons colours to match the magic it just ate
        void SetPalette(Element element)
        {
            int i = (int)element;
            if (bodyMat && palettes != null && i < palettes.Length && palettes[i]) bodyMat.SetTexture("_BaseMap", palettes[i]);
            glow = SpellBook.Get(element).Color * eatenGlow;
        }

        // The model is animated so a box never matches it, especially with the wings out. Instead every
        // bone gets a sphere collider sized to the bone, so the hitbox moves with the animation exactly
        void SetUpHitbox()
        {
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (!skin || skin.bones == null || skin.bones.Length == 0) return;

            hitBones = skin.bones.Where(bone => bone).ToArray();
            float size = BoneBounds().size.magnitude;
            foreach (var bone in hitBones)
            {
                if (bone.childCount == 0) continue; // the "_end" bones at the tips
                Vector3 end = bone.GetChild(0).position;
                float length = Vector3.Distance(bone.position, end);
                if (length < 0.01f) continue;

                float worldRadius = Mathf.Clamp(length * 0.6f, size * 0.02f, size * 0.08f);
                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                float scale = Mathf.Max(bone.lossyScale.x, bone.lossyScale.y, bone.lossyScale.z);
                sphere.center = bone.InverseTransformPoint((bone.position + end) * 0.5f);
                sphere.radius = worldRadius / Mathf.Max(scale, 0.0001f);
            }

            // the colliders move every frame so give the boss a kinematic rigidbody, physics likes that better
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            foreach (var box in GetComponents<BoxCollider>()) box.enabled = false;
            RefreshHitbox();
        }

        // Keeps the aim point and size matched to where the bones are right now, explosions use these
        void RefreshHitbox()
        {
            nextHitbox = Time.time + hitboxRefresh;
            var b = BoneBounds();
            aimOffset = Quaternion.Inverse(transform.rotation) * (b.center - transform.position);
            radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 0.6f;
        }

        Bounds BoneBounds()
        {
            var b = new Bounds(hitBones[0].position, Vector3.zero);
            foreach (var bone in hitBones) b.Encapsulate(bone.position);
            return b;
        }

        void PlayAnim(string state)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            int hash = Animator.StringToHash(state);
            if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, 0.25f);
        }

        // Movement and attacking

        void Update()
        {
            if (IsDead) return;
            if (hitBones != null && Time.time >= nextHitbox) RefreshHitbox();
            var player = PlayerController.Instance;
            if (!player) return;
            float dt = Time.deltaTime;
            float playerDist = Vector3.Distance(Flat(player.transform.position), Flat(home));

            if (!IsAwake)
            {
                Idle(dt);
                if (playerDist < activationRange && !player.IsDead) WakeUp();
                return;
            }
            if (playerDist > leashRange || player.IsDead)
            {
                if (!player.IsDead) ResetFight(); // the player ran away so heal back up and go back to sleep
                Idle(dt);
                return;
            }

            // Fly in a circle around the arena while facing the player
            if (!IsRooted) orbitAngle += dt * 0.25f;
            Vector3 orbit = home + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * 22f;
            orbit.y = World.HeightAt(orbit) + hoverHeight + Mathf.Sin(Time.time * 1.3f) * 1.2f;
            if (!IsRooted) transform.position = Vector3.Lerp(transform.position, orbit, 1f - Mathf.Exp(-1.5f * dt));
            Vector3 look = player.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-3f * dt));

            if (!IsStunned && Time.time >= nextAttack)
            {
                float hp = Health / maxHealth;
                nextAttack = Time.time + Mathf.Lerp(1.5f, 3.2f, hp);
                Attack(player);
                PlayAnim("Attack");
            }

            Animate(dt);
        }

        void Idle(float dt)
        {
            Vector3 rest = World.OnGround(home, restHeight);
            transform.position = Vector3.Lerp(transform.position, rest, 1f - Mathf.Exp(-1f * dt));
            Animate(dt);
        }

        void WakeUp()
        {
            IsAwake = true;
            nextAttack = Time.time + 2.5f;
            PlayAnim("TakeOff");
            roar = 1f;
            SFX.PlayAt(SFX.Boom, transform.position, 1f);
            CameraShake.Add(0.6f);
            GameHUD.Banner("THE MAGIC EATER", "It hungers for your magic", new Color(1f, 0.85f, 0.3f));
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // Taking damage and eating magic

        public override void TakeDamage(float amount, Vector3 impulse, Element element)
        {
            if (IsDead) return;
            if (!IsAwake) WakeUp();

            if (immunities.Contains(element))
            {
                var col = SpellBook.Get(element).Color;
                GameHUD.Popup(AimPoint + Vector3.up * 3f, "IMMUNE", col);
                // Show it sucking the spell in
                FX.Burst(AimPoint + transform.forward * 3f, col, 25, 2f, 0.3f, 0.5f, -1f);
                hitFlash = 0.5f;
                return;
            }

            Health = Mathf.Max(0f, Health - amount);
            hitFlash = 1f;
            phaseDamage.TryGetValue(element, out float d);
            phaseDamage[element] = d + amount;
            GameHUD.Popup(AimPoint + Vector3.up * 3f, Mathf.RoundToInt(amount).ToString(), SpellBook.Get(element).Color);

            if (Health <= 0f) { Die(); return; }
            if (phase < PhaseThresholds.Length && Health / maxHealth <= PhaseThresholds[phase])
            {
                phase++;
                EatMagic();
            }
        }

        void EatMagic()
        {
            // Eat whatever magic did the most damage this phase, if nothing did just pick one it isnt immune to yet
            Element? pick = null;
            float most = 0f;
            foreach (var kv in phaseDamage)
                if (!immunities.Contains(kv.Key) && kv.Value > most) { most = kv.Value; pick = kv.Key; }
            if (pick == null)
                foreach (Element e in Enum.GetValues(typeof(Element)))
                    if (!immunities.Contains(e)) { pick = e; break; }
            phaseDamage.Clear();
            if (pick == null) return;

            var element = pick.Value;
            immunities.Add(element);
            eatenOrder.Add(element);
            var spell = SpellBook.Get(element);

            // This is the trailer moment. Everything slows down, the camera turns to the boss and the
            // sound cuts out while it breathes in, then it gulps the magic down with a bang
            GameTime.Dramatic(0.15f, 2.6f);
            roar = 1f;
            FX.Burst(AimPoint + transform.forward * 6f, spell.Color, 60, 3f, 0.4f, 1.2f, -3f);
            CameraFocus.Play(AimPoint, 2.6f, 1f, () =>
            {
                if (!this) return; // it died before the gulp happened
                CoreColor = spell.Color;
                SetPalette(element);
                roar = 1f;
                FX.Burst(AimPoint, spell.Color, 120, 14f, 0.5f, 1.5f, -2f);
                FX.Flash(AimPoint, spell.Color, 20f, 60f, 1.2f);
                SFX.Play2D(SFX.Boom, 1f, 0f);
                SFX.Play2D(SFX.Chime, 0.8f, 0f);
                CameraShake.Add(0.8f);
                GameHUD.Banner($"IT DEVOURS {spell.Sect.ToUpper()} MAGIC", $"The Magic Eater is now immune to {spell.Name} and can use it against you", spell.Color);
            });
            Devoured?.Invoke(element);
            nextAttack = Time.time + 1.2f;
        }

        void Die()
        {
            IsDead = true;
            GameTime.Dramatic(0.2f, 2.5f);
            for (int i = 0; i < 4; i++)
                FX.Burst(AimPoint + UnityEngine.Random.insideUnitSphere * 4f, SpellBook.All[i + 1].Color, 90, 16f, 0.6f, 2f, -1f);
            FX.Flash(AimPoint, Color.white, 30f, 120f, 2.5f);
            SFX.Play2D(SFX.Boom, 1f, 0f);
            CameraShake.Add(1f);
            GameHUD.Banner("THE MAGIC EATER FALLS", "Harmony returns to the world of magic", new Color(1f, 0.95f, 0.7f));
            Defeated?.Invoke();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) FX.Detach(ps);
            Destroy(gameObject, 0.05f);
        }

        // Attacks, these are stronger versions of the magic it has eaten

        void Attack(PlayerController player)
        {
            // Mostly uses the last magic it ate but it can always use the void orbs too
            var options = new List<Element?> { null };
            foreach (var e in eatenOrder) options.Add(e);
            if (eatenOrder.Count > 1) options.Add(eatenOrder[eatenOrder.Count - 1]);
            var pick = options[UnityEngine.Random.Range(0, options.Count)];

            Vector3 mouth = head.position + head.forward * 1.5f;
            Vector3 target = player.transform.position + Vector3.up;
            roar = 0.5f;

            // once its eaten air magic the Wind Ward cant stop any of its attacks
            bool pierce = IsImmune(Element.Air);

            switch (pick)
            {
                case null:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 spread = Quaternion.Euler(0f, (i - 1) * 12f, 0f) * (target - mouth).normalized;
                        BossProjectile.Launch(voidOrbPrefab, this, mouth, spread * voidOrbPrefab.speed);
                    }
                    break;

                case Element.Storm:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 r = UnityEngine.Random.insideUnitCircle * (i == 0 ? 0f : 6f);
                        Vector3 spot = World.OnGround(player.transform.position + new Vector3(r.x, 0f, r.y));
                        Telegraph.Create(telegraphPrefab, spot, 3f, 1.1f + i * 0.25f, SpellBook.Get(Element.Storm).Color, () =>
                        {
                            LightningArc.Create(lightningPrefab, new List<Vector3> { spot + Vector3.up * 40f, spot }, 4f);
                            FX.Flash(spot, new Color(0.7f, 0.6f, 1f), 15f, 20f, 0.3f);
                            FX.Burst(spot, new Color(0.8f, 0.7f, 1f), 30, 7f, 0.15f, 0.5f);
                            SFX.PlayAt(SFX.Zap, spot, 1f);
                            HurtPlayerNear(spot, 3f, 22f, IsImmune(Element.Air));
                        });
                    }
                    break;

                case Element.Fire:
                    BossProjectile.Launch(fireballPrefab, this, mouth, (target - mouth).normalized * fireballPrefab.speed);
                    SFX.PlayAt(SFX.Whoosh, mouth, 1f);
                    break;

                case Element.Earth:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 aim = target + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                        float g = boulderPrefab.gravity, speed = boulderPrefab.speed;
                        float t = Vector3.Distance(mouth, aim) / speed;
                        Vector3 v = (aim - mouth).normalized * speed + Vector3.up * (0.5f * g * t);
                        BossProjectile.Launch(boulderPrefab, this, mouth, v);
                    }
                    break;

                case Element.Nature:
                    {
                        Vector3 spot = World.OnGround(player.transform.position);
                        Telegraph.Create(telegraphPrefab, spot, 3.5f, 1.2f, SpellBook.Get(Element.Nature).Color, () =>
                        {
                            FX.Burst(spot, new Color(0.3f, 1f, 0.2f), 60, 6f, 0.2f, 1f, 1f);
                            SFX.PlayAt(SFX.Frost, spot, 1f);
                            var p = PlayerController.Instance;
                            if (p && Vector3.Distance(Flat(p.transform.position), Flat(spot)) < 3.5f)
                            {
                                bool through = IsImmune(Element.Air);
                                p.Root(2.5f, through);
                                p.TakeDamage(12f, through);
                            }
                        });
                    }
                    break;

                case Element.Air:
                    Telegraph.Create(telegraphPrefab, World.OnGround(transform.position), 30f, 0.8f, SpellBook.Get(Element.Air).Color, () =>
                    {
                        var p = PlayerController.Instance;
                        if (!p) return;
                        FX.Burst(AimPoint, new Color(0.8f, 0.95f, 1f), 80, 25f, 0.3f, 1f);
                        SFX.Play2D(SFX.Whoosh, 1f, 0f);
                        Vector3 away = p.transform.position - transform.position;
                        away.y = 0f;
                        if (away.magnitude > 45f) return;
                        // this attack only exists once its eaten air, so it always goes through the ward
                        p.AddImpulse(away.normalized * 28f + Vector3.up * 9f);
                        p.TakeDamage(10f, pierce);
                    });
                    break;
            }
        }

        static void HurtPlayerNear(Vector3 pos, float r, float damage, bool pierceWard)
        {
            var p = PlayerController.Instance;
            if (p && Vector3.Distance(p.transform.position + Vector3.up, pos) < r + 0.8f) p.TakeDamage(damage, pierceWard);
        }

        void Animate(float dt)
        {
            roar = Mathf.MoveTowards(roar, 0f, dt);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 4f);
            float flap = Mathf.Sin(Time.time * (IsAwake ? 3f : 1f)) * (IsAwake ? 30f : 8f);
            if (wings != null && wings.Length == 2)
            {
                wings[0].localRotation = Quaternion.Euler(0f, 0f, -flap);
                wings[1].localRotation = Quaternion.Euler(0f, 0f, flap);
            }
            if (head) head.localRotation = Quaternion.Euler(-roar * 35f, 0f, 0f);
            float pulse = 1.5f + Mathf.Sin(Time.time * 4f) * 0.5f + roar * 4f;
            if (coreMat) coreMat.SetColor("_BaseColor", CoreColor * pulse * 2f);
            if (coreLight) coreLight.color = CoreColor;
            if (bodyMat) bodyMat.SetColor("_EmissionColor", glow * (1f + roar * 4f) + Color.white * hitFlash * 0.8f);
        }
    }
}
