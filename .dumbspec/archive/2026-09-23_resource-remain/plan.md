# 2026-09-23_resource-remain: implementation plan (ResourceRemain)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
There is no test suite in this repo: the "red test" for every stage is the failing/absent state
(no build / no tooltip lines), the "green" is a successful `dotnet build ONI-mods.sln -c Debug`,
and the observable behavior is verified in-game by the user (per AGENTS.md).
This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Branch `2026-09-23_resource-remain`, `draft.md` (raw input)
- [x] Initial spec skeleton + refinement questions answered by user
- [x] Research R1–R6 → `research.md` (per-cycle stats API, panel UI/tooltip mechanism, calorie counter, dup food bans, SizeInTooltip patterns, repo build structure)
- [x] Spec v1 (full format, open questions resolved in review)
- [x] This plan

**Criterion:** all task files exist; every R-question has findings; spec v1 has `Created` + `Changes`.
**Commit:** `docs(spec): add ResourceRemain draft/research/spec/plan` (6175faa)

## Stage 1 — Mod scaffold (builds & loads empty)
- [x] Create `ResourceRemain/`: `ResourceRemain.csproj` (PackageId=ResourceRemain, ModName, ModDescription, net48, IsMod/GenerateMetadata/IsPacked, UtilLibs ProjectReference, Release OutDir=bin, WorkshopItemId=0)
- [x] `Mod.cs`: `UserMod2.OnLoad` (base.OnLoad, PUtil.LogDebug build line, no patches yet) — needed `using HarmonyLib; using KMod; using PeterHan.PLib.Core;` (UserMod2 is `KMod.UserMod2`, Harmony is `HarmonyLib.Harmony`)
- [x] `README.md` placeholder (EN), `preview.png` placeholder (64×64 gray PNG)
- [x] Register project in `ONI-mods.sln` (Project block + 4 config lines, new GUID `0E7B2C57-9AAC-4802-AC50-DECE7EBB5AA2`)
- [x] Build: `NUGET_PACKAGES=… dotnet build ONI-mods.sln -c Debug` → `.tmp/build_mod_dir/ResourceRemain_dev/` contains dll + mod.yaml

**Criterion:** build green; `ResourceRemain_dev/` folder populated (dll, mod.yaml, mod_info.yaml).
**Commit:** `chore: scaffold ResourceRemain mod project`

## Stage 2 — Core: per-cycle deltas, units, dup counts, localization
- [x] `CyclesCalc` (or similar): `tracker.ChartableData(600f)` → produced (Σ positive steps), consumed (|Σ negative steps|), net; available amount via `WorldInventory.GetAmount(tag,false)`; guards (no tracker / no data / |net| < 0.1 / N > 999) — implemented as `ResourceDelta.TryGetLastCycleStats` (game `Tuple<T,U>` uses `.second`; `worldInventory` null-guarded)
- [x] Unit formatting by tag: `GameUtil.GetFormattedByTag(tag, amount, GameUtil.TimeSlice.None)` (TimeSlice is nested in GameUtil); `ResourceDelta.GetDeadlineCycles` (null if net ≥ 0 or > 999 cycles)
- [x] Dup helpers (`DupFoodStats`): `GetAliveStandardDuplicants` (`Components.LiveMinionIdentities.GetWorldItems(activeWorldId)`, filter `mid.model == GameTags.Minions.Models.Standard` — model is a public Tag field on MinionIdentity, not PrefabTag); `CountAllowedDuplicants(dups, foodId)` via `mid.GetComponent<ConsumableConsumer>().IsPermitted(foodId)`
- [x] kcal-per-dup-per-cycle via game formula: `DupFoodStats.GetKcalPerDuplicantPerCycle()` = `(0f − MinionIdentity.GetCalorieBurnMultiplier()) × TUNING.DUPLICANTSTATS.STANDARD.BaseStats.CALORIES_BURNED_PER_CYCLE / 1000f` (kcal)
- [x] `STRINGS.RESOURCE_REMAIN` class (namespace STRINGS) + `ru.po` + `Localization.RegisterForTranslation(typeof(STRINGS.RESOURCE_REMAIN))` in OnLoad; keys CYCLE_ONE/FEW/MANY, DUP_ONE/FEW/MANY, FOR_DUPS, NONE; helper `CycleText` (ru/EN plural rule)
- [x] Build green — **plus fix:** `.po` must reach the game-side mod folder; build pipeline copies only `ModAssets/**` → moved to `ResourceRemain/ModAssets/strings/ru.po` (lands at `<mod>/strings/ru.po`, verified in `.tmp/build_mod_dir/ResourceRemain_dev/strings/ru.po`)

**Criterion:** build green; helpers compile with publicized game refs; ru.po loads (no TRANSLATION ERROR in log on load — verified in game).
**Commit:** `feat: ResourceRemain per-cycle stats helpers and localization`

