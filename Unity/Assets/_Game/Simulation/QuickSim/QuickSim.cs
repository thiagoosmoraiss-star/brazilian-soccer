using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Simulation.QuickSim
{
    /// <summary>
    /// Statistical match resolution (TECHNICAL_SPEC §10): MatchSetup -> MatchResult in milliseconds, by one-minute
    /// possession slices. Uses the shared Rules (sector strength, condition, fatigue, discipline, injuries, ratings,
    /// lineup/substitution AI) and quicksim.json coefficients. Deterministic per setup seed; independent RNG streams
    /// per subsystem. Does not depend on Match (D-19).
    /// </summary>
    public sealed class QuickSim
    {
        public const string InvalidSetup = "INVALID_MATCH_SETUP";

        private readonly GameDatabase _db;
        private readonly MatchRules _rules;
        private QuickSimDefinition Q => _db.QuickSim;

        public QuickSim(GameDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _rules = new MatchRules(db);
        }

        public MatchRules Rules => _rules;

        public MatchResult Simulate(MatchSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            var run = new Run(this, setup);
            return run.Play();
        }

        // ------------------------------------------------------------------------------------------------

        private sealed class PlayerAcc
        {
            public MatchPlayerSetup Setup;
            public MatchSide Side;
            public bool Started;
            public int MinuteOn = -1;
            public int MinuteOff = -1;
            public int Goals, Assists, Shots, ShotsOnTarget, Fouls, Yellows;
            public bool Red;
            public InjurySeverity Injury;
            public float Energy;
            public FormationRole LastRole;
        }

        private sealed class Team
        {
            public MatchSide Side;
            public MatchTeamSetup Setup;
            public TacticSetup Tactic;
            public FormationDefinition Formation;
            public readonly List<FieldPlayer> OnField = new List<FieldPlayer>();
            public readonly List<MatchPlayerSetup> Bench = new List<MatchPlayerSetup>();
            public readonly float[] Strength = new float[SectorInfo.Count];
            public int Goals, Shots, ShotsOnTarget, Fouls, Yellows, Reds, Corners, Subs, Windows, TiredSubs, PossessionMinutes;
        }

        private sealed class Run
        {
            private readonly QuickSim _sim;
            private readonly MatchSetup _setup;
            private readonly Team _home, _away;
            private readonly Dictionary<Id, PlayerAcc> _acc = new Dictionary<Id, PlayerAcc>();
            private readonly List<PlayerAcc> _order = new List<PlayerAcc>();
            private readonly List<MatchEvent> _events = new List<MatchEvent>();
            private readonly Rng _possession, _chances, _discipline, _injuries, _ratings, _subs;
            private readonly float[] _weights = new float[16];
            private int _minute;

            private QuickSimDefinition Q => _sim.Q;
            private MatchRules R => _sim._rules;

            public Run(QuickSim sim, MatchSetup setup)
            {
                _sim = sim;
                _setup = setup;
                var streams = new RngStreams(setup.Seed);
                _possession = streams.Get("QuickSim.Possession");
                _chances = streams.Get("QuickSim.Chances");
                _discipline = streams.Get("QuickSim.Discipline");
                _injuries = streams.Get("QuickSim.Injuries");
                _ratings = streams.Get("QuickSim.Ratings");
                _subs = streams.Get("QuickSim.Substitutions");
                _home = CreateTeam(MatchSide.Home, setup.Home);
                _away = CreateTeam(MatchSide.Away, setup.Away);
            }

            private Team CreateTeam(MatchSide side, MatchTeamSetup ts)
            {
                var formation = _sim._db.Formation(ts.Tactic.FormationId)
                                ?? throw new ArgumentException($"{InvalidSetup}: unknown formation '{ts.Tactic.FormationId}'.");
                var team = new Team { Side = side, Setup = ts, Tactic = ts.Tactic, Formation = formation };
                for (int i = 0; i < ts.Starters.Count; i++)
                {
                    var p = ts.Starters[i];
                    team.OnField.Add(new FieldPlayer(p, formation.Slots[i], p.Energy));
                    var acc = Register(p, side);
                    acc.Started = true;
                    acc.MinuteOn = 0;
                    acc.LastRole = formation.Slots[i].Role;
                }
                foreach (var p in ts.Bench)
                {
                    team.Bench.Add(p);
                    Register(p, side);
                }
                return team;
            }

            private PlayerAcc Register(MatchPlayerSetup p, MatchSide side)
            {
                if (_acc.ContainsKey(p.PlayerId)) throw new ArgumentException($"{InvalidSetup}: player {p.PlayerId} appears twice.");
                var acc = new PlayerAcc { Setup = p, Side = side, Energy = p.Energy };
                _acc.Add(p.PlayerId, acc);
                _order.Add(acc);
                return acc;
            }

            // ---------------- Main loop ----------------

            public MatchResult Play()
            {
                int halftime = R.Definition.Substitutions.HalftimeMinute;
                for (_minute = 1; _minute <= Q.Minutes; _minute++)
                {
                    if (_minute == halftime)
                    {
                        Recover(_home); Recover(_away);
                        SubstitutionWindow(_home, halftime: true); SubstitutionWindow(_away, halftime: true);
                    }
                    else if (IsWindow(_minute))
                    {
                        SubstitutionWindow(_home, halftime: false); SubstitutionWindow(_away, halftime: false);
                    }

                    UpdateStrength(_home, _away);
                    UpdateStrength(_away, _home);

                    var attack = RollPossession();
                    var defend = attack == _home ? _away : _home;
                    attack.PossessionMinutes++;
                    PlayAttack(attack, defend);
                    Fouls(attack, defend);
                    Injuries(_home); Injuries(_away);
                    Fatigue(_home); Fatigue(_away);
                }
                return BuildResult();
            }

            private bool IsWindow(int minute)
            {
                foreach (int w in R.Definition.Substitutions.Windows)
                    if (w == minute && w != R.Definition.Substitutions.HalftimeMinute) return true;
                return false;
            }

            private void UpdateStrength(Team team, Team opponent)
            {
                R.SectorStrengths(team.OnField, team.Strength);
                if (!_setup.NeutralVenue && team.Side == MatchSide.Home)
                    for (int s = 0; s < team.Strength.Length; s++) team.Strength[s] *= 1f + Q.HomeAdvantage;
            }

            private Team RollPossession()
            {
                float h = Possession(_home), a = Possession(_away);
                double ph = Math.Pow(h, Q.PossessionExponent), pa = Math.Pow(a, Q.PossessionExponent);
                return _possession.NextDouble() < ph / (ph + pa) ? _home : _away;
            }

            private float Possession(Team t) =>
                t.Strength[(int)Sector.Creation] * Q.LinePossession[t.Tactic.DefensiveLine - 1] * Q.PressurePossession[t.Tactic.Pressure - 1];

            // ---------------- Attacks and shots ----------------

            private void PlayAttack(Team att, Team def)
            {
                float attack = att.Strength[(int)Sector.Attack] * Q.MentalityAttack[att.Tactic.Mentality - 1];
                float defense = def.Strength[(int)Sector.Defense] * Q.MentalityDefense[def.Tactic.Mentality - 1]
                                * Q.LineDefense[def.Tactic.DefensiveLine - 1] * Q.PressureDefense[def.Tactic.Pressure - 1];
                double chance = Q.AttackChancePerMinute * Math.Pow(attack / Math.Max(1f, defense), Q.AttackRatioExponent);
                if (_chances.NextDouble() >= Math.Min(Q.MaxAttackChance, chance))
                {
                    if (_chances.NextDouble() < Q.Corners.FromAttackWithoutShot) Corner(att, def);
                    return;
                }

                var type = PickPlayType(att, def);
                var p = Q.PlayTypes[(int)type];
                var shooterWeights = type == PlayType.Cross ? Q.HeaderWeights : Q.ShooterWeights;
                var sector = type == PlayType.Cross ? Sector.Aerial : Sector.Attack;
                var shooter = PickPlayer(att, shooterWeights, sector, exclude: null, foulPropensity: false);
                if (shooter == null) return;
                var accuracyEffect = type == PlayType.Cross ? Effect.HeaderAccuracy
                    : type == PlayType.LongShot ? Effect.ShotAngleErrorOutOfBox : Effect.ShotAngleErrorInBox;
                var assistWeights = type == PlayType.Cross ? Q.CrossWeights : Q.AssistWeights;
                Shot(att, def, shooter, accuracyEffect, p.OnTarget, p.GoalGivenOnTarget, p.AssistChance, assistWeights,
                    cross: type == PlayType.Cross, blockable: true, penalty: false);
            }

            private PlayType PickPlayType(Team att, Team def)
            {
                float total = 0f;
                for (int t = 0; t < Q.PlayTypes.Count; t++)
                {
                    float w = Q.PlayTypes[t].Weight;
                    _weights[t] = w;
                    total += w;
                }
                double roll = _chances.NextDouble() * total;
                for (int t = 0; t < Q.PlayTypes.Count; t++)
                {
                    if (roll < _weights[t]) return (PlayType)t;
                    roll -= _weights[t];
                }
                return PlayType.OpenPlay;
            }

            private void Shot(Team att, Team def, FieldPlayer shooter, Effect accuracyEffect, float onTarget, float goalGivenOnTarget,
                float assistChance, IReadOnlyList<float> assistWeights, bool cross, bool blockable, bool penalty)
            {
                var acc = _acc[shooter.Setup.PlayerId];
                var attrs = shooter.Setup.Attributes;
                att.Shots++;
                acc.Shots++;
                _events.Add(new MatchEvent(_minute, MatchEventType.Shot, att.Side, shooter.Setup.PlayerId, Id.None));

                if (blockable && _chances.NextDouble() < Q.BlockChance * BlockFactor(def))
                {
                    if (_chances.NextDouble() < Q.Corners.FromBlock) Corner(att, def);
                    return;
                }

                // Accuracy: larger angle error / pressure error than average lowers the on-target chance (Balance effects).
                double accuracy = Math.Pow(1.0 / R.EffectRatio(accuracyEffect, attrs), Q.AccuracyExponent)
                                  * Math.Pow(1.0 / R.EffectRatio(Effect.PressureErrorMult, attrs), Q.ComposureExponent)
                                  * R.EnergyFactor(attrs, shooter.Energy);
                // Headed shots: winning the aerial duel depends on both teams' aerial strength.
                if (cross)
                    accuracy *= Math.Pow(att.Strength[(int)Sector.Aerial] / Math.Max(1f, def.Strength[(int)Sector.Aerial]), Q.AerialDuelExponent);
                if (_chances.NextDouble() >= Math.Min(Q.MaxShotProbability, onTarget * accuracy)) return;

                att.ShotsOnTarget++;
                acc.ShotsOnTarget++;
                _events.Add(new MatchEvent(_minute, MatchEventType.ShotOnTarget, att.Side, shooter.Setup.PlayerId, Id.None));

                var keeper = Keeper(def);
                double keeperFactor = Math.Pow(50.0 / Math.Max(1f, def.Strength[(int)Sector.Goalkeeping]), Q.KeeperExponent);
                if (cross && keeper != null) keeperFactor /= R.EffectRatio(Effect.GkCrossClaimRange, keeper.Setup.Attributes);
                if (keeper == null) keeperFactor *= Q.NoKeeperGoalMultiplier; // no goalkeeper on the pitch
                bool goal = att.Goals < Q.MaxGoalsPerTeam && _chances.NextDouble() < Math.Min(Q.MaxShotProbability, goalGivenOnTarget * keeperFactor);
                if (!goal)
                {
                    if (!penalty && _chances.NextDouble() < Q.Corners.FromSave) Corner(att, def);
                    return;
                }

                att.Goals++;
                acc.Goals++;
                Id assister = Id.None;
                if (!penalty && _chances.NextDouble() < assistChance)
                {
                    var a = PickPlayer(att, assistWeights, Sector.Creation, exclude: shooter, foulPropensity: false);
                    if (a != null)
                    {
                        assister = a.Setup.PlayerId;
                        _acc[assister].Assists++;
                    }
                }
                _events.Add(new MatchEvent(_minute, MatchEventType.Goal, att.Side, shooter.Setup.PlayerId, assister));
            }

            /// <summary>Defenders' ShotBlockChance relative to average, weighted by their defensive role.</summary>
            private float BlockFactor(Team def)
            {
                float sum = 0f, weight = 0f;
                foreach (var fp in def.OnField)
                {
                    float w = R.Definition.RoleSectorWeights[(int)fp.Slot.Role][(int)Sector.Defense];
                    if (w <= 0f) continue;
                    sum += w * R.EffectRatio(Effect.ShotBlockChance, fp.Setup.Attributes);
                    weight += w;
                }
                return weight > 0f ? sum / weight : 1f;
            }

            private void Corner(Team att, Team def)
            {
                att.Corners++;
                _events.Add(new MatchEvent(_minute, MatchEventType.Corner, att.Side, Id.None, Id.None));
                if (_chances.NextDouble() >= Q.Corners.ShotChance) return;
                var header = PickPlayer(att, Q.HeaderWeights, Sector.Aerial, exclude: null, foulPropensity: false);
                if (header == null) return;
                Shot(att, def, header, Effect.HeaderAccuracy, Q.Corners.OnTarget, Q.Corners.GoalGivenOnTarget, Q.Corners.AssistChance,
                    Q.CrossWeights, cross: true, blockable: false, penalty: false);
            }

            // ---------------- Fouls, cards, penalties ----------------

            private void Fouls(Team att, Team def)
            {
                Foul(def, att, defending: true);
                Foul(att, def, defending: false);
            }

            private void Foul(Team fouling, Team fouled, bool defending)
            {
                float share = defending ? Q.DefendingTeamFoulShare : 1f - Q.DefendingTeamFoulShare;
                float rate = Q.FoulsPerMinute * share * Q.PressureFoulMult[fouling.Tactic.Pressure - 1];
                if (_discipline.NextDouble() >= rate) return;
                var fouler = PickPlayer(fouling, Q.FoulWeights, null, exclude: null, foulPropensity: true);
                if (fouler == null) return;

                var acc = _acc[fouler.Setup.PlayerId];
                fouling.Fouls++;
                acc.Fouls++;
                _events.Add(new MatchEvent(_minute, MatchEventType.Foul, fouling.Side, fouler.Setup.PlayerId, Id.None));

                var card = R.CardForFoul(_discipline, acc.Yellows > 0);
                if (card == MatchRules.Card.Yellow)
                {
                    acc.Yellows++;
                    fouling.Yellows++;
                    _events.Add(new MatchEvent(_minute, MatchEventType.YellowCard, fouling.Side, fouler.Setup.PlayerId, Id.None));
                    if (acc.Yellows >= 2) SendOff(fouling, fouler, acc);
                }
                else if (card == MatchRules.Card.StraightRed)
                {
                    SendOff(fouling, fouler, acc);
                }

                if (defending && _discipline.NextDouble() < Q.Penalties.PerDefensiveFoul)
                {
                    _events.Add(new MatchEvent(_minute, MatchEventType.Penalty, fouled.Side, Id.None, Id.None));
                    var taker = BestTaker(fouled);
                    if (taker != null)
                        Shot(fouled, fouling, taker, Effect.PenaltyAimWobble, Q.Penalties.OnTarget, Q.Penalties.GoalGivenOnTarget, 0f,
                            Q.AssistWeights, cross: false, blockable: false, penalty: true);
                }
            }

            private void SendOff(Team team, FieldPlayer player, PlayerAcc acc)
            {
                // Law 3: a match needs at least 7 players per team; a further dismissal is not applied here.
                if (team.OnField.Count <= 7) return;
                acc.Red = true;
                team.Reds++;
                _events.Add(new MatchEvent(_minute, MatchEventType.RedCard, team.Side, player.Setup.PlayerId, Id.None));
                Leave(team, player, acc);
            }

            private FieldPlayer BestTaker(Team team)
            {
                FieldPlayer best = null;
                float bestRatio = float.MaxValue;
                foreach (var fp in team.OnField)
                {
                    if (fp.Setup.MainPosition == (int)Position.GOL) continue;
                    float ratio = R.EffectRatio(Effect.PenaltyAimWobble, fp.Setup.Attributes);
                    if (ratio < bestRatio || (ratio == bestRatio && fp.Setup.PlayerId.CompareTo(best.Setup.PlayerId) < 0))
                    {
                        best = fp;
                        bestRatio = ratio;
                    }
                }
                return best;
            }

            // ---------------- Injuries, fatigue, substitutions ----------------

            private void Injuries(Team team)
            {
                for (int i = team.OnField.Count - 1; i >= 0; i--)
                {
                    var fp = team.OnField[i];
                    if (_injuries.NextDouble() >= R.InjuryHazardPerMinute(fp.Setup.Attributes, fp.Energy)) continue;
                    var acc = _acc[fp.Setup.PlayerId];
                    acc.Injury = R.InjurySeverityRoll(_injuries);
                    _events.Add(new MatchEvent(_minute, MatchEventType.Injury, team.Side, fp.Setup.PlayerId, Id.None));
                    // Forced substitution when possible; otherwise the team plays one short.
                    if (!Substitute(team, fp, forced: true)) Leave(team, fp, acc);
                }
            }

            private void Fatigue(Team team)
            {
                foreach (var fp in team.OnField)
                {
                    fp.Energy = Math.Max(0f, fp.Energy - R.EnergyDrainPerMinute(fp.Setup.Attributes, team.Tactic));
                    _acc[fp.Setup.PlayerId].Energy = fp.Energy;
                }
            }

            private void Recover(Team team)
            {
                foreach (var fp in team.OnField)
                {
                    fp.Energy = Math.Min(100f, fp.Energy + R.Definition.Fatigue.HalftimeRecovery);
                    _acc[fp.Setup.PlayerId].Energy = fp.Energy;
                }
            }

            private void SubstitutionWindow(Team team, bool halftime)
            {
                var rules = R.Definition.Substitutions;
                if (!halftime && team.Windows >= rules.MaxWindows) return;
                bool used = false;
                while (team.Subs < _setup.MaxSubstitutions && team.TiredSubs < rules.MaxTiredSubstitutions)
                {
                    FieldPlayer tired = null;
                    foreach (var fp in team.OnField)
                        if (fp.Energy < rules.EnergyThreshold && (tired == null || fp.Energy < tired.Energy ||
                            (fp.Energy == tired.Energy && fp.Setup.PlayerId.CompareTo(tired.Setup.PlayerId) < 0)))
                            tired = fp;
                    if (tired == null || !Substitute(team, tired, forced: false)) break;
                    team.TiredSubs++;
                    used = true;
                }
                if (used && !halftime) team.Windows++;
            }

            /// <summary>Replaces <paramref name="off"/> with the best bench player for his slot. False when not allowed.</summary>
            private bool Substitute(Team team, FieldPlayer off, bool forced)
            {
                if (team.Subs >= _setup.MaxSubstitutions || team.Bench.Count == 0) return false;
                var on = Lineups.Best(R, team.Bench, off.Slot.Position);
                if (on == null) return false;
                if (!forced && R.EffectiveOvr(on, off.Slot.Position) < R.EffectiveOvr(off.Setup, off.Slot.Position) * R.EnergyFactor(off.Setup.Attributes, off.Energy) - R.Definition.Substitutions.BenchOvrMargin)
                    return false; // the bench is much weaker: keep the tired player

                var offAcc = _acc[off.Setup.PlayerId];
                Leave(team, off, offAcc);
                team.Bench.Remove(on);
                var onAcc = _acc[on.PlayerId];
                onAcc.MinuteOn = _minute;
                onAcc.LastRole = off.Slot.Role;
                team.OnField.Add(new FieldPlayer(on, off.Slot, on.Energy));
                team.Subs++;
                _events.Add(new MatchEvent(_minute, MatchEventType.Substitution, team.Side, off.Setup.PlayerId, on.PlayerId));
                return true;
            }

            private void Leave(Team team, FieldPlayer player, PlayerAcc acc)
            {
                team.OnField.Remove(player);
                acc.MinuteOff = _minute;
                acc.Energy = player.Energy;
                acc.LastRole = player.Slot.Role;
            }

            // ---------------- Selection helpers ----------------

            private FieldPlayer Keeper(Team team)
            {
                foreach (var fp in team.OnField) if (fp.Slot.Role == FormationRole.GK) return fp;
                return null;
            }

            /// <summary>Weighted pick among players on the pitch: position weight x sector rating (or foul propensity).</summary>
            private FieldPlayer PickPlayer(Team team, IReadOnlyList<float> positionWeights, Sector? sector, FieldPlayer exclude, bool foulPropensity)
            {
                var rng = foulPropensity ? _discipline : _chances;
                int n = team.OnField.Count;
                if (_weights.Length < n) throw new InvalidOperationException("Too many players on the pitch.");
                float total = 0f;
                for (int i = 0; i < n; i++)
                {
                    var fp = team.OnField[i];
                    float w = fp == exclude ? 0f : positionWeights[(int)fp.Slot.Position];
                    if (w > 0f && sector.HasValue) w *= R.SectorRating(fp.Setup.Attributes, sector.Value);
                    if (w > 0f && foulPropensity) w *= R.FoulPropensity(fp.Setup.Attributes);
                    _weights[i] = w;
                    total += w;
                }
                if (total <= 0f) return null;
                double roll = rng.NextDouble() * total;
                for (int i = 0; i < n; i++)
                {
                    if (roll < _weights[i]) return team.OnField[i];
                    roll -= _weights[i];
                }
                for (int i = n - 1; i >= 0; i--) if (_weights[i] > 0f) return team.OnField[i];
                return null;
            }

            // ---------------- Result ----------------

            private MatchResult BuildResult()
            {
                int minutes = Q.Minutes;
                var players = new List<PlayerMatchStats>(_order.Count);
                foreach (var a in _order)
                {
                    bool played = a.MinuteOn >= 0;
                    int off = a.MinuteOff >= 0 ? a.MinuteOff : minutes;
                    int minutesPlayed = played ? Math.Max(0, off - a.MinuteOn) : 0;
                    float? rating = null;
                    if (played)
                    {
                        var team = a.Side == MatchSide.Home ? _home : _away;
                        var other = a.Side == MatchSide.Home ? _away : _home;
                        rating = Ratings.Compute(R.Definition.Ratings, new Ratings.Contribution
                        {
                            Minutes = minutesPlayed, Goals = a.Goals, Assists = a.Assists, ShotsOnTarget = a.ShotsOnTarget,
                            ShotsOffTarget = a.Shots - a.ShotsOnTarget, Fouls = a.Fouls, Yellows = a.Yellows, Red = a.Red,
                            TeamGoalsFor = team.Goals, TeamGoalsAgainst = other.Goals, Role = a.LastRole,
                        }, _ratings);
                    }
                    players.Add(new PlayerMatchStats(a.Setup.PlayerId, a.Side, a.Started, minutesPlayed, a.Goals, a.Assists, a.Shots,
                        a.ShotsOnTarget, a.Fouls, a.Yellows, a.Red, rating, (float)Math.Round(a.Energy, 1), a.Injury));
                }

                float homePossession = (float)Math.Round(100.0 * _home.PossessionMinutes / minutes, 1);
                return new MatchResult(_home.Goals, _away.Goals, _events,
                    Stats(_home, homePossession), Stats(_away, (float)Math.Round(100f - homePossession, 1)), players);
            }

            private static TeamMatchStats Stats(Team t, float possession) =>
                new TeamMatchStats(t.Shots, t.ShotsOnTarget, possession, t.Fouls, t.Yellows, t.Reds, t.Corners, t.Subs);
        }
    }
}
