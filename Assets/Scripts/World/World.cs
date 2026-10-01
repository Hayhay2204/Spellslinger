using UnityEngine;

namespace SpellSlinger
{
    // Functions for checking the ground height and which zone something is in
    public static class World
    {
        // How wide the terrain is in metres (its centred on 0,0). The map uses this
        public const float Size = 800f;

        public static Terrain Terrain => Terrain.activeTerrain;

        public static float HeightAt(Vector3 p)
        {
            var t = Terrain;
            return t ? t.SampleHeight(p) + t.transform.position.y : 0f;
        }

        public static Vector3 OnGround(Vector3 p, float offset = 0f) => new(p.x, HeightAt(p) + offset, p.z);

        // Returns the zone at this position. Floating zones like the island count first over the ground below them
        public static Zone ZoneAt(Vector3 p)
        {
            Zone best = null;
            float bestDist = float.MaxValue;
            foreach (var z in Zone.All)
            {
                Vector3 c = z.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
                if (d > z.radius) continue;
                if (z.floating)
                {
                    if (p.y > c.y - 15f) return z;
                    continue;
                }
                if (d < bestDist) { bestDist = d; best = z; }
            }
            return best;
        }
    }
}
