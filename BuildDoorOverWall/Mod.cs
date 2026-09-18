using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KMod;
using UnityEngine;

using PeterHan.PLib.Core;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (BuildingDef, DoorConfig, ObjectLayer, Tag, GameTags, Grid) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            //
            // TODO
            PUtil.LogDebug("[BuildDoorOverWall] Build <date> commit=<hash>");

            // base.OnLoad runs Harmony's PatchAll, which only applies types carrying
            // HarmonyPatch attributes (0Harmony decompiled: PatchAllUncategorized
            // filters by HasHarmonyAttribute, 0Harmony.decompiled.cs:6829). No
            // attribute-bound classes exist anymore in this mod: all five postfixes
            // (the two BuildingDef cosmetic ones, the 6-arg BuildingDef HasDoor-bypass
            // one, the BuildTool.TryBuild one and the Assets.AddBuildingDef metadata
            // one) are attached PROGRAMMATICALLY
            // below, and PatchAll therefore ignores every patch class in the file.
            //
            // The two BuildingDef postfixes below are attached PROGRAMMATICALLY
            // instead of via attributes, because the IsValidPlaceLocation target
            // has an `out` parameter and attributes cannot express it: game
            // Harmony v2 resolves [HarmonyPatch] parameter types via
            // Type.GetMethod(name, allDeclared, null, paramTypes, [])
            // (AttributePatch, 0Harmony.decompiled.cs:5350), and plain
            // typeof(string) does not match the `out string` parameter — only
            // string& (ByRef) does, and a byref Type is not a legal attribute
            // constant expression (CS0182 in attribute position). The attribute
            // classes for
            // these two postfixes therefore carry NO [HarmonyPatch] attributes;
            // PatchAll ignores them and we patch them here via the programmatic
            // Harmony.Patch(MethodBase, ...) API (0Harmony.decompiled.cs:6882),
            // with a byref-normalizing reflection lookup that tolerates whether
            // the runtime reports `out T` as T or T&.
            //
            // TARGET: the 4-arg overload IsValidPlaceLocation(GameObject, Vector3,
            // Orientation, out string) (BuildingDef.cs:1098). The FindMethod arity
            // check (4 parameters) guarantees the 6-arg canonical overload is not
            // matched, so the survival drag flow (TryPlace, BuildingDef.cs:467,
            // which uses the 6-arg overload) is never touched.
            MethodInfo validPlace = FindMethod(typeof(BuildingDef), "IsValidPlaceLocation",
                typeof(GameObject), typeof(Vector3), typeof(Orientation), typeof(string));
            if (validPlace == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) не найдена — hover-text/visualizer postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) — hover-text/visualizer postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу hover-text/visualizer postfix на {0}".F(validPlace));
#endif
                harmony.Patch(validPlace, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch), nameof(BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch.Postfix)));
            }
            MethodInfo validReplace = FindMethod(typeof(BuildingDef), "IsValidReplaceLocation",
                typeof(Vector3), typeof(Orientation), typeof(ObjectLayer), typeof(ObjectLayer));
            if (validReplace == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) не найдена — preview-tint postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) — preview-tint postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу preview-tint postfix на {0}".F(validReplace));
#endif
                harmony.Patch(validReplace, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch), nameof(BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch.Postfix)));
            }
            // 3. Stage 2.1: BuildTool.TryBuild(int) — private method, resolved like the others.
            // Патчинг для случая когда дверь ставим верхним концом в стену, а нижним - в воздухе
            MethodInfo tryBuild = FindMethod(typeof(BuildTool), "TryBuild", typeof(int));
            if (tryBuild == null)
            {
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildTool.TryBuild(int) — upper-cell replacement fallback postfix skipped (game build mismatch?)");
            }
            else
            {
              // Патчинг для случая когда дверь ставим верхним концом в стену, а нижним - в воздухе
                harmony.Patch(tryBuild, postfix: new HarmonyMethod(typeof(BuildTool_TryBuild_DoorReplacement__Patch), nameof(BuildTool_TryBuild_DoorReplacement__Patch.Postfix)));
            }
            // 4. Stage 2.2: Assets.AddBuildingDef(BuildingDef) — public static; FindMethod now
            // includes BindingFlags.Static so the declared-only scan finds it.
            MethodInfo addBuildingDef = FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef));
            if (addBuildingDef == null)
            {
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve Assets.AddBuildingDef(BuildingDef) — all-door replacement metadata postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(addBuildingDef, postfix: new HarmonyMethod(typeof(Assets_AddBuildingDef_DoorReplacement__Patch), nameof(Assets_AddBuildingDef_DoorReplacement__Patch.Postfix)));
            }
            // 5. Stage 3: BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation,
            // bool, out string, bool) — the 6-arg canonical overload every placement
            // validity check funnels into (all the other overloads and the game's
            // TryPlace, TryReplaceTile (BuildingDef.cs:490) and BuildTool.TryBuild call
            // sites). A wall-over-door placement fails there in IsAreaClear's NotInTiles
            // branch on the hard block `else if (Grid.HasDoor[cell]) flag = false;`
            // (BuildingDef.cs:756-759, anchor cell only) — the ONLY failing check for a
            // wall def on a door cell (see the 6-arg postfix docs below). The postfix
            // flips that one failure so the native TryReplaceTile replacement fallback
            // (BuildTool.cs:377-379) proceeds. FindMethod arity (6 parameters) matches
            // only this overload.
            MethodInfo validPlace6 = FindMethod(typeof(BuildingDef), "IsValidPlaceLocation",
                typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string), typeof(bool));
            if (validPlace6 == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool) не найдена — HasDoor-bypass postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool) — 6-arg HasDoor-bypass postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу 6-arg HasDoor-bypass postfix на {0}".F(validPlace6));
#endif
                harmony.Patch(validPlace6, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidPlaceLocation6_DoorReplacement__Patch), nameof(BuildingDef_IsValidPlaceLocation6_DoorReplacement__Patch.Postfix)));
            }
            // 6. Bug A fix: BuildingDef.IsValidBuildLocation(GameObject, int,
            // Orientation, bool, out string) — the CORE int overload (BuildingDef.cs:1221)
            // that every Vector3 overload funnels into and that Constructable's
            // construction-start re-check calls (Constructable.PlaceDiggables,
            // Constructable.cs:747):
            //   IsValidBuildLocation(base.gameObject, pos, orientation, IsReplacementTile)
            // For a wall-over-door replacement plan that re-check runs with the plan's
            // own GameObject as source_go and replace_tile = true, and fails natively
            // in the NotInTiles branch's hard block
            //   flag = (replace_tile || gameObject2 == null || gameObject2 == source_go)
            //         && !Grid.HasDoor[cell];   (BuildingDef.cs:1285-1307)
            // because the live door is still at the anchor cell — cancelling the plan
            // ("Место не подходит для стройки") with nothing built. The postfix re-runs
            // the shared gate IsReplacementPlacementPossible, whose occupancy check is
            // now source_go-aware (ReplacementLayerOccupiedExcludingSelf), so the plan's
            // own presence in the ReplacementBackwall slot no longer blocks the re-check.
            // FindMethod arity (5 parameters: GameObject, int, Orientation, bool, string)
            // matches only this overload (the Vector3 overloads are 4-arg and
            // 5-arg-with-Vector3).
            MethodInfo validBuild = FindMethod(typeof(BuildingDef), "IsValidBuildLocation",
                typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string));
            if (validBuild == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string) не найдена — construction-recheck postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string) — construction-recheck postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу construction-recheck postfix на {0}".F(validBuild));
