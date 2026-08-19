using System.Globalization;

namespace Skyscraper.Config
{
    /// Culture-invariant number parsing.
    /// The tables use '.' as the decimal separator; on a machine with a comma
    /// locale the default float.Parse would silently mangle "1.33" into 133.
    public static class Num
    {
        const NumberStyles FS = NumberStyles.Float | NumberStyles.AllowThousands;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool F(string s, out float v) => float.TryParse(s, FS, Inv, out v);
        public static bool D(string s, out double v) => double.TryParse(s, FS, Inv, out v);
        public static bool I(string s, out int v)
        {
            // some columns store integers as "10.0"
            if (int.TryParse(s, NumberStyles.Integer, Inv, out v)) return true;
            if (double.TryParse(s, FS, Inv, out var d)) { v = (int)d; return true; }
            return false;
        }

        public static float F(string s, float fallback = 0f) => F(s, out var v) ? v : fallback;
        public static int I(string s, int fallback = 0) => I(s, out var v) ? v : fallback;
    }
}
