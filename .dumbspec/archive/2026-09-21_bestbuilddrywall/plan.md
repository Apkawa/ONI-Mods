# 2026-09-21_bestbuilddrywall: implementation plan (BestBuildDryWall)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (failing test → green implementation → refactor) + a commit at the end.
**TDD deviation (repo fact):** this repo has NO automated test suites — the only automated
verification is `dotnet build`, and acceptance is manual in-game (root AGENTS.md). So the
"red test" for every stage is "feature absent / build baseline", "green" is "implementation
builds cleanly", and the observable Criterion below is what the user verifies in-game.

This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Research R1: Drywall plan placement API (`BuildingDef.TryPlace`, `BuildTool`, validation)
- [x] Research R2: facade ("Схема") storage/switching (`BuildingFacade`, `FacadeSelectionPanel`)
- [x] Research R3: ghost rect + size text in dig/cancel tools (`DragTool` Box mode, `NameDisplayScreen`)
- [x] Research R4: repo/mod structure (csproj template, sln wiring, PatchUtil, build targets)
- [x] Write `spec.md` v1 (from draft + research)
- [x] Write `plan.md`

**Criterion:** `research.md`, `spec.md`, `plan.md` exist, are internally consistent, and match the draft's two features.
**Commit:** `docs(bestbuilddrywall): spec, research, and plan for BestBuildDryWall mod`

## Stage 1 — Mod skeleton + solution wiring
- [x] Create `BestBuildDryWall/` project: `BestBuildDryWall.csproj` (exact BuildDoorOverWall template, `PackageId=BestBuildDryWall`), `Mod.cs` (`namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2`, `OnLoad` → `base.OnLoad` + `PUtil.LogDebug`), `README.md` (Russian feature description)
- [x] Register project in `ONI-mods.sln`: new GUID `{4AB6DBA8-0FFD-4076-91A2-A54A90C5B875}`, `Project(...)` block + 4 `ProjectConfigurationPlatforms` lines (no comments in sln)
- [x] `dotnet build ONI-mods.sln -c Debug` → clean build (29 pre-existing warnings, 0 errors, no MSB3027); `BestBuildDryWall.dll` (332 KB, ILRepack-inlined) + `mod.yaml`/`mod_info.yaml` verified in `BestBuildDryWall/bin/Debug/net48/`

**Criterion:** clean Debug build of the whole sln; packed mod dll + both yamls present in `BestBuildDryWall/bin/Debug/net48/`.
**Commit:** `feat(bestbuilddrywall): add mod skeleton wired into the solution`

## Stage 2 — Feature 2: batch scheme repaint without rebuild
- [x] Verify patch targets & field accessibility: `BuildTool.OnDragTool(int cell, int distFromOrigin)` protected override (BuildTool.cs:302), fields `facadeID`/`selectedElements`/`buildingOrientation`/`def` + public `visualizer`, instant-build condition `DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild)`, `Grid.Objects[cell, (int)ObjectLayer.Backwall]` via `ObjectLayerIndexer`, Shift via `Input.GetKey(KeyCode.LeftShift/RightShift)` — all reachable through the publicized game assembly
- [x] Implement Feature 2 in `Mod.cs` (one task): `BuildTool_OnDragTool_FacadeRepaint__Patch.Prefix` — repaint case = cell holds a built `BuildingComplete` with same `Def` + same `primaryElement.Element.tag`, tool facade differs from `BuildingFacade.CurrentFacade` → `ApplyBuildingFacade(Db.GetBuildingFacades().TryGet(facadeID))` / `ApplyDefaultFacade()` and `return false`; guards: only `ExteriorWall` def, skip in InstantBuild/Sandbox mode; registered in `OnLoad` via `PatchUtil.TryPatch`; `#if DEBUG` logs; clean build (0 errors, no new warnings). Refactor: fixed `sameScheme` equivalence (default+default case no longer re-applies default facade).
  - Notes from implementation: `Building.primaryElement` is a lowercase public field (used `bc.primaryElement.Element.tag`); `BuildingFacadeResource` lives in `namespace Database` (`using Database;` added).

**Criterion:** clean build; (manual) with Drywall tool + a different scheme selected, brushing over a built drywall of the same material repaints it instantly (no rebuild, persists to save); other-material cells behave as before.
**Commit:** `feat(bestbuilddrywall): instant scheme repaint of built drywall without rebuild`

