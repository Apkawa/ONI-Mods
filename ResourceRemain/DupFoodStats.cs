using System.Collections.Generic;

namespace OxygenNotIncluded.Mods
{
    public static class DupFoodStats
    {
        /// <summary>
        /// All ALIVE standard-model duplicants in the ACTIVE world (bionics eat nothing,
        /// so they are excluded). Returns a NEW list — safe for the caller to iterate
        /// multiple times (once for the total, once per food).
        /// </summary>
        public static List<MinionIdentity> GetAliveStandardDuplicants()
        {
            var result = new List<MinionIdentity>();
            var cluster = ClusterManager.Instance;
            if (cluster == null) return result;
            var dups = Components.LiveMinionIdentities.GetWorldItems(cluster.activeWorldId);
            if (dups == null) return result;
            foreach (var mid in dups)
            {
                if (mid == null) continue;
                if (mid.model == GameTags.Minions.Models.Standard) result.Add(mid);
            }
            return result;
        }

        /// <summary>
        /// How many of the given duplicants may eat a food with the given FoodID
        /// (e.g. <c>Edible.FoodID</c>, "FieldRation"). Uses the game's per-dup ban check
        /// (<c>ConsumableConsumer.IsPermitted</c>). A dup without a ConsumableConsumer
        /// is not permitted.
        /// </summary>
        public static int CountAllowedDuplicants(List<MinionIdentity> dups, string foodId)
        {
            if (dups == null) return 0;
            int count = 0;
            foreach (var mid in dups)
            {
                if (mid == null) continue;
                var consumer = mid.GetComponent<ConsumableConsumer>();
                if (consumer != null && consumer.IsPermitted(foodId)) count++;
            }
            return count;
        }

        /// <summary>
        /// The MINIMUM calories (in kcal, as the game's UI displays them) each standard
        /// duplicant burns per game cycle. Mirrors the game's own calorie-counter
        /// tooltip (<c>MeterScreen_Rations.OnTooltip</c>) — "Duplicants consume a
        /// minimum of N calories each per cycle" — which computes
        /// <c>(0f - MinionIdentity.GetCalorieBurnMultiplier()) * DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE</c>
        /// in internal calorie units; this method returns the same value converted
        /// to kcal (internal units / 1000). The value depends on the current
        /// "Calorie Burn" custom-game setting and is a minimum: traits (e.g.
        /// Calorie Burner) can make a specific duplicant burn more.
        /// </summary>
        public static float GetKcalPerDuplicantPerCycle()
        {
            return (0f - MinionIdentity.GetCalorieBurnMultiplier())
                * TUNING.DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE / 1000f;
        }
    }
}
