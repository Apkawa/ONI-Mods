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
            // Ладно, это я вспоминал как я обходил этот момент. потом почитал что ты написал, это было то же самое
            // harmony.Patch(typeof(BuildingDef).GetMethodSafe(nameof(BuildingDef.CreateBuildingDef));
            // var original = typeof(BuildingDef).GetMethod(nameof(
            //                     BuildingDef.IsValidPlaceLocation), false,
            //                     typeof(GameObject),
            //                     typeof(int),
            //                     typeof(Orientation),
            //                     typeof(bool),
            //                     typeof(string), // the real parameter is `out string` (a ByRef type);
            //                                      // ByRef types are not legal attribute constants (CS0182)
            //                     typeof(bool)
            //                 );
            // var prefix = typeof(UserMod2).GetMethod(
            //   nameof(BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch)
            // );
            // harmony.Patch( original , prefix: new HarmonyMethod(prefix));

            // var original = typeof(BuildingDef).GetMethod(nameof(
            //                     BuildingDef.IsValidPlaceLocation), false,
            // typeof(Vector3), typeof(Orientation), typeof(ObjectLayer), typeof(ObjectLayer)
            //                 );
            // var prefix = typeof(UserMod2).GetMethod(
            //   nameof(BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch)
            // );
            // harmony.Patch( original , prefix: new HarmonyMethod(prefix));

            // base.OnLoad runs Harmony's PatchAll, which only applies types carrying
            // HarmonyPatch attributes (0Harmony decompiled: PatchAllUncategorized
            // filters by HasHarmonyAttribute, 0Harmony.decompiled.cs:6829). No
            // attribute-bound classes exist anymore in this mod: all five patches
            // (the two BuildingDef cosmetic postfixes, the BuildTool.TryBuild postfix,
            // the Assets.AddBuildingDef metadata postfix and the Stage 2.2.1
            // Constructable.MarkArea prefix+postfix pair) are attached PROGRAMMATICALLY
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
            MethodInfo tryBuild = FindMethod(typeof(BuildTool), "TryBuild", typeof(int));
            if (tryBuild == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] BuildTool.TryBuild(int) не найдена — upper-cell replacement fallback postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildTool.TryBuild(int) — upper-cell replacement fallback postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу upper-cell replacement fallback postfix на {0}".F(tryBuild));
#endif
                harmony.Patch(tryBuild, postfix: new HarmonyMethod(typeof(BuildTool_TryBuild_DoorReplacement__Patch), nameof(BuildTool_TryBuild_DoorReplacement__Patch.Postfix)));
            }
            // 4. Stage 2.2: Assets.AddBuildingDef(BuildingDef) — public static; FindMethod now
            // includes BindingFlags.Static so the declared-only scan finds it.
            MethodInfo addBuildingDef = FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef));
            if (addBuildingDef == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Assets.AddBuildingDef(BuildingDef) не найдена — replacement metadata postfix пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve Assets.AddBuildingDef(BuildingDef) — all-door replacement metadata postfix skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу replacement metadata postfix на {0}".F(addBuildingDef));
#endif
                harmony.Patch(addBuildingDef, postfix: new HarmonyMethod(typeof(Assets_AddBuildingDef_DoorReplacement__Patch), nameof(Assets_AddBuildingDef_DoorReplacement__Patch.Postfix)));
            }
            // 5. Stage 2.2.1: Constructable.MarkArea() — private no-param method, resolved by
            // name only (FindMethod with zero underlying types). The prefix+postfix pair
            // below keeps the door plan out of the tile layer at cells that never held a
            // foundation tile (backwall/air) — see the class doc comment.
            MethodInfo markArea = FindMethod(typeof(Constructable), "MarkArea");
            if (markArea == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Constructable.MarkArea() не найдена — door tile-layer unwind пропущен");
#endif
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve Constructable.MarkArea() — door tile-layer unwind skipped (game build mismatch?)");
            }
            else
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] Патчу door tile-layer unwind prefix+postfix на {0}".F(markArea));
#endif
                harmony.Patch(markArea,
                    prefix: new HarmonyMethod(typeof(Constructable_MarkArea_DoorTileUnwind__Patch), nameof(Constructable_MarkArea_DoorTileUnwind__Patch.Prefix)),
                    postfix: new HarmonyMethod(typeof(Constructable_MarkArea_DoorTileUnwind__Patch), nameof(Constructable_MarkArea_DoorTileUnwind__Patch.Postfix)));
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
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] FindMethod: {0}.{1} — имя не совпадает с {2}".F(type.Name, m.Name, name));
#endif
                    continue;
                }
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != underlyingTypes.Length)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] FindMethod: {0}.{1} — аргументов {2}, ожидалось {3}".F(type.Name, name, ps.Length, underlyingTypes.Length));
#endif
                    continue;
                }
                bool match = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt.IsByRef)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] FindMethod: {0}.{1} — параметр {2} byref ({3}), базовый тип {4}".F(type.Name, name, i, ps[i].ParameterType.Name, pt.GetElementType().Name));
