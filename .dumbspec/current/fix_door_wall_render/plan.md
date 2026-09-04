# fix_door_wall_render: implementation plan (door replacement breaks wall rendering)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.

**Test-adapter note:** the repo has no automated unit-test harness and the game cannot be run
in this sandbox. A stage's "test" is therefore: (a) a static check (shell script in `./.tmp/`,
kept out of the build) asserting observable facts about the code, and (b) a code-level trace of
the game flow against `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp` performed by an
independent agent. "Red" = the trace/check identifies the failing behavior line-by-line;
"green" = implementation + static check + `dotnet build ONI-mods.sln -c Debug` pass.
Final in-game verification is a manual step for the user (checklist in Stage 4).

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Superficial research (ralph, 2 rounds: round 2 re-verified all citations) → `research.md` §1–§8 (mod patches, door-def diff table, game flow end-to-end, BlockTileRenderer mechanics, cancel path, log inspection)
- [x] Refine spec.md v2 (root cause: IsTilePiece ⇒ Constructable.MarkArea overwrites layer-9 of the live wall cell; cancel never restores it; completion self-heals; LogicPorts ruled out)
- [x] Write plan.md

**Criterion:** `draft.md`, `research.md`, `spec.md`, `plan.md` exist in the task dir; spec open questions
reduced to 4 deep-trace items; no research finding left unwritten.
**Commit:** `<type>(<scope>): <summary>`

## Stage 1 — Red: deep trace of completion in the broken orientation + fix mechanics verification
- [ ] Trace (a) — what actually happens at construction COMPLETION in the broken orientation (anchor in air, wall in upper cell, plan exists in `Grid.Objects[upperWallCell, 9]` instead of the wall): does the plan GO carry a `BuildingComplete` component and which KPrefabID tags does the UnderConstruction prefab have? What does `GetReplacementCandidate(upperWallCell)` in `Constructable.OnCompleteWork`/`FinishConstruction` (Constructable.cs:124-291) actually return — the wall or the plan GO? Do the plan GO's gates (`Def.Replaceable`, `CanReplace`) pass? Conclude: is the wall destroyed exactly once, not at all, or is there a double-handling — i.e. is there a HIDDEN second bug beyond rendering?
- [ ] Trace (b) — fix mechanics: confirm `Constructable.MarkArea` runs at spawn (`OnSpawn` :345) inside `Instantiate` inside `def.TryReplaceTile` (BuildingDef.cs:529-540) — i.e. the layer-9 overwrite is complete BEFORE `TryReplaceTile` returns, so restore-after-return is the correct window; confirm `TileVisualizer.RefreshCell` → `RefreshCellInternal` (TileVisualizer.cs:5-21) → `BlockTileRenderer.Rebuild(layer, cell)` (:775-784) re-dirties the chunk and `GetConnectionBits` (:581-628) then reads the restored live wall GO (connection bits correct → no insets); confirm nothing else between plan creation and completion reads `Grid.Objects[upperWallCell, 9]` in a way that would break with the wall (not the plan) restored there.
- [ ] Trace (c) — consumers of `Grid.IsTileUnderConstruction[anchor]=true` (set at plan spawn; compare with the native flow where the same happens) and a short save/load note (informational; the corrupted state disappears after the fix).
- [ ] Record everything with file:line citations in `.tmp/fix_door_wall_render_trace.md`; state explicitly whether the Stage-2 restore design (spec "Подход") needs any adjustment based on (a)–(c).

**Criterion:** `.tmp/fix_door_wall_render_trace.md` answers (a) with a definitive destruction-count conclusion, (b) line-by-line with no unverified assumption, (c) consumers listed; design confirmed or adjusted with justification.
**Commit:** N/A (trace doc lives in .tmp)

