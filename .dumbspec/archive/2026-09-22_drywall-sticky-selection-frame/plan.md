# 2026-09-22_drywall-sticky-selection-frame: implementation plan (sticky selection frame)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.
Note: no automated tests exist for this repo; the red/green cycle is "build must pass" plus the user's manual in-game acceptance (per AGENTS.md).

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] draft.md captured verbatim (Refs: #2)
- [x] initial spec.md skeleton from draft
- [x] research subagent: frame mechanics, mouse/shift handlers, task #2 constraints → research.md
- [x] spec.md v1 (Created header + Changes)
- [x] plan.md written

**Criterion:** research.md + spec.md v1 + plan.md exist and are consistent; open questions non-blocking.
**Commit:** `docs(spec): research+spec+plan for drywall sticky selection frame`

## Stage 1 — Hide the frame on LMB-up without Shift
- [x] In `BestBuildDryWall/Mod.cs`, in `DragTool_OnLeftClickUp__Patch.Postfix` (`:732-738`), after the original runs: read `__instance.areaVisualizer` (via the same reflection access the mod already uses in `ShiftRectSession.EnsureBoxVisualizer`, `Mod.cs:268-321`); if it is active, `SetActive(false)`
- [x] Add a `#if DEBUG` `PUtil.LogDebug` log of the hide (compiled out of release); no mod-name prefix in the message
- [x] Do NOT touch: session `End` timing (prefix/postfix split from task #2 Fix #3), GetMode prefix, ghost logic
- [x] Verify: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` — build passes (0 errors, 0 new warnings; independent acceptance subagent: PASS)
- [x] User in-game acceptance: case 1 OK, regressions OK; case 2 partially OK (frame cleared at LMB-up, but user wants it cleared at the moment Shift is released mid-drag) → moved to Stage 2

**Criterion:** build passes; user confirms both cases clear the frame and no regressions. (Case 2 refined in Stage 2.)
**Commit:** `fix(BestBuildDryWall): hide selection frame on LMB-up when Shift not held`

## Stage 2 — Case 2: hide the frame when Shift is released mid-drag
- [x] In `BestBuildDryWall/Mod.cs`, in `DragTool_OnMouseMove__Patch.Postfix`, in the existing `session active && !ShiftHeld()` branch (after `End("shift released")`): hide `__instance.areaVisualizer` if active (null-safe `SetActive(false)`)
- [x] Clear the frame's "W x H / N tiles" text on that path too (`RemoveCurrentAreaText()` is public on DragTool, self-guarded)
- [x] Preserve the "re-press Shift without moving" flow: show clause outside the session-active block, gated on tool/def (identical to GetMode prefix: `BuildTool` cast + `def.PrefabID == "ExteriorWall"`), `ShiftHeld()`, `dragging` (private field `DragTool.cs:44`, true only between click-down and click-up — no hover), `areaVisualizer != null && !activeSelf` → `SetActive(true)`; no-op in the normal path
- [x] `#if DEBUG` `PUtil.LogDebug` logs for the hide/show; no mod-name prefix in messages
- [x] Do NOT touch: session Begin/End logic, GetMode prefix, ghost logic, the Stage 1 mouse-up hide (verified by independent acceptance: diff confined to the OnMouseMove postfix region)
- [x] Build: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` — passes (0 errors, 0 BestBuildDryWall warnings; independent acceptance subagent: PASS)
- [x] User in-game acceptance: (a) drag with Shift → release Shift, keep dragging → frame disappears immediately; (b) release Shift → press Shift again without moving → drag → frame reappears; (c) hover with Shift (no click) → no frame; (d) normal full-Shift drag → unchanged — user: «супер, теперь работает как надо» (2026-09-22)

**Criterion:** build passes; user confirms (a)-(d). ✔
**Commit:** `fix(BestBuildDryWall): hide selection frame on Shift release mid-drag, show it again on Shift re-press`
