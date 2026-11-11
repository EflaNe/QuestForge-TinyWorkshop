using UnityEngine;
namespace TinyWorkshop
{
    [CreateAssetMenu(menuName = "Tiny Workshop/Movement Profile")]
    public sealed class MovementProfile : ScriptableObject
    {
        public float maxSpeed = 5.4f, acceleration = 45, braking = 65, jumpSpeed = 10.5f, gravity = 3.5f;
        public float coyoteTime = .12f, jumpBuffer = .12f, jumpCut = .5f, fallMultiplier = 1.4f;
    }
}
