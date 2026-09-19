using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KMod;
using UnityEngine;

using PeterHan.PLib.Core;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (BuildingDef, ObjectLayer, Tag, GameTags, Grid, Door, CopyBuildingSettings)
// resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

#if DEBUG
            PUtil.LogDebug("loaded");
#endif

            // Stage 3: Assets.AddBuildingDef(BuildingDef) — public static, resolved like
            // the other targets via the byref-normalizing FindMethod (DeclaredOnly +
            // Static). Hooks the single registration point for every vanilla, DLC and
            // mod building def (BuildingConfigManager.cs:114, Assets.cs:670 — dedup by
            // PrefabID), AFTER the full config chain (DoPostConfigureComplete), so the
            // D2 predicate and IsDoorDef see final def state. Fires once per def per
            // game start; the runtime replacement machinery (CanReplace,
            // GetReplacementCandidate, IsReplacementLayerOccupied) reads the fields
            // live and every placed object references the same def instance
            // (BuildingLoader.cs:219), so the injection below is all the placement/
            // completion flow consults. NO [HarmonyPatch] attributes: attached
            // programmatically, so PatchAll (UserMod2.OnLoad) ignores the patch class.
            MethodInfo addBuildingDef = FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef));
            if (addBuildingDef == null)
            {
                PUtil.LogError("could not resolve Assets.AddBuildingDef(BuildingDef) — replacement metadata postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(addBuildingDef, postfix: new HarmonyMethod(typeof(Assets_AddBuildingDef_ReplaceBuildingMaterial__Patch), nameof(Assets_AddBuildingDef_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 4: BuildTool.TryBuild(int) — PRIVATE instance method (BuildTool.cs:307),
            // the single choke point every drag funnels through (DragTool.OnDragTool →
            // BuildTool.OnDragTool → TryBuild; survival BuildMenu.cs:908 and plan
            // PlanScreen.cs:1820 both Activate this tool). It is void — NOT bool — so a
            // short-circuiting PREFIX is a plain `bool`-returning prefix: returning false
            // skips the original (no placement, no Grid writes); there is no `__result`.
            // Resolved like the other targets via the byref/NonPublic-tolerant FindMethod
            // (DeclaredOnly scan of typeof(BuildTool) finds the private TryBuild, exactly as
            // the reference mod does at BuildDoorOverWall/Mod.cs:88). The prefix rejects the
            // shifted/rotated drags (R9 Q2 cases 2–4) that the native fallback would
            // otherwise let create a replacement plan; the exact-overlap case (case 1) and
            // the no-candidate case (case 5) are left to run natively. Attached programmatically;
            // NO [HarmonyPatch] attribute (PatchAll in UserMod2.OnLoad would ignore it anyway).
            MethodInfo tryBuild = FindMethod(typeof(BuildTool), "TryBuild", typeof(int));
            if (tryBuild == null)
            {
                PUtil.LogError("could not resolve BuildTool.TryBuild(int) — shifted/rotated drag-rejection prefix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(tryBuild, prefix: new HarmonyMethod(typeof(BuildTool_TryBuild_ReplaceBuildingMaterial__Patch), nameof(BuildTool_TryBuild_ReplaceBuildingMaterial__Patch.Prefix)), postfix: new HarmonyMethod(typeof(BuildTool_TryBuild_ReplaceBuildingMaterial__Patch), nameof(BuildTool_TryBuild_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 6 (debug round 9): BuildTool.OnDragTool(int, int) — protected override
            // (BuildTool.cs:302-305). DEBUG-only drag-entry proof log (see the patch
            // class above): distinguishes "click never reached the drag tool" from a
            // silent pass-through inside TryBuild.
            MethodInfo onDragTool = FindMethod(typeof(BuildTool), "OnDragTool", typeof(int), typeof(int));
            if (onDragTool == null)
            {
                PUtil.LogError("could not resolve BuildTool.OnDragTool(int, int) — drag-entry log skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(onDragTool, postfix: new HarmonyMethod(typeof(BuildTool_OnDragTool_ReplaceBuildingMaterial__Patch), nameof(BuildTool_OnDragTool_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 5: BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)
            // — public, returns bool (BuildingDef.cs:1184-1207); its native body is purely
            // geometric (R10.1): no material or strictness signal, so it whitens exact overlap,
            // same-material exact overlap AND symmetric in-place rotations alike. The sole
            // caller in the game build is BuildTool.UpdateVis (BuildTool.cs:178), which ORs the
            // result into the red/white preview tint — nothing in the drag flow reads it.
            // The postfix therefore overwrites `__result` in BOTH directions for mod defs:
            // white only for a cross-material exact overlap, red otherwise (R11 Q3 row 4).
            // FindMethod's 4-arity match selects only this overload.
            MethodInfo isValidReplaceLocation = FindMethod(typeof(BuildingDef), "IsValidReplaceLocation",
                typeof(Vector3), typeof(Orientation), typeof(ObjectLayer), typeof(ObjectLayer));
            if (isValidReplaceLocation == null)
            {
                PUtil.LogError("could not resolve BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) — preview-tint postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(isValidReplaceLocation, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidReplaceLocation_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_IsValidReplaceLocation_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 6 (debug round): BuildingDef.IsValidPlaceLocation(GameObject, Vector3,
            // Orientation, out string) — public 4-arg overload (BuildingDef.cs:1098-1102);
            // the build hover card is its only UI consumer of the fail_reason out param
            // (BuildToolHoverTextCard.cs:48-55). The card passes replace_tile: false, so an
            // exact overlap always fails with OCCUPIED even when the replacement placement
            // would actually succeed — the user-reported red "Must be built in unoccupied
            // space" text under a white ghost. The postfix rewrites ONLY the out parameter,
            // never the bool: for a valid cross-material exact overlap it blanks the
            // misleading text. Same-material overlap keeps the red "занято" (the spec
            // verdict). FindMethod's byref-normalized 4-arity match selects only this
            // overload (TryPlace goes straight to the 6-arg overload, unaffected).
            MethodInfo isValidPlaceLocation4 = FindMethod(typeof(BuildingDef), "IsValidPlaceLocation",
                typeof(GameObject), typeof(Vector3), typeof(Orientation), typeof(string));
            if (isValidPlaceLocation4 == null)
            {
                PUtil.LogError("could not resolve BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) — hover-text postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(isValidPlaceLocation4, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidPlaceLocation_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_IsValidPlaceLocation_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 6 (debug round 10, root-cause fix): BuildingDef.ArePowerPortsInValidPositions
            // (GameObject, int, Orientation, out string) — PRIVATE (BuildingDef.cs:1391-1421),
            // the last remaining gate of IsAreaClear for the replace_tile: true path (the
            // slot-1 occupancy check already excludes the replacement candidate,
            // BuildingDef.cs:582-602). It fails whenever the NEW building's power-port cell
            // (def.PowerInputOffset/PowerOutputOffset → Grid.Objects[portCell, 29], the
            // WireConnectors layer) holds anything that is not `source_go` — and TryBuild
            // passes the ghost VISUALIZER as source_go (BuildTool.cs:376), so the OLD
            // building's own port entry (written by MarkArea, BuildingDef.cs:861-874)
            // always rejects an exact overlap of a power-building. Root cause of the
            // user-reported "white ghost, no plan" on the big transformer. The postfix
            // lifts the verdict ONLY for a mod-def cross-material exact overlap whose
            // occupied port cells are owned by the replacement candidate itself.
            MethodInfo arePowerPorts = FindMethod(typeof(BuildingDef), "ArePowerPortsInValidPositions", typeof(GameObject), typeof(int), typeof(Orientation), typeof(string));
            if (arePowerPorts == null)
            {
                PUtil.LogError("could not resolve BuildingDef.ArePowerPortsInValidPositions(GameObject, int, Orientation, out string) — power-port overlap fix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(arePowerPorts, postfix: new HarmonyMethod(typeof(BuildingDef_ArePowerPortsInValidPositions_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_ArePowerPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 6 (debug round 10): same fix for conduit ports —
            // BuildingDef.AreConduitPortsInValidPositions(GameObject, int, Orientation,
            // out string), PRIVATE (BuildingDef.cs:1423-1487). Same pattern via
            // IsValidConduitConnection (BuildingDef.cs:1621-1652): gas layer 15 / liquid
            // 19 / solid 23 (the *ConduitConnection layers) occupied by anything other
            // than source_go fails the placement, so gas/liquid/solid conduit buildings
            // hit the identical wall on exact overlap.
            MethodInfo areConduitPorts = FindMethod(typeof(BuildingDef), "AreConduitPortsInValidPositions", typeof(GameObject), typeof(int), typeof(Orientation), typeof(string));
            if (areConduitPorts == null)
            {
                PUtil.LogError("could not resolve BuildingDef.AreConduitPortsInValidPositions(GameObject, int, Orientation, out string) — conduit-port overlap fix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(areConduitPorts, postfix: new HarmonyMethod(typeof(BuildingDef_AreConduitPortsInValidPositions_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_AreConduitPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Stage 6 (debug round 12): same fix for logic ports —
            // BuildingDef.AreLogicPortsInValidPositions(GameObject, int, out string),
            // PRIVATE (BuildingDef.cs:1558-1586), the LAST gate of the IsAreaClear
            // &&-chain (:789). Scene-based: the ghost's LogicPorts and the old
            // building's physical ports both register into the logicCircuitManager
            // vis-elements, so an exact overlap of a logic-port building always
            // self-conflicts. (Round-10 assumption that this gate can never fire
            // for a prefab was wrong — proven by the clean-session log.)
            MethodInfo areLogicPorts = FindMethod(typeof(BuildingDef), "AreLogicPortsInValidPositions", typeof(GameObject), typeof(int), typeof(string));
            if (areLogicPorts == null)
            {
                PUtil.LogError("could not resolve BuildingDef.AreLogicPortsInValidPositions(GameObject, int, out string) — logic-port overlap fix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(areLogicPorts, postfix: new HarmonyMethod(typeof(BuildingDef_AreLogicPortsInValidPositions_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_AreLogicPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix)));
            }

            // Round 13: BuildingDef.MarkOverlappingPorts(GameObject, GameObject) —
            // PUBLIC instance (BuildingDef.cs:941-954), called from MarkArea for every
            // port cell a building claims. Without this, the replacement plan's
            // MarkArea tags the old building HasInvalidPorts → InvalidPortReporter
            // disables it (Functional flag) + "overlapping ports" status/notification,
            // and the tag survives plan cancellation (round-13 user report).
            MethodInfo markOverlappingPorts = FindMethod(typeof(BuildingDef), "MarkOverlappingPorts", typeof(GameObject), typeof(GameObject));
            if (markOverlappingPorts == null)
            {
                PUtil.LogError("could not resolve BuildingDef.MarkOverlappingPorts(GameObject, GameObject) — stale-port-tag suppression skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(markOverlappingPorts, prefix: new HarmonyMethod(typeof(BuildingDef_MarkOverlappingPorts_ReplaceBuildingMaterial__Patch), nameof(BuildingDef_MarkOverlappingPorts_ReplaceBuildingMaterial__Patch.Prefix)));
            }

            // Stage 6: Constructable.FinishConstruction(UtilityConnections, WorkerBase)
            // — PRIVATE void instance method (Constructable.cs:223-291). The mandatory
            // N >= 2 exact-overlap completion fix (R9 Q2.4): the anchor branch's
            // SimCellOccupier.DestroySelf fires the completion callback immediately per
            // cell (SimCellOccupier.cs:175-176), so FinishConstruction re-enters N times —
            // fire 1's non-anchor sweep (Constructable.cs:228-257) re-finds the same
            // still-dying old building at every non-anchor cell (DestroySelf(null) NRE,
            // double refund/Trigger/DeleteObject), and fires 2..N stack ghost duplicates
            // via Def.Build (:259). The short-circuiting PREFIX bails on re-entry (the
            // private `finished` field, :51/:289) and, on fire 1 only, clears the anchor
            // candidate's slot-1 entries reference-matched BEFORE the original body's
            // sweep. Void target — plain `bool` prefix return, no `__result`. Resolved
            // like the other targets via the byref/NonPublic-tolerant FindMethod;
            // attached programmatically; NO [HarmonyPatch] attribute (PatchAll in
            // UserMod2.OnLoad would ignore it anyway).
            MethodInfo finishConstruction = FindMethod(typeof(Constructable), "FinishConstruction", typeof(UtilityConnections), typeof(WorkerBase));
            if (finishConstruction == null)
            {
                PUtil.LogError("could not resolve Constructable.FinishConstruction(UtilityConnections, WorkerBase) — completion fix-up prefix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(finishConstruction, prefix: new HarmonyMethod(typeof(Constructable_FinishConstruction_ReplaceBuildingMaterial__Patch), nameof(Constructable_FinishConstruction_ReplaceBuildingMaterial__Patch.Prefix)));
            }
        }

        /// <summary>
        /// Resolves a method by name + per-parameter underlying type, normalizing
        /// byref parameters: an `out T` parameter is reported by the runtime as
        /// T& (IsByRef), so both sides are reduced to the underlying type before
        /// comparing (ParameterType.GetElementType() on T& yields T). First full
        /// match wins, null if none — callers log a skip rather than crash.
        /// BindingFlags.Static included so the declared-only scan also finds the
        /// static Assets.AddBuildingDef target; instance lookups are unaffected —
        /// no C# signature is both static and instance.
        /// </summary>
        private static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)
        {
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.Name != name)
                {
                    continue;
                }
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != underlyingTypes.Length)
                {
                    continue;
                }
                bool match = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt.IsByRef)
                    {
                        pt = pt.GetElementType();
                    }
                    if (pt != underlyingTypes[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return m;
                }
            }
            return null;
        }

        // Stage 3: every def this mod's Assets.AddBuildingDef postfix has injected
        // replacement metadata into. Populated in the postfix; robust marker for the
        // later patches (BuildTool.TryBuild, FinishConstruction, IsValidReplaceLocation).
        private static readonly HashSet<BuildingDef> ModDefs = new HashSet<BuildingDef>();

        /// <summary>
        /// Stage 3: is this one of the defs the mod has targeted? Primary check is the
        /// ModDefs set; the per-def ReplacementTags identity is a fallback in case the
        /// def was re-registered in a fresh process (set not yet populated for this
        /// instance): the injected list has exactly one entry — the def's own tag.
        /// </summary>
        private static bool IsModDef(BuildingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (ModDefs.Contains(def))
            {
                return true;
            }
            return def.ReplacementTags != null && def.ReplacementTags.Count == 1 && def.ReplacementTags[0] == def.Tag;
        }

        // Stage 3: shared replacement-candidate layer set, assigned to every targeted
        // def. A placed same-pref old building lives on the Building layer (the
        // regular-building default, BuildingDef.cs:123), so the candidate lookup
        // (GetReplacementCandidate, BuildingDef.cs:324-345) must name exactly that
        // layer. GetReplacementCandidate applies no tag filter (only the
        // BuildingComplete != null check, :333), so no other layer may be listed:
        // unrelated occupants there would be routed into the destroy/refund path.
        // Shared instance, created once, never mutated (door-mod pattern).
        private static readonly List<ObjectLayer> SharedReplacementCandidateLayers = new List<ObjectLayer>()
        {
            ObjectLayer.Building
        };

        /// <summary>
        /// Stage 3: D2 scope filter — a "regular building" def: lives on the Building
        /// layer (walls/conduits/tiles/attachables/gantries out), is not a tile piece
        /// (TileLayer == NumLayers, == !IsTilePiece, BuildingDef.cs:276), carries no
        /// native replacement machinery yet (ReplacementLayer/ReplacementTags null),
        /// has not been opted out of replacement (Replaceable), and is not a door
        /// (excluded by design — the door mod covers them). BuildingComplete must
        /// exist: IsDoorDef reads its components, and AddBuildingDef fires after the
        /// full config chain, so it is in final state here.
        /// </summary>
        private static bool IsRegularBuildingDef(BuildingDef def)
        {
            if (def == null || def.BuildingComplete == null)
            {
                return false;
            }
            return def.ObjectLayer == ObjectLayer.Building          // regular building layer (walls/conduits/tiles/attachables/gantries out)
                && def.TileLayer == ObjectLayer.NumLayers          // == !def.IsTilePiece (BuildingDef.cs:276)
                && def.ReplacementLayer == ObjectLayer.NumLayers   // no native replacement machinery yet
                && def.ReplacementTags == null                     // no native replacement tags yet (idempotency marker too)
                && def.Replaceable                                 // game has not opted this def out
                && !IsDoorDef(def);                                // doors excluded by design
        }

        /// <summary>
        /// Verbatim from the reference mod (BuildDoorOverWall): generic door-def test.
        /// Every native door adds the Door component (Door.cs:7) or sets
        /// CopyBuildingSettings.copyGroupTag = GameTags.Door — safe to run here because
        /// Assets.AddBuildingDef fires after the full config chain, so the Door
        /// component and CopyBuildingSettings are in final state.
        /// </summary>
        private static bool IsDoorDef(BuildingDef def)
        {
            GameObject go = def.BuildingComplete;
            if (go == null)
            {
                return false;
            }
            CopyBuildingSettings cbs = go.GetComponent<CopyBuildingSettings>();
            if (cbs != null && cbs.copyGroupTag == GameTags.Door)
            {
                return true;
            }
            bool hasDoorComponent = go.GetComponent<Door>() != null;
            return hasDoorComponent;
        }

        /// <summary>
        /// Stage 4: the strict exact-overlap condition from R9. A drag over a placed
        /// building is "exact overlap" iff the anchor candidate at the drag position has
        /// the SAME anchor cell AND the SAME orientation as the drag — with the round-14
        /// exception for unrotatable buildings: a candidate WITHOUT a Rotatable
        /// component (PermittedRotations.Unrotatable — BuildingLoader.cs:176 adds the
        /// component only when def.PermittedRotations != Unrotatable) has no rotation
        /// degree of freedom and the game treats its orientation as Neutral
        /// (Building.cs:30-40), so the anchor cell alone decides exactness there.
        /// The candidate is recovered with the native single-anchor-cell lookup
        /// (def.GetReplacementCandidate, BuildingDef.cs:324-345) — under the Stage-3
        /// metadata that reads exactly the Building layer where a placed same-pref
        /// old building lives. The candidate's anchor cell uses the same convention the
        /// game uses for plans (Grid.PosToCell(transform.GetLocalPosition()),
        /// Constructable.cs:160). `candidate` is always out (null when there is none).
        /// Shared by the Stage-4 TryBuild prefix and the Stage-5 tint postfix.
        /// </summary>
        private static bool IsExactOverlap(BuildingDef def, Vector3 pos, Orientation orientation, out GameObject candidate)
        {
            candidate = def.GetReplacementCandidate(Grid.PosToCell(pos));
            if (candidate == null)
            {
                return false;
            }
            // Round 14 (root cause of the unrotatable failure, e.g. pumps/generators/
            // batteries): the orientation comparison only applies when the candidate
            // is actually rotatable; same pref on both sides (CanReplace) guarantees
            // the same rotatability, so the mixed case is impossible.
            Rotatable rotatable = candidate.GetComponent<Rotatable>();
            if (rotatable != null && rotatable.GetOrientation() != orientation)
            {
                return false;
            }
            return Grid.PosToCell(candidate.transform.GetLocalPosition()) == Grid.PosToCell(pos);
        }

        /// <summary>
        /// Stage 4: the "same material" predicate from R11 — the native same-material
        /// no-op gate (BuildTool.cs:366-371) in the form the Stage-5 tint postfix needs
        /// to decide red vs white on an exact-overlap hover. True when the drag's
        /// selected element equals the candidate building's primary element. Includes the
        /// native snow-tag quirk (BuildTool.cs:367-370 / door mod Mod.cs:988-994): an
        /// element whose tag hashes to 1542131326 is normalized to SimHashes.Snow so the
        /// comparison matches what the native body compares. Plain comparison otherwise.
        /// </summary>
        private static bool IsSameMaterial(GameObject candidate, IList<Tag> selected)
        {
            if (candidate == null || selected == null || selected.Count == 0)
            {
                return false;
            }
            PrimaryElement primary = candidate.GetComponent<PrimaryElement>();
            if (primary == null)
            {
                return false;
            }
            Tag tag = primary.Element.tag;
            if (tag.GetHash() == 1542131326)
            {
                tag = SimHashes.Snow.CreateTag();
            }
            return selected[0] == tag;
        }

        // Stage 4: reject shifted/rotated drags. PREFIX on private void
        // BuildTool.TryBuild(int) (BuildTool.cs:307-388). The native body is purely
        // geometric and would happily create a replacement plan for a drag whose
        // footprint merely OVERLAPS the old building (R9 Q2 cases 2–4: rotated-in-place,
        // rotated+shifted, or shifted-overlapping) — the spec allows ONLY the exact
        // overlap (same pref via CanReplace, same anchor cell, same orientation). This
        // prefix is pure rejection: it returns false (skip original) only when a
        // replacement candidate the native fallback would act on exists at the anchor
        // but is not an exact overlap. No Grid writes. The no-candidate case (R9 Q2
        // case 5) is left to run natively (a plain build may still proceed). The
        // same-material no-op (BuildTool.cs:366-371) stays native. Instant-build drags
        // are out of scope (debug mode).
        //
        // Harmony note: the target is VOID, so the short-circuit is a plain `bool`
        // prefix return (false = do not run the original). There is NO `out bool
        // __result` — that form only exists for a method that returns a value.
        public static class BuildTool_TryBuild_ReplaceBuildingMaterial__Patch
        {
            public static bool Prefix(BuildTool __instance, int cell)
            {
                BuildingDef def = __instance.def;
                if (def == null || !IsModDef(def))
                {
                    // Not a mod-targeted def: run the original untouched.
                    return true;
                }
                // Match TryBuild's own drag-time conversion exactly (BuildTool.cs:316).
                Vector3 pos = Grid.CellToPosCBC(cell, Grid.SceneLayer.Building);
                GameObject candidate;
                if (IsExactOverlap(def, pos, __instance.buildingOrientation, out candidate))
                {
                    // Exact overlap (R9 Q2 case 1): the native fallback creates the plan.
                    // Round 13: heal a STALE HasInvalidPorts tag on the candidate left by
                    // an earlier (pre-fix) replacement attempt — the old building's
                    // InvalidPortReporter disables it via the Functional flag and the
                    // tag never clears natively (it only clears when the port cell is
                    // re-marked with an empty occupant, which never happens here). The
                    // tag can never be a genuine third-party overlap: only this mod's
                    // flow ever marks a plan over the candidate's own port cells.
                    if (candidate.HasTag(GameTags.HasInvalidPorts))
                    {
                        candidate.RemoveTag(GameTags.HasInvalidPorts);
#if DEBUG
                        PUtil.LogDebug("cleared stale HasInvalidPorts tag from replacement candidate: {0}".F(candidate.name));
#endif
                    }
#if DEBUG
                    LogFallbackInputs(__instance, cell, pos);
#endif
                    return true;
                }
                // Not an exact overlap. Reject only when the native fallback WOULD have
                // found and acted on a same-pref candidate here (a shifted/rotated drag
                // over the old building); otherwise a normal build may proceed.
                if (candidate != null && def.CanReplace(candidate))
                {
#if DEBUG
                    Rotatable rejectedRot = candidate.GetComponent<Rotatable>();
                    PUtil.LogDebug("rejected drag: def={0} cell={1} orientation={2} | candidate anchor={3} candidateOrientation={4} rotatableNull={5} (shifted/rotated, not exact overlap)".F(def.PrefabID, cell, __instance.buildingOrientation, Grid.PosToCell(candidate.transform.GetLocalPosition()), rejectedRot == null ? "?" : rejectedRot.GetOrientation(), rejectedRot == null));
#endif
                    return false;
                }
#if DEBUG
                // Pass-through (no candidate, or a candidate the native fallback would
                // ignore): previously silent — now names which sub-case ran, so a
                // click with zero plan and zero other log lines is explained.
                PUtil.LogDebug("pass-through drag: def={0} cell={1} candidate={2} canReplace={3}".F(def.PrefabID, cell, candidate != null ? candidate.name : "null", candidate != null && def.CanReplace(candidate)));
#endif
                return true;
            }

            // Stage 6 (debug round): reports, right after the original body ran, whether a
            // plan actually landed in the def's ReplacementLayer slot at the drag cell —
            // this distinguishes "native fallback spawned the plan" from "silent failure"
            // without guessing (the user-reported transformer case). Read-only.
            public static void Postfix(BuildTool __instance, int cell)
            {
                BuildingDef def = __instance.def;
                if (def == null || !IsModDef(def))
                {
                    return;
                }
#if DEBUG
                GameObject slotObject = Grid.Objects[cell, (int)def.ReplacementLayer];
                PUtil.LogDebug("drag result: def={0} cell={1} replacementSlot={2} isConstructable={3}".F(def.PrefabID, cell, slotObject != null ? slotObject.name : "null", slotObject != null && slotObject.GetComponent<Constructable>() != null));
#endif
            }

            // Stage 6 (debug round): replicates the native fallback's own gates
            // (BuildTool.cs:350-371) and probes the 5-arg IsValidPlaceLocation
            // (replace_tile: true) DIRECTLY — bypassing the 4-arg hover postfix — so
            // Player.log names the exact gate that aborts the replacement plan spawn on
            // an exact-overlap drag. Read-only: no Grid writes.
            private static void LogFallbackInputs(BuildTool tool, int cell, Vector3 pos)
            {
                BuildingDef def = tool.def;
                GameObject candidate = def.GetReplacementCandidate(cell);
                bool occupied = false;
                def.RunOnArea(cell, tool.buildingOrientation, (int offsetCell) =>
                {
                    if (def.IsReplacementLayerOccupied(offsetCell))
                    {
                        occupied = true;
                    }
                });
                BuildingComplete complete = candidate != null ? candidate.GetComponent<BuildingComplete>() : null;
                PrimaryElement primary = candidate != null ? candidate.GetComponent<PrimaryElement>() : null;
                Tag candidateTag = primary != null ? primary.Element.tag : null;
                if (candidateTag != null && candidateTag.GetHash() == 1542131326)
                {
                    candidateTag = SimHashes.Snow.CreateTag();
                }
                IList<Tag> selected = tool.selectedElements;
                bool materialDiffers = selected != null && selected.Count > 0 && candidateTag != null && selected[0] != candidateTag;
                string probeReason = "(not run)";
                bool probeValid = def.IsValidPlaceLocation(tool.visualizer, pos, tool.buildingOrientation, replace_tile: true, out probeReason);
                PUtil.LogDebug("exact drag: def={0} cell={1} ReplacementLayer={2} candidate={3} candidateAnchor={4} replacementLayerOccupied={5} slot11Occupied={6} BuildingComplete={7} Replaceable={8} CanReplace={9} selectedCount={10} selected0={11} candidateTag={12} materialDiffers={13} probeReplaceValid={14} probeReason={15}".F(def.PrefabID, cell, def.ReplacementLayer, candidate != null ? candidate.name : "null", candidate != null ? Grid.PosToCell(candidate.transform.GetLocalPosition()) : -1, occupied, Grid.Objects[cell, (int)def.ReplacementLayer] != null, complete != null, complete != null && complete.Def.Replaceable, candidate != null && def.CanReplace(candidate), selected != null ? selected.Count : -1, selected != null && selected.Count > 0 ? selected[0] : "n/a", candidateTag, materialDiffers, probeValid, probeReason));
            }
        }

        // Stage 6 (debug round 9): BuildTool.OnDragTool(int, int) — PROTECTED override
        // (BuildTool.cs:302-305); the drag tool's per-cell callback that unconditionally
        // calls TryBuild. The DEBUG postfix proves a world click/drag actually reached
        // the build tool, so a test run with no plan and no other log lines can no
        // longer hide in the "click never hit TryBuild" hole (hypothesis C). Fires on
        // every drag event while a mod def is selected (including the no-op repeats
        // TryBuild's dedup guard swallows at BuildTool.cs:309). DEBUG-only, read-only,
        // mod-defs only; attached programmatically; NO [HarmonyPatch] attribute.
        public static class BuildTool_OnDragTool_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildTool __instance, int cell, int distFromOrigin)
            {
                BuildingDef def = __instance.def;
                if (def == null || !IsModDef(def))
                {
                    return;
                }
#if DEBUG
                PUtil.LogDebug("drag entry: def={0} cell={1} dist={2}".F(def.PrefabID, cell, distFromOrigin));
#endif
            }
        }

        // Stage 5: bidirectional preview tint. POSTFIX on public bool
        // BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)
        // (BuildingDef.cs:1184-1207). The native body is purely geometric + a layer test
        // (R10.1) — with the Stage-3 metadata it returns true whenever every placement cell
        // has a Building-layer occupant and the ReplacementTile slot is empty: exact overlap,
        // same-material exact overlap AND symmetric in-place rotations all come out white,
        // shifts red. It carries no material or position-strictness signal, so the spec's
        // "red 'занято' as without the mod" for a same-material drag is NOT delivered
        // natively. Hence, unlike the door mod's one-way flip (`if (__result) return;`),
        // this postfix OVERWRITES `__result` in both directions for mod defs:
        //   - exact overlap + cross-material  → white (the only spec-valid replacement case);
        //   - exact overlap + same material   → red ("занято", as without the mod);
        //   - symmetric rotation in place (R9 case 2) → red (not exact overlap);
        //   - shifts (R9 cases 3–4) → red — the native false is already correct there
        //     (cells outside the old building have no slot-1 occupant).
        // Non-mod defs keep the native result untouched (always false for them: their
        // ReplacementLayer == NumLayers fails the first check, BuildingDef.cs:1186).
        //
        // Verdict source: the shared helpers IsExactOverlap (Stage 4) and
        // IsSameMaterial (Stage 4). The drag's selected element comes from
        // BuildTool.Instance.selectedElements — the private field BuildTool.Activate
        // stores from the material selection panel (BuildTool.cs:18,116-118), public in
        // the publicized game reference. The sole caller is UpdateVis, which runs only
        // while the tool is active, so Instance is live in practice; the null-guards
        // treat an unavailable selection as "not the same material" (keeps the native
        // white verdict on an exact-overlap hover). No Grid writes, no logging here
        // (UpdateVis fires on every mouse move).
        public static class BuildingDef_IsValidReplaceLocation_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __instance, Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer, ref bool __result)
            {
                if (!IsModDef(__instance))
                {
                    return;
                }
                GameObject candidate;
                bool exact = IsExactOverlap(__instance, pos, orientation, out candidate);
                IList<Tag> selected = BuildTool.Instance?.selectedElements;
                bool same = exact && IsSameMaterial(candidate, selected);
                __result = exact && !same;
            }
        }

        // Stage 6 (debug round): hover-card cosmetic. POSTFIX on public bool
        // BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)
        // (BuildingDef.cs:1098-1102) — the overload the build hover card calls to draw
        // its red fail-reason line (BuildToolHoverTextCard.cs:48-55). The card passes
        // replace_tile: false, so an exact overlap always reports OCCUPIED even when the
        // replacement placement would actually succeed — the user-reported red "Must be
        // built in unoccupied space" under a white ghost. The postfix rewrites ONLY the
        // out parameter, never the bool: for a valid cross-material exact overlap it
        // blanks the misleading text. The native false stays (TryPlace uses the 6-arg
        // overload directly; UpdateVis discards its reason). Same-material overlap keeps
        // the red "занято" (the spec verdict). No Grid writes.
        public static class BuildingDef_IsValidPlaceLocation_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, Vector3 pos, Orientation orientation, ref string fail_reason, ref bool __result)
            {
                if (__result || !IsModDef(__instance))
                {
                    return;
                }
                GameObject candidate;
                if (!IsExactOverlap(__instance, pos, orientation, out candidate))
                {
                    return;
                }
                IList<Tag> selected = BuildTool.Instance?.selectedElements;
                if (IsSameMaterial(candidate, selected))
                {
                    return;
                }
                fail_reason = "";
            }
        }

        // Stage 6 (debug round 10, root-cause fix helpers): the exact-overlap candidate
        // for a cross-material drag — the single gate both port-check postfixes share.
        // Returns the old-building GO, or null when the native rejection must stand
        // (not a mod def / not an exact overlap / same material — the last one is the
        // spec's red "занято" case, which never reaches a replace_tile: true port
        // check in the first place).
        private static GameObject GetReplacementOverlapCandidate(BuildingDef def, int cell, Orientation orientation)
        {
            if (!IsModDef(def))
            {
                return null;
            }
            GameObject candidate;
            if (!IsExactOverlap(def, Grid.CellToPosCBC(cell, Grid.SceneLayer.Building), orientation, out candidate))
            {
                return null;
            }
            if (IsSameMaterial(candidate, BuildTool.Instance?.selectedElements))
            {
                return null;
            }
            return candidate;
        }

        // Every power-port cell of the NEW building (same def + same anchor + same
        // orientation ⇒ same rotated offsets as the OLD building) must be empty or
        // owned by the candidate's own WireConnectors entry. Any other occupant (a
        // wire bridge, another building) keeps the native rejection.
        private static bool PowerPortEntriesOwnedBy(BuildingDef def, int cell, Orientation orientation, GameObject candidate)
        {
            if (def.RequiresPowerInput)
            {
                GameObject entry = Grid.Objects[Grid.OffsetCell(cell, Rotatable.GetRotatedCellOffset(def.PowerInputOffset, orientation)), (int)ObjectLayer.WireConnectors];
                if (entry != null && entry != candidate)
                {
                    return false;
                }
            }
            if (def.RequiresPowerOutput)
            {
                GameObject entry = Grid.Objects[Grid.OffsetCell(cell, Rotatable.GetRotatedCellOffset(def.PowerOutputOffset, orientation)), (int)ObjectLayer.WireConnectors];
                if (entry != null && entry != candidate)
                {
                    return false;
                }
            }
            return true;
        }

        // Same ownership rule for the primary conduit ports; the layer comes from the
        // same Grid.GetObjectLayerForConduitType the native MarkArea/IsValidConduit-
        // Connection pair uses, so it always matches whatever the old building wrote.
        private static bool ConduitPortEntriesOwnedBy(BuildingDef def, int cell, Orientation orientation, GameObject candidate)
        {
            if (def.InputConduitType != ConduitType.None)
            {
                GameObject entry = Grid.Objects[Grid.OffsetCell(cell, Rotatable.GetRotatedCellOffset(def.UtilityInputOffset, orientation)), (int)Grid.GetObjectLayerForConduitType(def.InputConduitType)];
                if (entry != null && entry != candidate)
                {
                    return false;
                }
            }
            if (def.OutputConduitType != ConduitType.None)
            {
                GameObject entry = Grid.Objects[Grid.OffsetCell(cell, Rotatable.GetRotatedCellOffset(def.UtilityOutputOffset, orientation)), (int)Grid.GetObjectLayerForConduitType(def.OutputConduitType)];
                if (entry != null && entry != candidate)
                {
                    return false;
                }
            }
            return true;
        }

        // Stage 6 (debug round 10): BuildingDef.ArePowerPortsInValidPositions(GameObject,
        // int, Orientation, out string) — private, BuildingDef.cs:1391-1421. Lifts the
        // "power connectors cannot overlap" verdict for a cross-material exact overlap
        // whose occupied port cells are the candidate's own entries: the old building
        // is exactly what the native replacement flow is about to destroy, so its ports
        // must not block the replacement plan. Fires only on replace_tile: true calls
        // (TryReplaceTile / instant-build path); the hover-card and TryPlace paths fail
        // on occupancy long before reaching the port checks.
        public static class BuildingDef_ArePowerPortsInValidPositions_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, Orientation orientation, ref string fail_reason, ref bool __result)
            {
                if (__result || fail_reason != STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_WIRECONNECTORS_OVERLAP)
                {
                    return;
                }
                GameObject candidate = GetReplacementOverlapCandidate(__instance, cell, orientation);
                if (candidate == null)
                {
                    return;
                }
                if (PowerPortEntriesOwnedBy(__instance, cell, orientation, candidate))
                {
                    __result = true;
                    fail_reason = null;
#if DEBUG
                    PUtil.LogDebug("power port overlap allowed for replacement: def={0} cell={1} candidate={2}".F(__instance.PrefabID, cell, candidate.name));
#endif
                }
            }
        }

        // Stage 6 (debug round 10): BuildingDef.AreConduitPortsInValidPositions(GameObject,
        // int, Orientation, out string) — private, BuildingDef.cs:1423-1487; identical
        // fix for gas/liquid/solid conduit buildings (primary ports; secondary
        // ISecondaryInput/ISecondaryOutput ports share the same cell-ownership rule but
        // are rare on regular buildings and stay natively rejected).
        public static class BuildingDef_AreConduitPortsInValidPositions_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, Orientation orientation, ref string fail_reason, ref bool __result)
            {
                if (__result
                    || (fail_reason != STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_GASPORTS_OVERLAP
                        && fail_reason != STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_LIQUIDPORTS_OVERLAP
                        && fail_reason != STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_SOLIDPORTS_OVERLAP))
                {
                    return;
                }
                GameObject candidate = GetReplacementOverlapCandidate(__instance, cell, orientation);
                if (candidate == null)
                {
                    return;
                }
                if (ConduitPortEntriesOwnedBy(__instance, cell, orientation, candidate))
                {
                    __result = true;
                    fail_reason = null;
#if DEBUG
                    PUtil.LogDebug("conduit port overlap allowed for replacement: def={0} cell={1} candidate={2}".F(__instance.PrefabID, cell, candidate.name));
#endif
                }
            }
        }

        // Stage 6 (debug round 12, root-cause fix, part 2): logic ports. The native
        // check (BuildingDef.cs:1558-1586) is scene-based: it takes ALL
        // logicCircuitManager vis-elements and conflicts them against the ghost's
        // LogicPorts. Both sides register into that scene list — the OLD building at
        // spawn (LogicPorts.CreatePhysicalPorts → LogicEventSender/LogicEventHandler,
        // LogicPorts.cs:221/256) and the GHOST when the native check itself calls
        // HackRefreshVisualizers (CreateVisualizers → LogicPortVisualizer,
        // LogicPorts.cs:163/174). Same def + anchor + orientation ⇒ identical port
        // cells, so the ghost's ports ALWAYS conflict with the candidate's own ports
        // — the third gate behind the same "white ghost, no plan" (power ports were
        // gate 1, lifted in round 11; the clean session's log then exposed this one).
        // Vis-elements carry no owner reference, so ownership is proven by per-cell
        // counting: at every ghost port cell, the scene elements that are not the
        // ghost's own must be no more than the candidate's own port count there. Any
        // surplus element (another gate or building) keeps the native rejection.
        private static bool LogicPortElementsOwnedBy(GameObject source_go, GameObject candidate)
        {
            LogicPorts prefabLogic = source_go.GetComponent<LogicPorts>();
            if (prefabLogic == null)
            {
                return false;
            }
            List<ILogicUIElement> scene = Game.Instance.logicCircuitManager.GetVisElements();
            LogicPorts candidateLogic = candidate.GetComponent<LogicPorts>();

            Dictionary<int, int> sceneCount = new Dictionary<int, int>();
            foreach (ILogicUIElement v in scene)
            {
                int c = v.GetLogicUICell();
                int n;
                sceneCount[c] = sceneCount.TryGetValue(c, out n) ? n + 1 : 1;
            }
            Dictionary<int, int> ownCount = new Dictionary<int, int>();
            if (prefabLogic.inputPorts != null)
            {
                foreach (ILogicUIElement p in prefabLogic.inputPorts)
                {
                    int c = p.GetLogicUICell();
                    int n;
                    ownCount[c] = ownCount.TryGetValue(c, out n) ? n + 1 : 1;
                }
            }
            if (prefabLogic.outputPorts != null)
            {
                foreach (ILogicUIElement p in prefabLogic.outputPorts)
                {
                    int c = p.GetLogicUICell();
                    int n;
                    ownCount[c] = ownCount.TryGetValue(c, out n) ? n + 1 : 1;
                }
            }
            Dictionary<int, int> candidateCount = new Dictionary<int, int>();
            if (candidateLogic != null)
            {
                if (candidateLogic.inputPorts != null)
                {
                    foreach (ILogicUIElement p in candidateLogic.inputPorts)
                    {
                        int c = p.GetLogicUICell();
                        int n;
                        candidateCount[c] = candidateCount.TryGetValue(c, out n) ? n + 1 : 1;
                    }
                }
                if (candidateLogic.outputPorts != null)
                {
                    foreach (ILogicUIElement p in candidateLogic.outputPorts)
                    {
                        int c = p.GetLogicUICell();
                        int n;
                        candidateCount[c] = candidateCount.TryGetValue(c, out n) ? n + 1 : 1;
                    }
                }
            }
            foreach (KeyValuePair<int, int> own in ownCount)
            {
                int sceneAt = 0;
                int candidateAt = 0;
                sceneCount.TryGetValue(own.Key, out sceneAt);
                candidateCount.TryGetValue(own.Key, out candidateAt);
                if (sceneAt - own.Value > candidateAt)
                {
                    return false;
                }
            }
            return true;
        }

        // Stage 6 (debug round 12): BuildingDef.AreLogicPortsInValidPositions(GameObject,
        // int, out string) — private, BuildingDef.cs:1558-1586, last gate of the
        // IsAreaClear &&-chain (:789). Note the signature has NO orientation parameter —
        // the ghost's own Rotatable orientation is authoritative (the build tool sets
        // it to the drag orientation), with the round-14 Neutral fallback for a ghost
        // WITHOUT a Rotatable component (unrotatable def — the game convention from
        // Building.cs:30-40). Lifts the "automation ports cannot overlap" verdict under
        // the same strict gates as the power/conduit fixes.
        public static class BuildingDef_AreLogicPortsInValidPositions_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, ref string fail_reason, ref bool __result)
            {
                if (__result || fail_reason != STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED)
                {
                    return;
                }
                // Round 14 fix (part 1b): a ghost WITHOUT a Rotatable component
                // (unrotatable def — pumps, generators, batteries, kilns) has no
                // orientation degree of freedom; Neutral (the game convention from
                // Building.cs:30-40) is the authoritative value. The round-14 part 1
                // edit updated this method's doc but missed the body — a pump ghost
                // then hit the early return and the native logic-port rejection
                // stood (Player_4.log: "Порты автоматизации не могут совпадать").
                Rotatable rotatable = source_go.GetComponent<Rotatable>();
                Orientation ghostOrientation = rotatable != null ? rotatable.GetOrientation() : Orientation.Neutral;
                GameObject candidate = GetReplacementOverlapCandidate(__instance, cell, ghostOrientation);
                if (candidate == null)
                {
                    return;
                }
                if (LogicPortElementsOwnedBy(source_go, candidate))
                {
                    __result = true;
                    fail_reason = null;
#if DEBUG
                    PUtil.LogDebug("logic port overlap allowed for replacement: def={0} cell={1} candidate={2}".F(__instance.PrefabID, cell, candidate.name));
#endif
                }
            }
        }

        // Round 13: is (existing, replaced) THIS MOD'S replacement pair — a construction
        // plan of a mod def placed at exactly the anchor cell + orientation (where
        // both sides carry a Rotatable component; unrotatable pairs compare the anchor
        // only — round 14) of an already-complete building? The native game can never
        // produce such a pair (two complete buildings can never share an anchor), so
        // this is precise: it is the moment the replacement plan's MarkArea rewrites
        // the old building's own port cells.
        private static bool IsReplacementPair(GameObject existing, GameObject replaced)
        {
            if (existing == null || replaced == null || existing == replaced)
            {
                return false;
            }
            if (replaced.GetComponent<Constructable>() == null)
            {
                return false;
            }
            if (existing.GetComponent<BuildingComplete>() == null)
            {
                return false;
            }
            // Round 14: an unrotatable pair (neither object has a Rotatable component —
            // PermittedRotations.Unrotatable, BuildingLoader.cs:176) has no orientation
            // to compare: anchor equality alone identifies the replacement pair. The
            // mixed case is impossible (same pref ⇒ same rotatability).
            Rotatable existingRot = existing.GetComponent<Rotatable>();
            Rotatable planRot = replaced.GetComponent<Rotatable>();
            if (existingRot != null && planRot != null && existingRot.GetOrientation() != planRot.GetOrientation())
            {
                return false;
            }
            return Grid.PosToCell(existing.transform.GetLocalPosition()) == Grid.PosToCell(replaced.transform.GetLocalPosition());
        }

        // Round 13: BuildingDef.MarkOverlappingPorts(GameObject, GameObject) — public
        // instance, BuildingDef.cs:941-954, called from MarkArea (:850-934) for every
        // port cell the marking building claims (conduit layers, power layer 29,
        // wire-bridge cells, secondary conduit). When the occupant is another object
        // it tags `existing` with GameTags.HasInvalidPorts.
        //
        // Why it breaks this mod: the replacement plan's MarkArea runs while the old
        // building is still in the world, so the old building's own power-port cells
        // (layer 29, written by ITS MarkArea at spawn) read back the OLD building as
        // the occupant → it gets tagged → its InvalidPortReporter (InvalidPortReporter.
        // cs:22-35) flips the Functional flag ports_not_overlapping OFF (disabling the
        // old building) and shows the "overlapping ports" status item + notification
        // (Db: InvalidPortOverlap, STRINGS.BUILDING "Building has overlapping ports").
        // The tag never clears afterward (only a re-mark with an empty occupant would),
        // so if the plan is CANCELLED the old building stays broken — the round-13
        // user report: "if the plan is cancelled the error stays and the transformer
        // stops working".
        //
        // The overlap here is the EXPECTED state of the replacement flow, not a real
        // error: the plan sits at exactly the old building's anchor with the same
        // orientation, so every port cell it claims is the old building's own cell.
        // Suppress the marking for that pair (and only that pair — every other
        // overlap keeps the native tag). The stale-tag heal on the TryBuild prefix
        // covers candidates tagged by pre-fix builds.
        public static class BuildingDef_MarkOverlappingPorts_ReplaceBuildingMaterial__Patch
        {
            public static bool Prefix(BuildingDef __instance, GameObject existing, GameObject replaced)
            {
                if (existing == null || existing == replaced || !IsModDef(__instance))
                {
                    return true;
                }
                if (IsReplacementPair(existing, replaced))
                {
#if DEBUG
                    PUtil.LogDebug("skipping overlapping-ports marking for replacement: existing={0} plan={1}".F(existing.name, replaced.name));
#endif
                    return false;
                }
                return true;
            }
        }

        // Stage 6: mandatory multi-cell exact-overlap completion fix. PREFIX on private
        // void Constructable.FinishConstruction(UtilityConnections, WorkerBase)
        // (Constructable.cs:223-291). Native completion is BROKEN for N >= 2 exact
        // overlap (R9 Q2.4): OnCompleteWork's anchor branch destroys the old building
        // via SimCellOccupier.DestroySelf(callback) (Constructable.cs:165-174), whose
        // loop fires the callback IMMEDIATELY per cell for air cells (always — the
        // old building sits in air) at SimCellOccupier.cs:175-176, so
        // FinishConstruction re-enters N times synchronously:
        //   fire 1: the non-anchor sweep (Constructable.cs:228-257, gated
        //           IsReplacementTile && PlacementOffsets.Length > 1) re-finds the
        //           SAME still-dying old building at every non-anchor cell — its
        //           slot-1 entries persist (DeleteObject at :213 is pending,
        //           OnCleanUp has not run) — and calls DestroySelf(null) (an NRE at
        //           SimCellOccupier.cs:176), plus a double refund (:245), double
        //           Trigger (:251) and double DeleteObject (:253) even without the NRE;
        //   fires 2..N: the plan's own DeleteObject (:290) only DEFERS destruction, so
        //               the callback guard (this != null && base.gameObject != null,
        //               :170) still passes and Def.Build (:259) stacks a ghost
        //               duplicate building.
        // The prefix mirrors the door mod's proven shape (BuildDoorOverWall/Mod.cs:
        // 1194-1441) MINUS its distinct-candidate D-fix branch — unreachable under
        // strict exact overlap: there is exactly one old building, same pref, exact
        // position, so the sweep can never find a "distinct candidate".
        //   1. Re-entry bail: the plan's private `finished` field (Constructable.cs:51,
        //      written at :289 by the first successful run) already true → skip
        //      original. Kills fires 2..N.
        //   2. Pre-sweep entry clear (fire 1 only, multi-cell plans): the anchor
        //      candidate — def.GetReplacementCandidate at the plan's anchor cell, the
        //      same convention OnCompleteWork uses (Constructable.cs:160) — is still
        //      fully grid-registered when fire 1 runs. Clear its slot-1 entries with
        //      reference-matched null writes over its whole placement area BEFORE the
        //      original body's sweep, so the sweep's per-cell GetReplacementCandidate
        //      (BuildingDef.cs:324-345) finds nothing and skips the destroy path.
        // After the fix the native remainder is correct: UnmarkArea (:258) clears the
        // plan's ReplacementTile slot; Def.Build (:259) marks the new building in
        // ObjectLayer at all N cells; the old building's own OnCleanUp unmark no-ops on
        // the already-cleared entries (reference-matched). 1x1 plans need no fix (the
        // sweep gate is false). No Grid writes except the reference-matched nulls.
        // NO [HarmonyPatch] attributes: attached programmatically in OnLoad.
        public static class Constructable_FinishConstruction_ReplaceBuildingMaterial__Patch
        {
            // Cached reflection handle to Constructable's private `finished` field
            // (Constructable.cs:51); resolved lazily on first use.
            private static FieldInfo s_finishedField;

            // Re-entry guard: reads Constructable's private `finished` field
            // (Constructable.cs:51; written at the end of the first successful
            // FinishConstruction, :289). Reflection because the publicizer
            // publicizes the type but not its members. Fail-open: a missing field
            // (game update) returns false — the guard is a no-op, old behavior.
            private static bool IsAlreadyFinished(Constructable constructable)
            {
                if (constructable == null)
                {
                    return true;
                }
                if (s_finishedField == null)
                {
                    s_finishedField = typeof(Constructable).GetField("finished", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (s_finishedField == null)
                {
                    return false;
                }
                return (bool)s_finishedField.GetValue(constructable);
            }

            // Clears the candidate's ObjectLayer grid entries at all of its placement
            // cells (Building.PlacementCells — the game's own rotated anchor+offsets
            // set for this placed building) — reference-matched `== candidate` null
            // writes, the same write shape as the native UnmarkArea
            // (BuildingDef.cs:976; a null write removes the ObjectLayers dictionary
            // entry, Grid.cs:452-465). No tile-piece branch: D2 excludes tile pieces
            // from the targeted defs. Returns the number of entries cleared.
            private static int ClearCandidateGridEntries(GameObject candidate)
            {
                if (candidate == null)
                {
                    return 0;
                }
                Building candidateBuilding = candidate.GetComponent<Building>();
                if (candidateBuilding == null || candidateBuilding.Def == null)
                {
                    return 0;
                }
                BuildingDef candidateDef = candidateBuilding.Def;
                int[] cells = candidateBuilding.PlacementCells;
                int cleared = 0;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    if (Grid.Objects[cell, (int)candidateDef.ObjectLayer] == candidate)
                    {
                        Grid.Objects[cell, (int)candidateDef.ObjectLayer] = null;
                        cleared++;
                    }
                }
                return cleared;
            }

            public static bool Prefix(Constructable __instance, UtilityConnections __0, WorkerBase __1)
            {
                // Constructable's `building` field is private ([MyCmpReq],
                // Constructable.cs:31) — the same component is fetched via
                // GetComponent, null-guarded.
                Building building = __instance.GetComponent<Building>();
                BuildingDef def = (building != null) ? building.Def : null;
                if (def == null || !IsModDef(def) || !__instance.IsReplacementTile)
                {
                    // Not a mod-targeted replacement plan: run the original untouched.
                    return true;
                }
                // Re-entry bail (fires 2..N of the anchor branch's per-cell completion
                // callback, SimCellOccupier.cs:175-176): the first run already built
                // the building (finished = true at Constructable.cs:289) and
                // DeleteObject()d the plan (:290, deferred only), so the callback
                // guard (:170) still passes and would build a ghost duplicate.
                if (IsAlreadyFinished(__instance))
                {
#if DEBUG
                    PUtil.LogDebug("FinishConstruction re-entry for plan {0} (finished == true) — skip".F(__instance.name));
#endif
                    return false;
                }
                // Pre-sweep entry clear (fire 1 only — the native sweep gate at
                // Constructable.cs:228 requires PlacementOffsets.Length > 1). The
                // anchor candidate is the still-dying old building (its DeleteObject
                // at Constructable.cs:213 is pending); under exact overlap its
                // placement area IS the new plan's area, so clearing all of its
                // slot-1 entries removes exactly what the sweep would re-find.
                if (def.PlacementOffsets.Length > 1)
                {
                    int anchorCell = Grid.PosToCell(__instance.transform.GetLocalPosition());
                    GameObject candidate = def.GetReplacementCandidate(anchorCell);
                    if (candidate != null)
                    {
                        int cleared = ClearCandidateGridEntries(candidate);
#if DEBUG
                        if (cleared > 0)
                        {
                            PUtil.LogDebug("FinishConstruction: cleared {0} grid entries of anchor candidate {1}".F(cleared, candidate.name));
                        }
#endif
                    }
                }
                // Let the original run: UnmarkArea (:258, plan's ReplacementTile slot),
                // Def.Build (:259, new building marked at all N cells), the anchor-
                // side old-building destruction already happened in OnCompleteWork.
                return true;
            }
        }

        // Stage 3: teach EVERY regular building def the replacement metadata so the
        // native replacement machinery (R3: BuildTool.TryBuild fallback → TryReplaceTile
        // → Constructable.OnSpawn/OnCompleteWork/FinishConstruction) can replace a
        // building with a new build of its own pref. Hooks Assets.AddBuildingDef
        // (Assets.cs:670, public static; sole caller BuildingConfigManager.RegisterBuilding,
        // BuildingConfigManager.cs:114), which fires exactly once per def at registration —
        // for every vanilla, DLC and mod IBuildingConfig — and AFTER the complete config
        // chain (DoPostConfigureComplete, BuildingConfigManager.cs:104).
        // Per-def injection (D1): ReplacementLayer = ReplacementTile (separate layer —
        // the same-layer option is dead, R8 D1.1), ReplacementCandidateLayers = shared
        // { Building }, ReplacementTags = FRESH per-def list { def.Tag } (NOT a shared
        // live list — CanReplace must match only the same pref, D1.3), Replaceable =
        // true (explicit; field default is already true, D1.4). EquivalentReplacementLayers
        // stays null (D1.5). NO [HarmonyPatch] attributes: attached programmatically in
        // OnLoad.
        public static class Assets_AddBuildingDef_ReplaceBuildingMaterial__Patch
        {
            public static void Postfix(BuildingDef __0)
            {
                if (__0 == null || !IsRegularBuildingDef(__0))
                {
                    return;
                }
                // Idempotency: never override a def that already carries its own
                // replacement tags (cannot happen for a fresh def passing the
                // D2 predicate, which requires ReplacementTags == null, but keep
                // the guard against re-registration).
                if (__0.ReplacementTags != null)
                {
                    return;
                }
                __0.ReplacementLayer = ObjectLayer.ReplacementTile;
                __0.ReplacementCandidateLayers = SharedReplacementCandidateLayers;
                __0.ReplacementTags = new List<Tag>() { __0.Tag };   // fresh per-def: own pref tag only
                __0.Replaceable = true;
                ModDefs.Add(__0);
#if DEBUG
                PUtil.LogDebug("injected replacement metadata into {0}".F(__0.PrefabID));
#endif
            }
        }
    }
}