## Stage 2 — Green: implement the restore fix in the TryBuild postfix
- [ ] Implement in `BuildDoorOverWall/Mod.cs` inside `BuildTool_TryBuild_DoorReplacement__Patch.Postfix` (no new patch class, no new Harmony binding): BEFORE `def.TryReplaceTile(...)` capture `Grid.Objects[cell, (int)def.TileLayer]` for every door area cell (skip the step when `def.TileLayer == ObjectLayer.NumLayers` — covers Door/WoodenDoor); AFTER plan creation (`plan != null`) restore every captured non-null GO back into `Grid.Objects` and call `TileVisualizer.RefreshCell(cell, def.TileLayer, def.ReplacementLayer)` per affected cell (English comments with game file:line references, matching existing style).
- [ ] Static check script `./.tmp/fix_door_wall_render_checks.sh`: asserts the capture+restore+refresh logic is present in the TryBuild postfix (capture before `TryReplaceTile`, restore guarded by `plan != null`, `RefreshCell` present, `NumLayers` skip present), existing mod invariants (single UserMod2, no `[HarmonyPatch]` attributes, no debug logging) and that `dotnet build ONI-mods.sln -c Debug` is green (0 CS errors; tolerated MSB3027/3021 into read-only ~/ONI).
- [ ] Green: static check passes; full build green.
- [ ] Refactor: keep the diff minimal; comments explain WHY (MarkArea overwrite, research §4.3) not what.

**Criterion:** static check passes; build green; the fix is confined to the existing TryBuild postfix body (door-def scoped via the existing `IsDoorDef` gate); no other file touched.
**Commit:** `<type>(<scope>): <summary>`

## Stage 3 — Acceptance: independent audit
- [ ] Independent ralph audit (does not trust the implementer's report) against `.tmp/fix_door_wall_render_trace.md` + the new Mod.cs, checking: (1) broken orientation — after plan creation `Grid.Objects[upperWallCell, 9]` = the live wall GO again, `RefreshCell` fires, connection bits recover (no border insets); (2) cancel — wall intact, rendering intact (restored entry is the wall, `UnmarkArea` layer-11 only, no corruption left); (3) completion — wall destroyed exactly once, refund + replaced-trigger intact, finished door's `BuildingComplete.OnSpawn` re-marks both cells (self-heal preserved); (4) regressions — native orientations (whole-in-wall, lower-in-wall) untouched (postfix bails on existing plan; capture/restore is a no-op there), Door/WoodenDoor no-op (`TileLayer == NumLayers`), door-over-rock (no candidate → no plan → no capture), hover tint + 4-arg validity postfixes unchanged, no recursion/new Harmony binding risk; (5) build green, packed dll with one UserMod2.

**Criterion:** audit report (`.tmp/acceptance_fix_door_wall_render.md`) — all checks PASS or each FAIL justified and re-fixed before commit.
**Commit:** `<type>(<scope>): <summary>` (same commit as Stage 2 if no re-fix needed)

## Stage 4 — Finalization
- [ ] Full review pass of the Mod.cs diff (naming, comments, no leftovers); confirm csproj/Directory.Build.* untouched (build invariants)
- [ ] Fresh full build; confirm bin/ artifacts: repacked mod dll (UtilLibs+PLib packed), mod.yaml/staticID=BuildDoorOverWall
- [ ] In-game verification checklist for the user (Russian) in the final report, covering: broken orientation × {Manual Airlock, Insulated Door, Mechanized Airlock} (no block borders), cancel → wall intact, complete build → door replaces wall + rendering normal, regression: Door/WoodenDoor same orientation, lower-in-wall, whole-in-wall, door-over-rock, hover color over wall/Backwall
- [ ] Update plan journal; note hand-off to `fix_buld_door` (its Stage 3/4 remain deferred; its checks.md item "Требует доработки" can be closed by the user after in-game verification)

**Criterion:** clean full build; repacked dll in bin/; checklist delivered; all earlier-stage checkboxes [x].
**Commit:** `<type>(<scope>): <summary>`
