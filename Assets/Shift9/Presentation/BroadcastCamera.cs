using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Parameters for the reference broadcast hard-cam: a fixed position off one sideline,
    /// elevated, aimed across the court at a low look-point, that yaws/tilts to follow play.
    ///
    /// The camera GEOMETRY (which side, elevated, aim across-and-down, follow along the length)
    /// is taken literally from the reference frames. The exact METRIC values below are NOT
    /// derivable from photographs and are reference-matched starting points to tune on screen. [FLAG]
    /// </summary>
    [System.Serializable]
    public struct BroadcastCameraConfig
    {
        [Tooltip("Distance the camera sits beyond the sideline, in feet. [FLAG: tune on screen]")]
        public float SidelineSetback;
        [Tooltip("Camera height above the floor, in feet. [FLAG: tune on screen]")]
        public float Height;
        [Tooltip("Camera position along the court length; 0 = center court (a fixed hard-cam).")]
        public float LengthOffset;
        [Tooltip("Height above the floor the camera aims at, in feet.")]
        public float LookHeight;
        [Tooltip("How much the aim point tracks the target down the length, 0..1.")]
        public float LengthFollow;
        [Tooltip("Vertical field of view in degrees. [FLAG: tune on screen]")]
        public float FieldOfView;
        [Tooltip("True = camera sits on the -x sideline (the reference's near/camera side).")]
        public bool NearSideNegativeX;

        public static BroadcastCameraConfig Default => new BroadcastCameraConfig
        {
            SidelineSetback = 16f,
            Height = 18f,
            LengthOffset = 0f,
            LookHeight = 7f,
            LengthFollow = 1f,
            FieldOfView = 30f,
            NearSideNegativeX = true
        };
    }

    public readonly struct CameraPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float FieldOfView;
        public CameraPose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
        }
    }

    /// <summary>
    /// Pure pose math for the broadcast camera (no scene dependency, so it is unit-testable).
    /// The fixed rig sits off the sideline; the aim point slides along the length with the target,
    /// which produces the yaw/tilt of a real hard-cam following the ball.
    /// </summary>
    public static class BroadcastCamera
    {
        public static CameraPose ComputePose(in BroadcastCameraConfig cfg, Vector3 target)
        {
            float side = cfg.NearSideNegativeX ? -1f : 1f;
            float sideX = side * (SimConstants.CourtHalfWidth + cfg.SidelineSetback);

            Vector3 position = new Vector3(sideX, cfg.Height, cfg.LengthOffset);
            Vector3 lookAt = new Vector3(0f, cfg.LookHeight, target.z * Mathf.Clamp01(cfg.LengthFollow));

            Vector3 dir = lookAt - position;
            Quaternion rotation = dir.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(dir, Vector3.up)
                : Quaternion.identity;

            return new CameraPose(position, rotation, cfg.FieldOfView);
        }
    }
}