#endif
                harmony.Patch(validBuild, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidBuildLocation_DoorReplacement__Patch), nameof(BuildingDef_IsValidBuildLocation_DoorReplacement__Patch.Postfix)));
            }
            // 7. Crash fix: Constructable.FinishConstruction(UtilityConnections, WorkerBase)
            // — private instance method, resolved by the same FindMethod scan.
            MethodInfo finishConstruction = FindMethod(typeof(Constructable), "FinishConstruction", typeof(UtilityConnections), typeof(WorkerBase));
            if (finishConstruction == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Constructable.FinishConstruction(UtilityConnections, WorkerBase) не найдена — duplicate-candidate prefix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve Constructable.FinishConstruction(UtilityConnections, WorkerBase) — duplicate-candidate prefix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу duplicate-candidate prefix на {0}".F(finishConstruction));
#endif
                harmony.Patch(finishConstruction, prefix: new HarmonyMethod(typeof(Constructable_FinishConstruction_DoorReplacement__Patch), nameof(Constructable_FinishConstruction_DoorReplacement__Patch.Prefix)));
            }
            // 8. Spec feature 2: BuildingDef.TryReplaceTile(GameObject, Vector3,
            // Orientation, IList<Tag>, int) — the WORKING overload (BuildingDef.cs:487):
            // the facadeID overload (BuildingDef.cs:508) delegates to it
            // (BuildingDef.cs:510), so patching this one bails every replacement-plan
            // creation path (the survival-drag fallback, BuildTool.cs:376, and the
            // mod's own BuildTool.TryBuild postfix facadeID call). Same-PrefabID
            // door-over-door must be skipped entirely: the native TryBuild element
            // gate (BuildTool.cs:371) only skips same-def+same-element, and
            // same-def+different-element (an iron-element Door over a gold-element
            // Door, one PrefabID "Door") would otherwise create a plan here. Bailing
            // leaves gameObject == null in the caller, so no plan is stored at
            // Grid.Objects[cell, ReplacementLayer] and PostProcessBuild(null) is a
            // no-op. FindMethod arity (5 parameters) matches only this overload — the
            // facadeID overload has 6.
            MethodInfo tryReplaceTile = FindMethod(typeof(BuildingDef), "TryReplaceTile",
                typeof(GameObject), typeof(Vector3), typeof(Orientation), typeof(IList<Tag>), typeof(int));
            if (tryReplaceTile == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int) не найдена — same-PrefabID door-over-door prefix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int) — same-PrefabID door-over-door prefix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу same-PrefabID door-over-door prefix на {0}".F(tryReplaceTile));
