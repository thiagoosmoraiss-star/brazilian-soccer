namespace Game.Data.Effects
{
    /// <summary>
    /// Source of a modifier. The enum order IS the fixed application order (TECHNICAL_SPEC §8):
    /// modifiers are always applied Energy, Morale, Form, OutOfPosition, Trait - regardless of input order.
    /// </summary>
    public enum ModifierSource
    {
        Energy = 0,
        Morale = 1,
        Form = 2,
        OutOfPosition = 3,
        Trait = 4,
    }

    /// <summary>Whether the modifier acts on the attribute input (before the curve) or on the curve output.</summary>
    public enum ModifierStage
    {
        PreCurve = 0,
        PostCurve = 1,
    }

    public enum ModifierOp
    {
        Multiply = 0,
        Add = 1,
    }

    /// <summary>
    /// Runtime modifier passed to <see cref="Balance.Eval"/>. Its value is produced by the owning system
    /// (energy, morale, ...) from data; this type only defines how it is applied.
    /// </summary>
    public readonly struct Modifier
    {
        public readonly ModifierSource Source;
        public readonly ModifierStage Stage;
        public readonly ModifierOp Op;
        public readonly float Value;

        public Modifier(ModifierSource source, ModifierStage stage, ModifierOp op, float value)
        {
            Source = source; Stage = stage; Op = op; Value = value;
        }
    }
}
