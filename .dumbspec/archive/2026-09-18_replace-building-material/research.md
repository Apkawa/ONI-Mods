# Research notes: 2026-09-18_replace-building-material

Collected 2026-09-18 via research subagent. Game build: Aquatic 731233. Decompiled sources at `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/`.

## R1. BuildDoorOverWall mechanics (reference mod)

- Project: `BuildDoorOverWall/` — single file `Mod.cs` (~1485 lines), `Mod : UserMod2`, `OnLoad(Harmony)`; all patches attached **programmatically** via `harmony.Patch(methodInfo, prefix:/postfix:)` (no `[HarmonyPatch]` attributes) using a reflection `FindMethod` helper (byref-normalizing, for `out string` params).
- Patches (all in `Mod.cs`):
  - postfix `BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)` (4-arg) — cosmetic hover tint.
  - postfix `BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` — cosmetic hover tint.
  - postfix private `BuildTool.TryBuild(int)` — actual placement for the door-over-wall case where the candidate sits in a non-anchor cell.
  - postfix `Assets.AddBuildingDef(BuildingDef)` — **metadata injection**: on def load, assigns directly on the def object: `ReplacementLayer = ObjectLayer.ReplacementTile`, `ReplacementCandidateLayers` (static shared list), `ReplacementTags` (static shared live list; each door's own pref tag appended to it), `Replaceable = true`.
  - postfix 6-arg canonical `BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool)`.
  - postfix `BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string)`.
  - **prefix** `Constructable.FinishConstruction(UtilityConnections, WorkerBase)`.
  - **prefix** `BuildingDef.TryReplaceTile(...)`.
- Gate helpers in the mod: `TryReplacementGate(def, source_go, cell, orientation, out reason)` mirrors the native `BuildTool.TryBuild` fallback gates; `TryFindReplacementCandidate` is area-aware (replaces the native single-cell `GetReplacementCandidate`); `ReplacementLayerOccupiedExcludingSelf` is source_go-aware.
- "Same material" skip: candidate found but `candidate.Def == def && selected[0] == tag` → no-op (Mod.cs:996-1002).
- Placement tail (mod mirrors native BuildTool.cs:377-378): `GameObject plan = def.TryReplaceTile(visualizer, pos, orientation, selected, facadeID); Grid.Objects[anchorCell, (int)def.ReplacementLayer] = plan;`
- PLib: only `PUtil.LogDebug/LogError` + `.F(...)` (PeterHan.PLib.Core via UtilLibs).

## R2. Game placement validation (Aquatic 731233)

- **No `Minimap` class in this build.** Placement goes through `BuildTool : DragTool` (`BuildTool.cs`), used by both survival (`BuildMenu.cs:908`) and plan screen (`PlanScreen.cs:1820`): `BuildTool.Instance.Activate(buildingDef, materialSelectionPanel.GetSelectedElementAsList, facadeID)`.
- Chain: `DragTool.OnLeftClickDown` → `DragTool.OnDragTool(cell, dist)` → `BuildTool.OnDragTool` → **private `BuildTool.TryBuild(int cell)`** (BuildTool.cs:307-388).
- `TryBuild` → `BuildingDef.TryPlace(visualizer, pos, orientation, selectedElements, facadeID)` (BuildingDef.cs:464; 3 overloads funnel here) → `IsValidPlaceLocation(..., replace_tile: false, out _, restrictToActiveWorld)` → `Instantiate(pos, orientation, selected_elements, layer)`.
- **6-arg canonical validity**: `BuildingDef.IsValidPlaceLocation(GameObject source_go, int cell, Orientation orientation, bool replace_tile, out string fail_reason, bool restrictToActiveWorld)` (BuildingDef.cs:1120-1181): `Grid.IsValidBuildingCell`, active world, `BuildLocationRule`/`CheckFoundation` branches, then `IsAreaClear(...)`.
- **`IsAreaClear` (private, BuildingDef.cs:547-790) is the blocker**: per placement cell (rotated `PlacementOffsets`), ObjectLayer occupancy check — a second building on the same ObjectLayer cell fails with `UI.TOOLTIPS.HELP_BUILDLOCATION_OCCUPIED` (BuildingDef.cs:604) unless: occupant is the replacement candidate itself (`replace_tile == true` && `gameObject == gameObject2`), uprootable plant, or wire-over-wire. Also tile-layer occupancy (608-613), AttachableBuilding layers 39/38 (616-634), power/conduit/logic port checks (789).
- **Native replacement fallback** in `BuildTool.TryBuild` (BuildTool.cs:350-386), only when `TryPlace` returned null **and** `def.ReplacementLayer != ObjectLayer.NumLayers`:
  - `GameObject replacementCandidate = def.GetReplacementCandidate(cell);` — **single anchor cell only**.
  - `def.RunOnArea(cell, orientation, c => def.IsReplacementLayerOccupied(c))` → if occupied, abort.
  - `component = replacementCandidate.GetComponent<BuildingComplete>(); if (component != null && component.Def.Replaceable && def.CanReplace(replacementCandidate))` → element/same-material gate (366-371: skip if `candidate.Def == def && selectedElements[0] == candidate element`) → `def.TryReplaceTile(visualizer, pos, orientation, selectedElements, facadeID)` (376) + `Grid.Objects[cell, (int)def.ReplacementLayer] = plan` (377).
- Instant-build branch (BuildTool.cs:325-348): `IsValidBuildLocation && IsValidPlaceLocation` → `def.Build(cell, ...)`; instant replacement via `InstantBuildReplace` (BuildTool.cs:390-431) → `SimCellOccupier.DestroySelf` + `def.Build` in callback.
- `BuildTool.PostProcessBuild` (433-478).

## R3. Native "replace" machinery (game already does build-over + demolish)

- Driven by def metadata: `BuildingDef.ReplacementLayer`, `ReplacementCandidateLayers`, `ReplacementTags`, `EquivalentReplacementLayers`, `Replaceable` (used today by floors-over-walls, ladders, foundation tiles, conduits, windows).
- **Plan creation**: `TryReplaceTile` (BuildingDef.cs:487-527) checks `IsValidPlaceLocation(..., replace_tile: true)`, sets `Constructable.IsReplacementTile = true` on the pref before `Instantiate`, false after.
- **`Constructable.OnSpawn`** (Constructable.cs:322-407): for `IsReplacementTile` with `ReplacementLayer != NumLayers` — occupies the replacement slot itself: `Grid.Objects[cell,(int)ReplacementLayer] = base.gameObject` (381-391), `blockTileRenderer.AddBlock(... isBlueprint:true)`, and **cancels in-progress deconstruction** of the existing occupant (`Grid.Objects[cell,(int)Def.ObjectLayer]` → `Deconstructable.CancelDeconstruction()`, 397-404).
- **Start re-check**: `Constructable.PlaceDiggables` (655-768) → `building.Def.IsValidBuildLocation(base.gameObject, pos, orientation, IsReplacementTile)` (747); on failure: "invalid location" status + `buildChore.Cancel("Need to dig")`.
- **Completion — `Constructable.OnCompleteWork(WorkerBase)`** (124-221): blended temp from fetched items; for `IsReplacementTile`: `GetReplacementCandidate(cell)` (161); anchor cell: `SimCellOccupier.DestroySelf(() => FinishConstruction(...))` (165-175); non-anchor: `Conduit.MarkForReplacement` / `BuildingComplete.Subscribe(-21016276, FinishConstruction)` (178-190); then **`replacementCandidate.GetComponent<Deconstructable>().SpawnItemsFromConstruction(worker)` (202-206) — refunds the old building's construction items to the worker**, `Trigger(1606648047, ReplaceCallbackParameters{TileLayer, Worker})` (211, OnObjectReplaced event), `DeleteObject()` (213).
- **`Constructable.FinishConstruction(UtilityConnections, WorkerBase)`** (223-291): for multi-cell plans a `RunOnArea` sweep (228-257) **destroys candidates in every NON-anchor cell** (same refund + event + DeleteObject pattern); then `UnmarkArea()`; real building created via `building.Def.Build(cell, orientation, storage, selectedElementsTags, initialTemperature, facade, ...)` (259); `storage.ConsumeAllIgnoringDisease(); this.DeleteObject();` (288-290).
- `BuildingComplete`: `replacingTileLayer` (26), `WasReplaced()` (44-47), event 1606648047 handler (124), post-replacement handling (257-286).
- `Deconstructable`: `public Tag[] constructionElements` (25), `SpawnItemsFromConstruction(WorkerBase)` (327/336), `CancelDeconstruction()` (414).
- Manual demolition is separate: `Demolishable : Workable` (`Demolishable.cs:7`, user menu "Demolish", `WorkChore<Demolishable>(Db.Get().ChoreTypes.Demolish, ...)`). Not part of the build-over flow.
- **Same-material no-op already exists natively**: BuildTool.cs:371 gate — `candidate.Def == def && selectedElements[0] == candidate element tag` → replacement skipped.

## R4. Building materials

- Element is a **Tag** (no int material ID). Def-side: `BuildingDef.MaterialCategory` (string[], e.g. `MATERIALS.ALL_METALS`, BuildingDef.cs:131), parallel `Mass` (137); `BuildingDef.PostProcess()` (1816-1838) builds `CraftRecipe` (Recipe) from MaterialCategory+Mass; `Recipe.GetAllIngredients(IList<Tag> selectedTags)` substitutes player-selected tags into the first N ingredient slots.
- `BuildingDef.DefaultElements()` (375-388) / `DefaultElementsWithPrimary` (390-395); `MaterialsAvailable` (1840-1853) checks world inventory.
- Player selection: `ProductInfoScreen.materialSelectionPanel.GetSelectedElementAsList` → `MaterialSelectionPanel.GetSelectedElementAsList` (line 64) → `MaterialSelector.CurrentSelectedElement` (line 20); `MaterialSelector.GetValidMaterials(Tag materialTypeTag)` (line 90).
- On placed object: `PrimaryElement.ElementID` set from `selected_elements[0]` in `BuildingDef.Create` (364-367) / `Instantiate` (534-536); `Constructable.SelectedElementsTags` (serialized, 78-86) carries the chosen list through construction; `BuildingDef.Build` copies to `Deconstructable.constructionElements` (435-443); `Constructable.OnCompleteWork` reads item mass/temperature (129-154).
- Facade is a separate cosmetic choice (`BuildTool.facadeID`, `ApplyBuildingFacade`).

## R5. Mod project structure (repo conventions)

- Repo root: `BuildDoorOverWall/`, `SizeInTooltip/`, `UtilLibs/`, `PublicisedAssembly/`, `ONI-mods.sln`, `Directory.Build.props` (+`.default`/`.user`), `Directory.Build.targets`, AGENTS/CONTRIBUTING/README. `UpdatedOniTemplate/` does **not** exist in the working tree (only a zipped upstream template in example_mods).
- Mod folder = `X.csproj` + `Mod.cs` (single file: `UserMod2` + patch classes) + README/AGENTS. `BuildDoorOverWall.csproj`: `net48`, `PackageId`/`AssemblyName`/`RootNamespace=$(PackageId)`, `<IsMod>true</IsMod>`, `GenerateMetadata=true`, `IsPacked=true` (ILRepack), Release OutDir=bin; **only** reference: `..\UtilLibs\UtilLibs.csproj` (game DLLs via shared `Directory.Build.props`).
- `UtilLibs`: net48, `IsMod=false`, `DoNotBuildAsMod=true`, `IsPacked=false`, `PackageReference PLib 4.19.0` — shared PLib carrier (keep `IsPacked=true` in mods so PLib packs in).
- `ONI-mods.sln`: currently BuildDoorOverWall + UtilLibs + **SizeInTooltip** (note: AGENTS.md says only the first two — SizeInTooltip is actually present). Adding a project = `Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}")` line + 4 config lines; sln parser has no comment support.
- `Directory.Build.props`: game refs with `<Publicize>true</Publicize>` (Assembly-CSharp, -firstpass, 0Harmony game v2 Private=False, UnityEngine, Newtonsoft); Publicizer 0.4.3 (IsMod=true, assets `build; contentfiles`); net48 ref assemblies 1.0.3; dotnet-ilrepack 2.0.45 (IsPacked=true).
- `Directory.Build.targets`: Clean before build; `GenerateModYaml` (title/description/staticID, Debug gets `[DEBUG]`+`_dev`); `GenerateModInfoYaml`; `ILRepack` after Build; `CopyModsToDevFolder` (fails read-only in sandbox — expected).
- Loader: `KMod.DLLLoader` loads every `*.dll` per mod folder, ≤1 `UserMod2` subclass per assembly, one `Harmony` per mod folder.

## R6. Key implication for the mod (facts, not design)

- The game's native replacement machinery (R3) already performs exactly the required end-state: old building destroyed at construction completion, its construction items refunded to the worker, new building built from `selectedElementsTags` (the player's material choice). It is gated purely by def metadata (`ReplacementLayer`/`ReplacementCandidateLayers`/`ReplacementTags`/`Replaceable` + `CanReplace`) and by the validations in R2.
- The native fallback in `BuildTool.TryBuild` (R2) only inspects the **anchor cell** (`GetReplacementCandidate(cell)`); BuildDoorOverWall already needed an area-aware re-implementation for a 1x2 door — multi-cell buildings will face the same limitation (R1, R2).
- `Constructable.OnSpawn` (R3) overwrites `Grid.Objects[cell,(int)ReplacementLayer]` with the plan and cancels in-progress deconstruction of the occupant on `Def.ObjectLayer`; `PlaceDiggables` re-runs `IsValidBuildLocation(..., IsReplacementTile)` at construction start and cancels the chore on failure.
- Open technical uncertainty (needs deep research as a plan stage): the door mod uses a **separate** `ReplacementLayer = ObjectLayer.ReplacementTile` because doors replace walls/foundations on different layers; a building replacing a same-pref building means the plan and the old occupant share the **same** ObjectLayer — the interaction of `OnSpawn` slot-occupation, `IsAreaClear` occupancy checks, and `GetReplacementCandidate` in that case is not established by the above facts.

