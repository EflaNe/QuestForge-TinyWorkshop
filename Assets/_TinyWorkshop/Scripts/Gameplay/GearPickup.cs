using UnityEngine;
namespace TinyWorkshop
{
    // Trigger around a gear (WorkshopPickup layer, collides only with the robot).
    public sealed class GearPickup : MonoBehaviour
    {
        public Gear gear;

        void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
        void OnTriggerStay2D(Collider2D other) => TryPickup(other);   // retry when the bag frees up

        void TryPickup(Collider2D other)
        {
            if (other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<RobotController2D>(out _))
                gear.TryCollect();
        }
    }
}
