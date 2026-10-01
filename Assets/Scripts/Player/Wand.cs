using UnityEngine;

namespace SpellSlinger
{
    // The wand you hold. It points at the cursor while you draw, sparkles, and flashes when you cast
    public class Wand : MonoBehaviour
    {
        public Transform tip;
        public Renderer tipRenderer;
        public Light tipLight;
        public ParticleSystem sparkles;
        [ColorUsage(false, true)] public Color tipColor = new(1.4f, 1.0f, 2.8f);
        public Vector3 restRotation = new(-6f, -8f, 0f);

        public Transform Tip => tip;

        Material tipMat;
        Vector3 basePos;
        Vector3 aimPoint;
        bool aiming;
        float flash, recoil;
        Color flashColor = Color.white;
        Color baseLightColor;

        void Awake()
        {
            basePos = transform.localPosition;
            tipMat = tipRenderer.material;
            baseLightColor = tipLight.color;
        }

        void OnDestroy() { if (tipMat) Destroy(tipMat); }

        public void AimAt(Vector3 worldPoint) { aiming = true; aimPoint = worldPoint; }
        public void StopAiming() => aiming = false;

        public void SetDrawing(bool drawing)
        {
            var em = sparkles.emission;
            em.rateOverTime = drawing ? 60f : 0f;
        }

        public void Flash(Color color)
        {
            flash = 1f;
            recoil = 1f;
            flashColor = color;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime; // uses unscaled time so it still moves normally when time is slowed down
            var parent = transform.parent;
            Quaternion target = aiming
                ? Quaternion.LookRotation(aimPoint - transform.position, parent.up)
                : parent.rotation * Quaternion.Euler(restRotation);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-18f * dt));

            // Makes the wand sway a bit when idle and kick back when you cast
            float sway = aiming ? 0f : 1f;
            transform.localPosition = basePos
                + new Vector3(Mathf.Sin(Time.time * 1.3f) * 0.004f, Mathf.Sin(Time.time * 2.1f) * 0.004f, 0f) * sway
                - Vector3.forward * 0.07f * recoil;

            recoil = Mathf.MoveTowards(recoil, 0f, dt * 5f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            tipLight.intensity = 1.2f + flash * 8f;
            tipLight.color = Color.Lerp(baseLightColor, flashColor, flash);
            tipMat.SetColor("_BaseColor", Color.Lerp(tipColor, flashColor * 5f, flash));
        }
    }
}
