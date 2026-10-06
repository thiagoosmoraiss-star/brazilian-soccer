using System;
using System.Collections.Generic;
using Game.Core.Ids;
using Game.Data.Definitions;

namespace Game.Career.World
{
    /// <summary>Club colors (ids and #RRGGBB values from Data/World/club_templates.json).</summary>
    public sealed class ClubColors
    {
        public string PrimaryId { get; internal set; }
        public string PrimaryHex { get; internal set; }
        public string SecondaryId { get; internal set; }
        public string SecondaryHex { get; internal set; }
    }

    /// <summary>Crest built from templates: shape + generic symbol + the two club colors (GAME_DESIGN §2).</summary>
    public sealed class Crest
    {
        public string ShapeId { get; internal set; }
        public string SymbolId { get; internal set; }
        public string PrimaryColorId { get; internal set; }
        public string SecondaryColorId { get; internal set; }
    }

    public sealed class Stadium
    {
        /// <summary>Level 1-10 (GAME_DESIGN §8).</summary>
        public int Level { get; internal set; }
        public int Capacity { get; internal set; }
    }

    /// <summary>Fictional club (X-02). Relations to players go through <see cref="Contract"/>.</summary>
    public sealed class Club
    {
        public Id Id { get; internal set; }
        public string Name { get; internal set; }
        public string ShortName { get; internal set; }
        public string City { get; internal set; }
        public string Uf { get; internal set; }
        public ClubColors Colors { get; internal set; }
        public Crest Crest { get; internal set; }
        /// <summary>Index into the generation divisions (0 = top division).</summary>
        public int DivisionIndex { get; internal set; }
        public string DivisionName { get; internal set; }
        /// <summary>Internal reputation 0-1000 (GAME_DESIGN §6).</summary>
        public int Reputation { get; internal set; }
        /// <summary>Displayed reputation, 1-5 stars.</summary>
        public int Stars { get; internal set; }
        /// <summary>Transfer-fund/wage-cap base for the market (`Market.Budget`, B5): synced from <see cref="Balance"/>
        /// at every month end (B6, X-44): max(0, Balance), never below zero.</summary>
        public long Budget { get; internal set; }
        public Stadium Stadium { get; internal set; }
        /// <summary>Fan base size.</summary>
        public int Fans { get; internal set; }
        /// <summary>Training Center level 1-5 (GAME_DESIGN §8, B7, X-45).</summary>
        public int TrainingCenterLevel { get; internal set; }

        /// <summary>Real cash (B6, X-44): always equal to the sum of this season's ledger entries plus every
        /// previous season's net (TEST_PLAN: "saldo = soma do livro").</summary>
        public long Balance { get; internal set; }
        /// <summary>Consecutive months this club has closed with a negative <see cref="Balance"/>.</summary>
        public int MonthsNegativeCash { get; internal set; }
        /// <summary>GAME_DESIGN §11: flagged after `cashAlert.alertMonths` consecutive negative months.</summary>
        public bool CashAlert { get; internal set; }
        /// <summary>GAME_DESIGN §11: the market (B5) stops signing for this club after `cashAlert.transferLockoutMonths`.</summary>
        public bool TransferLockout { get; internal set; }
    }

    public enum LedgerCategory
    {
        Tv = 0,
        Ticketing = 1,
        Sponsorship = 2,
        Prizes = 3,
        TransferIn = 4,
        Wages = 5,
        Maintenance = 6,
        TransferOut = 7,
        /// <summary>Staff wages (B7, X-45).</summary>
        StaffWages = 8,
        /// <summary>A facility upgrade's one-time cost (B7, X-45).</summary>
        FacilityInvestment = 9,
    }

    public enum StaffRole
    {
        Physio = 0,
        Assistant = 1,
        Scout = 2,
    }

    /// <summary>One of a club's three technical-staff roles (GAME_DESIGN §7, B7, X-45): always filled by the market AI.</summary>
    public sealed class StaffMember
    {
        public Id Id { get; internal set; }
        public Id ClubId { get; internal set; }
        public StaffRole Role { get; internal set; }
        /// <summary>Level 1-5.</summary>
        public int Level { get; internal set; }
    }

    public enum FacilityType
    {
        Stadium = 0,
        TrainingCenter = 1,
    }

