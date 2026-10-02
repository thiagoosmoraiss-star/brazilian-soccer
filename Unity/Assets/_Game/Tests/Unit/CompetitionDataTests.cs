using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Competitions: real files load; broken documents are rejected.</summary>
    public class CompetitionDataTests
    {
        private static string Read(string file) => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Competitions", file));

        [Test]
        public void RealFiles_Load()
        {
            var source = new DirectoryDataSource(TestPaths.DataRoot());
            var c = GameDataLoader.LoadCompetitions(source);
            Assert.IsTrue(c.IsSuccess, c.ToString());
            Assert.AreEqual(4, c.Value.League.Promoted, "X-39");
            Assert.AreEqual(8, c.Value.Cup.QualifiersPerDivision, "X-40");
            var cal = GameDataLoader.LoadCalendar(source);
            Assert.IsTrue(cal.IsSuccess, cal.ToString());
        }

        [Test]
        public void PromotedDifferentFromRelegated_IsRejected() =>
            Assert.IsFalse(CompetitionReaders.ReadCompetitions(Read("competitions.json").Replace("\"relegated\": 4", "\"relegated\": 3")).IsSuccess);

        [Test]
        public void CupSizeNotPowerOfTwo_IsRejected() =>
            Assert.IsFalse(CompetitionReaders.ReadCompetitions(Read("competitions.json").Replace("\"clubs\": 32", "\"clubs\": 30")).IsSuccess);

        [Test]
        public void TiebreakersNotEndingWithDraw_IsRejected() =>
            Assert.IsFalse(CompetitionReaders.ReadCompetitions(Read("competitions.json").Replace(",\n      \"Draw\"", "")).IsSuccess);

        [Test]
        public void UnknownWeekday_IsRejected() =>
            Assert.IsFalse(CompetitionReaders.ReadCalendar(Read("calendar.json").Replace("\"Saturday\"", "\"Sabado\"")).IsSuccess);

        [Test]
        public void LeagueIntervalBelowMinimum_IsRejected() =>
            Assert.IsFalse(CompetitionReaders.ReadCalendar(Read("calendar.json").Replace("\"leagueIntervalDays\": 7", "\"leagueIntervalDays\": 2")).IsSuccess);
    }
}
