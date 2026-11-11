using UnityEngine;
namespace TinyWorkshop
{
    public sealed class GearPickup : MonoBehaviour
    {
        public Gear gear;
        void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
        void OnTriggerStay2D(Collider2D other) => TryPickup(other);
        void TryPickup(Collider2D other)
        {
            if (other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<RobotController2D>(out var robot))
                gear.TryCollect(robot);
        }
    }
}
