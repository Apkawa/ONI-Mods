using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KMod;
using UnityEngine;
using UtilLibs;

using PeterHan.PLib.Core;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (BuildingDef, ObjectLayer, Tag, GameTags, Grid) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            try
            {
                // Explicit, verifiable registration of the tooltip POSTFIX.
                //
                // base.OnLoad only runs harmony.PatchAll(assembly)
                // (KMod/UserMod2.cs:15-18). The [HarmonyPatch] attribute now sits
                // on the CLASS (Harmony.PatchAll -> CreateClassProcessor(type).Patch(),
                // lib_sources/0Harmony/HarmonyLib/Harmony.cs:87-93), so PatchAll may
                // already have attached it — verify below and register explicitly
                // only if needed, a verified fallback that guarantees exactly one
                // copy of the postfix ends up attached.
                MethodInfo target = typeof(HoverTextScreen).GetMethod(
                    "BeginDrawing", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo postfix = typeof(FieldInfoTooltipPatch).GetMethod(
                    "Postfix", BindingFlags.Public | BindingFlags.Static);
                if (target == null || postfix == null)
                {
                    PUtil.LogError("registration failed: HoverTextScreen.BeginDrawing found={0}, FieldInfoTooltipPatch.Postfix found={1}".F(
                        target != null, postfix != null));
                    return;
                }

                // Harmony.GetPatchInfo returns null for methods that have never
                // been patched (Harmony.GetPatchInfo -> PatchProcessor.GetPatchInfo,
                // lib_sources/0Harmony).
                Patches patches = Harmony.GetPatchInfo(target);
                bool alreadyAttached = patches != null
                    && patches.Postfixes.Any(p => p.PatchMethod != null
                        && p.PatchMethod.MetadataToken == postfix.MetadataToken
                        && p.PatchMethod.DeclaringType == postfix.DeclaringType);
                if (alreadyAttached)
                {
                    PUtil.LogDebug("BeginDrawing postfix attached by PatchAll");
                }
                else
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    PUtil.LogDebug("BeginDrawing postfix attached manually (PatchAll did not apply it)");
                }
            }
            catch (Exception e)
            {
                PUtil.LogError("failed to register BeginDrawing postfix: " + e);
            }

            try
            {
                // Explicit, verifiable registration of the tooltip PREFIX on
                // the 4-arg HoverTextDrawer.DrawText — the drawing slot that
                // inserts the two field-info rows between the mass and
                // temperature rows of the element card (priority 1001, before
                // Better Info Cards' 1000). Same verify-then-register-fallback
                // pattern as the postfix above: PatchAll may have attached it
                // (top-level class, class-level [HarmonyPatch] attribute),
                // register it explicitly only if not.
                MethodInfo target = typeof(HoverTextDrawer).GetMethod(
                    "DrawText",
                    new[] { typeof(string), typeof(TextStyleSetting), typeof(Color), typeof(bool) });
                MethodInfo prefix = typeof(FieldInfoDrawTextPatch).GetMethod(
                    "Prefix", BindingFlags.Public | BindingFlags.Static);
                if (target == null || prefix == null)
                {
                    PUtil.LogError("registration failed: HoverTextDrawer.DrawText found={0}, FieldInfoDrawTextPatch.Prefix found={1}".F(
                        target != null, prefix != null));
                    return;
                }

                Patches patches = Harmony.GetPatchInfo(target);
                bool alreadyAttached = patches != null
                    && patches.Prefixes.Any(p => p.PatchMethod != null
                        && p.PatchMethod.MetadataToken == prefix.MetadataToken
                        && p.PatchMethod.DeclaringType == prefix.DeclaringType);
                if (alreadyAttached)
                {
                    PUtil.LogDebug("DrawText prefix attached by PatchAll");
                }
                else
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                    PUtil.LogDebug("DrawText prefix attached manually (PatchAll did not apply it)");
                }
            }
            catch (Exception e)
            {
                PUtil.LogError("failed to register DrawText prefix: " + e);
            }

            try
            {
                // Explicit, verifiable registration of the tooltip PREFIX on
                // HoverTextDrawer.NewLine(int) — the drawing slot that inserts
                // the two field-info rows between the mass and temperature
                // rows of the element card (priority 1001, before Better Info
                // Cards' 1000). Same verify-then-register-fallback pattern as
                // the postfix above: PatchAll may have attached it (top-level
                // class, class-level [HarmonyPatch] attribute), register it
                // explicitly only if not.
                MethodInfo target = typeof(HoverTextDrawer).GetMethod(
                    "NewLine", new[] { typeof(int) });
                MethodInfo prefix = typeof(FieldInfoNewLinePatch).GetMethod(
                    "Prefix", BindingFlags.Public | BindingFlags.Static);
                if (target == null || prefix == null)
                {
                    PUtil.LogError("registration failed: HoverTextDrawer.NewLine found={0}, FieldInfoNewLinePatch.Prefix found={1}".F(
                        target != null, prefix != null));
                    return;
                }

                Patches patches = Harmony.GetPatchInfo(target);
                bool alreadyAttached = patches != null
                    && patches.Prefixes.Any(p => p.PatchMethod != null
                        && p.PatchMethod.MetadataToken == prefix.MetadataToken
                        && p.PatchMethod.DeclaringType == prefix.DeclaringType);
                if (alreadyAttached)
                {
                    PUtil.LogDebug("NewLine prefix attached by PatchAll");
                }
                else
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                    PUtil.LogDebug("NewLine prefix attached manually (PatchAll did not apply it)");
                }
            }
            catch (Exception e)
            {
                PUtil.LogError("failed to register NewLine prefix: " + e);
            }
        }
    }
}
