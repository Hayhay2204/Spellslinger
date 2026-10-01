using UnityEngine;

namespace SpellSlinger
{
    // Spins the object around its Z axis, used for the wind wheels
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 90f;
        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
