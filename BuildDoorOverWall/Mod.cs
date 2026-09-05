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
            // attribute-bound classes exist anymore in this mod: all four postfixes
            // (the two BuildingDef cosmetic ones, the BuildTool.TryBuild one and the
            // Assets.AddBuildingDef metadata one) are attached PROGRAMMATICALLY
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
                    return;
                }
                // Idempotent: never override a def that already carries its own replacement
                // metadata (no native door does today).
                if (__0.ReplacementLayer != ObjectLayer.NumLayers)
                {
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
                return false;
            }
            if (def.ReplacementLayer == ObjectLayer.NumLayers || def.ReplacementCandidateLayers == null)
            {
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
                candidate = local;
            });
            if (candidate == null)
            {
                return false;
            }
            bool occupied = false;
            def.RunOnArea(cell, orientation, (c) =>
            {
                if (def.IsReplacementLayerOccupied(c))
                {
                    occupied = true;
                }
            });
            if (occupied)
            {
                return false;
            }
            string fail_reason;
            bool result = def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason, restrictToActiveWorld: false);
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
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, __0, Grid.PosToCell(__1), __2))
                {
                    __result = true;
                }
                else
                {
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
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, null, Grid.PosToCell(pos), orientation))
                {
                    __result = true;
                }
                else
                {
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
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] def.TileLayer={0} def.ReplacementLayer={1} ObjectLayer.NumLayers={1}".F(def.TileLayer, def.ReplacementLayer, ObjectLayer.NumLayers));
#endif
                // Create the plan exactly like the native fallback tail (BuildTool.cs:377-379).
                Vector3 pos = Grid.CellToPosCBC(__0, Grid.SceneLayer.Building);
                Orientation orientation = __instance.buildingOrientation;
                if (def.TileLayer != ObjectLayer.NumLayers) {
                  // pos = взять позицию верхней части двери
                  // Идея в том чтобы перевернуть дверь вверх ногами и ставить без бага
                 if(orientation == Orientation.Neutral) {
                  orientation = Orientation.R180;
                 }
                 if (orientation == Orientation.R90) {
                  orientation = Orientation.R270;
                 }
                }
                PUtil.LogDebug("orientation={0}; __instance.buildingOrientation={1}".F(orientation, __instance.buildingOrientation));
                GameObject plan = def.TryReplaceTile(visualizer, pos, orientation, selected, __instance.facadeID);
                Grid.Objects[__0, (int)def.ReplacementLayer] = plan;

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
                // NOTE Я добавил ранний возврат вместо комментирования кода
                return;
                // NOTE Весь этот код ниже бесполезный, проблему не решает

                // Tile-piece doors (def.TileLayer != ObjectLayer.NumLayers, e.g. PressureDoor)
                // write the plan into the TileLayer slot of every door cell INSIDE the
                // TryReplaceTile call below: KInstantiate -> Constructable.OnSpawn -> MarkArea
                // (Constructable.cs:441-461) runs BuildingDef.MarkArea synchronously
                // (BuildingDef.cs:829-843), overwriting Grid.Objects[cell, TileLayer] in both
                // cells before the call returns. Capture the current TileLayer occupant of
                // every area cell now — the same area/orientation walk the candidate search
                // above uses — so a live wall in a non-anchor cell can be restored afterwards.
                List<int> areaCells = new List<int>();
                List<GameObject> capturedTileObjects = new List<GameObject>();
                if (def.TileLayer != ObjectLayer.NumLayers)
                {
                    def.RunOnArea(__0, __instance.buildingOrientation, (c) =>
                    {
                        areaCells.Add(c);
                        capturedTileObjects.Add(Grid.Objects[c, (int)def.TileLayer]);
                    });
                }
                if (plan != null && def.TileLayer != ObjectLayer.NumLayers)
                {
                    // Restore ONLY the captured non-null occupants. The anchor capture is null in
                    // the broken placement (anchor was air), so nothing is restored there and the
                    // plan legitimately keeps its own anchor TileLayer slot (Constructable.cs:441-461
                    // wrote it and owns it until completion). Restoring the wall into its own cell
                    // makes completion's candidate lookup (BuildingDef.cs:326-338, the
                    // BuildingComplete gate) find it at the upper cell and destroy + refund it
                    // exactly once (Constructable.cs:234-254). One RefreshCell per restored cell
                    // rebuilds the block-tile RenderInfos; its 4-neighbor sweep (TileVisualizer.cs:23-33)
                    // covers the anchor too, so no separate anchor refresh is needed.
                    for (int i = 0; i < areaCells.Count; i++)
                    {
                        GameObject captured = capturedTileObjects[i];
                        if (captured != null)
                        {
                            Grid.Objects[areaCells[i], (int)def.TileLayer] = captured;
                            TileVisualizer.RefreshCell(areaCells[i], def.TileLayer, def.ReplacementLayer);
                        }
                    }
                }
#if DEBUG
                PUtil.LogDebug("[BuildDoorOverWall] TryBuild postfix: def={0} cell={1} — создан replacement-plan {2} (кандидат {3}, pos={4})".F(def.PrefabID, __0, plan == null ? "(null)" : plan.name, candidate.name, pos));
#endif
            }
        }
    }
}