## R7. Deep trace: native replacement mechanics (deep-research task A, 2026-09-18)

All paths under `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/` unless noted. ObjectLayer enum values (ObjectLayer.cs, implicit from 0): `Building=1, Backwall=2, Plants=5, DigPlacer=7, FoundationTile=9, PlasticTile=10, ReplacementTile=11, GasConduitConnection=15, LiquidConduitConnection=19, SolidConduitConnection=23, WireTile=27, WireConnectors=29, AttachableBuilding=38, Gantry=39, ReplacementBackwall=43, NumLayers=45`.

### R7.1 Plan slot occupancy

- **`BuildingDef.Instantiate` (529-540) writes no `Grid.Objects` slots.** It instantiates `BuildingUnderConstruction`, sets `PrimaryElement`/`SelectedElementsTags`, activates; grid writes happen inside KInstantiate/Activate (OnPrefabInit/OnSpawn) via `MarkArea`, completed by the time `TryReplaceTile` returns.
- **`Constructable.OnSpawn` (322-427) Grid.Objects writes:**
  - Via `MarkArea` (Constructable.cs:343-346 rotatable, 299-302 non-rotatable): `ObjectLayer layer = (IsReplacementTile ? def.ReplacementLayer : def.ObjectLayer); def.MarkArea(num, orientation, layer, base.gameObject);` (446-447).
  - `BuildingDef.MarkArea` (829-939) writes: main slot **every placement cell** `Grid.Objects[cell2,(int)layer] = go` (842); for a replacement plan `layer == ReplacementLayer`. An uprootable occupant of the same slot is evicted to the Plants layer first (838-841). Plus port slots: conduit in/out (851/859), power in/out layer 29 (866/872), WireBridge/HighWatt (880-881), LogicBridge (894), secondary conduits (913/935).
  - Tile-piece extra (Constructable.cs:448-460): claims `def.TileLayer` slot only if empty (452-455); `Grid.IsTileUnderConstruction[num] = true` (460).
  - Anchor-only IsReplacementTile branch (375-407), gated `IsReplacementTile` (375) + `ReplacementLayer != NumLayers` (378): re-write `Grid.Objects[cell,(int)ReplacementLayer] = base.gameObject` if slot null/self (381-384); else logs "multiple replacement tiles on the same cell!" and destroys the new plan (392-396); `blockTileRenderer.AddBlock(..., isBlueprint:true)` (385-389); reads ObjectLayer slot (397) → `Deconstructable.CancelDeconstruction()` of old occupant (398-404).
  - `PlaceDiggables` (655-768) writes `Grid.Objects[offset_cell,7] = diggable` (697) **only inside `if (!IsReplacementTile)`** (670-746) — replacement plans get no diggables.
