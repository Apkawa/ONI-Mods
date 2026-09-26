using System;
using HarmonyLib;
using UnityEngine;

using PeterHan.PLib.Core;

// Same namespace as Mod.cs so unqualified game types (HoverTextDrawer, Element,
// Grid, GameUtil, TextStyleSetting) resolve through the `OxygenNotIncluded`
// walk-up without extra usings.
namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Harmony PREFIX (priority 1001, above Better Info Cards'
    /// <c>HarmonyPriority.First</c> = 1000) for the 4-arg
    /// <see cref="HoverTextDrawer.DrawText"/> — a pure recorder. It never draws
    /// and never skips (void prefix): it only feeds
    /// <see cref="FieldInfoTooltipPatch.NoteText"/> with the text being drawn,
    /// so <see cref="FieldInfoNewLinePatch"/> can recognize the NewLine that
    /// follows the mass row (the last text drawn before it).
    ///
    /// The element card (SelectToolHoverTextCard.cs lines 681-744) draws, in
    /// order: title → disease row → category row → mass row(s) →
    /// (if not vacuum) NewLine + DrawIcon(iconDash) + DrawText(temperature) →
    /// space/buried rows. The mass row is up to four consecutive
    /// <c>DrawText</c> calls over
    /// <c>HoverTextHelper.MassStringsReadOnly(cell)</c> with no NewLine
    /// between (SelectToolHoverTextCard.cs:704-713), so the NewLine right
    /// after the last mass text is exactly the one preceding the temperature
    /// row.
    ///
    /// The card always calls the 2-arg <c>DrawText(text, style)</c> overload
    /// (HoverTextDrawer.cs:228-231), which delegates to this 4-arg method —
    /// so one prefix here catches every card text call.
    ///
    /// The priority is what makes the recording reliable: with Better Info
    /// Cards installed, this prefix (1001) fires BEFORE BIC's DrawText prefix
    /// (1000), so the text is observed even in BIC's intercept mode where the
    /// original call is skipped. Without BIC the same calls draw live, in
    /// place. A void prefix cannot skip the original — after this returns,
    /// the card's row is drawn normally.
    /// </summary>
    [HarmonyPatch(typeof(HoverTextDrawer), "DrawText",
        new[] { typeof(string), typeof(TextStyleSetting), typeof(Color), typeof(bool) })]
    [HarmonyPriority(1001)]
    public static class FieldInfoDrawTextPatch
    {
        // One-shot flag: a failure is logged once and then swallowed silently —
        // the tooltip must never break the game.
        private static bool s_errorLogged;

        public static void Prefix(HoverTextDrawer __instance, string text)
        {
            try
            {
                FieldInfoTooltipPatch.NoteText(text);
            }
            catch (Exception e)
            {
                if (!s_errorLogged)
                {
                    s_errorLogged = true;
                    PUtil.LogError("Tooltip DrawText prefix failed: " + e);
                }
            }
        }
    }
}