#endif
                harmony.Patch(tryReplaceTile, prefix: new HarmonyMethod(typeof(BuildingDef_TryReplaceTile_SameDoorPrefab__Patch), nameof(BuildingDef_TryReplaceTile_SameDoorPrefab__Patch.Prefix)));
            }
        }

        /// <summary>
        /// Resolves a method by name + per-parameter underlying type, normalizing
        /// byref parameters: an `out T` parameter is reported by the runtime as
        /// T& (IsByRef), so both sides are reduced to the underlying type before
        /// comparing (ParameterType.GetElementType() on T& yields T). First full
        /// match wins, null if none — callers log a skip rather than crash.
        /// BindingFlags.Static was added (Stage 2.2) so the declared-only scan also
        /// finds the static Assets.AddBuildingDef target; instance lookups are
        /// unaffected — no C# signature is both static and instance.
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

        // Stage 3: shared LIVE replacement-tag list, ASSIGNED (the same instance) to
        // every door def's and every wall-like def's ReplacementTags. Defs register in
        // arbitrary order (a wall may register before the doors), so the tags cannot be
        // a per-def snapshot: each newly registered buildable door appends its
        // per-PrefabID tag (Def.cs:10, set before Assets.AddBuildingDef) to this one
        // live list (deduped — a def might be re-processed), and the replacement
        // machinery reads ReplacementTags live at placement time (CanReplace,
        // BuildingDef.cs:278), so tags of later-registered doors are already visible to
        // defs registered earlier. POI doors (ShowInBuildMenu = false, BuildingDef.cs:149)
        // are never added — they must not become replacement candidates.
        private static readonly List<Tag> SharedReplacementTags = new List<Tag>()
        {
            GameTags.FloorTiles,
            GameTags.Backwall,
            GameTags.Ladders
        };

        // Stage 3: shared replacement-candidate layer set, assigned to every door and
        // wall-like def. Building is added over the original { FoundationTile, Backwall }
        // pair because placed doors live on the Building layer (enables door-over-door
        // later); its presence also doubles as the wall-branch idempotency marker.
        private static readonly List<ObjectLayer> SharedReplacementCandidateLayers = new List<ObjectLayer>()
        {
            ObjectLayer.FoundationTile,
            ObjectLayer.Backwall,
            ObjectLayer.Building
        };

        // 0. Stage 2.2: teach EVERY door BuildingDef the replacement metadata (the old
        // DoorConfig.CreateBuildingDef patch did this for the pneumatic door only). Hooks
        // Assets.AddBuildingDef (Assets.cs:670, public static; sole caller
        // BuildingConfigManager.RegisterBuilding, BuildingConfigManager.cs:114), which fires
        // exactly once per def at registration — for every vanilla, DLC and mod IBuildingConfig
        // (LegacyModMain.cs:26-46 -> GeneratedBuildings.cs:24) — and AFTER the complete config
        // chain (DoPostConfigureComplete, BuildingConfigManager.cs:104), so the Door component
        // and CopyBuildingSettings.copyGroupTag are in final state when IsDoorDef runs. Future
        // DLC/mod doors registered through the same path get the metadata automatically.
        // The runtime replacement machinery reads the def fields live (CanReplace,
        // BuildingDef.cs:278; GetReplacementCandidate, :324; IsReplacementLayerOccupied, :305 —
        // no caches of the replacement fields) and every placed object references this same def
        // instance (BuildingLoader.cs:196+), so fields set here are what BuildTool/Constructable
        // consult at build time. NO [HarmonyPatch] attributes: attached programmatically in OnLoad.
        public static class Assets_AddBuildingDef_DoorReplacement__Patch
        {
            public static void Postfix(BuildingDef __0)
            {
                if (__0 == null)
                {
                    return;
                }
                // A) Door defs (Stage 2.2, extended by Stage 3).
                if (IsDoorDef(__0))
                {
                    // Idempotent: never override a def that already carries its own replacement
                    // metadata (no native door does today).
                    if (__0.ReplacementLayer != ObjectLayer.NumLayers)
                    {
                        return;
                    }
                    // Stage 3: a buildable door joins the shared live tag list BEFORE the
                    // assignment below, so its per-PrefabID tag is already inside every
                    // def's ReplacementTags at placement time. Deduped — a def might be
                    // re-processed. POI doors (ShowInBuildMenu = false) are never added.
                    if (__0.ShowInBuildMenu)
                    {
                        Tag doorTag = __0.Tag;
                        if (!SharedReplacementTags.Contains(doorTag))
                        {
                            SharedReplacementTags.Add(doorTag);
                        }
                    }
                    __0.ReplacementLayer = ObjectLayer.ReplacementTile;
                    __0.ReplacementCandidateLayers = SharedReplacementCandidateLayers;
                    __0.ReplacementTags = SharedReplacementTags;
                    // Stage 3: WoodenDoor/InsulatedDoor set Replaceable = false in their
                    // configs; the replacement machinery consults the candidate's
                    // Replaceable (BuildTool.cs:362-364), so re-enable it for every door.
                    __0.Replaceable = true;
                }
                // B) Wall-like defs (Stage 3): the reverse direction — walls placed OVER
                // existing doors.
                if (IsWallDef(__0))
                {
                    // Idempotency guard (Stage 3): skip a def whose candidate layers
                    // already carry the mod-added Building layer (native or other-mod
                    // metadata is never overridden).
                    if (__0.ReplacementCandidateLayers != null &&
                        __0.ReplacementCandidateLayers.Contains(ObjectLayer.Building))
                    {
                        return;
                    }
                    // The wall's own tags { FloorTiles, Backwall } are a subset of the
                    // shared list's initial values, so the reassignment loses nothing.
                    __0.ReplacementCandidateLayers = SharedReplacementCandidateLayers;
                    __0.ReplacementTags = SharedReplacementTags;
                    // Defs without a native replacement layer adopt the wall-layer one
                    // (matches ExteriorWall & co., ExteriorWallConfig.cs:27-28).
                    if (__0.ReplacementLayer == ObjectLayer.NumLayers)
                    {
                        __0.ReplacementLayer = ObjectLayer.ReplacementBackwall;
                    }
                }
                // C) Foundation tile defs (Stage 4): the second reverse direction —
                // foundation tiles placed OVER existing doors.
                //
                // WHY ADD-IN-PLACE RATHER THAN A WHOLESALE ASSIGN: the native candidate
                // layers (CreateFoundationTileDef, BuildingTemplates.cs:51-56) are
                // { FoundationTile, LadderTile, Backwall } — the LadderTile entry is what
                // keeps the vanilla "tile over ladder" replacement working
                // (GetReplacementCandidate, BuildingDef.cs:324-341), and assigning
                // SharedReplacementCandidateLayers would drop it and break that path.
                // Only ObjectLayer.Building is added, which is the ONLY missing piece for
                // this direction: with it, the native fallback's GetReplacementCandidate
                // (BuildTool.cs:352) finds a placed door (doors live on the Building
                // layer). NO validity flips are needed here: the 6-arg
                // IsValidPlaceLocation(replace_tile:true) that TryReplaceTile runs
                // (BuildingDef.cs:490) passes natively (IsAreaClear's own-layer occupancy
                // skip when the occupant IS the candidate, BuildingDef.cs:577-608; no
                // HasDoor block for the Tile rule) and the construction-start
                // IsValidBuildLocation core check passes natively as well.
                // ReplacementLayer (natively ReplacementTile) and Replaceable are
                // deliberately left untouched.
                if (IsTileDef(__0))
                {
                    // Idempotency marker: the mod-added Building candidate layer, same as
                    // the wall branch — the tile branch adds exactly this and nothing else.
                    if (__0.ReplacementCandidateLayers != null &&
                        __0.ReplacementCandidateLayers.Contains(ObjectLayer.Building))
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] AddBuildingDef: деф={0} — ReplacementCandidateLayers уже содержит Building, пропуск (плитка)".F(__0.PrefabID));
#endif
                        return;
                    }
                    // CreateFoundationTileDef (BuildingTemplates.cs:51-56) always sets the
                    // native list; the null case is a guard for exotic defs only.
                    if (__0.ReplacementCandidateLayers == null)
                    {
                        __0.ReplacementCandidateLayers = new List<ObjectLayer>()
                        {
                            ObjectLayer.FoundationTile,
                            ObjectLayer.LadderTile,
                            ObjectLayer.Backwall,
                            ObjectLayer.Building
                        };
                    }
                    else
                    {
                        // In-place add: keeps the native LadderTile entry, so the vanilla
                        // tile-over-ladder replacement stays native and untouched.
                        __0.ReplacementCandidateLayers.Add(ObjectLayer.Building);
                    }
                    // The native tile tags { FloorTiles, Ladders, Backwall }
                    // (BuildingTemplates.cs:57-62) are a subset of the shared live list's
                    // contents, so the reassignment loses nothing and gains every door's
                    // per-PrefabID tag (CanReplace, BuildingDef.cs:278-285). Identity
                    // check: a def already carrying the shared list is not reassigned.
                    if (__0.ReplacementTags != SharedReplacementTags)
                    {
                        __0.ReplacementTags = SharedReplacementTags;
                    }
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] AddBuildingDef: деф={0} — плитка: добавлен ObjectLayer.Building в ReplacementCandidateLayers, ReplacementTags = shared list".F(__0.PrefabID));
#endif
                }
            }
        }

        /// <summary>
        /// Stage 2 (hover warning + preview tint): the plain, non-replacement validity
        /// check reports "unoccupied space" (wall) / "back wall" (drywall) while the
        /// door hovers over a replacement candidate, so the hover card prints a false
        /// warning (BuildToolHoverTextCard.cs:50) and the preview tint stays red
        /// (BuildTool.cs:177) — although the drag succeeds via the game's own
        /// replacement fallback. Both postfixes below re-run the survival-drag gate
        /// (BuildTool.cs:350-385 plus TryReplaceTile's check at BuildingDef.cs:490) and
        /// hide the false failure ONLY on cosmetic paths: the 4-arg
        /// IsValidPlaceLocation overload (BuildingDef.cs:1098) that feeds the hover
        /// card and the UpdateVis tint, and the IsValidReplaceLocation tint input.
        /// The replace_tile:false half of the 6-arg canonical overload the drag routing
        /// depends on is NEVER flipped (Stage 3 patches the 6-arg method itself, but
        /// only the replace_tile:true wall-over-door path — see the 6-arg postfix
        /// below): TryPlace (BuildingDef.cs:467, called from BuildTool.cs:323) must
        /// keep failing so TryBuild still takes the replacement fallback
        /// (BuildTool.cs:350), never plain def.Build.
        /// </summary>

        /// <summary>
        /// Stage 2.2: generic door-def test. The game has no BuildCategories type and no shared
        /// door tag — "Doors" menu membership is hardcoded static lists (BuildMenu.cs:151-159,
        /// TUNING/BUILDINGS.cs:687-709). The two universal signals instead: every native door adds
        /// the Door component (Door.cs:7, in ConfigureBuildingTemplate or DoPostConfigureComplete),
        /// and every buildable door sets CopyBuildingSettings.copyGroupTag = GameTags.Door
        /// (DoorConfig.cs:38, PressureDoorConfig.cs:41, WoodenDoorConfig.cs:60,
        /// ManualPressureDoorConfig.cs:36, InsulatedDoorConfig.cs:62) — which mod doors follow too
        /// (peterhaneve AirlockDoor: AirlockDoorConfig.cs:123). Composite = covers all native doors
        /// (incl. BunkerDoor/GravitasDoor/POI doors) and future DLC/mod doors using either signal.
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
        /// Stage 3: generic wall-def test. A wall-like building is one that sits on the
        /// backwall layer and is built into empty space: def.ObjectLayer ==
        /// ObjectLayer.Backwall && def.BuildLocationRule == BuildLocationRule.NotInTiles.
        /// In the installed game build this matches ExteriorWall, GlassExteriorWall
        /// (DLC5) and ThermalBlock — the buildings this mod lets be placed OVER existing
        /// doors (the reverse direction of the existing door-over-wall feature; this is
        /// the scope gate for that direction). FacilityBackWallWindow (POI-only) also
        /// matches but is harmless: it is not buildable from the menu.
        /// </summary>
        private static bool IsWallDef(BuildingDef def)
        {
            return def.ObjectLayer == ObjectLayer.Backwall &&
                def.BuildLocationRule == BuildLocationRule.NotInTiles;
        }

        /// <summary>
        /// Stage 4: generic foundation-tile-def test. A foundation tile is built by
        /// the Tile rule and lives on the foundation tile layer:
        /// def.BuildLocationRule == BuildLocationRule.Tile && def.TileLayer ==
        /// ObjectLayer.FoundationTile. CreateFoundationTileDef
        /// (BuildingTemplates.cs:46-64) is what assigns TileLayer = FoundationTile, and
        /// no other def pair combines both signals: ladders use TileLayer = LadderTile
        /// (BuildingTemplates.cs:68) and doors use BuildLocationRule.Tile with
        /// TileLayer = NumLayers — so the test selects exactly the foundation tiles
        /// (Tile, MetalTile, InsulationTile plus every other CreateFoundationTileDef
        /// def, e.g. WoodTile/BunkerTile/RocketWallTile — the helper covers all of
        /// them, no per-PrefabID allow-list needed). This is the scope gate for the
        /// tile-over-door direction (Stage 4).
        /// </summary>
        private static bool IsTileDef(BuildingDef def)
        {
            return def != null && def.BuildLocationRule == BuildLocationRule.Tile && def.TileLayer == ObjectLayer.FoundationTile;
        }

        /// <summary>
        /// Spec feature 2: the same-PrefabID door-over-door test, shared by the
        /// replacement gate (IsReplacementPlacementPossible) and the
        /// BuildingDef.TryReplaceTile prefix. TRUE only when BOTH the dragged def and
        /// the found candidate are doors (IsDoorDef) with equal PrefabID — e.g. an
        /// iron-element Door over a gold-element Door: one PrefabID "Door" def, two
        /// element variants of it. Different-PrefabID door-over-door (Door over
        /// PressureDoor) and every non-door direction return false, so those paths
        /// are untouched. The candidate's BuildingComplete null check mirrors the
        /// native fallback's (BuildTool.cs:362-363) and is defense only —
        /// GetReplacementCandidate already requires it (BuildingDef.cs:324).
        /// </summary>
        private static bool IsSameDoorPrefab(BuildingDef def, GameObject candidateGo)
        {
            if (!IsDoorDef(def) || candidateGo == null)
            {
                return false;
            }
            BuildingComplete complete = candidateGo.GetComponent<BuildingComplete>();
            return complete != null && IsDoorDef(complete.Def) && complete.Def.PrefabID == def.PrefabID;
        }

        /// <summary>
        /// source_go-aware replacement-layer occupancy: mirrors the native
        /// BuildingDef.IsReplacementLayerOccupied (BuildingDef.cs:305-322 — the
        /// ReplacementLayer slot plus every EquivalentReplacementLayers slot, with
        /// no exclusion) but ignores an occupant that IS source_go itself. Needed
        /// for the construction-start re-check (Constructable.PlaceDiggables,
        /// Constructable.cs:747): the replacement plan's own GameObject sits in the
        /// ReplacementLayer slot from creation (Constructable OnEnable,
        /// Constructable.cs:375-391; MarkArea, Constructable.cs:441-461), so the
        /// native check reports the slot as occupied BY THE PLAN ITSELF and the
        /// re-check can never pass. For every other caller source_go is the drag
        /// visualizer (never in the slot) or null, so behavior there is unchanged.
        /// </summary>
        private static bool ReplacementLayerOccupiedExcludingSelf(BuildingDef def, int cell, GameObject self)
        {
            GameObject occupant = Grid.Objects[cell, (int)def.ReplacementLayer];
            if (occupant != null && occupant != self)
            {
                return true;
            }
            if (def.EquivalentReplacementLayers != null)
            {
                foreach (ObjectLayer layer in def.EquivalentReplacementLayers)
                {
                    GameObject other = Grid.Objects[cell, (int)layer];
                    if (other != null && other != self)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Mirrors the survival drag gate in BuildTool.TryBuild (BuildTool.cs:350-385):
        //  - a replacement candidate exists at the anchor cell (BuildTool.cs:352;
        //    GetReplacementCandidate, BuildingDef.cs:324)
        //  - the replacement layer is unoccupied in every door cell (BuildTool.cs:354-360;
        //    native IsReplacementLayerOccupied, BuildingDef.cs:305 — here replaced by
        //    the source_go-aware ReplacementLayerOccupiedExcludingSelf so the
        //    construction-start re-check, where source_go IS the plan that occupies
        //    the slot, is not blocked by its own presence)
        //  - the candidate is a Replaceable, tag-matching occupant (BuildTool.cs:362-364;
        //    CanReplace, BuildingDef.cs:278)
        //  - TryReplaceTile's own validity check passes (BuildingDef.cs:490)
        // Stage 3: the gate also covers the REVERSE direction (a wall-like def placed over an
        // existing door); Stage 4 extends it to the second reverse direction (a foundation
        // tile def placed over an existing door). Everything from the ReplacementLayer guard
        // down is shared by all directions; only the scope gate and the extra "the found
        // candidate itself is a door" check (inside TryFindReplacementCandidate, reverse
        // direction = IsWallDef || IsTileDef) differ.
        private static bool IsReplacementPlacementPossible(BuildingDef def, GameObject source_go, int cell, Orientation orientation)
        {
            return TryReplacementGate(def, source_go, cell, orientation, out _);
        }

        // E-1 (Stage 2.5, Bug E): the gate now reports WHY it failed, so the
        // hover/tint/recheck paths can log the failing step instead of a bare
        // "false". Logic is unchanged from the pre-E-1 gate — every return point
        // got a reason string, the order and the conditions are identical.
        private static bool TryReplacementGate(BuildingDef def, GameObject source_go, int cell, Orientation orientation, out string reason)
        {
            // Scope gate (Stage 2.2, extended by Stage 3, extended by Stage 4): a door def,
            // a wall-like def OR a foundation tile def.
            // Vanilla defs (windows, moulding tiles, building templates) also set
            // ReplacementLayer/CandidateLayers — the fix must not change their behavior:
            // the only IsTileDef match outside buildable foundation tiles is the
            // Deprecated MouldingTile (unplaceable — IsAvailable is false,
            // BuildingDef.cs:287-298), so admitting tile defs changes no vanilla drag.
            if (!IsDoorDef(def) && !IsWallDef(def) && !IsTileDef(def))
            {
                reason = "scope: def is not a door/wall/tile";
                return false;
            }
            if (def.ReplacementLayer == ObjectLayer.NumLayers || def.ReplacementCandidateLayers == null)
            {
                reason = "ReplacementLayer=" + def.ReplacementLayer + " (NumLayers) or ReplacementCandidateLayers==null";
                return false;
            }
            // Stage 3: reverse direction (wall over door). The shared tag list makes placed
            // doors valid candidates for wall defs; the area-aware candidate search, the
            // per-cell Replaceable/CanReplace gates and the "the found candidate itself
            // is a door" check are extracted into TryFindReplacementCandidate (Stage 3
            // refactor, logic unchanged) and called here.
            GameObject candidate;
            if (!TryFindReplacementCandidate(def, cell, orientation, out candidate, out _))
            {
                reason = "no replacement candidate in the door area";
                return false;
            }
            // Same-PrefabID door-over-door is not a replacement (spec feature 2): keep
            // the vanilla red "occupied" preview — no flip, no plan. The native
            // TryBuild element gate (BuildTool.cs:371) only skips same-def+same-element;
            // same-def+different-element (an iron-element Door over a gold-element
            // Door, one PrefabID "Door") must be skipped entirely.
            if (IsSameDoorPrefab(def, candidate))
            {
                reason = "candidate is the same door PrefabID (" + def.PrefabID + ")";
                return false;
            }
            bool occupied = false;
            def.RunOnArea(cell, orientation, (c) =>
            {
                // source_go-aware: during the construction-start re-check source_go
                // is the replacement plan itself, which owns the ReplacementLayer
                // slot (Constructable.cs:375-391) — the native check would report
                // it as occupied and cancel the build.
                if (ReplacementLayerOccupiedExcludingSelf(def, c, source_go))
                {
                    occupied = true;
                }
            });
            if (occupied)
            {
                reason = "ReplacementLayer occupied in the door area";
                return false;
            }
            string fail_reason;
            bool result = def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason, restrictToActiveWorld: false);
            reason = result ? null : "native IsValidPlaceLocation(replace_tile:true) failed: " + fail_reason;
            return result;
        }

        // E-1 (Stage 2.5, Bug E): the hover/tint/recheck gate verdict logger.
        // The hover paths run EVERY FRAME while the build tool is held, so the
        // verdict is logged only when it changes (static key) — otherwise the
        // log would spam per frame. The key includes the context (hover/tint/
        // recheck), def, cell, orientation, verdict and reason: static hover on
        // one cell logs once; moving between cells logs once per new state.
        private static string s_lastGateLogKey = null;

        private static void LogGateVerdict(string context, BuildingDef def, int cell, Orientation orientation, bool pass, string reason)
        {
#if DEBUG
            string key = context + "|" + (def == null ? "(null)" : def.PrefabID) + "|" + cell + "|" + orientation + "|" + (pass ? "pass" : "fail") + "|" + (reason ?? "");
            if (key == s_lastGateLogKey)
            {
                return;
            }
            s_lastGateLogKey = key;
            PUtil.LogDebug("[BuildDoorOverWall] gate {0}: def={1} cell={2} orient={3} => {4} {5}".F(context, def == null ? "(null)" : def.PrefabID, cell, orientation, pass ? "PASS" : "FAIL", reason ?? ""));
#endif
        }

        /// <summary>
        /// Stage 3 refactor (extracted from IsReplacementPlacementPossible, logic
        /// unchanged): the area-aware replacement-candidate search plus its gates. The
        /// door is 1x2 and the candidate (wall / door) may sit in EITHER door cell, so
        /// GetReplacementCandidate (single-cell, BuildingDef.cs:324) is run over the
        /// whole area; the anchor-only lookup is why a door with its upper cell on the
        /// wall (anchor = lower cell, GenerateOffsets BuildingDef.cs:1795) showed a red
        /// ghost and could not be placed (Stage 2.1). Iterate the whole area with the
        /// same per-cell gate the native fallback uses; anchor-first order is preserved
        /// (RunOnArea visits offset (0,0) first). A candidate is accepted only when its
        /// BuildingComplete is Replaceable (BuildTool.cs:362-364) and tag-matches
        /// (CanReplace, BuildingDef.cs:278). In the reverse direction — a wall-like or
        /// foundation-tile def (Stage 4); IsWallDef/IsTileDef are pure def tests, so
        /// computing them here instead of before the search is byte-identical for the
        /// gate, whose scope gate admits only door, wall and foundation-tile defs — in
        /// the reverse direction the found candidate must itself be a door. Returns true
        /// (with the
        /// winner in the out args) only when a candidate was found and passed every gate.
        /// </summary>
        private static bool TryFindReplacementCandidate(BuildingDef def, int cell, Orientation orientation, out GameObject candidate, out BuildingComplete candidateComplete)
        {
            // Locals, not the out args: C# forbids capturing out parameters inside
            // a lambda (CS1628); the out args are assigned from the locals at the end.
            GameObject found = null;
            BuildingComplete foundComplete = null;
            def.RunOnArea(cell, orientation, (c) =>
            {
                if (found != null)
                {
                    return;
                }
                GameObject local = def.GetReplacementCandidate(c);
                if (local == null)
                {
                    return;
                }
                BuildingComplete complete = local.GetComponent<BuildingComplete>();
                if (complete == null || !complete.Def.Replaceable)
                {
                    return;
                }
                if (!def.CanReplace(local))
                {
                    return;
                }
                found = local;
                foundComplete = complete;
            });
            candidate = found;
            candidateComplete = foundComplete;
            if (candidate == null)
            {
                return false;
            }
            // Stage 3 (reverse direction, extended by Stage 4): for NON-door wall/tile
            // defs the found candidate must itself be a door — the shared tag gate
            // already excludes other buildings, but this makes the "wall over door" /
            // "tile over door" intent explicit and is the hook the 6-arg
            // IsValidPlaceLocation HasDoor bypass postfix builds on. For foundation
            // tile defs this keeps the gate away from the vanilla
            // tile-over-ladder/backwall/foundation replacement (those candidate types
            // are never accepted here); the native fallback handles them with no mod flip.
            // E-3 (Stage 2.5, Bug E): the check is SKIPPED for door defs. The four
            // airlock doors are both door defs AND IsTileDef-true (BuildLocationRule.Tile
            // + TileLayer=FoundationTile — ManualPressureDoorConfig.cs:10), so without
            // this exclusion they were rejected at THIS step for tile/backwall
            // candidates (Player.log: "no replacement candidate in the door area") even
            // though those drags are valid vanilla foundation replacements: the gate's
            // native 6-arg call with replace_tile:true passes for them (IsAreaClear
            // :608 skips the tile-layer block when a candidate is present, and
            // IsValidTileLocation :810 bypasses the backwall block when
            // replacement_tile==true).
            if (!IsDoorDef(def) && (IsWallDef(def) || IsTileDef(def)) && (candidateComplete == null || !IsDoorDef(candidateComplete.Def)))
            {
                return false;
            }
            return true;
        }

        // 1. Suppress the false warning text (and stop the visualizer from painting
        // the hovered cell invalid).
        // BuildToolHoverTextCard.UpdateHoverElements (BuildToolHoverTextCard.cs:49-55)
        // prints `fail_reason` whenever this check fails, and BuildTool.UpdateVis
        // (BuildTool.cs:177) ORs this method's result with IsValidReplaceLocation to
        // pick red vs white preview tint. The 4-arg overload it targets
        // (BuildingDef.cs:1098) feeds ONLY those two cosmetic paths — the survival
        // drag flow (BuildTool.TryBuild -> def.TryPlace, BuildingDef.cs:467) uses the
        // 6-arg overload and is never touched — so forcing the bool result true here
        // (and only when the replacement drag would actually succeed) makes the
        // caller never draw the false fail text / red tint without changing any drag
        // routing decision.
        //
        // WHY THIS SHAPE (round-2 crash fix): the game's 0Harmony has NO postfix
        // convention for out parameters. In EmitCallParameter
        // (0Harmony.decompiled.cs ~4444) any parameter name starting with `__` that
        // is not one of the special names (__instance, __originalMethod, __args,
        // __result, __resultRef, __state, __exception, __runOriginal) is parsed as a
        // POSITIONAL index via int.TryParse, so a byref out-reason parameter with a
        // `__`-prefixed name died with "does not contain a valid index" before the
        // method was even patched; and the both-byref emission branch emits a value
        // load (Ldarg), so an out parameter cannot be written back from a postfix at
        // all in this Harmony version. The postfix therefore does not touch the out
        // reason — it just flips `__result` to true so the caller (hover card) never
        // draws the text. Positional names __0/__1/__2 (original parameter indices
        // 0=source_go, 1=pos, 2=orientation) are bulletproof in this Harmony build.
        // NO [HarmonyPatch] attributes on purpose: the target has an `out string`
        // parameter and attribute targets cannot express byref parameter types
        // (a byref Type is not a legal attribute constant, CS0182; and plain
        // typeof(string) fails Type.GetMethod resolution — see OnLoad). This
        // class is attached programmatically in OnLoad via harmony.Patch(...).
        public static class BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject __0, Vector3 __1, Orientation __2, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                // E-1 (Stage 2.5, Bug E): log the verdict + failing step (dedup'd,
                // DEBUG only) — this is the hover path the user sees as red/green.
                int cell = Grid.PosToCell(__1);
                string reason;
                bool pass = TryReplacementGate(__instance, __0, cell, __2, out reason);
                LogGateVerdict("hover", __instance, cell, __2, pass, reason);
                __result = pass;
            }
        }

        // 2. Fix the preview tint.
        // BuildTool.UpdateVis (BuildTool.cs:178) ORs this method's result into the
        // plain check to decide red vs white. The game's check
        // (IsValidReplaceLocation, BuildingDef.cs:1184-1207) demands a Building-layer
        // occupant on every placement cell, which is false for drywall (Backwall-layer
        // only) and for a door anchored on a 1-high wall — so the preview stayed red
        // even though the drag queues the replacement plan. The only caller in the game
        // build is BuildTool.cs:178, so returning true when the survival drag would
        // actually succeed is safe and stays inside the preview.
        // NO [HarmonyPatch] attributes on purpose (same reason as the
        // IsValidPlaceLocation patch above): attached programmatically in OnLoad
        // via harmony.Patch(...).
        public static class BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch
        {
            public static void Postfix(BuildingDef __instance, Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer, ref bool __result)
            {
                if (__result || !IsDoorDef(__instance))
                {
                    return;
                }
                // E-1 (Stage 2.5, Bug E): tint verdict, same dedup'd logging.
                int cell = Grid.PosToCell(pos);
                string reason;
                bool pass = TryReplacementGate(__instance, null, cell, orientation, out reason);
                LogGateVerdict("tint", __instance, cell, orientation, pass, reason);
                __result = pass;
            }
        }

        // 3. Survival drag: a 1x2 door whose replacement candidate sits in the NON-anchor cell
        // (upper cell on the wall, lower/anchor cell in the air) could never be placed: the
        // replacement fallback in BuildTool.TryBuild (BuildTool.cs:350-385) looks for a candidate
        // only in the anchor cell (BuildTool.cs:352; GetReplacementCandidate is single-cell,
        // BuildingDef.cs:324). Every vanilla replacement def is 1x1, so that anchor-only gate was
        // never exercised by a multi-cell def; the door (1x2) is the first. This postfix replicates
        // the native fallback for exactly that case: it fires only when TryBuild produced no
        // replacement plan at the anchor, and creates the plan with the same calls the native tail
        // uses (BuildTool.cs:377-379). Completion then takes the game's designed multi-cell path:
        // Constructable finds no candidate at the anchor, so FinishConstruction's RunOnArea
        // (Constructable.cs:~225-256) destroys the candidate in each non-anchor cell (returns its
        // items to the worker, Trigger(1606648047), DeleteObject).
        // Deliberately NOT patching BuildingDef.GetReplacementCandidate to be area-aware: that would
        // make Constructable's anchor branch (Constructable.cs:161) also find the upper-cell
        // candidate and could double-handle it (deferred DestroySelf still in the Grid when
        // FinishConstruction's RunOnArea re-looks at the cell).
        // NO [HarmonyPatch] attributes on purpose: TryBuild is private — attached programmatically
        // in OnLoad via harmony.Patch(...) like the other two BuildingDef postfixes.
        // Патчинг для случая когда дверь ставим верхним концом в стену, а нижним - в воздухе
        public static class BuildTool_TryBuild_DoorReplacement__Patch
        {
            public static void Postfix(BuildTool __instance, int __0)
            {
                BuildingDef def = __instance.def;
                if (def == null || !IsDoorDef(def))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: пропуск, def={0} (null или не дверь)".F(def == null ? "(null)" : def.PrefabID));
#endif
                    return;
                }
                if (def.ReplacementLayer == ObjectLayer.NumLayers)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — ReplacementLayer={2}, не замена".F(def.PrefabID, __0, def.ReplacementLayer));
#endif
                    return;
                }
                // Mirror TryBuild's early-return guard (BuildTool.cs:309): if the native method
                // bailed out there was no build attempt to complete.
                GameObject visualizer = __instance.visualizer;
                if (visualizer == null)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — visualizer == null".F(def.PrefabID, __0));
