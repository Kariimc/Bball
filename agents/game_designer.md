# Technical Architecture Blueprint — Retro Voxel Basketball

**Target:** Unity 2022.3 LTS+ · URP · Steam (Windows/Linux/macOS)
**Pillars:** Pure Voxel Art Direction · Weight-Grounded Momentum · Frame-Perfect Input Responsiveness
**Architecture posture:** Deterministic fixed-step simulation, render-decoupled input, data-oriented voxel meshing (Jobs/Burst), event-driven juice.

---

## 0. Global Simulation Contract (read first)

Every system below obeys one rule: **gameplay simulation is deterministic and runs on the fixed timestep; rendering interpolates.** This is the only way to get reproducible shot outcomes, replays, and (later) rollback netcode.

```
Project Settings → Time
  Fixed Timestep            = 0.0166667   // 60 Hz simulation
  Maximum Allowed Timestep  = 0.05        // hard clamp; prevents spiral-of-death
  Maximum Particle Timestep = 0.03
```

- **Simulation tick** (`FixedUpdate`, `dt = Time.fixedDeltaTime`): character motor, ball integration, rim solver, AI.
- **Render tick** (`Update`): interpolation, camera, UI, particles, juice.
- **Input**: polled out-of-band at device rate via the New Input System callback path, *timestamped*, and *consumed* on the simulation tick. Input is never read directly in `FixedUpdate`; it is drained from a buffer.

```csharp
// Core/SimClock.cs
// Single source of truth for interpolation alpha between fixed steps.
public static class SimClock
{
    public static double SimTime { get; private set; }      // accumulated fixed time
    public static uint    Tick    { get; private set; }      // monotonic sim tick index
    public static float   Alpha   { get; internal set; }     // [0,1] render interpolation factor

    public static void AdvanceFixed(float dt)
    {
        SimTime += dt;
        Tick++;
    }
}
```

`Alpha` is computed each render frame as `(Time.timeAsDouble - lastFixedTime) / fixedDeltaTime`, clamped to `[0,1]`, and used to lerp transforms between the previous and current simulation pose so 60 Hz sim looks smooth at 144 Hz.

---

## 1. Input Processing Architecture & Frame-Perfect Responsiveness

### 1.1 Design

Three layers:

1. **Capture** — New Input System `InputAction` callbacks push *events* (not states) with `Time.realtimeSinceStartupAsDouble` timestamps. Runs at device poll rate, independent of framerate.
2. **Buffer** — A fixed-capacity ring buffer (zero-GC) holds recent input events. Consumers query "was action X pressed within the last N ticks?" — this is the buffering window that survives un-cancellable animation states.
3. **Consume** — The simulation drains/inspects the buffer on the fixed tick. Buffered actions are marked consumed so they fire exactly once.

### 1.2 Buffered action event

```csharp
// Input/BufferedInput.cs
using UnityEngine;

public enum InputActionId : byte
{
    None = 0, Pass, Shoot, Steal, Dash, Pump, Switch
}

public readonly struct InputEvent
{
    public readonly InputActionId Action;
    public readonly double        Timestamp;   // realtime seconds, high precision
    public readonly uint          CaptureTick; // sim tick at capture time
    public readonly Vector2       Stick;       // movement vector snapshot at press

    public InputEvent(InputActionId action, double ts, uint tick, Vector2 stick)
    {
        Action = action; Timestamp = ts; CaptureTick = tick; Stick = stick;
    }
}
```

### 1.3 Zero-GC ring buffer with frame-window querying

```csharp
// Input/InputBuffer.cs
using UnityEngine;

/// <summary>
/// Fixed-capacity, allocation-free ring buffer for timestamped input events.
/// Supports action buffering across un-cancellable animation states: a pass/steal
/// pressed up to <see cref="_windowTicks"/> sim ticks early is honored the instant
/// the character becomes actionable.
/// </summary>
public sealed class InputBuffer
{
    private readonly InputEvent[] _events;
    private readonly bool[]       _consumed;
    private int  _head;          // next write index
    private int  _count;
    private readonly int _windowTicks;   // configurable buffer window (in sim ticks)

    public InputBuffer(int capacity = 64, int windowTicks = 8)
    {
        _events    = new InputEvent[capacity];
        _consumed  = new bool[capacity];
        _windowTicks = windowTicks;
    }

    /// <summary>Push from the input callback thread/path. O(1), no allocation.</summary>
    public void Push(in InputEvent e)
    {
        _events[_head]   = e;
        _consumed[_head] = false;
        _head = (_head + 1) % _events.Length;
        if (_count < _events.Length) _count++;
    }

    /// <summary>
    /// Returns true (and consumes) if <paramref name="action"/> was pressed within the
    /// buffer window relative to the current tick. Call from FixedUpdate when the
    /// character is actionable. Most-recent-first scan so the freshest press wins.
    /// </summary>
    public bool TryConsume(InputActionId action, uint currentTick, out InputEvent evt)
    {
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _events.Length) % _events.Length;
            ref readonly InputEvent e = ref _events[idx];
            if (_consumed[idx]) continue;
            if (e.Action != action) continue;
            // Expired? Buffer only honors presses inside the window.
            if (currentTick - e.CaptureTick > (uint)_windowTicks) break;
            _consumed[idx] = true;
            evt = e;
            return true;
        }
        evt = default;
        return false;
    }

    /// <summary>Non-consuming peek, for prediction/feedback systems.</summary>
    public bool WasPressed(InputActionId action, uint currentTick, int windowOverride = -1)
    {
        int window = windowOverride < 0 ? _windowTicks : windowOverride;
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _events.Length) % _events.Length;
            ref readonly InputEvent e = ref _events[idx];
            if (_consumed[idx] || e.Action != action) continue;
            if (currentTick - e.CaptureTick > (uint)window) break;
            return true;
        }
        return false;
    }
}
```

### 1.4 Capture layer (New Input System → buffer + instant feedback)

