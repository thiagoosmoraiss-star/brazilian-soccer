using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Ids;

namespace Game.Match.AI
{
    /// <summary>One side's running match statistics (A7a; TECHNICAL_SPEC §5 "MatchStats").</summary>
    public sealed class AiTeamStats
    {
        public int Shots;
        public int ShotsOnTarget;
        public int Passes;
        public int Tackles;
        public int Saves;
        public int Corners;
        public int ThrowIns;
        public int GoalKicks;
        /// <summary>Steps this side had the ball (or touched it last).</summary>
        public long PossessionSteps;
        // Referee (A8a).
        public int Fouls;
        public int YellowCards;
        public int RedCards;
        public int Offsides;
        public int FreeKicks;
        public int Penalties;
        public int PenaltyGoals;
        public int Crosses, LongBalls, Headers, AerialDuels, HeaderShots, HeaderGoals;
        // Duels (A9b).
        public int Slides, SlidesWon, SlideFouls, BodyDuels, Runs, ThroughBalls, KeeperRushes, Smothers;
        /// <summary>Shots this side's outfield players blocked (F1b).</summary>
        public int ShotBlocks;
        /// <summary>First touches of this side that escaped (D-36).</summary>
        public int Miscontrols;

        internal void Clear()
        {
            Shots = ShotsOnTarget = Passes = Tackles = Saves = Corners = ThrowIns = GoalKicks = 0;
            Fouls = YellowCards = RedCards = Offsides = FreeKicks = Penalties = PenaltyGoals = 0;
            Crosses = LongBalls = Headers = AerialDuels = HeaderShots = HeaderGoals = 0;
            Slides = SlidesWon = SlideFouls = BodyDuels = Runs = ThroughBalls = KeeperRushes = Smothers = ShotBlocks = Miscontrols = 0;
            PossessionSteps = 0;
        }
    }

    public sealed partial class AiMatch
    {
        /// <summary>Recorded events are capped so recording never allocates during the match.</summary>
        public const int MaxEvents = 512;

        public AiTeamStats HomeStats { get; } = new AiTeamStats();
        public AiTeamStats AwayStats { get; } = new AiTeamStats();
        public AiTeamStats Stats(AiTeam team) => team == Home ? HomeStats : AwayStats;
        public IReadOnlyList<MatchEvent> Events => _events;

        private readonly List<MatchEvent> _events = new List<MatchEvent>(MaxEvents);
        private readonly int[] _playerGoals = new int[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _playerAssists = new int[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _playerShots = new int[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _playerShotsOnTarget = new int[2 * MatchTeamSetup.StarterCount];
        /// <summary>Shooter of the shot still in flight (global index), or -1.</summary>
        private int _pendingShot = -1;
        /// <summary>Last passer of the side in possession, credited with the assist if a teammate scores.</summary>
        private int _assistCandidate = -1;

        /// <summary>Possession share of <paramref name="team"/>, 0-100.</summary>
        public float PossessionPercent(AiTeam team)
        {
            long total = HomeStats.PossessionSteps + AwayStats.PossessionSteps;
            return total == 0 ? 50f : 100f * Stats(team).PossessionSteps / total;
        }

        private void RecordEvent(MatchEventType type, MatchSide side, Id player, Id other)
        {
            if (_events.Count < MaxEvents) _events.Add(new MatchEvent(EventMinute, type, side, player, other));
        }

        private int EventMinute => Math.Max(1, Math.Min(TotalDisplayMinutes, (int)ClockMinutes + 1));

        private void OnShot(AiPlayer p)
        {
            Stats(p.Team).Shots++;
            _playerShots[p.Global]++;
            _headerShotInFlight = false;
            _pendingShot = p.Global;
            for (int i = 0; i < _blockTried.Length; i++) _blockTried[i] = false; // a new shot: everybody may block it
            MarkOffside(p.Team, p); // a rebound to an offside teammate is offside
            RecordEvent(MatchEventType.Shot, p.Team.Side, p.Setup.PlayerId, Id.None);
        }

        private void OnShotOnTarget()
        {
            if (_pendingShot < 0) return;
            var shooter = Players[_pendingShot];
            Stats(shooter.Team).ShotsOnTarget++;
            _playerShotsOnTarget[shooter.Global]++;
            RecordEvent(MatchEventType.ShotOnTarget, shooter.Team.Side, shooter.Setup.PlayerId, Id.None);
        }

        private void OnPassed(AiTeam team, AiPlayer passer, int target)
        {
            Stats(team).Passes++;
            MarkOffside(team, passer);
            _assistCandidate = passer.Global;
            team.Receiver = target;
            team.ReceiverPasser = passer.Global;
        }

        /// <summary>The pass's receiver stops being one once the ball is controlled, dead or touched by anybody else.</summary>
        private void ClearReceiver(AiTeam team)
        {
            if (team.Receiver < 0) return;
            if (Ball.State == BallState.Controlled || Ball.State == BallState.Dead || Ball.LastTouch != team.ReceiverPasser) team.Receiver = -1;
        }

        /// <summary>Goal for <paramref name="scorers"/>: the last touch scores (own goal: no scorer), the side's last
        /// passer assists.</summary>
        private void CountGoal(AiTeam scorers)
        {
            scorers.Goals++;
            int last = Ball.LastTouch;
            bool ownGoal = last < 0 || !scorers.Owns(last);
            if (!ownGoal && _pendingShot == last) OnShotOnTarget();
            var scorer = ownGoal ? Id.None : Players[last].Setup.PlayerId;
            var assist = Id.None;
            if (!ownGoal)
            {
                _playerGoals[last]++;
                if (_assistCandidate >= 0 && _assistCandidate != last && scorers.Owns(_assistCandidate))
                {
                    _playerAssists[_assistCandidate]++;
                    assist = Players[_assistCandidate].Setup.PlayerId;
                }
            }
            RecordEvent(MatchEventType.Goal, scorers.Side, scorer, assist);
            _pendingShot = -1;
            _assistCandidate = -1;
        }

        /// <summary>The single output of the match (D-19, TECHNICAL_SPEC §5): goals, events, fouls and cards (A8a),
        /// substitutions (A9c: everybody who played, with his minutes). No ratings or injuries yet.</summary>
        /// <summary>The match's <see cref="MatchResult"/>: player ratings by the formula QuickSim uses, and — for a knockout
        /// match level at the end — the penalty shootout by the shared rules (C1; the visual shootout is post-MVP).
        /// Deterministic: the same match gives the same result however often it is asked.</summary>
        public MatchResult Result()
        {
            var players = RatePlayers(PlayerResults());
            var (homePens, awayPens) = Shootout();
            return new MatchResult(Home.Goals, Away.Goals, _events, TeamResult(Home), TeamResult(Away), players, homePens, awayPens);
        }

        private TeamMatchStats TeamResult(AiTeam team)
        {
            var s = Stats(team);
            return new TeamMatchStats(s.Shots, s.ShotsOnTarget, PossessionPercent(team), s.Fouls, s.YellowCards, s.RedCards, s.Corners, SubstitutionsMade(team.Side));
        }
    }
}
