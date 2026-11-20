using UnityEngine;
namespace TinyWorkshop
{
    // Tuning data for RobotController2D. One asset per "feel" (Normal / Heavy / Light).
    [CreateAssetMenu(menuName = "Tiny Workshop/Movement Profile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Run")]
        public float maxSpeed = 5.4f;
        [Tooltip("Speed gained per second while a direction is held.")]
        public float acceleration = 45;
        [Tooltip("Speed lost per second when no direction is held.")]
        public float braking = 65;

        [Header("Jump")]
        public float jumpSpeed = 10.5f;
        [Tooltip("Rigidbody gravity scale while rising.")]
        public float gravity = 3.5f;
        [Tooltip("Gravity is multiplied by this while falling, for a snappier landing.")]
        public float fallMultiplier = 1.4f;
        [Tooltip("Upward speed is multiplied by this when jump is released early (short hop).")]
        public float jumpCut = .5f;

        [Header("Assist")]
        [Tooltip("Seconds after leaving a ledge during which a jump is still accepted.")]
        public float coyoteTime = .12f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        public float jumpBuffer = .12f;
    }
}
