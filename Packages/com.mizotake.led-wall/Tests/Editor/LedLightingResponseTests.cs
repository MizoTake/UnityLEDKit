using NUnit.Framework;
using UnityEngine;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedLightingResponseTests
    {
        [Test]
        public void ResponseIsIndependentOfHowElapsedTimeIsSplitIntoFrames()
        {
            var single = LedLightingResponse.InterpolationWeight(0.1f, 0.08f);
            var half = LedLightingResponse.InterpolationWeight(0.05f, 0.08f);
            Assert.That(1f - (1f - half) * (1f - half), Is.EqualTo(single).Within(0.000001f));
            Assert.That(LedLightingResponse.InterpolationWeight(0.016f, 0f), Is.EqualTo(1f));
            Assert.That(LedLightingResponse.InterpolationWeight(-1f, 0.08f), Is.Zero);
        }

        [Test]
        public void CutsResetWholePanelChangesButPreserveSmallMovingDetails()
        {
            var red = new[] { Color.red, Color.red, Color.red, Color.red };
            var blue = new[] { Color.blue, Color.blue, Color.blue, Color.blue };
            var smallChange = new[] { Color.blue, Color.red, Color.red, Color.red };
            Assert.That(LedLightingResponse.IsCut(red, blue, 0.55f), Is.True);
            Assert.That(LedLightingResponse.IsCut(red, smallChange, 0.55f), Is.False);
            Assert.That(LedLightingResponse.IsCut(red, blue, 0f), Is.False);
            Assert.That(LedLightingResponse.IsCut(null, blue, 0.55f), Is.True);
        }

        [Test]
        public void CutDetectionNormalizesHdrInputAndDoesNotClipEmission()
        {
            var first = new[] { new Color(10, 0, 0) };
            Assert.That(LedLightingResponse.IsCut(first, new[] { new Color(9, 0, 0) }, 0.55f), Is.False);
            Assert.That(LedLightingResponse.IsCut(first, new[] { new Color(0, 0, 10) }, 0.55f), Is.True);
        }
    }
}
