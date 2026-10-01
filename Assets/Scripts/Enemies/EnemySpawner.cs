using System.Collections.Generic;
using UnityEngine;

namespace SpellSlinger
{
    // Spawns a group of husks around this point in the wastelands and respawns them a while
    // after the player kills them all
    public class EnemySpawner : MonoBehaviour
    {
        public Enemy huskPrefab;
        public Enemy brutePrefab;
        public int count = 4;
        public float spread = 8f;
        [Range(0f, 1f)] public float bruteChance = 0.15f;
        public float respawnDelay = 45f;

        readonly List<Enemy> alive = new();
        float clearedAt = -1f;

        void Start() => Populate();

        void Update()
        {
            alive.RemoveAll(e => e == null);
            if (alive.Count > 0) return;

            if (clearedAt < 0f) clearedAt = Time.time;
            var player = PlayerController.Instance;
            bool playerFar = !player || Vector3.Distance(player.transform.position, transform.position) > 60f;
            if (Time.time - clearedAt > respawnDelay && playerFar) Populate();
        }

        void Populate()
        {
            clearedAt = -1f;
            for (int i = 0; i < count; i++)
            {
                var prefab = brutePrefab && Random.value < bruteChance ? brutePrefab : huskPrefab;
                if (!prefab) continue;
                Vector2 r = Random.insideUnitCircle * spread;
                Vector3 p = World.OnGround(transform.position + new Vector3(r.x, 0f, r.y), prefab.hoverHeight);
                var e = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                FX.Burst(p, new Color(0.5f, 0.15f, 1f), 30, 4f, 0.3f, 0.7f);
                alive.Add(e);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, spread);
        }
    }
}
