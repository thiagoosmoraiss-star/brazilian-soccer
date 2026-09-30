using System.Linq;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Rules.Ovr;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class OvrTests
    {
        private static Data.Ovr.OvrDefinition RealOvr()
        {
            var result = GameDataLoader.LoadOvr(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(result.IsSuccess, result.ToString());
            return result.Value;
        }

        private static int[] All(int v) => Enumerable.Repeat(v, AttrInfo.Count).ToArray();

        [Test]
        public void RealOvrJson_Loads_WithWeightsForEveryPosition()
        {
            var ovr = RealOvr();
            for (int p = 0; p < PositionInfo.Count; p++)
            {
                float sum = 0f;
                for (int a = 0; a < AttrInfo.Count; a++) sum += ovr.Weight((Position)p, (Attr)a);
                Assert.AreEqual(1f, sum, 1e-4f, ((Position)p).ToString());
            }
        }

        [Test]
        public void Rating_IsTheWeightedAverage()
        {
            var ovr = RealOvr();
            foreach (Position p in System.Enum.GetValues(typeof(Position)))
                Assert.AreEqual(60, OvrCalculator.Rating(ovr, All(60), p), p.ToString());

            // Goalkeeper rating is driven by goalkeeping attributes.
            var keeper = All(30);
            keeper[(int)Attr.GkReflexes] = keeper[(int)Attr.GkPositioning] = keeper[(int)Attr.GkHandling] = keeper[(int)Attr.GkAerial] = 80;
            Assert.Greater(OvrCalculator.Rating(ovr, keeper, Position.GOL), 70);
            Assert.Less(OvrCalculator.Rating(ovr, keeper, Position.ATA), 40);
        }

        [Test]
        public void Effective_AppliesSecondaryAndOutOfPositionFactors()
        {
            var ovr = RealOvr();
            var attrs = All(80);
            var secondary = new[] { Position.VOL };
            int main = OvrCalculator.Effective(ovr, attrs, Position.MC, secondary, Position.MC);
            int sec = OvrCalculator.Effective(ovr, attrs, Position.MC, secondary, Position.VOL);
            int outOf = OvrCalculator.Effective(ovr, attrs, Position.MC, secondary, Position.ZAG);
            Assert.AreEqual(80, main);
            Assert.AreEqual((int)System.Math.Round(80 * ovr.SecondaryPositionFactor, System.MidpointRounding.AwayFromZero), sec);
            Assert.AreEqual((int)System.Math.Round(80 * ovr.OutOfPositionFactor, System.MidpointRounding.AwayFromZero), outOf);
            Assert.Greater(main, sec);
            Assert.Greater(sec, outOf);
        }

        [TestCase("{\"schemaVersion\":1,\"secondaryPositionFactor\":0.97,\"outOfPositionFactor\":0.9,\"positions\":[]}", TestName = "Ovr_MissingPositions_IsRejected")]
        [TestCase("{\"schemaVersion\":1,\"secondaryPositionFactor\":0.9,\"outOfPositionFactor\":0.97,\"positions\":[]}", TestName = "Ovr_FactorsInverted_IsRejected")]
        [TestCase("{\"schemaVersion\":1,\"secondaryPositionFactor\":0.97,\"outOfPositionFactor\":0.9,\"positions\":[],\"x\":1}", TestName = "Ovr_UnknownProperty_IsRejected")]
        [TestCase("{\"schemaVersion\":1,\"secondaryPositionFactor\":0.97,\"outOfPositionFactor\":0.9,\"positions\":[{\"position\":\"GK\",\"weights\":[]}]}", TestName = "Ovr_UnknownPosition_IsRejected")]
        public void InvalidOvrJson_IsRejected(string json)
        {
            Assert.IsFalse(OvrJsonReader.Read(json).IsSuccess);
        }
    }
}
