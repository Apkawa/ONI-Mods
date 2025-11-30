using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using KMod;


// https://github.com/alex-3141/ONI-Mods/blob/master/BuildOverPlants/BuildOverPlants/BuildOverPlants.cs


namespace OxygenNotIncluded.Mods.Example
{
    public class ExampleMod : UserMod2
    {
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            // the assembly of this UserMod
            Console.WriteLine(assembly.GetName());

            // path to your mod's folder
            // path; 

            // the `Mod` instance for your mod
            Console.WriteLine(mod);

            Console.WriteLine($"Mod <{mod.title}> loaded: {mod.staticID}");
            HarmonyLib.Harmony.DEBUG = true;
            base.OnLoad(harmony);
        }


        [HarmonyPatch(typeof(LadderConfig))]
        [HarmonyPatch(nameof(LadderConfig.CreateBuildingDef))]
        public static class LadderConfigCreateBuildingDef__Patch
        {
            public static void Postfix(ref BuildingDef __result)
            {
                __result.ReplacementTags = new List<Tag>()
                {
                    GameTags.FloorTiles,
                    GameTags.Ladders,
                    GameTags.Backwall
                };
            }
        }

        // [HarmonyPatch(typeof(TileConfig))]
        // [HarmonyPatch(nameof(TileConfig.CreateBuildingDef))]
        // public static class TileConfig_CreateBuildingDef__Patch
        // {
        //     public static void Postfix(ref BuildingDef __result)
        //     {
        //         
        //         Console.WriteLine("TileConfig_CreateBuildingDef__Patch");
        //         __result.ReplacementTags.Append(GameTags.Door);
        //     }
        // }


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
                    ObjectLayer.LadderTile,
                    ObjectLayer.Backwall
                };
                __result.ReplacementTags = new List<Tag>()
                {
                    GameTags.Door,
                    GameTags.Ladders,
                    GameTags.FloorTiles,
                    GameTags.Backwall
                };
                __result.EquivalentReplacementLayers = new List<ObjectLayer>()
                {
                    ObjectLayer.ReplacementTile,
                    ObjectLayer.ReplacementLadder
                };
            }
        }

        [HarmonyPatch(typeof(DoorConfig))]
        [HarmonyPatch(nameof(DoorConfig.DoPostConfigureComplete))]
        public static class DoorConfig_DoPostConfigureComplete__Patch
        {
            public static void Postfix(ref GameObject go)
            {
                go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles);
            }
        }


        [HarmonyPatch(typeof(BuildTool))]
        [HarmonyPatch("InstantBuildReplace")]
        [HarmonyDebug]
        public static class BuildTool_InstantBuildReplace_Patch
        {
            public static bool Prefix(ref BuildTool __instance, int cell, Vector3 pos,
                GameObject tile,
                ref BuildingDef ___def
            )
            {
                if (___def.ReplacementCandidateLayers != null && ___def.WidthInCells > 1 || ___def.HeightInCells > 1)
                {
                    for (int index1 = 0; index1 < ___def.PlacementOffsets.Length; ++index1)
                    {
                        CellOffset rotatedCellOffset1 =
                            Rotatable.GetRotatedCellOffset(___def.PlacementOffsets[index1],
                                __instance.GetBuildingOrientation);


                        int cell1 = Grid.OffsetCell(cell, rotatedCellOffset1);
                        if (cell == cell1)
                        {
                            continue;
                        }

                        foreach (var layer in ___def.ReplacementCandidateLayers)
                        {
                            if (Grid.ObjectLayers[(int)layer].ContainsKey(cell1))
                            {
                                GameObject tile1 = Grid.ObjectLayers[(int)layer][cell1];
                                Console.WriteLine("5");
                                UnityEngine.Object.Destroy((UnityEngine.Object)tile1);
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}