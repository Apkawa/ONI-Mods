# 2026-09-21_extract-findmethod: implementation plan (вынос FindMethod в UtilLibs)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.

Note: this project has no test suites — the "test" for every stage is the observable gate:
`NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug`
from the repo root, plus grep checks where noted.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Write draft.md (raw input)
- [x] Confirm refinement with user (proceed as-is, scope = all mods in repo)
- [x] Research: FindMethod definitions/call sites, UtilLibs structure, sln membership, build constraints → research.md
- [x] Refine spec.md to v1 (Created header + Changes)
- [x] Review spec with user: class name `ReflectionUtil`, null-logging stays in mods
- [x] Write plan.md

**Criterion:** draft/spec/research/plan exist on branch `2026-09-21_extract-findmethod`; open questions resolved.
**Commit:** `chore(spec): add extract-findmethod spec + plan`

## Stage 1 — ReflectionUtil in UtilLibs
- [x] **Red:** confirm `UtilLibs.ReflectionUtil` does not exist yet (grep) and the two private copies exist (baseline)
- [x] Create `UtilLibs/ReflectionUtil.cs`: namespace `UtilLibs`, `public static class ReflectionUtil`, method `public static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)` — body copied verbatim, unified XML doc comment (drop mod-specific "Stage 2.2" wording)
- [x] **Green:** build the solution — passes
- [x] Refactor: verify `UtilLibs.dll` in bin contains the type; no other files touched

**Criterion:** solution builds; `grep -rn "class ReflectionUtil" UtilLibs/` finds exactly one type with the `public static` method signature.
**Commit:** `refactor(utillibs): add ReflectionUtil.FindMethod shared helper` (accepted by independent check)

## Stage 2 — Refactor BuildDoorOverWall onto ReflectionUtil
- [x] Remove `private static FindMethod` + its XML doc comment from `BuildDoorOverWall/Mod.cs` (lines 226–263 region)
- [x] Update the 8 call sites (`:54, :70, :88, :100, :120, :155, :173, :202`) to `ReflectionUtil.FindMethod(...)`; add `using UtilLibs;` if absent
- [x] Adjust comments that reference the private method (e.g. lines 50, 98, 118, 152, 172, 200) so they still read correctly
- [x] **Green:** build the solution — passes
- [x] Refactor: `grep -n "FindMethod" BuildDoorOverWall/Mod.cs` shows only call sites (no definition)

**Criterion:** BuildDoorOverWall builds; no local FindMethod definition remains; behavior of calls unchanged (identical method bodies).
**Commit:** `refactor(builddooroverwall): use UtilLibs.ReflectionUtil.FindMethod` (accepted by independent check)

## Stage 3 — Refactor ReplaceBuildingMaterial onto ReflectionUtil
- [x] Remove `private static FindMethod` + its XML doc comment from `ReplaceBuildingMaterial/Mod.cs` (lines 233–270 region)
- [x] Update the 10 call sites (`:38, :61, :75, :94, :116, :140, :157, :175, :191, :216`) to `ReflectionUtil.FindMethod(...)`; add `using UtilLibs;` if absent
- [x] Adjust comments that reference the private method (e.g. lines 27, 54, 93, 114, 213) so they still read correctly
- [x] **Green:** build the solution — passes
- [x] Refactor: `grep -n "FindMethod" ReplaceBuildingMaterial/Mod.cs` shows only call sites (no definition)

**Criterion:** ReplaceBuildingMaterial builds; no local FindMethod definition remains.
**Commit:** `refactor(replacebuildingmaterial): use UtilLibs.ReflectionUtil.FindMethod` (accepted by independent check)

## Stage 4 — Phase-1 verification (partial — final acceptance deferred to Stage 9)
- [x] Full build of `ONI-mods.sln` (-c Debug) from repo root — passes
- [x] Repo-wide grep: `FindMethod` definitions exist exactly once (in `UtilLibs/ReflectionUtil.cs`); all 18 mod call sites use `ReflectionUtil.FindMethod`
- [x] Confirm no csproj/sln changes were needed (packing already works via ILRepack on mod side)
- [~] Report to user; in-game acceptance is performed by the user
- Note: the independent final-acceptance run was interrupted by the user who then expanded scope (phase 2). Its checks are re-run (superseded) in Stage 9.

**Criterion:** single source of truth for FindMethod; clean build; user confirms in-game behavior.
**Commit:** N/A (verification only; amend previous commit if a fix is needed)

