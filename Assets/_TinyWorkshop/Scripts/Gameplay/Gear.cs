using UnityEngine;
namespace TinyWorkshop
{
    // A produced gear: flies on a physics arc, settles on platforms (main game),
    // is pulled in by a nearby robot and gets recovered if it ends up out of reach.
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Gear : MonoBehaviour
    {
        public WorkshopSession session;

        [Header("Magnet (main game)")]
        [Tooltip("The robot pulls the gear in from this distance.")]
        public float magnetRange = 1.15f;
        public float magnetSpeed = 9;
        [Tooltip("Seconds after spawning before the magnet works, so gears are not sucked out of the machine.")]
        public float magnetDelay = .45f;

        [Header("Recovery")]
        [Tooltip("Gears resting above this height are out of jump reach and get recovered.")]
        public float unreachableAbove = -.8f;
        [Tooltip("Gears leaving this area are put back at the session's safe gear point.")]
        public Rect playArea = new Rect(-9.65f, -5.1f, 19.3f, 10.5f);
        [Tooltip("Physics lab: thrown gears disappear after this many seconds.")]
        public float labLifetime = 20;

        const float StuckTimeBeforeRecovery = 2;
        const float BounceSoundSpeed = 1.4f;

        public int Id { get; private set; }
        public bool Collectable { get; private set; }
        public bool Collected { get; private set; }

        Rigidbody2D body;
        Transform visual;
        float spawnTime, nextBounceSoundTime, stuckTime, gravityBeforeMagnet;
        bool landed, magnetized;

        public void Initialize(WorkshopSession owner, int id, Vector2 velocity, bool collectable)
        {
            session = owner;
            Id = id;
            Collectable = collectable;
            body = GetComponent<Rigidbody2D>();
            visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            spawnTime = Time.time;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.Range(-220, 220);
        }

        void Update()
        {
            if (!session || Collected)
                return;
            AnimateVisual();
            if (session.mode == WorkshopMode.Physics && Time.time - spawnTime > labLifetime)
                Expire();
            else
                RecoverIfLost();
        }

        void FixedUpdate()
        {
            if (session && Collectable && !Collected)
                UpdateMagnet();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!session)
                return;
            PlayBounceSound(collision.relativeVelocity.magnitude);
            bool hitFromAbove = collision.contactCount > 0 && collision.GetContact(0).normal.y > .5f;
            if (session.mode == WorkshopMode.MainGame && !landed && hitFromAbove)
                SettleOnPlatform(collision.GetContact(0).point);
        }

        // ---------------------------------------------------------------- collecting
        public bool TryCollect()
        {
            if (Collected || !Collectable || !session.TryCollect(this))
                return false;
            Collected = true;
            PlayCollectFeedback();
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        void PlayCollectFeedback()
        {
            var feedback = session.Feedback;
            session.Hud.Floater(transform.position + Vector3.up * .5f, "+1", new Color(.7f, 1, .7f));
            feedback.Play("collect", 1, 1 + session.Carried * .12f);   // pitch rises with each carried gear
            feedback.Burst(transform.position, 12, new Color(.65f, 1, .7f));
            feedback.Hitstop(.035f);
            feedback.Shake(.05f);
        }

        // ---------------------------------------------------------------- magnet
        void UpdateMagnet()
        {
            if (!session.Robot)
                return;
            Vector2 toRobot = (Vector2)session.Robot.transform.position - body.position;
            bool inRange = toRobot.sqrMagnitude < magnetRange * magnetRange;
            bool shouldPull = Time.time - spawnTime > magnetDelay && session.CanCarryMore && inRange;
            if (shouldPull)
            {
                if (!magnetized)
                    gravityBeforeMagnet = body.gravityScale;
                magnetized = true;
                body.gravityScale = 0;
                body.linearDamping = 0;
                body.linearVelocity = toRobot.normalized * magnetSpeed;
            }
            else if (magnetized)
            {
                // Robot walked away or the bag is full: back to normal physics.
                magnetized = false;
                body.gravityScale = gravityBeforeMagnet;
            }
        }

        // ---------------------------------------------------------------- landing
        // Main game: a gear stops rolling once it touches a floor or platform top,
        // so the spot it was launched at is where the player has to go.
        void SettleOnPlatform(Vector2 contact)
        {
            landed = true;
            body.linearDamping = 5;
            body.angularDamping = 6;
            var v = body.linearVelocity;
            body.linearVelocity = new Vector2(v.x * .2f, Mathf.Min(v.y, 1.2f));
            session.Feedback.Burst(contact, 5, new Color(.9f, .8f, .55f));
        }

        void PlayBounceSound(float impactSpeed)
        {
            if (impactSpeed <= BounceSoundSpeed || Time.time < nextBounceSoundTime)
                return;
            session.Feedback.Play("bounce", Mathf.Clamp01(impactSpeed / 12));
            nextBounceSoundTime = Time.time + .12f;
        }

        // ---------------------------------------------------------------- recovery
        void RecoverIfLost()
        {
            bool outside = !playArea.Contains(transform.position);
            bool restingOutOfReach = transform.position.y > unreachableAbove && body.linearVelocity.sqrMagnitude < .03f;
            stuckTime = restingOutOfReach ? stuckTime + Time.deltaTime : 0;
            if (!outside && stuckTime <= StuckTimeBeforeRecovery)
                return;
            transform.position = session.safeGearPoint;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            stuckTime = 0;
        }

        void Expire()
        {
            session.RemoveWorldGear(this);
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- look
        // Spawn pop, then a gentle bob once resting so the gear reads as "pick me up".
        void AnimateVisual()
        {
            if (!visual)
                return;
            float age = Time.time - spawnTime;
            visual.localScale = Vector3.one * Tween.Pop(age, .3f, 1.25f, .18f, .12f);
            bool bobbing = landed && Collectable;
            visual.localPosition = bobbing ? Vector3.up * (Mathf.Sin(Time.time * 4 + Id) * .04f + .03f) : Vector3.zero;
        }
    }
}
