// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (Tag, ToolTip, Mathf, GameObject) resolve without extra usings.
using System;
using System.Collections.Generic;
using System.Linq;
using PeterHan.PLib.Core;

namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Extends the calorie-counter tooltip (MeterScreen_Rations.OnTooltip,
    /// MeterScreen_Rations.cs:12-30) with per-cycle survival estimates:
    /// - the header gets "(N cycles for M dups)" right after the available amount;
    /// - each per-food line gets "(n cycles for m dups)" where m counts only the
    ///   duplicants allowed to eat THAT food (ConsumableConsumer.IsPermitted), and
    ///   "(-)" when no live standard duplicant may eat it.
    /// The rebuild reproduces the game's tooltip line-for-line, so if anything fails
    /// the original game tooltip is left untouched — everything is computed BEFORE
    /// <c>Tooltip.ClearMultiStringTooltip()</c> is called, and the whole body is
    /// exception-safe (one PUtil error log at most).
    /// </summary>
    public static class RationsTooltip
    {
        private static bool loggedError;

        /// <summary>
        /// Rebuilds <see cref="meter"/>'s tooltip with the survival estimates.
        /// Never throws; on any failure the game's original tooltip (built by
        /// OnTooltip just before the postfix) is left as-is.
        /// </summary>
        public static void Extend(MeterScreen_Rations meter)
        {
            try
            {
                if (meter == null || meter.Tooltip == null
                    || meter.ToolTipStyle_Header == null || meter.ToolTipStyle_Property == null)
                {
                    return;
                }
                ClusterManager cluster = ClusterManager.Instance;
                WorldContainer? world = cluster != null ? cluster.activeWorld : null;
                WorldInventory? inventory = world != null ? world.worldInventory : null;
                RationTracker tracker = WorldResourceAmountTracker<RationTracker>.Get();
                if (world == null || inventory == null || tracker == null)
                {
                    return;
                }

                // (a) The game's exact call (MeterScreen_Rations.cs:15). The dictionary maps
                // foodId -> UNIT count (RationTracker.GetItemData: units = Edible.Units,
                // RationTracker.cs:41-49); the return value is the TOTAL in internal
                // calorie units (Σ Edible.Calories) — that is the value the game shows
                // as "Calories Available" (1 kcal = 1000 internal units).
                Dictionary<string, float> rations = new Dictionary<string, float>();
                float totalInternal = tracker.CountAmount(rations, inventory);

                // (c) Live standard-model duplicants and the minimum per-dup burn (kcal).
                // GetAliveStandardDuplicants always returns a new non-null list.
                List<MinionIdentity> dups = DupFoodStats.GetAliveStandardDuplicants()!;
                int dupCount = dups.Count;
                float kcalPerDup = DupFoodStats.GetKcalPerDuplicantPerCycle();

                // (d) Overall estimate appended after the available amount.
                string overallEstimate = BuildEstimate(totalInternal, dupCount, kcalPerDup);

                // (f) Header EXACTLY as the game formats it (MeterScreen_Rations.cs:18),
                // with the estimate right after the {0} amount.
                string amountText = GameUtil.GetFormattedCalories(totalInternal);
                if (overallEstimate.Length > 0)
                {
                    amountText += " " + overallEstimate;
                }
                string perDupText = GameUtil.GetFormattedCalories(
                    (0f - MinionIdentity.GetCalorieBurnMultiplier())
                    * TUNING.DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE);
                string header = string.Format(
                    STRINGS.UI.TOOLTIPS.METERSCREEN_MEALHISTORY.ToString(), amountText, perDupText);

                // (g) Same per-food order as the game (MeterScreen_Rations.cs:20-24):
                // descending total food calories (units × CaloriesPerUnit).
                var ordered = rations
                    .OrderByDescending(x => x.Value * (EdiblesManager.GetFoodInfo(x.Key)?.CaloriesPerUnit ?? (-1f)))
                    .ToDictionary(t => t.Key, t => t.Value);

                // (e) Clear LAST — only after every value above is computed, so an abort
                // here still leaves the game's original tooltip intact.
                meter.Tooltip.ClearMultiStringTooltip();

                // (f) Header + spacer, exactly as the game adds them (:18-19).
                meter.Tooltip.AddMultiStringTooltip(header, meter.ToolTipStyle_Header);
                meter.Tooltip.AddMultiStringTooltip("", meter.ToolTipStyle_Property);

                // (g) Per-food lines, exactly as the game formats them (:27), each with
                // the estimate for only the duplicants permitted to eat that food.
                foreach (KeyValuePair<string, float> entry in ordered)
                {
                    EdiblesManager.FoodInfo foodInfo = EdiblesManager.GetFoodInfo(entry.Key);
                    string line;
                    if (foodInfo != null)
                    {
                        // Internal calories exactly as the game displays them:
                        // units × CaloriesPerUnit (MeterScreen_Rations.cs:27).
                        float foodInternal = entry.Value * foodInfo.CaloriesPerUnit;
                        line = foodInfo.Name + ": " + GameUtil.GetFormattedCalories(foodInternal);
                        string estimate = BuildEstimate(
                            foodInternal, DupFoodStats.CountAllowedDuplicants(dups, entry.Key), kcalPerDup);
                        if (estimate.Length > 0)
                        {
                            line += " " + estimate;
                        }
                    }
                    else
                    {
                        line = string.Format(
                            STRINGS.UI.TOOLTIPS.METERSCREEN_INVALID_FOOD_TYPE.ToString(), entry.Key);
                    }
                    meter.Tooltip.AddMultiStringTooltip(line, meter.ToolTipStyle_Property);
                }

                // (h) The game's OnTooltip adds nothing beyond header + spacer + per-food
                // lines (MeterScreen_Rations.cs:12-30) — nothing else to mirror.
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PUtil.LogError("rations tooltip rebuild failed, original tooltip kept: " + e);
                }
            }
        }

        /// <summary>
        /// The "(N cycles for M dups)" estimate for <paramref name="internalCalories"/>
        /// (internal game units, 1 kcal = 1000 units) shared among <paramref name="dups"/>
        /// duplicants each burning <paramref name="kcalPerDup"/> kcal per cycle.
        /// Returns <see cref="CycleText.None"/> ("(-)") when there are no duplicants, and
        /// an empty string when no estimate is possible: calorie burn is disabled
        /// (kcalPerDup &lt;= 0 → infinite) or the result exceeds 999 cycles.
        /// </summary>
        private static string BuildEstimate(float internalCalories, int dups, float kcalPerDup)
        {
            if (dups <= 0)
            {
                return CycleText.None;
            }
            if (kcalPerDup <= 0f)
            {
                return string.Empty;
            }
            int cycles = (int)Math.Floor((internalCalories / 1000f) / (kcalPerDup * (float)dups));
            if (cycles > 999)
            {
                return string.Empty;
            }
            return CycleText.ForDups(cycles, dups);
        }
    }
}
