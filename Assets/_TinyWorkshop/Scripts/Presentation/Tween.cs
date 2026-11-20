using UnityEngine;
namespace TinyWorkshop
{
    // Small easing helpers shared by the cosmetic scripts.
    public static class Tween
    {
        // "Pop in": grows from `from` to `peak` in `rise` seconds, then settles to 1 in `settle` seconds.
        public static float Pop(float age, float from, float peak, float rise, float settle)
        {
            if (age < rise)
                return Mathf.Lerp(from, peak, age / rise);
            return Mathf.Lerp(peak, 1, Mathf.Clamp01((age - rise) / settle));
        }
    }
}