#endif
                    return;
                }
                if (Grid.PosToCell(visualizer) != __0 && (def.BuildingComplete.GetComponent<LogicPorts>() != null || def.BuildingComplete.GetComponent<LogicGateBase>() != null))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} — visualizer в клетке {1}, build-клетка {2} (logic-здание) — пропуск".F(def.PrefabID, Grid.PosToCell(visualizer), __0));
#endif
                    return;
                }
                // A replacement plan already at the anchor (created natively or by a previous drag
                // event) means the placement already happened.
                if (Grid.Objects[__0, (int)def.ReplacementLayer] != null)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — replacement-plan уже есть ({2})".F(def.PrefabID, __0, Grid.Objects[__0, (int)def.ReplacementLayer].name));
#endif
                    return;
                }
                // Instant-build mode: the native fallback takes InstantBuildReplace (Stage 3,
                // deferred) — do not interfere.
                if (DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — instant-build (DebugHandler={2}, Sandbox={3}) — без вмешательства".F(def.PrefabID, __0, DebugHandler.InstantBuildMode, Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild));
#endif
                    return;
                }
                IList<Tag> selected = __instance.selectedElements;
                if (selected == null || selected.Count == 0)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — selectedElements пуст (count={2})".F(def.PrefabID, __0, selected == null ? -1 : selected.Count));
