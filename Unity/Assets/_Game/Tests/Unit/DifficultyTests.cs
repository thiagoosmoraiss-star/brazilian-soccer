using System;
using System.IO;
using System.Linq;
using Game.Core.Contracts.Match;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>F1c (D-23): the levels in data, the reference level is the current AI exactly, a level acts on the user's
    /// opponent only. The monotonicity batches are in Tests/Simulation/DifficultyTests.</summary>
    public class DifficultyTests
    {
        private const float Dt = 1f / 50f;

        private static JObject Ranges() =>
            JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "difficulty.json")));

        private static MatchSetup WithLevel(MatchSetup s, string level, MatchSide user = MatchSide.Home) =>
            s.WithDifficulty(new MatchDifficulty(level, user));

        [Test]
        public void DifficultyFile_HasEasyNormalHard_WithNormalTheReferenceAndTheDefault()
        {
            var d = Db().Difficulty;
            CollectionAssert.AreEqual(new[] { "easy", "normal", "hard" }, d.Levels.Select(l => l.Id).ToArray(), "D-23: Fácil, Normal e Difícil");
            Assert.AreEqual("normal", d.ReferenceLevel, "D-23a");
            Assert.AreEqual("normal", d.DefaultLevel);
            Assert.IsTrue(d.Level("normal").IsNeutral);
        }

        [Test]
        public void DifficultyFile_RejectsUnknownKeysAndBadValues()
        {
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "difficulty.json"));
            Assert.IsTrue(DifficultyReader.Read(json).IsSuccess);
            JObject J() => JObject.Parse(json);
            var j = J(); j["levels"][0]["match"]["extra"] = 1;
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "unknown key");
            j = J(); j["levels"][1]["match"]["decisionNoiseScale"] = 1.1;
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "the reference level must change nothing");
            j = J(); j["levels"][1]["quickSim"]["opponentStrengthScale"] = 0.9;
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "the reference level must change nothing in the QuickSim either");
            j = J(); j["default"] = "nightmare";
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "unknown default level");
            j = J(); j["levels"][2]["id"] = "easy";
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "duplicate id");
            j = J(); j["levels"][0]["match"]["reactionScale"] = -0.5;
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "negative factor");
            j = J(); j["levels"][0]["match"]["decisionIntervalScale"] = 0;
            Assert.IsFalse(DifficultyReader.Read(j.ToString()).IsSuccess, "zero decision interval");
        }

        [Test]
        public void TheReferenceLevel_ReproducesTheCurrentAi_WithTheSameSeed()
        {
            foreach (ulong seed in Ranges()["reference"]["seeds"].Values<ulong>())
                foreach (var user in new[] { MatchSide.Home, MatchSide.Away })
                {
                    var setup = AiMatchTests.Setup(seed);
                    var none = new AiMatch(Db(), setup, Dt);
                    var reference = new AiMatch(Db(), WithLevel(setup, Db().Difficulty.ReferenceLevel, user), Dt);
                    none.RunToEnd();
                    reference.RunToEnd();
                    CollectionAssert.AreEqual(none.Snapshot(), reference.Snapshot(), $"seed {seed}, user {user}: the whole match state");
                }
            // The QuickSim side of this is checked on the whole batch (Tests/Simulation/DifficultyTests).
        }

        [Test]
        public void ALevel_ActsOnTheUsersOpponentOnly()
        {
            var setup = AiMatchTests.Setup(1);
            var m = new AiMatch(Db(), WithLevel(setup, "hard", MatchSide.Home), Dt);
            Assert.AreSame(DifficultyLevelDefinition.Neutral, m.Home.Difficulty, "the user's own AI teammates stay at the reference");
            Assert.AreEqual("hard", m.Away.Difficulty.Id);
            m = new AiMatch(Db(), WithLevel(setup, "easy", MatchSide.Away), Dt);
            Assert.AreEqual("easy", m.Home.Difficulty.Id);
            Assert.AreSame(DifficultyLevelDefinition.Neutral, m.Away.Difficulty);
            m = new AiMatch(Db(), setup, Dt);
            Assert.AreSame(DifficultyLevelDefinition.Neutral, m.Home.Difficulty, "no difficulty: AI × AI, calibration");
            Assert.AreSame(DifficultyLevelDefinition.Neutral, m.Away.Difficulty);
        }

        [Test]
        public void ALevel_ChangesDecisionsAndReactions_NeverAttributes()
        {
            var setup = AiMatchTests.Setup(1);
            var hard = new AiMatch(Db(), WithLevel(setup, "hard"), Dt);
            var easy = new AiMatch(Db(), WithLevel(setup, "easy"), Dt);
            for (int i = 0; i < hard.Away.Players.Length; i++)
                Assert.IsTrue(setup.Away.Starters[i].Attributes.SequenceEqual(hard.Away.Players[i].Setup.Attributes)
                              && setup.Away.Starters[i].Attributes.SequenceEqual(easy.Away.Players[i].Setup.Attributes), "GAME_DESIGN §24: nunca atributos");
            var h = hard.Away.Difficulty;
            var e = easy.Away.Difficulty;
            Assert.Less(h.DecisionNoiseScale, e.DecisionNoiseScale);
            Assert.Less(h.DecisionIntervalScale, e.DecisionIntervalScale);
            Assert.Less(h.ReactionScale, e.ReactionScale);
            Assert.Less(h.PositionErrorScale, e.PositionErrorScale);
            Assert.Less(h.KeeperReactionScale, e.KeeperReactionScale);
            Assert.Greater(h.QuickSimStrengthScale, e.QuickSimStrengthScale);
        }

        [Test]
        public void AnUnknownLevel_IsRejected()
        {
            var setup = WithLevel(AiMatchTests.Setup(1), "nightmare");
            Assert.Throws<ArgumentException>(() => new AiMatch(Db(), setup, Dt));
        }

        [Test]
        public void ASetup_KeepsItsDifficulty_WhenTheDurationChanges()
        {
            var setup = WithLevel(AiMatchTests.Setup(1), "hard", MatchSide.Away);
            var other = setup.WithDuration(setup.DurationMinutes + 1);
            Assert.AreEqual("hard", other.Difficulty.LevelId);
            Assert.AreEqual(MatchSide.Away, other.Difficulty.UserSide);
            Assert.AreEqual(MatchSide.Home, other.Difficulty.OpponentSide);
            Assert.AreSame(setup, setup.WithDuration(setup.DurationMinutes));
        }
    }
}