- **`BuildTool.TryBuild:377`** re-writes `Grid.Objects[cell,(int)def.ReplacementLayer] = plan` after `TryReplaceTile` (redundant with OnSpawn; indexer setter re-fires partitioner event, Grid.cs:443-462).
- **Net (floor-over-wall case):** plan sits in `Def.ReplacementLayer` for ALL placement cells during construction, NOT in its own `Def.ObjectLayer`; the OLD building stays in `Grid.Objects[its cell, its ObjectLayer]` for the entire construction (OnSpawn only cancels deconstruction); the old slot is taken over by the new building's `Def.Build → MarkArea` (BuildingDef.cs:416→842).

### R7.2 Candidate survival at completion

- **`GetReplacementCandidate` (BuildingDef.cs:324-345):** if `ReplacementCandidateLayers != null` — scans `Grid.ObjectLayers[layer]` per layer, requires occupant **non-null and has `BuildingComplete`** (333); no tag check here. Else (null) — returns `TileLayer` occupant with no component check (340-343). Single-cell; multi-cell callers iterate themselves. `ReplacementTags` is only used by `CanReplace` (279-285).
- **At `OnCompleteWork` time the old building is still fully grid-registered** (line 161 `GetReplacementCandidate(cell)` expected to find it; nothing deletes it between OnSpawn and OnCompleteWork). Anchor sequence (158-214): `SimCellOccupier.DestroySelf(() => FinishConstruction(...))` deferred (165-175); non-SimCellOccupier fallback: `Conduit.GetFlowManager().MarkForReplacement(cell)` + `BuildingComplete.Subscribe(-21016276, FinishConstruction)` (176-196); `skipCleanup` on candidate's `KAnimGraphTileVisualizer` (197-201); **refund** `SpawnItemsFromConstruction(worker)` (202-206); `Trigger(1606648047, ReplaceCallbackParameters{TileLayer = building.Def.TileLayer, Worker})` (207-212) sets `replacingTileLayer` (BuildingComplete.cs:26/124-128) making `WasReplaced()` (44-47) true iff `TileLayer != NumLayers`; `DeleteObject()` (213).
- **Who clears the old ObjectLayer slot:** `BuildingComplete.OnCleanUp` (241-301) unmarks its own ObjectLayer slot on destroy **unless `WasReplaced()`** (257-260). Consequences:
  - **Same-pref building replacement (TileLayer == NumLayers):** `replacingTileLayer = NumLayers` → `WasReplaced() == false` → old building's `OnCleanUp` DOES clear its ObjectLayer slot at `DeleteObject` (213/253), before the new `Def.Build` marks it. Clean.
  - **Replaced tile-pieces / defs with `TileLayer != NumLayers`:** `WasReplaced() == true` → unmark skipped; old `ObjectLayers` entry dangles pointing at the destroyed GO until the new building's `MarkArea` overwrites it. `GetReplacementCandidate` tolerates dangling entries (`GetComponent` on destroyed GO → null → returns null, 333).
