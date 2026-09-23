using PeterHan.PLib.Core;
using UnityEngine;

namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Simulation overlay that highlights every cell of the active world that is
    /// in the "Space" biome zone — space exposed to the void, including buildings
    /// standing at the space boundary (walls, doors) — with a muted red tint.
    /// Rendering goes through the game's built-in per-cell colour func
    /// (<see cref="SimDebugView.getColourFuncs"/>): <see cref="GetColor"/> is the
    /// registered per-cell handler, added to that dictionary by a patch on
    /// <c>SimDebugView.OnPrefabInit</c>.
    /// </summary>
    public class SpaceOverlayMode : OverlayModes.Mode
    {
        /// <summary>Unique mode id used by OverlayScreen / OverlayMenu.</summary>
        public static readonly HashedString ID = "SpaceOverlay";

        /// <summary>Muted red tint applied to space-exposed cells.</summary>
        public static readonly Color Tint = new Color(1.0f, 0.2f, 0.2f, 0.35f);

        /// <summary>
        /// Desired overlay state, maintained by the active instance's
        /// <see cref="Enable"/>/<see cref="Disable"/>; read by the OnSpawn postfix
        /// to re-activate the overlay after a level load.
        /// </summary>
        public static bool Active = false;

        public override HashedString ViewMode() => ID;

        public override string GetSoundName() => "Off";

        public override void Enable()
        {
            Active = true;
#if DEBUG
            PUtil.LogDebug("enabled");
#endif
        }

        public override void Disable()
        {
            Active = false;
#if DEBUG
            PUtil.LogDebug("disabled");
#endif
        }

        /// <summary>
        /// Whether the given cell should be highlighted: the cell is revealed
        /// (fog of war lifted — everything is visible when fog of war is
        /// disabled in the game options), inside the active world, and in the
        /// "Space" biome zone. Space outside the map is also the Space zone,
        /// so <see cref="Grid.IsActiveWorld"/> is what bounds the tint to the
        /// map itself. Buildings (walls, doors) do not change a cell's zone, so
        /// buildings standing at the space boundary are tinted along with the
        /// space behind them.
        /// </summary>
        private static bool ShouldHighlight(int cell)
        {
            return Grid.IsActiveWorld(cell)
                && Grid.IsVisible(cell)
                && Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell) == ProcGen.SubWorld.ZoneType.Space;
        }

        /// <summary>
        /// Per-cell colour func registered in <see cref="SimDebugView.getColourFuncs"/>:
        /// returns <see cref="Tint"/> for space-exposed cells and transparent
        /// otherwise — cells hidden by fog of war, cells outside the active
        /// world, and any cell before the world's zone render data exists.
        /// Runs on a background thread every frame while the overlay is active,
        /// so it reads sim state directly like the game's built-in funcs.
        /// </summary>
        public static Color GetColor(SimDebugView view, int cell)
        {
            if (Game.Instance?.world?.zoneRenderData == null)
            {
                return Color.clear;
            }
            return ShouldHighlight(cell) ? Tint : Color.clear;
        }
    }
}
