# fix_buld_door: implementation plan (BuildDoorOverWall rewrite)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.

**Test-adapter note:** the repo has no automated unit-test harness and the game cannot be run
in this sandbox. A stage's "test" is therefore: (a) a static check (shell script/grep in `./.tmp/`,
kept out of the build) asserting observable facts about the code, and (b) a code-level trace of the
game flow against `/home/apkawa/code/ONI_MODS/Assembly-CSharp` performed by an independent agent.
"Red" = the check fails before implementation; "green" = check + `dotnet build ONI-mods.sln -c Debug` pass.
Final in-game verification is a manual step for the user (checklist in Stage 4).

**User journal note:** `checks.md` in the task dir is the user's manual in-game verification journal —
the orchestrator does NOT modify it.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Research (workflow, 3 sections: current mod / game Door+BuildPlan mechanics / donor tile_rep mod) → research.md
- [x] Refine spec.md from draft + research (corrections: no BuildPlan class, LadderConfig = game-class patch, donor is tag-based, root causes of bugs 1–2)
- [x] Write plan.md

**Criterion:** `draft.md`, `research.md`, `spec.md`, `plan.md` exist in the task dir; spec open questions
reduced to 3 implementation-stage questions; no research finding left unwritten.
**Commit:** `docs(fix_buld_door): research, refined spec and implementation plan`

## Stage 1 — Rewrite mod: clean skeleton + door replacement metadata
- [x] Red: static check script `./.tmp/fix_buld_door_checks.sh` fails on current Mod.cs (asserts: no `ExampleMod`, no `LadderConfig` patch, no `Harmony.DEBUG`, no `Console.WriteLine`, no force-valid `IsValidPlaceLocation` prefix, no `InstantBuildReplace` prefix; and `dotnet build ONI-mods.sln -c Debug` must be green at all times)
- [x] Rewrite `BuildDoorOverWall/Mod.cs`: single entry `UserMod2` class, correct namespace/usings (note research §1.9 — unqualified game-type resolution depends on namespace or explicit usings), remove all dead/test code (LadderConfig patch, commented TileConfig patch, logging patches, debug prints, `Harmony.DEBUG`, template name `ExampleMod`)
- [x] Implement `DoorConfig.CreateBuildingDef` Postfix: `ReplacementLayer = ObjectLayer.ReplacementTile`, `ReplacementCandidateLayers = { FoundationTile, Backwall }`, `ReplacementTags = { FloorTiles, Backwall, Ladders }`
- [x] Green: static check passes; `dotnet build ONI-mods.sln -c Debug` green; ILRepack packs UtilLibs/PLib as before

**Criterion:** Mod.cs is a single small file: OnLoad boilerplate + exactly the door-def replacement-metadata patch;
static check passes; build green; no reference anywhere in Mod.cs to ladder-config patching, forced validity,
raw destroys, or debug logging.
**Commit:** `refactor(fix_buld_door): rewrite mod around native replacement metadata`

## Stage 2 — Survival mode: hover-warning fix + flow verification (priority per user)

Context: in-game check after Stage 1 (user journal `checks.md`) — in survival a door replaces wall and
Backwall already. Remaining problem: hovering a door over wall/Backwall shows a false
«free construction space required» warning even though the plan is placed correctly.
User hint: the warning likely comes from the BuildTool visualizer path / `UpdateVis` (build/hover messages).

**Rework record (2026-09-03, crash after first Stage-2 implementation):** the first implementation
bound the two `BuildingDef` postfixes via `[HarmonyPatch(...)]` attributes (6-arg `IsValidPlaceLocation`
and 4-arg `IsValidReplaceLocation`). In-game the game crashed at mod load with
`HarmonyException: Undefined target method ... BuildingDef_IsValidPlaceLocation_ClearFalseReplacementWarning__Patch::Postfix`
(see `~/ONI/logs/.../Player.log`). Root cause (orchestrator, verified): the game's Harmony v2 resolves
attribute targets via `Type.GetMethod(name, allDeclared, null, paramTypes, [])`, which does NOT match an
`out string` parameter against plain `typeof(string)` — only `typeof(string).MakeByRefType()` matches
(empirical .NET test `.tmp/sigdump`; game 0Harmony decompile `.tmp/harmony_decomp`, `DeclaredMethod` :9461).
`MakeByRefType()` is not a legal attribute-argument constant expression (CS0182), so attribute binding
cannot express this. **Fix:** strip the `[HarmonyPatch]` attributes from both `BuildingDef` patch classes
(`PatchAll` only processes types with `HasHarmonyAttribute`) and apply both postfixes programmatically in
`OnLoad` via `harmony.Patch(original, postfix: new HarmonyMethod(...))`, resolving `original` with a
byref-normalized manual matcher (`IsByRef → GetElementType`); loud `Debug.LogError` if a target is not
found. The `DoorConfig` attribute patch (parameterless target) stays as-is (worked in-game).

