using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Balance/ball.json: real file loads; broken documents are rejected.</summary>
    public class BallDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "ball.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadBall(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(105.0f, r.Value.Pitch.Length, "GAME_DESIGN §2: 105x68 m inicial");
            Assert.AreEqual(68.0f, r.Value.Pitch.Width);
            Assert.Less(r.Value.Pitch.GoalWidth, r.Value.Pitch.Width);
        }

        [Test]
        public void GoalWiderThanPitch_IsRejected() =>
            Assert.IsFalse(BallReader.Read(Read().Replace("\"goalWidth\": 7.32", "\"goalWidth\": 680.0")).IsSuccess);

        [Test]
        public void BounceRestitutionAboveOne_IsRejected() =>
            Assert.IsFalse(BallReader.Read(Read().Replace("\"bounceVerticalRestitution\": 0.52", "\"bounceVerticalRestitution\": 1.5")).IsSuccess);

        [Test]
        public void NegativeRadius_IsRejected() =>
            Assert.IsFalse(BallReader.Read(Read().Replace("\"radius\": 0.11", "\"radius\": -0.11")).IsSuccess);
    }
}
