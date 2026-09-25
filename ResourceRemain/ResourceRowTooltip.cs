// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (Tag, ToolTip, Mathf, GameObject) resolve without extra usings.
using System;
using PeterHan.PLib.Core;
using UnityEngine;

namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Dynamic per-cycle stats tooltip for the pinned-resources panel rows and
    /// the "Show all resources" screen rows. Attached to the row root's
    /// GameObject — the same GameObject that carries the row's own MultiToggle
    /// button.
    ///
    /// Unity's EventSystem gives pointer enter/exit to the FIRST component
    /// implementing IPointerEnterHandler/IPointerExitHandler in component order
    /// on the hit GameObject, so the runtime-added ToolTip (always after the
    /// prefab's MultiToggle) would never be notified of the hover. Mod.cs
    /// therefore prefixes MultiToggle.OnPointerEnter/OnPointerExit to forward
    /// the event to the ToolTip on the same GameObject — see
    /// MultiToggle_OnPointerEnter_ResourceRemain__Patch in Mod.cs for the full
    /// reasoning (a full-row raycast-target child was rejected because it would
    /// swallow the row button's and the nested pin/notify buttons' clicks).
    ///
    /// <see cref="ToolTip.refreshWhileHovering"/> makes the game re-run
    /// <see cref="ToolTip.OnToolTip"/> every 0.2 s while the row is hovered
    /// (ToolTipScreen.cs:206-211 Update → ToolTip.cs:273-288
    /// UpdateWhileHovered), so the line tracks live game state.
    /// ToolTip.RebuildDynamicTooltip (ToolTip.cs:182-205) uses OnComplexToolTip
    /// only when OnToolTip is null — so a Func&lt;string&gt; callback is the
    /// correct hook.
    /// </summary>
    public static class ResourceRowTooltip
    {
        private static bool loggedError;

        /// <summary>
        /// Builds the two-line per-cycle stats tooltip:
        /// "This Cycle: +800 units (-200 units +1000 units), 100 cycles" and,
        /// once a cycle has been completed,
        /// "Last Cycle: -800 units (-200 units +1000 units), 100 cycles".
        /// The "This Cycle" line is shown whenever current stats are available
        /// (zero values included — the tooltip must stay visible while hovered);
        /// the "Last Cycle" line is ALWAYS appended together with it — before
        /// the first completed cycle it reads "Last Cycle: -" (plain dash,
        /// punctuation only, no localization). Returns
        /// <see cref="string.Empty"/> only when the sampler is not ready yet.
        /// </summary>
        public static string BuildTooltipLine(Tag resourceTag)
        {
            try
            {
                string line = string.Empty;
                if (ResourceDelta.TryGetCurrentStats(resourceTag, out ResourceDelta.CycleStats cur))
                {
                    line = BuildValueLine(
                        STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE.ToString(),
                        cur.Net, cur.Consumed, cur.Produced, resourceTag,
                        ResourceDelta.GetDeadlineCycles(cur));
                }
                if (line.Length > 0)
                {
                    if (ResourceDelta.TryGetPreviousStats(resourceTag, out ResourceDelta.PreviousCycleStats prev))
                    {
                        line += "\n" + BuildValueLine(
                            STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE.ToString(),
                            prev.Net, prev.Consumed, prev.Produced, resourceTag,
                            ResourceDelta.GetPreviousDeadlineCycles(prev));
                    }
                    else
                    {
                        // Previous cycle not accumulated yet: plain dash.
                        line += "\n" + STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE.ToString() + ": -";
                    }
                }
                return line;
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogWarning("failed to build pinned row tooltip line, line suppressed: {0}".F(e));
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// One stats line: "<header>: {±net} (-{consumed} +{produced})" plus an
        /// optional ", {N cycles}" deadline suffix. Signs are added ourselves;
        /// the amounts fed to the game formatter are always non-negative
        /// (never feed it negative floats).
        /// </summary>
        private static string BuildValueLine(string header, float net, float consumed, float produced, Tag tag, int? deadline)
        {
            string line = header + ": "
                + (net < 0f ? "-" : "+")
                + ResourceDelta.FormatAmount(tag, Mathf.Abs(net))
                + " (-"
                + ResourceDelta.FormatAmount(tag, consumed)
                + " +"
                + ResourceDelta.FormatAmount(tag, produced)
                + ")";
            if (deadline.HasValue)
            {
                line += ", " + CycleText.Cycles(deadline.Value);
            }
            return line;
        }

        /// <summary>
        /// Attaches a dynamic ToolTip to a resource row's GameObject (the row
        /// root, alongside its own MultiToggle — the enter/exit forwarding
        /// registered in Mod.cs is what makes the tooltip actually show). If
        /// the row prefab already carries a ToolTip with an existing OnToolTip
        /// callback it is kept and our line is appended below it; otherwise the
        /// callback is set to the stats line. Null-guarded; never throws.
        /// </summary>
        public static void Attach(GameObject rowGo, Tag resourceTag)
        {
            try
            {
                if (rowGo == null)
                {
                    return;
                }
                ToolTip tip = rowGo.GetComponent<ToolTip>();
                if (tip == null)
                {
                    tip = rowGo.AddComponent<ToolTip>();
                }
                tip.refreshWhileHovering = true;
                Func<string> old = tip.OnToolTip;
                if (old != null)
                {
                    tip.OnToolTip = () =>
                    {
                        string baseText = old() ?? string.Empty;
                        string extra = BuildTooltipLine(resourceTag);
                        if (string.IsNullOrEmpty(extra))
                        {
                            return baseText;
                        }
                        return string.IsNullOrEmpty(baseText) ? extra : baseText + "\n" + extra;
                    };
                }
                else
                {
                    tip.OnToolTip = () => BuildTooltipLine(resourceTag);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogWarning("failed to attach pinned row tooltip: {0}".F(e));
                }
            }
        }
    }
}