- [ ] Red: trace the hover/preview path — find the actual method (look for `UpdateVis`/`UpdateVisualizer` in BuildTool.cs; where `fail_reason` from non-replacement `BuildingDef.IsValidPlaceLocation` feeds the tooltip; where the preview color is set, cf. `IsValidReplaceLocation` at BuildTool.cs:178); identify exactly where the «free construction space» warning is emitted for a door hovering over a FoundationTile / Backwall occupant. Write to `./.tmp/stage2_hover_trace.md`: (a) the exact call chain, (b) why the plain validity check fails there, (c) that replacement validity (`replace_tile=true`) succeeds for the same cell (that is the failing check — the warning is false)
- [ ] Red (continued): trace the survival drag path end-to-end with the current metadata-only Mod.cs: `BuildTool.TryBuild` → replacement fallback → `TryReplaceTile` (IsReplacementTile plan) → `Constructable.OnCompleteWork`/`FinishConstruction` candidate destruction (both door cells) + `Def.Build`; plus door-over-rock regression (native Diggable path). Confirm no code change is needed here (works in-game per user); record gaps only if found
- [ ] Green: implement the minimal fix in `BuildDoorOverWall/Mod.cs` (door-def scoped) so the hover preview shows a valid state — no «free space» warning, correct preview color — when a valid replacement candidate exists in the door's cell; the drag path must still reach the replacement fallback (survival `TryReplaceTile` / sandbox `InstantBuildReplace`) and must NOT be diverted to plain `TryPlace`/`def.Build`
- [ ] Green: `./.tmp/fix_buld_door_checks.sh` passes (extend it with new assertions if needed, never weaken existing ones); full build green
- [ ] Refactor: keep the fix minimal and commented (English, game file:line references)

**Criterion:** independent acceptance trace (code-level) shows: (a) hover over foundation/Backwall with the door → the visualizer path no longer emits the «free space» warning (validity true when a replacement candidate is valid); (b) survival drag still takes the replacement path (IsReplacementTile plan) and door-over-rock is unchanged; build green.
**Commit:**

## Stage 3 — Sandbox: instant wall→door replacement (DEFERRED — user focuses on survival first)

Note: the Stage-1 in-game check proved the replacement candidate gate (`Replaceable`/`CanReplace`)
already passes for wall/Backwall in survival; re-verify what specifically is missing in InstantBuild mode
and do not duplicate fixes that Stage 2 already made.

- [ ] Red: independent trace of `BuildTool.TryBuild` in InstantBuild mode for the door def over a FoundationTile / Backwall occupant; assert each fact line-by-line against Assembly-CSharp; record every gap (does the normal gate at BuildTool.cs:325 still pass over a foundation so `def.Build` fires instead of the replacement fallback? what does the multi-cell branch of `InstantBuildReplace` cover/miss?)
- [ ] Implementation: close gaps so that sandbox drag of the door over a wall → `BuildTool.cs:350` replacement fallback → `InstantBuildReplace` → wall candidate destroyed (both door cells) → `def.Build` spawns the door
- [ ] Bug 2: when the replacement candidate is a plan (`BuildingUnderConstruction`), cancel it properly — trigger `GameHashes.Cancel` (2127324410) so `Constructable.OnCancel` clears materials/uproots — before/instead of raw destroy, so no orphan Diggables/chores remain
- [ ] Green: static check + full build + independent re-trace

**Criterion:** independent acceptance trace (code-level) shows the complete sandbox flow: door over foundation → wall gone, door built; door over wall plan → plan cancelled, no orphans; build green.
**Commit:**

## Stage 4 — Finalization
- [ ] Full review pass of Mod.cs (naming, comments, no leftovers), confirm csproj/Directory.Build.* untouched (build invariants)
- [ ] Fresh full build; confirm bin/ artifacts: repacked mod dll (UtilLibs+PLib packed), mod.yaml/staticID=BuildDoorOverWall
- [ ] Prepare in-game verification checklist for the user (Russian) in the final report (user keeps their own journal in `checks.md` — do not modify it), covering: survival door-over-wall (queue), survival door-over-rock (regression), hover warning over wall/Backwall (fixed), sandbox door-over-foundation, sandbox door-over-backwall, sandbox door-over-wall-plan (bug 2), ladder placement (regression, LadderConfig patch removed)
- [ ] Update plan journal, archive task per dumbspec layout

**Criterion:** clean full build; repacked dll in bin/; checklist delivered; all earlier-stage checkboxes [x].
**Commit:**
