using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KMod;
using UnityEngine;

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
            // filters by HasHarmonyAttribute, 0Harmony.decompiled.cs:6829). That is
            // how the DoorConfig.CreateBuildingDef postfix below lands.
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
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) — hover-text/visualizer postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(validPlace, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch), nameof(BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch.Postfix)));
            }
            MethodInfo validReplace = FindMethod(typeof(BuildingDef), "IsValidReplaceLocation",
                typeof(Vector3), typeof(Orientation), typeof(ObjectLayer), typeof(ObjectLayer));
            if (validReplace == null)
            {
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) — preview-tint postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(validReplace, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch), nameof(BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch.Postfix)));
            }
        }

        /// <summary>
        /// Resolves a method by name + per-parameter underlying type, normalizing
        /// byref parameters: an `out T` parameter is reported by the runtime as
        /// T& (IsByRef), so both sides are reduced to the underlying type before
        /// comparing (ParameterType.GetElementType() on T& yields T). First full
        /// match wins, null if none — callers log a skip rather than crash.
        /// </summary>
        private static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)
        {
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
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

        /// <summary>
        /// Teaches the door BuildingDef the replacement metadata so the game's own
        /// replacement flow (BuildTool / Constructable) can treat an existing
        /// foundation tile or backwall as a replacement candidate for a door:
        /// the door builds into ObjectLayer.ReplacementTile and replaces
        /// FoundationTile / Backwall occupants tagged FloorTiles, Backwall or Ladders.
        /// </summary>
        [HarmonyPatch(typeof(DoorConfig))]
        [HarmonyPatch(nameof(DoorConfig.CreateBuildingDef))]
        public static class DoorConfig_CreateBuildingDef__Patch
        {
            public static void Postfix(ref BuildingDef __result)
            {
                __result.ReplacementLayer = ObjectLayer.ReplacementTile;
                __result.ReplacementCandidateLayers = new List<ObjectLayer>()
                {
                    ObjectLayer.FoundationTile,
                    ObjectLayer.Backwall
                };
                __result.ReplacementTags = new List<Tag>()
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
            // door-def scope: vanilla defs (exterior walls, windows, thermal blocks, moulding tiles, templates) also set ReplacementLayer/CandidateLayers; the Stage-2 fix must not change their behavior
            if (def.PrefabID != DoorConfig.ID)
            {
                return false;
            }
            if (def.ReplacementLayer == ObjectLayer.NumLayers || def.ReplacementCandidateLayers == null)
            {
                return false;
            }
            GameObject candidate = def.GetReplacementCandidate(cell);
            if (candidate == null)
            {
                return false;
            }
            BuildingComplete complete = candidate.GetComponent<BuildingComplete>();
            if (complete == null || !complete.Def.Replaceable)
            {
                return false;
            }
            if (!def.CanReplace(candidate))
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
            return def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason, restrictToActiveWorld: false);
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
                if (__result || __instance.PrefabID != DoorConfig.ID)
                {
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, null, Grid.PosToCell(pos), orientation))
                {
                    __result = true;
                }
            }
        }
    }
}
