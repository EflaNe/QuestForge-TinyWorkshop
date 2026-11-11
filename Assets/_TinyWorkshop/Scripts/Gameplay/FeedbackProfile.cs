using UnityEngine;
namespace TinyWorkshop
{
    [CreateAssetMenu(menuName = "Tiny Workshop/Feedback Profile")]
    public sealed class FeedbackProfile : ScriptableObject
    {
        [Range(0, 1)] public float effectsVolume = .7f, musicVolume = .24f;
        [Range(0, .2f)] public float shakeStrength = .035f;
        public float pickupPunch = .15f, landingPunch = .18f;
    }
}
