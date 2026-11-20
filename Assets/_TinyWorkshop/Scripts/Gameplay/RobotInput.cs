using UnityEngine;
using UnityEngine.InputSystem;
namespace TinyWorkshop
{
    // Keyboard -> RobotController2D. Replace this component to add touch or gamepad controls;
    // the movement code does not change.
    [RequireComponent(typeof(RobotController2D))]
    public sealed class RobotInput : MonoBehaviour
    {
        RobotController2D robot;

        void Awake() => robot = GetComponent<RobotController2D>();

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            float right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0;
            float left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0;
            robot.Move(right - left);
            if (keyboard.spaceKey.wasPressedThisFrame)
                robot.PressJump();
            if (keyboard.spaceKey.wasReleasedThisFrame)
                robot.ReleaseJump();
        }
    }
}