## Stage 5 — Unified patch helper in UtilLibs (phase 2)
- [x] **Red:** confirm no patch-helper type exists yet in UtilLibs (grep)
- [x] Create `UtilLibs/PatchUtil.cs`: `public static class PatchUtil` with `public static bool TryPatch(Harmony harmony, Type type, string name, Type[] paramTypes, string feature, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)`:
  - resolve via `ReflectionUtil.FindMethod` (keeps byref normalization);
  - auto-built signature string `<TypeName>.<name>(<arg1>, <arg2>, ...)`;
  - null → `PUtil.LogError("could not resolve {0} — {1} skipped (game build mismatch?)".F(sig, feature))` + `#if DEBUG` log + `return false`;
  - else → `harmony.Patch(resolved, prefix, postfix, transpiler)` + `#if DEBUG` log + `return true`.
- [x] **Green:** build the solution — passes (0 errors; 3 new CS8625 at the `= null` defaults — same style as existing code)
- [x] Refactor: verify `UtilLibs.dll` contains the new type; no other files touched

**Criterion:** solution builds; exactly one patch-helper type in UtilLibs with the TryPatch signature.
**Commit:** `feat(utillibs): add PatchUtil.TryPatch resolve+log+patch helper`

## Stage 6 — Refactor BuildDoorOverWall onto PatchUtil.TryPatch
- [x] Rewrite the 8 resolve→null-check→Patch blocks to single `PatchUtil.TryPatch(...)` calls (feature phrases from the existing log texts; patch parts unchanged)
- [x] Adjust/remove now-redundant comments and `#if DEBUG` null-branch logs
- [x] **Green:** build the solution — passes (0 errors)
- [x] Refactor: `grep -c "ReflectionUtil.FindMethod(" BuildDoorOverWall/Mod.cs` is 0; `grep -c "TryPatch(" BuildDoorOverWall/Mod.cs` is 8
- Note: error-sig now auto-built from plain arg types — 3 sites print `string` where old hand-written log said `out string` (diagnostic text only; feature phrase identical).

**Criterion:** BuildDoorOverWall builds; all 8 sites go through the shared helper; no resolve/null-check code left in the mod.
**Commit:** `refactor(builddooroverwall): use PatchUtil.TryPatch`

## Stage 7 — Refactor ReplaceBuildingMaterial onto PatchUtil.TryPatch
- [x] Rewrite the 10 resolve→null-check→Patch blocks to single `PatchUtil.TryPatch(...)` calls (feature phrases from the existing log texts; patch parts unchanged)
- [x] Adjust/remove now-redundant comments and `#if DEBUG` null-branch logs
- [x] **Green:** build the solution — passes (0 errors; −92/+10 lines)
- [x] Refactor: `grep -c "ReflectionUtil.FindMethod(" ReplaceBuildingMaterial/Mod.cs` is 0; `grep -c "TryPatch(" ReplaceBuildingMaterial/Mod.cs` is 10

**Criterion:** ReplaceBuildingMaterial builds; all 10 sites go through the shared helper.
**Commit:** `refactor(replacebuildingmaterial): use PatchUtil.TryPatch`

## Stage 8 — Refactor SizeInTooltip onto PatchUtil.TryPatch
- [x] Rewrite the single `AccessTools.Method` + null-check + Patch block (`Mod.cs:51` region) to `PatchUtil.TryPatch(...)`
- [x] Drop the now-unused `using` (removed `using System.Reflection;`; kept `using HarmonyLib;`; added `using UtilLibs;`)
- [x] **Green:** build the solution — passes (0 errors; −14/+3 lines)
- [x] Refactor: no `AccessTools` references remain in SizeInTooltip; `TryPatch(` count is 1

**Criterion:** SizeInTooltip builds; no AccessTools left; same single patch (prefix on HoverTextConfiguration.DrawInstructions) registered.
**Commit:** `refactor(sizeintooltip): use PatchUtil.TryPatch`

## Stage 9 — Final verification & hand-off (both phases)
- [x] Full build of `ONI-mods.sln` (-c Debug) from repo root — passes (0 errors; 24 pre-existing CS86xx warnings, none in the 4 touched files)
- [x] Repo-wide grep: `FindMethod` definitions exactly once (UtilLibs); all 19 call sites use the shared helper (8 + 10 + 1 `TryPatch(`); zero `AccessTools` in the three mods
- [x] Confirm no csproj/sln changes; packed mod dlls still contain `ReflectionUtil` + the new helper (strings check — all 3 mod dlls contain both types)
- [x] Independent acceptance run of Stage 4 criteria + stage-5..8 criteria
- [x] One-line fix: SizeInTooltip feature phrase `"patch"` → `"size prefix"` (avoids "patch patch attached" in DEBUG log) — commit `407eec3`
- [~] Report to user; in-game acceptance is performed by the user

**Criterion:** single source of truth for resolve+patch; clean build; user confirms in-game behavior of all three mods.
**Commit:** N/A (verification only; amend previous commit if a fix is needed)