#endif
                    return;
                }
                // Area-aware candidate with the native gate (BuildTool.cs:352-364).
                GameObject candidate = null;
                int candidateCell = -1;
                def.RunOnArea(__0, __instance.buildingOrientation, (c) =>
                {
                    if (candidate != null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — кандидат уже найден ({1})".F(c, candidate.name));
#endif
                        return;
                    }
                    GameObject local = def.GetReplacementCandidate(c);
                    if (local == null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — кандидата нет".F(c));
#endif
                        return;
                    }
                    BuildingComplete complete = local.GetComponent<BuildingComplete>();
                    if (complete == null || !complete.Def.Replaceable)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — кандидат {1}, Replaceable={2}".F(c, local.name, complete == null ? "(null)" : complete.Def.Replaceable.ToString()));
#endif
                        return;
                    }
                    if (!def.CanReplace(local))
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — CanReplace({1}) = false".F(c, local.name));
#endif
                        return;
                    }
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — найден кандидат {1}".F(c, local.name));
#endif
                    candidate = local;
                    candidateCell = c;
                });
                if (candidate == null)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — кандидат не найден в области двери".F(def.PrefabID, __0));
#endif
                    return;
                }
                // The replacement layer must be unoccupied in every door cell (BuildTool.cs:354-360).
                bool occupied = false;
                def.RunOnArea(__0, __instance.buildingOrientation, (c) =>
                {
                    if (def.IsReplacementLayerOccupied(c))
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: клетка {0} — replacement-layer занят".F(c));
#endif
                        occupied = true;
                    }
                });
                if (occupied)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — replacement-layer занят в области двери".F(def.PrefabID, __0));
