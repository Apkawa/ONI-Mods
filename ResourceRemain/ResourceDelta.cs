// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (Tag, ClusterManager, WorldContainer, WorldInventory, GameUtil) resolve
// without extra usings.
using System;

// The per-second sampler state class (CycleStats.cs) is named `CycleStats` too,
// and inside this class the simple name `CycleStats` always binds to the nested
// stats struct below. The alias reaches the sampler's public API unambiguously.
using CycleStatsApi = OxygenNotIncluded.Mods.CycleStats;

namespace OxygenNotIncluded.Mods
{
    public static class ResourceDelta
    {
        /// <summary>
        /// Current (in-progress) cycle figures for one resource tag, read from
        /// the per-second sampler (CycleStats) instead of the game's resource
        /// tracker history (whose ring buffer is shorter than one cycle).
        /// </summary>
        public struct CycleStats
        {
            public float Produced;        // sum of positive amount steps in the current cycle
            public float Consumed;        // |sum of negative amount steps| in the current cycle
            public float Net;             // Produced - Consumed
            public float Available;       // current available amount (total - reserved)
            public float ElapsedSeconds;  // game-seconds elapsed in the current cycle
        }

        /// <summary>
        /// Previous completed cycle figures for one resource tag, read from
        /// the per-second sampler (CycleStats).
        /// </summary>
        public struct PreviousCycleStats
        {
            public float Produced;         // sum of positive amount steps in the previous cycle
            public float Consumed;         // |sum of negative amount steps| in the previous cycle
            public float Net;              // Produced - Consumed
            public float BoundaryAmount;   // available amount at the end of the previous cycle
        }

        /// <summary>
        /// Tries to read the current (in-progress) cycle stats for a resource
        /// tag from the per-second sampler. Zero figures are valid data; false
        /// only when the sampler is not ready for this world+tag (settle gate
        /// pending, no sample yet, sim not running) or the active world /
        /// inventory is unavailable.
        /// </summary>
        public static bool TryGetCurrentStats(Tag tag, out CycleStats stats)
        {
            stats = default;
            if (tag == null) return false;
            WorldInventory inventory;
            if (!TryGetActiveInventory(out inventory)) return false;
            int worldId = ClusterManager.Instance.activeWorldId;
            if (!CycleStatsApi.IsReady(worldId, tag)) return false;
            if (!CycleStatsApi.TryGetCurrent(worldId, tag, out float produced, out float consumed, out float elapsedSeconds))
            {
                return false;
            }
            stats.Produced = produced;
            stats.Consumed = consumed;
            stats.Net = produced - consumed;
            stats.ElapsedSeconds = elapsedSeconds;
            stats.Available = inventory.GetAmount(tag, includeRelatedWorlds: false);
            return true;
        }

        /// <summary>
        /// Tries to read the previous completed cycle's stats for a resource
        /// tag from the per-second sampler. Zero figures are valid data; false
        /// until a cycle boundary has been crossed and the sampler is ready
        /// for this world+tag, or the active world is unavailable.
        /// </summary>
        public static bool TryGetPreviousStats(Tag tag, out PreviousCycleStats stats)
        {
            stats = default;
            if (tag == null) return false;
            if (ClusterManager.Instance == null) return false;
            int worldId = ClusterManager.Instance.activeWorldId;
            if (!CycleStatsApi.IsReady(worldId, tag)) return false;
            if (!CycleStatsApi.TryGetPrevious(worldId, tag, out float produced, out float consumed, out float boundaryAmount))
            {
                return false;
            }
            stats.Produced = produced;
            stats.Consumed = consumed;
            stats.Net = produced - consumed;
            stats.BoundaryAmount = boundaryAmount;
            return true;
        }

        /// <summary>Formats an amount with the game's own per-tag display logic (calories/units/mass, localized; no per-time suffix).</summary>
        public static string FormatAmount(Tag tag, float amount)
        {
            if (tag == null) return string.Empty;
            return GameUtil.GetFormattedByTag(tag, amount, GameUtil.TimeSlice.None);
        }

        /// <summary>
        /// Approximate number of full cycles the current available supply will
        /// last at the current cycle's rate normalized to a full cycle
        /// (rate = net * 600 / elapsed). Null if net is non-negative,
        /// |net| &lt; 0.1 (noise), the cycle has elapsed less than 1 second, or
        /// the supply outlasts 999 cycles; 0 means already depleted.
        /// </summary>
        public static int? GetDeadlineCycles(CycleStats stats)
        {
            if (stats.Net >= 0f || -stats.Net < 0.1f) return null;
            if (stats.ElapsedSeconds < 1f) return null;
            float ratePerCycle = stats.Net * CycleStatsApi.CYCLE_SECONDS / stats.ElapsedSeconds; // negative
            return FloorCycleCount(stats.Available / -ratePerCycle);
        }

        /// <summary>
        /// Approximate number of full cycles the previous cycle's boundary
        /// supply would last at the previous cycle's net rate
        /// (floor(boundaryAmount / |net|)). Null if net is non-negative,
        /// |net| &lt; 0.1 (noise), or the supply outlasts 999 cycles;
        /// 0 means already depleted.
        /// </summary>
        public static int? GetPreviousDeadlineCycles(PreviousCycleStats stats)
        {
            if (stats.Net >= 0f || -stats.Net < 0.1f) return null;
            return FloorCycleCount(stats.BoundaryAmount / -stats.Net);
        }

        // Shared clamp for both deadline helpers: floor, 0 = already depleted,
        // null when the estimate outlasts 999 full cycles.
        private static int? FloorCycleCount(float cycles)
        {
            int n = (int)Math.Floor(cycles);
            if (n < 0) return 0;
            if (n > 999) return null;
            return n;
        }

        // Same guard as the old tracker-based path: an active world with a
        // live world inventory.
        private static bool TryGetActiveInventory(out WorldInventory inventory)
        {
            inventory = null;
            ClusterManager cluster = ClusterManager.Instance;
            if (cluster == null) return false;
            WorldContainer world = cluster.activeWorld;
            if (world == null) return false;
            inventory = world.worldInventory;
            return inventory != null;
        }
    }
}
