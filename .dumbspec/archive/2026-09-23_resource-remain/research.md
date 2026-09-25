# 2026-09-23_resource-remain: research

Superficial context collection for the ResourceRemain mod.

## Questions

- [x] R1: Per-cycle production/usage stats per resource — where does the game track them?
- [x] R2: Right-side "Resources" panel UI (favorites + "Show all") — classes, resource entry widget, tooltip mechanism.
- [x] R3: Top calorie counter UI — class, existing tooltip text ("each dup eats 500 kcal/cycle"), available calories data source.
- [x] R4: kcal per dup per cycle by game mode; how food permissions/bans per dup work; alive dup count.
- [x] R5: SizeInTooltip mod (this repo) — tooltip patch pattern and reuse of existing localized strings.
- [x] R6: Repo mod structure — how a new mod is added (template, sln, PLib, i18n docs, build output).

## Findings

### R1 — per-cycle production/usage stats

- **No per-resource production/consumption ledger exists.** The game tracks only the **total available amount** per resource via `ResourceTracker` (a `WorldTracker`/`Tracker`, one per world+Tag), ring buffer of `DataPoint` (max 750), sampled every frame by singleton `TrackerTool` (50 trackers/frame; paused while game paused).
  - Files: `lib_sources/Assembly-CSharp/Tracker.cs`, `WorldTracker.cs`, `ResourceTracker.cs`, `TrackerTool.cs`, `DataPoint.cs`, `WorldInventory.cs`.
- Accessors (public):
  - `TrackerTool.Instance.GetResourceStatistic(int worldID, Tag tag)` → `ResourceTracker` (nullable for unknown tags).
  - `Tracker.GetCurrentValue()`, `GetDelta(float secondsAgo)` (change over last N **game-seconds**), `GetAverageValue/GetMinValue/GetMaxValue`, `ChartableData(float periodLength)` (Tuple<time,value>[]), `GetDataTimeLength()`, `GetCompressedData()`.
  - **1 cycle = 600 game-seconds** (`GameClock.AddTime`). "Last cycle" ≈ `GetDelta(600f)`.
  - Current amount (exactly what the panel shows): `ClusterManager.Instance.activeWorld.worldInventory.GetAmount(tag, includeRelatedWorlds:false)` (available = total − reserved for errands); `GetTotalAmount`, reserved via `MaterialNeeds.GetAmount(tag, worldId, …)`.
- **The Resources panel trend is derived from the amount history**: sparkline = `ChartableData(3000f)` (5 cycles), redrawn every 4 sim-seconds (`ResourceEntry.Sim4000ms`).
- **A tooltip already exists on resource rows**: `ResourceEntry.OnToolTip` computes `GetDelta(150f)` and shows `UI.RESOURCESCREEN.TREND_TOOLTIP` ("INCREASED/DECREASED … in the last cycle"). Same pattern in `ResourceCategoryHeader.RefreshChart()/OnTooltip`. NOTE: 150 game-seconds = 1/4 cycle despite the "last cycle" wording. (User reported "no tooltip at all" — need R2 to find which widget lacks it: probably the pinned/favorites panel or conditional display.)
- **Food kcal is the only explicit produced/consumed tracker**: `WorldResourceAmountTracker<RationTracker>` (`RationTracker.Get()`), `Frame {amountProduced, amountConsumed}`, `currentFrame`/`previousFrame` (per **day**, reset on NewDay), `RegisterAmountProduced` (from `Edible.OnCraft`), `RegisterAmountConsumed` (from `RationMonitor.OnEatComplete`), `amountsConsumedByID`.
- **Units**: `GameUtil.MeasureUnit {mass, kcal, quantity}`; `GameUtil.GetFormattedByTag(tag, amount, TimeSlice, …)`; tag→unit mapping via `GameTags.DisplayAsCalories`/`DisplayAsUnits`/`MaterialCategories`; also `AllResourcesScreen.Instance.units` (Tag→MeasureUnit, populated when that screen spawns); `TimeSlice.PerCycle` appends "/cycle" suffix (`UI.UNITSUFFIXES.PERCYCLE`). Display name `tag.ProperName()`.
- Caveats: `WorldInventory.accessibleAmounts` refreshed incrementally (few frames stale); `TrackerTool.Instance` nullable before spawn; element/gas tags have no pickupables (tracker stays 0) but the panel only shows category/prefab tag sets.

### R2 — Resources panel UI & tooltip mechanism

