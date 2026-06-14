using UnityEngine;

namespace Shift9.Presentation.Animation
{
    public enum LocomotionState : byte { Idle, Jog, Sprint }

    /// <summary>
    /// Pure mapping from floor speed to the locomotion blend value the Animator consumes. The sim
    /// owns position; this only translates how fast a player is moving into a normalized 0..1
    /// "Speed" parameter (Idle→Jog→Sprint), so it stays cosmetic and unit-testable.
    /// </summary>
    public static class LocomotionMapper
    {
        public const float IdleMaxNormalized = 0.05f; // below this = standing
        public const float JogMaxNormalized = 0.7f;   // below this = jog, at/above = sprint

        public static float Normalize(float speed, float maxSpeed)
        {
            if (maxSpeed <= 0f) return 0f;
            return Mathf.Clamp01(speed / maxSpeed);
        }

        public static LocomotionState Classify(float normalized)
        {
            if (normalized < IdleMaxNormalized) return LocomotionState.Idle;
            if (normalized < JogMaxNormalized) return LocomotionState.Jog;
            return LocomotionState.Sprint;
        }
    }
}
