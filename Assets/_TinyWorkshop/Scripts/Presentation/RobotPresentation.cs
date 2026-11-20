using UnityEngine;
namespace TinyWorkshop
{
    // Everything the player sees and hears from the robot: squash & stretch, walk bob, lean,
    // idle breathing, animator parameters, footsteps, jump/land effects and the happy bubble.
    // Purely cosmetic: it reads RobotController2D and never changes physics.
    [RequireComponent(typeof(RobotController2D))]
    public sealed class RobotPresentation : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int HappyId = Animator.StringToHash("Happy");
        const float StepInterval = .19f;

        public WorkshopFeedback feedback;
        [Tooltip("Child that squashes and leans; the physics body itself never scales.")]
        public Transform visual;
        public SpriteRenderer bodySprite;
        [Tooltip("Animator with Speed (float) and Happy (trigger); frames face right.")]
        public Animator animator;
        [Tooltip("Speech bubble shown briefly on pickup / delivery.")]
        public SpriteRenderer emote;

        RobotController2D robot;
        Vector3 visualHome, emoteHome;
        float squash, walkPhase, nextStepAt;
        float emoteStartedAt = float.NegativeInfinity, emoteUntil = float.NegativeInfinity;

        void Awake()
        {
            robot = GetComponent<RobotController2D>();
            if (visual)
                visualHome = visual.localPosition;
            if (emote)
            {
                emoteHome = emote.transform.localPosition;
                emote.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            robot.Jumped += OnJumped;
            robot.Landed += OnLanded;
        }

        void OnDisable()
        {
            robot.Jumped -= OnJumped;
            robot.Landed -= OnLanded;
        }

        // ---------------------------------------------------------------- reactions
        void OnJumped()
        {
            squash = .17f;
            feedback.Play("jump");
            feedback.Burst(transform.position + Vector3.down * .4f, 4, new Color(.6f, .8f, .65f));
        }

        void OnLanded()
        {
            squash = -feedback.profile.landingPunch;
            feedback.Play("land", .55f);
            feedback.Burst(transform.position + Vector3.down * .42f, 5, new Color(.75f, .7f, .5f));
        }

        public void PickupReaction()
        {
            squash = feedback.profile.pickupPunch;
            Emote(.9f);
        }

        // Happy face + heart bubble; calling again while visible just extends it.
        public void Emote(float duration)
        {
            if (animator)
                animator.SetTrigger(HappyId);
            if (Time.time >= emoteUntil)
                emoteStartedAt = Time.time;
            emoteUntil = Time.time + duration;
        }

        // ---------------------------------------------------------------- per frame
        void Update()
        {
            float speed = Mathf.Abs(robot.Velocity.x);
            float speed01 = speed / robot.Profile.maxSpeed;
            AnimateBody(speed01);
            AnimateEmote();
            PlayFootsteps(speed01);
            if (animator)
                animator.SetFloat(SpeedId, robot.Grounded ? speed : 0);
            if (bodySprite && speed > .1f)
                bodySprite.flipX = robot.Velocity.x < 0;
        }

        void AnimateBody(float speed01)
        {
            if (!visual)
                return;
            bool grounded = robot.Grounded;
            squash = Mathf.Lerp(squash, 0, Time.deltaTime * 15);
            walkPhase += Time.deltaTime * 18 * speed01;
            float bob = grounded ? Mathf.Abs(Mathf.Sin(walkPhase)) * .045f * speed01 : 0;
            float sway = grounded ? Mathf.Sin(walkPhase) * 2 * speed01 : 0;
            float breathe = grounded && speed01 < .1f ? Mathf.Sin(Time.time * 3.2f) * .018f : 0;
            visual.localPosition = visualHome + Vector3.up * bob;
            visual.localScale = new Vector3(1 - squash - breathe * .5f, 1 + squash + breathe, 1);
            visual.localRotation = Quaternion.Euler(0, 0, -robot.Velocity.x * .9f + sway);
        }

        void AnimateEmote()
        {
            if (!emote)
                return;
            bool visible = Time.time < emoteUntil;
            emote.gameObject.SetActive(visible);
            if (!visible)
                return;
            float age = Time.time - emoteStartedAt;
            float fade = Mathf.Clamp01((emoteUntil - Time.time) / .15f);
            emote.transform.localPosition = emoteHome + Vector3.up * (Mathf.Sin(age * 6) * .04f + age * .15f);
            emote.transform.localScale = Vector3.one * Tween.Pop(age, .2f, 1.15f, .12f, .1f) * fade;
        }

        void PlayFootsteps(float speed01)
        {
            if (speed01 < .2f || !robot.Grounded || Time.time < nextStepAt)
                return;
            feedback.Play("step", .6f);
            nextStepAt = Time.time + StepInterval;
        }
    }
}
