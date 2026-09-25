using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Preserves authored framing and follows only the target's world Z travel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ForwardFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        private Vector3 startPosition;
        private float startTargetZ;

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("Assign a target to ForwardFollowCamera before playing.", this);
                enabled = false;
                return;
            }
            startPosition = transform.position;
            startTargetZ = target.position.z;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = startPosition + Vector3.forward * (target.position.z - startTargetZ);
        }
    }
}