#endif
                        pt = pt.GetElementType();
                    }
                    if (pt != underlyingTypes[i])
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] FindMethod: {0}.{1} — параметр {2}: {3} != {4}".F(type.Name, name, i, pt, underlyingTypes[i]));
#endif
                        match = false;
                        break;
                    }
                }
                if (match)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] FindMethod: найдена {0}".F(m));
#endif
                    return m;
                }
            }
#if DEBUG
            PUtil.LogDebug("[BuildDoorOverWall] FindMethod: {0}.{1} не найдена — патч будет пропущен".F(type.Name, name));
#endif
            return null;
        }

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
                if (__0 == null || !IsDoorDef(__0))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] AddBuildingDef postfix: пропуск, def={0} (null или не дверь)".F(__0 == null ? "(null)" : __0.PrefabID));
#endif
                    return;
                }
                // Idempotent: never override a def that already carries its own replacement
                // metadata (no native door does today).
                if (__0.ReplacementLayer != ObjectLayer.NumLayers)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] AddBuildingDef postfix: def={0} уже имеет ReplacementLayer={1} — пропуск".F(__0.PrefabID, __0.ReplacementLayer));
#endif
                    return;
                }
                __0.ReplacementLayer = ObjectLayer.ReplacementTile;
                __0.ReplacementCandidateLayers = new List<ObjectLayer>()
                {
                    ObjectLayer.FoundationTile,
                    ObjectLayer.Backwall
                };
                __0.ReplacementTags = new List<Tag>()
                {
                    GameTags.FloorTiles,
                    GameTags.Backwall,
                    GameTags.Ladders
                };
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] AddBuildingDef postfix: def={0} — проставлены метаданные замены (ReplacementLayer={1}, CandidateLayers={2}, Tags={3})".F(__0.PrefabID, __0.ReplacementLayer, __0.ReplacementCandidateLayers.Count, __0.ReplacementTags.Count));
#endif
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
        /// The 6-arg canonical overload the drag routing depends on is NEVER patched:
        /// TryPlace (BuildingDef.cs:467, called from BuildTool.cs:323) must keep
        /// failing so TryBuild still takes the replacement fallback
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
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsDoorDef: def={0} — BuildingComplete == null, false".F(def.PrefabID));
#endif
                return false;
            }
            CopyBuildingSettings cbs = go.GetComponent<CopyBuildingSettings>();
            if (cbs != null && cbs.copyGroupTag == GameTags.Door)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsDoorDef: def={0} — copyGroupTag == Door, true".F(def.PrefabID));
#endif
                return true;
            }
            bool hasDoorComponent = go.GetComponent<Door>() != null;
#if DEBUG
            PUtil.LogDebug("[BuildDoorOverWall] IsDoorDef: def={0} — cbs={1}, copyGroupTag={2}, Door-компонент={3}".F(def.PrefabID, cbs == null ? "(null)" : "есть", cbs == null ? "(null)" : cbs.copyGroupTag.ToString(), hasDoorComponent));
