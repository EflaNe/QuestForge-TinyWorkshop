using UnityEngine;
namespace TinyWorkshop
{
    public sealed class PhysicsBumper : MonoBehaviour
    {
        public WorkshopSession session;
        public Vector2 designedVelocity = new Vector2(5.8f, 6);
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!session.DesignedPhysics || !collision.rigidbody || !collision.rigidbody.TryGetComponent<Gear>(out _))
                return;
            collision.rigidbody.linearVelocity = designedVelocity;
            session.Feedback.Burst(collision.GetContact(0).point, 5, new Color(.6f, 1, .8f));
        }
    }
}
