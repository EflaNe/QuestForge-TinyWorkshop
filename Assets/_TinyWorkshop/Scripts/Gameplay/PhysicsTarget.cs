using UnityEngine;
namespace TinyWorkshop
{
    public sealed class PhysicsTarget : MonoBehaviour
    {
        public WorkshopSession session;
        public SpriteRenderer visual;
        bool scored;
        void OnTriggerEnter2D(Collider2D other)
        {
            if (scored || !other.attachedRigidbody || !other.attachedRigidbody.TryGetComponent<Gear>(out _))
                return;
            scored = true;
            visual.color = new Color(.65f, 1, .7f);
            session.HitTarget();
            session.Feedback.Burst(transform.position, 10, new Color(.65f, 1, .7f));
        }
    }
}
