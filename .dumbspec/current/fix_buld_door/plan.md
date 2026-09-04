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

**Rework record 2 (2026-09-03, second in-game crash):** the programmatic patching worked (target
resolved) but the postfix signature used `ref string __out_fail_reason`. The game's 0Harmony does NOT
implement the `__out_` postfix convention: in `EmitCallParameter` (~:4444 in `.tmp/harmony_decomp`) any
non-special `__`-prefixed parameter name is parsed as a POSITIONAL index via `int.TryParse`
(`"out_fail_reason"` → "does not contain a valid index" → crash). The both-byref emission branch only
emits a value-load (`Ldarg`), so writing an out-parameter back from a postfix is impossible in this
Harmony build. **Fix:** change the warning-text approach — patch the 4-arg overload
`BuildingDef.IsValidPlaceLocation(GameObject source_go, Vector3 pos, Orientation orientation,
out string fail_reason)` (BuildingDef.cs:1098) instead, with a positional-arg postfix
`(__instance, GameObject __0, Vector3 __1, Orientation __2, ref bool __result)` that sets
`__result = true` under the door gate. That overload feeds ONLY the hover text (BuildToolHoverTextCard.cs:50)
and the visualizer tint (BuildTool.cs:177) — the survival drag path (`TryBuild` → `TryPlace` → 6-arg
overload replace_tile:false → replacement fallback → `TryReplaceTile`) never uses it, so placement
routing is untouched. The tint patch (`IsValidReplaceLocation`) and DoorConfig patch are unchanged.

- [x] Red: trace the hover/preview path — find the actual method (look for `UpdateVis`/`UpdateVisualizer` in BuildTool.cs; where `fail_reason` from non-replacement `BuildingDef.IsValidPlaceLocation` feeds the tooltip; where the preview color is set, cf. `IsValidReplaceLocation` at BuildTool.cs:178); identify exactly where the «free construction space» warning is emitted for a door hovering over a FoundationTile / Backwall occupant. Write to `./.tmp/stage2_hover_trace.md`: (a) the exact call chain, (b) why the plain validity check fails there, (c) that replacement validity (`replace_tile=true`) succeeds for the same cell (that is the failing check — the warning is false)
- [x] Red (continued): trace the survival drag path end-to-end with the current metadata-only Mod.cs: `BuildTool.TryBuild` → replacement fallback → `TryReplaceTile` (IsReplacementTile plan) → `Constructable.OnCompleteWork`/`FinishConstruction` candidate destruction (both door cells) + `Def.Build`; plus door-over-rock regression (native Diggable path). Confirm no code change is needed here (works in-game per user); record gaps only if found
- [x] Green: implement the minimal fix in `BuildDoorOverWall/Mod.cs` (door-def scoped) so the hover preview shows a valid state — no «free space» warning, correct preview color — when a valid replacement candidate exists in the door's cell; the drag path must still reach the replacement fallback (survival `TryReplaceTile` / sandbox `InstantBuildReplace`) and must NOT be diverted to plain `TryPlace`/`def.Build`
- [x] Green: `./.tmp/fix_buld_door_checks.sh` passes (extend it with new assertions if needed, never weaken existing ones); full build green
- [x] Refactor: keep the fix minimal and commented (English, game file:line references)

**Criterion:** independent acceptance trace (code-level) shows: (a) hover over foundation/Backwall with the door → the visualizer path no longer emits the «free space» warning (validity true when a replacement candidate is valid); (b) survival drag still takes the replacement path (IsReplacementTile plan) and door-over-rock is unchanged; build green.
**Acceptance:** independent audit ACCEPT, 10/10 checks PASS (full trace `.tmp/acceptance_stage2.md`): target uniqueness, game-Harmony v2 postfix binding legality (no `__out_`/positional-`__N` misuse — the round-2 crash class), no recursion, hover/drag/rock/vanilla behavior, door-def scope, build green, packed dll with one UserMod2.
**Commit:** 792c568

## Stage 2.1 — Survival: door with the wall in its UPPER (non-anchor) cell (user in-game report)

**User report:** the Stage-2 hover fix is confirmed in-game (no red warning). New bug: a 1×2 door
positioned so that only its UPPER cell sits on the wall (lower cell in air) → build ghost turns red
and the door cannot be placed at all; the inverse (lower cell on wall, upper in air) works.