## Stage 3 — Resource row tooltips (pinned panel + show-all screen)
- [x] Postfix `PinnedResourcesPanel.CreateRow(Tag)` (private, verified `PinnedResourcesPanel.cs:143`; returns nested `PinnedResourceRow` with public `gameObject`): row → `GetComponent<ToolTip>()` (AddComponent if missing), `refreshWhileHovering = true`. Implemented as shared `ResourceRowTooltip.Attach(GameObject, Tag)`. **Adaptation:** used string-based `OnToolTip` (not `OnComplexToolTip`) and *wrap* any prefab-supplied `OnToolTip` (append our line with `\n`) — OnComplexToolTip is ignored by the game when OnToolTip is non-null, so wrapping is the only way to coexist.
- [x] Postfix `AllResourcesScreen.SpawnCategoryRow(Tag, MeasureUnit)` (private, `AllResourcesScreen.cs:282` — the only row-creation point; per-row rows are created inline, so reached via private `resourceRows` dict (publicized): `row.GameObject` + dict-key Tag; category headers never touched). Attach-once guard: static `HashSet<Tag>` reset per screen instance.
- [x] Row tooltip builder: `ResourceRowTooltip.BuildTooltipLine(Tag)` — **1 line** `±net (−consumed +produced)` [+ `, N циклов` appended after a comma only when net < 0 → constant line count, mid-hover hot-swap safe]; noise guard (produced/consumed < 0.05 → no line); absolute values fed to the game formatter, signs added manually; whole body try/catch, no-throw, one-shot `PUtil.LogWarning`
- [x] Build green (0 errors, 0 warnings) — in-game acceptance **pending user**: hovering a pinned row and a show-all row shows the detail lines

**Criterion:** build green; user confirms tooltips appear in both places with correct numbers.
**Commit:** `feat: resource row tooltips (pinned panel + show-all screen)`

## Stage 4 — Calorie counter tooltip (MeterScreen_Rations)
- [x] Postfix `MeterScreen_Rations.OnTooltip` (protected override, no params; patch class `MeterScreen_Rations_OnTooltip_ResourceRemain__Patch`): `RationsTooltip.Extend(meter)` rebuilds the multi-string tooltip mirroring the game's exact code — total = `WorldResourceAmountTracker<RationTracker>.Get().CountAmount(dict, inventory)` **return value** (dict values are unit counts, NOT calories), per-food line value = `units × FoodInfo.CaloriesPerUnit`, same `OrderByDescending` order, same styles (`ToolTipStyle_Header`/`ToolTipStyle_Property`), header format `UI.TOOLTIPS.METERSCREEN_MEALHISTORY` with the estimate appended after the amount:
  - header gets ` (N циклов для M дубликантов)` (M = alive standard dups; N = floor(kcal / (kcalPerDup × M))); `(-)` when M = 0; no suffix when kcalPerDup ≤ 0 or N > 999
  - each per-food line gets ` (N циклов для M дубликантов)` with M = dups permitted that food; ` (-)` when M = 0
  - `ClearMultiStringTooltip()` runs LAST (all values computed first) → on error the original game tooltip is left untouched; one-shot `PUtil.LogError`
- [x] Build green (sln 0 errors; no ResourceRemain warnings) — in-game acceptance **pending user**: calorie tooltip shows overall + per-food estimates, `(-)` for foods banned for everyone

**Criterion:** build green; user confirms the calorie tooltip matches the mockup.
**Commit:** `feat: calorie counter tooltip cycle estimates`

## Stage 5 — Docs, final build, wrap-up
- [x] `ResourceRemain/README.md` per `mod-readme` skill (EN, Features, DLC matrix, Changelog 2026-09-24 v0.0.1, Source-and-Support; no Options section); `preview.png` left as placeholder — user replaces with an in-game screenshot
- [x] Root `README.md`: mod table row added `| [ResourceRemain](ResourceRemain/README.md) | Shows per-cycle resource change and remaining supply estimates … |`
- [x] Final `dotnet build ONI-mods.sln -c Debug` green (0 errors, no ResourceRemain warnings); `.tmp/build_mod_dir/ResourceRemain_dev/` up to date (dll, pdb, mod.yaml, mod_info.yaml, strings/ru.po)
- [x] Spec `Changes` updated (v2: ModAssets/strings path, OnToolTip wrapping, CountAmount return value)

**Criterion:** build green; READMEs in place; root mod list updated.
**Commit:** `docs: ResourceRemain README and root mod list`

## Fix (user feedback 2026-09-24, after in-game test)
- [x] Row tooltips invisible: row root's `MultiToggle` (IPointerEnter/Exit handler) stole hover before runtime-added `ToolTip`; fixed via prefix patches on `MultiToggle.OnPointerEnter/OnPointerExit` forwarding to a same-GO `ToolTip` (clicks/nested toggles unaffected). Child-GO-with-sprite approach rejected: raycast would swallow row buttons (EventSystem only hits the raycast GO + descendants).
- [x] Rations text in EN: installed mod folder's `strings/` was empty (ru.po never reached the game) — per user decision dropped .po entirely: `CycleText` now glues game keys `STRINGS.UI.FORMATDAY` («Циклов: {0:F1}», number passed as string to avoid «100.0») + `STRINGS.DUPLICANTS.STATS.SUBJECTS.DUPLICANT/POSSESSIVE/PLURAL` → «(Циклов: 100, 7 дубликанты)»; row deadline uses the same cycles key; removed `ru.po`/`ModAssets/`/`RegisterForTranslation`, kept only `NONE`.
- [x] `ResourceRemain.csproj`: added `UnityEngine.UI.dll` reference (needed for `PointerEventData`; the shared props reference is commented out repo-wide)
- [x] Build green; in-game re-acceptance **pending user**

