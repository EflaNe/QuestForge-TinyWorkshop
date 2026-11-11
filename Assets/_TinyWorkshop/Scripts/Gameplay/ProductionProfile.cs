using UnityEngine;
namespace TinyWorkshop
{
    [CreateAssetMenu(menuName = "Tiny Workshop/Production Profile")]
    public sealed class ProductionProfile : ScriptableObject
    {
        public int energyPerPress = 1, threshold = 3;
        public float pressDuration = .10f, cooldown = .12f;
    }
}
