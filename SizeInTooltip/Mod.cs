using System;
using System.Reflection;
using HarmonyLib;
using KMod;
using STRINGS;
using UnityEngine;

using PeterHan.PLib.Core;

// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (DragTool, HoverTextConfiguration, HoverTextDrawer, Grid) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.LogDebug("[SizeInTooltip] Build <date> commit=<hash>");

            // The prefix is attached PROGRAMMATICALLY (no [HarmonyPatch] attributes):
            // base.OnLoad runs Harmony's PatchAll, which only applies types carrying
            // HarmonyPatch attributes (same rationale as BuildDoorOverWall/Mod.cs).
            //
            // TARGET: HoverTextConfiguration.DrawInstructions(HoverTextScreen,
            // HoverTextDrawer) (HoverTextConfiguration.cs:72) — the shared
            // mouse-button hint line («(ЛКМ) ПЕРЕТАЩИТЬ (ПКМ) НАЗАД»). Every tool
            // card builds the tooltip through this one method: the base
            // UpdateHoverElements (HoverTextConfiguration.cs:119-120) and every
            // per-tool override (DigToolHoverTextCard.cs:40-41,
            // CancelToolHoverTextCard, DeconstructToolHoverTextCard,
            // BuildToolHoverTextCard, MopToolHoverTextCard, HarvestToolHoverTextCard,
            // PrioritizeToolHoverTextCard, EmptyPipeToolHoverTextCard,
            // PlaceToolHoverTextCard, MoveToLocationToolHoverTextCard,
            // AttackToolHoverTextCard, SandboxStoryTraitToolHoverTextCard), so a
            // single prefix covers ALL drag-rectangle tools (Dig, Demolish,
            // Cancel, and the rest — every DragTool subclass).
            //
            // As a PREFIX it runs right where the title was already drawn
            // (DrawTitle immediately before it in every card) and before the
            // mouse-button line — exactly the spec layout:
            //   КОМАНДА "КОПАТЬ" / 15 x 4 / 60 (в клетках) / (ЛКМ) ПЕРЕТАЩИТЬ (ПКМ) НАЗАД
            // (the numeric size lines only — the «РАЗМЕР:» header was dropped by
            // user decision after in-game acceptance: no hardcoded RU text → no
            // i18n issue in non-RU locales)
            //
            // DrawInstructions is protected in the game source, but the game
            // assembly is publicized at build time (Directory.Build.props
            // Publicize=true), so it is public in the compiled reference and
            // AccessTools resolves it without any reflection flags.
            MethodInfo drawInstructions = AccessTools.Method(typeof(HoverTextConfiguration), nameof(HoverTextConfiguration.DrawInstructions), new[] { typeof(HoverTextScreen), typeof(HoverTextDrawer) });
            if (drawInstructions == null)
            {
                PUtil.LogError("[SizeInTooltip] could not resolve HoverTextConfiguration.DrawInstructions(HoverTextScreen, HoverTextDrawer) — patch skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(drawInstructions, prefix: new HarmonyMethod(typeof(HoverTextConfiguration_DrawInstructions_SizeInTooltip__Patch), nameof(HoverTextConfiguration_DrawInstructions_SizeInTooltip__Patch.Prefix)));
                PUtil.LogDebug("[SizeInTooltip] size prefix attached to {0}".F(drawInstructions));
            }
        }
    }

    // Draws the numeric size lines into the cursor tooltip while a drag-rectangle
    // tool is dragging (between the command title and the mouse-button hint, no
    // header line — the «РАЗМЕР:» header was dropped by user decision after
    // in-game acceptance: hardcoded RU text would be an i18n problem in
    // non-RU locales). Attached programmatically in Mod.OnLoad (no attributes).
    //
    // Data source — the SAME state the game's in-rectangle label is computed
    // from (DragTool.OnMouseMove Box/Line branch, DragTool.cs:366-399):
    //  - DragTool.Dragging — public property over private `dragging` (DragTool.cs:44, 62);
    //    the rectangle exists only between LMB down and up (DragTool.cs:125-155, 214-243).
    //  - DragTool.GetMode() — protected virtual (DragTool.cs:64), public in the
    //    publicized reference; Brush tools (BuildTool) drag a length, not a
    //    rectangle (DragTool.cs:354-365, TOOL_LENGTH_FMT), so only Box/Line count.
    //  - areaVisualizerSpriteRenderer — protected SpriteRenderer (DragTool.cs:36),
    //    public in the publicized reference; .size holds the rectangle size in
    //    METERS (DragTool.cs:391).
    //  - W = Mathf.RoundToInt(size.x), H = Mathf.RoundToInt(size.y) — the exact
    //    rounding the game does (DragTool.cs:394).
    // The numeric line is formatted with the GAME's own localized key
    // UI.TOOLS.TOOL_AREA_FMT (STRINGS/UI.cs:13708, "{0} x {1}\n{2} tiles"), so it
    // auto-localizes and its numbers always match the in-rectangle label.
    //
    // The whole body is null-guarded and wrapped so any unexpected null/type
    // issue is a silent no-op — the mod must never throw into the game loop
    // (one PUtil error log at most).
    public static class HoverTextConfiguration_DrawInstructions_SizeInTooltip__Patch
    {
        private static bool loggedError;

        public static void Prefix(HoverTextConfiguration __instance, HoverTextScreen screen, HoverTextDrawer drawer)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                DragTool? dragTool = __instance.gameObject.GetComponent<DragTool>();
                if (dragTool == null)
                {
                    return; // non-drag tool — tooltip stays exactly as the game draws it.
                }
                if (!dragTool.Dragging)
                {
                    return; // not dragging — no rectangle exists to report.
                }
                if (dragTool.GetMode() == DragTool.Mode.Brush)
                {
                    return; // Brush tools drag a length, not a rectangle.
                }
                SpriteRenderer? areaVisualizer = dragTool.areaVisualizerSpriteRenderer;
                if (areaVisualizer == null)
                {
                    return;
                }
                Vector2 size = areaVisualizer.size; // meters
                int w = Mathf.RoundToInt(size.x);
                int h = Mathf.RoundToInt(size.y);
                if (w <= 0 || h <= 0)
                {
                    return; // rectangle not positioned yet (first frame of the drag).
                }
                // Body-text style: the same style the cards use for their
                // informational lines (e.g. DigToolHoverTextCard.cs:51-64), distinct
                // from the hint style of the mouse-button line below us.
                TextStyleSetting style = __instance.Styles_BodyText.Standard;
                string area = string.Format(UI.TOOLS.TOOL_AREA_FMT.ToString(), w, h, w * h);
                // No header line is drawn (the «РАЗМЕР:» header was dropped by user
                // decision after in-game acceptance). The loop below draws the
                // numeric size lines; its first NewLine puts the first numeric
                // line on its own line right after the title.
                // The '\n' inside TOOL_AREA_FMT: HoverTextDrawer.DrawText
                // (HoverTextDrawer.cs:197-231) has NO newline handling — it sets the
                // whole string on a single pooled LocText widget and advances x by
                // the widget's full rendered width; the drawer's line mechanism is
                // NewLine (HoverTextDrawer.cs:241). The game itself renders multi-line
                // text through this drawer by splitting on '\n' and drawing each part
                // with NewLine (PrebuildToolHoverTextCard.cs:24-33), so we do the same.
                string[] lines = area.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0)
                    {
                        continue;
                    }
                    drawer.NewLine();
                    drawer.DrawText(lines[i], style);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("[SizeInTooltip] unexpected failure in DrawInstructions prefix, size line suppressed: " + e);
                }
            }
        }
    }
}
