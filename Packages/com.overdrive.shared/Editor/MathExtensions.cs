using UnityEngine;

namespace Overdrive
{
    public static class MathExtensions
    {
        public const float DefaultDelta = 0.0001f;

        public static bool Approximately(this float a, float b, float delta = DefaultDelta)
        {
            return Mathf.Abs(a - b) < delta;
        }

        public static bool Approximately(this Vector2 a, Vector2 b, float delta = DefaultDelta)
        {
            return Mathf.Abs(a.x - b.x) < delta && Mathf.Abs(a.y - b.y) < delta;
        }
    }
}
