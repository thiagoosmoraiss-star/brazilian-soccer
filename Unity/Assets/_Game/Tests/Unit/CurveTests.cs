using Game.Data.Effects;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class CurveTests
    {
        private static PiecewiseLinearCurve Curve(params float[] xy)
        {
            var pts = new CurvePoint[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new CurvePoint(xy[2 * i], xy[2 * i + 1]);
            return new PiecewiseLinearCurve(pts);
        }

        [Test]
        public void Interpolates_Linearly_BetweenPoints()
        {
            var c = Curve(0f, 0f, 10f, 100f, 20f, 50f);
            Assert.AreEqual(0f, c.Evaluate(0f), 1e-5f);
            Assert.AreEqual(50f, c.Evaluate(5f), 1e-5f);
            Assert.AreEqual(100f, c.Evaluate(10f), 1e-5f);
            Assert.AreEqual(75f, c.Evaluate(15f), 1e-5f);
            Assert.AreEqual(50f, c.Evaluate(20f), 1e-5f);
        }

        [Test]
        public void ClampsToEndpoints_OutsideDomain()
        {
            var c = Curve(1f, 2f, 99f, 8f);
            Assert.AreEqual(2f, c.Evaluate(-50f));
            Assert.AreEqual(2f, c.Evaluate(1f));
            Assert.AreEqual(8f, c.Evaluate(99f));
            Assert.AreEqual(8f, c.Evaluate(500f));
        }

        [Test]
        public void DeclaredMonotoneCurve_NeverInvertsWhenSampled()
        {
            var c = Curve(1f, 0f, 30f, 10f, 60f, 10f, 99f, 40f);
            float prev = c.Evaluate(1f);
            for (float x = 1f; x <= 99f; x += 0.25f)
            {
                float y = c.Evaluate(x);
                Assert.GreaterOrEqual(y, prev, $"x={x}");
                prev = y;
            }
        }
    }
}
