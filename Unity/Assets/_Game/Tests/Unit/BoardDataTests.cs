using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Board/board.json: real file loads; broken documents are rejected.</summary>
    public class BoardDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Board", "board.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadBoard(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(20, r.Value.Confidence.DismissalThreshold, "GAME_DESIGN §6: dismissal below 20");
        }

        [Test]
        public void ResetAtOrBelowDismissalThreshold_IsRejected() =>
            Assert.IsFalse(BoardReader.Read(Read().Replace("\"resetAfterDismissal\": 50", "\"resetAfterDismissal\": 10")).IsSuccess);

        [Test]
        public void NegativeObjectivePositions_IsRejected() =>
            Assert.IsFalse(BoardReader.Read(Read().Replace("\"promoteTopPositions\": 4", "\"promoteTopPositions\": 0")).IsSuccess);
    }
}
