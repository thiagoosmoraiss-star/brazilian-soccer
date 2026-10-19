using System.Collections.Generic;

namespace Game.UI.Presenters
{
    // C3b screen models: squad, player profile, lineup and tactic.

    /// <summary>One line of the squad list: a section title, or a player.</summary>
    public sealed class SquadRowModel
    {
        /// <summary>Section title ("Goleiros"…); null for a player row.</summary>
        public string Section { get; internal set; }
        public int PlayerId { get; internal set; }
        public string Pos { get; internal set; }
        public string Name { get; internal set; }
        public string Age { get; internal set; }
        public string Ovr { get; internal set; }
        public string Energy { get; internal set; }
        /// <summary>"Titular", "Banco", or the reason he cannot play ("Lesionado"…); may be empty.</summary>
        public string Status { get; internal set; }
        /// <summary>True when the status is a problem (injured, suspended).</summary>
        public bool StatusBad { get; internal set; }
    }

    public sealed class SquadModel
    {
        public HeaderModel Header { get; internal set; }
        public string Title { get; internal set; }
        public string Summary { get; internal set; }
        public ButtonModel Lineup { get; internal set; }
        public string ColumnsHeader { get; internal set; }
        public List<SquadRowModel> Rows { get; } = new List<SquadRowModel>();
    }

    public sealed class LabelValueModel
    {
        public string Label { get; internal set; }
        public string Value { get; internal set; }
        /// <summary>0-1 for a bar (attributes), or a negative value for none.</summary>
        public float Bar { get; internal set; } = -1f;
    }

    public sealed class InfoGroupModel
    {
        public string Title { get; internal set; }
        public List<LabelValueModel> Rows { get; } = new List<LabelValueModel>();
    }

    public sealed class PlayerModel
    {
        public HeaderModel Header { get; internal set; }
        public ButtonModel Back { get; internal set; }
        public string Name { get; internal set; }
        public string Positions { get; internal set; }
        public string Ovr { get; internal set; }
        public string Potential { get; internal set; }
        public string Status { get; internal set; }
        public bool StatusBad { get; internal set; }
        public List<InfoGroupModel> Groups { get; } = new List<InfoGroupModel>();
    }

    /// <summary>One tactic control: ◀ value ▶.</summary>
    public sealed class StepperModel
    {
        public string Label { get; internal set; }
        public string Value { get; internal set; }
        public bool CanPrevious { get; internal set; }
        public bool CanNext { get; internal set; }
    }

    public enum TacticControl { Formation, Mentality, DefensiveLine, Pressure }

    public enum LineupArea { Starter, Bench, Reserve }

    public sealed class LineupSlotModel
    {
        public string Pos { get; internal set; }
        public string Name { get; internal set; }
        public string Ovr { get; internal set; }
        public string Energy { get; internal set; }
        /// <summary>Why this choice is a problem (cannot play, out of position), or null.</summary>
        public string Warning { get; internal set; }
        public bool Selected { get; internal set; }
        public bool Empty { get; internal set; }
    }

    public sealed class LineupModel
    {
        public string Title { get; internal set; }
        public ButtonModel Back { get; internal set; }
        public string NextMatch { get; internal set; }
        public List<StepperModel> Tactic { get; } = new List<StepperModel>();
        public string Mode { get; internal set; }
        public ButtonModel Automatic { get; internal set; }
        public string Hint { get; internal set; }
        public string StartersTitle { get; internal set; }
        public List<LineupSlotModel> Starters { get; } = new List<LineupSlotModel>();
        public string BenchTitle { get; internal set; }
        public List<LineupSlotModel> Bench { get; } = new List<LineupSlotModel>();
        public string ReservesTitle { get; internal set; }
        public List<LineupSlotModel> Reserves { get; } = new List<LineupSlotModel>();
    }
}