```csharp
// Input/PlayerInputRouter.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputRouter : MonoBehaviour
{
    [SerializeField] private InstantFeedbackChannel _feedback; // §1.5
    public InputBuffer Buffer { get; private set; }

    private InputAction _move;

    private void Awake() => Buffer = new InputBuffer(capacity: 64, windowTicks: 8);

    private void OnEnable()
    {
        var actions = GetComponent<PlayerInput>().actions;
        _move = actions["Move"];
        Bind(actions["Pass"],  InputActionId.Pass);
        Bind(actions["Shoot"], InputActionId.Shoot);
        Bind(actions["Steal"], InputActionId.Steal);
        Bind(actions["Dash"],  InputActionId.Dash);
    }

    private void Bind(InputAction action, InputActionId id)
    {
        // 'performed' fires on the device poll, off the render frame — frame-perfect capture.
        action.performed += ctx =>
        {
            var e = new InputEvent(id, Time.realtimeSinceStartupAsDouble,
                                   SimClock.Tick, _move.ReadValue<Vector2>());
            Buffer.Push(e);
            // §1.5 — fire visual feedback on the EXACT capture frame, before sim solves.
            _feedback.Emit(id, transform.position);
        };
    }

    // Movement is a continuous state, sampled (not buffered) on the sim tick.
    public Vector2 SampleMove() => _move.ReadValue<Vector2>();
}
```

### 1.5 Instant feedback loop (pre-simulation)

The key to "frame-perfect feel" is decoupling *acknowledgement* from *resolution*. The instant an input is captured we flash a UI tick / spawn a confirm particle — even though the physics resolves on the next fixed tick. The brain reads the acknowledgement as zero-latency.

```csharp
// Input/InstantFeedbackChannel.cs
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Immediate, render-frame visual ACK for an input, emitted before the simulation
/// resolves the action. Uses a pooled flash to avoid per-press instantiation GC.
/// </summary>
public sealed class InstantFeedbackChannel : MonoBehaviour
{
    [SerializeField] private InputFlash _flashPrefab;
    private ObjectPool<InputFlash> _pool;

    private void Awake()
    {
        _pool = new ObjectPool<InputFlash>(
            createFunc: () => Instantiate(_flashPrefab),
            actionOnGet: f => f.gameObject.SetActive(true),
            actionOnRelease: f => f.gameObject.SetActive(false),
            defaultCapacity: 16, maxSize: 64);
    }

    public void Emit(InputActionId action, Vector3 worldPos)
    {
        var flash = _pool.Get();
        flash.Play(action, worldPos, returnToPool: _pool.Release);
    }
}
```

`InputFlash` is a tiny driver: position at `worldPos`, tint per action, animate scale/alpha over ~80 ms, then self-release. Because it's pooled and driven in `Update`, it never touches the simulation and never allocates after warm-up.

### 1.6 Shot Release Mechanic — "Green Release"

Two pieces of math:

**(a) Jump-shot apex via kinematics.** With vertical launch velocity `v0` and gravity `g`, time to apex and the ideal release timing are:

```
t_apex = v0 / g
```

The "perfect release" target is a tunable fraction of the rise (commonly slightly before apex, where the hand is fully extended):

```
t_target = k_apex * t_apex,   k_apex ∈ [0.85, 1.0]
```

**(b) Timing error → make probability.** Let the player's release time error be `Δt = t_release − t_target`. Map it through a Gaussian, scaled by player rating `R ∈ [0,1]` (rating widens the "green" window and lifts the floor):

```
P_make(Δt) = P_floor(R) + (P_max(R) − P_floor(R)) · exp( −(Δt)² / (2σ(R)²) )
σ(R)       = σ_min + (σ_max − σ_min) · R          // higher rating → wider forgiveness
```

A "GREEN" (guaranteed-ish, juiced) release is `|Δt| ≤ ε_green(R)`.

```csharp
// Gameplay/ShotReleaseSystem.cs
using UnityEngine;

[System.Serializable]
public struct ShooterRating
{
    [Range(0,1)] public float rating;     // R
    public float sigmaMin, sigmaMax;      // forgiveness window (seconds)
    public float pFloor, pMax;            // make-probability bounds
    public float greenEpsilon;            // |Δt| under which release is GREEN
    public float apexFraction;            // k_apex
}

public sealed class ShotReleaseSystem : MonoBehaviour
{
    [SerializeField] private ShooterRating _rating = new ShooterRating
    {
        rating = 0.7f, sigmaMin = 0.04f, sigmaMax = 0.10f,
        pFloor = 0.15f, pMax = 0.99f, greenEpsilon = 0.025f, apexFraction = 0.95f
    };

    private float _v0;          // vertical launch velocity of current jump
    private float _gravity;     // positive magnitude
    private float _tCharged;    // time elapsed since jump began
    private bool  _airborne;

    public struct ReleaseResult
    {
        public float DeltaT;        // t_release - t_target
        public float MakeProbability;
        public bool  IsGreen;
        public bool  Made;          // resolved deterministically against rng
    }

    /// <summary>Begin a jump shot. v0 derived from desired apex height h: v0 = sqrt(2·g·h).</summary>
    public void BeginJump(float apexHeight, float gravity)
    {
        _gravity  = Mathf.Abs(gravity);
        _v0       = Mathf.Sqrt(2f * _gravity * Mathf.Max(0.01f, apexHeight));
        _tCharged = 0f;
        _airborne = true;
    }

    public void Tick(float dt) { if (_airborne) _tCharged += dt; }

    /// <summary>
    /// Resolve a release at the current charge time. <paramref name="rng01"/> is a
    /// deterministic uniform sample [0,1) from the sim's seeded RNG (NOT UnityEngine.Random).
    /// </summary>
    public ReleaseResult Release(float rng01)
    {
        _airborne = false;
        float tApex   = _v0 / _gravity;                 // t_apex = v0/g
        float tTarget = _rating.apexFraction * tApex;   // t_target
        float dt      = _tCharged - tTarget;            // Δt

        float sigma = Mathf.Lerp(_rating.sigmaMin, _rating.sigmaMax, _rating.rating);
        float gauss = Mathf.Exp(-(dt * dt) / (2f * sigma * sigma));
        float pFloor = Mathf.Lerp(_rating.pFloor, _rating.pFloor + 0.10f, _rating.rating);
        float p      = pFloor + (_rating.pMax - pFloor) * gauss;

        bool green = Mathf.Abs(dt) <= _rating.greenEpsilon;
        if (green) p = Mathf.Max(p, 0.99f);             // green lifts to near-certain

        return new ReleaseResult
        {
            DeltaT = dt, MakeProbability = p, IsGreen = green, Made = rng01 < p
        };
    }
}
```

