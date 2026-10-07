using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Shared fixtures for the A3 pass/shot tests.</summary>
    internal static class KickTestSupport
    {
        public const float Dt = 1f / 50f;

        private static GameDatabase _db;
        private static JObject _ranges;

        /// <summary>A3 acceptance ranges (Data/TestRanges/kicking.json; TEST_PLAN: ranges live in data, not in test code).</summary>
        public static JObject Ranges() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "kicking.json"))));
        public static GameDatabase Db() => _db ?? (_db = Load());

        private static GameDatabase Load()
        {
            var r = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        /// <summary>A player with every attribute at <paramref name="all"/>, then the given overrides.</summary>
        public static MatchPlayerSetup Player(int all = 70, bool leftFooted = false, int weakFoot = 5, float energy = 100f,
            params (Attr attr, int value)[] overrides)
        {
            var a = Enumerable.Repeat(all, AttrInfo.Count).ToArray();
            foreach (var (attr, value) in overrides) a[(int)attr] = value;
            return new MatchPlayerSetup(new Id(1), a, 0, new int[0], energy, 3, null, leftFooted, weakFoot);
        }

        public static PracticeSession Session(ulong seed, MatchPlayerSetup[] players, Vector3[] positions)
        {
            var db = Db();
            return new PracticeSession(db.Balance, Pitch.From(db.Ball.Pitch), db.Ball.Ball, db.Movement, db.Fatigue, db.Kicking,
                players, positions, seed);
        }

        /// <summary>Steps with no input until the ball is owned or dead, or <paramref name="seconds"/> pass.</summary>
        public static PracticeEvent RunUntilSettled(PracticeSession s, float seconds)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                var ev = s.Step(Vector2.Zero, false, ActionCommand.None, Vector2.Zero, Dt);
                if (ev == PracticeEvent.Goal || ev == PracticeEvent.Out || ev == PracticeEvent.Captured) return ev;
            }
            return PracticeEvent.None;
        }

        /// <summary>Lets the dribble settle the ball at the kicker's feet (one step) so the kick starts from a controlled ball.</summary>
        public static void TakeControl(PracticeSession s)
        {
            for (int i = 0; i < 5 && s.Ball.State != BallState.Controlled; i++)
                s.Step(Vector2.Zero, false, ActionCommand.None, Vector2.Zero, Dt);
            Assert.AreEqual(BallState.Controlled, s.Ball.State, "the drill starts with the ball at the first player's feet.");
        }
    }
}