**Red trace (orchestrator, verified against `.tmp/game_decomp`):**
1. Door `PlacementOffsets` = [(0,0),(0,1)] (`GenerateOffsets`, BuildingDef.cs:1795-1812) →
   **anchor = the door's LOWER cell** (rotations handled by `Rotatable.GetRotatedCellOffset` in
   `RunOnArea`, BuildingDef.cs:819).
2. `GetReplacementCandidate(cell)` is single-cell (BuildingDef.cs:324-346).
3. `BuildTool.TryBuild` (BuildTool.cs:311-387): plain `TryPlace` fails over the wall; the replacement
   fallback (BuildTool.cs:350-385) looks for a candidate **only in the anchor cell**
   (`def.GetReplacementCandidate(cell)`, BuildTool.cs:352). Wall in the upper cell → candidate null →
   fallback skipped → no plan. Context: every vanilla replacement def is 1×1 (foundation, ladder,
   exterior wall, moulding, window), so the anchor-only gate was never exercised by a multi-cell def;
   the door (1×2) is the first.
4. The Stage-2 helper `IsReplacementPlacementPossible` used the same anchor-only lookup → false →
   red tint through both cosmetic postfixes (consistent with the user's red ghost).
5. The rest of the chain already works for this case if a plan is created: the 6-arg
   `IsValidPlaceLocation(replace_tile:true)` passes (per-cell candidate whitelisting,
   BuildingDef.cs:580-585; backwall tolerated when `replacement_tile=true`, `IsValidTileLocation`
   BuildingDef.cs:805-826; air cell clear; logic-port check is conflict-based,
   `AreLogicPortsInValidPositions` BuildingDef.cs:779+); completion takes the game's DESIGNED
   multi-cell path — `Constructable` finds no candidate at the anchor → `FinishConstruction`'s
   `RunOnArea` (Constructable.cs:~225-256) destroys the candidate in each NON-anchor cell
   (DestroySelf / Deconstructable.SpawnItemsFromConstruction returns the metal /
   Trigger(1606648047) / DeleteObject).

**Fix (two parts, both door-def-scoped):**
- A. Helper: anchor-only candidate lookup → area-aware search (`RunOnArea` + single-cell
  `GetReplacementCandidate` per cell, same BuildingComplete/Replaceable/CanReplace gate,
  anchor-first order preserved — offset (0,0) is visited first).
- B. New postfix on `BuildTool.TryBuild(int cell)` (private method → NO attribute, programmatic
  patch in OnLoad, positional `__0`): fires only when TryBuild produced no replacement plan at the
  anchor (checks `Grid.Objects[cell, ReplacementLayer] == null`), door scope, native early-return
  guard mirrored (BuildTool.cs:309: visualizer null / visualizer cell ≠ drag cell when the def has
  LogicPorts/LogicGateBase), survival mode only (instant mode → native InstantBuildReplace, Stage 3
  deferred). Then mirrors the native fallback gate (area-aware candidate with
  BuildingComplete/Replaceable/CanReplace, per-cell `IsReplacementLayerOccupied` clear, native
  element-tag gate incl. the snow-hash quirk, BuildTool.cs:366-371) and creates the plan with the
  exact native tail calls (`def.TryReplaceTile(visualizer, pos, orientation, selectedElements,
  facadeID)` + `Grid.Objects[cell, ReplacementLayer] = plan`, BuildTool.cs:377-379), plus the
  `PostProcessBuild` master-priority mirror (BuildTool.cs:440-448) since the native call already ran
  with a null result.
  **Why NOT patch `GetReplacementCandidate` to be area-aware instead:** that would make Constructable's
  anchor branch (Constructable.cs:161) also find the upper-cell candidate and destroy it in the
  single-cell branch, while `FinishConstruction`'s `RunOnArea` could then re-encounter it (deferred
  `DestroySelf` still in the Grid) and double-handle it (double item spawn). Patching `TryBuild`
  keeps the completion flow on the designed multi-cell path (no candidate at anchor → RunOnArea
  branch), zero new risk.