> **Determinism note:** make/miss must be sampled from a *seeded* sim RNG (e.g. xorshift on the sim tick + match seed), never `UnityEngine.Random`, so replays and netcode reproduce outcomes bit-for-bit.

---

## 2. Kinematic Character Controller & Momentum Physics

### 2.1 Why custom kinematics

Rigidbody `AddForce` is framerate-sensitive, non-deterministic under variable substeps, and "mushy." We drive velocity analytically and resolve collisions with `Physics.ComputePenetration` / capsule casts, giving snappy-yet-weighted motion with zero sliding.

### 2.2 Mathematical model

**Linear acceleration toward desired velocity.** Desired velocity `v_des = dir · V_max·(stick magnitude)`. We move current `v` toward `v_des` at a rate that depends on whether we're accelerating, braking, or reversing.

```
v_des   = clamp(|stick|,0,1) · V_max · d̂            // d̂ = input direction
```

**Plant-the-feet reversal.** Let `θ` be the angle between current velocity `v` and desired direction `d̂`. When `cos θ < τ_reverse` (sharp cut), we enter a **brake phase** decelerating at `a_break` until `|v|` drops below a plant threshold, then accelerate into the new direction. This removes ice-skating on direction changes.

```
cosθ = (v · d̂) / (|v| · |d̂|)
phase = (cosθ < τ_reverse) ? BRAKE : DRIVE
```

**Friction / deceleration (no input).** Coulomb-style kinetic friction on a wood court, opposing motion:

```
a_friction = −μ_wood · g · v̂          // μ_wood ≈ 0.45–0.6
v(t+dt)    = v − a_friction·dt,  clamped so it never reverses sign (stops at 0)
```

**Acceleration integration (explicit, clamped).**

```
a_drive  = (v_des − v) / τ_accel         // τ_accel = time-constant (s) to reach target
v ← v + a_drive·dt                        // DRIVE phase
v ← v − a_break · v̂ · dt                  // BRAKE phase (μ-independent hard plant)
|v| ← min(|v|, V_max)
```

**Momentum conservation: sprint → pull-up jumper.** On entering the shot, horizontal momentum is *preserved but bled*: a fraction `β_carry` of horizontal velocity carries into the jump (drift), the rest converts to a vertical impulse contribution, so a moving pull-up has authentic lean/drift.

```
v_xz_air = β_carry · v_xz_ground                  // β_carry ∈ [0.2,0.5]
Δv_y    += α_convert · |v_xz_ground|              // momentum → lift contribution
```

### 2.3 The motor

