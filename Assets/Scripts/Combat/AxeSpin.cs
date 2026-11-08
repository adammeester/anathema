using UnityEngine;

namespace Anathema.Combat
{
    // Tumbles a thrown axe end over end around its local X axis.
    public class AxeSpin : MonoBehaviour
    {
        public float degreesPerSecond = 900f;

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, 0f, 0f, Space.Self);
        }
    }
}
