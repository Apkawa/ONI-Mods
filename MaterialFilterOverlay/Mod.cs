using System.Reflection;

using HarmonyLib;
using KMod;
using UnityEngine;

using PeterHan.PLib.Core;
using UtilLibs;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        /// <summary>
        /// The OverlayLegend instance whose legend we replaced with our panel.
        /// OverlayLegend is a KScreen recreated per level, so a null owner (or a
        /// destroyed panelGo) means "nothing built for the current screen".
        /// </summary>
        private static OverlayLegend panelOwner;

        /// <summary>
        /// Root GameObject of the <see cref="LegendPanel"/> owned by
        /// <see cref="panelOwner"/>; null when no panel is alive.
        /// </summary>
        private static GameObject panelGo;

        /// <summary>
        /// The live <see cref="LegendPanel"/>; null when no panel is alive.
        /// </summary>
        private static LegendPanel panel;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            PUtil.LogDebug("Build <date> commit=<hash>");

            // Replace the vanilla "Схема материалов" (TileMode) legend's category
            // filter section with our LegendPanel.
            PatchUtil.TryPatch(harmony, typeof(OverlayLegend), "SetLegend",
                new[] { typeof(OverlayModes.Mode), typeof(bool) },
                "TileMode legend replacement",
                postfix: new HarmonyMethod(typeof(Mod).GetMethod("SetLegendPostfix", BindingFlags.NonPublic | BindingFlags.Static)));
        }

        /// <summary>
        /// Runs after <c>OverlayLegend.SetLegend(OverlayModes.Mode mode, bool refreshing)</c>.
        /// For TileMode: hide the vanilla ToolParameterMenu and mount our
        /// LegendPanel at the same parent vanilla used for the menu. For any other
        /// mode: remove our panel if this screen owns it (vanilla panels for the
        /// other overlays are never touched).
        /// </summary>
        private static void SetLegendPostfix(OverlayLegend __instance, OverlayModes.Mode mode)
        {
            if (mode == null)
            {
                return;
            }
            if (!(mode is OverlayModes.TileMode))
            {
                if (panelOwner == __instance && panelGo != null)
                {
                    // Leave Game.Instance.tileOverlayFilters as-is: vanilla
                    // TileMode.OnFiltersChanged owns it while its mode is active.
                    UnityEngine.Object.Destroy(panelGo);
                    panelGo = null;
                    panelOwner = null;
                    panel = null;
                }
                return;
            }

            // The vanilla category filter menu, recreated by PopulateGeneratedLegend
            // before our postfix runs (TileMode always has legendFilters != null).
            var filterMenu = AccessTools.Field(typeof(OverlayLegend), "filterMenu").GetValue(__instance) as ToolParameterMenu;
            // Capture the menu's RectTransform before hiding it so the panel can be
            // pinned to the exact slot the menu occupied (the menu is hidden, but its
            // RectTransform size/anchors stay valid).
            RectTransform menuRt = null;
            if (filterMenu != null)
            {
                menuRt = filterMenu.GetComponent<RectTransform>();
                filterMenu.gameObject.SetActive(false);
            }

            // Same parent vanilla instantiates the menu at:
            //   Util.KInstantiateUI(toolParameterMenuPrefab, diagramsParent.transform.parent.gameObject)
            // (diagramsParent is a [SerializeField] private GameObject on OverlayLegend).
            var diagramsParent = AccessTools.Field(typeof(OverlayLegend), "diagramsParent").GetValue(__instance) as GameObject;
            if (diagramsParent == null)
            {
                PUtil.LogError("diagramsParent is null — cannot place legend panel");
                return;
            }
            var parent = diagramsParent.transform.parent;

            // Build exactly one panel per OverlayLegend instance: skip on re-activation
            // / refresh, rebuild (destroying the stale one) when the screen changed.
            if (panelOwner == __instance && panelGo != null)
            {
                return;
            }
            if (panelGo != null)
            {
                UnityEngine.Object.Destroy(panelGo);
            }
            panel = new LegendPanel();
            panel.Build(parent, menuRt);
#if DEBUG
            PUtil.LogDebug("TileMode legend: panel built under {0}".F(parent.name));
#endif
            // Panel build ends with the vanilla initial state (ALL selected, no
            // materials checked), which matches what vanilla TileMode already put
            // into tileOverlayFilters, so no initial OnFilterChanged is raised here.
            panel.OnFilterChanged += ApplyOverlayFilter;
            panelGo = panel.Root;
            panelOwner = __instance;
        }

        /// <summary>
        /// LegendPanel.OnFilterChanged handler: fills <c>Game.Instance.tileOverlayFilters</c>
        /// with the effective filter selection, clears the stale highlight targets
        /// of the active TileMode, and re-runs the overlay. 1+ materials
        /// checked → exactly those element tags; otherwise the tags of the selected
        /// category (ALL → union of all category tags, mirroring
        /// TileMode.OnFiltersChanged + Mode.InFilter).
        /// </summary>
        private static void ApplyOverlayFilter()
        {
            var game = Game.Instance;
            if (game == null || panel == null)
            {
                return;
            }
            var filters = game.tileOverlayFilters;
            filters.Clear();
            var materials = panel.GetSelectedMaterials();
            if (materials.Count > 0)
            {
                foreach (var element in materials)
                {
                    filters.Add(element.tag);
                }
            }
            else
            {
                filters.AddRange(LegendPanel.GetCategoryTags(panel.SelectedCategory));
            }
#if DEBUG
            PUtil.LogDebug("tileOverlayFilters: {0} tag(s); category {1}, {2} material(s) checked".F(
                filters.Count, panel.SelectedCategory, materials.Count));
#endif
            ClearModeHighlightTargets();
            game.ForceOverlayUpdate();
        }

        /// <summary>
        /// Clears the active TileMode's stale highlight target set, mirroring the
        /// tail of vanilla TileMode.OnFiltersChanged():
        /// <c>DisableHighlightTypeOverlay(layerTargets); layerTargets.Clear();
        /// Game.Instance.ForceOverlayUpdate();</c>
        /// TileMode.Update() only ever ADDS newly matching objects to
        /// <c>layerTargets</c> (it removes targets that go offscreen, never targets
        /// that simply stop matching), so without this step previously highlighted
        /// objects — e.g. aluminum buildings after selecting a copper-ore filter —
        /// keep glowing until the camera scrolls them off and back on.
        /// <c>DisableHighlightTypeOverlay</c> resets the highlight state of every
        /// current target and clears the set itself.
        /// </summary>
        private static void ClearModeHighlightTargets()
        {
            if (panelOwner == null)
            {
                return;
            }
            // OverlayLegend.currentMode is the Mode instance whose filters drive the
            // legend right now (set by SetLegend); for our panel it is the active
            // TileMode.
            var mode = AccessTools.Field(typeof(OverlayLegend), "currentMode").GetValue(panelOwner) as OverlayModes.TileMode;
            if (mode == null)
            {
                return;
            }
            var targets = AccessTools.Field(typeof(OverlayModes.TileMode), "layerTargets").GetValue(mode);
            if (targets == null)
            {
                return;
            }
            // Mode.DisableHighlightTypeOverlay<T> is a protected generic instance
            // method: invoke the <PrimaryElement> instantiation on the mode.
            AccessTools.Method(typeof(OverlayModes.Mode), "DisableHighlightTypeOverlay")
                .MakeGenericMethod(typeof(PrimaryElement))
                .Invoke(mode, new[] { targets });
        }
    }
}
