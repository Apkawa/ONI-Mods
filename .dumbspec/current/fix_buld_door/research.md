# Research — fix_buld_door

Superficial context collection (dependencies, files, signatures, fixed constraints).
No solutions are designed here — that happens in spec/plan.

Researchers append findings under `##` sections as they work.

## 2. Game: Door & BuildPlan mechanics

### 2.0 Scope facts (whole-tree searches)

- **There is NO `BuildPlan` class in this decompiled tree.** `grep -rl "class BuildPlan"` over all of `/home/apkawa/code/ONI_MODS` → no matches; the literal string `BuildPlan` appears nowhere. Planned-construction in this build is represented by the `BuildingUnderConstruction` GameObject (see 2.2).
- **There is NO `PlacementSystem` class either** (`grep -rl "PlacementSystem"` → no matches). The player placement entry point is `BuildTool : DragTool` (Assembly-CSharp/BuildTool.cs:7).
- Decompiled tree size: 3988 files at `/home/apkawa/code/ONI_MODS/Assembly-CSharp`.

### 2.1 Door

- `public class Door : Workable, ISaveLoadable, ISim200ms, INavDoor` (Assembly-CSharp/Door.cs:7). Enums: `DoorType { Pressure, ManualPressure, Internal, Sealed }` (Door.cs:9), `ControlState { Auto, Opened, Locked, NumStates }` (Door.cs:17).
- Multi-cell representation: Door has NO second TileInfo/offset field of its own. It holds `[MyCmpReq] public Building building;` (Door.cs:273) and iterates `building.PlacementCells` (`int[]`, one entry per occupied cell — Door.cs:56, 486, 501, 622, 739). `Door()` ctor calls `SetOffsetTable(OffsetGroups.InvertedStandardTable)` (Door.cs:393-396).
- Door ↔ wall link: **no wall-reference field exists on `Door` in this build** (full file read, 946 lines; field block Door.cs:263-343 — none reference a wall/ore cell). Grid-side bookkeeping is per-cell, set in `OnSpawn` (Door.cs:426-496): `Grid.FakeFloor.Add`, `Pathfinding.Instance.AddDirtyNavGridCell`, `Grid.HasDoor[num] = true`, `SimMessages.SetCellProperties(num, 8)`, `Grid.RenderedByWorld[num] = false`; cleared in `OnCleanUp` (Door.cs:498-527), incl. `SimMessages.ReplaceAndDisplaceElement(cell, SimHashes.Vacuum, CellEventLogger.Instance.DoorOpen, 0f)` when a placement cell is solid (Door.cs:511-514).
- Closed-door solid behavior: `SetSimState` (Door.cs:662): closes by `SimMessages.ReplaceAndDisplaceElement(cell2, component.ElementID, CellEventLogger.Instance.DoorClose, mass, temperature, byte.MaxValue, 0, handle3.index)` (Door.cs:720); opens by `SimMessages.Dig(cell, handle2.index, skipEvent: true)` (Door.cs:693). The door cells themselves hold the wall element when closed.
- `Sim200ms` (Door.cs:896): when `do_melt_check` is set and any placement cell is no longer `Grid.Solid` → `Util.KDestroyGameObject(this)` (Door.cs:921-939) — a door whose wall support is destroyed is destroyed too.

### 2.2 BuildPlan equivalents (this build)

- `public class BuildingUnderConstruction : Building` (Assembly-CSharp/BuildingUnderConstruction.cs:3) — the in-world "plan" object.
- Creation: `BuildingDef.Instantiate(Vector3 pos, Orientation orientation, IList<Tag> selected_elements, int layer = 0)` (Assembly-CSharp/BuildingDef.cs:529) → `GameUtil.KInstantiate(BuildingUnderConstruction, pos, Grid.SceneLayer.Front, null, layer)` (line 533), sets `PrimaryElement.ElementID` and `Constructable.SelectedElementsTags = selected_elements` (line 537).
- Who instantiates: `BuildTool.TryBuild(int cell)` (Assembly-CSharp/BuildTool.cs:307):
  - survival path: `gameObject = def.TryPlace(visualizer, pos, buildingOrientation, selectedElements, facadeID)` (BuildTool.cs:323) → `BuildingDef.TryPlace(...)` (BuildingDef.cs:464) → `Instantiate(...)` (BuildingDef.cs:529). So a placed build item is a `BuildingUnderConstruction` prefab, not a data-only plan object.
  - sandbox/instant path: `flag = DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild)` (BuildTool.cs:319); when flag: gate `def.IsValidBuildLocation(visualizer, pos, buildingOrientation) && def.IsValidPlaceLocation(visualizer, pos, buildingOrientation, out fail_reason)` (BuildTool.cs:325), then `gameObject = def.Build(cell, buildingOrientation, null, selectedElements, Mathf.Min(def.Temperature, b), facadeID, playsound: false, GameClock.Instance.GetTime())` (BuildTool.cs:348).
- `BuildingDef` prefab refs: `BuildingComplete`, `BuildingPreview`, `BuildingUnderConstruction` (BuildingDef.cs:237-241); `PlacementOffsets` / `ConstructionOffsetFilter` (BuildingDef.cs:243-245); materials consumed in `Build`→`Create` via `recipe.GetAllIngredients(selected_elements)` and `resource_storage.ConsumeAndGetDisease(...)` (BuildingDef.cs:347-372).

### 2.3 Sandbox / instant placement (BuildTool.TryBuild, BuildTool.cs:307-388)

- Instant build only when `flag` (line 319) and both validity gates pass (line 325); the actual object creation is `def.Build(...)` (line 348).
- Existing-building cleanup before instant build, when `def.ObjectLayer == ObjectLayer.Building`: `def.RunOnArea(cell, buildingOrientation, ...)` over which `Uprootable.CanUproot(Grid.Objects[offset_cell, (int)def.ObjectLayer], out var uprootable)` → `uprootable.CompleteWork(null)` (BuildTool.cs:327-336).
- Backwall removal in instant path: when `def.ObjectLayer == ObjectLayer.Backwall` and `BackwallManager.HasBackwall(offset_cell)` → `SimMessages.Dig(offset_cell, -1, skipEvent: true, backwall: true)` (BuildTool.cs:337-346).
- **No ore/wall (solid-cell) removal in the instant path of BuildTool** — solid cells are dealt with by the validity gates instead; door-over-rock behavior lives in the replacement flow (2.4) / `Constructable`, not in BuildTool's main instant branch.
- `PostProcessBuild(bool instantBuild, Vector3 pos, GameObject builtItem)` (BuildTool.cs:433): for non-instant builds applies master priority via `Prioritizable.SetMasterPriority` from `BuildMenu.Instance`/`PlanScreen.Instance` (lines 441-452); plays place sound or the `NOMATERIAL` pop; deactivates the tool if `def.OnePerWorld` (lines 474-477).

### 2.4 Existing "replace existing build" precedent (BuildTool.cs:350-431 + BuildingDef.cs)

