using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
namespace TinyWorkshop
{
    public sealed class WorkshopMachine : MonoBehaviour
    {
        public WorkshopSession session;
        public ProductionProfile[] profiles;
        public Transform buttonVisual, bodyVisual, outlet;
        public BoxCollider2D clickArea;
        public SpriteRenderer[] chargeLights;
        public float interactionRange = 2.4f;
        [Header("Main game polish (optional)")]
        [Tooltip("Charge lights are soft glows over painted lamps: unlit = invisible, lit = their colour.")]
        public bool glowLights;
        public Color[] glowColors;
        [Tooltip("\"Press me\" marks next to the button, shown while producing is possible.")]
        public SpriteRenderer readySpark;
        [Tooltip("Flash behind the button on each press.")]
        public SpriteRenderer pressGlow;
        public float pressDepth = .14f;
        public int ProfileIndex
        {
            get; private set;
        }
        public int Energy
        {
            get; private set;
        }
        public bool IsNear => !session.Robot || Vector2.Distance(session.Robot.transform.position, transform.position) < interactionRange;
        float nextPress, pressedAt = -10;
        Vector3 buttonHome;
        Vector3[] lightScales;
        public ProductionProfile Settings => profiles[ProfileIndex];
        void Awake()
        {
            if (buttonVisual)
                buttonHome = buttonVisual.localPosition;
            lightScales = new Vector3[chargeLights.Length];
            for (int i = 0; i < chargeLights.Length; i++)
                lightScales[i] = chargeLights[i].transform.localScale;
            if (readySpark)
                sparkScale = readySpark.transform.localScale;
            if (pressGlow)
                glowScale = pressGlow.transform.localScale;
        }
        float producedAt = -10, lightPopAt = -10, sparkAlpha;
        Vector3 sparkScale, glowScale;
        int lastLit;
        public bool Hovered
        {
            get; private set;
        }
        void Update()
        {
            var k = Keyboard.current;
            bool pressed = session.mode == WorkshopMode.OneButton && k != null && k.spaceKey.wasPressedThisFrame;
            Hovered = false;
            if (session.mode == WorkshopMode.MainGame && Mouse.current != null)
            {
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                var point = session.worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                Hovered = !overUi && clickArea.OverlapPoint(point);
                if (Hovered && Mouse.current.leftButton.wasPressedThisFrame)
                    pressed = true;
            }
            if (pressed)
            {
                if (session.mode == WorkshopMode.OneButton && session.DemoComplete)
                {
                    session.Restart();
                    return;
                }
                TryPress();
            }
            Animate();
        }
        void Animate()
        {
            // Unscaled time keeps the press animation alive during hitstop, so the freeze reads as "impact".
            float now = Time.unscaledTime;
            float t = (now - pressedAt) / Mathf.Max(.08f, Settings.pressDuration * 1.6f);
            float pulse = t < 1 ? Mathf.Sin(t * Mathf.PI) * (1 - t * .4f) : 0;
            float p = (now - producedAt) / .35f;
            float big = p < 1 ? Mathf.Sin(p * Mathf.PI) * (1 - p) : 0;
            bool ready = session.mode != WorkshopMode.MainGame || (session.QuestActive && IsNear && session.Produced < 3 && session.WorldGearCount < 3);
            // Idle "press me" breathing on the button when the player can actually use it.
            float idle = ready && t >= 1 ? (Mathf.Sin(now * 5) * .5f + .5f) * .08f : 0;
            if (buttonVisual)
            {
                buttonVisual.localPosition = buttonHome + Vector3.down * (pulse * pressDepth);
                float bs = 1 + idle + (Hovered && ready ? .1f : 0) - pulse * .12f;
                buttonVisual.localScale = new Vector3(bs + pulse * .1f, bs, 1);
            }
            if (bodyVisual)
                bodyVisual.localScale = new Vector3(1 + pulse * .05f - big * .07f, 1 - pulse * .06f + big * .13f, 1);
            float pop = Mathf.Clamp01(1 - (now - lightPopAt) / .2f);
            if (readySpark)
            {
                sparkAlpha = Mathf.MoveTowards(sparkAlpha, ready && t >= 1 ? 1 : 0, Time.unscaledDeltaTime * 4);
                float beat = Mathf.Sin(now * 5) * .5f + .5f;
                readySpark.color = new Color(1, 1, 1, sparkAlpha * (.55f + beat * .45f));
                readySpark.transform.localScale = sparkScale * (1 + beat * .12f + (Hovered ? .15f : 0));
            }
            if (pressGlow)
            {
                float flash = Mathf.Max(pulse, big);
                pressGlow.color = new Color(1, .85f, .45f, flash * .75f + (Hovered && ready ? .18f : 0));
                pressGlow.transform.localScale = glowScale * (1 + flash * .35f);
            }
            for (int i = 0; i < chargeLights.Length; i++)
            {
                if (glowLights)
                {
                    bool on = i < Energy;
                    var c = glowColors != null && i < glowColors.Length ? glowColors[i] : new Color(1, .8f, .4f);
                    float a = on ? .85f : p < 1 ? (1 - p) * .9f : 0;
                    chargeLights[i].color = new Color(c.r, c.g, c.b, a);
                    float gs = on && i == lastLit - 1 ? 1 + pop * .9f : 1;
                    chargeLights[i].transform.localScale = lightScales[i] * gs;
                    continue;
                }
                bool lit = i < Energy;
                chargeLights[i].color = lit ? new Color(1, .78f, .35f) : p < 1 ? Color.Lerp(new Color(1, .95f, .7f), new Color(.24f, .33f, .31f), p) : new Color(.24f, .33f, .31f);
                float s = lit && i == lastLit - 1 ? 1 + pop * .8f : 1;
                chargeLights[i].transform.localScale = lightScales[i] * s;
            }
        }
        public bool TryPress()
        {
            if (Time.unscaledTime < nextPress)
                return false;
            if (session.mode == WorkshopMode.MainGame && !session.QuestActive)
            {
                session.Deny(session.QuestState == "Locked" ? "Önce E ile vardiyayı başlat." : "Üretim tamamlandı.");
                return false;
            }
            if (!IsNear)
            {
                session.Deny("Makineye yaklaş, sonra tıkla.");
                return false;
            }
            if (session.Produced >= 3)
            {
                session.Notify("Üretim tamam! Dişlileri tamir istasyonuna götür.");
                return false;
            }
            if (session.WorldGearCount >= 3)
            {
                session.Deny("Önce yerdeki dişlileri topla.");
                return false;
            }
            nextPress = Time.unscaledTime + Settings.cooldown;
            pressedAt = Time.unscaledTime;
            Energy += Settings.energyPerPress;
            lastLit = Mathf.Min(Energy, chargeLights.Length);
            lightPopAt = Time.unscaledTime;
            // Rising pitch: each press audibly "charges" the machine.
            session.Feedback.Play("press", 1, .9f + Energy * .12f);
            session.Feedback.Shake(.035f);
            session.Feedback.Burst(buttonVisual ? (Vector2)buttonVisual.position : (Vector2)transform.position, 4, new Color(1, .8f, .45f));
            if (Energy >= Settings.threshold)
            {
                Energy = 0;
                producedAt = Time.unscaledTime;
                var velocity = session.mode == WorkshopMode.MainGame ? session.DropVelocity(outlet.position) : new Vector2(3.5f + (session.Produced % 3) * .6f, 6);
                session.Produce(outlet.position, velocity);
                session.Feedback.Play("produce");
                session.Feedback.Burst(outlet.position, 18, new Color(1, .73f, .32f));
                session.Feedback.Puff(outlet.position, 7);
                session.Feedback.Shake(.12f);
                session.Feedback.Hitstop(.07f);
            }
            return true;
        }
        public void SelectProfile(int index)
        {
            ProfileIndex = Mathf.Clamp(index, 0, profiles.Length - 1);
            Energy = 0;
        }
    }
}
