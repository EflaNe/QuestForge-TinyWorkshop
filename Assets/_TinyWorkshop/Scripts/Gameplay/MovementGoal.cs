using UnityEngine;
namespace TinyWorkshop
{
    public sealed class MovementGoal : MonoBehaviour
    {
        public WorkshopSession session;
        [Tooltip("Optional sprite that bobs gently so the goal reads as \"reach me\".")]
        public Transform visual;
        Vector3 home;
        void Awake()
        {
            if (visual)
                home = visual.localPosition;
        }
        void Update()
        {
            if (visual)
                visual.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * 3) * .08f);
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<RobotController2D>(out _))
                session.CompleteDemo();
        }
    }
}
