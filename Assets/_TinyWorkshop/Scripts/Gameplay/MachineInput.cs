using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace TinyWorkshop
{
    // One-button lab: SPACE presses the machine. Main game: left-click on the machine.
    [RequireComponent(typeof(WorkshopMachine))]
    public sealed class MachineInput : MonoBehaviour
    {
        [Tooltip("Main game: clicking inside this area presses the machine.")]
        public BoxCollider2D clickArea;

        public bool Hovered { get; private set; }

        WorkshopMachine machine;

        void Awake() => machine = GetComponent<WorkshopMachine>();

        void Update()
        {
            var session = machine.session;
            if (session.mode == WorkshopMode.OneButton)
                HandleSpace(session);
            else if (session.mode == WorkshopMode.MainGame)
                HandleMouse(session);
        }

        void HandleSpace(WorkshopSession session)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
                return;
            if (session.DemoComplete)
                session.Restart();   // lab finished: SPACE plays it again
            else
                machine.TryPress();
        }

        void HandleMouse(WorkshopSession session)
        {
            var mouse = Mouse.current;
            Hovered = mouse != null && !PointerOverUi() && clickArea.OverlapPoint(session.worldCamera.ScreenToWorldPoint(mouse.position.ReadValue()));
            if (Hovered && mouse.leftButton.wasPressedThisFrame)
                machine.TryPress();
        }

        static bool PointerOverUi() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }
}