- **Favorites (right side panel) = `PinnedResourcesPanel`** (`Assembly-CSharp/PinnedResourcesPanel.cs`), `KScreen`, singleton `.Instance`, `IRender1000ms` (refresh every sim-second). Rows = instantiations of `public GameObject linePrefab`, wrapped in nested data class `PinnedResourcesPanel.PinnedResourceRow` (public fields: `gameObject, icon, nameLabel, valueLabel, pinToggle, notifyToggle, newLabel, Tag Tag`). Rows created in `CreateRow(Tag)`; rows dict is private. **No code-side tooltip** (`rg ToolTip` → 0 matches).
- **"Show all" screen = `AllResourcesScreen`** (`Assembly-CSharp/AllResourcesScreen.cs`), `ShowOptimizedKScreen`, `ISim1000ms` (labels) + `ISim4000ms` (sparklines). Rows = **private** nested `AllResourcesScreen.ResourceRow : ScreenRowBase` (refs `availableLabel, totalLabel, reservedLabel, sparkLayer, pinToggle, notificiationToggle`) and `CategoryRow`; created in `SpawnCategoryRow(Tag, GameUtil.MeasureUnit)` (line ~282) from `resourceLinePrefab`; stored in **private** `Dictionary<Tag, ResourceRow> resourceRows`. `public Dictionary<Tag, GameUtil.MeasureUnit> units`. **No code-side tooltip** on rows (only baked `PIN_TOOLTIP`/`UNPIN_TOOLTIP` keys on toggles, likely serialized `ToolTip` components on the prefabs).
- **`ResourceEntry`/`ResourceCategoryHeader`** belong to the *management* Resources screen (`ResourceCategoryScreen`), NOT the side panel / show-all screen. `ResourceEntry.OnToolTip()` (private, lines 177–188) builds: name + `UI.RESOURCESCREEN.AVAILABLE_TOOLTIP` (available/reserved/total) + trend line from `TrackerTool…GetDelta(150f)` with `UI.RESOURCESCREEN.TREND_TOOLTIP` / `TREND_TOOLTIP_NO_CHANGE`. So the "no tooltip" the user sees is exactly on the side panel & show-all rows.
- **ToolTip mechanism (this build):** `ToolTip : KMonoBehaviour` (`Assembly-CSharp-firstpass/ToolTip.cs`): `public Func<string> OnToolTip`, `public ComplexTooltipDelegate OnComplexToolTip` (`delegate List<Tuple<string, TextStyleSetting>>`), `UseFixedStringKey/FixedStringKey`, `forceRefresh`, `refreshWhileHovering`, `SetSimpleTooltip/AddMultiStringTooltip/ClearMultiStringTooltip/RebuildDynamicTooltip/UpdateWhileHovered`. `OnComplexToolTip` only used when `OnToolTip == null`. Rendered by `ToolTipScreen` singleton (`Assembly-CSharp-firstpass/ToolTipScreen.cs`): `SetToolTip`, `defaultTooltipHeaderStyle/defaultTooltipBodyStyle`, `MarkTooltipDirty`, `HotSwapTooltipString` (per existing line index only — changing line count mid-hover requires re-hover or `MarkTooltipDirty`).
- **Runtime AddComponent<ToolTip>() caveat:** no `OnAdded` lifecycle hook; hover enter/exit still works (EventSystem handlers); tooltip won't auto-clear on click unless subscribed manually.
- **Precedents for multi-line dynamic tooltips:** `DateTime.BuildTooltip` (`OnComplexToolTip` = append `List<Tuple<string,TextStyleSetting>>`, spacer `(" ", null)`, header/body styles from `ToolTipScreen.Instance`); `KIconToggleMenu.ToggleInfo` (runtime `tooltip.OnComplexToolTip` on a toggle's ToolTip); `SaveGame.GetColonyToolTip()`.
- **Recommended patch points:** postfix `PinnedResourcesPanel.CreateRow(Tag)` (row = return value, `Tag` known) and `AllResourcesScreen.SpawnCategoryRow(Tag, MeasureUnit)` (new rows created inside); tag rows with own component or dictionary to identify resource in a global tooltip handler. Probe `row.GetComponent<ToolTip>()` at runtime before adding (a row may already carry serialized ToolTips; `ToolTip.OnPrefabInit` errors on >1).
- **Refresh cadence:** (a) `Render1000ms` → `Refresh`/`RefreshLine` per sim-second (value label only on change); (b) `Sim1000ms` → `RefreshRows`, `Sim4000ms` → `RefreshCharts`.

### R3 — Top calorie counter tooltip

- **Widget: `MeterScreen_Rations`** (`Assembly-CSharp/MeterScreen_Rations.cs`), base `MeterScreen_ValueTrackerDisplayer` (public fields `Label (LocText)`, `Tooltip (ToolTip)`, `diagnosticGraph`, `ToolTipStyle_Header`, `ToolTipStyle_Property`; abstract `InternalRefresh()`, `protected abstract string OnTooltip()`), hosted in `MeterScreen` (top bar, `IRender1000ms`, `public MeterScreen_ValueTrackerDisplayer[] valueDisplayers`, singleton `MeterScreen.Instance`). Scene object name: `Rations`.
- **Current tooltip = `OnTooltip()` (protected override)**, wired via `Tooltip.OnToolTip = OnTooltip` in base `OnSpawn`. It:
  - `rationsDict = RationTracker.CountAmount(rationsDict, activeWorld.worldInventory)` (live scan of `WorldInventory.GetPickupables(GameTags.Edible)`, skips `StoredPrivate`, sums `Edible.Calories` = Units × `EdiblesManager.FoodInfo.CaloriesPerUnit`).
  - `Label.text = GameUtil.GetFormattedCalories(calories)`.
  - `Tooltip.ClearMultiStringTooltip()`, then `AddMultiStringTooltip(string.Format(UI.TOOLTIPS.METERSCREEN_MEALHISTORY, calories, perDupKcal), ToolTipStyle_Header)` + spacer + per-food lines `"{foodInfo.Name}: {kcal}"` (per-food breakdown ALREADY exists; keys are `Edible.FoodID` → `EdiblesManager.GetFoodInfo(id).Name`).
- **String keys:** `STRINGS.UI.TOOLTIPS.METERSCREEN_MEALHISTORY = "Calories Available: {0}\n\nDuplicants consume a minimum of {1} calories each per cycle"`; `METERSCREEN_INVALID_FOOD_TYPE = "Invalid Food Type: {0}"`.
- **Per-dup kcal/cycle formula** (inline in the tooltip): `(0f - MinionIdentity.GetCalorieBurnMultiplier()) * DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE`. Base `DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE = -1_000_000f` (1000 kcal, `TUNING/DUPLICANTSTATS.cs:272`). `MinionIdentity.GetCalorieBurnMultiplier()`: custom-game setting `CustomGameSettingConfigs.CalorieBurn` (Disabled 0 / Easy 0.5 / Default 1 / Hard 1.5 / VeryHard 2); Survival → "Default" (1.0 → 1000), Nosweat → "Easy" (0.5 → 500). It is a MINIMUM (traits can add more). Same formula in `FoodDiagnostic`, `FOOD.FOOD_CALORIES_PER_CYCLE`.
- **Internal calorie units:** 1 kcal = 1000 "calorie units"; `GameUtil.GetFormattedCalories` divides by 1000 + suffix (`UI.UNITSUFFIXES.CALORIES.*`).
- **Proven patch pattern** (Peter Han `FoodTooltip`, `lib_sources/peterhaneve_ONIMods/FoodTooltip/FoodTooltipPatches.cs:93–105`): `[HarmonyPatch(typeof(MeterScreen_Rations), "OnTooltip")]` postfix, then `__instance.Tooltip.AddMultiStringTooltip(line, __instance.ToolTipStyle_Property)`. Return value of `OnTooltip` is ignored for multi-string tooltips.
- **Refresh:** labels per render-second (`Render1000ms`); tooltip rebuilt on hover-enter; while hovered only if `forceRefresh`/`refreshWhileHovering` (0.2 s throttle, `ToolTip.UpdateWhileHovered`); changing line count mid-hover needs re-hover/`MarkTooltipDirty`. Per-cycle updates: set `Tooltip.forceRefresh = true` at load, or subscribe `GameHashes.NewDay` (631075836) + `ActiveWorldChanged` (1983128072) and `Tooltip.RebuildDynamicTooltip()` / cache strings.
- `RationTracker.Get()` exposes `currentFrame`/`previousFrame` (`Frame{amountProduced, amountConsumed}`) — per-day kcal produced/consumed (reset on NewDay; produced via `Edible.OnCraft`, consumed via `RationMonitor.OnEatComplete`).

### R4 — Alive dups & food bans

- **Food bans are per-duplicant**: `ConsumableConsumer` (component on MinionIdentity) has `[Serialize] public HashSet<Tag> forbiddenTagSet` (tags = FoodID strings) + `public HashSet<Tag> dietaryRestrictionTagSet` (immutable per-model; bionics = all Edible forbidden).
  - **The predicate: `public bool IsPermitted(string consumable_id)`** (also `IsDietRestricted`, `SetPermitted`, `consumableRulesChanged` Action).
  - `ConsumerManager.instance.DefaultForbiddenTagsList` — only seeds NEW dupes (the "Default" row in Management → Consumables screen `ConsumablesTableScreen`); not a live global ban.
  - AI path: `ClosestEdibleSensor` → `Game.Instance.fetchManager.FindEdibleFetchTarget(Storage, forbiddenTagSet, new[]{GameTags.Edible})` — exclusion = `exclude_tags.Contains(pickup.PrefabTag)`.
  - Food tag == FoodID string by construction (`EntityTemplates.ExtendEntityToFood`; `EdiblesManager.GetFoodInfo(id)` maps IDs, strips "Compost" suffix).
  - Stored dupes: `StoredMinionIdentity.IsPermittedToConsume(string)` (cryo keeps its own `forbiddenTagSet`).
- **Alive dups API:** `Components.LiveMinionIdentities` (`Cmps<MinionIdentity>`: `.Items` all worlds, `.GetWorldItems(worldId, checkChildWorlds, filter)`, `.Count`); `Components.LiveMinionIdentitiesByModel` keyed by model tag; `ClusterManager.Instance.activeWorldId` / `.activeWorld`. Standard (eating) dups: model tag `GameTags.Minions.Models.Standard` (bionics `MAX_CALORIES = 0`, never eat).
- **Per-dup calorie burn is NOT scaled by bans** — a dup with food banned still burns the full rate (starves). Burn = model base (`DUPLICANTSTATS.GetStatsFor(model).BaseStats.CALORIES_BURNED_PER_CYCLE`, standard −1_000_000 units = 1000 kcal) + custom-setting delta (`MinionIdentity.GetCalorieBurnMultiplier()` is the UI-level approximation used by `FoodDiagnostic`/`MeterScreen_Rations`) + trait deltas (`CaloriesDelta` attribute).
- **Colony-level food availability is ban-agnostic**: `RationTracker.CountAmount` (skips only `StoredPrivate`), `ColonyRationMonitor` (out-of-rations iff no Edible with `UnreservedFetchAmount > 0`), `FoodDiagnostic` (demand = alive dupes × `3000 × multiplier`, supply = `KCalTracker.GetAverageValue(150)`). No existing "some dup can eat this" filter — the mod must iterate dups and call `IsPermitted(foodId)` itself.
- `RationTracker.amountsConsumedByID` — per-FoodID consumed ledger (registered from `RationMonitor.OnEatComplete`); `GetAmountConsumedForIDs(List<string>)`.

### R5 — SizeInTooltip mod (pattern reference)

- Location: `SizeInTooltip/` in this repo (single `Mod.cs`, 154 lines + csproj). Repo root `README.md` mod list includes it.
- **Build:** `net48`, `<IsMod>`, `<GenerateMetadata>`, `<IsPacked>` (packs UtilLibs+PLib into mod dll), `ProjectReference ..\UtilLibs`. `Directory.Build.props` imported automatically (do NOT re-import explicitly in csproj). Game refs publicized via BepInEx.AssemblyPublicizer 0.4.3 (build;contentfiles only); game `0Harmony.dll`; ILRepack 2.0.45; Release → `bin/`; `bin/mod.yaml`+`mod_info.yaml` generated from csproj props.
- **Patch style:** NO `[HarmonyPatch]` attributes — programmatic `PatchUtil.TryPatch(harmony, typeof(T), nameof(M), paramTypes, "label", prefix/postfix: ...)` from `UtilLibs/PatchUtil.cs` (reflection by name+params; graceful skip with `PUtil.LogWarning` on game-build rename). `UserMod2.OnLoad(Harmony)`: `base.OnLoad(harmony)` first, then patches, then `PUtil.LogDebug("Build <date>...")`.
- **Naming/defensive patterns:** patch class `<TargetType>_<TargetMethod>_<Feature>__Patch`; namespace `OxygenNotIncluded.Mods` (unqualified game types resolve); patch body: null guards + early return, whole body in try/catch with one-shot `loggedError` + `PUtil.LogError` (never throw into game loop); verbose logs in `#if DEBUG`; PLib auto-prepends `[PLib/{assembly}]`.
- **String reuse:** `string.Format(UI.TOOLS.TOOL_AREA_FMT.ToString(), w, h, w*h)` — game `STRINGS` static `LocString` fields hold the CURRENT localized value at runtime (`Localization.OverloadStrings` overwrites them via selected .po). No own keys, no shipped .po; zero hardcoded RU/EN text (a «РАЗМЕР:» header was dropped for exactly that reason).
- **Repo i18n recipe** (`docs/i18n.md`): own strings = static `LocString` fields in a class in `namespace STRINGS` + `Localization.RegisterForTranslation(typeof(MOD))` in OnLoad; single lang → `<mod>/strings/ru.po` (msgctxt `STRINGS.MOD.FIELD`, `{N}`/`<link>` must match 1:1); multi-lang → `translations/<code>.po` selected by `Localization.GetLocale()?.Code`.
- **Pitfalls:** `HoverTextDrawer.DrawText` has no newline handling (split `\n` manually); protected game members need publicized ref; `strings/` loads ALL .po (last wins); `DLLLoader` loads every *.dll in mod folder → keep deps packed (`IsPacked`).
- Note: `HoverTextConfiguration`/`HoverTextDrawer` are the HOVER-TEXT (card) system (tools etc.) — different from the `ToolTip`/`ToolTipScreen` system (R2/R3). Resource-row tooltips use the `ToolTip` system, so SizeInTooltip's drawer-prefix trick does NOT apply to them; its reusable parts are the PatchUtil/defensive/i18n patterns.

### R6 — Repo structure for a new mod

- **No `UpdatedOniTemplate/` exists in the repo** (docs-only artifact). Clone the minimal mod `SizeInTooltip/` (csproj + Mod.cs + README.md + preview.png) as the starting structure.
- **csproj** (per mod, everything else inherited from `Directory.Build.props`): `PackageId`, `Version`, `TargetFramework=net48`, `AssemblyName`/`RootNamespace=$(PackageId)`, `IsMod=true`, `GenerateMetadata=true`, `IsPacked=true`, `ModName`, `ModDescription`, `WorkshopItemId=0`, Release `OutDir=bin`, `ProjectReference ..\UtilLibs` (needed for PLib/PatchUtil). Do NOT import `Directory.Build.props` explicitly (auto-imported).
- **staticID** is generated: Release → `$(AssemblyName)`, Debug → `$(AssemblyName)_dev` + " [DEBUG]" title.
- **ONI-mods.sln** currently lists 6 projects (BuildDoorOverWall, UtilLibs, SizeInTooltip, ReplaceBuildingMaterial, BestBuildDryWall, SpaceOverlay) — repo docs claiming only 2 are stale. Add = `Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ResourceRemain", "ResourceRemain\ResourceRemain.csproj", "{new-GUID}"` + `EndProject` before `Global`, plus 4 `{GUID}.Debug|Any CPU.ActiveCfg/Build.0` + Release lines in `GlobalSection(ProjectConfigurationPlatforms)`. No comments allowed in sln.
- **bin/ output:** Debug → `bin/Debug/net48/` (packed dll, pdb, mod.yaml, mod_info.yaml); Release → `bin/` (+ README.txt, workshop_build.vdf). **CopyModsToDevFolder** (Directory.Build.targets, AfterTargets=ILRepack) auto-copies to `$(ModFolder)= $(SolutionRoot).tmp/build_mod_dir/<Name>_<dev|release>/`. Enable only ONE flavor (dev XOR release) per mod.
- **Root README.md** mod table: two columns `Mod` (relative link `<Mod>/<Mod>.README.md`) | `Description` (one line, English). New row: `| [ResourceRemain](ResourceRemain/README.md) | ... |`.
- **Directory.Build.props** injects: SolutionRoot + props.user (GameLibsFolder=`$(HOME)/ONI/dlls`, ModFolder), game refs (Assembly-CSharp[-firstpass] publicized, game 0Harmony, UnityEngine), publicizer (IsMod), ILRepack 2.0.45 + BBCode tool (IsPacked), net48 refassemblies (non-Windows), WarningsAsErrors=CS0618,CS0612, TargetGameVersion=Aquatic (731233), SupportedContent=ALL, APIVersion=2, Author=Apkawa.
- All repo mod READMEs: English, mod-readme skill template, changelog appended per release (AGENTS.md rule).

### R7 — 'MISSING' instead of '(-)'

**Question:** Why does the calorie-counter tooltip (`MeterScreen_Rations` postfix) show literal `MISSING` where it should show `(-)` — the `NONE` placeholder used for food banned for all alive dups?

**The `NONE` definition + every usage site:**
- **Definition** — `ResourceRemain/Strings.cs:18`, in `namespace STRINGS { public class RESOURCE_REMAIN { … } }`:
  ```csharp
  public static LocString NONE = "(-)";   // localized "(-)" placeholder
  ```
  - Intended key (if registered): `STRINGS.RESOURCE_REMAIN.NONE`; default text `"(-)"`.
  - The initializer `"(-)"` is a **string**, converted via `LocString`'s implicit operator → `new LocString("(-)")` → `_text = "(-)"`, `_key = default(StringKey)` (`String = null`, `Hash = 0`).
- **Usage 1** — `ResourceRemain/Strings.cs:75-78`, `CycleText.None` getter:
  ```csharp
  public static string None
  {
      get { return STRINGS.RESOURCE_REMAIN.NONE.ToString(); }
  }
  ```
- **Usage 2** — `ResourceRemain/RationsTooltip.cs:145`, `BuildEstimate(...)` returns `CycleText.None` when `dups <= 0`:
  ```csharp
  if (dups <= 0) { return CycleText.None; }   // RationsTooltip.cs:143-146
  ```
- **Callers of `BuildEstimate`** (both end up at the `NONE` path when no allowed dups):
  - `RationsTooltip.cs:64` — header: `overallEstimate = BuildEstimate(totalInternal, dupCount, kcalPerDup)` (→ `(-)` when `dupCount == 0`, i.e. no alive standard dups).
  - `RationsTooltip.cs:105-106` — per-food: `estimate = BuildEstimate(foodInternal, DupFoodStats.CountAllowedDuplicants(dups, entry.Key), kcalPerDup)` (→ `(-)` when that food is banned for **all** alive dups).
- The **only** mod-local `LocString` left in the mod is `NONE`. All plural wording now uses **game** keys (`STRINGS.UI.FORMATDAY`, `STRINGS.DUPLICANTS.STATS.SUBJECTS.*`, `STRINGS.UI.TOOLTIPS.*`), which the game registers, so those render fine. (`ResourceDelta.cs:48` `GameUtil.TimeSlice.None` is an unrelated enum.)

**How the game renders "MISSING":**
- `LocString.ToString()` (`lib_sources/Assembly-CSharp/LocString.cs:49-52`) returns `Strings.Get(key).String` — it **never** uses its own `_text`.
- `Strings.Get(key)` (`lib_sources/Assembly-CSharp-firstpass/Strings.cs:25-33`) → `RootTable.Get(key)` (single-level hash lookup `Entries[key.Hash]`); if the key is absent → `GetInvalidString(key)`.
- `GetInvalidString` (`Strings.cs:9-23`) builds `"MISSING" + "." + key.String` and records it in `invalidKeys`. With the unregistered key (`key.String == null`) this yields **`"MISSING."`** (the trailing dot is from the empty key string) — what the UI shows as the `MISSING` placeholder.
- A key only ever reaches `RootTable` through `LocString.CreateLocStringKeys` (`LocString.cs:69-101`, specifically `Strings.Add(text2, text3)` at :88-93), which is invoked by `Localization.RegisterForTranslation` (`lib_sources/Assembly-CSharp/Localization.cs:511-520`).

**Root cause (confirmed via git):** the mod **no longer registers** `STRINGS.RESOURCE_REMAIN` for translation.
- Before, `Mod.OnLoad` called `Localization.RegisterForTranslation(typeof(STRINGS.RESOURCE_REMAIN))` and shipped `ResourceRemain/strings/ru.po` (plan.md:40).
- Fix round **`1304a36`** ("fix: row tooltip hover via MultiToggle prefix + game-key localization") **removed** `ru.po`, `ModAssets/`, and the `RegisterForTranslation` call, keeping only `NONE` (spec.md:91: "removed `ru.po`/`ModAssets/`/`RegisterForTranslation`, kept only `NONE`").
- With no registration, `NONE`'s key stays `default(StringKey)` and is never added to the `Strings` table → `ToString()` hits the `MISSING` fallback.
- The game does **not** auto-register mod assemblies: `KMod/DLLLoader.LoadDLLs` (`KMod/DLLLoader.cs:50-136`) only finds the `UserMod2` subclass per assembly and calls `OnLoad`; it never calls `RegisterForTranslation`/`CreateLocStringKeys`. `Localization.CollectLocStringTreeRoots` (`Localization.cs:165-170`) only walks the one assembly stored in `translatable_assemblies`, which only contains an assembly if the mod explicitly called `AddAssembly`/`RegisterForTranslation`.
- The `.po` mechanism is irrelevant to the fix: `Localization.OverloadStrings` (`Localization.cs:522-591`) only rewrites a `LocString` field's `_text` (never the `Strings` table) and only for assemblies in `translatable_assemblies` (excludes the mod). So a `.po` alone would NOT have fixed `NONE`; it only worked before because of the `RegisterForTranslation` call (which populates the table with the default `"(-)"`).

**Shipped `.po` check:**
- No `.po` / `ModAssets` under the current `ResourceRemain/` source.
- `.tmp/build_mod_dir/ResourceRemain_dev/strings/ru.po` is a **stale artifact** (dated 02:06; mod sources modified 11:06). It maps `msgctxt "STRINGS.RESOURCE_REMAIN.NONE"` `msgid "(-)"` `msgstr "(-)"` plus obsolete keys (CYCLE_*/DUP_*/FOR_DUPS) that no longer exist. It does not reflect the current mod and, even if loaded, would not help (see `.po` mechanism above).

**Minimal change (facts only — NOT implemented):**
- `(-)` is language-independent punctuation, so it does not need localization. Stop routing it through `LocString`/`Strings`:
  - `Strings.cs:18` → `public const string NONE = "(-)";` (plain string), and
  - `Strings.cs:75-78` → `CycleText.None` returns `STRINGS.RESOURCE_REMAIN.NONE` directly.
  - No `Strings.Get`, no registration, no `.po` required → UI shows `(-)`.
- (Heavier alternative: re-add `Localization.RegisterForTranslation(typeof(STRINGS.RESOURCE_REMAIN))` in `Mod.OnLoad` so the key lands in the table; the plain-string approach above is more minimal and matches the "hardcoded punctuation default" note.)

**Unknowns / open points:**
- Exact displayed string: analysis says `"MISSING."` (trailing dot from the null key string); the report phrases it as `MISSING`. The trailing dot was not confirmed against a live in-game screenshot/log — could confirm in `~/ONI/logs/Oxygen Not Included/Player.log` (`invalidKeys`) if needed.
- Whether any other future mod-local `LocString` re-introduced without registration would hit the same bug (only `NONE` exists today).

### R8 — tooltip missing for some resources

**Observation being explained:** on one asteroid, no tooltip on the Aluminium Ore row while Copper Ore shows one; on other asteroids the Aluminium Ore tooltip works. (Facts only; no source modified.)

#### R8.1 — The exact guard chain that decides whether the row tooltip line is produced

Chain (top → bottom), all in the mod:

1. **Row creation** — Harmony postfixes attach the callback (Mod.cs:29 pinned `PinnedResourcesPanel.CreateRow(Tag)`, Mod.cs:39 all-resources `AllResourcesScreen.SpawnCategoryRow(Tag, MeasureUnit)`):
   - Pinned postfix (Mod.cs:83-91): `ResourceRowTooltip.Attach(__result.gameObject, tag)` — tag is the row's tag from the active world's pinned/notify/new-discovery set (PinnedResourcesPanel.cs:176-195).
   - All-resources postfix (Mod.cs:131-161): walks `__instance.resourceRows` (declared AllResourcesScreen.cs:100) and attaches each unattached `Tag` exactly once.
2. **`ResourceRowTooltip.Attach`** (ResourceRowTooltip.cs:90-131) sets on the row's ToolTip:
   - if the ToolTip already had an `OnToolTip` delegate (`old != null`): `tip.OnToolTip = () => { baseText = old() ?? ""; extra = BuildTooltipLine(tag); if (IsNullOrEmpty(extra)) return baseText; return IsNullOrEmpty(baseText) ? extra : baseText + "\n" + extra; }` (ResourceRowTooltip.cs:104-117) — **when no line, returns the base string only**.
   - else (fresh ToolTip, the normal case for these rows): `tip.OnToolTip = () => BuildTooltipLine(resourceTag);` (ResourceRowTooltip.cs:120) — **when no line, returns `string.Empty`**. Also sets `tip.refreshWhileHovering = true` (:103) so the callback re-runs every 0.2 s while hovered (ToolTip.cs:273-288).
3. **`ResourceRowTooltip.BuildTooltipLine`** (ResourceRowTooltip.cs:43-80) — returns `string.Empty` in three cases:
   - `!ResourceDelta.TryGetLastCycleStats(...)` → `return string.Empty;` (:47-50)
   - **noise guard**: `if (s.Produced < 0.05f && s.Consumed < 0.05f) return string.Empty; // unchanged last cycle — no line.` (:51-54)
   - any exception → `return string.Empty;` (one-shot warning log, :71-79)
4. **`ResourceDelta.TryGetLastCycleStats`** (ResourceDelta.cs:21-42) — returns `false` in four cases:
   - `TrackerTool.Instance == null || ClusterManager.Instance == null` (:24)
   - `ResourceTracker tracker = TrackerTool.Instance.GetResourceStatistic(ClusterManager.Instance.activeWorldId, tag); if (tracker == null) return false;` (:25-26) — **always the ACTIVE world, whatever asteroid the panel row belongs to** (both panels only ever show the active world: PinnedResourcesPanel.cs:136/:176, AllResourcesScreen.cs:438/:515/:589/:616).
   - `Tuple<float, float>[] points = tracker.ChartableData(600f); if (points == null || points.Length < 2) return false;` (:27-28)
   - `WorldInventory inventory = ClusterManager.Instance.activeWorld.worldInventory; if (inventory == null) return false;` (:38-39)
   - Otherwise it sums per-point steps over the window: `step = points[i].second - points[i-1].second; step>0 → produced += step else consumed += -step` (:29-34) and sets `stats.Available = inventory.GetAmount(tag, includeRelatedWorlds:false)` (:40).

So the mod's line is absent exactly when: no tracker for (active world, tag) — **or** fewer than 2 history points — **or** the available amount changed by less than 0.05 in total (absolutely) between consecutive samples inside the 600 s window (noise guard). Note there is no time-based "series too short" guard beyond `points.Length < 2`; the 600 s window silently shrinks to whatever the buffer holds (see R8.3).

#### R8.2 — Empty-string `OnToolTip` ⇒ tooltip is fully hidden by the game

- `ToolTip.OnPointerEnter` (ToolTip.cs:207-211) → `OnHoverStateChanged(true)` (:250-263) → `ToolTipScreen.Instance.SetToolTip(this)` (:256).
- `ToolTipScreen.SetToolTip` (ToolTipScreen.cs:51-56) → `ConfigureTooltip()`:
  - `tooltipSetting.RebuildDynamicTooltip()` (ToolTipScreen.cs:70) — in `RebuildDynamicTooltip` (ToolTip.cs:182-205): clears the list, calls `OnToolTip()`, and **only adds the string if `!string.IsNullOrEmpty(text)`** (:187-191). Empty ⇒ `multiStringCount == 0`.
  - `if (tooltipSetting.multiStringCount == 0) { clearMultiStringTooltip(); }` (ToolTipScreen.cs:71-74) — destroys all label children.
  - `bool flag = multiTooltipContainer.transform.childCount != 0; toolTipWidget.SetActive(flag);` (ToolTipScreen.cs:80-81) — **zero lines ⇒ the whole tooltip widget is deactivated ⇒ nothing visible.**
- Whitespace-only string is *not* suppressed by `IsNullOrEmpty`; and the incubation blank check `toolTipIsBlank = (component2.text == null || component2.text == "") && toolTipIsBlank` (ToolTipScreen.cs:239) also doesn't treat `" "` as blank — so whitespace-only would show an effectively empty tooltip box. The mod returns true `string.Empty`, so the widget is hidden entirely.
- Side effect of `refreshWhileHovering`: while continuously hovering, `UpdateWhileHovered` (ToolTip.cs:273-288) only hot-swaps existing label children (`HotSwapTooltipString`, ToolTipScreen.cs:256-262, guarded by `childCount > lineIndex`) and never destroys/recreates lines — so if the line count changes mid-hover (1↔0), the container keeps the previously rendered text until pointer exit/re-enter.

#### R8.3 — Tracker creation and per-world data facts (game side)

**Creation** (`TrackerTool.cs`):
- `TrackerTool.Instance` is set in `OnSpawn` (TrackerTool.cs:21-24); `null` before that.
- On spawn, trackers are created for every existing world (`ClusterManager.Instance.WorldContainers`, :25-28); for **every later world**, `ClusterManager.Instance.Subscribe(-1280433810, Refresh)` (:34) with `-1280433810 == GameHashes.WorldAdded` (GameHashes.cs:443; fired at ClusterManager.cs:558) → `AddNewWorldTrackers(worldID)` (:53-57).
- `AddNewWorldTrackers` (TrackerTool.cs:70-141) creates, **per world, unconditionally (independent of current inventory amounts)**:
  - fixed world trackers (Stress/KCal/Idle/Breathability/PowerUse/Battery/Crop/WorkingToilet/Radiation, :72-80), per-chore-group WorkTime+ChoreCount (:90-94), AllChoresCount+AllWorkTime (:95-96);
  - a `ResourceTracker` for every tag in `GameTags.CalorieCategories` (1: Edible) + every prefab carrying that tag (:97-104); same for `UnitCategories` (14 tags, :1047-1051) (:105-112), `MaterialCategories` (18 tags, :1055-1059) (:113-120), `OtherEntityTags` (3 tags, :1067) (:121-128); plus `CookingIngredient`-tagged prefabs (:129-132), every food type (`EdiblesManager.GetAllFoodTypes()`, :133-136), and **every element** (`ElementLoader.elements`, :137-140).
- `AddResourceTracker` dedupes per (world, tag) (:143-149).
- `GetResourceStatistic(worldID, tag)` (TrackerTool.cs:151-154) is a LINQ `Find` — **returns null iff no ResourceTracker exists for that (world, tag) pair**. The tag set is identical for all worlds in a save, so "null for Al on one world but not Cu on the same world" cannot arise from creation logic.
- **A world with zero of a resource still gets its tracker**, and its data stays 0: `ResourceTracker.UpdateData` (ResourceTracker.cs:11-17) adds `worldInventory.GetAmount(tag, includeRelatedWorlds:false)` per sample — 0.0 when the world holds none. Only if `worldInventory == null` is no point added at all (:13-16).

**Sampling rate / buffer** (`TrackerTool.cs`, `Tracker.cs`, `DataPoint.cs`):
- `TrackerTool.Update()` (Unity per-frame, TrackerTool.cs:176-208): skips entirely when `SpeedControlScreen.Instance.IsPaused || !trackerActive` (:178-181); otherwise round-robins exactly **`numUpdatesPerFrame = 50` (:19) world trackers per frame** (:199-207). Each `UpdateData` call appends one `DataPoint` (`Tracker.AddPoint`, Tracker.cs:159-168) whose `periodStart` = previous point's `periodEnd` (first point: current time), `periodEnd` = `GameClock.Instance.GetTime()`, value = current available amount (DataPoint.cs:3-14).
- Ring buffer capacity: `maxPoints = Mathf.CeilToInt(750f)` (Tracker.cs:15); oldest points trimmed in `AddPoint` (:166-167). **`dataPoints` is not serialized** → every save load starts an empty buffer per tracker.
- So each tracker is sampled once every `N_total/50` frames (N_total = all world trackers across all worlds ≈ a few hundred per world × world count — roughly 900-1700 for a 2-4 asteroid cluster, i.e. **~0.3-0.6 game-seconds between samples at 1×, 60 fps; the span of the full 750-point buffer is ≈ 750 × that ≈ ~200-400 game-seconds**, i.e. **less than one 600 s cycle** — estimate, scales with world/prefab/element counts).
- **Immediately after the resource appears in the world** (world spawn or save load): 0 points at t=0; first point after the tracker's first `UpdateData` (≤ ~0.6 s); `ChartableData(600f)` then has exactly **1 point** (Tracker.cs:27-37 falls back to the single last point) → mod guard `points.Length < 2` fails → **no tooltip line**. A second point lands one sample interval later (~0.3-0.6 s). A *visible* line additionally needs the noise guard to pass (change ≥ 0.05 between consecutive samples in-window) — so for a resource whose amount actually changes, the line appears within roughly one sample interval (~≤1 game-second at 1×) of the change; for a resource that never changes (0→0), **no line ever appears**, no matter how long the world has existed.
- `ChartableData(600f)` on an established static resource returns the whole buffer (up to 750 points), all with equal values → Produced=Consumed=0 → noise guard suppresses the line.

**Why the game sparkline uses `ChartableData(3000f)` despite the ~750-point buffer** (no time-compression; the window is just wider than the data):
- `AllResourcesScreen.RefreshCharts` (AllResourcesScreen.cs:583-637): `num = 3000f`; `axis_x.min_value = time - 3000`, `axis_x.max_value = array[^1].first` (:596/:602, :623/:629) — the x-axis window is fixed at 5 cycles; points keep their **absolute game-time** x. `GraphBase.GetRelativePosition` (GraphBase.cs:41-50) maps absolute→relative via `axis_x.min_value/max_value`. Since every buffered point is within the last ~200-400 s (< 3000 s), `ChartableData(3000f)` returns the **entire buffer** and the sparkline line only fills the rightmost part of the 5-cycle window.
- `LineLayer.RefreshLine` (LineLayer.cs:138-214) does optional **point-count decimation only** (`compressDataToPointCount = 256`, :48, `DropValues`/`Average` modes, :142-201) — it never extends the time span; kept points retain their original times.
- `Tracker.GetCompressedData()` (Tracker.cs:170-188, 10 time-buckets, time-weighted average) has **zero callers** in Assembly-CSharp (dead API in this build); `standardSampleRate = 4` / `defaultCyclesTracked = 5` (Tracker.cs:7-9) are dead constants (only referenced by a third-party mod via reflection, peterhaneve_ONIMods FastTrack). Same pattern in the management screen: `ResourceEntry.RefreshChart` uses `ChartableData(3000f)` (ResourceEntry.cs:280) and `SparkLayer.ScaleToData` (SparkLayer.cs:141-162) can scale the axis to the data's own min/max x when `scaleWidthToData` is set — again no time stretching.

#### R8.4 — Most likely root cause (observation, evidence-based)

The symptoms match the mod's **own noise guard** (ResourceRowTooltip.cs:51-54), not a missing tracker:
- The tracker for (active world, Al-ore tag) exists on every world identically (R8.3 creation logic — same tag list per world; `GetResourceStatistic` null only for tags never created, which would be world-independent). A fresh-world "<2 points" cause is ruled out by the observation itself: Copper Ore on the same asteroid shows a tooltip, so that world's trackers already have ≥2 points and are being sampled normally.
- On the affected asteroid, Aluminium Ore's available amount evidently did not change by ≥0.05 between any two consecutive samples inside the 600 s window (nothing mined/stored/used there recently), while Copper Ore did → `TryGetLastCycleStats` returns `true` with `Produced=Consumed=0` → `BuildTooltipLine` returns `string.Empty` → `RebuildDynamicTooltip` leaves `multiStringCount == 0` → `ToolTipScreen.ConfigureTooltip` hides the widget (R8.2) → no visible tooltip.
- On other asteroids, Al is actively changing (mined/refined) → Produced/Consumed ≥ 0.05 → line shown. Both panels read the active world (R8.1), so "per asteroid" differences are exactly per-active-world tracker-data differences.
- Secondary (same mechanism, rarer): right after a save load or a newly revealed asteroid, **every** row on that world lacks the line until ≥2 samples exist (~≤1 game-second at 1×) — but that would suppress Copper too, so it doesn't fit the reported case.

#### R8.5 — Unknowns

- Exact per-world tracker count in the user's save (world count, prefab/element count) — the 0.3-0.6 s sample interval and ~200-400 s buffer span are estimates from the 50-per-frame budget; the in-game symptom is unaffected by the exact values.
- Whether the affected asteroid truly has zero Al movement in the window vs. tiny (<0.05) churn — not distinguishable from outside the game; both produce the identical empty-line path.
- The mid-hover stale-text behavior (R8.2) means a line that vanishes while continuously hovered may linger visually until re-hover — could confuse in-game verification of the fix.
- Not verified: whether the row root prefab ships a serialized `ToolTip` with `UseFixedStringKey` (if so, the mod's `OnToolTip` overrides the fixed text entirely — including the empty case — because `RebuildDynamicTooltip` only honors `OnComplexToolTip` when `OnToolTip == null`, ToolTip.cs:184-204). No code-side tooltip was found on these rows (R2), so this is considered unlikely.

### R9 — post-save-load fake delta

#### R9.1 — Save-load sequence (top level)

- **Entry:** `SaveLoader.OnSpawn` (`Assembly-CSharp/SaveLoader.cs:240-282`). If an active save file exists and is loadable (`WorldGen.CanLoad`):
  1. `Sim.SIM_Initialize` + `SimMessages.CreateSimElementsTable/CreateDiseaseTable` (:245-247), `loadedFromSave = true` (:248), then `Load(activeSaveFilePath)` (public, :903-976 → private `Load(IReader)` :346-453).
  2. Private `Load(IReader)` order: `Sim.LoadWorld(reader2)` (:418) → `Sim.Start()` (:424) → **`SceneInitializer.Instance.PostLoadPrefabs()`** (:425) → `saveManager.Load(reader)` (:427) → `Game.Instance.Load(deserializer)` (:440) → `ClusterManager.Instance.InitializeWorldGrid()` (:442).
- **In-game reload:** `LoadScreen.DoLoad(path)` (used e.g. from `SaveLoader.UpgradeActiveSaveDLCInfo`, SaveLoader.cs:1412-1416) — same `Load(string)` path under a `LoadingOverlay`.
- `SaveLoader.loadedFromSave` is a public `bool { get; private set; }` (SaveLoader.cs:199) — true once a save load succeeded. **No public "just loaded" event** is fired by SaveLoader itself.
- `WorldManager.cs` does not exist in this build (world management lives in `ClusterManager`/`WorldContainer`/`SceneInitializer`).

#### R9.2 — Tracker buffer & DataPoint facts

- **Buffer is never serialized**: `Tracker.dataPoints` is a plain `protected List<DataPoint>` (Tracker.cs:13); `Tracker` is a plain (non-KMono) class, `DataPoint` is a plain struct (`periodStart, periodEnd, periodValue`, DataPoint.cs:1-15). No `[Serialize]` anywhere in the chain; `Tracker` exposes no `Save/Load`. ⇒ **every save load starts with an empty buffer per tracker**; no pre-load points carry over. There is no `ClearData`; only `OverwriteData(List<DataPoint>)` (Tracker.cs:190-193), which has **zero callers** in Assembly-CSharp/firstpass.
- **Clock = `GameClock.Instance.GetTime()`** — absolute *game time* in game-seconds (`timeSinceStartOfCycle + cycle*600`, GameClock.cs:138-141), NOT SimulationInstance time. `AddPoint` (Tracker.cs:159-168): first point's `periodStart = GetTime()`, otherwise previous `periodEnd`; `periodEnd = GetTime()`; NaN→0; trimmed to `maxPoints = 750`.
- **GameClock survives load**: `cycle`, `timeSinceStartOfCycle`, `frame`, `time`, `timePlayed` are all `[Serialize]` (GameClock.cs:21-37); `OnDeserialized` (GameClock.cs:52-61) recomputes `cycle`/`timeSinceStartOfCycle` from absolute `time` and zeroes `time`. So after a load, `GameClock.GetTime()` already equals the saved absolute game time and keeps advancing (via `Sim33ms(dt) → AddTime(dt)`, GameClock.cs:63-66, sim-tick driven — 1 cycle = 600 game-seconds, `NewCycle` 631075836 fired per cycle, GameClock.cs:82-92). **A mod can capture `timeAtLoad = GameClock.Instance.GetTime()` on the first frame after load.**
- `ChartableData(period)` (Tracker.cs:17-40) falls back to the **single last point** when nothing is in the window, and to `(0f,0f)` when the buffer is empty — never throws.
- `GetDelta(secondsAgo)` (Tracker.cs:139-157) **returns 0f when the buffer has < 2 points**.
- **Sampling driver:** `TrackerTool.Update()` (Unity `Update`, TrackerTool.cs:176-208) — round-robins **50 world trackers per frame** (numUpdatesPerFrame, :19), **skipped entirely while `SpeedControlScreen.Instance.IsPaused`** (:178-181). `ResourceTracker.UpdateData` (ResourceTracker.cs:11-17) adds `worldInventory.GetAmount(tag, includeRelatedWorlds:false)` when `worldInventory != null` — **no other guard**: it samples whatever the inventory reports, including the early 0s.

#### R9.3 — Pause/time state after load; who unpauses

- **Sim ticks are driven by Unity frame time, so they stop while paused.** `Game.Update()` (plain Unity `Update`, Game.cs:1376-1402) reads `Time.deltaTime` (:1381) and feeds `SimEveryTick(deltaTime)` (:1398), which accumulates `simDt` and runs 1/60-s sim sub-ticks (Game.cs:1404-1431). Paused ⇒ `Time.timeScale = 0` (set in `SpeedControlScreen.OnChanged`, SpeedControlScreen.cs:265-283, and in `Game.OnSpawn`, Game.cs:939) ⇒ `Time.deltaTime = 0` ⇒ no sim ticks ⇒ `GameClock` (ISim33ms, GameClock.cs:10) does not advance and `ResourceTracker` gets no `UpdateData` (additionally explicitly gated: `TrackerTool.Update` returns while `SpeedControlScreen.Instance.IsPaused`, TrackerTool.cs:178-181).
- **After a save load the game is PAUSED, and nothing in the decompiled code auto-unpauses on the load path.** `Game.OnSpawn` (Game.cs:923-985) unconditionally does `SpeedControlScreen.Instance.Pause(playSound:false)` (:934) and `Time.timeScale = 0f` (:939). The only auto-`Unpause` call sites in Assembly-CSharp are: `NewBaseScreen` (new-colony intro only — spawned from Game.OnSpawn :944-951 only when `SaveLoader.Instance.Cluster != null`, i.e. world-gen path, NOT plain save loads) and `SpeedControlScreen.DebugStepFrame` (dev stepping, SpeedControlScreen.cs:322-334). `SpeedControlScreen.OnSpawn` (SpeedControlScreen.cs:98-106) restores `speed = SaveGame.Instance.GetSpeed()` but does NOT unpause. `LoadScreen.OnDeactivate` (LoadScreen.cs:891-895) unpause is null-guarded and only runs in the frontend scene, where `SpeedControlScreen.Instance` is null. ⇒ On the save-load path the game stays paused until the user presses the pause/play button (`Action.TogglePause`, `SpeedControlScreen.OnKeyDown`, SpeedControlScreen.cs:291-314).
- **`Game.isLoading` is not a useful post-load flag.** Private bool (Game.cs:599); `SetIsLoading()` (Game.cs:1350-1353) is only called from `LoadScreen.ForceStopGame()` (LoadScreen.cs:1241-1245) when loading while a game is already running (`Game.Instance != null` — the "new game from pause menu" path); never reset in decompiled code (a fresh `Game` object in the new backend scene starts with false). Public getter `IsLoading()` at Game.cs:1355-1358.
- **`Game.IsPaused`** is a private bool (Game.cs:568) synced in `Game.LateUpdate` (Game.cs:1523-1540) to `Time.timeScale == 0` — no public getter. The PUBLIC pause-state signal is the **`PauseChanged` event = `-1788536802`** (GameHashes.cs:104), fired with `Boxed<bool>` on every pause-state flip in `Game.LateUpdate` (Game.cs:1531-1540) — a mod can subscribe and learn the first unpause.
- **Load events/flags (who fires when):**
  - `Game.Instance.OnLoad` — public `Action<GameSaveData>` (Game.cs:388), invoked after `GameSaveData` deserialization inside `Game.Instance.Load` (Game.cs:1791-1794). `Game.Instance.Load` runs at SaveLoader.cs:440 — AFTER `saveManager.Load` (SaveLoader.cs:427). Since `GameClock` is `ISaveLoadable` (GameClock.cs:10) and therefore deserialized during `saveManager.Load`, **inside the `OnLoad` callback `GameClock.Instance.GetTime()` already equals the restored absolute game time** — a reliable `timeAtLoad` capture point, settable by a mod in its `UserMod2.OnLoad` (mods load before any save loads).
  - `SaveGameReady` = `-1917495436` (GameHashes.cs:224) — fired in `SaveGame.OnSpawn` (SaveGame.cs:324-328). `SaveGame` is `ISaveLoadable` (SaveGame.cs:14) ⇒ on save loads it is deserialized during `saveManager.Load`, so its `OnSpawn` fires on the frame after the load completes (KMonoBehaviour defers `OnSpawn` to Unity `Start()`, KMonoBehaviour.cs:108-142). `SceneInitializer.NewSaveGamePrefab()` (SceneInitializer.cs:52-58) is only the new-game path (SaveLoader.cs:1067-1068). Exact Start-ordering between SaveGame and Game on that frame is Unity-internal (unverifiable from code).
  - `BaseAlreadyCreated` = `-1992507039` and `StartGameUser` = `-838649377` (GameHashes.cs:105/:225) — fired in `Game.OnSpawn` (:954-959) only when `SaveLoader.Instance.loadedFromSave` — i.e. during `PostLoadPrefabs` (SaveLoader.cs:425), BEFORE `saveManager.Load` (:427), so **before GameClock is restored** — unsuitable for timeAtLoad.
  - `GameHashes.Loaded` = `-1594984443` and `Saved` = `1589904519` (GameHashes.cs:3-4) are **defined but never triggered anywhere** in Assembly-CSharp (verified by search).
  - `SaveLoader.Instance.loadedFromSave` — public bool (SaveLoader.cs:199, set at :248) — still true afterwards, usable as a "this session came from a save" check.
  - `PerformanceCaptureMonitor.FinishedLoadingSave()` (PerformanceCaptureMonitor.cs:139-149) — one-frame coroutine started at Game.cs:1795; only measures load time/memory, fires no event.
- **No "worlds loaded" event exists.** `ClusterManager.RegisterWorldContainer` (ClusterManager.cs:102-105) adds to `m_worldContainers` silently; `WorldAdded` (-1280433810) is only fired by `CreateRocketInteriorWorld` (ClusterManager.cs:558, rocket interiors); `WorldRemoved` (-1078710002) by `UnregisterWorldContainer` (ClusterManager.cs:107-111). Loaded worlds get their trackers solely because `TrackerTool.OnSpawn` (TrackerTool.cs:21-36) walks `ClusterManager.Instance.WorldContainers` at spawn.
- `KMonoBehaviour` lifecycle for ordering: `OnPrefabInit` in Awake, `OnSpawn` in Unity `Start()` (KMonoBehaviour.cs:108-142) — prefabs instantiated mid-frame spawn on the NEXT frame; `KMonoBehaviour.isLoadingScene` static (KMonoBehaviour.cs:14) is set false in `SceneInitializerLoader.Awake` (backend scene start).

#### R9.4 — WorldInventory fill-in after load (exact chain)

- `WorldInventory` is a component of the world container: `WorldContainer.worldInventory = GetComponent<WorldInventory>()` (WorldContainer.cs:260, :454). It is `[SerializationConfig(OptIn)]` (only `pinnedResources`/`notifyResources` are `[Serialize]`) — **`Inventory` and `accessibleAmounts` are empty right after a load.**
- **Population chain:** worlds are deserialized inside `saveManager.Load(reader)` (SaveLoader.cs:427) → `WorldInventory.OnSpawn` starts the `InitialRefresh` coroutine (WorldInventory.cs:127-130) → after 1 frame it calls `ReachabilityMonitor.Instance.UpdateReachability()` on **every Pickupable** (WorldInventory.cs:132-149) → `ReachableChanged` fires for those whose reachability changed → the `FetchableMonitor` SM (NOT serialized: `SerializeType.Never`, FetchableMonitor.cs:78 ⇒ recreated in its default `unfetchable` state on load) transitions to `fetchable` (FetchableMonitor.cs:93-95) → `RegisterFetchable()` → `Game.Instance.fetchManager.Add` + `Game.Instance.Trigger(AddedFetchable)` (FetchableMonitor.cs:18-22) → `WorldInventory.OnAddedFetchable` adds the pickupable to `Inventory` (WorldInventory.cs:310-345).
- `ReachabilityMonitor` also runs a continuous batched `UpdateReachability` every SIM_1000ms (ReachabilityMonitor.cs:24, :46) — but sim ticks stop while paused, so during the post-load pause only the `InitialRefresh` path and live events refresh reachability.
- **`WorldInventory.Update()` (WorldInventory.cs:266-303) is a plain Unity `Update` — NOT pause-gated.** It round-robins **one tag per frame**: finds the `Inventory` entry at index `accessibleUpdateIndex`, sums `TotalAmount` of its Pickupables (same world, excluding `StoredPrivate`, :279-285), writes `accessibleAmounts[key] = num3` (:296), advances the index (:297). `accessibleAmounts` starts empty; `GetTotalAmount` uses `TryGetValue` (WorldInventory.cs:156-161) ⇒ **any tag whose turn has not come yet returns 0.**
- `GetAmount(tag, includeRelatedWorlds:false)` = `GetTotalAmount - MaterialNeeds.GetAmount(tag, worldId)`, clamped ≥ 0 (WorldInventory.cs:186-199) — exactly what `ResourceTracker.UpdateData` samples (ResourceTracker.cs:11-17).
- **Vanilla knows the counts are invalid until the first full sweep:** the `hasValidCount` block (WorldInventory.cs:286-295) fires `PinnedResourcesPanel.Instance.ClearExcessiveNewItems()` + `Refresh()` when the first full round-robin completes (and only for the active world). The flag itself is private.
- **Zero-window timing:** 1 tag/frame at 60 fps ⇒ ~60 tags per real second, independent of game speed. A world with N tags in `Inventory` ⇒ ~N/60 real seconds until every tag is non-zero. In game-seconds the window is N/60 at 1x speed and N·S/60 at speed S (WorldInventory.Update counts frames, not sim time). So if the user unpauses shortly after load (typical), the first few tracked seconds contain 0s for tags not yet round-robined; the subsequent 0→real jump lands in the left edge of the mod's 600-game-second window and is summed as fake "produced" = the real amount (matches the reported +100500-unit artifact). If the user stays paused longer than the fill-in takes, the artifact shrinks/disappears — consistent with the artifact being intermittent/size-dependent.

#### R9.5 — Vanilla UI behavior for the same artifact (no guard)

- `ResourceEntry` (top-left bar / pinned panel row): `Sim4000ms` (ResourceEntry.cs:270-272) → `RefreshChart()` (:275-280) → `LineLayer.RefreshLine(resourceStatistic.ChartableData(3000f), "resourceAmount")` — **no post-load check**. Tooltip trend: `GetDelta(150f)` (ResourceEntry.cs:182) — **no post-load check**.
- `ResourceCategoryHeader.RefreshChart` (ResourceCategoryHeader.cs:336-341): same `ChartableData(3000f)`, **no guard**.
- So the vanilla sparkline shows the same 0→jump spike after a load, and the vanilla trend tooltip shows the fake "+X" delta, until the early zero-points are pushed out of the 750-point buffer (≈200-400 game-seconds at 1x). The only "guards" are `GetDelta` returning 0 for <2 points (Tracker.cs:139-157) and the `ChartableData` fallbacks (Tracker.cs:17-40). The `hasValidCount` refresh (WorldInventory.cs:286-295) is the only vanilla reaction to inventory fill-in completing.

#### R9.6 — Candidate filter mechanisms (facts only, no final design)

1. **Capture `timeAtLoad`.** Feasible via plain assignment: `Game.Instance.OnLoad += data => { timeAtLoad = GameClock.Instance.GetTime(); }` in `UserMod2.OnLoad` (Game.cs:388, fires at :1791-1794; GameClock already restored by then — ISaveLoadable, deserialized during `saveManager.Load`, SaveLoader.cs:427 < :440). `GameClock.GetTime()` is the absolute game-seconds clock that also drives the tracker points (Tracker.cs:159-168 uses `GameClock.Instance.GetTime()`), so the comparison is in the same time base. No Harmony needed. Alternative signals: `SaveGameReady` (SaveGame.cs:327; ordering vs OnLoad is same-frame Start order, unverified) and `PauseChanged` (-1788536802, Boxed<bool>, Game.cs:1531-1540) for detecting the first unpause. `BaseAlreadyCreated` is NOT usable for the time (fires before GameClock is restored).
2. **Ignore stats until `GameClock.GetTime() > timeAtLoad + X`.** Feasible — the mod recomputes stats on each tooltip refresh (~0.2 s, ToolTip.cs:273-288 refreshWhileHovering), so a time gate is a one-line check. X must exceed the zero-window: ~N/60 real seconds (N = tag count in `WorldInventory.Inventory`, runtime value), i.e. ~1.7 s for 100 tags, ~5 s for 300 tags, at 1x speed (longer in game-seconds at higher speed: N·S/60).
3. **Skip the first big positive step.** Feasible in code — `ResourceDelta.TryGetLastCycleStats` already sums per-step deltas (ResourceDelta.cs:29-34), so dropping the first positive step is trivial. Facts limiting it: the buffer is empty at every load (Tracker.cs:13, never serialized) so the first step is unambiguous; but the fake jump and the first genuine cycle's production live in the SAME window, and no sign/magnitude signature distinguishes them — only their timing relative to load does. A flat "skip first positive step" filter would also discard legitimate production in the first cycle.
4. **Wait until the tracker has ≥ N game-seconds of post-load history.** Feasible: `Tracker.GetDataTimeLength()` (Tracker.cs:42-50) returns the buffer span (last `periodEnd` − first `periodStart`); since the buffer is empty at load, any span is purely post-load **until the buffer fills to 750 points** (≈200-400 game-seconds), after which the span no longer reflects "time since load". The first point's time is also directly readable (first element of `ChartableData`, Tracker.cs:17-40) — it equals the first sampling after unpause (≈ load + unpause delay), usable as a proxy anchor.
5. **Clamp per-step delta.** Feasible trivially (same per-step loop), but there is no canonical "max plausible per-sample production" to calibrate against — large pumps/factories can legitimately move 10s of kg per sample; a fixed clamp would corrupt large worlds.
6. **Rewrite the buffer post-load.** `Tracker.OverwriteData(List<DataPoint>)` (Tracker.cs:190-193) is public and has zero callers in vanilla — a mod could seed/overwrite each resource tracker's buffer (e.g. inject a flat baseline at the current real amount once `WorldInventory` has a valid count) after load, with no Harmony. `hasValidCount`'s existence (WorldInventory.cs:286-295) shows the game itself only considers inventory counts valid after the first full round-robin sweep.

#### R9.7 — Unknowns / not verifiable from code

- Exact `OnSpawn` ordering among Game / TrackerTool / SaveGame / WorldInventory on the first post-load frame (all deferred to Unity `Start()`, KMonoBehaviour.cs:108-142; Start order is Unity-internal). Which of them are scene `preloadPrefabs` vs `prefabs` is set in the backend scene asset, not in code (only `NewSaveGamePrefab` for new games is explicit, SaveLoader.cs:1067-1068).
- Whether a save load from the main menu ever auto-unpauses: no auto-unpause found in decompiled Assembly-CSharp for the load path (only NewBaseScreen for new colonies and DebugStepFrame dev tool) — expected to require the user's pause/play press; the observed artifact implies sampling starts soon after that press.
- Exact `WorldInventory.Inventory` tag count per world (determines the zero-window length; runtime data only).
- `SaveGameReady` vs `Game.OnLoad` exact order on the load path (same-frame Start ordering).
- Whether the vanilla sparkline visually shows the left-edge jump (code + buffer semantics say it must, but no in-game verification was done in this research).
- `MaterialNeeds` state right after load (reserved amounts for restored errands) — affects the magnitude of the first real value, not the mechanism.

### R10 — previous-cycle stats + cycle string keys

**Question:** add a "previous cycle" line to the resource-row tooltip. Mockup (RU):
```
Текущий цикл: +800шт (-200шт +1000шт)
Предыдущий цикл: -800шт (-200шт +1000шт), Циклов: 100
```
1 cycle = 600 game-seconds; the ", Циклов: N" deadline suffix shows only when that cycle's net is negative.

#### R10.1 — Tracker history facts (file:line)

- `Tracker.dataPoints`: `protected List<DataPoint>` ring buffer, `maxPoints = Mathf.CeilToInt(750f)` (Tracker.cs:13, :15); never serialized (R9.2) — every save load starts empty.
- `DataPoint` struct: `{float periodStart; float periodEnd; float periodValue}` (DataPoint.cs:3-14). **Contiguous**: `AddPoint` (Tracker.cs:159-168) sets the new point's `periodStart` = previous point's `periodEnd` (first point: current time); value = current available amount, sampled at `AddPoint` time, i.e. at `periodEnd`. So a tuple `(periodStart, value)` pairs a start time with a value measured one sample later — the plotted series is step-shifted, but **differences between consecutive values are exact changes between consecutive sample instants** (what the mod already sums, ResourceDelta.cs:29-34).
- `ChartableData(float periodLength)` (Tracker.cs:17-40): captures `time = GameClock.Instance.GetTime()` internally; walks points newest→oldest while `!(periodStart < time - periodLength)` — **points with `periodStart >= time - period` are included (boundary start == cutoff INCLUDED)**; returns the game's `Klei.Tuple<float,float>[]` (first = periodStart, second = periodValue), reversed to oldest→newest. Fallbacks: nothing in window → single array with the newest point; empty buffer → `((0f,0f))`. Never throws.
- `GetDataTimeLength()` (Tracker.cs:42-50): sums `periodEnd - periodStart` over all points = total buffered span (≈ last-sample time − oldest start, since points are contiguous). Public — usable as a runtime "how much history do I have" guard.
- `GetDelta(float secondsAgo)` (Tracker.cs:139-157): `array = ChartableData(secondsAgo)`; returns 0f for < 2 points; `second = array[^1].second` (newest value); loop newest→oldest: `if (time - array[i].first >= secondsAgo) num = array[i].second` with `num` initialized to **−1f**; returns `second − num`. **Quirk (IL-verified against Assembly-CSharp.dll, dump at `.tmp/tracker_il.txt`, and re-simulated with the exact decompiled logic in `.tmp/tracker_sim.py`):** every point in the window has `periodStart >= time - secondsAgo`, so no point can satisfy `time - start >= secondsAgo` (except tick-grid exact alignment) — `num` stays −1 and **in steady state GetDelta returns `value + 1`**, not a real delta. The vanilla trend tooltip (`ResourceEntry.cs:182`, `GetDelta(150f)`) therefore shows ≈ (current amount + 1). **Do not use GetDelta for window deltas in the mod** — keep the existing consecutive-step summation.
- Sampling cadence (R8.3 recap): `TrackerTool.Update()` is a plain Unity per-frame `Update` (TrackerTool.cs:176-208), round-robining `numUpdatesPerFrame = 50` (TrackerTool.cs:19) world trackers per frame, skipped while `SpeedControlScreen.Instance.IsPaused` (TrackerTool.cs:178-181). Per-tracker point period = `N/50` frames, N = total world trackers across ALL worlds (content-dependent, see R10.2). Points are frozen while paused (game time frozen too, so windows don't expire during pause — R9.3).

#### R10.2 — Feasibility of two 600 s windows (1200 s buffer)

- Buffer span in game-seconds = 750 points × (N/50 frames/point) / (FPS frames per game-second) = **15·N/FPS** (at 1× speed). At 60 fps: span ≈ 0.25·N; at 30 fps ≈ 0.5·N. Game speed S scales span down by 1/S (frames/second of real time is constant; game time advances S×).
  - Full 600 s span needs **N ≥ 2400** (60 fps) / ≥ 1200 (30 fps).
  - Full 1200 s span needs **N ≥ 4800** (60 fps) / ≥ 2400 (30 fps).
- N per world (from `AddNewWorldTrackers`, TrackerTool.cs:70-141 + GameTags.cs:1045-1069): ~9 fixed world trackers (+ElectrobankJoules DLC3, +RocketFuel/Oxidizer in rocket interiors) + 2 per chore group + 2 global (AllChoresCount/AllWorkTime) + one `ResourceTracker` per distinct tag across CalorieCategories (1) ∪ UnitCategories (14) ∪ MaterialCategories (18) ∪ OtherEntityTags (3), deduped per (world, tag) (TrackerTool.cs:143-149), plus distinct tags from CookingIngredient-tagged prefabs, every food type ID (`EdiblesManager.GetAllFoodTypes()`), and every element (`ElementLoader.elements`). Order of magnitude: **a few hundred per world** (R8.3 estimated N ≈ 900–1700 for a 2–4-asteroid cluster ⇒ per-world ~300–500).
- **Conclusion: the buffer span is runtime-dependent and generally SHORTER than 1200 game-seconds — and possibly even shorter than 600.** With R8.3's estimate (N ≈ 900–1700, 60 fps) span ≈ 225–425 game-seconds: even the current 600 s "cycle" window is often partially covered, and a full 1200 s two-window view is generally unattainable (would need N ≥ 4800). A single world's tracker count is not statically determinable (depends on loaded prefabs/elements/food types). **The mod must guard at runtime** (span check below) and treat both windows as "best effort".
- **Split algorithm (from one call, no extra APIs needed — there is no offset-window API; `GetDelta` only covers [now−s, now]):**
  1. `points = tracker.ChartableData(1200f)` → `[(t0,v0), …, (tn,vn)]` oldest→newest.
  2. `cutoff = GameClock.Instance.GetTime() - 600f` (drift between this call and ChartableData's internal GetTime is sub-frame, ≤ ~16 ms — negligible for a 600 s boundary).
  3. `j` = largest index with `points[j].first < cutoff` (last point starting strictly before the boundary; j = −1 if none).
  4. **Previous window** = points `0..j` plus bridge sample `v_j` (first point at/after boundary): produced/consumed = sign-sum of consecutive steps across indices `0..j+1`; `prev_net = v_j − v_0` (using the bridge value as the window-end amount; its true sample instant is one period after the boundary — within one sample interval, consistent with the current-window computation).
  5. **Current window** = points `j..n`: produced/consumed = sign-sum of steps across `j..n`; `current_net = v_n − v_j`. The bridge point serves as both the previous window's terminal sample and the current window's initial sample — consistent.
  6. A point exactly on the boundary (`start == cutoff`) is included by ChartableData and lands in the current window (j is the last strictly-before) — consistent.
- **Edge cases:**
  1. **Buffer span < 1200 s** (the common case per above, and always right after load — R9): `ChartableData(1200f)` returns the entire buffer; the previous window is only partially covered. Detect: `span = now - points[0].first < 1200f` (or `tracker.GetDataTimeLength() < 1200f`) → previous line is partial or must be hidden (design decision; see Unknowns).
  2. **No point with `start <= cutoff`** (span < 600 s: fresh load / newly revealed world / new tracker): no previous data at all → hide previous line (current line is partial too — existing behavior of the current implementation).
  3. **Fewer than 2 samples in a window**: no step to sum → produced/consumed 0, net meaningless → hide the line (the existing `points.Length < 2` guard generalizes per-window).
  4. **ChartableData single-point fallback** (idle/fresh tracker, e.g. world whose inventory was null at sample time): array length 1 → same guard.
  5. **Pause**: harmless — game time and buffer both freeze (R9.3); no expiry, no growth.
  6. **High game speed S**: span ≈ 15·N/(FPS·S) — at 8× speed a 60 fps cluster with N ≈ 1500 covers only ~28 game-seconds: both windows are heavily partial. Same as the current single-line behavior (it too silently shrinks to the buffered span, R8.3).
  7. **Do NOT use `GetDelta`** for either window (value+1 quirk, R10.1).
  8. **Post-load zero-fill artifact (R9)** applies to the previous window exactly as it does to the current one: the first 0→real jump lands at the left edge and is summed as fake "produced" — any R9 filter must cover the 1200 s view too.

#### R10.3 — Exact string keys for the cycle labels

RU .po (`~/ONI/game/OxygenNotIncluded_Data/StreamingAssets/strings/strings_preinstalled_ru_klei.po`):

- Lines 101568–101571:
  ```
  #. STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE
  msgctxt "STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE"
  msgid "Last Cycle"
  msgstr "Предыдущий цикл"
  ```
- Lines 101591–101594:
  ```
  #. STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE
  msgctxt "STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE"
  msgid "This Cycle"
  msgstr "Текущий цикл"
  ```
- **No format args and no trailing colon** in either msgid/msgstr — the ": " separator must be added by the caller (vanilla bakes it into the `UPTIME.NAME` template, see below).
- `strings_template.pot:82146–82165` confirms the EN defaults (there is no `strings_preinstalled_en*.po`; English = class defaults): "Last Cycle", "Last {0} Cycles", "Uptime:\n{0}{1}: {2}\n{0}{3}: {4}\n{0}{5}: {6}", "This Cycle".

STRINGS class (`lib_sources/Assembly-CSharp/STRINGS/UI.cs`, nested `public class ELEMENTAL` at :15458, `public class UPTIME` at :15471–15480):
- `STRINGS.UI.ELEMENTAL.UPTIME.NAME = "Uptime:\n{0}{1}: {2}\n{0}{3}: {4}\n{0}{5}: {6}"` (UI.cs:15473)
- **`STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE = "This Cycle"`** (UI.cs:15475)
- **`STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE = "Last Cycle"`** (UI.cs:15477)
- `STRINGS.UI.ELEMENTAL.UPTIME.LAST_X_CYCLES = "Last {0} Cycles"` (UI.cs:15479)

Vanilla usage context (`Assembly-CSharp/AdditionalDetailsPanel.cs:152–163`, element details panel "Uptime" label):
```csharp
string text = UI.ELEMENTAL.UPTIME.NAME;
text = text.Replace("{0}", "    • ");
text = text.Replace("{1}", UI.ELEMENTAL.UPTIME.THIS_CYCLE);   // "Текущий цикл"
text = text.Replace("{2}", GameUtil.GetFormattedPercent(num2 * 100f));
text = text.Replace("{3}", UI.ELEMENTAL.UPTIME.LAST_CYCLE);    // "Предыдущий цикл"
text = text.Replace("{4}", GameUtil.GetFormattedPercent(num3 * 100f));
text = text.Replace("{5}", UI.ELEMENTAL.UPTIME.LAST_X_CYCLES.Replace("{0}", "5"));
text = text.Replace("{6}", GameUtil.GetFormattedPercent(num4 * 100f));
```
Data source: `Operational.GetCurrentCycleUptime() / GetLastCycleUptime() / GetUptimeOverCycles(5)` (AdditionalDetailsPanel.cs:140–151) — per-component cycle **uptime percentages** (a different metric from resource deltas, but the same "current/last cycle" label pair and `label: value` line convention). These are the **only** LAST_CYCLE/THIS_CYCLE keys in the game (rg over Assembly-CSharp + firstpass: only UI.cs:15475/:15477; the unrelated `COLONY_ACHIEVEMENTS.MISC_REQUIREMENTS.STATUS.EXOSUIT_THIS_CYCLE`, STRINGS/COLONY_ACHIEVEMENTS.cs:93, is a different key).

#### R10.4 — FORMATDAY confirmation

- **`STRINGS.UI.FORMATDAY = "{0:F1} cycles"`** (STRINGS/UI.cs:15889 — top-level field of `UI`, not nested).
- RU .po lines 102090–102093: `msgctxt "STRINGS.UI.FORMATDAY"`, `msgid "{0:F1} cycles"`, **`msgstr "Циклов: {0:F1}"`**. (An obsolete commented-out old entry exists at :136065–136067 — `msgid "{0} cycles"`, `msgstr "циклов: {0}"` — inactive.)
- Vanilla usage: `GameUtil.AppendFormattedCycles(StringBuilder, float seconds, bool forceCycles = false)` (GameUtil.cs:1519–1529): when `forceCycles || Math.Abs(seconds) > 100f` → `builder.AppendFormat(UI.FORMATDAY, seconds / 600f)`; otherwise plain time formatting. So FORMATDAY is the game's generic "N cycles" formatter, fed **floats** (game-seconds / 600).
- Mod usage (confirmed working): `ResourceRemain/Strings.cs:54–57`, `CycleText.Cycles(int n) = string.Format(STRINGS.UI.FORMATDAY.ToString(), n.ToString())` — the int is passed as a string, so `{0:F1}` renders it verbatim ("100") → RU "Циклов: 100", EN "100 cycles". **The existing `, Циклов: 100` suffix mechanism works unchanged on a second line**; it is already appended per-line in `BuildTooltipLine` (ResourceRowTooltip.cs).
- Grammar note: "Циклов:" is uninflected for all counts (same as vanilla's usage of this key); small-N plural forms (цикл/цикла/циклов) are not handled by the game key.

#### R10.5 — Neighborhood keys (±40 lines around the UPTIME block, RU .po)

- 101532–101556: `STRINGS.UI.ELEMENTAL.THERMALCONDUCTIVITY.{ADJECTIVES.VERY_HIGH_CONDUCTIVITY ("Extremely Conductive"), ADJECTIVES.VERY_LOW_CONDUCTIVITY ("Highly Insulating"), NAME ("Thermal Conductivity: {0}"), TOOLTIP}`.
- 101558–101566: `STRINGS.UI.ELEMENTAL.UNITS.{NAME ("Stack Units: {0}"), TOOLTIP ("This stack contains {0} units of {1}")}`.
- 101568–101594: `STRINGS.UI.ELEMENTAL.UPTIME.{LAST_CYCLE, LAST_X_CYCLES, NAME, THIS_CYCLE}` — the complete UPTIME block (4 keys, self-contained).
- 101596–101604: `STRINGS.UI.ELEMENTAL.VAPOURIZATIONPOINT.{NAME ("Vaporization Point: {0}"), TOOLTIP}`.
- 101606–101639: `STRINGS.UI.ENDOFDAYREPORT.{ADDED ("Added"), AVERAGE_TIME_DETAILS_HEADER, BASE_DETAILS_HEADER, CALORIES_CREATED.{NAME, NEGATIVE_TOOLTIP, POSITIVE_TOOLTIP}, CHORE_STATUS.NAME}`.
- The UPTIME keys are used only by the element details panel (R10.3) — no resource-UI usage — so reusing them for resource cycle lines is safe (no collision with existing resource text).

#### R10.6 — Implementation-relevant facts (no code written)

- Line construction matching the mockup and the vanilla `label: value` convention:
  - current: `STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE` + `": " + <net sign+amount> + " (-<consumed> +<produced>)"` + optional deadline suffix;
  - previous: `STRINGS.UI.ELEMENTAL.UPTIME.LAST_CYCLE` + same shape + optional `", " + CycleText.Cycles(deadline)` (Strings.cs:54–57, existing) — appended only when that window's net < 0 (existing `GetDeadlineCycles` logic, ResourceDelta.cs:55–61).
- Data: **one** `tracker.ChartableData(1200f)` call + split at `now − 600f` (R10.2); no second API needed.
- Runtime guards: span from `now - points[0].first` (or `GetDataTimeLength()`) — full current line needs span ≥ 600 f, full previous line needs ≥ 1200 f; per-window ≥ 2 samples. N is content- and FPS-dependent, so these checks must happen at runtime (tooltip refresh is 0.2 s, R8.2).
- The deadline on the previous line needs `stats.Available`: the existing `GetDeadlineCycles` uses the current available amount (ResourceDelta.cs:40, :55–61) — reusing it with the previous window's Net is the natural extension (design decision; see Unknowns).

#### R10.7 — Unknowns / open points

- **Exact N (total world trackers) in the user's save** — runtime only; determines whether 600 s / 1200 s spans are actually full. Static estimate: few hundred per world ⇒ 1200 s generally not reachable, 600 s borderline. (A quick in-game log of `TrackerTool` tracker counts, or a runtime PUtil log of `GetDataTimeLength()`, would settle it.)
- **User's render FPS and typical game speed** — both scale the buffered span in game-seconds (span ≈ 15·N/(FPS·S)).
- **Desired behavior when the buffer covers only part of the previous cycle** (span in (600, 1200)): show partial previous stats, hide the line, or label it — design decision for the implementer.
- **RU plural forms** for the ", Циклов: N" suffix at small N — FORMATDAY is uninflected; acceptable if vanilla-parity is the goal.
- Whether the deadline estimate on the previous line should use the current available amount or the amount at the previous cycle's end (the latter is derivable from the split: `v_j`, the bridge value, ≈ amount at boundary) — design decision.
- Not verified in-game: whether the actual span in the user's saves reaches 600 s (R8.3's 225–425 s estimate is N-derived; if the real N is higher, the current line already shows full cycles and 1200 s may be closer than estimated).

## Implementation notes

(2026-09-23, sampler task — new API facts discovered while implementing
`ResourceRemain/CycleStats.cs`; append-only, no earlier text modified.)

#### IN1 — No prefab-collection API in this build

- `Components.Prefabs` and `PrefabCollection` do NOT exist in Aquatic 731233
  (grep across all of `lib_sources/Assembly-CSharp*` + the decomp sets: zero
  matches). The game's prefab registry is `Assets.Prefabs`
  (`public static List<KPrefabID>`) + `Assets.RegisterOnAddPrefab` /
  `Assets.GetPrefab(tag)` (asset-side only).
- Consequence: the per-world sampler is NOT prefab-registered. It is a plain
  `GameObject` (name "ResourceRemainCycleStats") parented under the world
  container's GameObject with `AddComponent<CycleStatsSampler>()`.
  `KMonoBehaviour` auto-handles the rest: `autoRegisterSimRender = true`
  (KMonoBehaviour.cs:20) ⇒ `Start()` → `Spawn()` (KMonoBehaviour.cs:116) adds
  the `ISim1000ms` updater to `SimAndRenderScheduler.instance` and removes it
  in `OnLoadLevel` (KMonoBehaviour.cs:97). No KPrefabID ⇒ the GO is not
  serialized; it is respawned after every load via the registration hooks
  below.

#### IN2 — `GameHashes` is an ENUM

- `public enum GameHashes` (global namespace, GameHashes.cs:1) — not a static
  class of int fields. Event calls need explicit casts:
  `(int)GameHashes.PauseChanged` = -1788536802, `(int)GameHashes.WorldAdded`
  = -1280433810. `KMonoBehaviour.Subscribe(int hash, Action<object>)` /
  `Trigger` / `BoxingTrigger` take ints.

#### IN3 — Publicizer rewrites protected virtual → public virtual

- `KMonoBehaviour.OnSpawn` / `OnLoadLevel` are `protected virtual` in the
  decompiled sources but PUBLIC in the publicized assemblies used at build
  time (AssemblyPublicizer rewrites access). Overrides must be written
  `public override` (CS0507 otherwise).
- Harmony still resolves them: `ReflectionUtil.FindMethod` scans
  Public|NonPublic|DeclaredOnly, so `PatchUtil.TryPatch(harmony,
  typeof(WorldContainer), nameof(WorldContainer.OnSpawn), new Type[0], ...)`
  works for the protected-in-source method.

#### IN4 — `GameSaveData` is nested in `Game`

- `public class GameSaveData` is declared INSIDE `Game` (Game.cs:307). The
  `Game.OnLoad` field is `public Action<Game.GameSaveData>` (Game.cs:388) —
  handler signature must be `void OnGameLoad(Game.GameSaveData data)`.

#### IN5 — `float.IsFinite` does not exist on net48

- `float.IsFinite`/`double.IsFinite` are .NET Core 2.0+; absent from the
  net48 reference assemblies (CS0117). Use
  `!float.IsNaN(x) && !float.IsInfinity(x)`.

#### IN6 — New game never fires `Game.OnLoad`

- `Game.Instance.Load(Deserializer)` (Game.cs:1758, fires `OnLoad` at
  :1791-1793) has exactly ONE call site: `SaveLoader.cs:440` (save load).
- The new-game path (`SaveLoader` ~:1020-1076: `Sim.Load` from new worldgen,
  `OnWorldGenComplete.Signal(m_cluster)`, "NewGame" OniMetrics event) never
  calls `Game.Load`.
- Consequence: T_ref capture is
  - **save load**: `Game.OnLoad` handler sets
    `TRef = GameClock.Instance.GetTime()` (GameClock already restored at
    that point — ISaveLoadable, deserialized during `saveManager.Load` at
    SaveLoader.cs:435, before `Game.Instance.Load` at :440);
  - **new game**: first `GameHashes.PauseChanged` unpause — `Game.LateUpdate`
    (Game.cs:1531-1540) fires `Trigger(-1788536802, BoxedBools.Box(IsPaused))`,
    i.e. a `Boxed<bool>` whose `value == false` on unpause; world gen is
    already complete by then.
  - **safety net**: the sampler's first tick pins T_ref if still unset.

#### IN7 — `WorldAdded` fires once only (rocket interiors)

- Single fire site in this build:
  `ClusterManager.CreateRocketInteriorWorld` →
  `BoxingTrigger((int)GameHashes.WorldAdded, worldContainer.id)` — data is
  `Boxed<int>` (the new WorldContainer id). Fired on the ClusterManager
  object ⇒ subscribe `ClusterManager.Instance.Subscribe(...)`.
- The MAIN world (save load or new game) never fires WorldAdded. The
  main-world sampler therefore comes from a Harmony postfix on
  `WorldContainer.OnSpawn` — the universal spawn hook (every spawned world
  container, main world included, fires it once; the main world is
  instantiated from save/worldgen data before the scene's first frame). The
  WorldAdded subscription is kept (per spec) as a redundant path for rocket
  interiors; the `HashSet<int>` of spawned worlds makes double-spawn a no-op.

#### IN8 — Save-load ordering

- Sequence (SaveLoader.cs): `saveManager.Load(reader)` (:435, instantiates
  the saved world containers — Unity `Start()`/`KMonoBehaviour.Spawn` for
  them fires on the NEXT frame) → `Game.Instance.Load(deserializer)` (:440,
  fires `OnLoad`) → next frame: world container `OnSpawn` postfix runs.
- Consequence: on save load the `Game.OnLoad` handler (T_ref capture +
  static-state reset) runs BEFORE the new world's OnSpawn postfix
  (re-subscription to the new `Game` instance + sampler spawn). The old
  `Game`/world objects are destroyed with the old scene, so
  `subscribedGame` (the cached subscribed Game) is cleared in the OnLoad
  handler and the new Game is (re)subscribed on its first world container's
  OnSpawn.

#### IN9 — Tag-set choice (no `World.resources`)

- `World` (Assembly-CSharp/World.cs) has NO `resources` field/set in this
  build. World identity = `WorldContainer.id` (`[Serialize] public int id`,
  WorldContainer.cs:18); world inventory =
  `WorldContainer.worldInventory` (public property, :260).
- Tag set = union of the keys of `WorldInventory.Inventory` (private
  `Dictionary<Tag, HashSet<Pickupable>>`, WorldInventory.cs:19 — publicized)
  and the keys of `WorldInventory.accessibleAmounts` (private
  `Dictionary<Tag, float>`, :21 — derived from Inventory in
  WorldInventory.Update). Amount per tag:
  `WorldInventory.GetAmount(Tag tag, bool includeRelatedWorlds)` (:186,
  public) = accessibleAmounts − MaterialNeeds, clamped ≥ 0; we call it with
  `includeRelatedWorlds: false`.

#### IN10 — Other confirmed API facts

- `ISim1000ms` (Assembly-CSharp-firstpass) is in the GLOBAL namespace:
  `void Sim1000ms(float dt)`; the dt is the sim step's game-time delta.
- `GameClock.Instance.GetTime()` (GameClock.cs:138) =
  `timeSinceStartOfCycle + (float)cycle * 600f` — absolute game-seconds.
- `Game.Instance` — public static property (Game.cs:679), assigned in
  `Game`'s OnPrefabInit (Game.cs:793); non-null by the time any world
  container spawns. `ClusterManager.Instance` assigned in
  `ClusterManager.OnPrefabInit` (ClusterManager.cs:157);
  `ClusterManager.activeWorld` (public property, :90),
  `ClusterManager.GetWorld(int)` (public, :325),
  `ClusterManager.activeWorldId` (:64).
- `Boxed<T>` is a public CLASS with public `T value` (Boxed.cs);
  `BoxedBools.Box(bool)` returns the shared `Boxed<bool>` True/False
  instances; unbox idiom: `data is Boxed<int> boxed → boxed.value`.
- `KMonoBehaviour.Subscribe(int, Action<object>)` is public; subscriptions
  live on the KMonoBehaviour and die with it (per-Game-instance
  re-subscription handled by `subscribedGame` in `CycleStats.EnsureStarted`).
- `PUtil` (PLib) auto-prefixes `[PLib/{assembly}]`; no mod name in messages;
  `.F(...)` for format strings.

#### IN11 — Registration as implemented (Mod.OnLoad)

- `PatchUtil.TryPatch(harmony, typeof(WorldContainer),
  nameof(WorldContainer.OnSpawn), new Type[0], "cycle stats sampler",
  postfix: WorldContainer_OnSpawn_ResourceRemain__Patch.Postfix)` — postfix
  calls `CycleStats.OnWorldContainerSpawned(wc)` (null-guarded, one-shot
  LogError).
- `CycleStats.EnsureStarted()` called directly from OnLoad too — a no-op at
  mod-load time (`Game.Instance` is null then), kept as belt-and-braces.
- Deviation from the task text: `Components.Prefabs.Create` /
  `PrefabCollection.Register` do not exist (IN1) — plain-GameObject spawn
  instead, per IN1.

#### IN12 — `this.enabled = false` does NOT stop `ISim1000ms` ticks (crash-fix round, 2026-09-25)

- The catch in `CycleStatsSampler.Sim1000ms` now sets `this.enabled = false`
  after the one-shot `PUtil.LogError`, per the task. Verified against the
  decompiled game source that this does NOT actually unregister the tick:
  - `KMonoBehaviour.Start()` (KMonoBehaviour.cs:108-142, via `Spawn()` at
    :116) adds the component to `SimAndRenderScheduler.instance` ONCE at
    spawn; `UpdateBucketWithUpdater.Update` (UpdateBucketWithUpdater.cs:55-83)
    iterates its entries and calls `value.updater.Update(...)` with NO
    `MonoBehaviour.enabled` check, and `Sim1000msUpdater.Update`
    (SimAndRenderScheduler.cs:171-174) just calls `updater.Sim1000ms(dt)`.
  - So after `enabled = false` the scheduler keeps invoking `Sim1000ms`
    every sim-second. The try/catch around the whole body still guarantees
    no exception escapes into the game loop (a deterministic failure just
    re-catches every tick after the one-shot log), but the "disable" is
    best-effort signalling only.
  - If a hard stop were ever needed, the public API is
    `SimAndRenderScheduler.instance.Remove(this)` (SimAndRenderScheduler.cs:333,
    removes from the `sim1000ms` bucket for `ISim1000ms`). NOT added — the
    task specified `enabled = false` exactly; left as a documented option.

#### IN13 — Mono/net48 `Dictionary` version bump confirms the crash root cause

- Confirmed (crash log `.tmp/Player_resource_2.log`): on Mono/.NET Framework
  4.8, `Dictionary<K,V>[existingKey] = value` increments the internal version
  and invalidates any live enumerator → `InvalidOperationException: Collection
  was modified` on the next `MoveNext()`. The finalization `foreach` over
  `Dictionary<Tag, Bucket>` with `tags[kv.Key] = b` inside threw on the first
  tick of every new 600 s cycle.
- Fix shape: `Bucket` is now a CLASS (in-place mutation, no dictionary
  write-back on the hot path); finalization is two phases on the same tick
  (phase 1 collects the boundary-crossed keys into a scratch `List<Tag>`,
  phase 2 mutates the referenced buckets); the sampling step only writes
  `tags[tag] = b` when a bucket is newly created.