    /// <summary>An in-progress facility upgrade (GAME_DESIGN §8: "obras com custo, tempo"; B7, X-45).</summary>
    public sealed class FacilityProject
    {
        public Id Id { get; internal set; }
        public Id ClubId { get; internal set; }
        public FacilityType Type { get; internal set; }
        public int TargetLevel { get; internal set; }
        public DateTime CompletesOn { get; internal set; }
    }

    public enum ObjectiveType
    {
        Promote = 0,
        AvoidRelegation = 1,
        BreakEven = 2,
    }

    /// <summary>A club's season objective (GAME_DESIGN §6, B7, X-45): set at season start, evaluated at season end.</summary>
    public sealed class Objective
    {
        public Id ClubId { get; internal set; }
        public int Year { get; internal set; }
        public ObjectiveType Type { get; internal set; }
        /// <summary>Set once the season ends.</summary>
        public bool? Achieved { get; internal set; }
    }

    /// <summary>The club's manager (GAME_DESIGN §6, B7, X-45): confidence drives dismissal; a new manager (same club,
    /// automatic simulation) resets it rather than ending the career, which only a human save would end.</summary>
    public sealed class Manager
    {
        public Id Id { get; internal set; }
        public Id ClubId { get; internal set; }
        /// <summary>0-100; below `board.json`'s dismissalThreshold at a season end is a dismissal.</summary>
        public int Confidence { get; internal set; }
        public int Dismissals { get; internal set; }
    }

    /// <summary>One cash movement (B6, X-44, TECHNICAL_SPEC §13): the club's <see cref="Club.Balance"/> is always the
    /// sum of its ledger entries. Only the current season's entries are kept (archived as a net total per season,
    /// TECHNICAL_SPEC §11); positive = revenue, negative = expense.</summary>
    public sealed class LedgerEntry
    {
        public Id Id { get; internal set; }
        public Id ClubId { get; internal set; }
        public DateTime Date { get; internal set; }
        public LedgerCategory Category { get; internal set; }
        public long Amount { get; internal set; }
    }

    public enum Foot
    {
        Right = 0,
        Left = 1,
    }

    public readonly struct BirthDate : IEquatable<BirthDate>
    {
        public readonly int Year, Month, Day;

        public BirthDate(int year, int month, int day) { Year = year; Month = month; Day = day; }

        /// <summary>Age in whole years on the given date.</summary>
        public int AgeOn(int year, int month, int day)
        {
            int age = year - Year;
            if (month < Month || (month == Month && day < Day)) age--;
            return age;
        }

        public bool Equals(BirthDate o) => Year == o.Year && Month == o.Month && Day == o.Day;
        public override bool Equals(object obj) => obj is BirthDate o && Equals(o);
        public override int GetHashCode() => (Year * 400) + (Month * 32) + Day;
        public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";
    }

    /// <summary>
    /// Player (TECHNICAL_SPEC §4.2): identity, 18 attributes (array indexed by Attr), hidden potential,
    /// positions. No traits in B1 (X-36). Age and OVR are computed, not stored.
    /// </summary>
    public sealed class Player
    {
        // Identity (fixed)
        public Id Id { get; internal set; }
        public string FirstName { get; internal set; }
        public string LastName { get; internal set; }
        public string Nationality { get; internal set; }
        public BirthDate BirthDate { get; internal set; }
        public int HeightCm { get; internal set; }
        public Foot PreferredFoot { get; internal set; }
        /// <summary>Weak foot 1-5.</summary>
        public int WeakFoot { get; internal set; }
        public ulong AvatarSeed { get; internal set; }

        // Attributes, potential, positions
        internal int[] AttributeValues;
        public IReadOnlyList<int> Attributes => AttributeValues;
        public ReadOnlySpan<int> AttributeSpan => AttributeValues;
        /// <summary>Hidden potential 40-99; shown to the user only as a scouting range (B5).</summary>
        public int Potential { get; internal set; }
        public Position MainPosition { get; internal set; }
        internal Position[] SecondaryPositionValues;
        public IReadOnlyList<Position> SecondaryPositions => SecondaryPositionValues;
        public ReadOnlySpan<Position> SecondaryPositionSpan => SecondaryPositionValues;

        public string Name => FirstName + " " + LastName;