**Commit:** `fix: row tooltip hover via MultiToggle prefix + game-key localization`

## Stage 6 — Fix round 2 (user feedback 2026-09-24, in-game test #2)
- [x] R7: `STRINGS.RESOURCE_REMAIN.NONE` → plain `const string NONE = "(-)"` (Strings.cs); `CycleText.None` now returns the plain string (RationsTooltip.cs needed no change); no `LocString`, no registration
- [x] Sampler: new `CycleStats.cs` — `CycleStatsSampler : KMonoBehaviour, ISim1000ms`, one plain-GO instance per world (spawn via `WorldContainer.OnSpawn` Harmony postfix + first-tick guard for the active world; `WorldAdded` fires only for rocket interiors — see IN notes); per (world, tag) buckets: current/previous cycle produced/consumed, lastValue, boundaryAmount; 600-s buckets aligned to `GameClock.GetTime()`, current→previous flip at boundary; settle gate — discard accumulation with `time < T_ref + 30` game-seconds (`T_ref` = `Game.OnLoad` on save load / first unpause via `PauseChanged` on new game, first-tick safety pin)
- [x] `ResourceDelta`: tracker/`ChartableData` code removed; `TryGetCurrentStats` / `TryGetPreviousStats` on the sampler API (no noise guard — `IsReady` freshness instead); deadlines — current: `available / (netSoFar × 600 / elapsedSeconds)`, previous: `boundaryAmount / |netPrev|`; guards kept (net ≥ 0, |net| < 0.1, elapsed < 1 s, N > 999 → no deadline); zero values flow through
- [x] `ResourceRowTooltip`: two fixed lines via `STRINGS.UI.ELEMENTAL.UPTIME.THIS_CYCLE` / `LAST_CYCLE` (verified in `STRINGS/UI.cs:15475/15477`) + `": "`, `BuildValueLine` = `±net (−consumed +produced)` + optional `, {CycleText.Cycles(N)}`; previous line omitted until its bucket is complete; attach/wrap/hover mechanism unchanged; stale `ru.po` removed from build output
- [x] Build green: `NUGET_PACKAGES=… dotnet build ONI-mods.sln -c Debug` (0 errors, no ResourceRemain warnings beyond pre-existing nullable ones), `.tmp/build_mod_dir/ResourceRemain_dev/` refreshed (dll 2026-09-24 21:35)
- [ ] In-game re-acceptance **pending user**: `(-)` instead of MISSING; no giant +delta right after save load; tooltip on Aluminium ore / unchanged resources in general; two-line format matches the mockup

**Criterion:** build green; user confirms all 4 items (MISSING fix, load artifact, tooltip on all resources, previous-cycle line) in-game.
**Commit:** `fix: cycle sampler + two-line row tooltip (round-2 feedback)`

## Stage 7 — Fix round 3 (user feedback 2026-09-24, in-game test #3)
- [x] Crash fix (`CycleStats.cs`): cycle-boundary finalization mutated `Dictionary<Tag, Bucket>` during enumeration (`tags[kv.Key] = b` inside `foreach`) → Mono/net48 bumps Dictionary version on existing-key set → `InvalidOperationException` on first tick of a new cycle. Fixed: `Bucket` is a class (in-place mutation, no hot-path dictionary writes), two-phase finalization (collect keys in scratch `List<Tag>` → mutate), sampling stores a bucket only on first sight, catch block sets `this.enabled = false` (IN12: enabled alone doesn't unregister the ISim dispatch — best-effort, the try/catch still contains any exception)
- [x] `ResourceRowTooltip.cs`: previous-cycle line always shown together with the current line; while the previous bucket is not accumulated, `Предыдущий цикл: -` (plain dash, no localization); deadline `, {CycleText.Cycles(N)}` unchanged when data exists
- [x] Build green: `NUGET_PACKAGES=… dotnet build ONI-mods.sln -c Debug` (0 errors, same warning baseline), `.tmp/build_mod_dir/ResourceRemain_dev/` refreshed (2026-09-25 00:42)
- [x] In-game re-acceptance: user confirmed 2026-09-25 after playing — «пока проблем не заметил, думаю можно закрывать» (no crash at new-cycle start; both line behaviors observed)

**Criterion:** build green; user confirms crash is gone and the two line behaviors in-game.
**Commit:** `fix: dictionary-mutation crash at cycle boundary + dash for unaccumulated previous cycle`

## Done
Task accepted by the user and closed on 2026-09-25; directory archived to `.dumbspec/archive/`.