#endif
                    return;
                }
                // Native element gate (BuildTool.cs:366-371): proceed only when the candidate def
                // differs from ours or the selected element differs from the candidate's element
                // (the 1542131326 hash is the native snow-tag quirk).
                Tag tag = candidate.GetComponent<PrimaryElement>().Element.tag;
                if (tag.GetHash() == 1542131326)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: элемент {0} — snow-хэш {1}, заменяю на SimHashes.Snow".F(tag, tag.GetHash()));
#endif
                    tag = SimHashes.Snow.CreateTag();
                }
                if (candidate.GetComponent<BuildingComplete>().Def == def && selected[0] == tag)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} — кандидат {1} того же дефа и элемент {2} уже выбран — пропуск".F(def.PrefabID, candidate.name, tag));
#endif
                    return;
                }
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] def.TileLayer={0} def.ReplacementLayer={1} ObjectLayer.NumLayers={1}".F(def.TileLayer, def.ReplacementLayer, ObjectLayer.NumLayers));
#endif
                // Create the plan exactly like the native fallback tail (BuildTool.cs:377-379).
                Orientation orientation = __instance.buildingOrientation;
                int anchorCell = __0;
                // E-2 (Stage 2.5, Bug D): the Stage-1.5 mirror originally fired for every
                // def with TileLayer != NumLayers — in this door-only postfix that was
                // exactly the four foundation-tile doors (ManualPressure/Pressure/
                // Insulated/Bunker, TileLayer = FoundationTile). Mirroring the plan's
                // anchor + orientation for a DOOR drag was never intended: the user's
                // "the new door came out rotated" in Bug D was this flip. The mirror now
                // stays only for wall/tile defs (IsWallDef || IsTileDef — never true in
                // this door-only postfix, so the block is inert here; the D fix in the
                // FinishConstruction prefix makes the non-anchor-cell mirror case safe
                // without flipping).
                if (IsWallDef(def) || IsTileDef(def)) {
                  // pos = взять позицию верхней части двери
                  // Идея в том чтобы отзеркалировать дверь и ставить без бага
                 if(orientation == Orientation.Neutral) {
                  orientation = Orientation.R180;
                  anchorCell = Grid.CellAbove(__0);
                 }
                 if (orientation == Orientation.R90) {
                  orientation = Orientation.R270;
                  anchorCell = Grid.CellRight(__0);
                 }
                 // якорь плана = клетка стены (кандидат): guard MarkArea (Constructable.cs:452)
                 // не вытеснит стену из TileLayer-слота, т.к. слот якоря уже занят живой стеной
                }
                Vector3 pos = Grid.CellToPosCBC(anchorCell, Grid.SceneLayer.Building);
                PUtil.LogDebug("orientation={0}; __instance.buildingOrientation={1}".F(orientation, __instance.buildingOrientation));
                GameObject plan = def.TryReplaceTile(visualizer, pos, orientation, selected, __instance.facadeID);
                Grid.Objects[anchorCell, (int)def.ReplacementLayer] = plan;

#if DEBUG
                PUtil.LogDebug("def.TryReplaceTile({0},{1},{2},{3}, {4}) => plan={5}".F(visualizer, pos, orientation, selected, __instance.facadeID, plan));
