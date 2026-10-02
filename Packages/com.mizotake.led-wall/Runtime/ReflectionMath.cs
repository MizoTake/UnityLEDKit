using UnityEngine;

namespace Mizotake.LedWall
{
    public static class ReflectionMath
    {
        public static Matrix4x4 CreateReflection(Vector3 point, Vector3 normal)
        {
            normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            var distance = -Vector3.Dot(normal, point);
            var result = Matrix4x4.identity;
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++) result[row, column] -= 2f * normal[row] * normal[column];
                result[row, 3] = -2f * distance * normal[row];
            }
            return result;
        }

        public static Vector2Int GetTargetSize(int width, int height, float scale, int maximumSize)
        {
            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);
            maximumSize = Mathf.Max(16, maximumSize);
            scale = Mathf.Clamp(scale, 0.05f, 1f);
            var factor = Mathf.Min(scale, (float)maximumSize / Mathf.Max(width, height));
            return new Vector2Int(Mathf.Max(16, Mathf.RoundToInt(width * factor)), Mathf.Max(16, Mathf.RoundToInt(height * factor)));
        }
    }
}