- [x] Green: implement A + B in Mod.cs (English comments, game file:line references) — direct field access compiled (Publicizer), no reflection fallback needed
- [x] Green: extend `./.tmp/fix_buld_door_checks.sh` (new static checks, existing ones never weakened) + full build green (0 CS errors; only tolerated MSB3027/3021 into read-only ~/ONI)
- [x] Acceptance: independent audit — `.tmp/acceptance_stage21.md`, 5/5 PASS (target scenario, completion exactly-once, all regressions, compile-level, build state). Auditor's note: the Stage-2 4-arg flip also affects the instant gate (BuildTool.cs:325) — in sandbox instant mode a door builds free over a wall without destroying it; that is the deferred Stage-3 scope, not a Stage-2.1 violation

**Criterion:** independent acceptance trace shows: (a) upper-wall door drag → replacement plan queued
at the anchor (IsReplacementTile), completion destroys the upper-cell wall and returns its items;
(b) lower-wall door drag unchanged (native path, no double plan — the postfix bails when a plan
already exists); (c) hover green for both orientations; (d) door-over-rock, vanilla defs, instant
mode untouched; build green.
**Commit:** `fix(fix_buld_door): stage 2.1 — door replacement when the wall is in the upper (non-anchor) cell`

## Stage 2.2 — Generalize to ALL door types + future-proof for DLC/mod doors (user in-game report)

**User report:** Stage 2.1 works for the pneumatic door (Pneumatic Door, `DoorConfig`). Not for:
Wicker Door (`WoodenDoorConfig`, "плетёная дверь", DLC2-gated), Manual Airlock
(`ManualPressureDoorConfig`), Insulated Door (`InsulatedDoorConfig`), Mechanized Airlock
(`PressureDoorConfig`). User wants it to cover all doors and ideally future doors added by DLC or
mods (reference: peterhaneve `AirlockDoor` mod).

**Research (`.tmp/research_stage22.md`):**
- No `BuildCategories` type in the build; "Doors" category membership is hardcoded static lists
  (BuildMenu.cs:151-159, TUNING/BUILDINGS.cs:687-709) — NOT a def property, NOT future-proof.
- No shared door base class; no shared KPrefabID tag (`GameTags.Door` is only ever the value of
  `CopyBuildingSettings.copyGroupTag`, never AddTag-ed onto prefabs, §4).
- Universal door test = composite of the two signals that exist:
  (1) `def.BuildingComplete.GetComponent<CopyBuildingSettings>().copyGroupTag == GameTags.Door` —
  covers all five buildable doors (Door :38, PressureDoor :41, WoodenDoor :60, ManualPressureDoor
  :36, InsulatedDoor :62) + GravitasDoor + peterhaneve's both mod doors (AirlockDoorConfig.cs:123);
  (2) has the native `Door` component (Door.cs:7) — additionally covers BunkerDoor, POI doors
  (non-buildable, harmless). Composite = rank-1 OR rank-2: 6/6 target coverage incl. the mod doors.
- Hook: `Assets.AddBuildingDef(BuildingDef def)` — public static (Assets.cs:670), sole caller
  `BuildingConfigManager.RegisterBuilding` (:114), fires exactly once per def, for every vanilla +
  DLC + mod `IBuildingConfig` (LegacyModMain.cs:26-46 → GeneratedBuildings.cs:24), and AFTER the
  full config chain (`DoPostConfigureComplete`, :104) — so the Door component / copyGroupTag are in
  final state when the door test runs; future DLC/mod doors registered through the same path get
  the metadata automatically. The runtime replacement machinery reads the def fields live
  (`CanReplace` :278, `GetReplacementCandidate` :324, `IsReplacementLayerOccupied` :305 — no caches
  of the replacement fields, §8), and every placed object references the same def instance
  (BuildingLoader.cs:196+), so fields set at def creation are what BuildTool/Constructable consult.
- Native caveat (out of scope, same as vanilla): natural cave backwalls are sim-data only (no
  GameObject in Grid.ObjectLayers) — invisible to the replacement machinery.
- Note: `FindMethod` in Mod.cs scans `BindingFlags.Instance` only — the static hook requires adding
  `BindingFlags.Static` (safe: no C# signature can be both static and instance, so existing
  instance lookups are unaffected).
- WoodenDoor/InsulatedDoor set `Replaceable = false` — that's the CANDIDATE-side flag (they
  themselves cannot be replaced); it does not stop them from replacing walls. Left untouched.

