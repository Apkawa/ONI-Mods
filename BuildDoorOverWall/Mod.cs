using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using KMod;


// https://github.com/alex-3141/ONI-Mods/blob/master/BuildOverPlants/BuildOverPlants/BuildOverPlants.cs


namespace OxygenNotIncluded.Mods.Example
{
    public class ExampleMod: UserMod2
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
        
        // [HarmonyPatch(typeof(BuildingDef))]
        // [HarmonyPatch(nameof(BuildingDef.IsValidBuildLocation))]
        // // [HarmonyPatch(nameof(BuildingDef.IsValidPlaceLocation))]
        // [HarmonyDebug]
        // public static class BuildDefDebug__Patch
        // {
        //     public static void Postfix(ref bool __result)
        //     {
        //     }
        // }

    }
}