Fires when `gameObject == null && def.ReplacementLayer != ObjectLayer.NumLayers` (BuildTool.cs:350):
- `def.GetReplacementCandidate(cell)` (BuildingDef.cs:324) — walks `ReplacementCandidateLayers`, else `Grid.ObjectLayers[(int)TileLayer][cell]`; returns the occupant GameObject.
- `def.IsReplacementLayerOccupied(offset_cell)` (BuildingDef.cs:305) — `Grid.Objects[cell, (int)ReplacementLayer] != null` plus `EquivalentReplacementLayers` scan.
- Gate (BuildTool.cs:361-371): occupant has `BuildingComplete`, and `component.Def.Replaceable && def.CanReplace(replacementCandidate)`, plus element/dup check `component.Def != def || selectedElements[0] != tag`; `CanReplace` (BuildingDef.cs:278) = occupant `KPrefabID.HasAnyTags(ReplacementTags)`.
- Survival: `gameObject = def.TryReplaceTile(visualizer, pos, buildingOrientation, selectedElements, facadeID)` (BuildTool.cs:376; BuildingDef.cs:508 → 487) then `Grid.Objects[cell, (int)def.ReplacementLayer] = gameObject` (BuildTool.cs:377). `TryReplaceTile` sets `BuildingUnderConstruction.GetComponent<Constructable>().IsReplacementTile = true` around `Instantiate` (BuildingDef.cs:492-495).
- Sandbox: `InstantBuildReplace(int cell, Vector3 pos, GameObject tile)` (BuildTool.cs:390) — first destroys neighbor candidates (other `PlacementOffsets` cells) via `SimCellOccupier.DestroySelf`/`Object.Destroy` (lines 392-416); then destroys the main tile (via `SimCellOccupier.DestroySelf` if present, line 423) and calls `def.Build(...)` (lines 421, 427).
- Replace validation: `def.IsValidBuildLocation(visualizer, pos, buildingOrientation, replace_tile: true) && def.IsValidPlaceLocation(visualizer, pos, buildingOrientation, replace_tile: true, out fail_reason2)` (BuildTool.cs:379); `BuildingDef.IsValidReplaceLocation(Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer)` (BuildingDef.cs:1184) is used for the hover-preview color (BuildTool.cs:178).

### 2.5 Survival placement (BuildTool.cs:307-349, BuildingDef.cs:454-540)

- Survival placement = `def.TryPlace(...)` (BuildTool.cs:323). **There is no "enqueue wall removal" step in BuildTool for survival mode**: it only instantiates the `BuildingUnderConstruction` plan object (BuildingDef.cs:464-485); validity is checked up-front via `IsValidPlaceLocation`. Wall removal in survival mode would have to come from other systems (`Constructable`/door flow), not from a placement-enqueue method.
- `BuildingDef.Build(int cell, Orientation orientation, Storage resource_storage, IList<Tag> selected_elements, float temperature, bool playsound = true, float timeBuilt = -1f)` (BuildingDef.cs:407): `Create(...)` (BuildingDef.cs:347 — consumes `recipe.GetAllIngredients(selected_elements)` from `resource_storage`, lines 350-362), `MarkArea(cell, orientation, ObjectLayer, gameObject)` (line 416), tile marking (417-424), `BuildingComplete.SetCreationTime(timeBuilt)` (444-448), triggers event `-1661515756` (449-450).
- Entry per cell: `BuildTool.OnDragTool(int cell, int distFromOrigin)` → `TryBuild(cell)` (BuildTool.cs:302-305); `TryBuild` dedupes via `lastDragCell`/`lastDragOrientation` (line 309) and skips Logic-port buildings when the cursor is not over the visualizer cell (line 309).

### 2.6 Validity / invalidation of a plan's location

- `IsValidPlaceLocation` overloads (BuildingDef.cs:1098-1120) funnel to the core taking `replace_tile` and `restrictToActiveWorld` flags; `IsValidBuildLocation` overloads (BuildingDef.cs:1209-1221).
- `IsAreaClear(GameObject source_go, int cell, Orientation orientation, ObjectLayer layer, ObjectLayer tile_layer, bool replace_tile, bool restrictToActiveWorld, out string fail_reason, bool permitUproots = true)` (BuildingDef.cs:547): per-`PlacementOffsets` loop with `Grid.IsCellOffsetValid` (554), `Grid.WorldIdx` vs active world (561), `Grid.IsValidBuildingCell` (566); `fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_INVALID_CELL` (556, 563, 568).
- No code found in the tree that re-validates an already-placed `BuildingUnderConstruction` after its location becomes invalid (pending: `BuildingUnderConstruction.cs` / `Constructable.cs` not yet read — see open items).

### 2.7 Door build definition (DoorConfig)

- `public class DoorConfig : IBuildingConfig`, `public const string ID = "Door"` (Assembly-CSharp/DoorConfig.cs:6-8).
- `CreateBuildingDef()` (DoorConfig.cs:10-23) → `BuildingTemplates.CreateBuildingDef("Door", 1, 2, "door_internal_kanim", 30, 10f, TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER2, MATERIALS.ALL_METALS, 1600f, BuildLocationRule.Tile, ...)` (line 12): width 1, height 2, construction time 10s, melt point 1600, **BuildLocationRule.Tile**.
- Door def flags (DoorConfig.cs:13-19): `Entombable = true`, `Floodable = false`, `IsFoundation = false`, `PermittedRotations = PermittedRotations.R90`, `ForegroundLayer = Grid.SceneLayer.InteriorWall`, single logic input port `Door.OPEN_CLOSE_PORT_ID` at (0,0).
- `DoPostConfigureComplete` (30-45): `doorType = Internal`, `AccessControl.controlEnabled = true`, `Workable.workTime = 3f`, adds `ZoneTile`, `KBoxCollider2D`, destroys `BuildingEnabledButton`.
- `DoPostConfigureUnderConstruction(GameObject go)` (47-50): `go.AddTag(GameTags.NoCreatureIdling)` on the under-construction prefab.
- `BuildingTemplates.CreateBuildingDef(...)` (BuildingTemplates.cs:7-39): sets `SceneLayer = Grid.SceneLayer.Building` (19), `ObjectLayer = ObjectLayer.Building` (31), calls `InitDef()` (11) and `GenerateOffsets()` (33). `Entombable` field: `public bool Entombable = true` (BuildingDef.cs:66); consumers `BuildingComplete.cs:97`, `BuildingLoader.cs:267`; `POIBunkerExteriorDoor.cs:15` sets `Entombable = false`.
- `BuildingDef.GenerateOffsets(int width, int height)` (BuildingDef.cs:1795-1814) fills `PlacementOffsets` from a static cache; for the 1×2 door the offsets are `[(0,0), (0,1)]` (anchor + one cell up per the formula at 1809-1810).
- `BuildingDef.PostProcess()` (BuildingDef.cs:1816-1825): builds `CraftRecipe` from `MaterialCategory`/`Mass`.
- `BuildLocationRule` enum (lib_sources/Assembly-CSharp/BuildLocationRule.cs:1-23): `Anywhere, OnFloor, OnFloorOverSpace, OnCeiling, OnWall, InCorner, Tile, NotInTiles, Conduit, LogicBridge, WireBridge, HighWattBridgeTile, BuildingAttachPoint, OnFloorOrBuildingAttachPoint, OnFoundationRotatable, BelowRocketCeiling, OnRocketEnvelope, WallFloor, NoLiquidConduitAtOrigin, OnBackWall`.

### 2.8 Survival-mode door-into-rock flow