**Fix design:**
- A. New static helper `IsDoorDef(BuildingDef)` (composite door test, see above) in the Mod class.
- B. New attribute-less patch class `Assets_AddBuildingDef_DoorReplacement__Patch`, postfix
  `(BuildingDef __0)` (positional naming — this Harmony build parses `__N`; plain names are not
  assigned): IsDoorDef gate → idempotent skip if `ReplacementLayer != NumLayers` (never override a
  def's own metadata) → set the Stage-1 metadata (`ReplacementLayer = ReplacementTile`,
  `ReplacementCandidateLayers = {FoundationTile, Backwall}`, `ReplacementTags = {FloorTiles,
  Backwall, Ladders}`).
- C. REMOVE `DoorConfig_CreateBuildingDef__Patch` (class + its two `[HarmonyPatch]` attributes): the
  AddBuildingDef hook covers the Door def itself, same values — single source of truth. The file
  then has ZERO attribute-bound patch classes (all four postfixes are programmatic).
- D. Generalize the three `def.PrefabID != DoorConfig.ID` gates (helper, IsValidReplaceLocation tint
  postfix, TryBuild postfix) to `!IsDoorDef(def)`.
- E. `FindMethod` binding flags += `BindingFlags.Static`; OnLoad wiring block for
  `FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef))` + programmatic patch +
  Debug.LogError fallback (same style).
- F. Check script: attribute-count checks 2 → 0; programmatic `harmony.Patch(` count 3 → 4;
  replace the three "'PrefabID != DoorConfig.ID' present 3 times" checks with the IsDoorDef-gate
  checks (no `PrefabID != DoorConfig.ID` anywhere; `!IsDoorDef(` present ≥4 times); new: no
  `DoorConfig_CreateBuildingDef`, AddBuildingDef wiring present, `copyGroupTag == GameTags.Door`
  present, `BindingFlags.Static` present, idempotent guard present. All Stage-2/2.1 checks kept.

- [x] Green: implement A–F in Mod.cs + F in the check script; build green (0 CS errors; ~50 checks PASS)
- [x] Live verification: user in-game — «все работает как я хотел» (all five doors now replace walls). Formal independent audit was interrupted by the user's next bug report and is folded into the Stage 2.2.1 audit

**Criterion:** all five door defs (WoodenDoor only when DLC2 is active) get the replacement metadata
exactly once at creation; vanilla defs unchanged; pneumatic-door behavior byte-identical (same
metadata values, same gates — only the def test generalized); peterhaneve-style mod doors would be
caught by the same hook/test; build green; no regression on the Stage-1/2/2.1 confirmed behaviors.
**Commit:** `fix(fix_buld_door): stage 2.2 — generalize door replacement to all door defs (Assets.AddBuildingDef hook + IsDoorDef gate)`

## Stage 2.2.1 — Fix: wall rendering breaks when a non-pneumatic door is placed lower-in-air / upper-on-wall; persists after cancel

**User report (verbatim, RU):** «Проверил, все работает как я хотел, но всплыл баг: если ставить дверь
(любую кроме обычной пневматической) нижней частью в воздухе, а верхней в стене, то ломается отрисовка
стены (начинает разбиваться на отдельные блоки, т.е. появляются границы у стены, отмена установки не
исправляет отрисовку.). если целиком ставить в стене - то все нормально, если только нижней частью - то
нормально.»

### Red trace (decompile-verified, `.tmp/game_decomp/`)

The broken doors are exactly the tile-piece doors: PressureDoor / WoodenDoor / ManualPressureDoor /
InsulatedDoor all have `IsFoundation = true` → `TileLayer = FoundationTile` → `def.IsTilePiece == true`
(`.tmp/research_stage22.md`). The pneumatic `Door` is `IsFoundation = false` → `TileLayer = NumLayers`
→ not a tile piece. This matches the user's «кроме обычной пневматической».

Chain:
1. Our TryBuild postfix creates the replacement plan (`Constructable`, `IsReplacementTile = true`).
2. `Constructable.OnSpawn` (Constructable.cs:343-346) calls the private no-param `MarkArea()` (:441-461).
3. `MarkArea` writes the plan into `Grid.Objects[cell, ReplacementTile]` at EVERY placement cell
   (`def.MarkArea`, BuildingDef.cs:829-843) — intended; the game unwinds it on cancel
   (`Constructable.UnmarkArea`, :463-477, replacement layer only).
4. Then the tile-piece branch: `if (!def.IsTilePiece) return;` (:448) — passes for the 4 tile-piece
   doors, returns for the pneumatic.
