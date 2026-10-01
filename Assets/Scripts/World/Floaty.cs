using UnityEngine;

namespace SpellSlinger
{
    // Makes decorations spin and bob up and down
    public class Floaty : MonoBehaviour
    {
        public Vector3 spin = new(0f, 40f, 0f);
        public float bobHeight = 0.25f, bobSpeed = 1.2f;
        Vector3 basePos;
        float phase;

        void Start() { basePos = transform.localPosition; phase = Random.value * 10f; }

        void Update()
        {
            transform.Rotate(spin * Time.deltaTime, Space.World);
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        }
    }
}
