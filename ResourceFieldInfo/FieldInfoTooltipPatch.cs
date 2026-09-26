using System;
using HarmonyLib;
using STRINGS;
using UnityEngine;

using PeterHan.PLib.Core;

// Same namespace as Mod.cs so unqualified game types (Grid, PlayerController,
// ClusterManager, OverlayScreen, ...) resolve through the `OxygenNotIncluded`
// walk-up without extra usings.
namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Harmony POSTFIX for <see cref="HoverTextScreen.BeginDrawing"/>: while the
    /// default (select) tool is active, computes the resource field under the
    /// cursor and stages a pending field-info request
    /// (<see cref="TryPeekPending"/>/<see cref="ClearPending"/>).
    ///
    /// The two lines ("Клетки: N" / "Всего: Xт") are drawn IN THE MIDDLE of the
    /// element card — after the mass line, BEFORE the temperature line — as
    /// ordinary vanilla-style rows (NewLine + dash icon + text). The drawing
    /// is split over two priority-1001 prefixes:
    /// <see cref="FieldInfoDrawTextPatch"/> (a prefix on
    /// <c>HoverTextDrawer.DrawText</c>) only records the last text drawn
    /// (<see cref="NoteText"/>), and
    /// <see cref="FieldInfoNewLinePatch"/> (a prefix on
    /// <c>HoverTextDrawer.NewLine(int)</c>) performs the insertion on the
    /// NewLine that follows the mass row — identified by the last drawn text
    /// equaling the final element of
    /// <c>HoverTextHelper.MassStringsReadOnly(cell)</c> — so the two rows
    /// land between the mass and temperature rows in both cases, with and
    /// without Better Info Cards (with BIC installed, the re-entrant drawer
    /// calls run through BIC's own priority-1000 prefixes and are recorded
    /// into the current frame's card in order, so BIC's re-render draws them
    /// between the mass and temperature rows).
    ///
    /// The guard replicates the exact conditions under which
    /// <c>SelectToolHoverTextCard.UpdateHoverElements</c> draws its element data
    /// block for the hovered cell (see <see cref="ElementBlockWouldBeDrawn"/>).
    /// </summary>
    [HarmonyPatch(typeof(HoverTextScreen), "BeginDrawing")]
    public static class FieldInfoTooltipPatch
    {
        // One-shot flag: the postfix must NEVER break the game's tooltip, so a
        // failure is logged once and then swallowed silently.
        private static bool s_errorLogged;

        // One-shot flag: "postfix first run" is logged exactly once, proving the
        // patch is live even if the user never hovers over a valid cell.
        private static bool s_firstRunLogged;

        // One-shot flag: the iconDash-null warning in DrawLines is logged exactly
        // once.
        private static bool s_dashNullLogged;

        // The stats are computed only while Ctrl is held — avoids the region
        // flood-fill cost on every hover frame.
        private static readonly bool s_requireCtrl = true;

        // Pending draw request: staged by the BeginDrawing postfix, consumed
        // exactly once by FieldInfoNewLinePatch.Prefix on the NewLine that
        // follows the mass row. Reset to false at the top of every
        // BeginDrawing postfix, so a frame that never draws that NewLine
        // simply drops the pending state.
        private static bool s_pending;
        private static int s_pendingCell;
        private static int s_pendingCount;
        private static float s_pendingMass;

        // The last text drawn by any card while a field-info request is
        // pending (recorded by FieldInfoDrawTextPatch via NoteText, reset each
        // frame by the postfix). FieldInfoNewLinePatch compares it against the
        // expected last mass-row text to find the NewLine that follows the
        // mass row.
        private static string s_lastText;

        // Report the pending state WITHOUT clearing it.
        internal static bool TryPeekPending(out int cell, out int cellCount, out float totalMass)
        {
            bool had = s_pending;
            cell = s_pendingCell;
            cellCount = s_pendingCount;
            totalMass = s_pendingMass;
            return had;
        }

        // Clear the pending state. Called by FieldInfoNewLinePatch.Prefix before
        // its re-entrant drawer calls (so re-entrancy sees no pending state
        // and NoteText records nothing).
        internal static void ClearPending()
        {
            s_pending = false;
        }

        // Records the last text drawn by any card while a field-info request is
        // pending (set by FieldInfoDrawTextPatch). Reset each frame by the postfix.
        internal static void NoteText(string text)
        {
            if (s_pending)
            {
                s_lastText = text;
            }
        }

        internal static string LastText => s_lastText;

        // ---- Diagnostic state (unconditional; PUtil.LogDebug logs at INFO level
        // in the game log — the game's Debug.LogFormat maps to "[INFO]"). ----
        // Heartbeat throttle: log the guard state at most once per this many frames.
        private const int HeartbeatIntervalFrames = 180;
        private static int s_lastHeartbeatFrame = -HeartbeatIntervalFrames;
        // "Change-based" throttle state: each logs only when its id/key changes.
        private static string s_lastSkipReason;      // last guard-skip id (null = all guards passed)
        private static string s_lastDrawSkipReason;  // last DrawLines null-tool/card skip id
        private static string s_lastDrewKey;         // last "drew lines" key (slot|cell|count|mass)
        private static string s_lastStagedKey;       // last "staged field lines" key (cell|count|mass)

        // Log a guard-skip message only when the skip id changes (change-based throttle).
        private static void LogGuardSkip(string id, string message)
        {
            if (s_lastSkipReason != id)
            {
                s_lastSkipReason = id;
                PUtil.LogDebug(message);
            }
        }

        // Log a DrawLines null-tool/card message only when the id changes.
        private static void LogDrawSkip(string id, string message)
        {
            if (s_lastDrawSkipReason != id)
            {
                s_lastDrawSkipReason = id;
                PUtil.LogDebug(message);
            }
        }

        // Log a staging message only when the key changes (change-based throttle).
        private static void LogStaged(string key, string message)
        {
            if (s_lastStagedKey != key)
            {
                s_lastStagedKey = key;
                PUtil.LogDebug(message);
            }
        }

        // Postfix on the non-void BeginDrawing (__result is optional and omitted):
        // runs right after the original returns. All that happens here is staging
        // the pending state — the actual drawing is done by
        // FieldInfoNewLinePatch.Prefix on the NewLine that follows the mass row
        // (FieldInfoDrawTextPatch.Prefix only records the last drawn text).
        public static void Postfix(HoverTextScreen __instance)
        {
            try
            {
                // Drop any unconsumed staging from a previous frame (e.g. a frame
                // that drew no mass-row NewLine). Must precede every early return.
                s_pending = false;
                s_lastText = null;

                // One-shot proof the patch is live (first postfix entry).
                if (!s_firstRunLogged)
                {
                    s_firstRunLogged = true;
                    PUtil.LogDebug("postfix first run");
                }

                // Full guard-state snapshot, each boolean evaluated independently
                // (no short-circuit) so one line shows exactly which is false.
                // Cell-dependent booleans are only evaluated for a valid cell (the
                // Grid indexers are unguarded).
                PlayerController dbgController = PlayerController.Instance;
                bool dbgHasPlayer = dbgController != null;
                bool dbgDefaultTool = dbgHasPlayer && dbgController.IsUsingDefaultTool();
                bool dbgCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool dbgOverlay = OverlayScreen.Instance != null;
                int dbgCell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()));
                bool dbgCellValid = Grid.IsValidCell(dbgCell);
                bool dbgElementBlock = dbgCellValid && ElementBlockWouldBeDrawn(dbgCell);

                // Heartbeat: at most once per ~HeartbeatIntervalFrames frames. This is
                // also the de-facto proof the postfix is attached and being hit.
                int dbgFrame = (int)Time.frameCount;
                if (dbgFrame - s_lastHeartbeatFrame >= HeartbeatIntervalFrames)
                {
                    s_lastHeartbeatFrame = dbgFrame;
                    PUtil.LogDebug(
                        "postfix heartbeat: player={0} defaultTool={1} ctrl={2} overlay={3} cell={4} cellValid={5} elementBlock={6}".F(
                            dbgHasPlayer, dbgDefaultTool, dbgCtrl, dbgOverlay, dbgCell, dbgCellValid, dbgElementBlock));
                }

                // (a) Default (select) tool only — no build/dig/etc. mode.
                PlayerController controller = PlayerController.Instance;
                if (controller == null || !controller.IsUsingDefaultTool())
                {
                    LogGuardSkip(
                        dbgHasPlayer ? "not default tool" : "no player controller",
                        dbgHasPlayer ? "skip: not default tool" : "skip: no player controller");
                    return;
                }

                // (b) Ctrl held (left or right).
                if (s_requireCtrl && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    LogGuardSkip("ctrl not held", "skip: ctrl not held");
                    return;
                }

                // (c) The world view must be active (mirrors the game card's early
                // return, SelectToolHoverTextCard.cs:171).
                if (OverlayScreen.Instance == null)
                {
                    LogGuardSkip("no overlay screen", "skip: no overlay screen");
                    return;
                }

                // (d) The cell under the cursor (same computation as the game's
                // hover card, SelectToolHoverTextCard.cs:170).
                int cell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()));
                if (!Grid.IsValidCell(cell))
                {
                    LogGuardSkip("no valid cell", "skip: no valid cell ({0})".F(cell));
                    return;
                }

                // (e) The cell must be one the game would show its element data
                // block for (flag2/flag3 + overlay filters).
                if (!ElementBlockWouldBeDrawn(cell))
                {
                    LogGuardSkip("element block not drawn", "skip: element block not drawn");
                    return;
                }

                // (f) Defensive: the screen and its drawer are always non-null in
                // practice.
                if (__instance == null || __instance.drawer == null)
                {
                    LogGuardSkip("drawer null", "skip: drawer null");
                    return;
                }

                // All guards passed — clear the skip reason so a later guard failure
                // re-logs (its id will differ from null).
                s_lastSkipReason = null;

                (int cellCount, float totalMass) = FieldRegionInfo.GetField(cell);
                if (cellCount == 0)
                {
                    LogGuardSkip("empty field", "skip: empty field at cell {0}".F(cell));
                    return;
                }

                // Stage the draw request. FieldInfoNewLinePatch.Prefix consumes it
                // on the NewLine that follows the mass row and inserts the two
                // rows between the mass and temperature rows (inside the shadow
                // bar, with dash icons — with and without BIC).
                s_pending = true;
                s_pendingCell = cell;
                s_pendingCount = cellCount;
                s_pendingMass = totalMass;
                LogStaged(
                    cell + "|" + cellCount + "|" + GameUtil.GetFormattedMass(totalMass),
                    "staged field lines for cell {0}: {1} cells, {2} mass".F(
                        cell, cellCount, GameUtil.GetFormattedMass(totalMass)));
            }
            catch (Exception e)
            {
                // Log once, then swallow — the game's tooltip must never break.
                if (!s_errorLogged)
                {
                    s_errorLogged = true;
                    PUtil.LogError("Tooltip postfix failed: " + e);
                }
            }
        }

        /// <summary>
        /// The single drawing slot for the field-info tooltip lines:
        /// "Клетки: &lt;N&gt;" (localized) and "Всего: &lt;mass&gt;" (localized),
        /// drawn as ordinary vanilla-style rows — NewLine + dash icon + text —
        /// so they match the element card's own data rows (SelectToolHoverTextCard.cs
        /// lines 700-727).
        /// </summary>
        /// <remarks>
        /// The style is taken from the live select-tool card's
        /// <c>Styles_BodyText.Standard</c> — the exact <c>TextStyleSetting</c> the
        /// card uses for its ordinary data lines (element category, mass,
        /// temperature; SelectToolHoverTextCard.cs lines 702, 711, 727; the
        /// field itself is declared on the card's base class,
        /// HoverTextConfiguration.cs line 45, and the card is a component of the
        /// SelectTool object — SelectTool.cs line 125). The dash icon is the
        /// card's public <c>iconDash</c> field (SelectToolHoverTextCard.cs line 21);
        /// the 4-arg <c>DrawIcon</c> call mirrors the card's own
        /// <c>DrawIcon(iconDash)</c> binding (HoverTextDrawer.cs:251-273).
        /// The <c>LocString</c> labels convert to <c>string</c> implicitly
        /// (LocString.cs line 44).
        /// </remarks>
        // <paramref name="slot"/> names the drawing slot (currently
        // "new-line" — consumed by FieldInfoNewLinePatch) for change-throttled
        // logging.
        internal static void DrawLines(HoverTextDrawer drawer, int cell, int cellCount, float totalMass, string slot)
        {
            SelectTool selectTool = SelectTool.Instance;
            if (selectTool == null)
            {
                LogDrawSkip("null selectTool", "skip: SelectTool.Instance is null");
                return;
            }
            SelectToolHoverTextCard card = selectTool.GetComponent<SelectToolHoverTextCard>();
            if (card == null)
            {
                LogDrawSkip("null card", "skip: SelectToolHoverTextCard is null");
                return;
            }

            // Tool and card both present — clear the null-skip id so a later null
            // re-logs (its id will differ from null).
            s_lastDrawSkipReason = null;

            TextStyleSetting style = card.Styles_BodyText.Standard;
            if (style == null)
            {
                LogDrawSkip("null style", "skip: Styles_BodyText.Standard is null");
                return;
            }

            Sprite dash = card.iconDash;
            string cellsLine = STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME + ": " + cellCount;
            string totalLine = STRINGS.UI.ALLRESOURCESSCREEN.TOTAL + ": " + GameUtil.GetFormattedMass(totalMass);
            if (dash != null)
            {
                drawer.NewLine();
                drawer.DrawIcon(dash, Color.white, 18, 2);
                drawer.DrawText(cellsLine, style, Color.white, override_color: false);
                drawer.NewLine();
                drawer.DrawIcon(dash, Color.white, 18, 2);
                drawer.DrawText(totalLine, style, Color.white, override_color: false);
            }
            else
            {
                if (!s_dashNullLogged)
                {
                    s_dashNullLogged = true;
                    PUtil.LogWarning("iconDash sprite is null; drawing rows without dash");
                }
                drawer.DrawText(cellsLine, style, Color.white, override_color: false);
                drawer.DrawText(totalLine, style, Color.white, override_color: false);
            }

            // "drew lines" log: once per (slot, cell, count, mass) change.
            string drewKey = slot + "|" + cell + "|" + cellCount + "|" + GameUtil.GetFormattedMass(totalMass);
            if (s_lastDrewKey != drewKey)
            {
                s_lastDrewKey = drewKey;
                PUtil.LogDebug("drew lines for cell {0}: {1} cells, {2} mass (slot={3}, mouse y={4})".F(
                    cell, cellCount, GameUtil.GetFormattedMass(totalMass), slot, (int)KInputManager.GetMousePos().y));
            }
        }

        /// <summary>
        /// Replicates, for cell <paramref name="cell"/>, the conditions that gate
        /// the element data block in <c>SelectToolHoverTextCard.UpdateHoverElements</c>
        /// (the <c>if (flag2)</c> at line 681 and everything that computes it).
        ///
        /// Line references are to the decompiled SelectToolHoverTextCard.cs:
        /// <list type="bullet">
        /// <item>191: <c>bool flag2 = true;</c></item>
        /// <item>192-195: <c>if (Grid.DupePassable[num] &amp;&amp; Grid.Solid[num]) flag2 = false;</c></item>
        /// <item>196-200: <c>flag3 = Grid.IsVisible(num) &amp;&amp; Grid.WorldIdx[num] == ClusterManager.Instance.activeWorldId;</c></item>
        /// <item>201-204: <c>if (!flag3) flag2 = false;</c></item>
        /// <item>205-215: per-overlay-mode filter via the private
        /// <c>overlayFilterMap</c> (entries built in <c>OnSpawn</c>, lines 106-141) —
        /// the map is private, so its predicates are replicated inline below.</item>
        /// <item>445-449: the radiation overlay branch resets <c>flag2 = true</c> when
        /// <c>flag3</c> holds.</item>
        /// </list>
        ///
        /// One known simplification: lines 658-662 can also clear flag2 per hovered
        /// object (<c>BuildingComplete</c> foundation over a solid cell); that needs
        /// the hover-object list, which is not available from the
        /// <c>BeginDrawing</c> postfix (before the card's <c>UpdateHoverElements</c>
        /// runs), so it is not replicated.
        /// </summary>
        private static bool ElementBlockWouldBeDrawn(int cell)
        {
            // flag3 (lines 196-200): the cell is visible and in the active world.
            bool flag3 = Grid.IsVisible(cell);
            if (Grid.WorldIdx[cell] != ClusterManager.Instance.activeWorldId)
            {
                flag3 = false;
            }

            // flag2 (lines 191-204): starts true, cleared for a solid the dupes can
            // pass through (the game then shows the object instead of the element)
            // and for any cell outside the visible active world.
            bool flag2 = !(Grid.DupePassable[cell] && Grid.Solid[cell]);
            if (!flag3)
            {
                flag2 = false;
            }

            // overlayFilterMap loop (lines 205-215): the game compares the CURRENT
            // OVERLAY mode against the map and, on a match, clears flag2 when the
            // mode's predicate fails. The map's predicates (OnSpawn, lines 106-141)
            // are replicated here per mode; the if-chain mirrors the loop's
            // match-then-break semantics.
            OverlayScreen overlay = OverlayScreen.Instance;
            if (overlay != null)
            {
                HashedString mode = overlay.GetMode();
                if (mode == OverlayModes.Oxygen.ID || mode == OverlayModes.GasConduits.ID)
                {
                    // Lines 106-115: the element must be a gas.
                    if (!Grid.Element[cell].IsGas)
                    {
                        flag2 = false;
                    }
                }
                else if (mode == OverlayModes.LiquidConduits.ID)
                {
                    // Lines 121-125: the element must be a liquid.
                    if (!Grid.Element[cell].IsLiquid)
                    {
                        flag2 = false;
                    }
                }
                else if (mode == OverlayModes.Radiation.ID)
                {
                    // Lines 445-449: the radiation branch (re-)enables the element
                    // block whenever the cell is in the visible active world,
                    // overriding the map entry `Grid.Radiation[i] > 0f` (116-120).
                    flag2 = flag3;
                }
                else if (mode == OverlayModes.Decor.ID
                    || mode == OverlayModes.Rooms.ID
                    || mode == OverlayModes.Logic.ID)
                {
                    // Lines 126-128: map entries are `() => false` — the element
                    // block is never shown in these modes.
                    flag2 = false;
                }
                else if (mode == OverlayModes.TileMode.ID)
                {
                    // Lines 129-141: the element must match one of the active
                    // tile-overlay filter tags.
                    Element element = Grid.Element[cell];
                    bool matches = false;
                    foreach (Tag filter in Game.Instance.tileOverlayFilters)
                    {
                        if (element.HasTag(filter))
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (!matches)
                    {
                        flag2 = false;
                    }
                }
                // Any other mode has no map entry: flag2 is unchanged.
            }

            return flag2;
        }
    }
}
