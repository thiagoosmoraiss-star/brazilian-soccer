using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Balance/movement.json and fatigue.json: real files load; broken documents are rejected.</summary>
    public class MovementDataTests
    {
        private static string ReadMovement() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "movement.json"));
        private static string ReadFatigue() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "fatigue.json"));

        [Test]
        public void RealMovementFile_Loads()
        {
            var r = GameDataLoader.LoadMovement(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(0.4f, r.Value.PlayerRadius, "GAME_DESIGN §25: jogadores como círculos ~0,4 m");
        }

        [Test]
        public void RealFatigueFile_Loads()
        {
            var r = GameDataLoader.LoadFatigue(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(6f, r.Value.ReferenceDurationMinutes, "GAME_DESIGN §18: taxas definidas para 6 min");
        }

        [Test]
        public void MovementWithMediumBandBelowNoLossBand_IsRejected() =>
            Assert.IsFalse(MovementReader.ReadMovement(ReadMovement().Replace("\"turnMediumLossMaxDegrees\": 90.0", "\"turnMediumLossMaxDegrees\": 10.0")).IsSuccess);

        [Test]
        public void MovementWithNegativeRadius_IsRejected() =>
            Assert.IsFalse(MovementReader.ReadMovement(ReadMovement().Replace("\"playerRadius\": 0.4", "\"playerRadius\": -0.4")).IsSuccess);

        [Test]
        public void FatigueWithZeroThreshold_IsRejected() =>
            Assert.IsFalse(MovementReader.ReadFatigue(ReadFatigue().Replace("\"lowEnergyThreshold\": 0.6", "\"lowEnergyThreshold\": 0")).IsSuccess);

        [Test]
        public void FatigueWithSubOneSprintMultiplier_IsRejected() =>
            Assert.IsFalse(MovementReader.ReadFatigue(ReadFatigue().Replace("\"sprintCostMultiplier\": 5.5", "\"sprintCostMultiplier\": 0.5")).IsSuccess);
    }
}
