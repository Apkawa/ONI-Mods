using System;
using HarmonyLib;
using UnityEngine;

using PeterHan.PLib.Core;

// Same namespace as Mod.cs so unqualified game types (HoverTextDrawer, Grid,
// HoverTextHelper, TextStyleSetting) resolve through the `OxygenNotIncluded`
// walk-up without extra usings.
namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Harmony PREFIX (priority 1001, above Better Info Cards'
    /// <c>HarmonyPriority.First</c> = 1000) for
    /// <see cref="HoverTextDrawer.NewLine(int)"/> — the drawing slot for the
    /// field-info tooltip lines.
    ///
    /// The element card (SelectToolHoverTextCard.cs lines 681-744) draws, in
    /// order: title → disease row → category row → mass row(s) →
    /// (if not vacuum) NewLine + DrawIcon(iconDash) + DrawText(temperature) →
    /// space/buried rows. The insertion point is the NewLine that follows the
    /// mass row — uniquely identifiable because nothing else is drawn between
    /// the mass row and that NewLine, and the last text drawn just before it
    /// (recorded by <see cref="FieldInfoDrawTextPatch"/> via
    /// <see cref="FieldInfoTooltipPatch.NoteText"/>) equals the final element
    /// of <c>HoverTextHelper.MassStringsReadOnly(cell)</c> (the mass row is
    /// up to four consecutive DrawText calls with no NewLine between —
    /// SelectToolHoverTextCard.cs:704-713). On that NewLine the pending state
    /// staged by <see cref="FieldInfoTooltipPatch.Postfix"/> is consumed and
    /// the two field-info rows are inserted between the mass and temperature
    /// rows, inside the shadow bar with dash icons.
    ///
    /// The void prefix cannot skip the original — after the insertion, the
    /// card's own temperature NewLine/DrawIcon/DrawText run normally, so no
    /// empty dash row appears and nothing is glued onto our rows. With Better
    /// Info Cards installed, our re-entrant drawer calls are recorded by BIC's
    /// priority-1000 prefixes (which run after ours, 1001) BEFORE the card's
    /// temperature-row actions, so BIC's re-render draws our rows between
    /// mass and temperature. Without BIC the same calls draw live, in place.
    /// Re-entrancy is safe: the pending state is cleared before the re-entrant
    /// calls, so they neither re-trigger the insertion nor record anything.
    /// </summary>
    [HarmonyPatch(typeof(HoverTextDrawer), "NewLine", new[] { typeof(int) })]
    [HarmonyPriority(1001)]
    public static class FieldInfoNewLinePatch
    {
        // One-shot flag: a failure is logged once and then swallowed silently —
        // the tooltip must never break the game.
        private static bool s_errorLogged;

        public static void Prefix(HoverTextDrawer __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!FieldInfoTooltipPatch.TryPeekPending(out int cell, out int count, out float mass)) return;
                string expected = ExpectedLastMassText(cell);
                if (expected == null || FieldInfoTooltipPatch.LastText != expected) return;
                FieldInfoTooltipPatch.ClearPending();
                FieldInfoTooltipPatch.DrawLines(__instance, cell, count, mass, "new-line");
            }
            catch (Exception e)
            {
                if (!s_errorLogged)
                {
                    s_errorLogged = true;
                    PUtil.LogError("Tooltip NewLine prefix failed: " + e);
                }
            }
        }

        // The last text the element card draws on its mass row: the final
        // element of HoverTextHelper.MassStringsReadOnly(cell) (all elements
        // are drawn consecutively, no NewLine between —
        // SelectToolHoverTextCard.cs:704-713).
        private static string ExpectedLastMassText(int cell)
        {
            string[] arr = HoverTextHelper.MassStringsReadOnly(cell);
            return arr.Length > 0 ? arr[arr.Length - 1] : null;
        }
    }
}
