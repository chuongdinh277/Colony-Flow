using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        [SerializeField, Range(25f, 55f)] private float fieldOfView = 36f;
        [SerializeField, Range(0f, 45f)] private float tiltFromBoardNormal = 25f;
        public float TiltAngle => tiltFromBoardNormal;
        [SerializeField, Min(1f)] private float distance = 28.15f;
        [SerializeField] private Vector3 lookAt = Vector3.zero;

        public void Apply(Camera target)
        {
            if (target == null) return;
            target.orthographic = true;
            target.orthographicSize = distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            target.fieldOfView = fieldOfView;
            // The play surface is XY and model height points toward -Z.
            // Orbit around its focal point to expose the near cube faces without
            // changing the center of the composition or the grid's world axes.
            Quaternion rotation = Quaternion.Euler(-tiltFromBoardNormal, 0f, 0f);
            target.transform.SetPositionAndRotation(lookAt - rotation * Vector3.forward * distance, rotation);
        }
    }
}
