using UnityEngine;

namespace SpellSlinger
{
    // Added to a mentor when their sect joins the boss fight. They cast their own magic at the beast.
    // The Air Mages dont attack anyone, they keep a Wind Ward on the player instead
    public class AllyCaster : MonoBehaviour
    {
        public Element element;

        NPC npc;
        SpellCaster spells;
        float nextCast;

        void Start()
        {
            npc = GetComponent<NPC>();
            var player = PlayerController.Instance;
            if (player) spells = player.GetComponent<SpellCaster>();
            nextCast = Time.time + Random.Range(1.5f, 3.5f);
        }

        float Cooldown => element switch
        {
            Element.Fire => 4f,
            Element.Earth => 5.5f,
            Element.Nature => 8f,
            Element.Air => 2f,
            _ => 5f,
        };

        void Update()
        {
            var boss = MagicEater.Instance;
            if (!boss || boss.IsDead || !boss.IsAwake || !spells) return;

            Vector3 look = boss.AimPoint - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-6f * Time.deltaTime));

            if (Time.time < nextCast) return;
            nextCast = Time.time + Cooldown * Random.Range(0.85f, 1.2f);

            Vector3 hand = transform.position + Vector3.up * 1.6f + transform.forward * 0.8f;
            switch (element)
            {
                case Element.Fire:
                    Projectile.Launch(spells.fireballPrefab, hand, boss.AimPoint);
                    break;
                case Element.Earth:
                    Projectile.Launch(spells.boulderPrefab, hand, boss.AimPoint);
                    break;
                case Element.Nature:
                    VineField.Create(spells.vinesPrefab, World.OnGround(boss.AimPoint), boss);
                    break;
                case Element.Air:
                    var player = PlayerController.Instance;
                    if (!player || player.IsDead || player.ShieldActive) return; // already safe, check again soon
                    ShieldBubble.Activate(spells.windWardPrefab, player);
                    nextCast = Time.time + 10f;
                    break;
            }
            if (npc) npc.PlayTeach();
            FX.Burst(hand, SpellBook.Get(element).Color, 20, 3f, 0.12f, 0.5f);
        }
    }
}
