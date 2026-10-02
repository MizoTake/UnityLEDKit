using NUnit.Framework;
using UnityEngine;

namespace Mizotake.LedWall.Tests
{
    public sealed class ReflectionMathTests
    {
        [Test]
        public void HorizontalPlaneReflectsAcrossItsActualHeight()
        {
            var matrix = ReflectionMath.CreateReflection(new Vector3(0f, 2f, 0f), Vector3.up);
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(new Vector3(3f, 5f, -4f)), new Vector3(3f, -1f, -4f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void TiltedPlaneReflectionIsAnInvolutionAndPreservesPlanePoints()
        {
            var point = new Vector3(1f, -2f, 3f);
            var normal = new Vector3(1f, 2f, 3f);
            var matrix = ReflectionMath.CreateReflection(point, normal);
            var input = new Vector3(7f, 8f, -3f);
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(matrix.MultiplyPoint(input)), input), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(point), point), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(matrix.MultiplyPoint(input) - point, normal.normalized), Is.EqualTo(-Vector3.Dot(input - point, normal.normalized)).Within(0.0001f));
        }

        [TestCase(1920, 1080, 0.5f, 1024, 960, 540)]
        [TestCase(3840, 2160, 1f, 1024, 1024, 576)]
        [TestCase(0, 0, 0f, 0, 16, 16)]
        [TestCase(1080, 1920, 1f, 960, 540, 960)]
        public void TargetSizeIsBoundedAndPreservesAspect(int width, int height, float scale, int limit, int expectedWidth, int expectedHeight)
        {
            Assert.That(ReflectionMath.GetTargetSize(width, height, scale, limit), Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
        }
    }
}
