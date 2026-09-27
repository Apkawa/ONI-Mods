# 2026-09-26_material-filter-overlay: implementation plan (MaterialFilterOverlay)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.
"Test" = `dotnet build` green (no automated game tests; in-game acceptance is done by the user).

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Write draft.md (raw input)
- [x] Initial spec skeleton from draft
- [x] User refinement (Q&A: whole category fallback, no select-all, no persistence, autonomous)
- [x] Research via subagent (TileMode / OverlayLegend / ToolParameterMenu / ElementLoader / repo patterns) → research.md
- [x] Spec v1 (Created timestamp + Changes)
- [x] Plan written

**Criterion:** research.md, spec.md (v1), plan.md exist in the task dir; user approved autonomous execution.
**Commit:** `feat(material-filter-overlay): spec, research and plan`

## Stage 1 — Project scaffolding
- [x] Create `MaterialFilterOverlay/` mod project (csproj net48, IsMod, GenerateMetadata, IsPacked, ProjectReference UtilLibs) per BuildDoorOverWall/SpaceOverlay pattern
- [x] `Mod.cs`: `namespace OxygenNotIncluded.Mods`, `UserMod2`, `OnLoad(Harmony)` stub (logs OnLoad)
- [x] Add project to `ONI-mods.sln`
- [x] Build green (`NUGET_PACKAGES=... dotnet build ONI-mods.sln -c Debug`)

**Criterion:** `dotnet build ONI-mods.sln -c Debug` succeeds with the new project in the sln.
**Commit:** `feat(material-filter-overlay): scaffold mod project`

## Stage 2 — Verify element display-name localization
- [x] Subagent: find the exact localization key format for element display names (RU "Медь" for Copper) in the .po files / STRINGS, and confirm `Element.name` values used as keys (e.g. `STRINGS.ELEMENTS.COPPER.NAME` vs `STRINGS.UI.ELEMENTS...`)
- [x] Append the confirmed format to research.md (`Element.name` is already localized; values carry `<link>` markup)

**Criterion:** research.md contains a verified localization key format with an example from the RU strings file.
**Commit:** `chore(material-filter-overlay): record element localization format`

## Stage 3 — UI: custom legend panel
- [x] Build the panel with PLib: 11 category rows `[+] CategoryName [checkbox]`, single-select behaviour (one category checked at a time, ALL as in vanilla), "+" button visible only on the selected category
- [x] Materials tree: on "+" click expand/collapse rows with independent material checkboxes (from `ElementLoader.FindElements` by `materialCategory`), labels via verified localization
- [x] Postfix `OverlayLegend.SetLegend(Mode, bool)`: when mode is TileMode — hide vanilla `ToolParameterMenu` (AccessTools field `filterMenu`) and attach our panel to the same parent; on other modes leave vanilla untouched
- [x] Build green

**Criterion:** build green; code compiles and the panel-creation path is wired (in-game visual check deferred to user acceptance).
**Commit:** `feat(material-filter-overlay): legend panel with per-material sub-filters`

## Stage 4 — Filter logic
- [x] Category selection → fill `Game.Instance.tileOverlayFilters` with the vanilla category→tags mapping (METAL→Metal+RefinedMetal, BUILDABLE→BuildableRaw+BuildableProcessed, FILTER→Filter, LIQUIFIABLE→Liquifiable, LIQUID→Liquid, CONSUMABLEORE→ConsumableOre+Sublimating, ORGANICS→Organics, FARMABLE→Farmable+Agriculture, GAS→Breathable+Unbreathable, MISC→Other, ALL→everything), then `Game.Instance.ForceOverlayUpdate()`
- [x] Sub-material selection → replace the list with the selected materials' element tags; empty sub-selection → fall back to category mapping; `ForceOverlayUpdate()` on every change
- [x] Switching to another category: clear sub-selection, collapse the tree, move "+" to the new category
- [x] Build green

**Criterion:** build green; state machine covers all spec rules (verified in-game by user).
**Commit:** `feat(material-filter-overlay): per-material filtering logic`

## Stage 5 — Docs + final
- [x] Write `MaterialFilterOverlay/README.md` (English, mod-readme skill)
- [x] Add mod to the root `README.md` mod list
- [x] Final build green
- [x] Archive task to `.dumbspec/archive/2026-09-26_material-filter-overlay/`

**Criterion:** root + mod READMEs updated, final build green, task archived.
**Commit:** `docs(material-filter-overlay): READMEs and archive`

## Stage 6 — Bugfix: radio dot/border rendering (user-reported)
- [x] Research: diagnose how `borderImage` + `toggle.states` sprites combine for checked/unchecked states, and why the user's `hollow` inversion produced "no dot" for both states
- [x] Implement the correct rendering: unchecked = hollow ring, checked = filled dot (restore correct `hollow` semantics)
- [x] Build green (`dotnet build ONI-mods.sln -c Debug`)
- [x] Acceptance: independent check of the state→sprite mapping in the final code + build output

**Criterion:** unchecked rows show a hollow ring, the selected category shows a filled dot; build green.
**Commit:** `chore(material-filter-overlay): stage 6 diagnosis; revert failed hollow inversion (no code delta vs HEAD 45128ea)`

## Stage 7 — Restyle radio markers to vanilla style (user request)
- [x] Research: pixel analysis of the vanilla «Схема микробов» tab marker (white square, black dot centered when selected) → research.md
- [x] Implement: `GetRadioBorderSprite` → solid white disc (`hollow: false`); dot sprite → small black dot on transparent bg for the checked state; unchecked state keeps no mark
- [x] Build green (`dotnet build ONI-mods.sln -c Debug`)
- [x] Acceptance: independent check of the new state→sprite mapping + build output
- [x] User in-game acceptance: markers read correctly (dot on selected, empty elsewhere); requested slightly larger dot → `dotRadius` `size/6f` → `size/3f`, build green

**Criterion:** unchecked row = plain white circle; selected row = white circle with a black dot in the center; build green.
**Commit:** `fix(material-filter-overlay): restyle radio markers to vanilla style (white disc + black dot)`; `fix(material-filter-overlay): enlarge black radio dot (size/6 -> size/3)`