```csharp
// Gameplay/VoxelCharacterController.cs
using UnityEngine;

/// <summary>
/// Deterministic kinematic motor. Manages velocity analytically (no Rigidbody forces),
/// resolves penetration via capsule casts. Runs in FixedUpdate; render pose interpolates.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
public sealed class VoxelCharacterController : MonoBehaviour
{
    [Header("Velocity")]
    [SerializeField] private float _vMax = 7.5f;          // V_max (m/s), set from attributes
    [SerializeField] private float _accelTau = 0.10f;     // τ_accel: time-constant to V_des
    [SerializeField] private float _brakeDecel = 28f;     // a_break (m/s²) hard plant
    [SerializeField] private float _muWood = 0.55f;       // μ_wood kinetic friction
    [SerializeField] private float _gravity = 26f;        // g (tuned, not 9.81 — arcade weight)
    [Header("Reversal")]
    [Range(-1,1)] [SerializeField] private float _reverseDot = 0.2f; // τ_reverse
    [SerializeField] private float _plantSpeed = 1.5f;    // |v| under which BRAKE→DRIVE
    [Header("Air / Shot carry")]
    [SerializeField] private float _carry = 0.35f;        // β_carry
    [SerializeField] private float _convert = 0.15f;      // α_convert

    private CapsuleCollider _capsule;
    private Vector3 _velocity;        // current planar+vertical velocity (world)
    private bool    _grounded;
    private bool    _braking;

    // Interpolation state for render decoupling.
    public Vector3 PrevPosition { get; private set; }
    public Vector3 CurrPosition { get; private set; }
    public Vector3 Velocity => _velocity;

    private void Awake()
    {
        _capsule = GetComponent<CapsuleCollider>();
        PrevPosition = CurrPosition = transform.position;
    }

    /// <param name="stick">planar input (x,z packed in Vector2 x,y)</param>
    public void SimulateFixed(Vector2 stick, float dt)
    {
        PrevPosition = CurrPosition;

        Vector3 inDir = new Vector3(stick.x, 0f, stick.y);
        float mag = Mathf.Clamp01(inDir.magnitude);
        Vector3 dHat = mag > 1e-4f ? inDir / inDir.magnitude : Vector3.zero;

        Vector3 vXZ = new Vector3(_velocity.x, 0f, _velocity.z);

        if (mag > 1e-4f)
        {
            float speed = vXZ.magnitude;
            float cosT  = speed > 1e-4f ? Vector3.Dot(vXZ / speed, dHat) : 1f;

            // Sharp cut → plant the feet (BRAKE) until slow enough to re-drive.
            if (cosT < _reverseDot && speed > _plantSpeed) _braking = true;
            if (speed <= _plantSpeed) _braking = false;

            if (_braking)
            {
                // a_break opposes current motion (hard, μ-independent).
                vXZ = StepTowardZero(vXZ, _brakeDecel * dt);
            }
            else
            {
                Vector3 vDes = dHat * (_vMax * mag);
                // a_drive = (v_des - v)/τ  → exponential-ish approach, clamped per step.
                Vector3 aDrive = (vDes - vXZ) / Mathf.Max(1e-4f, _accelTau);
                vXZ += aDrive * dt;
            }
        }
        else
        {
            // No input → kinetic friction decel: a = μ·g, opposing v̂, never overshoots zero.
            float decel = _muWood * _gravity * dt;
            vXZ = StepTowardZero(vXZ, decel);
            _braking = false;
        }

        // Clamp planar speed to V_max.
        if (vXZ.magnitude > _vMax) vXZ = vXZ.normalized * _vMax;

        // Gravity on vertical channel.
        float vy = _velocity.y;
        if (!_grounded) vy -= _gravity * dt;
        else if (vy < 0f) vy = 0f;

        _velocity = new Vector3(vXZ.x, vy, vXZ.z);

        // Integrate + resolve.
        Vector3 next = CurrPosition + _velocity * dt;
        next = ResolveCollisions(next, ref _velocity);
        CurrPosition = next;
        transform.position = next; // render interpolation re-applies in Update (§2.4)
    }

    /// <summary>Sprint→pull-up: bleed horizontal momentum into drift + a lift contribution.</summary>
    public float EnterJumpShot(out Vector3 airDriftXZ)
    {
        Vector3 vXZ = new Vector3(_velocity.x, 0f, _velocity.z);
        airDriftXZ  = vXZ * _carry;                 // v_xz_air = β_carry · v_xz_ground
        float lift  = _convert * vXZ.magnitude;     // Δv_y += α_convert·|v_xz|
        _velocity   = new Vector3(airDriftXZ.x, _velocity.y, airDriftXZ.z);
        return lift;
    }

    private static Vector3 StepTowardZero(Vector3 v, float delta)
    {
        float s = v.magnitude;
        if (s <= delta) return Vector3.zero;        // clamp at 0 — never reverse sign
        return v - v / s * delta;
    }

    // Capsule-cast / depenetration collision resolution against court + bodies.
    private Vector3 ResolveCollisions(Vector3 target, ref Vector3 vel)
    {
        // (1) ground check via short downward cast; (2) lateral depenetration via
        // Physics.ComputePenetration against overlapped colliders. Slide velocity along
        // the contact normal: v ← v − (v·n̂)n̂. Omitted body is mechanical; pattern:
        //   foreach overlap: if ComputePenetration(...) target += normal*dist;
        //                    vel -= Vector3.Project(vel, normal);
        _grounded = Physics.SphereCast(target + Vector3.up * 0.5f, _capsule.radius * 0.9f,
                                       Vector3.down, out _, 0.6f, ~0,
                                       QueryTriggerInteraction.Ignore);
        return target;
    }

    public void SetMaxVelocity(float vMax) => _vMax = Mathf.Max(0f, vMax); // from attributes
}
```

### 2.4 Render interpolation

```csharp
// Gameplay/MotorView.cs — runs in Update, smooths 60Hz sim to display rate.
public sealed class MotorView : MonoBehaviour
{
    [SerializeField] private VoxelCharacterController _motor;
    [SerializeField] private Transform _view;   // visual child
    private void LateUpdate()
        => _view.position = Vector3.LerpUnclamped(
               _motor.PrevPosition, _motor.CurrPosition, SimClock.Alpha);
}
```

---

## 3. High-Velocity Projectile & Rim Collision Physics

### 3.1 Integrator choice

For a basketball we want **stable, deterministic** flight with spin-independent translation. **Velocity-Verlet** beats explicit Euler for energy stability over arcs and is still cheap:

```
x(t+dt) = x(t) + v(t)·dt + ½·a(t)·dt²
a(t+dt) = F(x,v)/m                         // gravity (+ optional drag)
v(t+dt) = v(t) + ½·(a(t) + a(t+dt))·dt
```

The closed form `x(t) = x0 + v0·t + ½·a·t²` is used only for *predictive* arc rendering (the aim line), not for the live ball.

### 3.2 Ball physics manager

```csharp
// Gameplay/BallPhysics.cs
using UnityEngine;

/// <summary>
/// Deterministic Velocity-Verlet ball. Does not use Rigidbody/PhysicMaterial; all
/// restitution and net behavior is solved explicitly for reproducible bounces.
/// </summary>
public sealed class BallPhysics : MonoBehaviour
{
    [SerializeField] private float _radius = 0.12f;
    [SerializeField] private float _gravity = 26f;       // match arcade g
    [SerializeField] private float _linearDrag = 0.02f;  // light air damp
    [SerializeField] private RimSolver _rim;             // §3.3

    private Vector3 _pos, _vel, _accel;
    public Vector3 Position => _pos;
    public Vector3 Velocity => _vel;
    public Vector3 PrevPosition { get; private set; }

    public void Launch(Vector3 origin, Vector3 velocity)
    {
        _pos = PrevPosition = origin;
        _vel = velocity;
        _accel = ComputeAccel(_vel);
    }

    private Vector3 ComputeAccel(Vector3 v)
        => new Vector3(0f, -_gravity, 0f) - v * _linearDrag; // gravity + linear drag

    public void SimulateFixed(float dt)
    {
        PrevPosition = _pos;

        // Velocity-Verlet half-step.
        Vector3 newPos = _pos + _vel * dt + 0.5f * _accel * dt * dt;
        Vector3 newAccel = ComputeAccel(_vel);
        Vector3 newVel = _vel + 0.5f * (_accel + newAccel) * dt;

        // Continuous collision against rim/backboard/net along the segment.
        if (_rim.Resolve(_pos, ref newPos, ref newVel, _radius, dt))
            newAccel = ComputeAccel(newVel);

        _pos = newPos; _vel = newVel; _accel = newAccel;
        transform.position = _pos; // view interpolates with SimClock.Alpha like §2.4
    }
}
```

