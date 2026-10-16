using System;
using System.Globalization;
using Game.Data.Strings;

namespace Game.UI.Presenters
{
    /// <summary>Number and date formatting for the screens (PT-BR: decimal comma, dd/MM/yyyy); units come from the
    /// string table.</summary>
    public static class Fmt
    {
        /// <summary>Money in the game's currency: "R$ 1,2 mi", "R$ 850 mil", "R$ 900" (negative with a minus sign).</summary>
        public static string Money(StringTable s, long value)
        {
            string sign = value < 0 ? "−" : "";
            long abs = Math.Abs(value);
            if (abs >= 1_000_000) return sign + s.Format("fmt.money_millions", Decimal(abs / 1_000_000.0, abs >= 10_000_000 ? 1 : 2));
            if (abs >= 1_000) return sign + s.Format("fmt.money_thousands", Decimal(abs / 1_000.0, 0));
            return sign + s.Format("fmt.money_units", abs.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Decimal comma, no thousands separator, trailing zeros trimmed.</summary>
        public static string Decimal(double value, int decimals)
        {
            string text = Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals), CultureInfo.InvariantCulture);
            return text.Replace('.', ',');
        }

        /// <summary>Thousands with a dot: 12.500.</summary>
        public static string Thousands(long value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

        public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string ShortDate(DateTime d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);

        public static string Month(StringTable s, int month) => s.Get("month." + month);

        /// <summary>"+3", "0", "−2".</summary>
        public static string Signed(int value) => value > 0 ? "+" + value : value < 0 ? "−" + (-value) : "0";

        /// <summary>One decimal, for ratings: "6,8".</summary>
        public static string Rating(float value) => Decimal(value, 1);

        public static string Stars(int stars, int max = 5)
        {
            stars = Math.Max(0, Math.Min(max, stars));
            return new string('★', stars) + new string('☆', max - stars);
        }
    }
}