- **Non-anchor cells in `FinishConstruction` (223-291):** RunOnArea sweep (228-257) gated `IsReplacementTile && PlacementOffsets.Length > 1`; calls **`GetReplacementCandidate(offset_cell)` per cell** (234); per found candidate: `SimCellOccupier.DestroySelf(null)` (240, no deferred callback), `SpawnItemsFromConstruction` (245), `Trigger(1606648047, ...)` (251), `DeleteObject()` (253). Differences from anchor: no `Conduit.MarkForReplacement`/`Subscribe` fallback, no `skipCleanup`. Then `UnmarkArea()` (258, clears plan's ReplacementLayer slot), `Def.Build(cell, orientation, storage, selectedElementsTags, ...)` (259), `storage.ConsumeAllIgnoringDisease(); this.DeleteObject();` (288-290).

### R7.3 Start re-check with the plan as source

- `PlaceDiggables:747` → 4-arg `IsValidBuildLocation` (1209-1213) → **5-arg `IsValidBuildLocation` (1221-1359)**. **This chain does NOT call `IsValidPlaceLocation` (1120) nor `IsAreaClear` (547)** — the start re-check cannot produce `HELP_BUILDLOCATION_OCCUPIED` at all.
- 5-arg body: `Grid.IsValidBuildingCell` (1223) → `HELP_BUILDLOCATION_INVALID_CELL`; `IsAreaValid` (1228, 1361+ — per-offset validity only); `BuildLocationRule` switch (1234-1357): foundation/`CheckFoundation` failures (FLOOR/WALL/CORNER/CORNER_FLOOR/SPACE/ONROCKETENVELOPE); `NotInTiles` (1285-1307): `flag = (replace_tile || gameObject2 == null || gameObject2 == source_go) && !Grid.HasDoor[cell]` (1288, FoundationTile layer 9); ObjectLayer occupant clause `component3 == null || component3.Def.ReplacementLayer == ReplacementLayer` (1301) — **passes for same-pref** (identical def); `Tile` rule: layer-27 occupant with NotInTiles rule → `flag = false` **with no fail_reason assigned** (1315-1318, decompiled caveat); Backwall occupant with NotInTiles rule → `flag = replace_tile` (1326); `BuildingAttachPoint` → `HELP_BUILDLOCATION_ATTACHPOINT` (1331-1351); `Anywhere/Conduit/OnFloorOrBuildingAttachPoint` → true (1352-1356). Final: `flag && ArePowerPortsInValidPositions && AreConduitPortsInValidPositions` (1358).
- The plan's own ReplacementLayer slot **cannot** cause a start-re-check failure (no `ReplacementLayer` occupancy check in `IsValidBuildLocation`; `IsReplacementLayerOccupied` is called only from `BuildTool.TryBuild:354-360` before the plan exists).
- Fail reasons still possible for same-position same-pref (all about surroundings, unchanged by replacement): INVALID_CELL, FLOOR/WALL/CORNER/CORNER_FLOOR/SPACE/ONROCKETENVELOPE, NOT_IN_TILES (via `Grid.HasDoor[cell]`), silent layer-27 failure, ATTACHPOINT, port overlaps (`WIRECONNECTORS_OVERLAP` 1391-1421 layer 29, `GASPORTS_OVERLAP` 1632, `LIQUIDPORTS_OVERLAP` 1642, `SOLIDPORTS_OVERLAP` 1652 via `IsValidConduitConnection` 1621-1658) — each with `!= source_go` self-exclusion covering the plan's own port marks.
- **Placement-time** `TryReplaceTile` → `IsValidPlaceLocation(src_go = BuildTool.visualizer, ..., replace_tile: true)` (490 → 1120-1182 → `IsAreaClear` 1181) DOES run 586-614: per cell `gameObject = GetReplacementCandidate(num)` (580-582); `gameObject2 = Grid.Objects[num,(int)layer]` (586) = old building; failure at 602-607 (`HELP_BUILDLOCATION_OCCUPIED`) is prevented by clause `(gameObject == null || gameObject != gameObject2)` (candidate == occupant, line 602). Tile-layer check (608-613) skipped for non-tile-piece defs; when active requires `(gameObject == null || gameObject == source_go)`.

### R7.4 Every `IsReplacementTile` read

`BuildTool.cs` has zero reads (fallback infers from `def.ReplacementLayer != ObjectLayer.NumLayers`, 350). Constructable.cs: 64 (field), 158 (OnCompleteWork gate), 228 (FinishConstruction non-anchor sweep gate), 375 (OnSpawn slot occupation + blueprint block + deconstruction cancel), 388 (blockTileRenderer.AddBlock key), 446 (MarkArea layer choice), 470 (UnmarkArea layer choice), 549/555 (OnCleanUp RemoveBlock), 607 (OnDiggableReachabilityChanged early return), 670 (PlaceDiggables skip diggables), 747 (start re-check `replace_tile` arg). BuildingDef.cs: 493/495 (writes — `TryReplaceTile` sets the flag on `BuildingUnderConstruction` around `Instantiate` so OnPrefabInit/OnSpawn mark the plan in the ReplacementLayer).

### R7.5 Caveats

1. The `IsValidBuildLocation → IsValidPlaceLocation → IsAreaClear` chain does NOT exist in this decompilation.
2. `Tile`-rule branch (1315-1318) sets `flag = false` without `fail_reason` (decompilation artifact).
3. Dangling `ObjectLayers` entry for replaced tile-pieces / `TileLayer != NumLayers` replacements is inferred (sole writers of ObjectLayers: Grid.cs:443-462 indexer; `OnCleanUp` skips unmark when `WasReplaced()`).
4. `SimCellOccupier.DestroySelf` (143+) has two paths (replace-element/vacuum vs not); `FinishConstruction` callback is queued after `ClearCellProperties`.

## R8. Design decisions D1–D2 (deep-research task B1, 2026-09-18)

### D1 — Metadata configuration

**D1.1 `ReplacementLayer` → `ObjectLayer.ReplacementTile`** (separate layer, door-mod style). Option B (`ReplacementLayer = Def.ObjectLayer`) is dead:
1. Native fallback never starts: `BuildTool.cs:354-361` `RunOnArea` → `def.IsReplacementLayerOccupied(offset_cell)` (BuildingDef.cs:305-309: `Grid.Objects[cell,(int)ReplacementLayer] != null`) — the old same-pref building in the same slot makes it occupied → no plan created.
2. Even with a plan, `MarkArea` (Constructable.cs:441-447 → BuildingDef.cs:842) would overwrite the old building's slot with the plan; at completion `GetReplacementCandidate` (328-335) would find the plan, which has no `BuildingComplete` (BuildingLoader.cs:49-59 add only `BuildingUnderConstruction`/`Constructable`/`Storage`; `BuildingComplete` added only on the complete building, 196-219) → refund/destroy path (Constructable.cs:165-213) skipped, `Def.Build` overwrites the slot → ghost old building + no refund. The `OnSpawn` anchor branch (375-407) does not save it: at 381 the slot already holds `base.gameObject` (written by MarkArea before), so the "multiple replacement tiles" destroy path (394-395) is never taken — damage is the silent slot overwrite.

Option A (`ReplacementTile`) survives the full native flow with zero validity patches:
- Placement: `TryPlace` fails `HELP_BUILDLOCATION_OCCUPIED` (IsAreaClear 580-607, candidate looked up only `if (replace_tile)`) → intended "plain placement fails, replacement fallback takes over" (BuildTool.cs:350). Fallback: candidate found (D1.2), slot 11 empty → `IsReplacementLayerOccupied` false, gates 364 pass, same-material no-op at 371, `TryReplaceTile` (487-506) passes `IsValidPlaceLocation(replace_tile:true)` (490) because per-cell candidate == occupant (skip at 602); `IsReplacementTile` set around `Instantiate` (493-495) → plan in slot 11 for all placement cells, old building untouched in slot 1.
- Spawn: anchor branch reads slot 11 → null/self (382) → keeps plan, `AddBlock` (385-389), `RefreshCell` (390); slot 1 read (397-404) → only `CancelDeconstruction` on old building.
- Completion: `OnCompleteWork:158-215` finds old building (still slot 1), DestroySelf + `SpawnItemsFromConstruction` refund + ReplaceCallback + `DeleteObject`; multi-cell per-cell sweep in `FinishConstruction` (228-257) under exact-overlap placement; `UnmarkArea` (258) clears slot 11, `Def.Build` (259) → `MarkArea:842` writes new building into slot 1.

**D1.2 `ReplacementCandidateLayers` → `{ ObjectLayer.Building }`** (one shared immutable static list). Old same-pref building sits on `ObjectLayer.Building` (default BuildingDef.cs:123; `CreateBuildingDef` assigns it, BuildingTemplates.cs:31). `GetReplacementCandidate` does no tag filtering (only `BuildingComplete != null`, 333) — the list must name exactly the layer where the old building lives; adding other layers would route unrelated occupiers into the destroy path (Constructable.cs:161-213 / 234-253, no tag gate there). A different building on the same Building-layer cell can be returned as candidate — fine: same-pref filtering is in `CanReplace` for the anchor (BuildTool.cs:364); non-anchor only matters under exact overlap (cells contain the old building itself). Shared static, created once, never mutated (door-mod pattern Mod.cs:286-291).

**D1.3 `ReplacementTags` → per-def list `{ def.Tag }`** (NOT the door-mod shared live list). `CanReplace` (BuildingDef.cs:278-285) → `KPrefabID.HasAnyTags` (firstpass KPrefabID.cs:281-292) matches `PrefabTag == tag || tags.Contains(tag)`. A placed complete building carries `PrefabTag = Tag(def.PrefabID)` (`BuildingLoader.AddID`, BuildingLoader.cs:287; setter 144-155) and `Def.Tag = TagManager.Create(PrefabID)` (Def.cs:10,20) — PrefabID unique per prefab, so `def.CanReplace(old) ⇔ old.PrefabTag == def.Tag ⇔ SAME pref`. Decisive against a shared list: with it, an iron Shelf dragged exactly over a Refinery would pass BuildTool.cs:364 and 371 (`component.Def != def`) and replace cross-pref — out of scope. Fresh per-def list also makes re-registration idempotent (`ReplacementTags != null` after injection). Safety: plans are never false candidates — plan `PrefabTag` is `def.PrefabID + "UnderConstruction"` (BuildingLoader.cs:166), no `BuildingComplete`, lives on slot 11 (not in candidate list). Native same-material rejection (BuildTool.cs:366-371) is independent.

**D1.4 `Replaceable` → `true`** (set explicitly; idempotent). Field default `true` (BuildingDef.cs:68); sole game reader `BuildTool.cs:364` (on the candidate's def). Native writers = false: InsulatedDoorConfig.cs:34, WoodenDoorConfig.cs:30, RocketInteriorGas/Liquid port configs :23, RocketEnvelopeWindowTileConfig.cs:19, RocketWallTileConfig.cs:24, TilePOIConfig.cs:15, PixelPackConfig.cs:20, LargeBackwallFarmConfig.cs:22, UnderwaterMilkFeederConfig.cs:35. Writers = true: ShelfConfig.cs:66, FabricatedWoodMakerConfig.cs:40, CeilingFossilSculptureConfig.cs:31, FossilSculptureConfig.cs:29, ResearchClusterModuleConfig.cs:41, WideFarmTileConfig.cs:32.

**D1.5 `EquivalentReplacementLayers` → leave `null`.** Sole reader: `IsReplacementLayerOccupied` (BuildingDef.cs:311-319). Sole native writers: BuildingTemplates.cs:63 (foundation tiles → `{ReplacementLadder}`), :71 (ladders → `{ReplacementTile}`). The def's own slot 11 is already checked (307-309); no second slot to alias.

**D1 concrete C#** (in the `Assets.AddBuildingDef` postfix, guarded by D2 predicate):
```csharp
private static readonly List<ObjectLayer> SharedReplacementCandidateLayers =
    new List<ObjectLayer> { ObjectLayer.Building };
// per targeted def (BuildingDef __0):
__0.ReplacementLayer = ObjectLayer.ReplacementTile;
__0.ReplacementCandidateLayers = SharedReplacementCandidateLayers;
__0.ReplacementTags = new List<Tag> { __0.Tag };   // fresh per-def: own pref tag only
__0.Replaceable = true;
// __0.EquivalentReplacementLayers: untouched (stays null)
```

### D2 — Scope filter predicate

```csharp
private static bool IsRegularBuildingDef(BuildingDef def)
{
    if (def == null || def.BuildingComplete == null) return false;
    return def.ObjectLayer == ObjectLayer.Building          // regular building layer (walls/conduits/tiles/attachables/gantries out)
        && def.TileLayer == ObjectLayer.NumLayers          // == !def.IsTilePiece (BuildingDef.cs:276)
        && def.ReplacementLayer == ObjectLayer.NumLayers   // no native replacement machinery yet
        && def.ReplacementTags == null                     // no native replacement tags yet (idempotency marker too)
        && def.Replaceable                                 // game has not opted this def out
        && !IsDoorDef(def);                                // doors excluded by design
}
// door test — verbatim from reference mod (Mod.cs:463-477); safe because Assets.AddBuildingDef
// fires after the full config chain (Mod.cs:296-301) so Door component / CopyBuildingSettings are final:
private static bool IsDoorDef(BuildingDef def)
{
    GameObject go = def.BuildingComplete;
    CopyBuildingSettings cbs = go.GetComponent<CopyBuildingSettings>();
    if (cbs != null && cbs.copyGroupTag == GameTags.Door) return true;
    return go.GetComponent<Door>() != null;
}
```
Why not `BuildLocationRule`: regular buildings span many rules (OnFloor MetalRefinery, OnBackWall Shelf, Anywhere Ladder/GasConduit/ports); no single rule characterizes them. `ShowInBuildMenu` deliberately not required: a leaked POI-structure def can only replace its own pref (`CanReplace` same-pref) and is not draggable from the build menu — harmless.

Evaluation table (all verified in decompiled configs):
| Def | KEPT/EXCLUDED | by which clause |
| --- | --- | --- |
| MetalRefinery (MetalRefineryConfig.cs:28, 3×4 OnFloor) | **KEPT** | — |
| Shelf (ShelfConfig.cs:50,66, 1×1 OnBackWall) | **KEPT** | — |
| Door (DoorConfig.cs:12,32,38) | EXCLUDED | `IsDoorDef` (door clause is load-bearing — doors pass every generic clause) |
| Foundation tile (TileConfig → CreateFoundationTileDef, BuildingTemplates.cs:49-63) | EXCLUDED | TileLayer/ReplacementLayer/ReplacementTags clauses |
| Ladder (LadderConfig.cs:10-11 → CreateLadderDef, BuildingTemplates.cs:66-72) | EXCLUDED | native replacement clauses |
| ExteriorWall (ExteriorWallConfig.cs:12,24,27-31) | EXCLUDED | ObjectLayer + native replacement |
| FacilityBackWallWindow (FacilityBackWallWindowConfig.cs:11,18,20-24) | EXCLUDED | ObjectLayer + native replacement |
| GasConduit (GasConduitConfig.cs:11-16,28-29) | EXCLUDED | conduit layer + native replacement |
| RocketInteriorGasInputPort (RocketInteriorGasInputPortConfig.cs:17,23) | EXCLUDED | `Replaceable == false` clause |

## R9. Design decisions D3.1 — placement mechanics (deep-research task B2a, 2026-09-18)

**Q1 — Native fallback suffices for PLAN CREATION in the exact-overlap case, any N ≥ 1.**
- The clicked cell C is passed straight into `TryBuild` (DragTool.cs:418-424 → BuildTool.cs:302-316) and is the placement anchor: area = `{ C + Rotatable.GetRotatedCellOffset(PlacementOffsets[i], orientation) }`; rotation pivots around the anchor (Rotatable.cs:215-226); offset (0,0) is always a building cell (BuildingDef.cs:1801,1809-1810).
- Exact overlap ⟺ `C == A_old` (old building's anchor cell = `Grid.PosToCell(B.transform.GetLocalPosition())`, same convention as plans, Constructable.cs:160).
- Anchor candidate check passes (BuildTool.cs:350-364: candidate found at C, slot 11 empty, `Replaceable` true, `CanReplace` true via per-def tag); `IsAreaClear` passes per cell (BuildingDef.cs:579-606: candidate == occupant at every cell); `MarkArea` marks slot 11 for ALL N cells (Constructable.cs:443-447 → BuildingDef.cs:833-842). No `TryBuild` postfix needed for placement.

**Q2 — Sub-case table (C = clicked, R = drag orientation, A_old/R_old = old building):**
| # | Drag | Native result | Spec status |
|---|------|---------------|-------------|
| 1 | C==A_old, R==R_old | plan created, exact overlap | **VALID** |
| 2 | C==A_old, R≠R_old, symmetric footprint (e.g. 3×3 rot 180°) | plan created, rotated in place | INVALID (orientation) |
| 3 | C==A_old, R≠R_old, asymmetric | plan created, rotated+shifted | INVALID |
| 4 | C≠A_old, C∈S_old | plan created, SHIFTED (overlapping cells pass candidate==occupant, outside cells pass empty) | INVALID (shift) |
| 5 | C∉S_old | no candidate; normal build may proceed | N/A |
Shifted/rotated drags "succeed" natively — rejection must be mod-side.
- Preview tint (`BuildTool.UpdateVis` :177-179 = `IsValidPlaceLocation(4-arg) || IsValidReplaceLocation`, BuildingDef.cs:1184-1207 — verbatim in R10.1/R10.4): white for exact overlap AND for case 2 (symmetric rotation), **and also for a same-material drag — the native body is purely geometric, it has no material/strictness signal** (R10.1); red for shifts (cases 3–4). The Stage 5 tint postfix must therefore be **bidirectional** (flip `true→false` for same-material exact overlap so the spec's red "занято" holds; optionally also case 2), unlike the door mod's one-way flip (`if (__result) return;`, Mod.cs:820-835).
- **Guard = PREFIX on `BuildTool.TryBuild(int)`** (not postfix — a postfix would have to undo a live Constructable). Body:
  ```
  int candAnchor  = Grid.PosToCell(candidate.transform.GetLocalPosition());
  Orientation candOrient = candidate.GetComponent<Rotatable>().GetOrientation();
  bool exact = (candAnchor == cell) && (candOrient == __instance.buildingOrientation);
  if (isModDef(def) && candidate != null && def.CanReplace(candidate) && !exact) return false; // skip original
  ```
  where `candidate = def.GetReplacementCandidate(cell)`. Pure rejection — no Grid writes. Same-material no-op stays native (BuildTool.cs:366-371). Box/line mode calls TryBuild per cell (DragTool.cs:256-270) — guard applies per cell. Instant-build: prefix skip also skips `InstantBuildReplace`; instant exact-overlap is natively broken for N≥2 (N× `def.Build` → stacked buildings) — **out of scope** (debug mode; door mod same position, Mod.cs:1009-1018).
- A prefix on `BuildingDef.TryReplaceTile` would NOT cover instant-build drags; `TryBuild` is the single choke point.

**Q2.4 — CORRECTION to R8 D1: native completion is BROKEN for N ≥ 2 exact overlap. The mod MUST carry a `Constructable.FinishConstruction` prefix.**
- `OnCompleteWork` (Constructable.cs:160-161) finds the old building at the ANCHOR cell only; `SimCellOccupier.DestroySelf(callback)` (165-174) loops over every old-building cell and, for air cells (always), fires the callback IMMEDIATELY per cell (SimCellOccupier.cs:176) → `FinishConstruction` re-enters N times synchronously.
- Fire 1: the non-anchor sweep (Constructable.cs:228-257, gated `PlacementOffsets.Length > 1`) re-finds the SAME still-dying old building at every non-anchor cell (its slot-1 entries persist — `DeleteObject` at :213 runs after the `DestroySelf` call, OnCleanUp later) → `component5.DestroySelf(null)` NREs at SimCellOccupier.cs:176 (door mod's Bug D, Mod.cs:1194-1225,1387-1397); even without the NRE it would double-refund (:245), double-Trigger (:251), double-DeleteObject (:253).
- Fires 2..N: pass the callback guard (`this != null && base.gameObject != null`, :170 — plan's `DeleteObject` at :290 only defers) → ghost duplicate builds via `Def.Build` (:259) (door mod's Bug C, Mod.cs:1226-1234).
- Required `FinishConstruction` prefix (mirror door mod Mod.cs:1194-1441, MINUS its distinct-candidate D-fix branch — unreachable under strict exact overlap):
  1. **Re-entry bail**: plan's private `finished` flag (reflection/publicizer) already true → skip original.
  2. **Pre-sweep entry clear**: anchor candidate = `def.GetReplacementCandidate(anchorCell)`; for every non-anchor placement cell whose candidate is the SAME GO, clear its slot-1 entries with reference-matched null writes (`Grid.Objects[cell,(int)ObjectLayer.Building] = null` where entry == GO) before the original body (door mod's `ClearCandidateGridEntries`, Mod.cs:1375-1386; null write removes the dictionary entry, Grid.cs:452-465).
- After the fix, native remainder is correct: `UnmarkArea` (:258) clears slot 11; `Def.Build` (:259) writes the new building to slot 1 at all N cells; old building's `OnCleanUp` (BuildingComplete.cs:241-263) runs its own reference-matched `UnmarkArea` (WasReplaced() false for regular buildings, :44-47/:257-260) — no-ops on already-cleared entries. 1×1 buildings need NO completion fix (sweep gate false).

**Q3 — `Assets.AddBuildingDef` postfix sufficiency: YES.** Registration: LegacyModMain.cs:28-41 → GeneratedBuildings.cs:19-24 (every non-abstract IBuildingConfig, vanilla+DLC+mod) → BuildingConfigManager.cs:104,114 (`DoPostConfigureComplete` then `Assets.AddBuildingDef`) → Assets.cs:670-674 (dedup by PrefabID). Fires once per def per game start, before both modes, after the full config chain. All replacement fields are read live (CanReplace :280-284, GetReplacementCandidate :326-328, IsReplacementLayerOccupied :305-307, BuildTool drag-time reads, Constructable.cs:446); no def cloning/caching (placed objects reference the same def instance, BuildingLoader.cs:219).
- Carries over from the door mod's postfix (Mod.cs:307-343): structure only — null guard, predicate, idempotency guard (skip if `ReplacementLayer != NumLayers`), the four field assignments, debug log. Does NOT carry over: shared mutable tag list (per-def singleton here), door-specific branches, POI `ShowInBuildMenu` gate.
- Mod-def marker: static `HashSet<BuildingDef>` populated in the postfix (robust vs other mods); per-def `ReplacementTags` identity as fallback.

### CONCLUSION (D3.1)
1. Placement side: native fallback suffices for the only spec-valid case (exact overlap); a **prefix** on `BuildTool.TryBuild(int)` rejects shifted/rotated drags (pure rejection, no grid writes); same-material no-op stays native; no Grid writes needed anywhere on the placement side.
2. Completion side: **mandatory prefix** on `Constructable.FinishConstruction` (re-entry bail on `finished` + pre-sweep reference-matched clearing of the anchor candidate's slot-1 entries at non-anchor cells), door-mod template minus the distinct-candidate branch.
3. Instant build: out of scope (debug mode; natively broken for N≥2 anyway).

## R10. Verbatim source bodies: validation chain (deep-research task B2b, 2026-09-18)

Quoted verbatim from the decompiled sources (game build Aquatic 731233, `lib_sources/Assembly-CSharp/`; whitespace normalized) to back the R2/R7.3 claims and the D3.1 patch set.

### R10.1 `BuildingDef.IsValidReplaceLocation` (BuildingDef.cs:1184-1207)

```csharp
public bool IsValidReplaceLocation(Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer)
{
    if (replace_layer == ObjectLayer.NumLayers)
    {
        return false;
    }
    bool result = true;
    int cell = Grid.PosToCell(pos);
    for (int i = 0; i < PlacementOffsets.Length; i++)
    {
        CellOffset rotatedCellOffset = Rotatable.GetRotatedCellOffset(PlacementOffsets[i], orientation);
        int cell2 = Grid.OffsetCell(cell, rotatedCellOffset);
        if (!Grid.IsValidBuildingCell(cell2))
        {
            return false;
        }
        if (Grid.Objects[cell2, (int)obj_layer] == null || Grid.Objects[cell2, (int)replace_layer] != null)
        {
            result = false;
            break;
        }
    }
    return result;
}
```

Purely geometric + layer test — no def, tag, material, or position-strictness signal anywhere:
- Sole caller in the game build: `BuildTool.UpdateVis` (BuildTool.cs:175-190, R10.4) — feeds the red/white preview tint only; nothing in the drag flow calls it.
- With the D1 metadata (`ReplacementLayer = ReplacementTile`, `ObjectLayer = Building`) the result is `true` iff **every** placement cell has a Building-layer occupant **and** slot 11 (`ReplacementTile`) is empty. Consequences for the Q2 sub-cases:
  - case 1 (exact overlap): white — correct;
  - case 2 (symmetric rotation in place): white — the minor UX gap;
  - **same-material exact overlap: ALSO white** — the body cannot distinguish copper-over-copper from copper-over-steel, so the spec's "red 'занято' as without the mod" for same material is NOT delivered natively;
  - cases 3–4 (shifts): red — cells outside the old building have no slot-1 occupant;
  - without the D1 injection (`ReplacementLayer == NumLayers`, line 1186) the method always returns false — i.e. exactly the pre-mod behavior.
- **Implication for Stage 5:** the `IsValidReplaceLocation` postfix must be **bidirectional** — overwrite `__result` in both directions for mod defs (true→false on same-material / optionally rotated; false→true only if a future valid case ever needs it). The door mod's one-way flip (`if (__result || !IsDoorDef) return;`, Mod.cs:820-835) is NOT sufficient.
- The 4-arg `IsValidPlaceLocation` (BuildingDef.cs:1098, feeds the hover fail-text card, BuildToolHoverTextCard.cs:49-55) needs NO patch: once `flag2` is correct, `UpdateVis`'s OR (R10.4) and the card's text follow.

### R10.2 `BuildingDef.IsValidBuildLocation` 5-arg (BuildingDef.cs:1221-1359)

```csharp
public bool IsValidBuildLocation(GameObject source_go, int cell, Orientation orientation, bool replace_tile, out string fail_reason)
{
    if (!Grid.IsValidBuildingCell(cell))
    {
        fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_INVALID_CELL;
        return false;
    }
    if (!IsAreaValid(cell, orientation, out fail_reason))
    {
        return false;
    }
    bool flag = true;
    fail_reason = null;
    switch (BuildLocationRule)
    {
    case BuildLocationRule.OnFloor:
    case BuildLocationRule.OnCeiling:
    case BuildLocationRule.OnFoundationRotatable:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_FLOOR;
        }
        break;
    case BuildLocationRule.OnWall:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_WALL;
        }
        break;
    case BuildLocationRule.InCorner:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_CORNER;
        }
        break;
    case BuildLocationRule.WallFloor:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_CORNER_FLOOR;
        }
        break;
    case BuildLocationRule.OnFloorOverSpace:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_FLOOR;
        }
        else if (!AreAllCellsValid(cell, orientation, WidthInCells, HeightInCells, (int check_cell) => World.Instance.zoneRenderData.GetSubWorldZoneType(check_cell) == SubWorld.ZoneType.Space))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_SPACE;
        }
        break;
    case BuildLocationRule.OnRocketEnvelope:
        if (!CheckFoundation(cell, orientation, BuildLocationRule, WidthInCells, HeightInCells, GameTags.RocketEnvelopeTile))
        {
            flag = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_ONROCKETENVELOPE;
        }
        break;
    case BuildLocationRule.NotInTiles:
    {
        GameObject gameObject2 = Grid.Objects[cell, 9];
        flag = (replace_tile || gameObject2 == null || gameObject2 == source_go) && !Grid.HasDoor[cell];
        if (flag)
        {
            GameObject gameObject3 = Grid.Objects[cell, (int)ObjectLayer];
            if (gameObject3 != null)
            {
                if (ReplacementLayer == ObjectLayer.NumLayers)
                {
                    flag = flag && (gameObject3 == null || gameObject3 == source_go);
                }
                else
                {
                    Building component3 = gameObject3.GetComponent<Building>();
                    flag = component3 == null || component3.Def.ReplacementLayer == ReplacementLayer;
                }
            }
        }
        fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_NOT_IN_TILES;
        break;
    }
    case BuildLocationRule.Tile:
    {
        flag = true;
        GameObject gameObject = Grid.Objects[cell, 27];
        if (gameObject != null)
        {
            Building component = gameObject.GetComponent<Building>();
            if (component != null && component.Def.BuildLocationRule == BuildLocationRule.NotInTiles)
            {
                flag = false;
            }
        }
        gameObject = Grid.Objects[cell, 2];
        if (gameObject != null)
        {
            Building component2 = gameObject.GetComponent<Building>();
            if (component2 != null && component2.Def.BuildLocationRule == BuildLocationRule.NotInTiles)
            {
                flag = replace_tile;
            }
        }
        break;
    }
    case BuildLocationRule.BuildingAttachPoint:
    {
        flag = false;
        for (int i = 0; i < Components.BuildingAttachPoints.Count; i++)
        {
            if (flag)
            {
                break;
            }
            for (int j = 0; j < Components.BuildingAttachPoints[i].points.Length; j++)
            {
                if (Components.BuildingAttachPoints[i].AcceptsAttachment(AttachmentSlotTag, Grid.OffsetCell(cell, attachablePosition)))
                {
                    flag = true;
                    break;
                }
            }
        }
        fail_reason = string.Format(UI.TOOLTIPS.HELP_BUILDLOCATION_ATTACHPOINT, AttachmentSlotTag);
        break;
    }
    case BuildLocationRule.Anywhere:
    case BuildLocationRule.Conduit:
    case BuildLocationRule.OnFloorOrBuildingAttachPoint:
        flag = true;
        break;
    }
    return flag && ArePowerPortsInValidPositions(source_go, cell, orientation, out fail_reason) && AreConduitPortsInValidPositions(source_go, cell, orientation, out fail_reason);
}
```

Confirms the R7.3 trace of the start re-check (`PlaceDiggables:747` → 4-arg wrapper :1209-1213 → this method):
- **No call to `IsValidPlaceLocation` (1120) or `IsAreaClear` (547)** — the start re-check cannot produce `HELP_BUILDLOCATION_OCCUPIED` at all (R7.3).
- The ONLY ObjectLayer-occupancy clause in the whole method is the `NotInTiles` branch (1285-1307). Regular buildings (D2 predicate admits OnFloor/OnWall/InCorner/Anywhere/OnFloorOrBuildingAttachPoint/…) never reach it. Inside it, once `ReplacementLayer != NumLayers` (post-D1-injection) the occupied-by-building clause becomes `component3.Def.ReplacementLayer == ReplacementLayer` (1301) — **true for same-pref** (identical def instance ⇒ identical field value) — which is why the start re-check never rejects a replacement plan because of its own old building.
- `Tile` rule (1308-1330): a layer-27 (WireTile) occupant with a `NotInTiles` rule sets `flag = false` at 1315-1318 **with no `fail_reason`** (decompilation artifact, R7.5 caveat 2); a layer-2 (Backwall) occupant with a `NotInTiles` rule sets `flag = replace_tile` (1326).
- Final conjunction (1358): the power/port checks pass `source_go` through — at start re-check time that is the **plan**, and both checks self-exclude it (`!= source_go`, R10.3), so the plan's own port marks (written by `MarkArea`, BuildingDef.cs:851/859/866/872) cannot fail it.
- Fail reasons still possible for a same-position same-pref replacement (all about the surroundings, unchanged by the replacement): `INVALID_CELL` (1223-1227), `FLOOR`/`WALL`/`CORNER`/`CORNER_FLOOR`/`SPACE`/`ONROCKETENVELOPE` (1236-1284), `NOT_IN_TILES` (1285-1307, via `Grid.HasDoor[cell]`), silent layer-27 failure (1315-1318), `ATTACHPOINT` (1331-1351), port overlaps via 1358 → R10.3.
- Note for the D2 predicate: because no `BuildLocationRule` clause characterizes "regular buildings", the predicate (D2) deliberately does not filter on it — the table there is evaluated instead on `ObjectLayer`/`TileLayer`/native-replacement fields.

### R10.3 `BuildingDef.IsValidConduitConnection` (BuildingDef.cs:1621-1658)

```csharp
private bool IsValidConduitConnection(GameObject source_go, ConduitType conduit_type, int utility_cell, ref string fail_reason)
{
    bool result = true;
    switch (conduit_type)
    {
    case ConduitType.Gas:
    {
        GameObject gameObject3 = Grid.Objects[utility_cell, 15];
        if (gameObject3 != null && gameObject3 != source_go)
        {
            result = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_GASPORTS_OVERLAP;
        }
        break;
    }
    case ConduitType.Liquid:
    {
        GameObject gameObject2 = Grid.Objects[utility_cell, 19];
        if (gameObject2 != null && gameObject2 != source_go)
        {
            result = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_LIQUIDPORTS_OVERLAP;
        }
        break;
    }
    case ConduitType.Solid:
    {
        GameObject gameObject = Grid.Objects[utility_cell, 23];
        if (gameObject != null && gameObject != source_go)
        {
            result = false;
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_SOLIDPORTS_OVERLAP;
        }
        break;
    }
    }
    return result;
}
```

Sole role: the per-port-cell conduit check behind `AreConduitPortsInValidPositions` (final conjunction at 1358). A gas/liquid/solid conduit connection (layers 15/19/23) in a port cell fails placement unless the occupant **is `source_go` itself**. At start re-check time `source_go` is the plan, whose own conduit port slots `MarkArea` wrote — so the replacement plan self-excludes and never fails against itself; only a genuinely foreign conduit in a port cell fails, exactly as without the mod.

### R10.4 `BuildTool.UpdateVis` (BuildTool.cs:175-190, the tint caller)

```csharp
private void UpdateVis(Vector3 pos)
{
    bool flag = def.IsValidPlaceLocation(visualizer, pos, buildingOrientation, out var _);
    bool flag2 = def.IsValidReplaceLocation(pos, buildingOrientation, def.ReplacementLayer, def.ObjectLayer);
    flag = flag || flag2;
    if (visualizer != null)
    {
        Color c = Color.white;
        float strength = 0f;
        if (!flag)
        {
            c = Color.red;
            strength = 1f;
        }
        SetColor(visualizer, c, strength);
    }
    ...
```

Confirms: `flag` = plain 4-arg `IsValidPlaceLocation` (BuildingDef.cs:1098, also feeds the hover fail-text card), ORed with `flag2` = `IsValidReplaceLocation(pos, orientation, def.ReplacementLayer, def.ObjectLayer)` (R10.1); red iff both false. With D1 metadata, `flag2` alone already whitens the exact-overlap hover — the only missing pieces are the material/strictness signals (R10.1 implication), which the Stage 5 postfix supplies.

## R11 — Final patch decisions (deep-research task B2b-close, 2026-09-18)

### Q1 — `IsValidBuildLocation` postfix: **NO**
The door mod's postfix targets the 5-arg core `IsValidBuildLocation(GameObject, int, Orientation, bool, out string)` (BuildingDef.cs:1221; the 4-arg wrapper at :1209 delegates to it). Guard `if (__result) return;`, effect: re-runs its area-aware gate and sets `__result` — it compensates its Bug A (construction-start re-check failing for wall-over-door on the NotInTiles `&& !Grid.HasDoor[cell]` clause, BuildingDef.cs:1288). For this mod, every 5-arg fail path passes natively at start (source_go = plan GO, `replace_tile = true`; chain never calls `IsAreaClear`, R7.3): INVALID_CELL/IsAreaValid/CheckFoundation unchanged from drag time; NotInTiles — no D2-admitted def carries it, `replace_tile` satisfies the layer-9 clause, `HasDoor` false, ObjectLayer clause 1301 true for same-pref (identical def instance); Tile rule — excluded by D2; BuildingAttachPoint — validated at placement; final port conjunction self-excludes `!= source_go` (R10.3) and the plan's own `MarkArea` port marks own those slots.

### Q2 — `BuildingDef.TryReplaceTile` prefix: **NO**
Door mod's prefix (Mod.cs:1445-1483, 5-param overload, BuildingDef.cs:487) rejects same-pref different-element door drags (iron Door over gold Door share one PrefabID) that the native element gate leaves open — its spec demands full skip. Here same-pref exact overlap IS the desired case: native `TryReplaceTile` handles it (`IsValidPlaceLocation(replace_tile:true)` at 490 passes per-cell candidate==occupant); invalid drags are rejected upstream by the `BuildTool.TryBuild` prefix (all drag entry points funnel through `TryBuild` — BuildMenu.cs:908, PlanScreen.cs:1820 — and `TryReplaceTile` has no other external callers); completion handled by the FinishConstruction prefix. Adding it would be wrong.

### Q3 — FINAL consolidated patch set (4 patches)

| # | Target (exact decompiled signature) | Type | Purpose | Key guard / condition |
|---|---|---|---|---|
| 1 | `public static void Assets.AddBuildingDef(BuildingDef)` | postfix | Inject replacement metadata: `ReplacementLayer = ReplacementTile`, `ReplacementCandidateLayers = {Building}`, `ReplacementTags = {def.Tag}`, `Replaceable = true`; record def in `ModDefs` | `def != null && IsRegularBuildingDef(def) && def.ReplacementTags == null` (idempotency; D2 predicate, R8) |
| 2 | `private void BuildTool.TryBuild(int)` (verified BuildTool.cs:307 — returns void, not bool) | prefix (bool-returning short-circuit; no `__result`) | Reject shifted/rotated drags (pure rejection, no grid writes) — cases 2–4 of R9 | `IsModDef(def) && candidate != null && def.CanReplace(candidate) && !(Grid.PosToCell(candidate.transform.GetLocalPosition()) == cell && candidate.GetComponent<Rotatable>().GetOrientation() == __instance.buildingOrientation)` with `candidate = def.GetReplacementCandidate(cell)` → `return false` |
| 3 | `private void Constructable.FinishConstruction(UtilityConnections, WorkerBase)` | prefix | Mandatory N≥2 completion fix: re-entry bail + pre-sweep clearing of the anchor candidate's non-anchor slot-1 entries before the native sweep (door-mod template minus the distinct-candidate D branch — unreachable under strict exact overlap) | `__instance.IsReplacementTile && IsModDef(def)`; then `IsAlreadyFinished(__instance)` → `return false`; else if `def.PlacementOffsets.Length > 1`: `RunOnArea` over non-anchor cells, and where `def.GetReplacementCandidate(c) == anchorCandidate` (reference match) → `ClearCandidateGridEntries(candidate)` |
| 4 | `public bool BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` | postfix | Bidirectional preview tint for mod defs: force **red** on same-material exact overlap (spec "занято") and on case-2 rotation in place; keep **white** only for cross-material exact overlap; shifts stay native-red | `IsModDef(def)` (sole caller is `BuildTool.UpdateVis`, so reading `BuildTool.Instance.selectedElements` is safe); verdict = `IsExactOverlap(def, pos, orientation, out candidate) && !IsSameMaterial(candidate, BuildTool.Instance.selectedElements)` |

**Helpers** (shared statics in Mod.cs):
- `private static readonly HashSet<BuildingDef> ModDefs` + `IsModDef(BuildingDef)` — populated in patch 1; fallback identity: `def.ReplacementTags != null && def.ReplacementTags.Count == 1 && def.ReplacementTags[0] == def.Tag`.
- `private static readonly List<ObjectLayer> SharedReplacementCandidateLayers = new List<ObjectLayer> { ObjectLayer.Building };` (shared immutable).
- `private static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)` — byref-normalizing declared-only reflection (door mod Mod.cs:230-263).
- `IsRegularBuildingDef(BuildingDef)` — D2 predicate (R8) + `IsDoorDef(BuildingDef)` — verbatim from door mod Mod.cs:463-477.
- `IsExactOverlap(BuildingDef def, Vector3 pos, Orientation orientation, out GameObject candidate)` — anchor candidate exists and anchor cell + orientation equal the drag (shared by patches 2 and 4).
- `IsSameMaterial(GameObject candidate, IList<Tag> selected)` — `selected[0] == candidate.GetComponent<PrimaryElement>().Element.tag`, with the snow-hash fix (door mod Mod.cs:988-994).
- `IsAlreadyFinished(Constructable c)` — reflection on the private `finished` field, fail-open (door mod Mod.cs:1261-1276).
- `ClearCandidateGridEntries(GameObject candidate)` — for each cell of `candidate.PlacementCells`: reference-matched `Grid.Objects[cell,(int)candidateDef.ObjectLayer] = null` (no tile-piece branch — D2 excludes tile pieces).

**Wiring + logging:** In `OnLoad`, after `base.OnLoad(harmony)`, resolve all four targets via `FindMethod` and attach programmatically via `harmony.Patch(...)` (`PUtil.LogError` + skip on null); `Assets.AddBuildingDef` postfix attached first (must exist before def registration; the other three order-independent). Under `#if DEBUG`: `PUtil.LogDebug(...F(...))` logs one line per injected def (PrefabID), one line per rejected drag (def, cell, orientation), one line per completion fix-up (re-entry bail / each cleared entry).
