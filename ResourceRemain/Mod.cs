using System.Collections.Generic;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using UnityEngine.EventSystems;

using UtilLibs;

namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            PUtil.LogDebug("Build 2026-09-23 commit=resource-remain-dev");

            // Postfix on PinnedResourcesPanel.CreateRow (PinnedResourcesPanel.cs:143,
            // private in the decompiled source): attach a dynamic per-cycle stats
            // tooltip to each pinned-resources row as soon as the panel builds it.
            // CreateRow is public in the publicized game reference (Directory.Build.props
            // Publicize=true), and PatchUtil.FindMethod also searches non-public members,
            // so the method resolves either way.
            //
            // The callback re-runs every 0.2 s while the row is hovered
            // (ToolTip.cs:273-288 UpdateWhileHovered, refreshWhileHovering), so the
            // tooltip line tracks live game state.
            PatchUtil.TryPatch(harmony, typeof(PinnedResourcesPanel), nameof(PinnedResourcesPanel.CreateRow), new[] { typeof(Tag) }, "pinned row tooltip", postfix: new HarmonyMethod(typeof(PinnedResourcesPanel_CreateRow_ResourceRemain__Patch), nameof(PinnedResourcesPanel_CreateRow_ResourceRemain__Patch.Postfix)));

            // Postfix on AllResourcesScreen.SpawnCategoryRow (AllResourcesScreen.cs:282,
            // private in the decompiled source, public in the publicized reference): the
            // only place resource rows of the "Show all resources" screen are created
            // (inline at :306/:352). After each call, every resource row created so far
            // sits in the private `resourceRows` dictionary (AllResourcesScreen.cs:100);
            // a static per-instance set tracks which Tags already got a tooltip so each
            // freshly created row is attached exactly once. Category header rows live in
            // the separate `categoryRows` dictionary and are never touched.
            PatchUtil.TryPatch(harmony, typeof(AllResourcesScreen), nameof(AllResourcesScreen.SpawnCategoryRow), new[] { typeof(Tag), typeof(GameUtil.MeasureUnit) }, "all-resources row tooltip", postfix: new HarmonyMethod(typeof(AllResourcesScreen_SpawnCategoryRow_ResourceRemain__Patch), nameof(AllResourcesScreen_SpawnCategoryRow_ResourceRemain__Patch.Postfix)));

            // Postfix on MeterScreen_Rations.OnTooltip (MeterScreen_Rations.cs:12,
            // `protected override string`, no parameters; publicized at build time):
            // after the game builds the calorie-counter tooltip, rebuild it with
            // per-cycle survival estimates — "(N cycles for M dups)" after the
            // available amount in the header, and a per-food estimate counting only
            // the duplicants allowed to eat that food ("(-)" when none may).
            // The rebuild reproduces the game's tooltip line-for-line; on any failure
            // the original tooltip is left untouched (RationsTooltip.Extend is
            // exception-safe and clears the tooltip only after computing everything).
            PatchUtil.TryPatch(harmony, typeof(MeterScreen_Rations), nameof(MeterScreen_Rations.OnTooltip), Array.Empty<Type>(), "rations tooltip", postfix: new HarmonyMethod(typeof(MeterScreen_Rations_OnTooltip_ResourceRemain__Patch), nameof(MeterScreen_Rations_OnTooltip_ResourceRemain__Patch.Postfix)));

            // Prefixes on MultiToggle.OnPointerEnter/OnPointerExit
            // (MultiToggle.cs:145/:224): the resource rows carry their own
            // MultiToggle (the row button, from the prefab, so it always
            // precedes anything added at runtime), and Unity's EventSystem only
            // notifies the FIRST pointer enter/exit handler in component order
            // on the hit GameObject — the ToolTip attached by
            // ResourceRowTooltip.Attach therefore never hears about the hover on
            // its own. The prefixes forward the event to a ToolTip on the same
            // GameObject. See MultiToggle_OnPointerEnter_ResourceRemain__Patch
            // below for the full reasoning (a full-row raycast-target child
            // GameObject was rejected: it would swallow the row button's and
            // the nested pin/notify buttons' clicks).
            PatchUtil.TryPatch(harmony, typeof(MultiToggle), nameof(MultiToggle.OnPointerEnter), new[] { typeof(PointerEventData) }, "row tooltip enter-forward", prefix: new HarmonyMethod(typeof(MultiToggle_OnPointerEnter_ResourceRemain__Patch), nameof(MultiToggle_OnPointerEnter_ResourceRemain__Patch.Prefix)));
            PatchUtil.TryPatch(harmony, typeof(MultiToggle), nameof(MultiToggle.OnPointerExit), new[] { typeof(PointerEventData) }, "row tooltip exit-forward", prefix: new HarmonyMethod(typeof(MultiToggle_OnPointerExit_ResourceRemain__Patch), nameof(MultiToggle_OnPointerExit_ResourceRemain__Patch.Prefix)));

            // Per-cycle resource stats sampler (CycleStats.cs): one
            // CycleStatsSampler (KMonoBehaviour, ISim1000ms) per world container.
            // The WorldContainer.OnSpawn postfix is the universal spawn hook —
            // it fires for the main world on save load AND new game (the
            // GameHashes.WorldAdded event only fires for rocket interiors in
            // this build) and for every later-added world. The postfix also runs
            // the one-time registration (Game.OnLoad / GameHashes.PauseChanged /
            // GameHashes.WorldAdded subscriptions + active-world spawn). The
            // direct EnsureStarted() call is a no-op at mod-load time (the game
            // scene does not exist yet) but covers the case where it does.
            PatchUtil.TryPatch(harmony, typeof(WorldContainer), nameof(WorldContainer.OnSpawn), new Type[0], "cycle stats sampler", postfix: new HarmonyMethod(typeof(WorldContainer_OnSpawn_ResourceRemain__Patch), nameof(WorldContainer_OnSpawn_ResourceRemain__Patch.Postfix)));
            CycleStats.EnsureStarted();
        }
    }

    // Postfix on PinnedResourcesPanel.CreateRow (PinnedResourcesPanel.cs:143): after the
    // panel builds a pinned-resources row, attach the per-cycle stats tooltip to its
    // GameObject — the nested PinnedResourceRow's public `gameObject` field
    // (PinnedResourcesPanel.cs:11). Attached programmatically in Mod.OnLoad (no
    // [HarmonyPatch] attributes — base.OnLoad runs Harmony's PatchAll, which only
    // applies attribute-carrying types, same pattern as SizeInTooltip/Mod.cs).
    //
    // The whole body is null-guarded and wrapped so any unexpected null/type issue
    // is a silent no-op — the mod must never throw into the game loop (one
    // PUtil error log at most).
    public static class PinnedResourcesPanel_CreateRow_ResourceRemain__Patch
    {
        private static bool loggedError;

        public static void Postfix(PinnedResourcesPanel __instance, Tag tag, PinnedResourcesPanel.PinnedResourceRow __result)
        {
            try
            {
                if (__instance == null || __result == null)
                {
                    return;
                }
                ResourceRowTooltip.Attach(__result.gameObject, tag);
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure in CreateRow postfix, row tooltip skipped: " + e);
                }
            }
        }
    }

    // Postfix on AllResourcesScreen.SpawnCategoryRow (AllResourcesScreen.cs:282).
    //
    // SpawnCategoryRow is the only row-creation point of the "Show all resources"
    // screen: per category it instantiates the category header prefab once
    // (AllResourcesScreen.cs:287-290, stored in `categoryRows`) and then, for every
    // discovered resource of that category, instantiates the resource row prefab
    // inline (:306) and stores it as `new ResourceRow(item, gameObject3)` in the
    // private `resourceRows` Dictionary<Tag, ResourceRow> (:352-353, field declared
    // at :100). ResourceRow exposes its Tag (ScreenRowBase.Tag, :25) and its
    // GameObject (ScreenRowBase.GameObject, :27) as public read-only properties.
    //
    // SpawnCategoryRow runs once per category and the `resourceRows` dictionary
    // accumulates across calls, so the postfix walks the dictionary and attaches
    // each row exactly once, remembering handled Tags in a static set. If the
    // screen instance is ever replaced (new AllResourcesScreen object), the set is
    // reset for the new instance. Category header rows are in `categoryRows`, not
    // `resourceRows`, so they are never touched.
    //
    // Whole body is wrapped so any unexpected null/type issue is a silent no-op —
    // the mod must never throw into the game loop (one PUtil error log at most).
    public static class AllResourcesScreen_SpawnCategoryRow_ResourceRemain__Patch
    {
        private static bool loggedError;

        private static AllResourcesScreen? lastScreen;
        private static readonly HashSet<Tag> attachedTags = new HashSet<Tag>();

        public static void Postfix(AllResourcesScreen __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                if (__instance != lastScreen)
                {
                    lastScreen = __instance;
                    attachedTags.Clear();
                }
                Dictionary<Tag, AllResourcesScreen.ResourceRow> rows = __instance.resourceRows;
                if (rows == null)
                {
                    return;
                }
                foreach (KeyValuePair<Tag, AllResourcesScreen.ResourceRow> entry in rows)
                {
                    if (!attachedTags.Add(entry.Key))
                    {
                        continue; // already attached on an earlier SpawnCategoryRow call.
                    }
                    AllResourcesScreen.ResourceRow row = entry.Value;
                    if (row == null || row.GameObject == null)
                    {
                        continue;
                    }
                    ResourceRowTooltip.Attach(row.GameObject, entry.Key);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure in SpawnCategoryRow postfix, row tooltips skipped: " + e);
                }
            }
        }
    }

    // Postfix on MeterScreen_Rations.OnTooltip (MeterScreen_Rations.cs:12-30).
    //
    // OnTooltip is a `protected override string` with no parameters (publicized at
    // build time) and runs on every tooltip refresh, so the game's own build of the
    // calorie-counter tooltip has just happened when the postfix runs — there is
    // nothing to capture in `__result` (the method returns ""), only to extend the
    // already-built tooltip in place. RationsTooltip.Extend rebuilds the same header,
    // spacer and per-food lines (in the game's exact order and formatting) with the
    // per-cycle survival estimates appended. Whole body is wrapped so any unexpected
    // null/type issue is a silent no-op — the mod must never throw into the game
    // loop (one PUtil error log at most).
    public static class MeterScreen_Rations_OnTooltip_ResourceRemain__Patch
    {
        private static bool loggedError;

        public static void Postfix(MeterScreen_Rations __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                RationsTooltip.Extend(__instance);
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure in OnTooltip postfix, rations estimate skipped: " + e);
                }
            }
        }
    }

    // Prefix on MultiToggle.OnPointerEnter (MultiToggle.cs:145). See
    // MultiToggle_OnPointerExit_ResourceRemain__Patch for the shared reasoning;
    // both prefixes are exception-safe (one PUtil error log at most) so the
    // game's pointer-event dispatch can never be thrown into.
    public static class MultiToggle_OnPointerEnter_ResourceRemain__Patch
    {
        private static bool loggedError;

        public static void Prefix(MultiToggle __instance, PointerEventData eventData)
        {
            try
            {
                ToolTip tip = __instance.GetComponent<ToolTip>();
                if (tip != null && tip.enabled)
                {
                    tip.OnPointerEnter(eventData);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure forwarding OnPointerEnter to the row ToolTip: " + e);
                }
            }
        }
    }

    // Prefix on MultiToggle.OnPointerExit (MultiToggle.cs:224).
    //
    // WHY: Unity's EventSystem delivers pointer enter/exit to the FIRST component
    // implementing IPointerEnterHandler/IPointerExitHandler in component order on
    // the raycast hit GameObject (and its descendants, never its ancestors). Both
    // resource-row kinds carry their own MultiToggle (the row button, part of the
    // row prefab, so it always precedes anything this mod adds at runtime), which
    // is why the runtime-added ToolTip never received OnPointerEnter/OnPointerExit
    // on its own and the tooltip never showed.
    //
    // The prefixes forward the event to a ToolTip on the SAME GameObject — exactly
    // the row tooltips this mod attaches — once per event (a MultiToggle that is
    // not the first handler on the hit object never runs, so no double-firing),
    // while MultiToggle keeps its own hover behaviour and every click (the row
    // button and the nested pin/notify buttons) works completely unchanged.
    //
    // WHY NOT a full-row, raycast-target child GameObject: a runtime-added child
    // becomes the topmost raycast target over the whole row, so pointer events
    // would land on the child and never reach the row's own MultiToggle (events
    // never route to ancestors) nor the nested pin/notify MultiToggles
    // (PinnedResourcesPanel.cs:217-218, AllResourcesScreen.cs:94-95).
    public static class MultiToggle_OnPointerExit_ResourceRemain__Patch
    {
        private static bool loggedError;

        public static void Prefix(MultiToggle __instance, PointerEventData eventData)
        {
            try
            {
                ToolTip tip = __instance.GetComponent<ToolTip>();
                if (tip != null && tip.enabled)
                {
                    tip.OnPointerExit(eventData);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure forwarding OnPointerExit to the row ToolTip: " + e);
                }
            }
        }
    }

    // Postfix on WorldContainer.OnSpawn (protected virtual override of
    // KMonoBehaviour.OnSpawn, no parameters, publicized at build time): fires
    // exactly once per spawned world container — the main world on save load
    // AND new game (worlds are instantiated from the save / world-gen data
    // before the scene's first frame), plus every later-added world (rocket
    // interiors — note GameHashes.WorldAdded only fires in
    // CreateRocketInteriorWorld in this build, so this postfix is the only
    // reliable hook for the main world). CycleStats.OnWorldContainerSpawned
    // runs the one-time event registration and spawns this world's sampler.
    // Whole body is null-guarded and wrapped so any unexpected issue is a
    // silent no-op — the mod must never throw into the game loop (one PUtil
    // error log at most).
    public static class WorldContainer_OnSpawn_ResourceRemain__Patch
    {
        private static bool loggedError;

        public static void Postfix(WorldContainer __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                CycleStats.OnWorldContainerSpawned(__instance);
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("unexpected failure in WorldContainer.OnSpawn postfix, cycle stats sampler skipped: " + e);
                }
            }
        }
    }
}
