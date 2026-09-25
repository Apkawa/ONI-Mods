using HarmonyLib;
using KMod;
using System;
using UtilLibs;

using PeterHan.PLib.Core;

// Namespace is the csproj RootNamespace (PrinterEasyInfo). Game types used by the
// patches (e.g. CarePackageContainer) live in the GLOBAL namespace in the
// installed game build (verified against the Assembly-CSharp.dll metadata:
// empty namespace, same as BuildingDef/Game/BuildTool), so they are referenced
// unqualified in the patch files.
namespace PrinterEasyInfo
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            // base.OnLoad runs Harmony's PatchAll, which only applies types carrying
            // HarmonyPatch attributes (0Harmony decompiled: PatchAllUncategorized
            // filters by HasHarmonyAttribute). There are no attribute-bound classes
            // in this mod yet: Stage 3 patches are attached PROGRAMMATICALLY via
            // PatchUtil.TryPatch (registration below).
            PUtil.LogDebug("loaded (version {0})".F(typeof(Mod).Assembly.GetName().Version));

            // Stage 3: CarePackageContainer.GenerateCharacter(bool is_starter)
            // (private instance method, Assembly-CSharp, global namespace) — fires
            // every time a care package is delivered/reshuffled and its character
            // is regenerated, i.e. exactly when the column contents change. The
            // postfix (stub for now, UI logic lands in Stage 3 steps 2-4) refreshes
            // the codex book button for the newly delivered item.
            PatchUtil.TryPatch(harmony, typeof(CarePackageContainer), "GenerateCharacter",
                new[] { typeof(bool) },
                "care-package codex info postfix",
                postfix: new HarmonyMethod(typeof(CarePackageContainer_GenerateCharacter_Patch), nameof(CarePackageContainer_GenerateCharacter_Patch.Postfix)));
        }
    }
}