#endif
            return hasDoorComponent;
        }

        // Mirrors the survival drag gate in BuildTool.TryBuild (BuildTool.cs:350-385):
        //  - a replacement candidate exists at the anchor cell (BuildTool.cs:352;
        //    GetReplacementCandidate, BuildingDef.cs:324)
        //  - the replacement layer is unoccupied in every door cell (BuildTool.cs:354-360;
        //    IsReplacementLayerOccupied, BuildingDef.cs:305)
        //  - the candidate is a Replaceable, tag-matching occupant (BuildTool.cs:362-364;
        //    CanReplace, BuildingDef.cs:278)
        //  - TryReplaceTile's own validity check passes (BuildingDef.cs:490)
        private static bool IsReplacementPlacementPossible(BuildingDef def, GameObject source_go, int cell, Orientation orientation)
        {
            // Door-def scope (Stage 2.2): vanilla defs (exterior walls, windows, thermal blocks,
            // moulding tiles, building templates) also set ReplacementLayer/CandidateLayers — the
            // fix must not change their behavior.
            if (!IsDoorDef(def))
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: def={0} cell={1} — не дверь, false".F(def.PrefabID, cell));
#endif
                return false;
            }
            if (def.ReplacementLayer == ObjectLayer.NumLayers || def.ReplacementCandidateLayers == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: def={0} cell={1} — ReplacementLayer={2}, CandidateLayers={3} — false".F(def.PrefabID, cell, def.ReplacementLayer, def.ReplacementCandidateLayers == null ? "(null)" : def.ReplacementCandidateLayers.Count.ToString()));
#endif
                return false;
            }
            // Area-aware replacement-candidate search: the door is 1x2 and the candidate (wall) may
            // sit in EITHER door cell. GetReplacementCandidate is single-cell (BuildingDef.cs:324);
            // the anchor-only lookup is why a door with its upper cell on the wall (anchor = lower
            // cell, GenerateOffsets BuildingDef.cs:1795) showed a red ghost and could not be placed
            // (Stage 2.1). Iterate the whole door area with the same per-cell gate the native fallback
            // uses; anchor-first order is preserved (RunOnArea visits offset (0,0) first).
            GameObject candidate = null;
            def.RunOnArea(cell, orientation, (c) =>
            {
                if (candidate != null)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — кандидат уже найден ({1})".F(c, candidate.name));
#endif
                    return;
                }
                GameObject local = def.GetReplacementCandidate(c);
                if (local == null)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — кандидата нет".F(c));
#endif
                    return;
                }
                BuildingComplete complete = local.GetComponent<BuildingComplete>();
                if (complete == null || !complete.Def.Replaceable)
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — кандидат {1}, Replaceable={2}".F(c, local.name, complete == null ? "(null)" : complete.Def.Replaceable.ToString()));
#endif
                    return;
                }
                if (!def.CanReplace(local))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — CanReplace({1}) = false".F(c, local.name));
#endif
                    return;
                }
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — найден кандидат {1}".F(c, local.name));
#endif
                candidate = local;
            });
            if (candidate == null)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: def={0} cell={1} — кандидат не найден в области двери, false".F(def.PrefabID, cell));
#endif
                return false;
            }
            bool occupied = false;
            def.RunOnArea(cell, orientation, (c) =>
            {
                if (def.IsReplacementLayerOccupied(c))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: клетка {0} — replacement-layer занят".F(c));
#endif
                    occupied = true;
                }
            });
            if (occupied)
            {
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: def={0} cell={1} — replacement-layer занят в области двери, false".F(def.PrefabID, cell));
#endif
                return false;
            }
            string fail_reason;
            bool result = def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason, restrictToActiveWorld: false);
#if DEBUG
            PUtil.LogDebug("[BuildDoorOverWall] IsReplacementPlacementPossible: def={0} cell={1} — IsValidPlaceLocation(replace_tile) = {2}{3}".F(def.PrefabID, cell, result, result ? "" : " (" + fail_reason + ")"));
#endif
            return result;
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
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidPlaceLocation postfix: def={0} pos={1} — уже валидно, без изменений".F(__instance.PrefabID, __1));
#endif
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, __0, Grid.PosToCell(__1), __2))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidPlaceLocation postfix: def={0} pos={1} — замена возможна, __result: {2} -> true".F(__instance.PrefabID, __1, __result));
#endif
                    __result = true;
                }
                else
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidPlaceLocation postfix: def={0} pos={1} — замена невозможна, остаётся {2}".F(__instance.PrefabID, __1, __result));
#endif
                }
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
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidReplaceLocation postfix: def={0} pos={1} — пропуск (result={2}, IsDoorDef={3})".F(__instance.PrefabID, pos, __result, IsDoorDef(__instance)));
#endif
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, null, Grid.PosToCell(pos), orientation))
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidReplaceLocation postfix: def={0} pos={1} — замена возможна, __result: {2} -> true".F(__instance.PrefabID, pos, __result));
#endif
                    __result = true;
                }
                else
                {
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] IsValidReplaceLocation postfix: def={0} pos={1} — замена невозможна, остаётся {2}".F(__instance.PrefabID, pos, __result));
#endif
                }
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
                // Create the plan exactly like the native fallback tail (BuildTool.cs:377-379).
                Vector3 pos = Grid.CellToPosCBC(__0, Grid.SceneLayer.Building);
                GameObject plan = def.TryReplaceTile(visualizer, pos, __instance.buildingOrientation, selected, __instance.facadeID);
                Grid.Objects[__0, (int)def.ReplacementLayer] = plan;
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — создан replacement-plan {2} (кандидат {3}, pos={4})".F(def.PrefabID, __0, plan == null ? "(null)" : plan.name, candidate.name, pos));
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
#if DEBUG
                else
                {
                    PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — TryReplaceTile вернул null".F(def.PrefabID, __0));
                }