### 3.3 Custom rim & backboard collision solver

Vector reflection with a tunable coefficient of restitution `e`, plus tangential friction `f_t` to create rattle/spin-out:

```
v' = v − (1+e)(v·n̂)n̂            // reflect normal component, scaled by restitution
v'_t = (1 − f_t)·v_t             // bleed tangential component (rattle damping)
```

- **Rattle in:** moderate `e` (~0.35) + asymmetric tangential damping keeps the ball oscillating inside the cylinder until energy drains below the rim plane.
- **Spin out:** glancing hits (`|v·n̂|` small, large tangential) with low `f_t` preserve tangential speed → ball rolls off.
- **Backboard deadening:** high-`f_t`, low-`e` material on the board plane kills speed (the "soft" bank).
- **Swish:** a trigger cylinder below the rim that, on entry with a downward-ish velocity and acceptable horizontal offset, applies a downward velocity bias + micro-damp to mimic net drag.

```csharp
// Gameplay/RimSolver.cs
using UnityEngine;

public sealed class RimSolver : MonoBehaviour
{
    [Header("Geometry (world space)")]
    [SerializeField] private Vector3 _rimCenter;
    [SerializeField] private float _rimRadius = 0.225f;   // regulation ~0.2286 m
    [SerializeField] private Plane  _backboard;           // set from board transform

    [Header("Material response")]
    [SerializeField] private float _rimRestitution = 0.35f;   // e (rim)
    [SerializeField] private float _rimTangential = 0.18f;    // f_t (rattle bleed)
    [SerializeField] private float _boardRestitution = 0.22f; // e (deadened board)
    [SerializeField] private float _boardTangential = 0.40f;  // f_t (board)

    [Header("Swish")]
    [SerializeField] private float _netDamp = 0.12f;      // micro-damp through net
    [SerializeField] private float _netDownBias = 1.2f;   // downward velocity nudge

    /// <summary>Resolve ball motion over [from→to]. Returns true if velocity changed.</summary>
    public bool Resolve(Vector3 from, ref Vector3 to, ref Vector3 vel, float r, float dt)
    {
        bool changed = false;

        // --- Rim torus approximated as a ring of contact: nearest point on rim circle. ---
        Vector3 flat = to - _rimCenter; flat.y = 0f;
        float ringDist = flat.magnitude;
        if (ringDist > 1e-4f)
        {
            Vector3 nearestOnRing = _rimCenter + flat / ringDist * _rimRadius;
            Vector3 delta = to - nearestOnRing;
            if (delta.sqrMagnitude < r * r)
            {
                Vector3 n = delta.normalized;                       // contact normal
                to = nearestOnRing + n * r;                         // depenetrate
                vel = Reflect(vel, n, _rimRestitution, _rimTangential);
                changed = true;
            }
        }

        // --- Backboard plane. ---
        float d = _backboard.GetDistanceToPoint(to);
        if (d < r && Vector3.Dot(vel, _backboard.normal) < 0f)
        {
            to += _backboard.normal * (r - d);
            vel = Reflect(vel, _backboard.normal, _boardRestitution, _boardTangential);
            changed = true;
        }

        // --- Swish: passing downward through the net cylinder below the rim. ---
        bool insideCylinder = ringDist < _rimRadius * 0.85f;
        if (insideCylinder && to.y < _rimCenter.y && vel.y < 0f)
        {
            vel *= (1f - _netDamp);          // net resistance micro-damp
            vel.y -= _netDownBias * dt;      // pull straight down → clean swish
            changed = true;
        }
        return changed;
    }

    /// <summary>v' = v − (1+e)(v·n̂)n̂ on normal; (1−f_t) on tangential.</summary>
    private static Vector3 Reflect(Vector3 v, Vector3 n, float e, float ft)
    {
        float vn = Vector3.Dot(v, n);
        Vector3 vNormal = vn * n;
        Vector3 vTangent = v - vNormal;
        return (-(1f + e) * vn) * n + v - vNormal       // reflected normal
             - ft * vTangent;                            // damped tangential
    }
}
```

---

## 4. Voxel Rendering Pipeline & Camera Systems

### 4.1 Meshing strategy: instancing vs. greedy meshing

| Approach | Draw calls | Vertex count | Best for | Cost |
|---|---|---|---|---|
| **GPU Instancing** of 1×1×1 cubes | 1 per material (instanced) | 24·N verts equivalent | static/uniform fields, particles, debris | overdraw + per-instance buffer churn when animating |
| **Greedy Meshing** (Burst job) → one mesh per character | 1 per character chunk | minimal (merged quads) | **animating voxel characters** | rebuild cost per pose change |

**Decision:** Animating voxel *characters* use **greedy meshing per skeletal chunk** (each limb is a voxel chunk parented to a bone; the chunk mesh is greedy-merged once and rigidly transformed by the bone — *no per-frame remesh*). This gives minimal vertices, hard flat-shaded faces, and clean silhouettes. Transient **debris/crowd** use **GPU instancing** because thousands of identical cubes with per-instance transforms are exactly instancing's sweet spot.

> Per-frame greedy remeshing is only triggered on *topology* change (e.g., a voxel destruction effect), not on animation — animation is bone transforms over static merged limb meshes.

### 4.2 Greedy meshing core (Jobs + Burst, zero managed GC)

