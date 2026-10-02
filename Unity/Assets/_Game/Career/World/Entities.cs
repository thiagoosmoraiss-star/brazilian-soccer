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
        /// <summary>Season budget, baseline v1 in fictional R$ (split into wage cap/transfer funds in B6).</summary>
        public long Budget { get; internal set; }
        public Stadium Stadium { get; internal set; }
        /// <summary>Fan base size.</summary>
        public int Fans { get; internal set; }
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
    }

    /// <summary>
    /// Minimal player-club link (§4.1 "Player - Club via Contract"). Terms (wage, length, clauses) are added in B5.
    /// </summary>
    public sealed class Contract
    {
        public Id Id { get; internal set; }
        public Id PlayerId { get; internal set; }
        public Id ClubId { get; internal set; }
    }

    /// <summary>The generated world: part of the future CareerState. References are Ids only.</summary>
    public sealed class WorldState
    {
        public ulong Seed { get; internal set; }
        public int StartYear { get; internal set; }
        /// <summary>Last Id issued (Ids are global across entity types and never reused).</summary>
        public int LastIssuedId { get; internal set; }
        public IReadOnlyList<string> DivisionNames { get; internal set; }
        public IReadOnlyList<Club> Clubs { get; internal set; }
        public IReadOnlyList<Player> Players { get; internal set; }
        public IReadOnlyList<Contract> Contracts { get; internal set; }

        /// <summary>Age on 1 January of the start year (season = calendar year, GAME_DESIGN §4).</summary>
        public int AgeAtStart(Player p) => p.BirthDate.AgeOn(StartYear, 1, 1);

        private Dictionary<Id, List<Player>> _squads;

        /// <summary>Players under contract with the club (index built once; the world is immutable in B1).</summary>
        public IReadOnlyList<Player> SquadOf(Id clubId)
        {
            if (_squads == null)
            {
                var byId = new Dictionary<Id, Player>();
                foreach (var p in Players) byId[p.Id] = p;
                var squads = new Dictionary<Id, List<Player>>();
                foreach (var c in Contracts)
                {
                    if (!squads.TryGetValue(c.ClubId, out var list)) squads[c.ClubId] = list = new List<Player>();
                    list.Add(byId[c.PlayerId]);
                }
                _squads = squads;
            }
            return _squads.TryGetValue(clubId, out var squad) ? squad : (IReadOnlyList<Player>)System.Array.Empty<Player>();
        }
    }
}