## Stage 3 — Feature 1: Shift-rect ghost + placement on release
- [x] Verify `DragTool`/`BuildTool` mouse plumbing (→ research.md R5): `BuildTool` overrides all three mouse handlers (OnMouseMove = base+UpdateVis, down/up = pure forwards) so DragTool-level patches fire via `base.`; `BuildTool.GetMode` is a protected override returning `Mode.Brush` → patch `BuildTool.GetMode` (virtual dispatch); no `cursorPos` field (parameter only); `downPos` protected, `dragging` private, `areaVisualizer` SetActive-toggled (never destroyed); Box mouse-up iterates raw downPos→cursor rect via `(int)Grid.PosToXY` floors + `IsValidCell && IsVisible` → `OnDragTool(cell, dist)`; `TryBuild` dedupe on `lastDragCell`/`lastDragOrientation` (reset -1 in the mouse-up prefix); ghost-clone recipe mirrors `BuildTool.OnActivateTool`; `CancelDragging`/`OnCmpDisable` postfixes needed for ghost cleanup
- [x] Implement Feature 1 in `Mod.cs` (one task): `ShiftRectSession` (MaxGhosts=2000, `Dictionary<int, GameObject>` ghosts, stride sparse sampling, incremental stale-destroy/new-create sync, ghost rect = exact game mouse-up rect computation); patches: `BuildTool.GetMode` prefix (→ `DragTool.Mode.Box` when def is ExteriorWall + Shift held); `DragTool.OnLeftClickDown` postfix (session start, first ghost at downPos cell); `DragTool.OnMouseMove` postfix (sync ghosts; end session + cleanup if Shift released mid-drag); `DragTool.OnLeftClickUp` prefix (end session, reset `lastDragCell = -1` for 1×1 dedupe, before native Box placement loop); `DragTool.CancelDragging` + `DragTool.OnCmpDisable` postfixes (ghost cleanup); per-cell placement via existing Feature-2 prefix (non-repaint cells fall through to vanilla `TryBuild`); clean build (0 errors, no new warnings); non-Shift path untouched (all new patches no-op without Shift/ExteriorWall)
  - Deviations found & fixed by implementation: `Util.KDestroyGameObject` (not `KDestroy`); `Grid.PosToXY(Vector3, out int, out int)` returns int cell coords directly (no float truncation); `UnityEngine.Input` lives in `UnityEngine.InputLegacyModule.dll` — project-local reference added in `BestBuildDryWall.csproj` (`Private=False`, game folder, ILRepack `**/Unity*` exclusion keeps it out of the packed dll).

**Criterion:** clean build; (manual) with Drywall tool + Shift: dragging shows the box ghost + `"{w} x {h}\n{w*h} tiles"` text + multiplied drywall ghosts; mouse-up places plans on all valid cells (and repaints same-material cells per Feature 2).
**Commit:** `feat(bestbuilddrywall): shift-rect drywall ghost placement on mouse release`

## Stage 4 — Finalize
- [x] Clean Debug + Release builds of the full sln — Debug: 29 warnings / 0 errors; Release: 30 warnings / 0 errors; none from BestBuildDryWall
- [x] Add `BestBuildDryWall` row to the root `README.md` mod table (Russian, both features)
- [x] Verify final artifacts: packed dll (PLib + UtilLibs inlined by ILRepack — verified via ilspycmd) + `mod.yaml`/`mod_info.yaml` in both `bin/Debug/net48/` (338 944 B dll, `BestBuildDryWall [DEBUG]`/`BestBuildDryWall_dev`) and `bin/` (Release, 337 920 B dll, `BestBuildDryWall`/`BestBuildDryWall`); no `UnityEngine*.dll` in outputs (InputLegacyModule ref is reference-only); stray PLib/UtilLibs dlls known-harmless

**Criterion:** both configurations build cleanly; README table updated; mod folder ready for the user's manual in-game acceptance.
**Commit:** `chore(bestbuilddrywall): finalize mod (release build, README row, artifact verification)`
- Caveat: `Directory.Build.targets` Clean removes `$(TargetDir)` per build — building Release (OutDir=`bin`) wipes `bin/Debug/net48/` too; rebuild Debug afterwards if needed.

