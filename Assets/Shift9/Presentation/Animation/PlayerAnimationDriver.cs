using UnityEngine;

namespace Shift9.Presentation.Animation
{
    /// <summary>
    /// Feeds the locomotion blend to a player's Animator from how fast the sim is moving the
    /// transform. Measures floor speed from frame-to-frame position (the sim drives position; this
    /// never moves the character), smooths it, and writes the normalized "Speed" parameter. Safe to
    /// attach to the current capsules: with no Animator/controller it just tracks state, ready for a
    /// rigged Humanoid model + clips to slot in later.
    /// </summary>
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        public static readonly int SpeedParam = Animator.StringToHash("Speed");

        [SerializeField] private Animator _animator;
        [SerializeField] private float _maxSpeed = 14f; // matches the sim's player speed (ft/s)
        [SerializeField] private float _smoothing = 0.12f;

        private Vector3 _lastPosition;
        private bool _hasLast;
        private float _normalized;
        private float _velocity;

        public LocomotionState State { get; private set; }
        public float NormalizedSpeed => _normalized;

        private void OnEnable()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            _lastPosition = transform.position;
            _hasLast = true;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 cur = transform.position;
            float speed = 0f;
            if (_hasLast)
            {
                float dx = cur.x - _lastPosition.x;
                float dz = cur.z - _lastPosition.z;
                speed = Mathf.Sqrt(dx * dx + dz * dz) / dt;
            }
            _lastPosition = cur;
            _hasLast = true;

            float target = LocomotionMapper.Normalize(speed, _maxSpeed);
            _normalized = Mathf.SmoothDamp(_normalized, target, ref _velocity, _smoothing);
            State = LocomotionMapper.Classify(_normalized);

            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetFloat(SpeedParam, _normalized);
        }
    }
}
