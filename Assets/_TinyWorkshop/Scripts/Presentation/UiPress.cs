using UnityEngine;
using UnityEngine.EventSystems;
namespace TinyWorkshop
{
    // Tactile UI buttons: grow a little on hover, squash on press, spring back on release.
    public sealed class UiPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.06f, pressScale = .9f;
        float target = 1, current = 1, velocity;
        bool hover, down;
        void OnDisable()
        {
            hover = down = false;
            target = current = 1;
            transform.localScale = Vector3.one;
        }
        public void OnPointerEnter(PointerEventData e)
        {
            hover = true;
            Retarget();
        }
        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            down = false;
            Retarget();
        }
        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            Retarget();
        }
        public void OnPointerUp(PointerEventData e)
        {
            down = false;
            velocity += 6;   // overshoot = springy release
            Retarget();
        }
        void Retarget() => target = down ? pressScale : hover ? hoverScale : 1;
        void Update()
        {
            // critically-underdamped spring, unscaled so it works during hitstop / pause
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            velocity += (target - current) * 260 * dt;
            velocity *= Mathf.Exp(-18 * dt);
            current += velocity * dt;
            transform.localScale = Vector3.one * current;
        }
    }
}