- Door def uses `BuildLocationRule.Tile` → in `IsAreaClear`, `IsValidTileLocation(source_go, num, replace_tile, ref fail_reason)` is called (BuildingDef.cs:661-667). `IsValidTileLocation` (792-817) rejects only: a `NotInTiles` building on `Grid.Objects[cell, 27]` (WireTile), a `HighWattBridgeTile` building on layer 29, and a backwall occupant on `Grid.Objects[cell, 2]` when `!replacement_tile`. **Solid ore cells are NOT rejected** — a door plan can be placed over solid rock.
- Plan-side digging: `Constructable.OnSpawn` (Constructable.cs:322): `waitForFetchesBeforeDigging = <Ladder> || <SimCellOccupier> || <Door> || <LiquidPumpingStation>` (line 408) — **doors wait for material fetches before digging**.
- `Constructable.PlaceDiggables()` (Constructable.cs:647-772): subscribes partitioner events solidChanged/digDestroyed/backwallChanged (664-667); for each placement cell with `Diggable.IsDiggable(offset_cell)` it instantiates the "DigPlacer" prefab into `Grid.Objects[offset_cell, 7]` with `diggable.choreTypeIdHash = Db.Get().ChoreTypes.BuildDig.IdHash` (674-703); for plants on `Grid.Objects[offset_cell, 5]` with `Uprootable.CanUproot(...)`, marks them via `uprootable.MarkForUproot()` and adds them to `pendingUproots` (708-743).
- `Diggable.IsDiggable(int cell)` (Diggable.cs:420-425): `if (Grid.Solid[cell]) return !Grid.Foundation[cell]; return GetUnstableCellAbove(cell) != Grid.InvalidCell;`
- `Diggable : Workable` (Diggable.cs:10); `OnSpawn` (107-136): registers `Grid.Objects[cached_cell, 7] = gameObject` (118), creates `WorkChore<Diggable>` for `Db.Get().ChoreTypes.Dig` or the custom `choreTypeIdHash` (119-124), `SetWorkTime(float.PositiveInfinity)` (125). Actual ore damage happens in `OnWorkTick` → `DoDigTick(cached_cell, dt)` (349-357, `DoDigTick` static at 378-385); `isDigComplete` when `!WillDigTile() && !WillDigBackwall()` (355); self-destroy via `Util.KDestroyGameObject` (313-324, 359-365); `OnCancel` (553-559) re-triggers `GameHashes.Cancel`.
- Net effect in survival mode: placing a door over ore spawns a `Diggable` (BuildDig chore) per ore cell + uproot marks per plant; workers dig, then the plan's build chore (gated at Constructable.cs:757-767 on `digs_complete && location-valid && fetchList complete`) runs `Constructable.OnCompleteWork`.

### 2.9 Plan completion & invalidation (Constructable)

- `public class Constructable : Workable, ISaveLoadable` (Constructable.cs:12). Key fields: `[Serialize] public bool IsReplacementTile` (63-64), `[Serialize] Tag[] selectedElementsTags` (78-79), `Notification invalidLocation` (36), `public bool isDiggingRequired = true` (55), `HashSet<Uprootable> pendingUproots` (74); `public Recipe Recipe => building.Def.CraftRecipe` (101); property `IList<Tag> SelectedElementsTags` (103-117).
- `OnCompleteWork(WorkerBase worker)` (124-215): computes `initialTemperature` from stored materials; if `IsReplacementTile` && multi-cell: destroys the replacement candidate (via `SimCellOccupier.DestroySelf` when present) or, for `Conduit`, calls `MarkForReplacement(replacementCandidate)` and subscribes to `-21016276` (186); before destroying, cancels deconstruction on the old occupant (199-206); ends in `FinishConstruction(...)`.
- `FinishConstruction(...)` (223-291): `UnmarkArea()` (228) → `building.Def.Build(cell, orientation, storage, selectedElementsTags, initialTemperature, facade, playsound: true, GameClock.Instance.GetTime())` (254-255) → `this.DeleteObject()` (290) — **the plan GameObject destroys itself after completing**.
- `MarkArea()` (433-447): `def.MarkArea(num, orientation, IsReplacementTile ? ReplacementLayer : ObjectLayer, gameObject)` (446); for tile pieces also marks `TileLayer` and sets `Grid.IsTileUnderConstruction[num] = true` (449-457). `BuildingDef.MarkArea(int cell, Orientation, ObjectLayer layer, GameObject go)` (BuildingDef.cs:829-844): writes `Grid.Objects[cell2, (int)layer] = go` per placement cell, moving uprootable plants to layer 5 first.
- `UnmarkArea()` (Constructable.cs:468-471) → `def.UnmarkArea(...)` (BuildingDef.cs:976).
- **Re-validation of an existing plan**: end of `PlaceDiggables` (747-772): `bool flag = building.Def.IsValidBuildLocation(gameObject, transform.GetPosition(), orientation, IsReplacementTile)` (747) → adds/removes `invalidLocation` notification and `BuildingStatusItems.InvalidBuildingLocation` status + tint (749-772); the build chore is (re)created only when `digs_complete && flag && fetchList == null` (757-767). This runs on partitioner solid/dig/backwall changes (664-667, 790-796 via `OnSolidChangedOrDigDestroyed`).

### 2.10 Plan cancellation / removal

- Cancel event ID: `GameHashes.Cancel = 2127324410` (GameHashes.cs:93).
- Emitters: user-menu button `"action_cancel"` (`Constructable.OnRefreshUserMenu` 851-854; `OnPressCancel()` → `gameObject.Trigger(2127324410)` at 856-859); `CancelTool : FilteredDragTool` (CancelTool.cs:4, `OnDragTool` 38-52 — triggers on every cell-object in an active layer); `WorldContainer.CancelChores()` (WorldContainer.cs:1165-1177) triggers it for all grid objects during world cleanup.
- `Constructable` subscribes `Subscribe(2127324410, OnCancelDelegate)` in `OnSpawn` (342) → `OnCancel` (861-866): hides DetailsScreen, `ClearMaterialNeeds()`, `ClearPendingUproots()`. **No Destroy/DeleteObject call inside Constructable for a normal (non-replacement) plan** — for the search done (`BuildingUnderConstruction` ∩ destroy-call cross-reference), the only destroy sites on plan objects are: self-`DeleteObject()` in `FinishConstruction` (290), replacement-candidate destruction (213, 253), and the "multiple replacement tiles" error path `Util.KDestroyGameObject(base.gameObject)` (395). Generic `Cancellable.OnCancel` does `this.DeleteObject()` (Cancellable.cs:17-20), but `Constructable` does NOT inherit `Cancellable`.
- `Constructable.OnCleanUp` (547-605): removes block-tile-renderer block for replacement tiles (549-556), frees partitioner entries (557-560), unregisters `SaveLoadRoot` (561-565), `fetchList.Cancel("Constructable destroyed")` (566-568), `UnmarkArea()` (569), and per placement cell deletes the dig placer `Diggable.GetDiggable(cell2).gameObject.DeleteObject()` (571-579) and force-cancels uproots of layer-1/layer-5 plants (580-596).
- `Diggable.GetDiggable(int cell)` = `Grid.Objects[cell, 7]?.GetComponent<Diggable>()` (Diggable.cs:415-422).

### 2.11 ObjectLayer numbers used in these flows (lib_sources/Assembly-CSharp/ObjectLayer.cs:1, 0-based)

`0 Minion, 1 Building, 2 Backwall, 3 Pickupables, 4 Canvases, 5 Plants, 6 FillPlacer, 7 DigPlacer, 8 MopPlacer, 9 FoundationTile, 10 PlasticTile, 11 ReplacementTile, 12 GasConduit, 13 GasConduitTile, 14 ReplacementGasConduit, 15 GasConduitConnection, 16 LiquidConduit, 17 LiquidConduitTile, 18 ReplacementLiquidConduit, 19 LiquidConduitConnection, 20 SolidConduit, 21 SolidConduitTile, 22 ReplacementSolidConduit, 23 SolidConduitConnection, 24 LadderTile, 25 ReplacementLadder, 26 Wire, 27 WireTile, 28 ReplacementWire, ...` (layer 29 = wire-connection used at BuildingDef.cs:866,627; consistent with `Grid.Objects[...,5]` = plants and `Grid.Objects[...,7]` = dig placers seen in Constructable/Diggable).

### 2.12 Building component (multi-cell bookkeeping)

- `public class Building : KMonoBehaviour, IGameObjectEffectDescriptor, IUniformGridObject, IApproachable` (Building.cs:10); `public BuildingDef Def` (12); `Orientation` read from `Rotatable` component (32-42).
- `public int[] PlacementCells` (44-54) lazily filled by `RefreshCells()` (111-130): per `Def.PlacementOffsets`, `placementCells[i] = Grid.OffsetCell(Grid.PosToCell(this), Rotatable.GetRotatedCellOffset(Def.PlacementOffsets[i], orientation))`.
- `PlacementCellsContainCell(int cell)` (99-109); `GetExtents()` (80-87); `GetValidPlacementExtents()` (89-97, padded by 1).

