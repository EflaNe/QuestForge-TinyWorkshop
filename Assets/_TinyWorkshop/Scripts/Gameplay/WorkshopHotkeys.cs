using UnityEngine;
using UnityEngine.InputSystem;
namespace TinyWorkshop
{
    // Scene-wide keyboard shortcuts. Robot and machine controls live in RobotInput / MachineInput.
    [RequireComponent(typeof(WorkshopSession))]
    public sealed class WorkshopHotkeys : MonoBehaviour
    {
        WorkshopSession session;

        void Awake() => session = GetComponent<WorkshopSession>();

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            HandleGlobalKeys(keyboard);
            switch (session.mode)
            {
                case WorkshopMode.OneButton:
                    HandleProfileKeys(keyboard);
                    break;
                case WorkshopMode.Movement:
                    HandleProfileKeys(keyboard);
                    if (keyboard.gKey.wasPressedThisFrame)
                        session.ToggleAssistance();
                    break;
                case WorkshopMode.Physics:
                    if (keyboard.tKey.wasPressedThisFrame)
                        session.TogglePhysics();
                    if (keyboard.spaceKey.wasPressedThisFrame)
                        session.Launch();
                    break;
                case WorkshopMode.MainGame:
                    if (keyboard.eKey.wasPressedThisFrame)
                        session.Interact();
                    break;
            }
        }

        // F1-F4 scenes, R restart, M mute, V screen shake, Tab / Esc menu.
        void HandleGlobalKeys(Keyboard keyboard)
        {
            if (keyboard.f1Key.wasPressedThisFrame)
                session.OpenScene(0);
            if (keyboard.f2Key.wasPressedThisFrame)
                session.OpenScene(1);
            if (keyboard.f3Key.wasPressedThisFrame)
                session.OpenScene(2);
            if (keyboard.f4Key.wasPressedThisFrame)
                session.OpenScene(3);
            if (keyboard.rKey.wasPressedThisFrame && session.mode != WorkshopMode.OneButton)
                session.Restart();
            if (keyboard.mKey.wasPressedThisFrame)
                session.Feedback.ToggleMute();
            if (keyboard.vKey.wasPressedThisFrame)
                session.Feedback.ToggleShake();
            if (keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                session.Hud.ToggleMenu();
        }

        void HandleProfileKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                session.SelectProfile(0);
            if (keyboard.digit2Key.wasPressedThisFrame)
                session.SelectProfile(1);
            if (keyboard.digit3Key.wasPressedThisFrame)
                session.SelectProfile(2);
        }
    }
}