#endif
                // The native PostProcessBuild already ran with a null build result, so mirror its
                // master-priority assignment (BuildTool.cs:440-448); the placement sound is
                // intentionally skipped.
                if (plan != null)
                {
                    Prioritizable prioritizable = plan.GetComponent<Prioritizable>();
                    if (prioritizable != null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: plan {0} — приоритет: BuildMenu={1}, PlanScreen={2}".F(plan.name, BuildMenu.Instance != null, PlanScreen.Instance != null));
#endif
                        if (BuildMenu.Instance != null)
                        {
#if DEBUG
                            PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: SetMasterPriority от BuildMenu = {0}".F(BuildMenu.Instance.GetBuildingPriority()));
#endif
                            prioritizable.SetMasterPriority(BuildMenu.Instance.GetBuildingPriority());
                        }
                        if (PlanScreen.Instance != null)
                        {
#if DEBUG
                            PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: SetMasterPriority от PlanScreen = {0}".F(PlanScreen.Instance.GetBuildingPriority()));
#endif
                            prioritizable.SetMasterPriority(PlanScreen.Instance.GetBuildingPriority());
                        }
                    }
                    else
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: план {0} без Prioritizable".F(plan.name));
#endif
                    }
                }
            }
        }

        // 4. Stage 3: the 6-arg canonical overload — let a wall be placed OVER an
        // existing door (the reverse of the door-over-wall feature). Every placement
        // validity check funnels into this 6-arg method (all the other IsValidPlaceLocation
        // overloads and the game's TryPlace, TryReplaceTile (BuildingDef.cs:490) and
        // BuildTool.TryBuild call sites), and a wall-over-door placement fails there in
        // IsAreaClear's NotInTiles branch on the hard block
        // `else if (Grid.HasDoor[cell]) flag = false;` (BuildingDef.cs:756-759, anchor
        // cell only). That block is the ONLY obstacle: for a wall def (ObjectLayer
        // Backwall, NotInTiles rule, TileLayer NumLayers) every other check passes on a
        // door cell — the loop's occupancy test reads the Backwall slot (a door sits on
        // the Building slot), the tile-layer test is skipped (NumLayers), the
        // foundation-slot test is skipped with replace_tile:true, and the three port
        // checks return true early for a null source_go / portless def. The postfix
        // therefore flips __result only when the native call failed, only on the
        // replace_tile:true path (NEVER flip TryPlace — that would build the wall
        // ALONGSIDE the door), only for wall defs, only when the anchor cell actually
        // has a door (i.e. the HasDoor block is what failed), and only when the shared
        // candidate search + the source_go-aware replacement-layer occupancy gate
        // confirm a real door-over replacement. Downstream: TryReplaceTile stores the construction
        // plan at the ReplacementLayer slot (BuildTool.cs:378: Grid.Objects[anchor,
        // ReplacementBackwall]), and construction completion (Constructable
        // OnCompleteWork, Constructable.cs:~161) destroys the door candidate (deferred
        // DestroySelf, items returned via Deconstructable.SpawnItemsFromConstruction)
        // before Def.Build places the wall; Door.OnDestroy clears Grid.HasDoor
        // (Door.cs:520).
        // WHY THIS SHAPE: the same 0Harmony v2 quirk as the 4-arg postfix above — a
        // postfix signature must NOT declare the method's `out string` parameter at a
        // byref position (EmitCallParameter chokes on it), so __4 (the out reason) and
        // __5 (restrictToActiveWorld) are simply omitted; positional __0/__1/__2/__3
        // plus ref __result are bulletproof in this Harmony build. The out reason is
        // left stale — the callers that matter here ignore it when the bool is true
        // (TryReplaceTile uses `out var _`, BuildTool.cs:377-379 checks the bool only).
        // NEVER dereferences __0 (source_go can be null — the mod's cosmetic gate calls
        // with null). NO [HarmonyPatch] attributes on purpose: attached programmatically
        // in OnLoad via harmony.Patch(...).
        public static class BuildingDef_IsValidPlaceLocation6_DoorReplacement__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject __0, int __1, Orientation __2, bool __3, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                if (!__3)
                {
                    return;
                }
                if (!IsWallDef(__instance))
                {
                    return;
                }
                if (!Grid.HasDoor[__1])
                {
                    return;
                }
                if (!TryFindReplacementCandidate(__instance, __1, __2, out _, out _))
                {
                    return;
                }
                // source_go-aware (Bug A, deviation from the original 2-change brief):
                // the construction-start re-check reaches this postfix through the
                // gate's final 6-arg call with the plan's own GameObject as __0. The
                // plan occupies the ReplacementBackwall slot from creation
                // (Constructable.cs:375-391), so the native IsReplacementLayerOccupied
                // would see THAT and never flip — leaving the plan cancelled. For the
                // drag callers __0 is the visualizer (never in the slot) or null, so
                // their behavior is unchanged.
                if (ReplacementLayerOccupiedExcludingSelf(__instance, __1, __0))
                {
                    return;
                }
                __result = true;
            }
        }

        // Bug A fix: the construction-start re-check (Constructable.PlaceDiggables,
        // Constructable.cs:747) calls the CORE int overload
        // IsValidBuildLocation(GameObject, int, Orientation, bool, out string)
        // (BuildingDef.cs:1221) with the plan's own GameObject as source_go and
        // IsReplacementTile as replace_tile. For a wall-over-door replacement plan
        // that call fails natively in the NotInTiles branch (BuildingDef.cs:1285-1307:
        // flag = (replace_tile || gameObject2 == null || gameObject2 == source_go)
        // && !Grid.HasDoor[cell]) because the live door is still at the anchor cell,
        // and Constructable then cancels the plan ("Место не подходит для стройки")
        // and builds nothing. Flipping via the shared gate makes the re-check pass
        // exactly like it does during the drag. The gate's own final 6-arg validity
        // call is flipped by the 6-arg postfix above (whose occupancy check is now
        // source_go-aware as well) — the 6-arg postfix never calls the gate, so there
        // is no recursion.
        // WHY THIS SHAPE: the same 0Harmony v2 quirk as the 6-arg postfix — a postfix
        // signature must NOT declare the method's `out string` parameter at a byref
        // position (EmitCallParameter chokes on it), so the out reason is omitted;
        // positional __0/__1/__2/__3 plus ref bool __result is bulletproof in this
        // Harmony build (EmitCallParameter maps __N straight to the original
        // method's Nth parameter, 0Harmony MethodCreatorTools.cs:640-651). __0 can be
        // null on some callers (BaseUtilityBuildTool.cs:415/473) — the gate and the
        // helper are null-safe. NO [HarmonyPatch] attributes on purpose: attached
        // programmatically in OnLoad via harmony.Patch(...).
        public static class BuildingDef_IsValidBuildLocation_DoorReplacement__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject __0, int __1, Orientation __2, bool __3, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                // E-1 (Stage 2.5, Bug E): construction-start recheck verdict,
                // same dedup'd logging (one call per build start, not per frame).
                string reason;
                bool pass = TryReplacementGate(__instance, __0, __1, __2, out reason);
                LogGateVerdict("recheck", __instance, __1, __2, pass, reason);
                __result = pass;
            }
        }

        // 7. Crash fix (door-over-door, both 1x2): when a MULTI-cell replacement plan
        // completes, OnCompleteWork's anchor branch (Constructable.cs:158-215) destroys
        // the anchor-cell candidate DEFERRED (SimCellOccupier.DestroySelf with the
        // FinishConstruction callback, :168-174) without calling FinishConstruction
        // inline. The deferred callback fires during the dying building's own OnCleanUp
        // — at that moment the old building's Grid.ObjectLayers entries are STILL
        // present (a 1x2 door registers its GO in ObjectLayers[ObjectLayer.Building]
        // at BOTH cells; the cleanup that removes them, BuildingComplete.OnCleanUp ->
        // Def.UnmarkArea, Building.cs:241-263 / BuildingDef.cs:976, has not run yet).
        // FinishConstruction (Constructable.cs:223-257) then sweeps the whole new area
        // (IsReplacementTile && PlacementOffsets.Length > 1 -> RunOnArea, :228-256) and
        // finds the SAME already-dying GO at the second cell: its DestroySelf /
        // SpawnItemsFromConstruction / Trigger / DeleteObject calls (:237-253) re-read
        // the temperature structure of an element whose KSplitCompactedVector handle is
        // already destroyed — ArgumentOutOfRangeException crash
        // (Deconstructable.SpawnItemsFromConstruction -> PrimaryElement.get_Temperature).
        // A 1x2 door is the first multi-cell replacement def the game sees (all vanilla
        // replacement defs are 1x1), so the native sweep was never exercised on a case
        // where the same GO occupies two cells of the new area.
        // The prefix (runs at FinishConstruction start, i.e. inside the deferred
        // callback) clears the DUPLICATED grid entries of the anchor candidate at the
        // non-anchor cells: the null write removes the ObjectLayers[layer] dictionary
        // entry (Grid.cs:452-465, ObjectLayerIndexer.set) so the native sweep's
        // GetReplacementCandidate (BuildingDef.cs:324 — ObjectLayers[...].ContainsKey
        // + BuildingComplete test) finds nothing there and skips the double-destroy.
        // The dying building's own cleanup (reference-matched `== go` null writes in
        // Def.UnmarkArea) sees the slot already empty and no-ops — same write shape as
        // the native code, no new code path.
        // Strictly additive: bails unless IsReplacementTile; the C fix (re-entry
        // skip) applies to every replacement plan, the D/R6 candidate handling only
        // when PlacementOffsets.Length > 1 (the native sweep's condition). Every
        // preview/drag path is untouched.
        // Stage 2.5 additions:
        //  * C fix (Bug C): the anchor branch's DestroySelf fires its completion
        //    callback ONCE PER CANDIDATE CELL (SimCellOccupier.cs:162-166 deferred
        //    / :176 inline) — for a 1x2 candidate (a door) the second fire re-enters
        //    FinishConstruction after the first run finished (Constructable.cs:289
        //    `finished = true`, :290 DeleteObject). The second run's Def.Build was
        //    the ghost duplicate building ("the door isn't demolished, natural
        //    blocks") plus a duplicate NewBuilding event and double storage drain.
        //    The prefix now returns false on re-entry.
        //  * D fix (Bug D): a DISTINCT live candidate at a non-anchor cell (partial
        //    overlap with a different door — the old door sits in exactly one cell
        //    of the new area, outside the anchor branch) was re-destroyed by the
        //    native sweep via DestroySelf(null) — the null callback NREs in the
        //    non-element cell branch (SimCellOccupier.cs:176), aborting the build
        //    (old door demolished, items lost, new door never built). The prefix
        //    now handles every distinct candidate itself with a safe non-null
        //    callback, item-spawn, replacement Trigger and DeleteObject.
        // NO [HarmonyPatch] attributes on purpose: attached programmatically in OnLoad
        // via harmony.Patch(...) like the other registrations.
        public static class Constructable_FinishConstruction_DoorReplacement__Patch
        {
            // The D fix's safe replacement for the native sweep's null DestroySelf
            // callback (Constructable.cs:240): non-null, so both DestroySelf
            // branches (deferred / inline) are safe.
            private static readonly System.Action NoopAction = delegate
            {
            };

            private static FieldInfo s_finishedField;

            // Bug C: reads Constructable's private `finished` field
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

            // Clears the candidate's grid entries (its own ObjectLayer at all its
            // cells, plus its TileLayer when it is a tile piece) — reference-matched
            // `== candidate` null writes, the same write shape as the native
            // UnmarkArea (BuildingDef.cs:976). Needed because a replaced building's
            // OnCleanUp skips unmarking (BuildingComplete.cs:257, WasReplaced) and
            // its cells sit outside the new building's marked area — without this
            // the entries stay stale: ghost tile visuals, and a later
            // GetReplacementCandidate would find the dead object.
            private static void ClearCandidateGridEntries(GameObject candidate)
            {
                if (candidate == null)
                {
                    return;
                }
                Building candidateBuilding = candidate.GetComponent<Building>();
                if (candidateBuilding == null || candidateBuilding.Def == null)
                {
                    return;
                }
                BuildingDef candidateDef = candidateBuilding.Def;
                int[] cells = candidateBuilding.PlacementCells;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    if (Grid.Objects[cell, (int)candidateDef.ObjectLayer] == candidate)
                    {
                        Grid.Objects[cell, (int)candidateDef.ObjectLayer] = null;
                    }
                    if (candidateDef.IsTilePiece && Grid.Objects[cell, (int)candidateDef.TileLayer] == candidate)
                    {
                        Grid.Objects[cell, (int)candidateDef.TileLayer] = null;
                    }
                }
            }

            public static bool Prefix(Constructable __instance, UtilityConnections __0, WorkerBase __1)
            {
                if (!__instance.IsReplacementTile)
                {
                    return true;
                }
                // Constructable's `building` field is private ([MyCmpReq],
                // Constructable.cs:31) — the same component is fetched via
                // GetComponent, null-guarded.
                Building building = __instance.GetComponent<Building>();
                if (building == null || building.Def == null)
                {
                    return true;
                }
                BuildingDef def = building.Def;
                // C fix (Stage 2.5, Bug C): re-entry guard — runs for EVERY
                // replacement plan, before the multi-cell handling. The second
                // FinishConstruction run (fired by the anchor branch's per-cell
                // completion callback on a multi-cell candidate) must be skipped:
                // the first run already built the building, set finished = true and
                // DeleteObject()d the plan — running the original again builds a
                // ghost duplicate.
                if (IsAlreadyFinished(__instance))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] FinishConstruction: второй заход для плана {0} (finished==true) — пропуск (C fix)".F(__instance.name));
