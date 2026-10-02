using UnityEngine;

namespace Mizotake.LedWall
{
    /// <summary>Frame-rate independent response and a normalized, whole-panel cut metric.</summary>
    public static class LedLightingResponse
    {
        public static float InterpolationWeight(float deltaTime, float responseSeconds) => responseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / responseSeconds);

        public static bool IsCut(Color[] previous, Color[] next, float threshold)
        {
            if (previous == null || next == null || previous.Length != next.Length) return true;
            if (threshold <= 0f || previous.Length == 0) return false;
            var difference = 0f;
            var peak = 1f;
            for (var index = 0; index < next.Length; index++)
            {
                var a = previous[index];
                var b = next[index];
                difference += Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
                peak = Mathf.Max(peak, Mathf.Max(a.maxColorComponent, b.maxColorComponent));
            }
            return difference / (previous.Length * peak) >= threshold;
        }
    }
}