#endif
            }
        }

        // 5. Stage 2.2.1: door tile-piece wall-rendering breakage.
        // The tile-piece doors (PressureDoor/WoodenDoor/ManualPressureDoor/InsulatedDoor:
        // IsFoundation = true -> TileLayer = FoundationTile -> def.IsTilePiece, unlike the
        // pneumatic Door) break wall rendering when placed with the lower (anchor) cell in
        // the air and the upper cell on the wall. Chain (verified in .tmp/game_decomp):
        // Constructable.OnSpawn (Constructable.cs:343-346) calls the private no-param
        // MarkArea (Constructable.cs:441-461), which writes the plan into
        // Grid.Objects[cell, ReplacementTile] at every door cell (def.MarkArea,
        // BuildingDef.cs:829-843), then — for tile pieces only — rewrites the plan into
        // Grid.Objects[cell, TileLayer] at EVERY door cell whenever the anchor cell's tile
        // layer is empty (Constructable.cs:452-458), i.e. INCLUDING the backwall/air cell
        // that never holds a foundation tile (backwalls live in the separate
        // BackwallManager.backwallElement array, not in Grid.ObjectLayers), plus
        // Grid.IsTileUnderConstruction[anchor] = true (Constructable.cs:460, unconditional
        // for tile pieces). The world mesh formula (World.cs:99: renderedByWorld &&
        // (Grid.Objects[cell, 9] == null || Grid.IsTileUnderConstruction[cell])) then stops
        // drawing the wall cell as part of the connected surface, and nothing unwinds the
        // tile-layer entry (Constructable.UnmarkArea, :463-477, is replacement-layer only,
        // and SimCellOccupier.DestroySelf never restores RenderedByWorld) — so the
        // breakage persists after cancel.
        //
        // Fix: restore the native anchor-gated invariant around MarkArea:
        //   - Prefix captures the door area's pre-MarkArea tile-layer occupancy into the
        //     static s_preMarkTileOccupied (door replacement plans only; the static is
        //     reset at the top of the prefix so an abandoned prefix can never leak state).
        //   - Postfix unwinds: for every door cell that NOW holds the plan in the tile
        //     layer but did NOT hold any foundation tile before MarkArea, clear the entry
        //     and TileVisualizer.RefreshCell it (already refreshes the 4 neighbors); and if
        //     the anchor was NOT pre-occupied (the tile branch ran at the anchor) clear
        //     Grid.IsTileUnderConstruction[anchor]. Case A (anchor ON the wall: anchor WAS
        //     pre-occupied, branch never ran) is left exactly as it is today — that is
        //     why it renders fine now (flag true keeps the World.cs:99 formula consistent).
        //     The mixed case (anchor in air, upper cell on a REAL foundation tile, e.g. a
        //     floor) is also left untouched: pre-occupied cells keep their tile-layer entry
        //     (native replacement flow).
        // Cancel needs no extra patch: the replacement-layer entry is unwound by the
        // game's UnmarkArea, the tile layer was never written at the wall cell, and
        // RenderedByWorld self-heals via the World.cs:99 formula on the next sim tick
        // (both placement and DestroySelf trigger solid-change ticks at the wall cell).
        //
        // NO [HarmonyPatch] attributes on purpose (MarkArea is private): attached
        // programmatically in OnLoad via harmony.Patch(prefix: ..., postfix: ...).
        // Parameter name: the game's 0Harmony resolves `__N` patch parameters as
        // POSITIONAL indices of the original method's DECLARED parameters
        // (0Harmony.decompiled.cs:4453 throws "No parameter found at index N" when the
        // index is out of range). MarkArea() has ZERO declared parameters, so the
        // receiver MUST use the special name `__instance` (INSTANCE_PARAM,
        // 0Harmony.decompiled.cs:4895; InjectionType.Instance -> Ldarg_0, valid for
        // prefixes and postfixes alike) — the same convention as the TryBuild and
        // hover patches in this file. (Stage 2.2.1 crash #1: naming the receiver __0
        // threw at patch-creation time in OnLoad and killed the mod load.)
        public static class Constructable_MarkArea_DoorTileUnwind__Patch
        {
            // Tile cells occupied in the door's tile layer BEFORE MarkArea ran, captured by
            // the prefix for the plan currently in flight; null when no qualifying
            // (door replacement, tile-piece) plan is in flight.
            private static HashSet<int> s_preMarkTileOccupied;

            public static void Prefix(Constructable __instance)
            {
                // Reset FIRST: the prefix runs for every MarkArea call, so an abandoned
                // early return must never leave a stale capture behind for a later postfix.
                s_preMarkTileOccupied = null;
                try
                {
                    if (__instance == null || !__instance.IsReplacementTile)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea prefix: пропуск ({0}, IsReplacementTile={1})".F(__instance == null ? "(null)" : __instance.gameObject.name, __instance == null ? "(null)" : __instance.IsReplacementTile.ToString()));
#endif
                        return;
                    }
                    Building building = __instance.GetComponent<Building>();
                    if (building == null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea prefix: {0} — нет Building-компонента".F(__instance.gameObject.name));
#endif
                        return;
                    }
                    BuildingDef def = building.Def;
                    if (def == null || !IsDoorDef(def) || !def.IsTilePiece)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea prefix: {0} — пропуск (def={1}, IsDoorDef={2}, IsTilePiece={3})".F(__instance.gameObject.name, def == null ? "(null)" : def.PrefabID, def == null ? false : IsDoorDef(def), def == null ? false : def.IsTilePiece));
#endif
                        return;
                    }
                    s_preMarkTileOccupied = new HashSet<int>();
                    int anchor = Grid.PosToCell(__instance.transform.GetPosition());
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] MarkArea prefix: def={0} — фиксирую занятость tile-layer до MarkArea (anchor={1})".F(def.PrefabID, anchor));
#endif
                    def.RunOnArea(anchor, building.Orientation, (c) =>
                    {
                        if (Grid.Objects[c, (int)def.TileLayer] != null)
                        {
#if DEBUG
                            PUtil.LogDebug("[BuildDoorOverWall] MarkArea prefix: def={0} — клетка {1} занята в tile-layer ({2})".F(def.PrefabID, c, Grid.Objects[c, (int)def.TileLayer].name));
#endif
                            s_preMarkTileOccupied.Add(c);
                        }
                    });
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("[BuildDoorOverWall] Constructable.MarkArea prefix failed — door tile-layer unwind skipped: " + ex);
                    s_preMarkTileOccupied = null;
                }
            }

            public static void Postfix(Constructable __instance)
            {
                try
                {
                    HashSet<int> preMark = s_preMarkTileOccupied;
                    // Read and immediately reset: the postfix runs for every MarkArea
                    // call, so a missing/abandoned capture must not leak into the next one.
                    s_preMarkTileOccupied = null;
                    if (preMark == null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea postfix: {0} — preMark == null (не qualifying-план или prefix упал), пропуск".F(__instance == null ? "(null)" : __instance.gameObject.name));
#endif
                        return; // not a qualifying door replacement plan (or prefix failed)
                    }
                    Building building = __instance.GetComponent<Building>();
                    if (building == null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea postfix: {0} — нет Building-компонента".F(__instance.gameObject.name));
#endif
                        return;
                    }
                    BuildingDef def = building.Def;
                    if (def == null)
                    {
#if DEBUG
                        PUtil.LogDebug("[BuildDoorOverWall] MarkArea postfix: {0} — def == null".F(__instance.gameObject.name));
#endif
                        return;
                    }
                    int anchor = Grid.PosToCell(__instance.transform.GetPosition());
#if DEBUG
                    PUtil.LogDebug("[BuildDoorOverWall] MarkArea postfix: def={0} — отматываю tile-layer (anchor={1}, зафиксированных клеток: {2})".F(def.PrefabID, anchor, preMark.Count));
#endif
                    def.RunOnArea(anchor, building.Orientation, (c) =>
                    {
                        if (Grid.Objects[c, (int)def.TileLayer] == __instance.gameObject && !preMark.Contains(c))
                        {
#if DEBUG
                            PUtil.LogDebug("[BuildDoorOverWall] MarkArea postfix: def={0} — клетка {1} занята планом, но до MarkArea была пуста — очищаю tile-layer".F(def.PrefabID, c));
#endif
                            Grid.Objects[c, (int)def.TileLayer] = null;
                            TileVisualizer.RefreshCell(c, def.TileLayer, def.ReplacementLayer);
                        }
                    });
                    // The tile branch ran at the anchor (it was empty pre-MarkArea) — it set
                    // Grid.IsTileUnderConstruction[anchor] = true unconditionally; undo it,
                    // since the tile layer is now fully unwound for this plan.
                    if (!preMark.Contains(anchor))
                    {
#if DEBUG
                        PUtil.LogDebug("Grid.IsTileUnderConstruction[{0}] = false".F(anchor));
#endif
                        Grid.IsTileUnderConstruction[anchor] = false;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("[BuildDoorOverWall] Constructable.MarkArea postfix failed — door tile-layer unwind incomplete: " + ex);
                    s_preMarkTileOccupied = null;
                }
            }
        }
    }
}
