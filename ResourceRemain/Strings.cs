// The string class lives in namespace STRINGS so it resolves as
// STRINGS.RESOURCE_REMAIN.<FIELD>. Everything else the mod localizes comes from
// the GAME's own keys (STRINGS.UI.FORMATDAY,
// STRINGS.DUPLICANTS.STATS.SUBJECTS.*), so no .po files are needed: the game's
// OverloadStrings (docs/i18n.md, step 1) has already replaced those LocString
// fields with the active-language text before mod code runs.
using System;

namespace STRINGS
{
    /// <summary>
    /// UI strings for Resource Remain. Only the "(-)" placeholder remains
    /// mod-local (identical in every language); all plural wording uses
    /// game-localized keys.
    /// </summary>
    public class RESOURCE_REMAIN
    {
        public const string NONE = "(-)";
    }
}

namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Localized plural helpers for the tooltip text. The duplicant word comes
    /// from the game's subject keys; English and Russian share the same
    /// selection rule: n == 1 → singular; last digit 2-4 (not 12-14) →
    /// possessive; otherwise → plural.
    /// </summary>
    public static class CycleText
    {
        private static LocString DupWord(int n)
        {
            int last = n % 10;
            int last2 = n % 100;
            if (n == 1)
            {
                return STRINGS.DUPLICANTS.STATS.SUBJECTS.DUPLICANT;
            }
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14))
            {
                return STRINGS.DUPLICANTS.STATS.SUBJECTS.DUPLICANT_POSSESSIVE;
            }
            return STRINGS.DUPLICANTS.STATS.SUBJECTS.DUPLICANT_PLURAL;
        }

        /// <summary>
        /// Formats "N cycles" using the game key STRINGS.UI.FORMATDAY
        /// (EN "{0:F1} cycles", RU "Циклов: {0:F1}"). The count is passed as a
        /// string: string.Format ignores format specifiers on string arguments,
        /// so "{0:F1}" renders as "100" (no ".0" decimal) → EN "100 cycles",
        /// RU "Циклов: 100".
        /// </summary>
        public static string Cycles(int n)
        {
            return string.Format(STRINGS.UI.FORMATDAY.ToString(), n.ToString());
        }

        /// <summary>Formats "N duplicant(s)" using the game subject keys.</summary>
        public static string Duplicants(int n)
        {
            return string.Format("{0} {1}", n, DupWord(n).ToString());
        }

        /// <summary>
        /// Formats "(N cycles, M duplicants)", e.g. RU
        /// "(Циклов: 100, 7 дубликанты)", EN "(100 cycles, 7 Duplicants)".
        /// </summary>
        public static string ForDups(int cycles, int dups)
        {
            return string.Format("({0}, {1})", Cycles(cycles), Duplicants(dups));
        }

        /// <summary>Plain "(-)" placeholder (identical in every language).</summary>
        public static string None
        {
            get { return STRINGS.RESOURCE_REMAIN.NONE; }
        }
    }
}
