using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Board;
using Game.Career.Market;
using Game.Career.Players;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Market;
using Game.Rules.Ovr;

namespace Game.UI.Presenters
{
    /// <summary>C3b (X-97): squad list, player profile, lineup and tactic.</summary>
    public sealed partial class ShellPresenter
    {
        private int _playerId;
        private (LineupArea Area, int Index)? _lineupSelection;

        // Squad sections, in the order of GAME_DESIGN §9's position list.
        private static readonly (string Key, Position[] Positions)[] Sections =
        {
            ("squad.section_keepers", new[] { Position.GOL }),
            ("squad.section_defenders", new[] { Position.ZAG, Position.LD, Position.LE }),
            ("squad.section_midfielders", new[] { Position.VOL, Position.MC, Position.MEI }),
            ("squad.section_forwards", new[] { Position.PD, Position.PE, Position.ATA }),
        };

        // ---------------------------------------------------------------- commands

        public void OpenPlayer(int playerId)
        {
            _playerId = playerId;
            Nav.Push(ScreenId.Player);
        }

        public void OpenLineup()
        {
            _lineupSelection = null;
            Nav.Push(ScreenId.Lineup);
        }

        /// <summary>◀ ▶ on a tactic control: the managed club's tactic changes (and is saved) at once.</summary>
        public void TacticStep(TacticControl control, int delta)
        {
            var t = Session.Career.UserTactic;
            string formation = t.FormationId;
            int mentality = t.Mentality, line = t.DefensiveLine, pressure = t.Pressure;
            switch (control)
            {
                case TacticControl.Formation:
                {
                    var ids = Session.Db.Formations.Select(f => f.Id).ToList();
                    int i = Math.Max(0, ids.IndexOf(formation));
                    formation = ids[(i + delta + ids.Count) % ids.Count];
                    break;
                }
                case TacticControl.Mentality: mentality = Clamp(mentality + delta, TacticModifiers.MentalityLevels); break;
                case TacticControl.DefensiveLine: line = Clamp(line + delta, TacticModifiers.LineLevels); break;
                default: pressure = Clamp(pressure + delta, TacticModifiers.PressureLevels); break;
            }
            Session.SetTactic(new TacticSetup(formation, mentality, line, pressure));
            Nav.Replace(ScreenId.Lineup);
        }

        private static int Clamp(int value, int levels) => Math.Max(1, Math.Min(levels, value));

        /// <summary>
        /// Lineup editing by taps: tap one player, then another to swap them (starter ↔ starter, starter ↔ bench, a
        /// reserve takes a starter's or a bench place). The first change turns the automatic lineup into the user's.
        /// </summary>
        public void LineupTap(LineupArea area, int index)
        {
            var (starters, bench, reserves) = LineupLists();
            int benchSize = Session.Db.Competitions.MatchRules.BenchSize;
            var selection = _lineupSelection;
            _lineupSelection = null;
            if (selection == null || (selection.Value.Area == LineupArea.Reserve && area == LineupArea.Reserve))
            {
                _lineupSelection = (area, index);
                Nav.Replace(ScreenId.Lineup);
                return;
            }
            var s = selection.Value;
            if (s.Area == area && s.Index == index) { Nav.Replace(ScreenId.Lineup); return; }

            Id At(LineupArea a, int i) => a == LineupArea.Starter ? starters[i] : a == LineupArea.Bench ? (i < bench.Count ? bench[i] : Id.None) : reserves[i];
            var st = new List<Id>(starters);
            var bn = new List<Id>(bench);
            while (bn.Count < benchSize) bn.Add(Id.None);
            void Put(LineupArea a, int i, Id id)
            {
                if (a == LineupArea.Starter) st[i] = id;
                else if (a == LineupArea.Bench) bn[i] = id;
            }
            Id x = At(s.Area, s.Index), y = At(area, index);
            Put(s.Area, s.Index, y);
            Put(area, index, x);
            if (st.Any(id => id.IsNone))
            {
                // A starter's place cannot be left empty: just select the new tap.
                _lineupSelection = (area, index);
                Nav.Replace(ScreenId.Lineup);
                return;
            }
            Session.SetLineup(st, bn.Where(id => !id.IsNone).ToList());
            Nav.Replace(ScreenId.Lineup);
        }

