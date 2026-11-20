using UnityEngine;
namespace TinyWorkshop
{
    // Look and sound of the production machine: the button dips, the body squashes,
    // charge lamps light up one by one, and producing a gear flashes everything.
    // Uses unscaled time so the animation keeps playing through hitstop.
    [RequireComponent(typeof(WorkshopMachine))]
    public sealed class MachinePresentation : MonoBehaviour
    {
        const float ProduceFlashLength = .35f;
        const float LampPopLength = .2f;

        public Transform buttonVisual, bodyVisual;
        public float pressDepth = .14f;
        [Tooltip("Soft glows over the painted lamps: unlit = invisible, lit = their colour.")]
        public SpriteRenderer[] chargeLights;
        public Color[] glowColors;
        [Tooltip("\"Press me\" marks next to the button, shown while pressing is possible.")]
        public SpriteRenderer readySpark;
        [Tooltip("Flash behind the button on each press.")]
        public SpriteRenderer pressGlow;

        WorkshopMachine machine;
        MachineInput input;
        Vector3 buttonHome, sparkScale, glowScale;
        Vector3[] lightScales;
        float sparkAlpha;

        WorkshopFeedback Feedback => machine.session.Feedback;
        float PressLength => Mathf.Max(.08f, machine.Profile.pressDuration * 1.6f);

        void Awake()
        {
            machine = GetComponent<WorkshopMachine>();
            input = GetComponent<MachineInput>();
            if (buttonVisual)
                buttonHome = buttonVisual.localPosition;
            if (readySpark)
                sparkScale = readySpark.transform.localScale;
            if (pressGlow)
                glowScale = pressGlow.transform.localScale;
            lightScales = new Vector3[chargeLights.Length];
            for (int i = 0; i < chargeLights.Length; i++)
                lightScales[i] = chargeLights[i].transform.localScale;
        }

        void OnEnable()
        {
            machine.Pressed += OnPressed;
            machine.Produced += OnProduced;
        }

        void OnDisable()
        {
            machine.Pressed -= OnPressed;
            machine.Produced -= OnProduced;
        }

        // ---------------------------------------------------------------- reactions
        void OnPressed()
        {
            // Rising pitch: each press audibly "charges" the machine.
            Feedback.Play("press", 1, .9f + machine.Energy * .12f);
            Feedback.Shake(.035f);
            Feedback.Burst(buttonVisual ? buttonVisual.position : transform.position, 4, new Color(1, .8f, .45f));
        }

        void OnProduced()
        {
            var outlet = machine.outlet.position;
            Feedback.Play("produce");
            Feedback.Burst(outlet, 18, new Color(1, .73f, .32f));
            Feedback.Puff(outlet, 7);
            Feedback.Shake(.12f);
            Feedback.Hitstop(.07f);
        }

        // ---------------------------------------------------------------- per frame
        void Update()
        {
            float now = Time.unscaledTime;
            float pressT = (now - machine.LastPressTime) / PressLength;
            float produceT = (now - machine.LastProduceTime) / ProduceFlashLength;
            float press = pressT < 1 ? Mathf.Sin(pressT * Mathf.PI) * (1 - pressT * .4f) : 0;      // button dip
            float produce = produceT < 1 ? Mathf.Sin(produceT * Mathf.PI) * (1 - produceT) : 0;  // big squash
            bool idle = machine.IsReady && pressT >= 1;
            bool hovered = input && input.Hovered;

            AnimateButton(press, idle, hovered && machine.IsReady, now);
            AnimateBody(press, produce);
            AnimateSpark(idle, hovered, now);
            AnimatePressGlow(Mathf.Max(press, produce), hovered && machine.IsReady);
            AnimateLights(produceT, now);
        }

        void AnimateButton(float press, bool idle, bool highlighted, float now)
        {
            if (!buttonVisual)
                return;
            float breathe = idle ? (Mathf.Sin(now * 5) * .5f + .5f) * .08f : 0;   // "press me"
            float size = 1 + breathe + (highlighted ? .1f : 0) - press * .12f;
            buttonVisual.localPosition = buttonHome + Vector3.down * (press * pressDepth);
            buttonVisual.localScale = new Vector3(size + press * .1f, size, 1);
        }

        void AnimateBody(float press, float produce)
        {
            if (bodyVisual)
                bodyVisual.localScale = new Vector3(1 + press * .05f - produce * .07f, 1 - press * .06f + produce * .13f, 1);
        }

        void AnimateSpark(bool idle, bool hovered, float now)
        {
            if (!readySpark)
                return;
            sparkAlpha = Mathf.MoveTowards(sparkAlpha, idle ? 1 : 0, Time.unscaledDeltaTime * 4);
            float beat = Mathf.Sin(now * 5) * .5f + .5f;
            readySpark.color = new Color(1, 1, 1, sparkAlpha * (.55f + beat * .45f));
            readySpark.transform.localScale = sparkScale * (1 + beat * .12f + (hovered ? .15f : 0));
        }

        void AnimatePressGlow(float flash, bool highlighted)
        {
            if (!pressGlow)
                return;
            pressGlow.color = new Color(1, .85f, .45f, flash * .75f + (highlighted ? .18f : 0));
            pressGlow.transform.localScale = glowScale * (1 + flash * .35f);
        }

        // Lit lamps = current energy. The newest lamp pops; after producing, all lamps flash and fade.
        void AnimateLights(float produceT, float now)
        {
            float pop = Mathf.Clamp01(1 - (now - machine.LastPressTime) / LampPopLength);
            for (int i = 0; i < chargeLights.Length; i++)
            {
                bool lit = i < machine.Energy;
                var color = glowColors != null && i < glowColors.Length ? glowColors[i] : new Color(1, .8f, .4f);
                color.a = lit ? .85f : produceT < 1 ? (1 - produceT) * .9f : 0;
                chargeLights[i].color = color;
                bool newest = lit && i == machine.Energy - 1;
                chargeLights[i].transform.localScale = lightScales[i] * (newest ? 1 + pop * .9f : 1);
            }
        }
    }
}