#endif
                    return false;
                }
                // D/R6 fix (multi-cell plans only — the native sweep only runs when
                // PlacementOffsets.Length > 1, Constructable.cs:228).
                if (def.PlacementOffsets.Length <= 1)
                {
                    return true;
                }
                // Mirror FinishConstruction's own setup (Constructable.cs:225-227).
                Rotatable rotatable = __instance.GetComponent<Rotatable>();
                Orientation orientation = (rotatable != null) ? rotatable.GetOrientation() : Orientation.Neutral;
                int anchorCell = Grid.PosToCell(__instance.transform.GetLocalPosition());
                // The anchor candidate is the one OnCompleteWork's anchor branch already
                // destroyed + item-spawned (Constructable.cs:161-213). It may be null
                // (no candidate at the anchor — the mirror case, or full area clear):
                // every candidate found below is then distinct and handled by the D fix.
                GameObject anchorCandidate = def.GetReplacementCandidate(anchorCell);
                if (def.ReplacementCandidateLayers == null)
                {
                    return true;
                }
                // Handle EVERY non-anchor-cell candidate here, so the native sweep
                // (Constructable.cs:228-256) finds nothing and can never call
                // DestroySelf(null) on it (the null-callback NRE — Bug D).
                def.RunOnArea(anchorCell, orientation, (offsetCell) =>
                {
                    if (offsetCell == anchorCell)
                    {
                        return;
                    }
                    GameObject candidate = def.GetReplacementCandidate(offsetCell);
                    if (candidate == null)
                    {
                        return;
                    }
                    if (candidate == anchorCandidate)
                    {
                        // R6: the SAME GO the anchor branch is already destroying
                        // (its DestroySelf + items + Trigger + DeleteObject run in
                        // OnCompleteWork). Clear its grid entries so the native sweep
                        // skips it instead of re-destroying the dying object.
                        ClearCandidateGridEntries(candidate);
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] FinishConstruction: кандидат {0} в {1} == якорный кандидат — запись в grid очищена (R6)".F(candidate.name, offsetCell));
#endif
                        return;
                    }
                    // D fix (Stage 2.5, Bug D): a distinct live candidate. Handle it
                    // exactly as the native sweep does (Constructable.cs:237-253) —
                    // but with a safe non-null callback (the sweep passes null, which
                    // NREs at SimCellOccupier.cs:176 when the cell isn't the
                    // candidate's own element — the usual case for a building placed
                    // in air).
                    SimCellOccupier occupier = candidate.GetComponent<SimCellOccupier>();
                    if (occupier != null)
                    {
                        occupier.DestroySelf(NoopAction);
                    }
                    Deconstructable deconstructable = candidate.GetComponent<Deconstructable>();
                    if (deconstructable != null)
                    {
                        deconstructable.SpawnItemsFromConstruction(__1);
                    }
                    Constructable.ReplaceCallbackParameters replaceParams = default(Constructable.ReplaceCallbackParameters);
                    replaceParams.TileLayer = def.TileLayer;
                    replaceParams.Worker = __1;
                    Boxed<Constructable.ReplaceCallbackParameters> boxed = Boxed<Constructable.ReplaceCallbackParameters>.Get(replaceParams);
                    candidate.Trigger(1606648047, (object)boxed);
                    Boxed<Constructable.ReplaceCallbackParameters>.Release(boxed);
                    candidate.DeleteObject();
                    // The candidate's cells sit outside (or only partly inside) the
                    // new building's marked area, and its replaced-path OnCleanUp
                    // skips unmarking — clear its entries explicitly (stale entries
                    // would draw ghost visuals and be found by a later
                    // GetReplacementCandidate).
                    ClearCandidateGridEntries(candidate);
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] FinishConstruction: кандидат {0} в {1} обработан префиксом (D fix)".F(candidate.name, offsetCell));
#endif
                });
                return true;
            }
        }

        // 8. Spec feature 2: same-PrefabID door-over-door must be SKIPPED entirely — no
        // replacement plan, vanilla red "occupied" feedback. The native TryBuild
        // fallback's element gate (BuildTool.cs:371) only skips same-def+same-element;
        // same-def+different-element (an iron-element Door dragged over a
        // gold-element Door — one PrefabID "Door" def, two element variants) would
        // create a replacement plan in TryReplaceTile. Bailing HERE is the chokepoint:
        // every plan-creation path funnels into the 5-parameter overload
        // TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int)
        // (BuildingDef.cs:487) — the facadeID overload delegates to it
        // (BuildingDef.cs:508-510), and the only external caller is the survival-drag
        // fallback (BuildTool.cs:376); the mod's own BuildTool.TryBuild postfix also
        // calls the facadeID overload (above), so it is covered too. Bailing leaves
        // gameObject == null in the caller, so no plan is stored at
        // Grid.Objects[cell, ReplacementLayer] and PostProcessBuild(null) is a no-op.
        // The check is AREA-AWARE: it scans the dragged def's full area (RunOnArea)
        // and bails if ANY cell has a same-PrefabID door candidate (via the shared
        // IsSameDoorPrefab) — so the non-anchor cell of a 1x2 door counts, not just
        // the anchor — while different-PrefabID door-over-door (ManualPressureDoor
        // over Door, where the native gate already passes) and every non-door
        // direction are untouched.
        // NO [HarmonyPatch] attributes on purpose: attached programmatically in OnLoad.
        public static class BuildingDef_TryReplaceTile_SameDoorPrefab__Patch
        {
            public static bool Prefix(BuildingDef __instance, GameObject __0, Vector3 __1, Orientation __2, IList<Tag> __3, int __4)
            {
                if (__instance == null || !IsDoorDef(__instance))
                {
                    return true;
                }
                // __0 is the dragged def's ghost (the visualizer); its cell is the
                // anchor (BuildTool.cs:309 equates Grid.PosToCell(visualizer) with the
                // build cell). The null guard is defensive — every known caller passes
                // a live visualizer.
                if (__0 == null)
                {
                    return true;
                }
                // AREA-AWARE check: scan the dragged def's FULL area, not just the
                // anchor — a 1x2 door dragged so its anchor is empty but its
                // non-anchor cell overlaps a live same-PrefabID door must also be
                // skipped. GetReplacementCandidate already requires a BuildingComplete
                // on the candidate (BuildingDef.cs:324); the shared predicate
                // re-checks it and both door-ness and equal PrefabID (spec feature 2).
                int anchor = Grid.PosToCell(__0);
                bool bail = false;
                __instance.RunOnArea(anchor, __2, (int offsetCell) =>
                {
                    GameObject candidate = __instance.GetReplacementCandidate(offsetCell);
                    if (candidate != null && IsSameDoorPrefab(__instance, candidate))
                    {
                        bail = true;
                    }
                });
                if (bail)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
