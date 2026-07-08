using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 localOffset = new Vector3(0f, 5.2f, -9.5f);
        public Vector3 lookAtOffset = new Vector3(0f, 1.1f, 0f);
        public float followSharpness = 7f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.TransformPoint(localOffset);
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.LookAt(target.position + lookAtOffset, Vector3.up);
        }
    }
}
