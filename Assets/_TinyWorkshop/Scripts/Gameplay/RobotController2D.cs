using System;
using UnityEngine;
namespace TinyWorkshop
{
    // Player movement only: run, jump (coyote time, jump buffer, variable height),
    // heavier fall and returning to the last safe platform.
    // Input arrives through Move / PressJump / ReleaseJump (see RobotInput);
    // visuals and sounds listen to the events (see RobotPresentation).
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class RobotController2D : MonoBehaviour
    {
        const float GroundProbeDistance = .065f;
        const float MaxGroundedRiseSpeed = .2f;
        const float UnassistedJumpBuffer = .025f;
        const float SafeEdgeMargin = .12f;

        [SerializeField] MovementProfile profile;
        [SerializeField] LayerMask groundMask = 1 << 8;
        [Tooltip("Below this height the robot returns to the last safe platform.")]
        [SerializeField] float fallLimit = -5.3f;
        [Tooltip("Coyote time and jump buffer on/off.")]
        public bool assistance = true;

        public event Action Jumped, Landed, Respawned;

        public MovementProfile Profile => profile;
        public bool Grounded { get; private set; }
        public Vector2 Velocity => body ? body.linearVelocity : Vector2.zero;

        Rigidbody2D body;
        CapsuleCollider2D shape;
        ContactFilter2D groundFilter;
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        float moveInput;
        float jumpPressedAt = float.NegativeInfinity;
        float lastGroundedAt = float.NegativeInfinity;
        bool jumpReleased;
        Vector2 safePosition;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<CapsuleCollider2D>();
            groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            safePosition = transform.position;
        }

        // ---------------------------------------------------------------- input
        public void Move(float direction) => moveInput = Mathf.Clamp(direction, -1, 1);
        public void PressJump() => jumpPressedAt = Time.time;
        public void ReleaseJump() => jumpReleased = true;
        public void SetProfile(MovementProfile value) => profile = value;

        // ---------------------------------------------------------------- physics step
        void FixedUpdate()
        {
            CheckGround();
            var velocity = body.linearVelocity;
            velocity.x = Run(velocity.x);
            velocity.y = Jump(velocity.y);
            body.gravityScale = profile.gravity * (velocity.y < 0 ? profile.fallMultiplier : 1);
            body.linearVelocity = velocity;
            if (body.position.y < fallLimit)
                Respawn();
        }

        void CheckGround()
        {
            bool wasGrounded = Grounded;
            bool rising = body.linearVelocity.y > MaxGroundedRiseSpeed;
            Grounded = !rising && shape.Cast(Vector2.down, groundFilter, groundHits, GroundProbeDistance) > 0;
            if (!Grounded)
                return;
            lastGroundedAt = Time.time;
            RememberSafePosition(groundHits[0]);
            if (!wasGrounded)
                Landed?.Invoke();
        }

        // Move toward the target speed at a fixed rate: acceleration while steering, braking when released.
        float Run(float speed)
        {
            float target = moveInput * profile.maxSpeed;
            float rate = moveInput == 0 ? profile.braking : profile.acceleration;
            return Mathf.MoveTowards(speed, target, rate * Time.fixedDeltaTime);
        }

        float Jump(float speed)
        {
            float bufferWindow = assistance ? profile.jumpBuffer : UnassistedJumpBuffer;
            float coyoteWindow = assistance ? profile.coyoteTime : 0;
            bool jumpRequested = Time.time - jumpPressedAt <= bufferWindow;
            bool canJump = Grounded || Time.time - lastGroundedAt <= coyoteWindow;
            if (jumpRequested && canJump)
            {
                speed = profile.jumpSpeed;
                jumpPressedAt = float.NegativeInfinity;   // consume the press
                lastGroundedAt = float.NegativeInfinity;  // close the coyote window: no second jump
                Grounded = false;
                Jumped?.Invoke();
            }
            if (jumpReleased)
            {
                if (speed > 0)
                    speed *= profile.jumpCut;             // released early: short hop
                jumpReleased = false;
            }
            return speed;
        }

        // ---------------------------------------------------------------- safe position
        // Store a point on top of the current platform, kept away from its edges,
        // so a respawn never drops the robot right back off the ledge.
        void RememberSafePosition(RaycastHit2D ground)
        {
            bool standingOnTop = ground.normal.y > .5f;
            if (!standingOnTop)
                return;
            var platform = ground.collider.bounds;
            float margin = shape.size.x * .5f + SafeEdgeMargin;
            float x = Mathf.Clamp(body.position.x, platform.min.x + margin, platform.max.x - margin);
            float y = platform.max.y + shape.size.y * .5f + .02f;
            safePosition = new Vector2(x, y);
        }

        public void Respawn()
        {
            body.position = safePosition + Vector2.up * .08f;
            body.linearVelocity = Vector2.zero;
            Respawned?.Invoke();
        }
    }
}
