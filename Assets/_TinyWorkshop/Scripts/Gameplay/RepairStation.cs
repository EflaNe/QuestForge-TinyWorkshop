using UnityEngine;
namespace TinyWorkshop
{
    public sealed class RepairStation : MonoBehaviour
    {
        public WorkshopSession session;
        public float interactionRange = 1.9f;
        [Tooltip("Optional bubble (e.g. delivery truck) that pops up while the robot carries gears.")]
        public SpriteRenderer callout;
        [Tooltip("Optional check icon that pops above the terminal on each delivered gear.")]
        public SpriteRenderer confirm;
        Vector3 calloutHome, confirmHome;
        float calloutShow, confirmAt = -10;
        public bool IsNear => session.Robot && Vector2.Distance(session.Robot.transform.position, transform.position) < interactionRange;
        Vector3 home;
        float punch;
        SpriteRenderer sprite;
        void Awake()
        {
            home = transform.localScale;
            sprite = GetComponent<SpriteRenderer>();
            if (callout)
                calloutHome = callout.transform.localPosition;
            if (confirm)
            {
                confirmHome = confirm.transform.localPosition;
                confirm.gameObject.SetActive(false);
            }
        }
        void Update()
        {
            punch = Mathf.Lerp(punch, 0, Time.deltaTime * 12);
            bool waiting = session && session.Carried > 0 && IsNear && session.QuestActive;
            float breathe = waiting ? Mathf.Sin(Time.time * 6) * .025f : 0;
            transform.localScale = new Vector3(home.x * (1 + punch * .6f + breathe), home.y * (1 - punch * .4f + breathe), home.z);
            if (sprite && session)
            {
                var target = session.QuestState == "Completed" || session.RewardClaimed ? new Color(.78f, 1, .8f) : Color.white;
                sprite.color = Color.Lerp(sprite.color, target, Time.deltaTime * 4);
            }
            if (callout && session)
            {
                bool want = session.QuestActive && session.Carried > 0 && !IsNear;
                calloutShow = Mathf.MoveTowards(calloutShow, want ? 1 : 0, Time.deltaTime * 5);
                callout.gameObject.SetActive(calloutShow > 0);
                float pop = calloutShow < 1 ? Mathf.SmoothStep(0, 1, calloutShow) : 1;
                callout.transform.localPosition = calloutHome + Vector3.up * Mathf.Sin(Time.time * 3) * .06f;
                callout.transform.localScale = Vector3.one * pop;
            }
            if (confirm)
            {
                float age = Time.time - confirmAt;
                bool on = age < .8f;
                confirm.gameObject.SetActive(on);
                if (on)
                {
                    float pop = age < .12f ? Mathf.Lerp(.3f, 1.25f, age / .12f) : Mathf.Lerp(1.25f, 1, Mathf.Clamp01((age - .12f) / .12f));
                    confirm.transform.localPosition = confirmHome + Vector3.up * age * .5f;
                    confirm.transform.localScale = Vector3.one * pop;
                    confirm.color = new Color(1, 1, 1, Mathf.Clamp01((.8f - age) / .25f));
                }
            }
        }
        // Called as each delivered gear lands in the station.
        public void Punch(float amount = .18f) => punch = Mathf.Max(punch, amount);
        // Clear visual confirmation for one delivered gear.
        public void Confirm()
        {
            confirmAt = Time.time;
            Punch(.22f);
            if (sprite)
                sprite.color = new Color(.75f, 1, .75f);
        }
        public bool Interact()
        {
            if (!IsNear)
                return false;
            session.Deliver();
            return true;
        }
    }
}