## 3. Donor mod: ReplaceFloors/tile_rep

### 3.1 File inventory (`/home/apkawa/code/ONI_MODS/ReplaceFloors/`)

| File | Role |
|---|---|
| `ReplaceFloors/mod.yaml` | Mod manifest: `title: replace floors`, `staticID: ReplaceFloors` |
| `ReplaceFloors/mod_info.yaml` | `supportedContent: ALL`, `minimumSupportedBuild: 490405`, `APIVersion: 2`, `version: "1.0"` |
| `ReplaceFloors/tile_rep.dll` | Built mod dll shipped at mod-folder root (5632 bytes); loaded by `KMod/DLLLoader` |
| `ReplaceFloors/tile_rep/tile_rep.csproj` | SDK-style project, `AssemblyName=tile_rep`, **TFM net472**, `LangVersion 11.0`, `AllowUnsafeBlocks=True`, `GenerateAssemblyInfo=False`; plain `<Reference Include>` for `Assembly-CSharp`, `0Harmony`, `UnityEngine.CoreModule`, `Assembly-CSharp-firstpass` (no versioning, resolved from a HintPath-less lookup — project is NOT self-contained) |
| `ReplaceFloors/tile_rep/Properties/AssemblyInfo.cs` | Leftover template: `AssemblyTitle("ClassLibrary1")`, v1.0.0.0 |
| `ReplaceFloors/tile_rep/tile_rep/Tile_rep_patch.cs` | Sole logic file: namespace `tile_rep`, `public class Tile_rep_patch : UserMod2` |

### 3.2 Tile_rep_patch.cs — patched methods (full file is 87 lines, decompiled)

