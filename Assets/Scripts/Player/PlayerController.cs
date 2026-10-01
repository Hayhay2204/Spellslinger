using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // First person movement, looking around, health, mana and respawning
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        public static event System.Action Died;
        public static event System.Action Respawned;

        [Header("Movement")]
        public float moveSpeed = 7f;
        public float sprintMultiplier = 1.7f;
        public float jumpHeight = 1.4f;
        public float gravity = -20f;
        public float lookSensitivity = 0.12f;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float maxMana = 100f;
        public float manaRegen = 14f;
        public float healthRegen = 3f;

        public Camera Cam;
        public Vector3 RespawnPoint { get; set; }
        public float Health { get; private set; }
        public float Mana { get; private set; }
        public bool IsDead { get; private set; }
        // SpellCaster turns this on while you're targeting so the mouse draws instead of looking around
        public bool LookLocked { get; set; }
        // Stops the player moving while they're talking to someone
        public bool MovementLocked { get; set; }
        public bool IsRooted => Time.time < rootedUntil;
        public float DamageFlash { get; private set; }
        public ShieldBubble Shield { get; set; }
        public bool ShieldActive => Shield != null && Shield.IsUp;

        CharacterController cc;
        float yaw, pitch;
        float verticalVelocity;
        Vector3 externalVelocity;
        float rootedUntil, lastDamageTime, deathTime, lastPiercedPopup = -99f;

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            Health = maxHealth;
            Mana = maxMana;
            yaw = transform.eulerAngles.y;
            RespawnPoint = transform.position;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.deltaTime;

            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 2f);

            if (IsDead)
            {
                if (Time.unscaledTime - deathTime > 1f && kb.rKey.wasPressedThisFrame) Respawn();
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked && !LookLocked && !MovementLocked)
            {
                Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 input = Vector2.zero;
            if (!MovementLocked && !IsRooted)
            {
                input = new Vector2(
                    (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                    (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            }
            float speed = moveSpeed * (kb.leftShiftKey.isPressed ? sprintMultiplier : 1f);
            Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f) * speed;

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (cc.isGrounded && !MovementLocked && !IsRooted && kb.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * dt;
            verticalVelocity = Mathf.Max(verticalVelocity, -40f);

            cc.Move((move + externalVelocity + Vector3.up * verticalVelocity) * dt);
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 25f * dt);

            // If you walk into the sea or fall through the map it sends you back to the respawn point
            if (transform.position.y < -3f) Teleport(RespawnPoint);

            Mana = Mathf.Min(maxMana, Mana + manaRegen * dt);
            if (Time.time - lastDamageTime > 5f) Health = Mathf.Min(maxHealth, Health + healthRegen * dt);
        }

        public bool TrySpendMana(float amount)
        {
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        // pierceWard is for attacks the Wind Ward cant stop (like the boss once its eaten air magic)
        public void TakeDamage(float amount, bool pierceWard = false)
        {
            if (IsDead) return;
            if (ShieldActive)
            {
                if (!pierceWard)
                {
                    Shield.Absorb();
                    return;
                }
                ShowWardPierced();
            }

            Health = Mathf.Max(0f, Health - amount);
            lastDamageTime = Time.time;
            DamageFlash = 1f;
            CameraShake.Add(0.35f);
            SFX.Play2D(SFX.Hurt, 0.8f);

            if (Health <= 0f)
            {
                IsDead = true;
                deathTime = Time.unscaledTime;
                Died?.Invoke();
            }
        }

        // Knockback. Sideways pushes slide you along the ground and upwards pushes work like a jump
        public void AddImpulse(Vector3 impulse)
        {
            externalVelocity += new Vector3(impulse.x, 0f, impulse.z);
            if (impulse.y > 0f) verticalVelocity = Mathf.Max(verticalVelocity, impulse.y);
        }

        // Used by the updraft to push you upwards
        public void Lift(float upwardSpeed) => verticalVelocity = Mathf.Max(verticalVelocity, upwardSpeed);

        public void Root(float seconds, bool pierceWard = false)
        {
            if (ShieldActive)
            {
                if (!pierceWard) { Shield.Absorb(); return; }
                ShowWardPierced();
            }
            rootedUntil = Mathf.Max(rootedUntil, Time.time + seconds);
        }

        // Lets the player know the ward didnt do anything, only once a second so it doesnt spam
        void ShowWardPierced()
        {
            if (Time.time - lastPiercedPopup < 1f) return;
            lastPiercedPopup = Time.time;
            GameHUD.Popup(Cam.transform.position + Cam.transform.forward * 3f, "WARD PIERCED", new Color(1f, 0.5f, 0.5f));
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            verticalVelocity = 0f;
            externalVelocity = Vector3.zero;
        }

        void Respawn()
        {
            Teleport(RespawnPoint);
            Health = maxHealth;
            Mana = maxMana;
            IsDead = false;
            rootedUntil = 0f;
            Respawned?.Invoke();
        }
    }
}