        /// <summary>Career condition (B4): energy, morale, form, injury, suspensions, season statistics.</summary>
        public PlayerCondition Condition { get; internal set; } = new PlayerCondition();
    }

    /// <summary>
    /// PlayerCondition block (TECHNICAL_SPEC §4.2): energy, morale, form, active injury, suspensions and yellows per
    /// competition, plus the season counters used by development (minutes) and the annual adjustment (ratings).
    /// </summary>
    public sealed class PlayerCondition
    {
        public const int Competitions = 2; // indexed by Season.CompetitionKind (League, Cup)

        /// <summary>Energy 0-100 at <see cref="EnergyDate"/> (recovers with the days elapsed).</summary>
        public float Energy { get; internal set; } = 100f;
        public System.DateTime? EnergyDate { get; internal set; }
        /// <summary>Morale 1-5 (3 = neutral).</summary>
        public int Morale { get; internal set; } = 3;
        internal readonly List<float> RecentRatingValues = new List<float>();
        public IReadOnlyList<float> RecentRatings => RecentRatingValues;

        public Game.Core.Contracts.Match.InjurySeverity Injury { get; internal set; }
        /// <summary>Club matches still to miss (light/medium injuries).</summary>
        public int InjuryMatchesLeft { get; internal set; }
        /// <summary>Unavailable until this date (severe injuries).</summary>
        public System.DateTime? InjuredUntil { get; internal set; }

        internal readonly int[] YellowCount = new int[Competitions];
        internal readonly int[] SuspendedMatchCount = new int[Competitions];
        public int Yellows(int competition) => YellowCount[competition];
        public int SuspendedMatches(int competition) => SuspendedMatchCount[competition];

        public int SeasonAppearances { get; internal set; }
        public int SeasonMinutes { get; internal set; }
        public int SeasonGoals { get; internal set; }
        public float SeasonRatingSum { get; internal set; }
        /// <summary>Share of the club's possible minutes played last season (fallback early in a season).</summary>
        public float PreviousMinutesShare { get; internal set; }
        /// <summary>Accumulated development in OVR points; applied in whole points.</summary>
        public float DevelopmentProgress { get; internal set; }

        /// <summary>Form = average of the last ratings (GAME_DESIGN §9); null without ratings.</summary>
        public float? Form
        {
            get
            {
                if (RecentRatingValues.Count == 0) return null;
                float sum = 0f;
                foreach (var r in RecentRatingValues) sum += r;
                return sum / RecentRatingValues.Count;
            }
        }

        public bool IsInjured(System.DateTime date) => InjuryMatchesLeft > 0 || (InjuredUntil.HasValue && date < InjuredUntil.Value);
    }

    /// <summary>
    /// Player-club link (§4.1 "Player - Club via Contract"). Terms (B5, X-43): season wage in fictional R$ and the
    /// years covered (inclusive). A player with no <see cref="Contract"/> in <see cref="WorldState"/> is a free agent.
    /// </summary>
    public sealed class Contract
    {
        public Id Id { get; internal set; }
        public Id PlayerId { get; internal set; }
        public Id ClubId { get; internal set; }
        public long Wage { get; internal set; }
        public int StartYear { get; internal set; }
        /// <summary>Last season year covered by the contract (inclusive).</summary>
        public int EndYear { get; internal set; }
        public int YearsLeft(int year) => Math.Max(0, EndYear - year + 1);
    }

    /// <summary>The world: part of the CareerState. References are Ids only. Squads change from B4 (youth, retirements).</summary>
    public sealed class WorldState
    {
        public ulong Seed { get; internal set; }
        public int StartYear { get; internal set; }
        /// <summary>Last Id issued (Ids are global across entity types and never reused).</summary>
        public int LastIssuedId { get; internal set; }
        public IReadOnlyList<string> DivisionNames { get; internal set; }
        public IReadOnlyList<Club> Clubs { get; internal set; }
        internal List<Player> PlayerList = new List<Player>();
        internal List<Contract> ContractList = new List<Contract>();
        internal List<Manager> ManagerList = new List<Manager>();
        internal List<StaffMember> StaffList = new List<StaffMember>();
        internal List<FacilityProject> FacilityProjectList = new List<FacilityProject>();
        public IReadOnlyList<Player> Players => PlayerList;
        public IReadOnlyList<Contract> Contracts => ContractList;
        public IReadOnlyList<Manager> Managers => ManagerList;
        public IReadOnlyList<StaffMember> StaffMembers => StaffList;
        public IReadOnlyList<FacilityProject> FacilityProjects => FacilityProjectList;

