using UnityEngine;
using UnityEngine.InputSystem;
namespace TinyWorkshop
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class RobotController2D : MonoBehaviour
    {
        public MovementProfile[] profiles;
        public Transform visual;
        public WorkshopSession session;
        public LayerMask groundMask = 1 << 8;
        public bool assistance = true;
        [Tooltip("Sprite stacked on the robot's back for each carried gear.")]
        public Sprite carrySprite;
        [Tooltip("Back-stack offset of carried gears from the visual origin (x mirrors with facing).")]
        public Vector2 carryOffset = new Vector2(.3f, .2f);
        public float carryScale = .62f;
        [Header("Sprite animation (optional)")]
        [Tooltip("Animator with Speed (float) and Happy (trigger) parameters; frames face right.")]
        public Animator animator;
        public SpriteRenderer bodySprite;
        [Tooltip("Speech bubble shown briefly on pickup / delivery.")]
        public SpriteRenderer emote;
        SpriteRenderer[] carryIcons;
        Vector3 visualHome, emoteHome;
        float emoteUntil = -10, emoteAt = -10;
        static readonly int SpeedId = Animator.StringToHash("Speed"), HappyId = Animator.StringToHash("Happy");
        int shownCarry;
        public int ProfileIndex
        {
            get; private set;
        }
        public bool Grounded
        {
            get; private set;
        }
        public Vector2 Velocity => body ? body.linearVelocity : Vector2.zero;
        Rigidbody2D body; CapsuleCollider2D shape;
        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        float lastFacing = -1;
        float horizontal, bufferedUntil = -10, groundedAt = -10, punch, walkPhase, stepAt;
        bool cutRequested, jumpConsumed;
        Vector3 safePosition;
        public MovementProfile Settings => profiles[ProfileIndex];
        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<CapsuleCollider2D>();
            safePosition = transform.position;
            if (visual)
                visualHome = visual.localPosition;
            if (emote)
            {
                emoteHome = emote.transform.localPosition;
                emote.gameObject.SetActive(false);
            }
            if (carrySprite && visual)
            {
                carryIcons = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("CarriedGear_" + i);
                    go.transform.SetParent(visual, false);
                    go.transform.localScale = Vector3.one * carryScale;
                    carryIcons[i] = go.AddComponent<SpriteRenderer>();
                    carryIcons[i].sprite = carrySprite;
                    carryIcons[i].sortingOrder = 11;
                    go.SetActive(false);
                }
            }
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k == null)
                return;
            horizontal = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            if (k.spaceKey.wasPressedThisFrame)
                bufferedUntil = Time.time + (assistance ? Settings.jumpBuffer : .025f);
            if (k.spaceKey.wasReleasedThisFrame)
                cutRequested = true;
            Animate();
        }
        void FixedUpdate()
        {
            var p = Settings;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            bool wasGrounded = Grounded;
            Grounded = body.linearVelocity.y <= .2f && shape.Cast(Vector2.down, filter, hits, .065f) > 0;
            if (Grounded)
            {
                groundedAt = Time.time;
                jumpConsumed = false;
                if (hits[0].normal.y > .5f)
                {
                    var bounds = hits[0].collider.bounds;
                    float margin = shape.size.x * .5f + .12f;
                    safePosition = new Vector3(Mathf.Clamp(body.position.x, bounds.min.x + margin, bounds.max.x - margin), bounds.max.y + shape.size.y * .5f + .02f, 0);
                }
                if (!wasGrounded)
                {
                    punch = -session.Feedback.profile.landingPunch;
                    session.Feedback.Play("land", .55f);
                    session.Feedback.Burst(transform.position + Vector3.down * .42f, 5, new Color(.75f, .7f, .5f));
                }
            }
            var velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, horizontal * p.maxSpeed, (horizontal == 0 ? p.braking : p.acceleration) * Time.fixedDeltaTime);
            bool jump = bufferedUntil >= Time.time && !jumpConsumed && (Grounded || Time.time - groundedAt <= (assistance ? p.coyoteTime : 0));
            if (jump)
            {
                velocity.y = p.jumpSpeed;
                bufferedUntil = -10;
                groundedAt = -10;
                jumpConsumed = true;
                Grounded = false;
                punch = .17f;
                session.Feedback.Play("jump");
                session.Feedback.Burst(transform.position + Vector3.down * .4f, 4, new Color(.6f, .8f, .65f));
            }
            if (cutRequested)
            {
                if (velocity.y > 0)
                    velocity.y *= p.jumpCut;
                cutRequested = false;
            }
            body.gravityScale = p.gravity * (velocity.y < 0 ? p.fallMultiplier : 1);
            body.linearVelocity = velocity;
            if (transform.position.y < -5.3f)
                Respawn();
        }
        void Animate()
        {
            if (!visual)
                return;
            punch = Mathf.Lerp(punch, 0, Time.deltaTime * 15);
            float moving = Mathf.Abs(Velocity.x) / Settings.maxSpeed;
            walkPhase += Time.deltaTime * 18 * moving;
            float bob = Grounded ? Mathf.Abs(Mathf.Sin(walkPhase)) * .045f * moving : 0;
            // Idle "breathing" keeps the robot alive between animation frames.
            float breathe = bodySprite && moving < .1f && Grounded ? Mathf.Sin(Time.time * 3.2f) * .018f : 0;
            visual.localPosition = visualHome + new Vector3(0, bob, 0);
            visual.localScale = new Vector3(1 - punch - breathe * .5f, 1 + punch + breathe, 1);
            visual.localRotation = Quaternion.Euler(0, 0, -Velocity.x * .9f + (Grounded ? Mathf.Sin(walkPhase) * 2 * moving : 0));
            if (animator)
                animator.SetFloat(SpeedId, Grounded ? Mathf.Abs(Velocity.x) : 0);
            if (bodySprite)
            {
                if (Velocity.x > .1f)
                    bodySprite.flipX = false;
                else if (Velocity.x < -.1f)
                    bodySprite.flipX = true;
            }
            if (emote)
            {
                bool on = Time.time < emoteUntil;
                emote.gameObject.SetActive(on);
                if (on)
                {
                    float age = Time.time - emoteAt;
                    float pop = age < .12f ? Mathf.Lerp(.2f, 1.15f, age / .12f) : Mathf.Lerp(1.15f, 1, Mathf.Clamp01((age - .12f) / .1f));
                    float fade = Mathf.Clamp01((emoteUntil - Time.time) / .15f);
                    emote.transform.localPosition = emoteHome + Vector3.up * (Mathf.Sin(age * 6) * .04f + age * .15f);
                    emote.transform.localScale = Vector3.one * pop * fade;
                }
            }
            if (carryIcons != null)
            {
                int carried = session ? session.Carried : 0;
                float facing = Velocity.x < -.1f ? 1 : Velocity.x > .1f ? -1 : lastFacing;
                lastFacing = facing;
                for (int i = 0; i < carryIcons.Length; i++)
                {
                    bool on = i < carried;
                    carryIcons[i].gameObject.SetActive(on);
                    if (!on)
                        continue;
                    // Stack on the back (opposite to movement), slightly lagging so it sways.
                    float sway = Mathf.Sin(Time.time * 6 + i) * .02f * (1 + moving * 2);
                    var target = new Vector3(facing * carryOffset.x + sway, carryOffset.y + i * .22f, 0);
                    var t = carryIcons[i].transform;
                    t.localPosition = i >= shownCarry ? target + Vector3.up * .5f : Vector3.Lerp(t.localPosition, target, Time.deltaTime * 14);
                    t.localRotation = Quaternion.Euler(0, 0, Time.time * 40 * (i % 2 == 0 ? 1 : -1));
                }
                shownCarry = carried;
            }
            if (moving > .2f && Grounded && Time.time > stepAt)
            {
                session.Feedback.Play("step", .6f);
                stepAt = Time.time + .19f;
            }
        }
        public void SelectProfile(int index)
        {
            ProfileIndex = Mathf.Clamp(index, 0, profiles.Length - 1);
        }
        public void PickupReaction()
        {
            punch = session.Feedback.profile.pickupPunch;
            Emote(.9f);
        }
        // Happy face + heart bubble: the robot "enjoys" picking up and delivering.
        public void Emote(float duration)
        {
            if (animator)
                animator.SetTrigger(HappyId);
            if (!emote)
                return;
            if (Time.time >= emoteUntil)
                emoteAt = Time.time;
            emoteUntil = Time.time + duration;
        }
        public void Respawn()
        {
            transform.position = safePosition + Vector3.up * .08f;
            body.linearVelocity = Vector2.zero;
            session.Notify("Güvenli noktaya döndün.");
        }
    }
}
