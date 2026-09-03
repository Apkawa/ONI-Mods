using System.Collections.Generic;
using HarmonyLib;
using KMod;

// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (BuildingDef, DoorConfig, ObjectLayer, Tag, GameTags) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
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
    }
}