```csharp
// Rendering/GreedyMeshJob.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Greedy-meshes a dense voxel chunk into merged quads. Runs in a Burst job over
/// NativeArrays — no managed allocation, no GC. One job per limb chunk, scheduled
/// off the main thread; results uploaded to a Mesh on completion.
/// </summary>
[BurstCompile]
public struct GreedyMeshJob : IJob
{
    [ReadOnly] public NativeArray<byte> Voxels;   // dense [X*Y*Z], 0 = empty else color idx
    public int3 Dims;                              // chunk dimensions

    public NativeList<float3> Vertices;
    public NativeList<int>    Indices;
    public NativeList<float4> Colors;             // flat per-quad color (RGBA)

    private int Index(int x, int y, int z) => x + Dims.x * (y + Dims.y * z);

    public void Execute()
    {
        // Sweep the 3 axes; for each, build 2D masks and merge maximal rectangles.
        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3, v = (d + 2) % 3;
            int3 x = int3.zero, q = int3.zero;
            q[d] = 1;

            int dimD = Dims[d], dimU = Dims[u], dimV = Dims[v];
            var mask = new NativeArray<int>(dimU * dimV, Allocator.Temp); // signed color, 0 empty

            for (x[d] = -1; x[d] < dimD; )
            {
                int n = 0;
                // Build the mask of visible faces between slab x[d] and x[d]+1.
                for (x[v] = 0; x[v] < dimV; x[v]++)
                for (x[u] = 0; x[u] < dimU; x[u]++)
                {
                    int a = (x[d] >= 0)        ? Voxels[Index(x.x, x.y, x.z)] : 0;
                    int3 xb = x; xb[d] += 1;
                    int b = (x[d] < dimD - 1)  ? Voxels[Index(xb.x, xb.y, xb.z)] : 0;
                    // Face exists where exactly one side is solid. Sign encodes facing.
                    mask[n++] = (a != 0 && b == 0) ? a : (b != 0 && a == 0) ? -b : 0;
                }

                x[d]++;
                n = 0;
                // Merge maximal rectangles in the mask.
                for (int j = 0; j < dimV; j++)
                for (int i = 0; i < dimU; )
                {
                    int c = mask[n];
                    if (c == 0) { i++; n++; continue; }

                    int w = 1; while (i + w < dimU && mask[n + w] == c) w++;        // width
                    int h = 1; bool grow = true;                                    // height
                    while (j + h < dimV && grow)
                    {
                        for (int k = 0; k < w; k++)
                            if (mask[n + k + h * dimU] != c) { grow = false; break; }
                        if (grow) h++;
                    }

                    EmitQuad(x, u, v, w, h, c, d);
                    for (int l = 0; l < h; l++)
                        for (int k = 0; k < w; k++) mask[n + k + l * dimU] = 0;     // clear
                    i += w; n += w;
                }
            }
            mask.Dispose();
        }
    }

    private void EmitQuad(int3 x, int u, int v, int w, int h, int color, int axis)
    {
        int3 du = int3.zero; du[u] = w;
        int3 dv = int3.zero; dv[v] = h;
        float3 p = new float3(x.x, x.y, x.z);

        int baseIdx = Vertices.Length;
        Vertices.Add(p);
        Vertices.Add(p + du);
        Vertices.Add(p + du + dv);
        Vertices.Add(p + dv);

        bool back = color < 0;
        float4 rgba = VoxelPalette.Lookup(math.abs(color));   // palette → flat color
        Colors.Add(rgba); Colors.Add(rgba); Colors.Add(rgba); Colors.Add(rgba);

        // Winding flips with face direction so normals are correct for flat shading.
        if (!back) { AddTri(baseIdx, 0,1,2); AddTri(baseIdx, 0,2,3); }
        else       { AddTri(baseIdx, 0,2,1); AddTri(baseIdx, 0,3,2); }
    }

    private void AddTri(int b, int a, int c, int d)
    { Indices.Add(b + a); Indices.Add(b + c); Indices.Add(b + d); }
}
```

> **GC/allocation discipline:** `Voxels`/output lists are `NativeArray`/`NativeList` allocated once per chunk and reused; the `mask` uses `Allocator.Temp` (stack-fast, auto-freed). Upload to `Mesh` via `Mesh.SetVertexBufferData`/`SetIndexBufferData` with `MeshUpdateFlags.DontRecalculateBounds` to skip CPU recompute. Normals are *not* recalculated — flat-shading derives them in-shader (§4.3), saving an O(n) pass.

### 4.3 URP flat-shade + grid-snap + outline shader

Structural outline (Shader Graph or hand-written HLSL):

**Pass 1 — Surface (flat shading + vertex grid-snap):**
- `Properties`: `_Palette` (texture/array), `_GridUnit` (world snap size), `_OutlineWidth`, `_OutlineColor`, `_FlatLightDir`.
- **Flat shading:** compute the face normal from screen-space derivatives `normalize(cross(ddx(worldPos), ddy(worldPos)))` → no smooth normals needed, perfectly faceted. One `NdotL` band (no specular) for the crisp matte look.
- **Vertex grid-snap:** in the vertex stage, round world position to `_GridUnit` to lock voxels to a virtual grid and kill sub-pixel shimmer:

```hlsl
// Vertex stage — snap to virtual grid for stable retro layout.
float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
worldPos = round(worldPos / _GridUnit) * _GridUnit;          // grid snap
OUT.positionCS = TransformWorldToHClip(worldPos);

// Fragment stage — flat shading from derivatives (no interpolated normals).
float3 n = normalize(cross(ddx(IN.worldPos), ddy(IN.worldPos)));
float  ndl = saturate(dot(n, -_FlatLightDir));
float  band = ndl > 0.5 ? 1.0 : 0.6;                         // 2-step toon band
half3  col = IN.color.rgb * band;
```

**Pass 2 — Outline:** inverted-hull (front-face cull, push vertices along normal by `_OutlineWidth` in clip space, output `_OutlineColor`). Crisp, resolution-independent silhouette. Disable MSAA-driven edge AA on this pass to keep edges hard; rely on a render-scale + point sampling for the "un-aliased" pixel-clean look.

### 4.4 Spring-arm broadcast camera

Tracks the **weighted midpoint** of ball and active player, follows on an elevated sideline rig, and lags via a critically-tunable spring-damper:

