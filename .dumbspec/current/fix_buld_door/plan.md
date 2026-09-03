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
- [ ] Red: static check script `./.tmp/fix_buld_door_checks.sh` fails on current Mod.cs (asserts: no `ExampleMod`, no `LadderConfig` patch, no `Harmony.DEBUG`, no `Console.WriteLine`, no force-valid `IsValidPlaceLocation` prefix, no `InstantBuildReplace` prefix; and `dotnet build ONI-mods.sln -c Debug` must be green at all times)
- [ ] Rewrite `BuildDoorOverWall/Mod.cs`: single entry `UserMod2` class, correct namespace/usings (note research §1.9 — unqualified game-type resolution depends on namespace or explicit usings), remove all dead/test code (LadderConfig patch, commented TileConfig patch, logging patches, debug prints, `Harmony.DEBUG`, template name `ExampleMod`)
- [ ] Implement `DoorConfig.CreateBuildingDef` Postfix: `ReplacementLayer = ObjectLayer.ReplacementTile`, `ReplacementCandidateLayers = { FoundationTile, Backwall }`, `ReplacementTags = { FloorTiles, Backwall, Ladders }`
- [ ] Green: static check passes; `dotnet build ONI-mods.sln -c Debug` green; ILRepack packs UtilLibs/PLib as before

**Criterion:** Mod.cs is a single small file: OnLoad boilerplate + exactly the door-def replacement-metadata patch;
static check passes; build green; no reference anywhere in Mod.cs to ladder-config patching, forced validity,
raw destroys, or debug logging.
**Commit:**

## Stage 2 — Sandbox: instant wall→door replacement (fixes bugs 1 & 2)
- [ ] Red: independent trace (ralph) of `BuildTool.TryBuild` for a door carrying the Stage-1 metadata over a FoundationTile / Backwall occupant; assert each fact line-by-line against Assembly-CSharp; record every gap (expected gaps: `candidate.Def.Replaceable` gate for vanilla foundation/backwall defs; candidate = `BuildingUnderConstruction` plan without working cancel)
- [ ] Implementation: close gaps so that sandbox drag of the door over a wall → `BuildTool.cs:350` replacement fallback → `InstantBuildReplace` → wall candidate destroyed (both door cells via the multi-cell branch) → `def.Build` spawns the door
- [ ] If vanilla foundation/backwall defs are not `Replaceable`, patch their defs (`Replaceable = true`) via config Postfixes — same technique as the donor mod's TilePOI patch
- [ ] Bug 2: when the replacement candidate is a plan (`BuildingUnderConstruction`), cancel it properly — trigger `GameHashes.Cancel` (2127324410) so `Constructable.OnCancel` clears materials/uproots — instead of / before raw destroy, so no orphan Diggables/chores remain
- [ ] Green: static check + full build + independent re-trace confirms: door over foundation → wall gone, door built; door over wall plan → plan cancelled, no orphans; no force-validity bypass remains in Mod.cs

**Criterion:** independent acceptance trace (code-level, against Assembly-CSharp) shows the complete sandbox flow is handled by game code paths with at most the minimal Mod patches; build green.
**Commit:**

## Stage 3 — Survival: queued wall removal + door build
- [ ] Red: independent trace of the survival path: door plan via `BuildingDef.TryReplaceTile` (IsReplacementTile=true), grid write `Grid.Objects[cell, ReplacementLayer]`, then `Constructable.OnCompleteWork`/`FinishConstruction` multi-cell candidate destruction, door over solid ore → `Diggable` per cell (research §2.8)
- [ ] Implementation: close any gaps found (e.g. candidate lookup/anchor-cell mismatch for the 1×2 door; refund of destroyed wall materials via `Deconstructable.SpawnItemsFromConstruction` if the candidate is deconstructable — verify it is)
- [ ] Verify cancel-tool compatibility: `CancelTool`/user-menu cancel on the door replacement plan behaves natively (Diggables removed in `OnCleanUp`)
- [ ] Green: static check + full build + independent re-trace

**Criterion:** independent acceptance trace shows: survival door-over-wall → wall enters removal (dig/deconstruct) queue, door enters build queue, both complete by workers; door-over-rock keeps working as before; build green.
**Commit:**

## Stage 4 — Finalization
- [ ] Full review pass of Mod.cs (naming, comments, no leftovers), confirm csproj/Directory.Build.* untouched (build invariants)
- [ ] Fresh full build; confirm bin/ artifacts: repacked mod dll (UtilLibs+PLib packed), mod.yaml/staticID=BuildDoorOverWall
- [ ] Write in-game verification checklist for the user (Russian), covering: sandbox door-over-foundation, sandbox door-over-backwall, sandbox door-over-wall-plan (bug 2), survival door-over-foundation (queue), survival door-over-rock (regression), ladder placement (regression, LadderConfig patch removed)
- [ ] Update plan journal, archive task per dumbspec layout

**Criterion:** clean full build; repacked dll in bin/; checklist delivered; all earlier-stage checkboxes [x].
**Commit:**
