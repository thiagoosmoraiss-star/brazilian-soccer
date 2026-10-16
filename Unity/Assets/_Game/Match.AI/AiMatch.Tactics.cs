using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// Tactics during the match (A9c; TECHNICAL_SPEC §9 "Durante a partida"; GAME_DESIGN §27): mentality, line and
    /// pressure change at once and the block eases in (<see cref="TacticsRuntime"/>); a new formation re-maps the players
    /// to its slots by least displacement and position fit. Called from the pause, never per step (it allocates).
    /// </summary>
    public sealed partial class AiMatch
    {
        private readonly Game.Data.Loading.GameDatabase _db;

        public TacticSetup TacticOf(MatchSide side) => (side == MatchSide.Home ? Home : Away).Runtime.Setup;

        /// <summary>Sets <paramref name="side"/>'s tactic now. False (nothing changes) for an unknown formation.</summary>
        public bool SetTactic(MatchSide side, TacticSetup tactic)
        {
            if (tactic == null) throw new ArgumentNullException(nameof(tactic));
            var team = side == MatchSide.Home ? Home : Away;
            var formation = _db.Formation(tactic.FormationId);
            if (formation == null) return false;
            if (formation.Id != team.Formation.Id) ChangeFormation(team, formation);
            team.Runtime.Apply(tactic, immediate: false);
            return true;
        }

        /// <summary>Re-maps the players to <paramref name="formation"/>'s slots: the keeper keeps the GK slot; the
        /// outfield players are matched greedily by the cheapest (normalized distance between old and new slot + a
        /// penalty when the slot is neither his position nor his old role).</summary>
        private void ChangeFormation(AiTeam team, FormationDefinition formation)
        {
            var slots = formation.Slots;
            var used = new bool[slots.Count];
            var placed = new bool[team.Players.Length];
            var pairs = new List<(float cost, int player, int slot)>();
            float penalty = _db.Tactics.Modifiers.FormationRolePenalty;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    bool gkSlot = slot.Role == FormationRole.GK;
                    if (gkSlot != p.IsGoalkeeper) continue;
                    float dx = slot.X - p.Slot.X, dy = slot.Y - p.Slot.Y;
                    float cost = MathF.Sqrt(dx * dx + dy * dy);
                    if (!Fits(p.Setup, slot)) cost += 0.5f * penalty;
                    if (slot.Role != p.Slot.Role) cost += 0.5f * penalty;
                    pairs.Add((cost, i, s));
                }
            }
            pairs.Sort((a, b) => a.cost != b.cost ? a.cost.CompareTo(b.cost) : a.player != b.player ? a.player.CompareTo(b.player) : a.slot.CompareTo(b.slot));
            foreach (var (_, player, slot) in pairs)
            {
                if (placed[player] || used[slot]) continue;
                team.Players[player].Slot = slots[slot];
                placed[player] = used[slot] = true;
            }
            team.Formation = formation;
            team.UpdateSlotExtent();
        }

        private static bool Fits(MatchPlayerSetup player, FormationSlot slot)
        {
            int pos = (int)slot.Position;
            if (player.MainPosition == pos) return true;
            foreach (int s in player.SecondaryPositions) if (s == pos) return true;
            return false;
        }

        /// <summary>Index of each player's slot in his side's current formation (snapshot).</summary>
        private static int SlotIndex(AiPlayer p)
        {
            var slots = p.Team.Formation.Slots;
            for (int s = 0; s < slots.Count; s++) if (ReferenceEquals(slots[s], p.Slot)) return s;
            return p.Local;
        }

        private void WriteTactics(System.IO.BinaryWriter w)
        {
            foreach (var team in new[] { Home, Away })
            {
                var t = team.Runtime.Setup;
                w.Write(t.FormationId);
                w.Write(t.Mentality);
                w.Write(t.DefensiveLine);
                w.Write(t.Pressure);
                var r = team.Runtime;
                for (int i = 0; i < TeamPhaseInfo.Count; i++) { w.Write(r.DepthState[i]); w.Write(r.BehindState[i]); w.Write(r.MaxState[i]); }
                w.Write(r.MinState);
                for (int i = 0; i < team.Players.Length; i++) w.Write(SlotIndex(team.Players[i]));
            }
        }

        private void ReadTactics(System.IO.BinaryReader rd)
        {
            foreach (var team in new[] { Home, Away })
            {
                var tactic = new TacticSetup(rd.ReadString(), rd.ReadInt32(), rd.ReadInt32(), rd.ReadInt32());
                var formation = _db.Formation(tactic.FormationId) ?? throw new ArgumentException("Unknown formation in the snapshot: " + tactic.FormationId);
                team.Formation = formation;
                team.Runtime.Apply(tactic, immediate: true);
                var r = team.Runtime;
                for (int i = 0; i < TeamPhaseInfo.Count; i++) { r.DepthState[i] = rd.ReadSingle(); r.BehindState[i] = rd.ReadSingle(); r.MaxState[i] = rd.ReadSingle(); }
                r.MinState = rd.ReadSingle();
                for (int i = 0; i < team.Players.Length; i++) team.Players[i].Slot = formation.Slots[rd.ReadInt32()];
                team.UpdateSlotExtent();
            }
        }
    }
}