```
target = (w_ball·p_ball + w_player·p_player) / (w_ball + w_player)
F = −k·x − c·v             // Hooke spring + viscous damping (x = cam − desired)
critically damped when  c = 2·sqrt(k·m)
```

```csharp
// Rendering/BroadcastCamera.cs
using UnityEngine;

/// <summary>
/// Elevated sideline broadcast rig. Follows the weighted ball/player midpoint with a
/// Hooke's-law spring-damper for physical, scale-enhancing lag on fast breaks.
/// Integrated in FixedUpdate for deterministic, framerate-independent damping; the
/// camera transform is interpolated in LateUpdate.
/// </summary>
public sealed class BroadcastCamera : MonoBehaviour
{
    [SerializeField] private Transform _ball, _player;
    [SerializeField] private float _ballWeight = 0.35f, _playerWeight = 0.65f;
    [SerializeField] private Vector3 _rigOffset = new Vector3(0f, 9f, -12f);
    [SerializeField] private float _k = 90f;       // spring stiffness
    [SerializeField] private float _mass = 1f;
    [SerializeField] private float _dampingRatio = 1.0f; // 1 = critically damped (no overshoot)
    [SerializeField] private float _lookHeight = 1.2f;

    private Vector3 _camPos, _camVel, _prevCamPos;

    private void Start() => _camPos = _prevCamPos = WeightedTarget() + _rigOffset;

    private Vector3 WeightedTarget()
    {
        float wsum = _ballWeight + _playerWeight;
        return (_ballWeight * _ball.position + _playerWeight * _player.position) / wsum;
    }

    private void FixedUpdate()
    {
        _prevCamPos = _camPos;
        Vector3 desired = WeightedTarget() + _rigOffset;

        // c = 2ζ√(k·m). x = cam − desired. a = (−k·x − c·v)/m.
        float c = 2f * _dampingRatio * Mathf.Sqrt(_k * _mass);
        Vector3 x = _camPos - desired;
        Vector3 accel = (-_k * x - c * _camVel) / _mass;

        _camVel += accel * Time.fixedDeltaTime;
        _camPos += _camVel * Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        Vector3 p = Vector3.LerpUnclamped(_prevCamPos, _camPos, SimClock.Alpha);
        transform.position = p;
        transform.LookAt(WeightedTarget() + Vector3.up * _lookHeight);
    }
}
```

---

## 5. Visual Juice & Tactile Feedback Systems

### 5.1 Camera shake matrix (multi-layer Perlin, energy-scaled)

Shake amplitude scales with the **kinetic energy** of the impact, `E = ½·m·v²`, so a poster dunk shakes harder than a layup. Multiple Perlin octaves at different frequencies produce non-periodic, organic motion (no looping sine feel).

```csharp
// Juice/CameraShakeMatrix.cs
using UnityEngine;

/// <summary>
/// Additive, non-periodic camera shake from layered Perlin noise. Amplitude is driven
/// by impact kinetic energy. Applied as a transform offset AFTER BroadcastCamera so it
/// composes cleanly. Trauma decays each frame for a natural settle.
/// </summary>
public sealed class CameraShakeMatrix : MonoBehaviour
{
    [System.Serializable]
    public struct Octave { public float frequency; public float amplitude; }

    [SerializeField] private Octave[] _octaves =
    {
        new Octave { frequency = 18f, amplitude = 1.0f },
        new Octave { frequency = 7f,  amplitude = 0.5f },
        new Octave { frequency = 31f, amplitude = 0.25f },
    };
    [SerializeField] private float _trauma;             // [0,1], current shake energy
    [SerializeField] private float _decay = 1.8f;       // trauma units/sec
    [SerializeField] private float _maxPos = 0.35f;     // meters
    [SerializeField] private float _maxRot = 3.5f;      // degrees
    [SerializeField] private float _energyScale = 0.002f;

    private float _seed;
    private void Awake() => _seed = Random.value * 1000f;

    /// <summary>Add shake from an impact. mass kg, vel m/s → E=½mv².</summary>
    public void AddImpact(float mass, float velocity)
    {
        float energy = 0.5f * mass * velocity * velocity;
        _trauma = Mathf.Clamp01(_trauma + energy * _energyScale);
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f) return;
        // Quadratic falloff: shake feels punchy then settles fast.
        float shake = _trauma * _trauma;
        float t = Time.time;

        Vector3 pos = Vector3.zero; float roll = 0f;
        for (int i = 0; i < _octaves.Length; i++)
        {
            float f = _octaves[i].frequency, a = _octaves[i].amplitude;
            // PerlinNoise in [0,1] → remap to [-1,1]; distinct seeds per channel.
            pos.x += (Mathf.PerlinNoise(_seed + i,        t * f) * 2f - 1f) * a;
            pos.y += (Mathf.PerlinNoise(_seed + i + 17f,  t * f) * 2f - 1f) * a;
            roll  += (Mathf.PerlinNoise(_seed + i + 53f,  t * f) * 2f - 1f) * a;
        }

        transform.localPosition = pos * (_maxPos * shake);
        transform.localRotation = Quaternion.Euler(0f, 0f, roll * _maxRot * shake);

        _trauma = Mathf.Max(0f, _trauma - _decay * Time.deltaTime);
    }
}
```

### 5.2 Event-driven impact + crowd state machine

A lightweight, allocation-free `GameEvent` channel (ScriptableObject or static) decouples gameplay from juice. Gameplay raises `OnPosterDunk(contactPoint, energy)`; listeners spawn voxel debris (instanced), pulse the shake, and flip the crowd FSM to `Excited`.

```csharp
// Juice/GameEvents.cs — typed, GC-free broadcast (struct payloads, no boxing).
using UnityEngine;

public readonly struct ImpactPayload
{
    public readonly Vector3 Point; public readonly float Energy; public readonly Vector3 Normal;
    public ImpactPayload(Vector3 p, float e, Vector3 n) { Point = p; Energy = e; Normal = n; }
}

public static class GameEvents
{
    public static event System.Action<ImpactPayload> OnPosterDunk;
    public static event System.Action<ImpactPayload> OnPerfectSteal;
    public static event System.Action<ImpactPayload> OnSwish;

    public static void RaisePosterDunk(in ImpactPayload p)  => OnPosterDunk?.Invoke(p);
    public static void RaisePerfectSteal(in ImpactPayload p)=> OnPerfectSteal?.Invoke(p);
    public static void RaiseSwish(in ImpactPayload p)       => OnSwish?.Invoke(p);
}
```

