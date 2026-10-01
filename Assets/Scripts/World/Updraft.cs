using UnityEngine;

namespace SpellSlinger
{
    // Column of wind that lifts the player up to Zephyr Isle
    public class Updraft : MonoBehaviour
    {
        public float radius = 4.5f;
        public float top = 90f;
        public float liftSpeed = 16f;
        public Vector3 pushToward;

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player || player.IsDead) return;
            Vector3 p = player.transform.position;
            Vector2 flat = new(p.x - transform.position.x, p.z - transform.position.z);
            if (flat.magnitude > radius || p.y > top || p.y < transform.position.y - 2f) return;

            player.Lift(liftSpeed);
            // Once you're near the top it pushes you over onto the island
            if (p.y > top - 10f)
            {
                Vector3 dir = pushToward - p;
                dir.y = 0f;
                player.AddImpulse(dir.normalized * 32f * Time.deltaTime);
            }
        }
    }
}
