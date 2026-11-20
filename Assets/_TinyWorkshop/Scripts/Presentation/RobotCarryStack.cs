using UnityEngine;
namespace TinyWorkshop
{
    // Shows the gears the robot is carrying, stacked on its back (opposite to the walking direction).
    [RequireComponent(typeof(RobotController2D))]
    public sealed class RobotCarryStack : MonoBehaviour
    {
        const int Capacity = 3;
        const float StackSpacing = .22f;

        public WorkshopSession session;
        [Tooltip("Icons are parented here so they follow the robot's squash and lean.")]
        public Transform anchor;
        public Sprite gearSprite;
        [Tooltip("Back offset from the anchor; x mirrors with facing.")]
        public Vector2 offset = new Vector2(.3f, .2f);
        public float scale = .62f;

        RobotController2D robot;
        SpriteRenderer[] icons;
        int shownCount;
        float backSide = -1;

        void Awake()
        {
            robot = GetComponent<RobotController2D>();
            if (!gearSprite || !anchor)
                return;
            icons = new SpriteRenderer[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                var icon = new GameObject("CarriedGear_" + i);
                icon.transform.SetParent(anchor, false);
                icon.transform.localScale = Vector3.one * scale;
                icons[i] = icon.AddComponent<SpriteRenderer>();
                icons[i].sprite = gearSprite;
                icons[i].sortingOrder = 11;
                icon.SetActive(false);
            }
        }

        void Update()
        {
            if (icons == null)
                return;
            int carried = session ? session.Carried : 0;
            float vx = robot.Velocity.x;
            if (Mathf.Abs(vx) > .1f)
                backSide = vx < 0 ? 1 : -1;
            float speed01 = Mathf.Abs(vx) / robot.Profile.maxSpeed;
            for (int i = 0; i < icons.Length; i++)
            {
                bool visible = i < carried;
                icons[i].gameObject.SetActive(visible);
                if (visible)
                    PlaceIcon(icons[i].transform, i, speed01);
            }
            shownCount = carried;
        }

        void PlaceIcon(Transform icon, int index, float speed01)
        {
            float sway = Mathf.Sin(Time.time * 6 + index) * .02f * (1 + speed01 * 2);
            var target = new Vector3(backSide * offset.x + sway, offset.y + index * StackSpacing, 0);
            bool justAdded = index >= shownCount;
            // A new gear drops onto the stack; existing ones follow smoothly so the stack sways.
            icon.localPosition = justAdded ? target + Vector3.up * .5f : Vector3.Lerp(icon.localPosition, target, Time.deltaTime * 14);
            icon.localRotation = Quaternion.Euler(0, 0, Time.time * 40 * (index % 2 == 0 ? 1 : -1));
        }
    }
}