5. Branch guard `Grid.Objects[anchor, TileLayer] == null` (:452) is true when the anchor cell is air
   (case B) or a backwall cell (case A / fully-in-wall).
6. Inside: `def.MarkArea(anchor, orientation, def.TileLayer, plan)` (:454) writes the plan into
   `Grid.ObjectLayers[FoundationTile]` at EVERY door cell unconditionally (BuildingDef.cs:829-843) —
   including the backwall cell, which never holds a foundation tile (backwalls live in the separate
   `BackwallManager.backwallElement` array; `Grid.Element` is the main element, Grid.cs:732) — breaking
   the native invariant. Plus `TileVisualizer.RefreshCell` at every cell and
   `Grid.IsTileUnderConstruction[anchor] = true` (:460, anchor only).
7. The plan's own `SimCellOccupier.OnSpawn` (:65-98) additionally sets `Grid.RenderedByWorld[cell] = false`
   at every placement cell, including the wall cell.
8. World-mesh state is per-cell: `World.cs:99` recomputes `RenderedByWorld` only for cells in that
   tick's `solid_substance_change_info`, via
   `substance.renderedByWorld && (Grid.Objects[cell, 9] == null || Grid.IsTileUnderConstruction[cell])`.
   At the wall cell (non-anchor): layer-9 entry = plan (non-null) + `IsTileUnderConstruction = false`
   → the world mesh stops drawing the cell as part of the connected wall surface → the wall splits into
   per-cell blocks. The backwall quads (`GroundRenderer.cs` biomeChecks, :256-272) come from the
   BackwallManager and are NOT gated by RenderedByWorld, so the seams stay visible.
9. On cancel the state is never unwound: `Constructable.UnmarkArea` never clears the FoundationTile entry
   (asymmetric with MarkArea), and `SimCellOccupier.DestroySelf` (:143-181) restores the main element +
   clears cell properties but never restores `RenderedByWorld` and never refreshes the cell → the
   corruption persists.
10. Case A (anchor ON the wall) survives today because `IsTileUnderConstruction[anchor] = true`
    (anchor = the wall cell) keeps the World.cs:99 formula consistent.

### Fix design

New nested patch class `Constructable_MarkArea_DoorTileUnwind__Patch` (NO `[HarmonyPatch]` attributes —
the file keeps ZERO attribute-bound classes), a prefix+postfix pair on the private no-param
`Constructable.MarkArea()`. Restores the native anchor-gated invariant: the plan occupies the tile layer
only at cells that already held a foundation tile (real replacement candidates), never at
backwall/air cells.

- A. OnLoad wiring (5th programmatic patch):
  `MethodInfo markArea = FindMethod(typeof(Constructable), "MarkArea");` → null → Debug.LogError
  fallback (same style as the other four); else `harmony.Patch(markArea, prefix: ..., postfix: ...)`
  with `nameof(Constructable_MarkArea_DoorTileUnwind__Patch.Prefix)` / `nameof(...Postfix)`.
- B. `Prefix(Constructable __0)`: first reset static `s_preMarkTileOccupied = null`. Gates:
  `__0 != null && __0.IsReplacementTile` (public field, Constructable.cs:64) &&
  `building = __0.GetComponent<Building>() != null` && `def = building.Def != null` &&
  `IsDoorDef(def)` && `def.IsTilePiece`. Then capture pre-MarkArea tile-layer occupancy:
  `anchor = Grid.PosToCell(__0.transform.GetPosition())`;
  `def.RunOnArea(anchor, building.Orientation, c => { if (Grid.Objects[c, (int)def.TileLayer] != null) s_preMarkTileOccupied.Add(c); });`
  try/catch + Debug.LogError; on error reset the static to null.
- C. `Postfix(Constructable __0)`: read + reset the static (null → not applicable, return).
  `anchor = Grid.PosToCell(__0.transform.GetPosition())`; per door cell via
  `def.RunOnArea(anchor, building.Orientation, c => ...)`: if
  `Grid.Objects[c, (int)def.TileLayer] == __0.gameObject` AND the cell is NOT in preMark →
  `Grid.Objects[c, (int)def.TileLayer] = null;` +
  `TileVisualizer.RefreshCell(c, def.TileLayer, def.ReplacementLayer);` (RefreshCell already refreshes
  the 4 neighbors). If the anchor is NOT in preMark (the branch ran and wrote at the anchor) →
  `Grid.IsTileUnderConstruction[anchor] = false;`. try/catch + Debug.LogError.