        public Manager ManagerOf(Id clubId) { foreach (var m in ManagerList) if (m.ClubId == clubId) return m; return null; }
        public StaffMember StaffOf(Id clubId, StaffRole role) { foreach (var s in StaffList) if (s.ClubId == clubId && s.Role == role) return s; return null; }
        public FacilityProject ActiveProjectOf(Id clubId, FacilityType type)
        { foreach (var p in FacilityProjectList) if (p.ClubId == clubId && p.Type == type) return p; return null; }

        /// <summary>Age on 1 January of the start year (season = calendar year, GAME_DESIGN §4).</summary>
        public int AgeAtStart(Player p) => p.BirthDate.AgeOn(StartYear, 1, 1);

        private Dictionary<Id, List<Player>> _squads;
        private Dictionary<Id, Id> _clubOf;
        private Dictionary<Id, Contract> _contractOf;

        internal void AddPlayer(Player player, Contract contract)
        {
            PlayerList.Add(player);
            if (contract != null) ContractList.Add(contract);
            LastIssuedId = Math.Max(LastIssuedId, Math.Max(player.Id.Value, contract?.Id.Value ?? 0));
            InvalidateIndex();
        }

        internal void RemovePlayer(Id playerId)
        {
            PlayerList.RemoveAll(p => p.Id == playerId);
            ContractList.RemoveAll(c => c.PlayerId == playerId);
            InvalidateIndex();
        }

        /// <summary>Moves (or signs) a player onto <see cref="Contract.ClubId"/>, replacing any existing contract (B5).</summary>
        internal void SetContract(Contract contract)
        {
            ContractList.RemoveAll(c => c.PlayerId == contract.PlayerId);
            ContractList.Add(contract);
            LastIssuedId = Math.Max(LastIssuedId, contract.Id.Value);
            InvalidateIndex();
        }

        /// <summary>Ends a player's contract: the player becomes a free agent (B5), still part of <see cref="Players"/>.</summary>
        internal void ReleasePlayer(Id playerId)
        {
            ContractList.RemoveAll(c => c.PlayerId == playerId);
            InvalidateIndex();
        }

        private void InvalidateIndex() { _squads = null; _clubOf = null; _contractOf = null; }

        /// <summary>Players under contract with the club (index rebuilt after squad changes).</summary>
        public IReadOnlyList<Player> SquadOf(Id clubId)
        {
            if (_squads == null) BuildIndex();
            return _squads.TryGetValue(clubId, out var squad) ? squad : (IReadOnlyList<Player>)Array.Empty<Player>();
        }

        public Id ClubOf(Id playerId)
        {
            if (_clubOf == null) BuildIndex();
            return _clubOf.TryGetValue(playerId, out var c) ? c : Id.None;
        }

        /// <summary>The player's current contract, or null for a free agent (B5).</summary>
        public Contract ContractOf(Id playerId)
        {
            if (_contractOf == null) BuildIndex();
            return _contractOf.TryGetValue(playerId, out var c) ? c : null;
        }

        /// <summary>Players with no active contract (B5 free agents).</summary>
        public IReadOnlyList<Player> FreeAgents()
        {
            if (_clubOf == null) BuildIndex();
            var result = new List<Player>();
            foreach (var p in PlayerList) if (!_clubOf.ContainsKey(p.Id)) result.Add(p);
            return result;
        }

        private void BuildIndex()
        {
            var byId = new Dictionary<Id, Player>();
            foreach (var p in PlayerList) byId[p.Id] = p;
            var squads = new Dictionary<Id, List<Player>>();
            var clubOf = new Dictionary<Id, Id>();
            var contractOf = new Dictionary<Id, Contract>();
            foreach (var c in ContractList)
            {
                if (!squads.TryGetValue(c.ClubId, out var list)) squads[c.ClubId] = list = new List<Player>();
                list.Add(byId[c.PlayerId]);
                clubOf[c.PlayerId] = c.ClubId;
                contractOf[c.PlayerId] = c;
            }
            _squads = squads;
            _clubOf = clubOf;
            _contractOf = contractOf;
        }
    }
}