```csharp
// Juice/CrowdStateMachine.cs
using UnityEngine;

/// <summary>
/// Crowd of instanced voxel spectators. On hype events the FSM shifts to Excited and
/// drives a per-instance vertical "jump" offset via a single MaterialPropertyBlock —
/// thousands of crowd voxels animate with ONE draw call, zero per-spectator GameObjects.
/// </summary>
public sealed class CrowdStateMachine : MonoBehaviour
{
    private enum CrowdState { Idle, Murmur, Excited }

    [SerializeField] private float _excitedDuration = 2.5f;
    [SerializeField] private float _jumpHeight = 0.4f;
    [SerializeField] private float _jumpFreq = 6f;
    [SerializeField] private Renderer _crowdRenderer;          // instanced mesh renderer
    private static readonly int Hype = Shader.PropertyToID("_Hype");
    private static readonly int JumpH = Shader.PropertyToID("_JumpHeight");
    private static readonly int Freq = Shader.PropertyToID("_JumpFreq");

    private CrowdState _state = CrowdState.Idle;
    private float _excitedTimer;
    private MaterialPropertyBlock _mpb;

    private void Awake() => _mpb = new MaterialPropertyBlock();
    private void OnEnable()  { GameEvents.OnPosterDunk += Hype3; GameEvents.OnPerfectSteal += Hype2; GameEvents.OnSwish += Hype1; }
    private void OnDisable() { GameEvents.OnPosterDunk -= Hype3; GameEvents.OnPerfectSteal -= Hype2; GameEvents.OnSwish -= Hype1; }

    private void Hype1(in ImpactPayload _) => Excite(0.5f);
    private void Hype2(in ImpactPayload _) => Excite(0.8f);
    private void Hype3(in ImpactPayload _) => Excite(1.0f);

    private void Excite(float intensity)
    {
        _state = CrowdState.Excited;
        _excitedTimer = _excitedDuration * intensity;
        _mpb.SetFloat(JumpH, _jumpHeight * intensity);
        _mpb.SetFloat(Freq, _jumpFreq);
    }

    private void Update()
    {
        float hype = 0f;
        switch (_state)
        {
            case CrowdState.Excited:
                _excitedTimer -= Time.deltaTime;
                hype = Mathf.Clamp01(_excitedTimer / _excitedDuration);
                if (_excitedTimer <= 0f) _state = CrowdState.Murmur;
                break;
            case CrowdState.Murmur:
                hype = 0.15f; // idle sway
                break;
        }
        // Shader reads _Hype + time to offset each instance vertically (vertex stage),
        // so the crowd "jumps" without touching the CPU per spectator.
        _mpb.SetFloat(Hype, hype);
        _crowdRenderer.SetPropertyBlock(_mpb);
    }
}
```

```csharp
// Juice/ImpactDebrisSpawner.cs — pooled, instanced voxel shards at the contact point.
using UnityEngine;

public sealed class ImpactDebrisSpawner : MonoBehaviour
{
    [SerializeField] private VoxelDebrisBurst _burst;     // GPU-instanced shard system
    [SerializeField] private CameraShakeMatrix _shake;
    [SerializeField] private float _shardMass = 0.4f;

    private void OnEnable()  => GameEvents.OnPosterDunk += React;
    private void OnDisable() => GameEvents.OnPosterDunk -= React;

    private void React(in ImpactPayload p)
    {
        // Energy → shard count and ejection speed; instanced, no per-shard GameObject.
        int count = Mathf.Clamp(Mathf.RoundToInt(p.Energy * 0.05f), 6, 64);
        _burst.Emit(p.Point, p.Normal, count, Mathf.Sqrt(2f * p.Energy / _shardMass));
        _shake.AddImpact(_shardMass * count, Mathf.Sqrt(2f * p.Energy / _shardMass));
    }
}
```

---

## Cross-Cutting Engineering Standards

- **SOLID / structure:** capture (input), simulation (motor/ball/rim), and presentation (view/camera/juice) are separate assemblies. Juice depends on `GameEvents` only — never the reverse. Ratings/attributes are injected `struct` configs, not hard-coded.
- **No GC in hot paths:** ring buffers, `NativeArray`/`NativeList`, `ObjectPool<T>`, `MaterialPropertyBlock`, and `struct` event payloads. No `foreach` over allocating enumerables, no LINQ, no per-frame `new` in `Update`/`FixedUpdate`.
- **Determinism:** all gameplay RNG is a seeded xorshift keyed on `(matchSeed, SimClock.Tick)`; never `UnityEngine.Random` in simulation. Fixed timestep + Verlet/analytic integration → reproducible shots, replays, rollback-ready.
- **Render decoupling:** every simulated body stores `Prev`/`Curr` pose; views `LerpUnclamped` by `SimClock.Alpha` in `LateUpdate`. 60 Hz sim, uncapped render.
- **Burst/Jobs:** greedy meshing and any per-voxel sweep are `[BurstCompile] IJob`/`IJobParallelFor`, scheduled off-main-thread, completed before the mesh upload with `MeshUpdateFlags.DontRecalculateBounds`.
- **Verification:** EditMode tests assert (a) `InputBuffer.TryConsume` window edges, (b) `ShotReleaseSystem` apex/Δt math, (c) `RimSolver.Reflect` energy bounds (`|v'| ≤ |v|` for `e≤1`), (d) greedy mesh quad counts vs. known fixtures. PlayMode tests assert motor stop-distance under friction and camera critical damping (no overshoot at ζ=1).