- D. Cancel needs no extra patches: the replacement-layer entry is unwound by the game's `UnmarkArea`;
  the tile layer was never written at the wall cell; `RenderedByWorld` self-heals via the World.cs:99
  formula on the next solid-change sim tick (the plan's `SetCellProperties` at placement and
  `DestroySelf` at cancel both trigger sim ticks at the wall cell).
- E. Mixed case preserved: door anchored in air with its upper cell on a REAL foundation tile (e.g. a
  floor) — preMark contains that cell → its tile-layer entry is left in place (native replacement flow
  untouched).
- F. Check script: `harmony.Patch(` count 4 → 5; `[HarmonyPatch]` still 0; new checks: class
  `Constructable_MarkArea_DoorTileUnwind__Patch` present, `FindMethod(typeof(Constructable), "MarkArea")`
  wiring present, `IsReplacementTile` gate present, `IsTileUnderConstruction` present,
  `s_preMarkTileOccupied` present, MarkArea LogError fallback present. All Stage-2/2.1/2.2 checks kept.

### Crash #1 (first live test of Stage 2.2.1) — receiver parameter naming

First build named the receiver `__0` (`Prefix(Constructable __0)` / `Postfix(Constructable __0)`).
In this 0Harmony build `__N` resolves as a POSITIONAL index into the original method's
DECLARED parameters (0Harmony.decompiled.cs:4453 throws "No parameter found at index N");
`MarkArea()` has ZERO declared parameters → `harmony.Patch` threw at patch-creation time in
`OnLoad` (Player.log 2026-09-04: `System.Exception: No parameter found at index 0` at
`AddPrefixes`, `Mod.OnLoad`) → the mod load failed → the game crashed at startup.
Fix: receiver renamed to the special name `__instance` (INSTANCE_PARAM,
0Harmony.decompiled.cs:4895; `InjectionType.Instance` → `Ldarg_0`, valid for prefixes and
postfixes alike) — the same convention as the TryBuild/hover patches in this file.
Regression checks added to the check script: the MarkArea patch class must use `__instance`
(≥4 uses) and contain no `__0`.

- [x] Red: trace above (root cause + all member accessibilities verified)
- [ ] Green: implement A–F in Mod.cs + the check-script updates; build green; check script `== RESULT: PASS`
- [ ] Independent acceptance ralph: audit Stage-2.2 invariants + the 2.2.1 fix (report → `.tmp/acceptance_stage221.md`)
- [ ] Live: user in-game test matrix (RU handoff)

**Criterion:** door replacement plans never write the plan object into
`Grid.ObjectLayers[FoundationTile]` at cells holding no foundation tile (backwall/air) — code-level
verified; cancel leaves no stale per-cell state (tile layer / RenderedByWorld / IsTileUnderConstruction);
in-game: wall renders intact for all 5 doors × {upper-on-wall, lower-on-wall, both-on-wall,
cancel-after-placement} matrix, no persistent breakage; no regression on Stage 1/2/2.1 behaviors.
**Commit:**

## Stage 3 — Sandbox: instant wall→door replacement (DEFERRED — user focuses on survival first)

Note: the Stage-1 in-game check proved the replacement candidate gate (`Replaceable`/`CanReplace`)
already passes for wall/Backwall in survival; re-verify what specifically is missing in InstantBuild mode
and do not duplicate fixes that Stage 2 already made.

Carry-over from Stage-2 acceptance: the Stage-2 hover patch now makes the 4-arg `IsValidPlaceLocation`
return true for the door over a valid replacement candidate — and the sandbox instant branch
(`BuildTool.TryBuild` else-branch at BuildTool.cs:325: `def.IsValidBuildLocation(...) &&
def.IsValidPlaceLocation(4-arg)` → `def.Build`) consumes exactly that overload. The Stage-3 trace
must re-verify whether the instant door-over-wall drag now diverts into `def.Build` (wall NOT destroyed)
instead of the replacement fallback → `InstantBuildReplace`, and whether `IsValidBuildLocation` even
passes over an occupied cell in that case. Also: the instant replacement fallback additionally gates on
`IsValidBuildLocation(replace_tile:true)` (BuildTool.cs:379), which the shared helper does not check —
acceptable in survival (no such gate in the `!flag` branch), must be reconciled for sandbox.

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