## Stage 5 — Acceptance iteration 1: bug fixes (root causes in research.md R6)
- [x] Diagnose all 4 reported bugs from the in-game log + sources (→ research.md R6): (1) Shift = default `Action.DragStraight` key → game `SnapToLine` collapses the rect; (2) `GetMode` prefix returned `true` after setting `__result` — original method overwrote it, so mode never became Box (log: continuous brush `TryBuild`s during drag, zero Box mouse-up loop); (3) blind `Util.Swap` in `ShiftRectSession.Update` — only the top-right→bottom-left diagonal yields a valid loop; (4) no per-ghost validity tint
- [x] Fix #2: `BuildTool_GetMode__Patch.Prefix` → `return false` (skip original) when forcing Box
- [x] Fix #3: conditional swaps in `ShiftRectSession.Update` (game pattern `if (x1 < x0) Util.Swap(...)`)
- [x] Fix #1: reverted user's `ShiftHeld() == true` hack → real key check via the game's `Action.DragStraight` binding (Shift by default: `Input.GetKey((KeyCode)Global.GetInputManager().GetDefaultController().GetInputForAction(Action.DragStraight))`) + new `DragTool_SnapToLine__Patch.Prefix` (no-op `return false`, `__result = cursorPos` while a session is active — covers both game call sites)
- [x] Fix #4: per-ghost validity `IsCellActionable` = `def.IsValidPlaceLocation(tool.visualizer, Grid.CellToPosCBC(cell, Grid.SceneLayer.Building), tool.GetBuildingOrientation, out _)` OR the game's replace path (mirrors the `TryBuild` fallback: `GetReplacementCandidate`/`IsReplacementLayerOccupied`/`Def.Replaceable`/`CanReplace`) OR the Feature-2 repaint case (extracted to shared `IsRepaintCase`) → ghost `KBatchedAnimController.TintColour` white/red at creation (mirrors `BuildTool.SetColor`); end-of-session log now includes the invalid-ghost count
- [x] Clean Debug + Release builds, no new warnings (Debug last, so both output dirs exist; no `UnityEngine*.dll` in outputs)
- [x] Game copy dir updated (`.tmp/build_mod_dir/BestBuildDryWall_dev/`)

**Criterion:** clean builds; (manual) with Drywall tool + Shift: box mode actually engages (no brush placement during drag), rect ghosts render in all 4 directions, invalid cells tint red, mouse-up places plans on the whole rect and repaints same-material cells per Feature 2; no straight-line snapping conflict while the session is active.
**Commit:** `fix(bestbuilddrywall): acceptance iteration 1 — real Box mode, all-direction rects, no straight-line conflict, red validity tint`

## Stage 6 — Acceptance iteration 2: crash fix (root cause in research.md R7)
- [x] Diagnose the crash from `.tmp/Player_best-build-drywall_2_crash.log` (→ research.md R7): NRE is in the GAME's own Box path — the wall-tool prefab has no serialized `areaVisualizer` (vanilla BuildTool never used Box), so `DragTool.OnMouseMove:377` null-derefs it every drag frame; the same null makes `OnLeftClickUp:239` skip the placement loop
- [x] `ShiftRectSession.EnsureBoxVisualizer(tool)`: lazily borrow the game's own box visualizer from a donor `DragTool` in `PlayerController.Instance.tools` (static cache, shared single scan), instantiate on the tool, set `areaVisualizerSpriteRenderer` + `areaColour`; no-donor → once-only `PUtil.LogWarning` + graceful Brush fallback
- [x] Gate `BuildTool_GetMode__Patch.Prefix` on `EnsureBoxVisualizer` success (visualizer must exist before forcing Box — first `GetMode()` call happens on hover / inside the original `OnLeftClickDown`, so the vanilla null-guarded Box activation then runs for free)
- [x] Move `ShiftRectSession.End("mouse up")` from the `OnLeftClickUp` prefix to a postfix (session stays active through the original ⇒ snapping suppressed + Box loop runs; `lastDragCell = -1` reset stays in the prefix)
- [x] Bonus (spec gap found during the fix): the wall tool also lacks `areaVisualizerTextPrefab` → no native size text. Borrowed it the same way (shared donor scan), plus an `OnLeftClickDown` postfix backfill for the same-frame Shift+click edge case (vanilla creates the text before its first in-method `GetMode()` call); text colour set reflectively (`LocText.color` lives in Unity.TextMeshPro — not a csproj dependency); vanilla `RemoveCurrentAreaText` cleans up the borrowed instance on every mouse-up/cancel/deactivate
- [x] Clean Debug + Release builds (Debug last), no new warnings; updated `.tmp/build_mod_dir/BestBuildDryWall_dev/`

**Criterion:** no crash; (manual) with Drywall tool + Shift: box drag works end-to-end — vanilla Box frame + size text (`"{w} x {h}\n{n} tiles"`, like dig/cancel), per-cell ghosts in all 4 directions with red invalid tint, mouse-up places plans on the whole 2-D rect (no straight-line collapse) and repaints same-material cells per Feature 2.
**Commit:** `fix(bestbuilddrywall): acceptance iteration 2 — borrow the game's box visualizer (crash fix), session through mouse-up, native size text`
