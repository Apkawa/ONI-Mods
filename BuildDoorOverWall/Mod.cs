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
            //                     typeof(string).MakeByRefType(),
            //                     typeof(bool)
            //                 );
            // var prefix = typeof(UserMod2).GetMethod(
            //   nameof(BuildingDef_IsValidPlaceLocation_ClearFalseReplacementWarning__Patch)
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
            // instead of via attributes, because attributes cannot express an
            // `out` parameter in a target signature: game Harmony v2 resolves
            // [HarmonyPatch] parameter types via Type.GetMethod(name, allDeclared,
            // null, paramTypes, []) (AttributePatch, 0Harmony.decompiled.cs:5350),
            // and plain typeof(string) does not match the `out string` parameter —
            // only string& (ByRef) does, and a byref Type is not a legal attribute
            // constant expression (CS0182 in attribute position). The attribute
            // classes for
            // these two postfixes therefore carry NO [HarmonyPatch] attributes;
            // PatchAll ignores them and we patch them here via the programmatic
            // Harmony.Patch(MethodBase, ...) API (0Harmony.decompiled.cs:6882),
            // with a byref-normalizing reflection lookup that tolerates whether
            // the runtime reports `out T` as T or T&.
            MethodInfo validPlace = FindMethod(typeof(BuildingDef), "IsValidPlaceLocation",
                typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string), typeof(bool));
            if (validPlace == null)
            {
                UnityEngine.Debug.LogError("[BuildDoorOverWall] could not resolve BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool) — hover-text postfix skipped (game build mismatch?)");
            }
            else
            {
                harmony.Patch(validPlace, postfix: new HarmonyMethod(typeof(BuildingDef_IsValidPlaceLocation_ClearFalseReplacementWarning__Patch), nameof(BuildingDef_IsValidPlaceLocation_ClearFalseReplacementWarning__Patch.Postfix)));
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
        /// Stage 2 (hover warning): the plain, non-replacement validity check reports
        /// "unoccupied space" (wall) / "back wall" (drywall) while the door hovers over
        /// a replacement candidate, so the hover card prints a false warning and the
        /// preview tint stays red — although the drag succeeds via the game's own
        /// replacement fallback. Both postfixes below re-run the survival-drag gate
        /// (BuildTool.cs:350-385 plus TryReplaceTile's check at BuildingDef.cs:490) for
        /// the DOOR DEF ONLY and hide the false failure without touching the bool result
        /// the drag routing depends on: TryPlace (BuildingDef.cs:467, called from
        /// BuildTool.cs:323) must keep failing so TryBuild still takes the replacement
        /// fallback (BuildTool.cs:350), never plain def.Build.
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

        // 1. Suppress the false warning text.
        // BuildToolHoverTextCard.UpdateHoverElements (BuildToolHoverTextCard.cs:49-55)
        // prints `fail_reason` whenever the plain check fails. The 4-arg overload it
        // calls (BuildingDef.cs:1098) funnels into this 6-arg canonical method, so
        // clearing the out-reason here (door def only, and only when the replacement
        // drag would actually succeed) makes the card draw an empty line instead of
        // "Must be built in unoccupied space" / "Obstructed by back wall".
        // The bool result is left FALSE: every routing decision based on the plain
        // check (TryPlace, BuildTool.cs:467; instant-build gate, BuildTool.cs:325)
        // keeps its current outcome, and the drag still falls into the replacement
        // fallback (BuildTool.cs:350).
        // NO [HarmonyPatch] attributes on purpose: the target has an `out string`
        // parameter and attribute targets cannot express byref parameter types
        // (a byref Type is not a legal attribute constant, CS0182; and plain
        // typeof(string) fails Type.GetMethod resolution — see OnLoad). This
        // class is attached
        // programmatically in OnLoad via harmony.Patch(...).
        public static class BuildingDef_IsValidPlaceLocation_ClearFalseReplacementWarning__Patch
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, Orientation orientation, bool replace_tile, ref string __out_fail_reason, bool restrictToActiveWorld)
            {
                if (__out_fail_reason == null || replace_tile || __instance.PrefabID != DoorConfig.ID)
                {
                    return;
                }
                if (IsReplacementPlacementPossible(__instance, source_go, cell, orientation))
                {
                    __out_fail_reason = string.Empty;
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
