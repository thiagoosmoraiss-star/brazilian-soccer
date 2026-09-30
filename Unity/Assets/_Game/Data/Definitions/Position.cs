namespace Game.Data.Definitions
{
    /// <summary>Player positions (GAME_DESIGN §9). Names are the documented codes and the JSON keys.</summary>
    public enum Position
    {
        GOL = 0,
        ZAG = 1,
        LD = 2,
        LE = 3,
        VOL = 4,
        MC = 5,
        MEI = 6,
        PD = 7,
        PE = 8,
        ATA = 9,
    }

    public static class PositionInfo
    {
        public const int Count = 10;
    }

    /// <summary>The 27 Brazilian federative units (fact, not balance). Clubs are fictional; the UF is real (X-02).</summary>
    public static class Uf
    {
        public static readonly string[] All =
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
            "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
        };

        public static bool IsValid(string code) => System.Array.IndexOf(All, code) >= 0;
    }
}
