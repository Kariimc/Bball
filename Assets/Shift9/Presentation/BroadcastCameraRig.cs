using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Drives a <see cref="Camera"/> to the reference broadcast hard-cam angle. Drop this on the
    /// main camera and assign the follow target (the ball, or a play-centroid transform). The rig
    /// holds a fixed sideline position and smoothly yaws/tilts to track the target down the court.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BroadcastCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private BroadcastCameraConfig _config = BroadcastCameraConfig.Default;
        [Tooltip("Seconds for the camera to catch up to the target along the length (0 = instant).")]
        [SerializeField] private float _trackSmoothing = 0.25f;

        private Camera _camera;
        private float _followZ;
        private float _followZVelocity;

        private void Awake() => _camera = GetComponent<Camera>();

        private void LateUpdate()
        {
            float targetZ = _target != null ? _target.position.z : 0f;
            _followZ = _trackSmoothing > 0f
                ? Mathf.SmoothDamp(_followZ, targetZ, ref _followZVelocity, _trackSmoothing)
                : targetZ;

            CameraPose pose = BroadcastCamera.ComputePose(_config, new Vector3(0f, 0f, _followZ));
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if (_camera != null) _camera.fieldOfView = pose.FieldOfView;
        }
    }
}