Class: `tile_rep.Tile_rep_patch : UserMod2`. One `Harmony` instance is created per mod folder by `KMod/DLLLoader` (game-runtime fact per AGENTS.md), so any `HarmonyPatch` classes in the mod are discovered through the Harmony instance named by the mod id. Nested `public static class A_Patches` with `[HarmonyPatch]`-declared patch classes (class-level patcher style; `UserMod2.OnLoad` in the DLL presumably calls `Harmony.PatchAll` on the mod's id — the source file shown has no `OnLoad` override, so patching is driven by whatever the compiled dll adds; the SOURCE file as checked in contains ONLY these patch classes).

Patches declared in the file (all Postfix, all in `A_Patches`):

| Patch class | Target type (Assembly-CSharp) | Target method | Effect |
|---|---|---|---|
| `AA__Patch` | `FarmTileConfig` | `DoPostConfigureComplete` | `go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles, false)` |
| `AAA__Patch` | `HydroponicFarmConfig` | `DoPostConfigureComplete` | same `AddTag(GameTags.FloorTiles, false)` |
| `AAAA__Patch` | `WireBridgeHighWattageConfig` | `DoPostConfigureComplete` | same |
| `AaAAA__Patch` | `FloorSwitchConfig` | `DoPostConfigureComplete` | same |
| `AaAA9A__Patch` | `HEPBridgeTileConfig` | `DoPostConfigureComplete` | same |
| `AAAAA__Patch` | `TilePOIConfig` | `DoPostConfigureComplete` | same |
| `AAAAAA__Patch` | `TilePOIConfig` | `CreateBuildingDef` | `__result.Replaceable = true` (ref BuildingDef __result) |

Notes from the source (Tile_rep_patch.cs:15,26,39,51,62,72,81):
- Postfix signature is `static void Postfix(ref GameObject go)` — i.e. the ORIGINAL of `DoPostConfigureComplete` returns/ref-passes the configured `GameObject`.
- `AddTag(GameTags.FloorTiles, false)` — second arg is `bool addTag`… verified against KPrefabID (see 3.3).
- The only "replace" mechanism implemented is tagging these tile prefabs with `GameTags.FloorTiles` and setting `BuildingDef.Replaceable = true` on `TilePOIConfig`. **There is NO destroy/spawn/BuildPlan code in the donor mod itself** — the actual replacement behavior (destroy old occupant / spawn new tile / cancel or keep BuildPlan) comes from GAME code that reacts to `GameTags.FloorTiles` / `BuildingDef.Replaceable`. The task's assumptions about "replacement logic in the mod" are therefore partially misplaced: the donor mod is 100% tag-based.

Stray artifacts at `/home/apkawa/code/ONI_MODS/` root: `LadderReplace.dll` present (noted, per task). **Note: `tile_rep.dll` is NOT actually at the ONI_MODS root** — `ls *.dll` shows only `LadderReplace.dll`; the task statement's `tile_rep.dll` at root does not exist on disk. The only `tile_rep.dll` is inside `ReplaceFloors/`. (AGENTS.md also lists "tile_rep.dll at repo root" as a known stray-file caveat.)

### 3.3 Where the patched targets live in Assembly-CSharp

| Donor patch target | Game file | Signature/notes |
|---|---|---|
| `FarmTileConfig.DoPostConfigureComplete` | `Assembly-CSharp/FarmTileConfig.cs:63` | `public override void DoPostConfigureComplete(GameObject go)`; class at `FarmTileConfig.cs:5` (`FarmTileConfig : IBuildingConfig`); def is **1×1** (`CreateBuildingDef("FarmTile", 1, 1, ...)` line 11), `BuildingTemplates.CreateFoundationTileDef(obj)` (line 12), `DragBuild = true` (line 23) |
| `HydroponicFarmConfig.DoPostConfigureComplete` | `Assembly-CSharp/HydroponicFarmConfig.cs` | class at line 5 |
| `WireBridgeHighWattageConfig.DoPostConfigureComplete` | `Assembly-CSharp/WireBridgeHighWattageConfig.cs` | class at line 5 |
| `FloorSwitchConfig.DoPostConfigureComplete` | `Assembly-CSharp/FloorSwitchConfig.cs` | class at line 6 |
| `HEPBridgeTileConfig.DoPostConfigureComplete` | `Assembly-CSharp/HEPBridgeTileConfig.cs` | class at line 4 |
| `TilePOIConfig.DoPostConfigureComplete` | `Assembly-CSharp/TilePOIConfig.cs:45` | `public override void DoPostConfigureComplete(GameObject go)` (void, one GameObject param); original body adds `GameTags.Bunker` |
| `TilePOIConfig.CreateBuildingDef` | `Assembly-CSharp/TilePOIConfig.cs:8` | `public override BuildingDef CreateBuildingDef()`; sets `obj.Replaceable = false` at line 15 (donor Postfix flips to `true`); also `obj.DebugOnly = true` (26), `obj.IsFoundation = true` (17), `obj.TileLayer = ObjectLayer.FoundationTile` (19), 1×1 def (10), `BuildLocationRule.Tile` |

Note: the game original is `void DoPostConfigureComplete(GameObject go)` (by-value param, void return). The donor's postfixes declare `Postfix(ref GameObject go)`; `go` matches the original's param name and the postfix only READS `go` (`GetComponent<KPrefabID>().AddTag(...)`) — it never reassigns it, so the `ref` is functionally inert.

Vanilla tag-adding style in configs (e.g. `GlassTileConfig.cs:54` `go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles);`) uses the single-arg form; donor passes explicit `false` for the second arg of `KPrefabID.AddTag` (KPrefabID lives in `Assembly-CSharp-firstpass`, not this decomp tree — per section 1.8).

### 3.4 How the tag-based replacement actually works in the game (facts)

The donor mod's tags only matter because of this game code:

1. **`BuildingTemplates.CreateFoundationTileDef(BuildingDef)`** — `Assembly-CSharp/BuildingTemplates.cs:46-64`. Sets, for the NEW building's def:
   - `TileLayer = ObjectLayer.FoundationTile`, `ReplacementLayer = ObjectLayer.ReplacementTile` (lines 49-50)
   - `ReplacementCandidateLayers = { FoundationTile, LadderTile, Backwall }` (lines 51-56)
   - `ReplacementTags = { GameTags.FloorTiles, GameTags.Ladders, GameTags.Backwall }` (lines 57-62)
   - `EquivalentReplacementLayers = { ReplacementLadder }` (line 63)
   → A foundation tile (e.g. GlassTile calls `CreateFoundationTileDef` at GlassTileConfig.cs:15) is a *new* def whose `ReplacementTags` includes `FloorTiles`; `CanReplace(go)` (BuildingDef.cs:278-285) returns true iff the candidate has ANY of the new def's `ReplacementTags`. So an OLD tile must CARRY `GameTags.FloorTiles` (on its KPrefabID) to be replaceable-by-foundation — that is exactly what the donor's AddTag enables for farm tiles etc. (vanilla `FarmTileConfig.DoPostConfigureComplete` only adds `GameTags.FarmTiles` — FarmTileConfig.cs:67 — never `FloorTiles`).
2. **Entry point `BuildTool.TryBuild(int cell)`** — `Assembly-CSharp/BuildTool.cs:307-388` (see also section 2.4):
   - `flag` = instant build: `DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild)` (line 319).
   - Non-instant (survival queue): `gameObject = def.TryPlace(...)` (line 323).
   - **Replacement fallback** (line 350): entered only if `gameObject == null && def.ReplacementLayer != ObjectLayer.NumLayers`.
   - `GetReplacementCandidate(cell)` (352) → `IsReplacementLayerOccupied` across `RunOnArea` (354-360) → gate `component.Def.Replaceable && def.CanReplace(replacementCandidate)` (364) → skip if same def AND same primary element (371) → non-instant: `def.TryReplaceTile(...)` (376) then **`Grid.Objects[cell, (int)def.ReplacementLayer] = gameObject`** (377 — single-GridPos write of the queue object); instant: `InstantBuildReplace(cell, pos, replacementCandidate)` (381).
3. **`BuildingDef.CanReplace(GameObject go)`** — `BuildingDef.cs:278-285`: `if (ReplacementTags == null) return false; return go.GetComponent<KPrefabID>().HasAnyTags(ReplacementTags);`
4. **`BuildingDef.IsReplacementLayerOccupied(int cell)`** — `BuildingDef.cs:305-322`.
5. **`BuildingDef.GetReplacementCandidate(int cell)`** — `BuildingDef.cs:324-345`: scans `ReplacementCandidateLayers` for first cell occupant with `BuildingComplete`; fallback `Grid.ObjectLayers[TileLayer][cell]`.
6. **Survival queue — `BuildingDef.TryReplaceTile(...)`** — `BuildingDef.cs:487-527`: if `IsValidPlaceLocation(src_go, pos, orientation, replace_tile: true, ...)` (490) sets `BuildingUnderConstruction`'s `Constructable.IsReplacementTile = true` around `Instantiate(...)` (492-495). The instantiated object (a `BuildingUnderConstruction` with `Constructable`) is what a worker later completes — this build has no data-only BuildPlan class (see section 2.0).
7. **Sandbox/instant — `BuildTool.InstantBuildReplace(int cell, Vector3 pos, GameObject tile)`** — `BuildTool.cs:390-431`:
   - For multi-cell defs (`def.PlacementOffsets.Length > 1`, line 392): `RunOnArea` over all offset cells ≠ `cell`, destroying each `GetReplacementCandidate(offset_cell)` (`SimCellOccupier.DestroySelf` or `Object.Destroy`, 394-416).
   - Old tile with `SimCellOccupier`: `DestroySelf(callback)`; callback does `Object.Destroy(tile)`, then `def.Build(...)` at the SAME cell, then `PostProcessBuild(true, ...)` (423-429). Without `SimCellOccupier`: immediate `Object.Destroy(tile)` + `def.Build` (417-422).
   - **No BuildPlan/BuildQueue object exists on the instant path** — sandbox instant build destroys + spawns directly; the queue write (`Grid.Objects[cell, ReplacementLayer] = ...`) happens only on the survival path (BuildTool.cs:377).
8. **Worker-side replacement (survival) — `Constructable`** — `Assembly-CSharp/Constructable.cs`:
   - `public bool IsReplacementTile;` (line 64).
   - On completion, lines 158-215: `int cell = Grid.PosToCell(base.transform.GetLocalPosition()); GameObject replacementCandidate = building.Def.GetReplacementCandidate(cell);` (160-161). If found: destroy OLD building (`SimCellOccupier.DestroySelf` whose callback calls `FinishConstruction`, 168-174; or via `BuildingComplete` subscription 183-190), refund materials via `Deconstructable.SpawnItemsFromConstruction(worker)` (205), fire replacement trigger hash `1606648047` with `ReplaceCallbackParameters { TileLayer, Worker }` (207-212), then `replacementCandidate.DeleteObject()` (213).
   - **Multi-cell at completion — `FinishConstruction`** (223-259): `if (IsReplacementTile && building.Def.PlacementOffsets.Length > 1)` (228) → `RunOnArea` over offset cells ≠ own cell destroying each candidate the same way (230-257). Then `UnmarkArea()` and `building.Def.Build(cell, orientation, storage, selectedElementsTags, initialTemperature, facade, playsound: true, ...)` (259) spawns the finished building.
   - `Constructable.cs:446`: `ObjectLayer layer = (IsReplacementTile ? def.ReplacementLayer : def.ObjectLayer);` (grid-slot marking depends on replacement mode); line 470 `def.UnmarkArea(layer: IsReplacementTile ? building.Def.ReplacementLayer : building.Def.ObjectLayer, ...)`.
   - `Constructable.cs:747`: re-validates with `building.Def.IsValidBuildLocation(base.gameObject, base.transform.GetPosition(), building.Orientation, IsReplacementTile)`.
9. **`IsValidPlaceLocation(..., replace_tile: bool, ...)`** — `BuildingDef.cs:1098-1120+`; core loop `IsAreaClear` (547-615+): iterates `PlacementOffsets` (551); in `replace_tile` mode fetches `gameObject = GetReplacementCandidate(num)` (580-583) and that candidate cell is excluded from the occupied checks (line 602 `(gameObject == null || gameObject != gameObject2)`, line 608 TileLayer check `gameObject == null || gameObject == source_go`).
10. Related: `BaseUtilityBuildTool.cs:484-486` sets `IsReplacementTile = true/false` identically (utility-build-tool variant of the same queue path).

### 3.5 Facts about the TWO-cell door (vanilla door configs)

From `Assembly-CSharp/WoodenDoorConfig.cs` (WoodenDoor requires DLC2, line 8-11):
- `CreateBuildingDef("WoodenDoor", 1, 2, "door_wood_kanim", 30, 10f, ... BuildLocationRule.Tile ...)` (line 15) → **`WidthInCells=1`, `HeightInCells=2`** (two-cell vertical door), 30 HP, 10 s construction.
- `PermittedRotations = PermittedRotations.R90` (28) → 2 `PlacementOffsets` (vertical neutral, horizontal rotated).
- `buildingDef.Replaceable = false` (30) — a door will NOT be removed by the replacement flow (`BuildTool.cs:364` requires `component.Def.Replaceable`).
- `IsFoundation = false` (40); **no `CreateFoundationTileDef` call** → door def has no `ReplacementLayer`/`ReplacementCandidateLayers`/`ReplacementTags` set → the `def.ReplacementLayer != ObjectLayer.NumLayers` fallback (BuildTool.cs:350) is NOT entered for doors. (`InsulatedDoorConfig.cs:34` likewise sets `Replaceable = false`.)
- `ForegroundLayer = Grid.SceneLayer.InteriorWall` (41), `DragBuild = true` (29); `ObjectLayer` = `ObjectLayer.Building` (default, BuildingTemplates.cs:31).
- `DoPostConfigureComplete` (54-67): adds `Door` (DoorType.Internal), `AccessControl`, `Workable` (workTime 3f), `ZoneTile`, `KBoxCollider2D`, `CopyBuildingSettings` (copyGroupTag `GameTags.Door`).
- The generic door base is `DoorConfig` (patched by the current BuildDoorOverWall mod per section 1): `DoorConfig.cs:10` `CreateBuildingDef()`, def **1×2** (`"door_internal_kanim"`, line 12); `DoorConfig.cs:30` `DoPostConfigureComplete(GameObject go)`.

Single-cell assumptions in the game replacement path (facts relevant to a two-cell building):
- `BuildTool.cs:377`: queue object is written to ONE GridPos (`Grid.Objects[cell, ReplacementLayer] = gameObject`), the drag cell; multi-cell defs mark all cells via `RunOnArea`/`MarkArea` (BuildingDef.cs:416-424).
- `InstantBuildReplace(int cell, ...)` takes ONE cell; the primary candidate is always `GetReplacementCandidate(cell)` at the drag cell (BuildTool.cs:352); neighbor-cell candidates destroyed only inside the `PlacementOffsets.Length > 1` branch (392-416).
- `Constructable.cs:160-161`: worker replacement destroys the candidate of ONE cell per queued Constructable (`Grid.PosToCell(transform.GetLocalPosition())`); other cells only via `PlacementOffsets.Length > 1` branch (228-257).
- `Building.RegisterBlockTileRenderer` (Building.cs:237-251): one `blockTileRenderer.AddBlock(..., isReplacement, ..., cell, ...)` per transform position.
- `IsAreaClear` replace-mode suppression (BuildingDef.cs:602, 608) is per-cell against the candidate object found at that cell's own layer; candidates live in tile layers (`FoundationTile`/`LadderTile`/`Backwall`), not arbitrary `ObjectLayer.Building` occupants — a multi-cell OLD `Building`-layer occupant is only ever returned for the specific cells listed in `ReplacementCandidateLayers`/`TileLayer` lookups (one cell per lookup).
- Door cells themselves carry the wall element when closed (`SimMessages.ReplaceAndDisplaceElement`, Door.cs:720 per section 2.1) — the donor mod has no code touching solid cells at all.

### 3.6 Project setup (tile_rep.csproj)

- TFM **net472** (tile_rep.csproj:5) — NOTE: repo invariant (AGENTS.md) is **net48** everywhere; this donor project predates it. `LangVersion 11.0` (8), `AllowUnsafeBlocks=True` (9), `GenerateAssemblyInfo=False` (4) with a hand-written `Properties/AssemblyInfo.cs` (stock VS template, "ClassLibrary1").
- References (tile_rep.csproj:13-18): plain `<Reference Include="Assembly-CSharp" />`, `0Harmony`, `UnityEngine.CoreModule`, `Assembly-CSharp-firstpass` — **no HintPath, no Version** → resolves only against a pre-populated folder; project is NOT self-buildable as checked in, and has **no Publicizer / ILRepack / PLib** wiring (no `<PackageReference>` at all; unlike the repo's `Directory.Build.props`-driven net48 setup).
- Shipping layout: `ReplaceFloors/` = mod folder with `mod.yaml` (`staticID: ReplaceFloors`), `mod_info.yaml` (`minimumSupportedBuild: 490405`, `APIVersion: 2`, `version: "1.0"`), and `tile_rep.dll` at the mod root — matches `KMod/DLLLoader` behavior (loads every `*.dll` in the mod folder, ≤1 `UserMod2` subclass per assembly, one `Harmony` per mod folder — AGENTS.md game-runtime fact).
- **Shipped dll ≡ checked-in source** (`strings tile_rep.dll`): it references exactly the same 6 config types (`FarmTileConfig`, `HydroponicFarmConfig`, `WireBridgeHighWattageConfig`, `FloorSwitchConfig`, `HEPBridgeTileConfig`, `TilePOIConfig`) + `BuildingDef`, the same 7 nested patch classes (`AA__Patch`…`AAAAAA__Patch`) and strings `DoPostConfigureComplete`, `CreateBuildingDef`, `FloorTiles`, `Replaceable`. **No** references to `BuildTool`, `Constructable`, `BuildPlan`-type classes, `SimMessages`, or `Grid`. PDB path embedded: `C:\Users\14063\Desktop\tile_rep\obj\Debug\tile_rep.pdb` (built on the original author's Windows machine; consistent with the "decompiled" look of the checked-in source).

## 1. BuildDoorOverWall — current mod

### 1.0 File list (`BuildDoorOverWall/`)

Source files (only two):
- `BuildDoorOverWall/Mod.cs` — the only C# source file (176 lines); contains the entire mod.
- `BuildDoorOverWall/BuildDoorOverWall.csproj`

Build artifacts (noted, skipped):
- `bin/Debug/net48/`: `BuildDoorOverWall.dll`, `BuildDoorOverWall.pdb`, `PLib.dll`, `UtilLibs.dll`, `UtilLibs.pdb`, `mod.yaml`, `mod_info.yaml` (i.e. PLib + UtilLibs are packed in — matches `IsPacked=true`).
- `obj/Debug/net48/publicized/Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll` (+ `.md5` files) and `obj/Debug/net48/BuildDoorOverWall.IgnoresAccessChecksTo.cs` → BepInEx AssemblyPublicizer is generating these at build time.
- No `mod.yaml`/`mod_info.yaml` checked in at the project root — mod metadata is generated into `bin/` (`GenerateMetadata=true`).

### 1.1 Entry class — Mod.cs

- File header (line 9) cites the donor mod: `// https://github.com/alex-3141/ONI-Mods/blob/master/BuildOverPlants/BuildOverPlants/BuildOverPlants.cs`
- Namespace: `OxygenNotIncluded.Mods.Example` (line 12) — template leftover.
- Entry class: `public class ExampleMod : UserMod2` (line 14) — **still the template name**, not `BuildDoorOverWall`.
- `OnLoad` (lines 16–43):
  - `HarmonyLib.Harmony.DEBUG = true;` (line 28)
  - `base.OnLoad(harmony)` (line 29) — base `UserMod2.OnLoad` runs PatchAll (per template semantics).
  - **One programmatic patch** (lines 31–42): `harmony.Patch` on `typeof(BuildingDef)` instance method `IsValidPlaceLocation` resolved via `GetMethodSafe(nameof(BuildingDef.IsValidPlaceLocation), false, typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string).MakeByRefType(), typeof(bool))` with `prefix:` set to a `HarmonyMethod` for `nameof(ExampleMod.BuildingDef_IsValidPlaceLocation_Patch)` (static method, lines 113–119).
  - So at load time the mod (a) applies the attribute-declared patches via base `OnLoad` PatchAll, and (b) manually patches `BuildingDef.IsValidPlaceLocation` with a prefix that sets `__result = true` and returns `false` (skipping the original).

### 1.2 All Harmony patches in Mod.cs

Attribute-declared (static nested classes, applied by `base.OnLoad` → Harmony PatchAll):

| Patch class (line) | Target | Target method | Patch | Effect |
|---|---|---|---|---|
| `LadderConfigCreateBuildingDef__Patch` (48) | `LadderConfig` | `CreateBuildingDef` | Postfix `ref BuildingDef __result` | Replaces `__result.ReplacementTags` with `{ GameTags.FloorTiles, GameTags.Ladders, GameTags.Backwall }` (52–57) |
| `DoorConfig_CreateBuildingDef__Patch` (76) | `DoorConfig` | `CreateBuildingDef` | Postfix `ref BuildingDef __result` | `ReplacementLayer = ObjectLayer.ReplacementTile`; `ReplacementCandidateLayers = { FoundationTile, LadderTile, Backwall }`; `ReplacementTags = { Door, Ladders, FloorTiles, Backwall }`; `EquivalentReplacementLayers = { ReplacementTile, ReplacementLadder }` (80–98) |
| `DoorConfig_DoPostConfigureComplete__Patch` (104) | `DoorConfig` | `DoPostConfigureComplete` | Postfix `ref GameObject go` | `go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles)` (108) |
| `BuildingDef_IsValidReplaceLocation_Patch` (124, `[HarmonyDebug]`) | `BuildingDef` | `IsValidReplaceLocation` | Postfix `ref bool __result` | **Logging only** — just `Console.WriteLine`, no state change (126–131) |
| `BuildTool_InstantBuildReplace_Patch` (138, `[HarmonyDebug]`) | `BuildTool` | `"InstantBuildReplace"` (name-string) | Prefix `bool Prefix(ref BuildTool __instance, int cell, Vector3 pos, GameObject tile, ref BuildingDef ___def)` | For defs matching `___def.ReplacementCandidateLayers != null && ___def.WidthInCells > 1 \|\| ___def.HeightInCells > 1` (line 145), iterates `___def.PlacementOffsets`, rotates via `Rotatable.GetRotatedCellOffset(offset, __instance.GetBuildingOrientation)`, maps with `Grid.OffsetCell(cell, offset)`; for each offset cell, foreach layer in `ReplacementCandidateLayers`: if `Grid.ObjectLayers[(int)layer].ContainsKey(cell1)` → `UnityEngine.Object.Destroy(occupant)` (160–168). Returns `true` (continue to original). |

Programmatic (in `OnLoad`, see 1.1):
- `BuildingDef.IsValidPlaceLocation` ← prefix `ExampleMod.BuildingDef_IsValidPlaceLocation_Patch` (static, lines 113–119): logs, sets `__result = true`, returns `false` (skip original). Effect: placement validation force-OK.

Commented-out code (61–71): `TileConfig.CreateBuildingDef` Postfix that would do `__result.ReplacementTags.Append(GameTags.Door)` — dead/commented.

Note: the `[HarmonyPatch]` classes use the class-level `[HarmonyPatch(typeof(T))]` + `[HarmonyPatch(nameof(M))]` style and are detected because they are static nested inside `ExampleMod`. The BuildTool one patches the **private** method by name string `"InstantBuildReplace"`.

### 1.3 "LadderConfig" — what it is

There is **no class named `LadderConfig` defined by the mod**. `LadderConfig` in Mod.cs (line 46–47) is the **game** class (Assembly-CSharp ladder building config). The mod itself defines only:
- `ExampleMod` (the `UserMod2` subclass)
- Nested static patch classes: `LadderConfigCreateBuildingDef__Patch`, `DoorConfig_CreateBuildingDef__Patch`, `DoorConfig_DoPostConfigureComplete__Patch`, `BuildingDef_IsValidReplaceLocation_Patch`, `BuildTool_InstantBuildReplace_Patch`

No other `.cs` files exist in the project (see 1.0). The task's assumption that the mod defines a `LadderConfig` is not supported by the source.

### 1.4 How the door gets built over a wall — mechanism as written

As implemented in code today:
1. **`DoorConfig.CreateBuildingDef` Postfix** (76–100): rewrites the door's `BuildingDef` so the door *can replace* walls/foundations: replacement layer = `ReplacementTile`, candidate layers = `FoundationTile` + `LadderTile` + `Backwall`, replacement tags = `Door`+`Ladders`+`FloorTiles`+`Backwall`, equivalent replacement layers = `ReplacementTile`/`ReplacementLadder`.
2. **`DoorConfig.DoPostConfigureComplete` Postfix** (104–110): tags the configured door prefab's `KPrefabID` with `GameTags.FloorTiles` so the door is treated as a floor tile.
3. **`BuildingDef.IsValidPlaceLocation` prefix** (113–119, applied in `OnLoad`): forces placement validity to `true` (skips game validation).
4. **`BuildTool.InstantBuildReplace` Prefix** (138–174): during actual build, manually destroys existing occupants in the door's `PlacementOffsets` cells across the candidate layers before the game's own logic runs (for multi-cell defs).
5. **`LadderConfig.CreateBuildingDef` Postfix** (48–59): also rewrites ladder replacement tags — changes *ladders*, not doors; appears to be leftover/dead for the "door over wall" feature but still fires on every ladder def creation.

So the mechanism = (a) BuildingDef replacement-metadata rewrites for doors, (b) floor-tile tagging of the door prefab, (c) forced-valid placement, (d) explicit destroy of displaced objects in `InstantBuildReplace`.

### 1.5 Dead / test / template-leftover code in Mod.cs

| Lines | What | Note |
|---|---|---|
| 9 | Comment URL to `BuildOverPlants.cs` (alex-3141/ONI-Mods) | donor-mod attribution |
| 12, 14 | namespace `OxygenNotIncluded.Mods.Example`, class `ExampleMod` | template naming kept |
| 18–27 | `Console.WriteLine` of `assembly.GetName()`, `mod`, `$"Mod <{mod.title}> loaded: {mod.staticID}"` | template debug prints |
| 22 | `// path;` | commented-out reference |
| 28 | `HarmonyLib.Harmony.DEBUG = true;` | Harmony debug mode on globally |
| 46–59 | `LadderConfigCreateBuildingDef__Patch` | rewrites **ladder** `ReplacementTags` — live but unrelated to the door feature |
| 61–71 | `TileConfig_CreateBuildingDef__Patch` | fully commented out |
| 113–119 | `BuildingDef_IsValidPlaceLocation_Patch` logs on every placement check | active logging in a hot path |
| 124–132 | `BuildingDef_IsValidReplaceLocation_Patch` | pure logging patch (test code) |
| 165 | `Console.WriteLine("5")` | leftover debug log |
| 145 | `if (___def.ReplacementCandidateLayers != null && ___def.WidthInCells > 1 \|\| ___def.HeightInCells > 1)` | missing-paren quirk: precedence = `(A != null && W > 1) || H > 1`. If `ReplacementCandidateLayers` is null and `HeightInCells > 1`, the body still runs and the inner `foreach` on the null list throws. |

### 1.6 csproj facts (`BuildDoorOverWall.csproj`)

- SDK: `Microsoft.NET.Sdk` (line 1).
- Line 2: `<Import Project="$(MSBuildThisFileDirectory)/../Directory.Build.props" />` is **commented out** — the mod project itself does not import shared props directly; references to game dlls / 0Harmony / UnityEngine / Publicizer / ILRepack must come from repo-root `Directory.Build.props` / `Directory.Build.targets` (see glob results: `Directory.Build.props`, `Directory.Build.props.default`, `Directory.Build.props.user`, `Directory.Build.targets` all exist at repo root) — to confirm by reading them.
- Package props (6–11): `PackageId=BuildDoorOverWall`, `Version=0.0.1`, `Authors=Apkawa`, `Copyright=$(AssemblyCopyright)`, `RepositoryUrl=https://github.com/Apkawa/Oni-Mods`, `TargetFramework=net48`.
- Build props (17–25): `ImplicitUsings=enable`, `Nullable=enable`, `LangVersion=default`, `AssemblyName=$(PackageId)`, `RootNamespace=$(PackageId)`, `IsMod=true`, `GenerateMetadata=true`, `IsPacked=true`.
- Mod-info props (30–36): `ModName=$(PackageId)`, `ModDescription` empty, `SupportedContent=ALL`, `MinimumSupportedBuild=$(TargetGameVersion)`, `APIVersion=2`.
- Release-only: `<OutDir>bin</OutDir>` when `Configuration|Platform == Release|AnyCPU` (38–40).
- References: **only** `<ProjectReference Include="..\UtilLibs\UtilLibs.csproj" />` (43). No `<Reference>` items and no `<PackageReference>` items in this csproj for game dlls, 0Harmony, UnityEngine, PLib, Publicizer, or ILRepack.

### 1.7 Build wiring (confirmed from repo-root files)

`Directory.Build.props` (repo root; auto-imported by MSBuild for every project; the commented-out import in the csproj is irrelevant — MSBuild auto-discovers `Directory.Build.props`):
- Imports `Directory.Build.props.user` (exists here) — sets `GameLibsFolder=$(HOME)/ONI/dlls`, `ModFolder=$(HOME)/ONI/mods`, `RefasmerInstalled=0` (props.user:6-14). (`props.default` fallback points at a Windows Steam path.)
- Sets `LangVersion=preview`, `AllowUnsafeBlocks=true` (9-10). `TargetGameVersion = QOL2025NovRelease = 700386` (42-44).
- **Game references added to every project** (56-145): `Assembly-CSharp` + `Assembly-CSharp-firstpass` (both with `<Publicize>true</Publicize>`), `0Harmony`, `UnityEngine`, `UnityEngine.CoreModule`, `Newtonsoft.Json` — all `HintPath=$(GameLibsFolder)/...`, `Private=False`.
- **Publicizer** (151-159): when `IsMod=true`, `PackageReference BepInEx.AssemblyPublicizer.MSBuild 0.4.3`, `IncludeAssets=build; contentfiles` (no runtime asset — prevents dll leakage into bin/). This is what creates `obj/Debug/net48/publicized/*.dll` and `BuildDoorOverWall.IgnoresAccessChecksTo.cs`.
- Comment at 161-163: do NOT add NuGet "Harmony"; the game's 0Harmony.dll v2 is referenced.
- Linux build: `Microsoft.NETFramework.ReferenceAssemblies.net471/net48` 1.0.3 when `OS != Windows_NT` (168-175).
- **ILRepack** (182-187): when `IsPacked=true`, `PackageReference dotnet-ilrepack 2.0.45` (`ExcludeAssets=all`).

`Directory.Build.targets` (repo root):
- `Clean` target wipes `$(TargetDir)` before each build (11-19).
- `GenerateModYaml` / `GenerateModInfoYaml` (22-49) write `mod.yaml` / `mod_info.yaml` into TargetDir.
- **`ILRepack` target** (57-70), `AfterTargets=Build`, condition `IsPacked=true`: runs `dotnet <nuget>/dotnet-ilrepack/2.0.45/tools/net8.0/any/ILRepackTool.dll /out:$(TargetPath) $(TargetPath) <others...> /lib:$(GameLibsFolder)`, packing all dlls in TargetDir **excluding** `*Harmony.dll`, `Splat.dll`, `Assembly-*`, `*_public.dll`, `Newtonsoft.Json`, `System.*`, `Microsoft.*`, `Unity*` — i.e. **UtilLibs.dll + PLib.dll get ILRepacked INTO BuildDoorOverWall.dll** (consistent with bin/Debug containing all three dlls before final pack).
- `CopyModsToDevFolder` (78-105), `AfterTargets=ILRepack`: copies dll+pdb+mod.yaml+mod_info.yaml (+ `ModAssets/`) to `$(ModFolder)/$(TargetName)_dev/` (= `$(HOME)/ONI/mods/BuildDoorOverWall_dev/`). In this sandbox that path is read-only → this step fails with MSB3027, artifacts stay in bin/ (per AGENTS.md).

Generated metadata (bin/Debug/net48/):
- `mod.yaml`: `title: 'BuildDoorOverWall'`, `description: ""`, `staticID: BuildDoorOverWall`.
- `mod_info.yaml`: `minimumSupportedBuild: 700386`, `version: 0.0.1`, `APIVersion: 2`.

### 1.8 Grounding of patched targets in decompiled game sources

All targets verified present in `/home/apkawa/code/ONI_MODS/Assembly-CSharp`:

| Mod patch target | Game source | Visibility/signature |
|---|---|---|
| `BuildingDef.IsValidPlaceLocation` (programmatic, OnLoad) | `BuildingDef.cs:1120` | `public bool IsValidPlaceLocation(GameObject source_go, int cell, Orientation orientation, bool replace_tile, out string fail_reason, bool restrictToActiveWorld)` — matches the 6-arg `GetMethodSafe` resolution exactly. Other overloads at :1098, :1104, :1115. |
| `BuildingDef.IsValidReplaceLocation` | `BuildingDef.cs:1184` | `public bool IsValidReplaceLocation(Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer)` |
| `BuildTool.InstantBuildReplace` | `BuildTool.cs:390` | `private GameObject InstantBuildReplace(int cell, Vector3 pos, GameObject tile)` — **private** → patched by name string, needs publicized access; called from `BuildTool.cs:381` as `InstantBuildReplace(cell, pos, replacementCandidate)`. Mod's prefix `___def` field access binds to **private field** `BuildTool.def` (`BuildTool.cs:20`, `private BuildingDef def;`). |
| `DoorConfig.CreateBuildingDef` | `DoorConfig.cs:10` | `public override BuildingDef CreateBuildingDef()` (door def 1×2, `"door_internal_kanim"`, line 12) |
| `DoorConfig.DoPostConfigureComplete` | `DoorConfig.cs:30` | `public override void DoPostConfigureComplete(GameObject go)` — mod's `Postfix(ref GameObject go)` binds to the `go` parameter. |
| `LadderConfig.CreateBuildingDef` | `LadderConfig.cs:8` | `public override BuildingDef CreateBuildingDef()` (ladder def 1×1, `"ladder_kanim"`, line 10); `LadderConfig` at `LadderConfig.cs:4` (`public class LadderConfig : IBuildingConfig`). |
| `GameTags` | `GameTags.cs:6` | `public class GameTags` — **global namespace**, no using needed. |
| `KPrefabID` | not in Assembly-CSharp decomp (lives in `Assembly-CSharp-firstpass`) | `KPrefabID.AddTag(...)` called at Mod.cs:108 with a single `Tag` arg. |

`GetMethodSafe` (used at Mod.cs:32 on `typeof(BuildingDef)`) is **not** a BCL `Type` method and not in UtilLibs — it is resolved from **PeterHan.PLib** (`using PeterHan.PLib.Core;` at Mod.cs:6); the string `GetMethodSafe` is present inside `PLib.dll` (checked via strings on bin artifact). PLib comes in transitively: `UtilLibs.csproj:27` `<PackageReference Include="PLib" Version="4.19.0" />` (`IsMod=false`, `DoNotBuildAsMod=true`, `IsPacked=false` in UtilLibs).

### 1.9 Namespace/usings facts (Mod.cs)

- Explicit usings (1-6): `System`, `System.Reflection`, `HarmonyLib`, `UnityEngine`, `KMod`, `PeterHan.PLib.Core`.
- Unqualified game types (`BuildingDef`, `DoorConfig`, `LadderConfig`, `Grid`, `Rotatable`, `ObjectLayer`, `Tag`, `Orientation`, `GameTags`, `KPrefabID`, `CellOffset`) resolve via (a) the enclosing namespace `OxygenNotIncluded.Mods.Example` → `OxygenNotIncluded` walk-up for `OxygenNotIncluded`-namespaced types, and (b) global namespace for `GameTags` etc. That is why the template namespace `OxygenNotIncluded.Mods.Example` is load-bearing for unqualified game-type resolution, despite being a "template leftover".