        public void AutomaticLineup()
        {
            _lineupSelection = null;
            Session.SetAutomaticLineup();
            Nav.Replace(ScreenId.Lineup);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Competition and date of the managed club's next match (the pending one, the next scheduled one, or today).</summary>
        private (CompetitionKind Kind, DateTime Date, Fixture Fixture) NextMatchContext()
        {
            var pending = Session.Pending;
            if (pending != null) return (pending.Fixture.Kind, pending.Fixture.Date, pending.Fixture);
            var next = NextUserFixture();
            return next != null ? (next.Kind, next.Date, next) : (CompetitionKind.League, Today, null);
        }

        private float EnergyOf(Player p, DateTime date) =>
            ConditionSystem.EnergyAt(Session.Db.Development, p, date, Staff.PhysioEnergyMultiplier(Session.Db, State.World, Session.ManagedClub.Id));

        private int Age(Player p) { var d = Today; return p.BirthDate.AgeOn(d.Year, d.Month, d.Day); }

        /// <summary>Why the player cannot play the next match, or null.</summary>
        private string Unavailability(Player p, CompetitionKind kind, DateTime date)
        {
            var c = p.Condition;
            if (c.IsInjured(date))
                return c.InjuredUntil.HasValue && date < c.InjuredUntil.Value
                    ? S.Format("status.injured_until", Fmt.ShortDate(c.InjuredUntil.Value))
                    : S.Format("status.injured_matches", c.InjuryMatchesLeft);
            int suspended = c.SuspendedMatches((int)kind);
            if (suspended > 0) return S.Format("status.suspended", suspended);
            return null;
        }

        private static int SectionOf(Position p)
        {
            for (int i = 0; i < Sections.Length; i++) if (Array.IndexOf(Sections[i].Positions, p) >= 0) return i;
            return Sections.Length - 1;
        }

        /// <summary>The squad in list order: section, then OVR (best first), then Id.</summary>
        private List<Player> SquadInOrder() =>
            State.World.SquadOf(Session.ManagedClub.Id)
                .OrderBy(p => SectionOf(p.MainPosition)).ThenBy(p => (int)p.MainPosition).ThenByDescending(Ovr).ThenBy(p => p.Id).ToList();

        /// <summary>The lineup shown and edited: the user's (players who left replaced by the automatic pick) or the
        /// automatic one; the reserves are the rest of the squad in list order.</summary>
        private (List<Id> Starters, List<Id> Bench, List<Id> Reserves) LineupLists()
        {
            var (kind, date, _) = NextMatchContext();
            var team = Session.Career.UserTeam(kind, date);
            var squad = SquadInOrder();
            var inSquad = new HashSet<Id>(squad.Select(p => p.Id));
            var plan = State.UserPlan;
            List<Id> starters, bench;
            if (plan != null && plan.ManualLineup)
            {
                starters = new List<Id>();
                for (int i = 0; i < MatchTeamSetup.StarterCount; i++)
                {
                    var id = i < plan.Starters.Count ? plan.Starters[i] : Id.None;
                    starters.Add(inSquad.Contains(id) ? id : team.Starters[i].PlayerId);
                }
                bench = plan.Bench.Where(id => inSquad.Contains(id) && !starters.Contains(id)).ToList();
            }
            else
            {
                starters = team.Starters.Select(p => p.PlayerId).ToList();
                bench = team.Bench.Select(p => p.PlayerId).ToList();
            }
            var used = new HashSet<Id>(starters.Concat(bench));
            var reserves = squad.Where(p => !used.Contains(p.Id)).Select(p => p.Id).ToList();
            return (starters, bench, reserves);
        }

        // ---------------------------------------------------------------- screens

        public SquadModel Squad()
        {
            var (kind, date, _) = NextMatchContext();
            var team = Session.Career.UserTeam(kind, date);
            var starters = new HashSet<Id>(team.Starters.Select(p => p.PlayerId));
            var bench = new HashSet<Id>(team.Bench.Select(p => p.PlayerId));
            var squad = SquadInOrder();
            var m = new SquadModel
            {
                Header = Header(),
                Title = S.Get("squad.title"),
                Lineup = new ButtonModel(S.Get("squad.lineup")),
                ColumnsHeader = S.Get("squad.columns"),
                Summary = S.Format("squad.summary", squad.Count,
                    squad.Count == 0 ? 0 : (int)Math.Round(squad.Average(Ovr)), squad.Count == 0 ? 0 : (int)Math.Round(squad.Average(Age))),
            };
            int section = -1;
            foreach (var p in squad)
            {
                int sec = SectionOf(p.MainPosition);
                if (sec != section)
                {
                    section = sec;
                    m.Rows.Add(new SquadRowModel { Section = S.Get(Sections[sec].Key) });
                }
                string problem = Unavailability(p, kind, date);
                string role = starters.Contains(p.Id) ? S.Get("squad.starter") : bench.Contains(p.Id) ? S.Get("squad.bench") : "";
                m.Rows.Add(new SquadRowModel
                {
                    PlayerId = p.Id.Value,
                    Pos = p.MainPosition.ToString(),
                    Name = p.Name,
                    Age = I(Age(p)),
                    Ovr = I(Ovr(p)),
                    Energy = S.Format("common.percent", (int)Math.Round(EnergyOf(p, date))),
                    Status = problem ?? role,
                    StatusBad = problem != null,
                });
            }
            return m;
        }

        private static string I(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public PlayerModel PlayerProfile()
        {
            var p = PlayerById(new Id(_playerId));
            if (p == null) return null;
            var (kind, date, _) = NextMatchContext();
            var club = Session.ManagedClub;
            var c = p.Condition;
            string problem = Unavailability(p, kind, date);
            var potential = MarketRules.ScoutPotentialRange(club.Reputation, p.Potential);
            var m = new PlayerModel
            {
                Header = Header(),
                Back = new ButtonModel(S.Get("common.back")),
                Name = p.Name,
                Positions = p.SecondaryPositions.Count == 0 ? p.MainPosition.ToString()
                    : S.Format("player.positions", p.MainPosition, string.Join(", ", p.SecondaryPositions)),
                Ovr = S.Format("player.ovr", Ovr(p)),
                Potential = S.Format("player.potential", potential.Min, potential.Max),
                Status = problem ?? S.Get("status.available"),
                StatusBad = problem != null,
            };

            var bio = new InfoGroupModel { Title = S.Get("player.group_bio") };
            Row(bio, "player.age", I(Age(p)));
            Row(bio, "player.born", Fmt.Date(new DateTime(p.BirthDate.Year, p.BirthDate.Month, p.BirthDate.Day)));
            Row(bio, "player.nationality", S.Get("nation." + p.Nationality.ToLowerInvariant()));
            Row(bio, "player.height", S.Format("player.height_value", Fmt.Decimal(p.HeightCm / 100.0, 2)));
            Row(bio, "player.foot", S.Get(p.PreferredFoot == Foot.Left ? "player.foot_left" : "player.foot_right"));
            Row(bio, "player.weak_foot", Fmt.Stars(p.WeakFoot));
            m.Groups.Add(bio);

            bool keeper = p.MainPosition == Position.GOL;
            if (keeper) m.Groups.Add(Attributes(p, "player.group_keeper", Attr.GkReflexes, Attr.GkPositioning, Attr.GkAerial, Attr.GkHandling));
            m.Groups.Add(Attributes(p, "player.group_physical", Attr.Speed, Attr.Agility, Attr.Stamina, Attr.Strength));
            if (!keeper)
                m.Groups.Add(Attributes(p, "player.group_technical", Attr.Passing, Attr.Crossing, Attr.Dribbling, Attr.Finishing, Attr.LongShots, Attr.Heading, Attr.Tackling));
            m.Groups.Add(Attributes(p, "player.group_mental", Attr.Vision, Attr.Positioning, Attr.Composure));
            if (keeper) m.Groups.Add(Attributes(p, "player.group_technical", Attr.Passing, Attr.Crossing, Attr.Dribbling, Attr.Finishing, Attr.LongShots, Attr.Heading, Attr.Tackling));

            var condition = new InfoGroupModel { Title = S.Get("player.group_condition") };
            Row(condition, "player.energy", S.Format("common.percent", (int)Math.Round(EnergyOf(p, date))));
            Row(condition, "player.morale", S.Get("morale." + Math.Max(1, Math.Min(5, c.Morale))));
            Row(condition, "player.form", c.Form.HasValue ? Fmt.Rating(c.Form.Value) : S.Get("common.none"));
            m.Groups.Add(condition);

            var season = new InfoGroupModel { Title = S.Format("player.group_season", State.Season.Year) };
            Row(season, "player.appearances", I(c.SeasonAppearances));
            Row(season, "player.goals", I(c.SeasonGoals));
            Row(season, "player.minutes", I(c.SeasonMinutes));
            Row(season, "player.average_rating", c.SeasonAppearances > 0 ? Fmt.Rating(c.SeasonRatingSum / c.SeasonAppearances) : S.Get("common.none"));
            m.Groups.Add(season);

            var contract = State.World.ContractOf(p.Id);
            var deal = new InfoGroupModel { Title = S.Get("player.group_contract") };
            if (contract != null)
            {
                Row(deal, "player.wage", S.Format("player.wage_value", Fmt.Money(S, contract.Wage)));
                Row(deal, "player.contract_end", I(contract.EndYear));
            }
            int yearsLeft = contract?.YearsLeft(State.Season.Year) ?? 0;
            long value = MarketRules.Value(Session.Db.Market, club.DivisionIndex, Ovr(p), Age(p), p.Potential, yearsLeft, c.Form ?? 6f);
            Row(deal, "player.value", Fmt.Money(S, value));
            m.Groups.Add(deal);
            return m;
        }

        private void Row(InfoGroupModel group, string key, string value) => group.Rows.Add(new LabelValueModel { Label = S.Get(key), Value = value });

        private InfoGroupModel Attributes(Player p, string titleKey, params Attr[] attrs)
        {
            var g = new InfoGroupModel { Title = S.Get(titleKey) };
            foreach (var a in attrs)
            {
                int v = p.Attributes[(int)a];
                g.Rows.Add(new LabelValueModel { Label = S.Get(AttrKey(a)), Value = I(v), Bar = v / (float)AttrInfo.MaxValue });
            }
            return g;
        }

        private static string AttrKey(Attr a)
        {
            switch (a)
            {
                case Attr.Speed: return "attr.speed";
                case Attr.Agility: return "attr.agility";
                case Attr.Stamina: return "attr.stamina";
                case Attr.Strength: return "attr.strength";
                case Attr.Passing: return "attr.passing";
                case Attr.Crossing: return "attr.crossing";
                case Attr.Dribbling: return "attr.dribbling";
                case Attr.Finishing: return "attr.finishing";
                case Attr.LongShots: return "attr.long_shots";
                case Attr.Heading: return "attr.heading";
                case Attr.Tackling: return "attr.tackling";
                case Attr.Vision: return "attr.vision";
                case Attr.Positioning: return "attr.positioning";
                case Attr.Composure: return "attr.composure";
                case Attr.GkReflexes: return "attr.gk_reflexes";
                case Attr.GkPositioning: return "attr.gk_positioning";
                case Attr.GkAerial: return "attr.gk_aerial";
                default: return "attr.gk_handling";
            }
        }

        public LineupModel Lineup()
        {
            var (kind, date, fixture) = NextMatchContext();
            var tactic = Session.Career.UserTactic;
            var formation = Session.Db.Formation(tactic.FormationId);
            var (starters, bench, reserves) = LineupLists();
            var plan = State.UserPlan;
            bool manual = plan != null && plan.ManualLineup;
            int benchSize = Session.Db.Competitions.MatchRules.BenchSize;
            var m = new LineupModel
            {
                Title = S.Get("lineup.title"),
                Back = new ButtonModel(S.Get("common.back")),
                NextMatch = fixture == null ? null : S.Format("lineup.next_match", CompetitionLine(fixture), Fmt.Date(fixture.Date)),
                Mode = S.Get(manual ? "lineup.mode_manual" : "lineup.mode_auto"),
                Automatic = new ButtonModel(S.Get("lineup.automatic"), manual),
                Hint = S.Get("lineup.hint"),
                StartersTitle = S.Get("lineup.starters"),
                BenchTitle = S.Format("lineup.bench", bench.Count, benchSize),
                ReservesTitle = S.Get("lineup.reserves"),
            };
            var formationIds = Session.Db.Formations.Select(f => f.Id).ToList();
            m.Tactic.Add(new StepperModel { Label = S.Get("tactic.formation"), Value = tactic.FormationId, CanPrevious = formationIds.Count > 1, CanNext = formationIds.Count > 1 });
            m.Tactic.Add(Stepper("tactic.mentality", "tactic.mentality_" + tactic.Mentality, tactic.Mentality, TacticModifiers.MentalityLevels));
            m.Tactic.Add(Stepper("tactic.line", "tactic.line_" + tactic.DefensiveLine, tactic.DefensiveLine, TacticModifiers.LineLevels));
            m.Tactic.Add(Stepper("tactic.pressure", "tactic.pressure_" + tactic.Pressure, tactic.Pressure, TacticModifiers.PressureLevels));

            for (int i = 0; i < starters.Count; i++)
                m.Starters.Add(Slot(starters[i], formation.Slots[i].Position, kind, date, _lineupSelection == (LineupArea.Starter, i)));
            for (int i = 0; i < benchSize; i++)
                m.Bench.Add(i < bench.Count
                    ? Slot(bench[i], null, kind, date, _lineupSelection == (LineupArea.Bench, i))
                    : new LineupSlotModel { Pos = "", Name = S.Get("lineup.empty"), Ovr = "", Energy = "", Empty = true, Selected = _lineupSelection == (LineupArea.Bench, i) });
            for (int i = 0; i < reserves.Count; i++)
                m.Reserves.Add(Slot(reserves[i], null, kind, date, _lineupSelection == (LineupArea.Reserve, i)));
            return m;
        }

        private StepperModel Stepper(string labelKey, string valueKey, int value, int levels) => new StepperModel
        {
            Label = S.Get(labelKey), Value = S.Get(valueKey), CanPrevious = value > 1, CanNext = value < levels,
        };

        /// <summary>A lineup row: at a starter's slot the OVR is the effective one there (position fit, GAME_DESIGN §9).</summary>
        private LineupSlotModel Slot(Id id, Position? slot, CompetitionKind kind, DateTime date, bool selected)
        {
            var p = PlayerById(id);
            if (p == null) return new LineupSlotModel { Pos = slot?.ToString() ?? "", Name = S.Get("lineup.empty"), Ovr = "", Energy = "", Empty = true, Selected = selected };
            var at = slot ?? p.MainPosition;
            int ovr = OvrCalculator.Effective(Session.Db.Ovr, p.AttributeSpan, p.MainPosition, p.SecondaryPositionSpan, at);
            string warning = Unavailability(p, kind, date);
            if (warning == null && slot.HasValue && at != p.MainPosition && !p.SecondaryPositions.Contains(at))
                warning = S.Format("lineup.out_of_position", p.MainPosition);
            return new LineupSlotModel
            {
                Pos = slot.HasValue ? at.ToString() : p.MainPosition.ToString(),
                Name = p.Name,
                Ovr = I(ovr),
                Energy = S.Format("common.percent", (int)Math.Round(EnergyOf(p, date))),
                Warning = warning,
                Selected = selected,
            };
        }
    }
}
