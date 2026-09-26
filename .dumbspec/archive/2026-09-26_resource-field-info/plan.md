# 2026-09-26_resource-field-info: implementation plan (ResourceFieldInfo)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage ends with a commit. This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Write `draft.md` (raw input verbatim)
- [x] Synchronous research subagent → `research.md` (6 topics + verdicts)
- [x] Refine `spec.md` to v1 (Created + Changes)
- [x] Write `plan.md`

**Criterion:** all four task files exist; spec v1 and plan committed to the task branch.
**Commit:** `docs(resource-field-info): add draft, research, spec v1 and plan`

## Stage 1 — Project scaffold
- [x] Create `ResourceFieldInfo/` folder with csproj (net48, `IsMod=true`, `IsPacked=true`, `GenerateMetadata`, ProjectReference → `UtilLibs`), following the `BuildDoorOverWall` csproj template
- [x] Create `ResourceFieldInfo/Mod.cs`: `namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2`, `OnLoad(Harmony)` calling `base.OnLoad(harmony)`
- [x] Register the project in `ONI-mods.sln` (`Project(` line + 4 configuration lines, matching an existing mod project's entry)
- [x] Run the solution build — it must succeed with the new project included

**Criterion:** `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` builds the new project (expected read-only `CopyModsToDevFolder` failure at the very end is OK) and emits the mod dll.
**Commit:** `feat(resource-field-info): scaffold mod project`

## Stage 2 — Feature implementation
- [x] Implement region calculation: 8-neighbor BFS over `Grid` (cardinal + diagonal cell helpers), same-`ElementIdx` predicate, summing `Grid.Mass[c]`; result = (cellCount, totalMass); cached per start cell, invalidated when the hovered cell changes → `ResourceFieldInfo/FieldRegionInfo.cs`
- [x] Implement the Harmony **prefix on `HoverTextDrawer.EndDrawing()`** with guards: `PlayerController.Instance.IsUsingDefaultTool()`, Ctrl held (Left/Right), valid cell under cursor via `Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()))` + `Grid.IsValidCell`, and the "raw material cell" condition (flag2/flag3 + overlay filters) replicated from `SelectToolHoverTextCard.UpdateHoverElements`; registered via `[HarmonyPatch]` attribute, applied by `base.OnLoad(harmony)`'s `PatchAll` → `ResourceFieldInfo/FieldInfoTooltipPatch.cs`
- [x] Emit the two tooltip lines before `EndDrawing` completes: `NewLine()` + `DrawText` with `STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME` + ": " + count, and `STRINGS.UI.ALLRESOURCESSCREEN.TOTAL` + ": " + `GameUtil.GetFormattedMass(totalMass)` (style `Styles_BodyText.Standard` from the live `SelectToolHoverTextCard`)
- [x] Add error handling: try/catch around the whole prefix body with one-shot `PUtil.LogError`; verbose diagnostics inside `#if DEBUG`

**Criterion:** solution build succeeds; the mod implements the full spec (guards, 8-neighbor region, mass sum, reused i18n strings, cache, logging).
**Commit:** `feat(resource-field-info): show region cell count and total mass on Ctrl+hover`

## Stage 3 — Acceptance (independent verification)
- [x] Independent build check: clean `dotnet build ONI-mods.sln -c Debug` succeeds and the packed mod dll exists in `bin/` → PASS (0 errors; packed dll with mod classes + ILRepacked PLib/UtilLibs in `bin/Debug/net48/` and `.tmp/build_mod_dir/ResourceFieldInfo_dev/`)
- [x] Independent code-review agent: verify the code against the spec checklist (hook point, all guards, 8-neighbor BFS, `Grid.Mass` sum, i18n reuse with no new .po, cache, try/catch + logging, net48/0Harmony/sln invariants) → all 8 checklist items PASS, no blockers; one minor gap (cache not invalidated on element change at the same cell) — fixed in the same stage

**Criterion:** build passes and the review agent reports every spec item satisfied (or lists concrete gaps).
**Commit:** `fix(resource-field-info): invalidate cached field when the hovered cell's element changes`

## Stage 4 — Documentation
- [x] Write `ResourceFieldInfo/README.md` in English per the `mod-readme` rules (description, Ctrl+hover usage, format, i18n note, changelog entry)
- [x] Add the mod to the mod list in the root `README.md`

**Criterion:** mod README exists in English with a changelog; root README lists the mod.
**Commit:** `docs(resource-field-info): add mod README and root README list entry`
