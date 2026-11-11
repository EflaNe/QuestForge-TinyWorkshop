using UnityEngine;
namespace TinyWorkshop
{
    public sealed class MovementGoal : MonoBehaviour
    {
        public WorkshopSession session;
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<RobotController2D>(out _))
                session.CompleteDemo();
        }
    }
}
