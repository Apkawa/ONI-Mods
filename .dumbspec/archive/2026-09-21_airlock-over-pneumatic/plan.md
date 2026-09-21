# 2026-09-21_airlock-over-pneumatic: implementation plan (airlock over pneumatic door)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red → green → refactor) + a commit at the end. No automated test suite exists in this repo: "red" = reproduced failure (baseline log `.tmp/Player_door_bug.log` + decompiled-code evidence in research.md), "green" = `dotnet build` passes + manual in-game acceptance by the user. This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Capture draft (draft.md, user clarification appended)
- [x] Research: log tail, mod source (BuildDoorOverWall/Mod.cs), game-side port check (research.md)
- [x] Root cause: Door & PressureDoor both declare a logic input port on the anchor cell; the live door's registered port conflicts with the dragged ghost's port in `BuildingDef.AreLogicPortsInValidPositions` → `TryReplaceTile` returns null → silent no-op
- [x] spec.md v1 written; open questions resolved with user (symmetric case in scope; `#if DEBUG` LogDebug; extend existing `IsValidPlaceLocation` postfix)
- [x] plan.md written

**Criterion:** draft/spec/research/plan exist in `.dumbspec/current/2026-09-21_airlock-over-pneumatic/`, root cause documented with evidence, user confirmed scope.
**Commit:** `docs(dumbspec): spec for airlock-over-pneumatic bug fix`

## Stage 1 — Log the native fail reason in the TryBuild path
- [x] In `BuildDoorOverWall/Mod.cs` TryBuild postfix: before calling `def.TryReplaceTile`, call `def.IsValidPlaceLocation(visualizer, anchorCell, orientation, replace_tile: true, out fail_reason, ...)`; on failure log `PUtil.LogDebug` with the reason — inside `#if DEBUG`, using `.F(...)`, no mod-name prefix in message
- [x] Verify build: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` (green, 28 pre-existing nullable warnings, 0 errors)

**Criterion:** Debug build succeeds; in the failure case the game log contains a line with the native fail reason (visible to user in a DEBUG run; not observable in release).
**Commit:** `feat(BuildDoorOverWall): log native IsValidPlaceLocation fail reason in TryBuild`

## Stage 2 — Door-over-door logic-port bypass in the IsValidPlaceLocation postfix
- [x] In `BuildDoorOverWall/Mod.cs`, extend the existing 6-arg `IsValidPlaceLocation` postfix: when `replace_tile == true`, native result is `false`, `fail_reason == STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED`, and the replacement candidate at `cell` is a door (`IsDoorDef` + `Replaceable`), reset `fail_reason` and return `true` (second postfix `BuildingDef_IsValidPlaceLocation6_DoorPortBypass__Patch`, additive, no allow-lists)
- [x] Confirm the same patch fixes both directions: `PressureDoor` over `Door` and `Door` over `PressureDoor` — superseded: user verified both directions in-game, 2026-09-21
- [x] Confirm the same-prefab guard (`TryReplaceTile` prefix) still blocks door-over-same-door — superseded: no regression reported in the in-game run, 2026-09-21
- [x] Verify build as in Stage 1 — superseded: mod built and ran in-game during user acceptance, 2026-09-21

**Criterion:** Build succeeds; per code review the bypass fires only for door-over-door replacement on the conflicting cell; foreign logic ports still block.
**Commit:** `fix(BuildDoorOverWall): allow door-over-door when only the replaced door's logic port conflicts`

## Stage 3 — Build + manual acceptance handoff
- [x] Final `dotnet build ONI-mods.sln -c Debug` from repo root (caches in `.cache/`) — mod built and deployed to the game for acceptance
- [x] Tell user the exact in-game acceptance steps: place `PressureDoor` over a live `Door`, place `Door` over a live `PressureDoor`, and re-verify an unchanged case (e.g. `ManualPressureDoor` over `Door`, wall-over-door) still works
- [x] Record acceptance outcome in plan.md; commit task files to `archive/` only after user confirms — user confirmed in-game: "вроде все работает теперь" (2026-09-21)

**Criterion:** `dotnet build` green; user reports both directions place successfully in-game and no regression in previously-working placements.
**Commit:** N/A (accepted manually; archived and squash-merged to master)
