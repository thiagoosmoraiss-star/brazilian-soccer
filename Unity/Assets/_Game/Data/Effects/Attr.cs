namespace Game.Data.Effects
{
    /// <summary>
    /// The 18 player attributes (X-11; order as in GAME_DESIGN §9). Values are integers 1–99.
    /// Player attributes are stored in an array indexed by this enum.
    /// </summary>
    public enum Attr
    {
        Speed = 0,          // Velocidade
        Agility = 1,        // Agilidade
        Stamina = 2,        // Resistência
        Strength = 3,       // Força
        Passing = 4,        // Passe
        Crossing = 5,       // Cruzamento
        Dribbling = 6,      // Drible
        Finishing = 7,      // Finalização
        LongShots = 8,      // Chute de longe
        Heading = 9,        // Cabeceio
        Tackling = 10,      // Desarme
        Vision = 11,        // Visão
        Positioning = 12,   // Posicionamento
        Composure = 13,     // Compostura
        GkReflexes = 14,    // Reflexo (GK)
        GkPositioning = 15, // Posicionamento GK
        GkAerial = 16,      // Jogo aéreo (GK)
        GkHandling = 17,    // Mãos (GK)
    }

    public static class AttrInfo
    {
        /// <summary>Number of attributes (length of an attribute array).</summary>
        public const int Count = 18;

        /// <summary>Attribute scale (rule, not balance): 1–99.</summary>
        public const int MinValue = 1;
        public const int MaxValue = 99;

        public static bool IsValid(Attr attr) => (int)attr >= 0 && (int)attr < Count;
    }
}
