using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using UnityEngine;
using UtilLibs;

namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        /// <summary>Icon asset file name.</summary>
        private static readonly string IconFileName = "overlay_space.png";

        private static Sprite? icon;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            LoadIcon();

            // Register our mode with OverlayScreen (called once on spawn).
            PatchUtil.TryPatch(harmony, typeof(OverlayScreen), "RegisterModes", Type.EmptyTypes,
                "overlay mode registration",
                postfix: new HarmonyMethod(typeof(Mod), nameof(RegisterModes_Postfix)));

            // Add a toggle entry to the overlay menu (called once on menu prefab init).
            PatchUtil.TryPatch(harmony, typeof(OverlayMenu), "InitializeToggles", Type.EmptyTypes,
                "overlay menu toggle registration",
                postfix: new HarmonyMethod(typeof(Mod), nameof(InitializeToggles_Postfix)));

            // Re-activate the overlay after a save load: OverlayScreen is recreated
            // per level and resets to the None mode, and the ActiveWorldChanged
            // event does not fire on load — restore the mode here, once the screen
            // has finished spawning (currentModeInfo already set to a valid mode).
            PatchUtil.TryPatch(harmony, typeof(OverlayScreen), "OnSpawn", Type.EmptyTypes,
                "overlay re-activation after level load",
                postfix: new HarmonyMethod(typeof(Mod), nameof(OnSpawn_Postfix)));

            // Register our per-cell colour func in SimDebugView.getColourFuncs so
            // the game's built-in per-cell pipeline renders the tint. All the
            // built-in colour funcs register before OnPrefabInit, so this postfix
            // runs after all of them.
            PatchUtil.TryPatch(harmony, typeof(SimDebugView), "OnPrefabInit", Type.EmptyTypes,
                "sim debug view colour func registration",
                postfix: new HarmonyMethod(typeof(Mod), nameof(GetColourFuncs_Postfix)));

            // Register our mode in StatusItem's private overlay-bitfield map so
            // the hover card stops logging "has no StatusItemOverlay value"
            // every frame (same mapping the game uses for simple visual modes
            // like Oxygen/TileMode).
            try
            {
                var field = AccessTools.Field(typeof(StatusItem), "overlayBitfieldMap");
                if (field != null)
                {
                    var map = (Dictionary<HashedString, StatusItem.StatusItemOverlays>)field.GetValue(null);
                    map[SpaceOverlayMode.ID] = StatusItem.StatusItemOverlays.None;
                }
                else
                {
                    PUtil.LogWarning("StatusItem.overlayBitfieldMap field not found");
                }
            }
            catch (Exception ex)
            {
                PUtil.LogWarning("failed to register status-item overlay mapping");
                PUtil.LogExcWarn(ex);
            }
        }

        // ---------------- patches ----------------

        private static void RegisterModes_Postfix(OverlayScreen __instance)
        {
            try
            {
                var registerMode = AccessTools.Method(typeof(OverlayScreen), "RegisterMode");
                if (registerMode == null)
                {
                    PUtil.LogWarning("OverlayScreen.RegisterMode method not found");
                    return;
                }
                registerMode.Invoke(__instance, new object[] { new SpaceOverlayMode() });
            }
            catch (Exception e)
            {
                PUtil.LogExcWarn(e);
            }
        }

        private static void InitializeToggles_Postfix(OverlayMenu __instance)
        {
            try
            {
                // OverlayToggleInfo is a private nested class of OverlayMenu —
                // RefreshButtons() hard-casts list entries to it, so the entry must be
                // exactly that type; instantiate it via reflection.
                var toggleInfoType = typeof(OverlayMenu).GetNestedType("OverlayToggleInfo", BindingFlags.NonPublic);
                if (toggleInfoType == null)
                {
                    PUtil.LogWarning("OverlayMenu.OverlayToggleInfo nested type not found");
                    return;
                }
                var ctor = toggleInfoType.GetConstructor(new[]
                {
                    typeof(string), typeof(string), typeof(HashedString),
                    typeof(string), typeof(Action), typeof(string), typeof(string)
                });
                if (ctor == null)
                {
                    PUtil.LogWarning("OverlayMenu.OverlayToggleInfo constructor not found");
                    return;
                }
                string name = STRINGS.MISC.STATUSITEMS.SPACE.NAME;
                string tooltip = STRINGS.MISC.STATUSITEMS.SPACE.TOOLTIP;
                var info = ctor.Invoke(new object[]
                {
                    name,            // button label
                    "overlay_space", // icon asset name (unused: sprite supplied via getSpriteCB)
                    SpaceOverlayMode.ID,
                    string.Empty,   // no required tech item
                    Action.NumActions, // no hotkey
                    tooltip,
                    name            // tooltip header
                });
                var spriteField = AccessTools.Field(typeof(KIconToggleMenu.ToggleInfo), "getSpriteCB");
                if (spriteField != null)
                {
                    spriteField.SetValue(info, new Func<Sprite>(GetIcon));
                }
                var listField = AccessTools.Field(typeof(OverlayMenu), "overlayToggleInfos");
                if (listField == null)
                {
                    PUtil.LogWarning("OverlayMenu.overlayToggleInfos field not found");
                    return;
                }
                var list = (List<KIconToggleMenu.ToggleInfo>)listField.GetValue(__instance);
                list.Add((KIconToggleMenu.ToggleInfo)info);
            }
            catch (Exception e)
            {
                PUtil.LogExcWarn(e);
            }
        }

        private static void OnSpawn_Postfix(OverlayScreen __instance)
        {
            if (!SpaceOverlayMode.Active)
            {
                return;
            }
            try
            {
                if (__instance.GetMode() != SpaceOverlayMode.ID)
                {
                    __instance.ToggleOverlay(SpaceOverlayMode.ID);
                }
#if DEBUG
                PUtil.LogDebug("overlay re-activated after level load");
#endif
            }
            catch (Exception e)
            {
                // ToggleOverlay derefs other UI singletons (ManagementMenu,
                // SimDebugView); if one is not ready at this point, log it and
                // leave the overlay off — the user can still toggle it manually.
                PUtil.LogExcWarn(e);
            }
        }

        private static void GetColourFuncs_Postfix(
            IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs)
        {
            ___getColourFuncs[SpaceOverlayMode.ID] = SpaceOverlayMode.GetColor;
        }

        // ---------------- icon ----------------

        // RefreshButtons() tolerates a null sprite (blank icon); the warning case is
        // already reported via PUtil.LogWarning in LoadIcon.
        private static Sprite GetIcon() => icon!;

        private void LoadIcon()
        {
            // The build copies ModAssets/* flat into the mod folder root
            // (Directory.Build.targets: CopyModsToDevFolder), so the root path is
            // primary; the ModAssets subfolder is a fallback for manually copied mods.
            var candidates = new[]
            {
                Path.Combine(path, IconFileName),
                Path.Combine(path, "ModAssets", IconFileName)
            };
            foreach (var candidate in candidates)
            {
                icon = PUIUtils.LoadSpriteFile(candidate);
                if (icon != null)
                {
                    return;
                }
            }
            PUtil.LogWarning("icon asset not found (tried: {0})".F(string.Join(", ", candidates)));
        }
    }
}
