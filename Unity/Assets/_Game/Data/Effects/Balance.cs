using System;

namespace Game.Data.Effects
{
    /// <summary>
    /// Typed entry point for effects: <c>Balance.Eval(Effect.X, attributes)</c>. No system computes an
    /// effect by reading an attribute directly.
    /// </summary>
    public sealed class Balance
    {
        private readonly BalanceCatalog _catalog;

        public Balance(BalanceCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (catalog.Schema.Count != Enum.GetValues(typeof(Effect)).Length)
                throw new ArgumentException("Catalog schema does not match the Effect enum.", nameof(catalog));
        }

        public float Eval(Effect effect, ReadOnlySpan<int> attributes) =>
            _catalog.Eval((int)effect, attributes);

        public float Eval(Effect effect, ReadOnlySpan<int> attributes, ReadOnlySpan<Modifier> modifiers) =>
            _catalog.Eval((int)effect, attributes, modifiers);
    }
}
