using Database;
using UnityEngine;
using HarmonyLib;
using KMod;
using UtilLibs;

using PeterHan.PLib.Core;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (BuildingDef, Grid, ...) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.LogDebug("loaded");

            // Feature 2: instant scheme (facade) repaint of an already-built Drywall.
            // Target: BuildTool.OnDragTool(int cell, int distFromOrigin)
            // (BuildTool.cs:302, protected override of DragTool.OnDragTool). When the
            // drag lands on a cell that ALREADY holds a built drywall of the SAME
            // element as the one selected in the tool, the vanilla game does
            // "nothing" (no build possible, no replacement plan) — this prefix
            // instead repaints the built object's facade instantly from the
            // tool's selected scheme (or back to the default when "no scheme"
            // is selected). Instant-build mode is deliberately NOT touched: there
            // the game already replaces the object (TryBuild, BuildTool.cs:319),
            // so the prefix returns true and lets the original run.
            // NO [HarmonyPatch] attributes: attached programmatically, like in
            // BuildDoorOverWall.
            PatchUtil.TryPatch(harmony, typeof(BuildTool), "OnDragTool",
                new[] { typeof(int), typeof(int) },
                "feature2: instant facade repaint over built drywall of same material",
                prefix: new HarmonyMethod(typeof(BuildTool_OnDragTool_FacadeRepaint__Patch), nameof(BuildTool_OnDragTool_FacadeRepaint__Patch.Prefix)));

            // Feature 1: Shift-box mode for Drywall. While the game's "drag
            // straight" hold-key (Shift by default, Action.DragStraight) is held
            // with the Drywall (ExteriorWall) tool active, the tool is forced into
            // the game's NATIVE Box mode (prefix on BuildTool.GetMode,
            // BuildTool.cs:480 — every DragTool handler asks the mode virtually, so
            // the whole down/move/up flow switches over): the native Box visuals
            // come for free (area visualizer + size text, DragTool.cs:125+/332+, no
            // construction during the drag), and the mouse-up per-cell placement
            // loop (DragTool.OnLeftClickUp, OnDragTool per valid cell) places the
            // plans — a built drywall of the same material with a different
            // scheme is repainted instantly by the Feature-2 prefix instead.
            // The SnapToLine prefix disables the hold-key's straight-line snapping
            // for the duration of a session so the drag keeps its 2D shape.
            // ShiftRectSession below adds the per-cell drywall ghosts over the
            // drag rect (one ghost per sampled cell, capped at MaxGhosts), tinted
            // red when the mouse-up loop will do nothing in that cell.
            // NO [HarmonyPatch] attributes: attached programmatically, like the
            // Feature-2 patch above.
            PatchUtil.TryPatch(harmony, typeof(BuildTool), "GetMode",
                new Type[0],
                "feature1: force native Box mode while Shift is held",
                prefix: new HarmonyMethod(typeof(BuildTool_GetMode__Patch), nameof(BuildTool_GetMode__Patch.Prefix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "OnLeftClickDown",
                new[] { typeof(Vector3) },
                "feature1: start the ghost session on drag start",
                postfix: new HarmonyMethod(typeof(DragTool_OnLeftClickDown__Patch), nameof(DragTool_OnLeftClickDown__Patch.Postfix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "OnMouseMove",
                new[] { typeof(Vector3) },
                "feature1: sync the per-cell ghosts to the drag rect",
                postfix: new HarmonyMethod(typeof(DragTool_OnMouseMove__Patch), nameof(DragTool_OnMouseMove__Patch.Postfix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "OnLeftClickUp",
                new[] { typeof(Vector3) },
                "feature1: keep the session through the native Box placement loop, end it after mouse up",
                prefix: new HarmonyMethod(typeof(DragTool_OnLeftClickUp__Patch), nameof(DragTool_OnLeftClickUp__Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(DragTool_OnLeftClickUp__Patch), nameof(DragTool_OnLeftClickUp__Patch.Postfix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "SnapToLine",
                new[] { typeof(Vector3) },
                "feature1: keep the 2D rect shape — no straight-line snapping while the session is active",
                prefix: new HarmonyMethod(typeof(DragTool_SnapToLine__Patch), nameof(DragTool_SnapToLine__Patch.Prefix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "CancelDragging",
                new Type[0],
                "feature1: clean up the ghosts on drag cancel",
                postfix: new HarmonyMethod(typeof(DragTool_CancelDragging__Patch), nameof(DragTool_CancelDragging__Patch.Postfix)));
            PatchUtil.TryPatch(harmony, typeof(DragTool), "OnCmpDisable",
                new Type[0],
                "feature1: clean up the ghosts on tool disable",
                postfix: new HarmonyMethod(typeof(DragTool_OnCmpDisable__Patch), nameof(DragTool_OnCmpDisable__Patch.Postfix)));
        }

        /// <summary>
        /// Feature 2: BuildTool.OnDragTool(int, int) prefix. Returns false (skip
        /// the original) ONLY in the exact "nothing happens" case it replaces
        /// (see IsRepaintCase below): drywall def, instant-build OFF, a completed
        /// drywall of the SAME def and SAME element at the cell, and the tool's
        /// selected scheme differs from the object's current scheme. All other
        /// drags return true and run vanilla logic untouched (fresh build,
        /// different-material replacement, instant-build replace, same-scheme
        /// no-op).
        /// </summary>
        public static class BuildTool_OnDragTool_FacadeRepaint__Patch
        {
            public static bool Prefix(BuildTool __instance, int __0, int __1)
            {
                if (!IsRepaintCase(__instance, __0))
                {
                    return true;
                }
                // Repaint: to the tool's scheme, or back to the default facade.
                GameObject go = Grid.Objects[__0, (int)__instance.def.ObjectLayer];
                BuildingFacade facadeComp = go.GetComponent<BuildingFacade>();
                string toolFacade = __instance.facadeID;
                if (string.IsNullOrEmpty(toolFacade) || toolFacade == "DEFAULT_FACADE")
                {
                    facadeComp.ApplyDefaultFacade(false);
                }
                else
                {
                    // IsRepaintCase already verified the resource exists.
                    BuildingFacadeResource res = Db.GetBuildingFacades().TryGet(toolFacade);
                    facadeComp.ApplyBuildingFacade(res, false);
                }
#if DEBUG
                PUtil.LogDebug("OnDragTool prefix: cell={0} toolFacade={1}".F(__0, toolFacade ?? "(null)"));
#endif
                return false;
            }

            /// <summary>
            /// The exact "nothing happens" case the Feature-2 prefix replaces,
            /// shared with the ghost validity tint (ShiftRectSession.
            /// IsCellActionable): drywall def, instant-build mode OFF, a completed
            /// drywall of the SAME def and SAME primary element at the cell, a
            /// facade component present, the tool's selected scheme different from
            /// the object's current scheme, and — for a non-default tool scheme —
            /// a known facade resource (an unknown id would leave the cell
            /// untouched anyway).
            /// </summary>
            public static bool IsRepaintCase(BuildTool tool, int cell)
            {
                BuildingDef def = tool.def;
                // Scope gate: drywall only (ExteriorWallConfig.ID = "ExteriorWall").
                if (def == null || def.PrefabID != "ExteriorWall")
                {
                    return false;
                }
                // Instant-build ON: the game's TryBuild dig+rebuild path already
                // handles the replacement (BuildTool.cs:319) — do not interfere.
                if (DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild))
                {
                    return false;
                }
                // The built object in the cell's drywall layer.
                GameObject go = Grid.Objects[cell, (int)def.ObjectLayer];
                if (go == null || !go.TryGetComponent(out BuildingComplete bc))
                {
                    return false;
                }
                // Same def only: another building type (e.g. a door over a wall)
                // keeps its native behavior.
                if (!ReferenceEquals(bc.Def, def))
                {
                    return false;
                }
                // The dragged material must match the built one: a different
                // element keeps the game's normal replacement behavior.
                IList<Tag> selected = tool.selectedElements;
                if (selected == null || selected.Count == 0)
                {
                    return false;
                }
                if (bc.primaryElement.Element.tag != selected[0])
                {
                    return false;
                }
                // The built object must carry a facade component.
                if (!go.TryGetComponent(out BuildingFacade facadeComp) || facadeComp == null)
                {
                    return false;
                }
                // Tool scheme: null / empty / "DEFAULT_FACADE" == default (no scheme).
                string toolFacade = tool.facadeID;
                bool toolIsDefault = string.IsNullOrEmpty(toolFacade) || toolFacade == "DEFAULT_FACADE";
                if (!toolIsDefault && Db.GetBuildingFacades().TryGet(toolFacade) == null)
                {
                    // Unknown facade id: the cell would be left to the game (no-op).
                    return false;
                }
                // CurrentFacade null/empty == original appearance (facadeComp.IsOriginal).
                string current = facadeComp.CurrentFacade;
                bool currentIsDefault = string.IsNullOrEmpty(current);
                // Same scheme as the tool selection: nothing to do. The condition
                // is exactly "tool scheme == current scheme": both default, or
                // both non-default with equal facade ids.
                bool sameScheme = toolIsDefault ? currentIsDefault : toolFacade == current;
                return !sameScheme;
            }
        }

        // Feature 1: the per-cell ghost session over the Shift-hold Box drag rect.
        // The native Box mode already draws the rect border and the size text;
        // this class adds one ghost clone per sampled cell so the player sees
        // what the mouse-up placement loop will build. The rect is computed
        // EXACTLY like the game's mouse-up loop (DragTool.OnLeftClickUp:244-269):
        // Grid.PosToXY (int cell coords in this build) + conditional min/max swap
        // per axis (DragTool.cs:248-255 — the game's own pattern, NOT a blind
        // Util.Swap of both axes), row-major order.
        // Big rects are subsampled (stride) so the ghost count stays capped at
        // MaxGhosts — the mouse-up placement loop itself still builds EVERY
        // valid cell regardless of the stride.
        // Each ghost is tinted once at creation: white when the mouse-up loop
        // will do something in that cell (place a plan, queue a replacement, or
        // repaint via the Feature-2 prefix), red when it will do nothing — the
        // same tint members the game uses for its own valid/invalid visualizer
        // (BuildTool.UpdateVis + SetColor, BuildTool.cs:181-189/485-492).
        public static class ShiftRectSession
        {
            private const int MaxGhosts = 2000;

            // One ghost = the clone plus its anim controller (for the tint).
            private struct Ghost
            {
                public GameObject go;
                public KBatchedAnimController? bac;
            }

            private static bool active;
            private static Dictionary<int, Ghost> ghosts = new Dictionary<int, Ghost>();
            // Number of red (invalid) ghosts in the current session, for the
            // #if DEBUG end-of-session log.
            private static int invalidCount;

            // Cached rect + stride of the last sync: an unchanged rect skips the
            // work entirely.
            private static int lastX0;
            private static int lastY0;
            private static int lastX1;
            private static int lastY1;
            private static int lastStride;

            public static bool Active
            {
                get { return active; }
            }

            public static bool ShiftHeld()
            {
                // The game's "drag straight" hold-key is Shift by default
                // (Action.DragStraight). Use the game's own input lookup — the
                // exact chain DragTool.OnMouseMove:335 / OnLeftClickUp:235 use —
                // so we follow the player's bindings (and the game's own
                // straight-line snapping decision) instead of a hardcoded key.
                return Input.GetKey((KeyCode)Global.GetInputManager().GetDefaultController().GetInputForAction(Action.DragStraight));
            }

            // The wall-tool prefab has no inspector-assigned box visualizer nor
            // size-text prefab (vanilla BuildTool.GetMode is always Mode.Brush),
            // so DragTool.areaVisualizer is null and the game's Box code path
            // null-derefs it (DragTool.cs:377/379/391) and skips the mouse-up
            // placement loop (DragTool.cs:239), and the Box size text is never
            // created (DragTool.cs:151-155, gated on the serialized
            // areaVisualizerTextPrefab). Borrow the game's own box visualizer
            // and size-text prefab from any DragTool that has them.
            private static GameObject? cachedBoxVisualizerPrefab;
            private static GameObject? cachedBoxVisualizerTextPrefab;
            // One-time warnings: GetMode is called every frame while hovering, so
            // a missing donor must not spam the log.
            private static bool warnedNoDonor;
            private static bool warnedNoTextDonor;

            public static bool EnsureBoxVisualizer(BuildTool tool)
            {
                if (tool.areaVisualizer == null)
                {
                    FindDonorPrefabs();
                    if (cachedBoxVisualizerPrefab == null)
                    {
                        if (!warnedNoDonor)
                        {
                            warnedNoDonor = true;
                            PUtil.LogWarning("box area visualizer donor not found; Box mode disabled");
                        }
                        return false;
                    }
                    // Same setup as the game's own OnPrefabInit (DragTool.cs:98-105):
                    // instantiate, start inactive, grab the SpriteRenderer,
                    // parent under the tool, tint the frame with the tool's own
                    // areaColour.
                    tool.areaVisualizer = GameUtil.KInstantiate(cachedBoxVisualizerPrefab, Vector3.zero, Grid.SceneLayer.Ore, tool.gameObject);
                    tool.areaVisualizer.SetActive(false);
                    tool.areaVisualizer.layer = cachedBoxVisualizerPrefab.layer;
                    tool.areaVisualizerSpriteRenderer = tool.areaVisualizer.GetComponent<SpriteRenderer>();
                    tool.areaVisualizer.GetComponent<Renderer>().material.color = tool.areaColour;
                }
                // The Box size text: only the serialized prefab is missing on the
                // wall tool. Once it is non-null the whole vanilla path runs on
                // its own — creation in OnLeftClickDown (DragTool.cs:151-155),
                // per-move text/position update in OnMouseMove (DragTool.
                // cs:392-398), and cleanup via RemoveCurrentAreaText
                // (DragTool.cs:180-187) on click-up / cancel / tool deactivate —
                // so no borrowed instance can leak.
                if (tool.areaVisualizerTextPrefab == null)
                {
                    FindDonorPrefabs();
                    if (cachedBoxVisualizerTextPrefab == null)
                    {
                        // The rectangle visualizer alone is the crash-critical
                        // part: a missing text donor only loses the size text.
                        if (!warnedNoTextDonor)
                        {
                            warnedNoTextDonor = true;
                            PUtil.LogWarning("box area text donor not found; no size text in Box drags");
                        }
                    }
                    else
                    {
                        tool.areaVisualizerTextPrefab = cachedBoxVisualizerTextPrefab;
#if DEBUG
                        PUtil.LogDebug("box size-text prefab borrowed from {0}".F(cachedBoxVisualizerTextPrefab.name));
#endif
                    }
                }
                return true;
            }

            // One shared scan of the player's tools for BOTH borrowed prefabs
            // (the rectangle donor and the text donor may be different tools).
            // Runs at most once per missing cache.
            private static void FindDonorPrefabs()
            {
                if (cachedBoxVisualizerPrefab != null && cachedBoxVisualizerTextPrefab != null)
                {
                    return;
                }
                PlayerController pc = PlayerController.Instance;
                if (pc == null)
                {
                    return;
                }
                foreach (InterfaceTool t in pc.tools)
                {
                    DragTool? donor = t as DragTool;
                    if (donor == null)
                    {
                        continue;
                    }
                    if (cachedBoxVisualizerPrefab == null && donor.areaVisualizer != null && donor.areaVisualizer.GetComponent<SpriteRenderer>() != null)
                    {
                        cachedBoxVisualizerPrefab = donor.areaVisualizer;
                    }
                    if (cachedBoxVisualizerTextPrefab == null && donor.areaVisualizerTextPrefab != null)
                    {
                        cachedBoxVisualizerTextPrefab = donor.areaVisualizerTextPrefab;
                    }
                    if (cachedBoxVisualizerPrefab != null && cachedBoxVisualizerTextPrefab != null)
                    {
                        break;
                    }
                }
            }

            public static void Begin(BuildTool tool)
            {
                if (active)
                {
                    // A second drag started without a mouse up in between: restart.
                    End("restart");
                }
                active = true;
                invalidCount = 0;
                int cell = Grid.PosToCell(tool.downPos);
                if (Grid.IsValidCell(cell) && Grid.IsVisible(cell))
                {
                    CreateGhost(tool, cell);
                }
#if DEBUG
                PUtil.LogDebug("box session begin: start cell={0}".F(cell));
#endif
            }

            public static void Update(BuildTool tool, Vector3 downPos, Vector3 cursorPos)
            {
                // The same rect computation as the game's mouse-up loop. In this
                // game build Grid.PosToXY returns INT cell coordinates directly
                // (PosToXY(Vector3, out int, out int), Grid.cs:1440 — via
                // PosToCell), so no float truncation is needed.
                Grid.PosToXY(downPos, out int x0, out int y0);
                Grid.PosToXY(cursorPos, out int x1, out int y1);
                // The game's own conditional swap (DragTool.cs:248-255): normalize
                // each axis independently so x0/y0 is ALWAYS the min corner and
                // x1/y1 the max corner, for every drag diagonal. A blind
                // Util.Swap on both axes only worked for one diagonal and left
                // the other three with x0>x1 / y0>y1 (empty rect → zero ghosts).
                if (x1 < x0)
                {
                    Util.Swap(ref x0, ref x1);
                }
                if (y1 < y0)
                {
                    Util.Swap(ref y0, ref y1);
                }
                int w = x1 - x0 + 1;
                int h = y1 - y0 + 1;
                int total = w * h;
                int stride = total <= MaxGhosts ? 1 : Mathf.CeilToInt((float)total / (float)MaxGhosts);
                if (x0 == lastX0 && y0 == lastY0 && x1 == lastX1 && y1 == lastY1 && stride == lastStride)
                {
                    return;
                }
                // (a) Stale ghosts: outside the new rect or off the sampled grid.
                List<int> stale = new List<int>();
                foreach (KeyValuePair<int, Ghost> kv in ghosts)
                {
                    Grid.CellToXY(kv.Key, out int gx, out int gy);
                    if (gx < x0 || gx > x1 || gy < y0 || gy > y1 ||
                        ((gx - x0) + (gy - y0) * w) % stride != 0)
                    {
                        stale.Add(kv.Key);
                    }
                }
                for (int i = 0; i < stale.Count; i++)
                {
                    Util.KDestroyGameObject(ghosts[stale[i]].go);
                    ghosts.Remove(stale[i]);
                }
                // (b) New ghosts, same row-major order as the mouse-up loop.
                for (int i = y0; i <= y1; i++)
                {
                    for (int j = x0; j <= x1; j++)
                    {
                        if (((j - x0) + (i - y0) * w) % stride != 0)
                        {
                            continue;
                        }
                        int cell = Grid.XYToCell(j, i);
                        if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell) || ghosts.ContainsKey(cell))
                        {
                            continue;
                        }
                        CreateGhost(tool, cell);
                    }
                }
                lastX0 = x0;
                lastY0 = y0;
                lastX1 = x1;
                lastY1 = y1;
                lastStride = stride;
            }

            public static void End(string reason)
            {
                if (!active)
                {
                    return;
                }
                active = false;
                int count = ghosts.Count;
                foreach (KeyValuePair<int, Ghost> kv in ghosts)
                {
                    Util.KDestroyGameObject(kv.Value.go);
                }
                ghosts.Clear();
                lastX0 = lastY0 = lastX1 = lastY1 = -1;
                lastStride = 1;
#if DEBUG
                PUtil.LogDebug("box session end ({0}): ghosts={1}, invalid={2}".F(reason, count, invalidCount));
#endif
            }

            // One ghost clone per cell — the same recipe as the tool's own
            // visualizer creation in BuildTool.OnActivateTool (BuildTool.cs:51-94).
            // No GridCompositor.ToggleMajor per ghost: the native drag flow
            // already drives the compositor, and a toggle per cell would thrash it.
            private static void CreateGhost(BuildTool tool, int cell)
            {
                BuildingDef def = tool.def;
                GameObject go = GameUtil.KInstantiate(def.BuildingPreview, Grid.CellToPosCBC(cell, def.SceneLayer), Grid.SceneLayer.Ore, null, LayerMask.NameToLayer("Place"));
                KBatchedAnimController component = go.GetComponent<KBatchedAnimController>();
                if (component != null)
                {
                    component.visibilityType = KAnimControllerBase.VisibilityType.Always;
                    component.isMovable = true;
                    component.Offset = def.GetVisualizerOffset();
                    component.SetLayer(LayerMask.NameToLayer("Place"));
                    // Validity tint — the same member the game tints (BuildTool
                    // .SetColor, BuildTool.cs:485-492, sets TintColour only;
                    // UpdateVis passes Color.white/strength 0 for valid,
                    // Color.red/strength 1 for invalid). The world does not change
                    // during the drag, so it is computed once at creation.
                    bool valid = IsCellActionable(tool, cell);
                    component.TintColour = valid ? Color.white : Color.red;
                    if (!valid)
                    {
                        invalidCount++;
                    }
                }
                else
                {
                    go.SetLayerRecursively(LayerMask.NameToLayer("Place"));
                }
                // Same scheme handling as the tool visualizer (BuildTool.cs:74-77).
                if (!string.IsNullOrEmpty(tool.facadeID) && tool.facadeID != "DEFAULT_FACADE")
                {
                    BuildingFacade facade = go.GetComponent<BuildingFacade>();
                    if (facade != null)
                    {
                        BuildingFacadeResource res = Db.GetBuildingFacades().TryGet(tool.facadeID);
                        if (res != null)
                        {
                            facade.ApplyBuildingFacade(res, false);
                        }
                    }
                }
                go.SetActive(true);
                ghosts[cell] = new Ghost { go = go, bac = component };
            }

            // Will the mouse-up placement loop do something in this cell? Mirrors
            // what BuildTool.TryBuild (BuildTool.cs:307-388) actually does per
            // cell — the same pos the game uses (Grid.CellToPosCBC(cell,
            // Grid.SceneLayer.Building), BuildTool.cs:316), the same orientation,
            // and the same (hidden) tool visualizer the game's own checks take:
            //   1. placeable: def.IsValidPlaceLocation (read-only; the game calls
            //      it with the same arguments in UpdateVis, BuildTool.cs:177, and
            //      through def.TryPlace in TryBuild, BuildTool.cs:323);
            //   2. repaint: the Feature-2 case (already-built drywall, same def +
            //      element, different scheme) — the Feature-2 prefix repaints it;
            //   3. replace: the game's TryBuild fallback (BuildTool.cs:350-386) —
            //      a built object the def can replace in a free replacement layer
            //      queues a replacement plan when the def or element differs.
            private static bool IsCellActionable(BuildTool tool, int cell)
            {
                BuildingDef def = tool.def;
                Vector3 pos = Grid.CellToPosCBC(cell, Grid.SceneLayer.Building);
                string err;
                if (def.IsValidPlaceLocation(tool.visualizer, pos, tool.GetBuildingOrientation, out err))
                {
                    return true;
                }
                if (BuildTool_OnDragTool_FacadeRepaint__Patch.IsRepaintCase(tool, cell))
                {
                    return true;
                }
                if (def.ReplacementLayer != ObjectLayer.NumLayers)
                {
                    GameObject candidate = def.GetReplacementCandidate(cell);
                    // Drywall is a 1x1 placement: the game's RunOnArea loop over
                    // PlacementOffsets collapses to this single cell.
                    if (candidate != null && !def.IsReplacementLayerOccupied(cell))
                    {
                        BuildingComplete bc = candidate.GetComponent<BuildingComplete>();
                        if (bc != null && bc.Def.Replaceable && def.CanReplace(candidate))
                        {
                            Tag tag = bc.primaryElement.Element.tag;
                            // Mirrors TryBuild's snow remap (BuildTool.cs:367-370).
                            if (tag.GetHash() == 1542131326)
                            {
                                tag = SimHashes.Snow.CreateTag();
                            }
                            // The game queues the plan when the def or the element
                            // differs; same def + same element falls through to the
                            // Feature-2 repaint case above (or a no-op).
                            IList<Tag> selected = tool.selectedElements;
                            if (bc.Def != def || (selected != null && selected.Count > 0 && selected[0] != tag))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        // Feature 1: BuildTool.GetMode() prefix (BuildTool.cs:480, protected
        // override returning Mode.Brush). The DragTool handlers (down/move/up)
        // call GetMode() virtually, so flipping __result to Mode.Box while Shift
        // is held switches the whole tool to the native Box behavior: the area
        // visualizer + size text during the drag, and the per-cell placement
        // loop on mouse up. Returning false SKIPS the original method — it would
        // otherwise run and overwrite __result back to Mode.Brush (the original
        // bug: Box mode never actually engaged). Normal tools fall through
        // (return true) and run the original untouched.
        // The flip is gated on EnsureBoxVisualizer: the wall-tool prefab has no
        // box visualizer, and the Box mode without one crashes the game (see
        // ShiftRectSession.EnsureBoxVisualizer). The first GetMode() call happens
        // inside the original OnLeftClickDown's own switch (DragTool.cs:156) —
        // and even earlier on hover — so the visualizer is created before any
        // vanilla Box code touches it, and the vanilla null-guarded Box
        // activation at DragTool.cs:170-175 then runs for free.
        public static class BuildTool_GetMode__Patch
        {
            public static bool Prefix(BuildTool __instance, ref DragTool.Mode __result)
            {
                if (__instance.def != null && __instance.def.PrefabID == "ExteriorWall" && ShiftRectSession.ShiftHeld()
                    && ShiftRectSession.EnsureBoxVisualizer(__instance))
                {
                    __result = DragTool.Mode.Box;
                    return false; // skip the original GetMode (it would return Mode.Brush and overwrite __result)
                }
                return true; // normal tools / no visualizer available: original runs as-is
            }
        }

        // Feature 1: DragTool.SnapToLine(Vector3) prefix (DragTool.cs:297,
        // protected, returns Vector3). While the "drag straight" hold-key is
        // held, the game calls SnapToLine from OnMouseMove:335-338 and
        // OnLeftClickUp:235-238, collapsing the cursor — and hence the whole
        // Box drag/placement rect — onto a single axis. While our rect session
        // is active the drag must keep its 2D shape, so the prefix skips the
        // original and passes the cursor through unchanged (__result = cursorPos;
        // a skipped value-returning method returns the prefix's __result).
        // Outside a session the original runs untouched — every other tool keeps
        // its vanilla straight-line behavior.
        public static class DragTool_SnapToLine__Patch
        {
            public static bool Prefix(DragTool __instance, Vector3 cursorPos, ref Vector3 __result)
            {
                if (ShiftRectSession.Active)
                {
                    __result = cursorPos; // no snapping this frame — a skipped value-returning method leaves __result as the return value
                    return false;
                }
                return true;
            }
        }

        // Feature 1: DragTool.OnLeftClickDown(Vector3) postfix. Starts the ghost
        // session when a drywall drag begins with Shift held. By this point the
        // game has already taken its Box branch (via the GetMode prefix above),
        // so downPos is set and the native area visualizer is up.
        // It also backfills the Box size text in the one case where the
        // vanilla creation block missed it: vanilla creates the text
        // (DragTool.cs:151-155) BEFORE its first in-method GetMode() call
        // (DragTool.cs:156), so if no hover frame ran the GetMode prefix
        // first, the borrowed text prefab was not yet assigned when the
        // creation block ran and the text was never created — while the
        // rectangle still worked (its activation, DragTool.cs:170-175,
        // follows the GetMode call). The backfill uses the EXACT vanilla
        // creation calls; everything downstream (per-move text/position
        // update, DragTool.cs:392-398, and the RemoveCurrentAreaText cleanup
        // on click-up/cancel/deactivate) stays 100% vanilla.
        public static class DragTool_OnLeftClickDown__Patch
        {
            public static void Postfix(DragTool __instance, Vector3 cursor_pos)
            {
                if (__instance is BuildTool bt && bt.def != null && bt.def.PrefabID == "ExteriorWall" && ShiftRectSession.ShiftHeld())
                {
                    if (bt.areaVisualizerText == Guid.Empty && bt.areaVisualizerTextPrefab != null && NameDisplayScreen.Instance != null)
                    {
                        bt.areaVisualizerText = NameDisplayScreen.Instance.AddAreaText("", bt.areaVisualizerTextPrefab);
                        SetAreaTextColour(NameDisplayScreen.Instance.GetWorldText(bt.areaVisualizerText), bt.areaColour);
#if DEBUG
                        PUtil.LogDebug("box size text backfilled in OnLeftClickDown postfix (no hover frame before click-down)");
#endif
                    }
                    ShiftRectSession.Begin(bt);
                }
            }

            // The vanilla creation block (DragTool.cs:154) tints the text with
            // the tool's areaColour via LocText's `color` property — inherited
            // from TextMeshProUGUI in Unity.TextMeshPro, an assembly this mod
            // does not reference (the csproj must stay untouched), and any
            // LocText-typed member access makes the compiler demand that
            // assembly (CS0012). Fetch the component by name and set the same
            // property reflectively instead; it is always present at runtime
            // (the game's own text UI is TextMeshPro).
            private static void SetAreaTextColour(GameObject worldText, Color32 colour)
            {
                Component? locText = worldText != null ? worldText.GetComponent("LocText") : null;
                if (locText == null)
                {
                    return;
                }
                System.Reflection.PropertyInfo colorProp = locText.GetType().GetProperty("color");
                if (colorProp == null || !colorProp.CanWrite)
                {
                    PUtil.LogWarning("box size text color property not found; leaving default color");
                    return;
                }
                colorProp.SetValue(locText, colour);
            }
        }

        // Feature 1: DragTool.OnMouseMove(Vector3) postfix. Syncs the per-cell
        // ghost set to the drag rect while Shift is held. Releasing Shift
        // mid-drag ends the session: the mode then falls back to Brush, so the
        // game's Box placement loop on mouse up is skipped and the drag cancels.
        public static class DragTool_OnMouseMove__Patch
        {
            public static void Postfix(DragTool __instance, Vector3 cursorPos)
            {
                if (!ShiftRectSession.Active)
                {
                    return;
                }
                if (!(__instance is BuildTool bt))
                {
                    // Safety: the session only exists under a build tool.
                    ShiftRectSession.End("not a build tool");
                    return;
                }
                if (!ShiftRectSession.ShiftHeld())
                {
                    ShiftRectSession.End("shift released");
                    return;
                }
                ShiftRectSession.Update(bt, __instance.downPos, cursorPos);
            }
        }

        // Feature 1: DragTool.OnLeftClickUp(Vector3) prefix + postfix. The
        // PREFIX only resets BuildTool.lastDragCell (private, publicized) so
        // the first cell of the rect is not swallowed by TryBuild's same-cell
        // dedupe (BuildTool.cs:309) — the session must stay ACTIVE while the
        // original runs, because (a) GetMode() must still return Box for the
        // mouse-up branch (DragTool.cs:234) and (b) the original's own
        // SnapToLine call (DragTool.cs:235-238) must stay suppressed by our
        // prefix. The POSTFIX (after the vanilla Box loop placed the plans on
        // every valid cell through BuildTool.OnDragTool, where the Feature-2
        // prefix repaints already-built drywall of the same material instead
        // of building) ends the session: destroys the ghosts + end log.
        public static class DragTool_OnLeftClickUp__Patch
        {
            public static bool Prefix(DragTool __instance, Vector3 cursor_pos)
            {
                if (ShiftRectSession.Active && __instance is BuildTool bt)
                {
                    bt.lastDragCell = -1; // the Box loop must not skip the first cell
                }
                return true; // do NOT end the session here — the original still needs it
            }

            public static void Postfix(DragTool __instance, Vector3 cursor_pos)
            {
                if (ShiftRectSession.Active)
                {
                    ShiftRectSession.End("mouse up");
                }
            }
        }

        // Feature 1: DragTool.CancelDragging() postfix — the native cleanup hides
        // only the game's own visuals; ours must go as well.
        public static class DragTool_CancelDragging__Patch
        {
            public static void Postfix(DragTool __instance)
            {
                if (ShiftRectSession.Active)
                {
                    ShiftRectSession.End("cancel");
                }
            }
        }

        // Feature 1: DragTool.OnCmpDisable() postfix — the same cleanup when the
        // tool is deactivated (tool switch / world shutdown).
        public static class DragTool_OnCmpDisable__Patch
        {
            public static void Postfix(DragTool __instance)
            {
                if (ShiftRectSession.Active)
                {
                    ShiftRectSession.End("tool disabled");
                }
            }
        }
    }
}
