# 2026-09-25_printer-easy-info: implementation plan (PrinterEasyInfo)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage ends with a build (`dotnet build ONI-mods.sln -c Debug`, net48) + a commit. No automated test suite exists in this repo; acceptance is a user in-game run — the build is the only automatic check.
This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Capture draft, initial spec; user refinement (icon only in non-duplicant columns; item codex info).
- [x] Research: scheme selection window (`ImmigrantScreen`/`CharacterSelectionController`), `CarePackageContainer`, book icon in item properties (`DetailsScreen.CodexEntryButton` → `ManagementMenu.OpenCodexToEntry`, sprite `OverviewUI_database_icon`), repo mod structure (template, sln, PatchUtil, PLib).
- [x] Write spec v1 and this plan.

**Criterion:** draft.md, research.md, spec.md (v1), plan.md exist on branch `2026-09-25_printer-easy-info`.
**Commit:** `docs(printer-easy-info): research, spec and plan`

## Stage 1 — Mod project skeleton
- [x] Create `PrinterEasyInfo/` project: csproj (net48, `IsMod`/`GenerateMetadata`/`IsPacked=true`, `ProjectReference` to `UtilLibs`, `ModDescription` etc., same as template/BuildDoorOverWall).
- [x] `Mod.cs`: `UserMod2` subclass, `OnLoad(Harmony)` (programmatic patches via `PatchUtil.TryPatch`), `OnUnload`/`OnAllModsLoaded` per repo pattern. (Note: this game's `UserMod2` has no `OnUnload` — omitted, per repo convention.)
- [x] Add project to `ONI-mods.sln` (Project line + 4 config lines, new GUID `6AA3C18A-AACB-48CF-A19A-47AFC70A4F43`).
- [x] Empty mod builds green and produces `PrinterEasyInfo/bin/.../PrinterEasyInfo.dll`.

**Criterion:** `dotnet build ONI-mods.sln -c Debug` succeeds; packed mod dll is produced; sln includes the new project.
**Commit:** `feat(printer-easy-info): add mod project skeleton`

## Stage 2 — Codex id resolution for care-package items
- [x] Helper: given `CarePackageInfo.id`, resolve a codex id with the same prefab-vs-element branch as `CarePackageContainer.SetAnimator()` (prefab first via `Assets.GetPrefab(id.ToTag())`, element fallback via `ElementLoader.GetElement(id.ToTag())`; codex id per `DetailsScreen.CodexEntryButton_GetCodexId()`). → `PrinterEasyInfo/CodexIdResolver.cs`
- [x] Validation: return id only if `CodexCache.entries.ContainsKey(id) || CodexCache.FindSubEntry(id) != null` (like `DetailsScreen.CodexEntryButton_GetCodexId()`), else empty (no button); null-cache guard.
- [x] Log resolution failures/warnings with `PUtil` (verbose in `#if DEBUG`).

**Criterion:** builds green; resolver is a testable static method used by Stage 3.
**Commit:** `feat(printer-easy-info): resolve codex id for care-package items` (fixed CS0136 duplicate `codexId` local; build green)

## Stage 3 — Book icon in care-package columns
- [x] Harmony postfix on `CarePackageContainer.GenerateCharacter(bool)` (private — via `PatchUtil.TryPatch`), registered in `Mod.OnLoad`. (Game types are in the GLOBAL namespace in this build — verified via DLL metadata.)
- [x] In the postfix: compute codex id from `__instance.Info.id`; remove any previously added button (reshuffle changes the item); if id non-empty, create a KButton+KImage with sprite `Assets.GetSprite("OverviewUI_database_icon")` + ToolTip (`UI.TOOLTIPS.OPEN_CODEX_ENTRY`) and place it in the column header at the pencil-icon position (deep-copy of the sibling pencil button via `Util.KInstantiateUI`, code-created fallback).
- [x] Click handler: `ManagementMenu.Instance.OpenCodexToEntry(codexId)` (same as `DetailsScreen.CodexEntryButton_OnClick`), null-guarded `Instance`, wired in both branches.
- [x] No button on columns without a codex entry (elements/items missing from the database) — stale button destroyed before the early-return (reshuffle-safe).

**Criterion:** builds green; in-game: care-package columns show the book icon in the pencil position; clicking opens the Codex entry for the column's item; reshuffle updates the button; duplicant columns unchanged. (Independent acceptance passed: build, patch registration, icon/logic, placement, reshuffle safety, resolver correctness, repo hygiene — all PASS. Final in-game check = user run, Stage 5.)
**Commit:** `feat(printer-easy-info): add book icon to care-package columns`

## Stage 4 — Documentation
- [x] Mod `README.md` in English per `mod-readme` skill (template verbatim, DLC matrix, features, limitations, changelog `2026-09-25: 1.0.0`).
- [x] Add `PrinterEasyInfo` row to the mod table in root `README.md`.
- [x] Changelog entry in the mod README.

**Criterion:** root README lists the mod; mod README exists, English, follows the template.
**Commit:** `docs(printer-easy-info): mod README and root README entry`

## Stage 5 — Acceptance
- [x] Final build green (0 errors, `PrinterEasyInfo.dll` 337,408 bytes, also copied to `.tmp/build_mod_dir/PrinterEasyInfo_dev/`).
- [x] User runs the mod in-game and confirms the behavior (position, click, reshuffle, missing-entry case).
  - User confirmed the icon works; reported a z-order bug: the Codex opened on top of / behind the still-open "Select a Blueprint" modal (game allows one modal at a time) → Stage 6.

## Stage 6 — Bugfix: close the selection window before opening the Codex (v1.0.1)
- [x] Research: `KScreen` has no `Parent`/`CloseScreen`; the window = nearest `CharacterSelectionController` ancestor by transform walk; its own synchronous close is `OnPressBack()` → `Show(false)` (avoid `KScreen.Deactivate` — it destroys).
- [x] Click handler → shared `OpenCodexEntry(container, codexId)`: walk up to the `CharacterSelectionController`, `OnPressBack()` (same as the game's ESC/close button), then `ManagementMenu.Instance.OpenCodexToEntry(codexId)`.
- [x] Version bump 1.0.0 → 1.0.1 (csproj) + README changelog entry; build green (dll 337,920 bytes, copied to `.tmp/build_mod_dir/PrinterEasyInfo_dev/`).

**Criterion:** user re-tests in-game: one click closes the selection window and opens the Codex entry on top, closable normally.
**Result:** user confirmed in-game ("вроде теперь все ок"). Task archived 2026-09-25.
**Commit:** `fix(printer-easy-info): close scheme window before opening codex entry`
