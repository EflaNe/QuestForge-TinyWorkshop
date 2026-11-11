using UnityEngine;
namespace TinyWorkshop
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Gear : MonoBehaviour
    {
        public WorkshopSession session;
        [Tooltip("Main game: robot pulls the gear in from this distance once it has settled.")]
        public float magnetRange = 1.15f;
        [Tooltip("Gears resting above this height are out of jump reach and get recovered.")]
        public float unreachableAbove = -.8f;
        public int Id
        {
            get; private set;
        }
        public bool Collectable
        {
            get; private set;
        }
        public bool Collected
        {
            get; private set;
        }
        Rigidbody2D body;
        Transform visual;
        float spawnedAt, impactAt, stuckFor;
        bool landed, pulled;
        float baseGravity = 1;
        public void Initialize(WorkshopSession owner, int id, Vector2 velocity, bool collectable)
        {
            session = owner;
            Id = id;
            Collectable = collectable;
            body = GetComponent<Rigidbody2D>();
            visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            spawnedAt = Time.time;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.Range(-220, 220);
        }
        void Update()
        {
            if (!session || Collected)
                return;
            if (visual)
            {
                // Spawn pop + gentle idle bob once resting, so gears read as "pick me up".
                float age = Time.time - spawnedAt;
                float pop = age < .18f ? Mathf.Lerp(.3f, 1.25f, age / .18f) : Mathf.Lerp(1.25f, 1, Mathf.Clamp01((age - .18f) / .12f));
                visual.localScale = Vector3.one * pop;
                visual.localPosition = landed && Collectable ? Vector3.up * (Mathf.Sin(Time.time * 4 + Id) * .04f + .03f) : Vector3.zero;
            }
            if (session.mode == WorkshopMode.Physics && Time.time - spawnedAt > 20)
            {
                session.RemoveWorldGear(this);
                Destroy(gameObject);
                return;
            }
            bool outside = transform.position.y < -5.1f || Mathf.Abs(transform.position.x) > 9.65f || transform.position.y > 5.4f;
            // Recover only gears resting where the robot cannot reach (higher than any platform + jump).
            bool unreachable = transform.position.y > unreachableAbove && body && body.linearVelocity.sqrMagnitude < .03f;
            stuckFor = unreachable ? stuckFor + Time.deltaTime : 0;
            if (outside || stuckFor > 2)
            {
                transform.position = session.safeGearPoint;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                stuckFor = 0;
            }
        }
        void FixedUpdate()
        {
            if (!session || Collected || !Collectable || !session.Robot)
                return;
            Vector2 toRobot = (Vector2)session.Robot.transform.position - body.position;
            bool pull = Time.time - spawnedAt > .45f && session.CanCarryMore && toRobot.sqrMagnitude < magnetRange * magnetRange;
            if (pull)
            {
                if (!pulled)
                    baseGravity = body.gravityScale;
                pulled = true;
                body.gravityScale = 0;
                body.linearDamping = 0;
                body.linearVelocity = toRobot.normalized * 9;
            }
            else if (pulled)
            {
                // Robot left range or bag is full: drop back into normal physics.
                pulled = false;
                body.gravityScale = baseGravity;
            }
        }
        public bool TryCollect(RobotController2D robot)
        {
            if (Collected || !Collectable || !session.TryCollect(this))
                return false;
            Collected = true;
            robot.PickupReaction();
            session.Hud.Floater(transform.position + Vector3.up * .5f, "+1", new Color(.7f, 1, .7f));
            session.Feedback.Play("collect", 1, 1 + session.Carried * .12f);
            session.Feedback.Burst(transform.position, 12, new Color(.65f, 1, .7f));
            session.Feedback.Hitstop(.035f);
            session.Feedback.Shake(.05f);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!session)
                return;
            if (Time.time > impactAt && collision.relativeVelocity.magnitude > 1.4f)
            {
                session.Feedback.Play("bounce", Mathf.Clamp01(collision.relativeVelocity.magnitude / 12));
                impactAt = Time.time + .12f;
            }
            // Main game: once a gear touches a floor/platform top it settles instead of rolling off,
            // so the launch target (often a platform) is where the player has to go.
            if (session.mode == WorkshopMode.MainGame && !landed && collision.contactCount > 0 && collision.GetContact(0).normal.y > .5f)
            {
                landed = true;
                body.linearDamping = 5;
                var v = body.linearVelocity;
                body.linearVelocity = new Vector2(v.x * .2f, Mathf.Min(v.y, 1.2f));
                body.angularDamping = 6;
                session.Feedback.Burst(collision.GetContact(0).point, 5, new Color(.9f, .8f, .55f));
            }
        }
    }
}
