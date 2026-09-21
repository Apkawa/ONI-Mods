# Research: mechanical airlock over pneumatic door (BuildDoorOverWall)

Superficial context only — facts, no solutions.

## Q1. Game log (`/home/apkawa/code/ONI_MODS/Apkawa_ONI_Mods/.tmp/Player_door_bug.log`, 1812 lines)

### Mod load / patches (15:17:27, lines 209-216)

```
[15:17:27.852] [1] [INFO] [PLib/BuildDoorOverWall] Build <date> commit=<hash>
[15:17:27.853] [1] [INFO] [PLib/BuildDoorOverWall] hover-text/visualizer postfix patch attached to BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, String)
[15:17:27.859] [1] [INFO] [PLib/BuildDoorOverWall] preview-tint postfix patch attached to BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)
[15:17:27.864] [1] [INFO] [PLib/BuildDoorOverWall] upper-cell replacement fallback postfix patch attached to BuildTool.TryBuild(Int32)
[15:17:27.860] [1] [INFO] [PLib/BuildDoorOverWall] all-door replacement metadata postfix patch attached to Assets.AddBuildingDef(BuildingDef)
[15:17:27.870] [1] [INFO] [PLib/BuildDoorOverWall] 6-arg HasDoor-bypass postfix patch attached to BuildingDef.IsValidPlaceLocation(GameObject, Int32, Orientation, Boolean, String, Boolean)
[15:17:27.875] [1] [INFO] [PLib/BuildDoorOverWall] construction-recheck postfix patch attached to BuildingDef.IsValidBuildLocation(GameObject, Int32, Orientation, Boolean, String)
[15:17:27.875] [1] [INFO] [PLib/BuildDoorOverWall] same-PrefabID door-over-door prefix patch attached to BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, Int32)
```

(Actual order in log: lines 209-216, 0Harmony v2 patches attached programmatically. A duplicate mod load of `BuildDoorOverWall [DEBUG]`/`BuildDoorOverWall` staticIDs appears at lines 118/158/206/344/347 — two mod folders loaded: dev + release.)

### Building IDs confirmed (repo `BuildDoorOverWall/AGENTS.md` door table)

- Пневматическая дверь (pneumatic door) = `DoorConfig`, PrefabID **`Door`**
- Механический шлюз (mechanical airlock) = `PressureDoorConfig`, PrefabID **`PressureDoor`**
- Ручной шлюз (manual airlock) = `ManualPressureDoorConfig`, PrefabID **`ManualPressureDoor`**

### Working attempts earlier in the session (ManualPressureDoor, 15:19:18–15:19:21, lines 1011-1050)

```
[15:19:18.772] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=ManualPressureDoor cell=97968 orient=Neutral => PASS 
...
[15:19:19.310] [1] [INFO] [PLib/BuildDoorOverWall] TryBuild postfix: def=ManualPressureDoor cell=100178 — replacement-plan уже есть (ManualPressureDoorUnderConstruction)
[15:19:19.318] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=ManualPressureDoor cell=100178 orient=Neutral => FAIL ReplacementLayer occupied in the door area
```

→ A ManualPressureDoor (manual airlock) replacement plan was successfully created over cell 100178 (object name `ManualPressureDoorUnderConstruction`), consistent with the user report that manual airlock over doors works.

### The failing attempts (last entries, 15:20:30–15:20:33)

All entries below are logged by `[PLib/BuildDoorOverWall]` (the mod's own DEBUG logging), no game-side error is visible in the log. Pattern repeats every ~0.2 s (user holding/re-clicking build). Two separate door locations:

**Location A: cells 100178 / 100914, pos (82.50, 137.01, -19.50):**

```
[15:20:30.359] [1] [INFO] [PLib/BuildDoorOverWall] TryBuild postfix: клетка 100178 — найден кандидат DoorComplete
[15:20:30.359] [1] [INFO] [PLib/BuildDoorOverWall] TryBuild postfix: клетка 100914 — кандидат уже найден (DoorComplete)
[15:20:30.359] [1] [INFO] [PLib/BuildDoorOverWall] def.TileLayer=FoundationTile def.ReplacementLayer=ReplacementTile ObjectLayer.NumLayers=NumLayers
[15:20:30.359] [1] [INFO] [PLib/BuildDoorOverWall] orientation=R180; __instance.buildingOrientation=Neutral
[15:20:30.359] [1] [INFO] [PLib/BuildDoorOverWall] def.TryReplaceTile(PressureDoorPreview(7118404)_visualizer (UnityEngine.GameObject),(82.50, 137.01, -19.50),R180,System.Collections.Generic.List`1[Tag], DEFAULT_FACADE) => plan=
```
(repeated 8× from 15:20:30.359 to 15:20:31.461 — `plan=` is EMPTY every time)

**Hover gate verdicts around the attempts (15:20:31.6–15:20:31.9):**

```
[15:20:31.645] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=99442 orient=Neutral => PASS 
[15:20:31.712] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=98706 orient=Neutral => PASS 
[15:20:31.761] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=98707 orient=Neutral => PASS 
[15:20:31.766] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=98707 orient=Neutral => PASS 
[15:20:31.814] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=97971 orient=Neutral => FAIL no replacement candidate in the door area
[15:20:31.814] [1] [INFO] [PLib/BuildDoorOverWall] gate tint: def=PressureDoor cell=97971 orient=Neutral => FAIL no replacement candidate in the door area
```

**Location B: cells 97234 / 97970, pos (82.50, 133.01, -19.50):**

```
[15:20:32.205] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=97234 orient=Neutral => FAIL native IsValidPlaceLocation(replace_tile:true) failed: Порты автоматизации не могут совпадать
[15:20:32.266] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=97233 orient=Neutral => PASS 
[15:20:32.313] [1] [INFO] [PLib/BuildDoorOverWall] gate hover: def=PressureDoor cell=97234 orient=Neutral => FAIL native IsValidPlaceLocation(replace_tile:true) failed: Порты автоматизации не могут совпадать
[15:20:32.716] [1] [INFO] [PLib/BuildDoorOverWall] TryBuild postfix: клетка 97234 — найден кандидат DoorComplete
[15:20:32.716] [1] [INFO] [PLib/BuildDoorOverWall] TryBuild postfix: клетка 97970 — кандидат уже найден (DoorComplete)
[15:20:32.716] [1] [INFO] [PLib/BuildDoorOverWall] def.TileLayer=FoundationTile def.ReplacementLayer=ReplacementTile ObjectLayer.NumLayers=NumLayers
[15:20:32.716] [1] [INFO] [PLib/BuildDoorOverWall] orientation=R180; __instance.buildingOrientation=Neutral
[15:20:32.716] [1] [INFO] [PLib/BuildDoorOverWall] def.TryReplaceTile(PressureDoorPreview(7118404)_visualizer (UnityEngine.GameObject),(82.50, 133.01, -19.50),R180,System.Collections.Generic.List`1[Tag], DEFAULT_FACADE) => plan=
```
(repeated 7× from 15:20:32.716 to 15:20:33.783 — `plan=` is EMPTY every time)

**Game shutdown (end of file):**

```
Game.OnApplicationQuit()
[15:20:40.312] [1] [INFO] [PLib/SandboxTools] Destroying FilteredDestroyTool
[Physics::Module] Cleanup current backend.
...
```

### Log facts / failure signature

- The dragged def is **PressureDoor** (mechanical airlock) — visualizer GameObject named `PressureDoorPreview(7118404)_visualizer`.
- The live candidate found under it is a **`DoorComplete`** object — i.e. a live pneumatic door (`Door` def; its completed building's GameObject is named `DoorComplete`).
- The mod's `BuildTool.TryBuild` postfix finds the candidate and calls `def.TryReplaceTile(...)`, which **returns an empty plan** every time (`plan=` with nothing after). No plan is ever stored; nothing is built.
- The hover gate (`TryReplacementGate`) reports, for cell 97234, that the **native** `IsValidPlaceLocation(replace_tile:true)` fails with the game's RU string **`Порты автоматизации не могут совпадать`** ("Automation ports cannot overlap").
- For location A (cells 100178/100914) no gate FAIL line for the door cells appears in the last block (gate lines there show PASS for empty cells and "no replacement candidate" for off-door cells); the failure signature there is still the empty `plan=` from `TryReplaceTile`.
- No game-side (non-mod) log lines appear in the failing section — the game logs nothing about the failed placement.

## Q2. Mod source (`BuildDoorOverWall/`)

Single source file: **`BuildDoorOverWall/Mod.cs`** (1357 lines, namespace `OxygenNotIncluded.Mods`, class `Mod : UserMod2`). csproj: `BuildDoorOverWall/BuildDoorOverWall.csproj` (net48, references game 0Harmony v2, PLib via UtilLibs).

### All Harmony patches are attached programmatically in `OnLoad(Harmony)` (Mod.cs:18-130)

| # | Target | Patch | Purpose |
|---|--------|-------|---------|
| 1 | `BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)` (BuildingDef.cs:1098) | postfix | cosmetic: suppresses false hover warning text for door replacements |
| 2 | `BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` | postfix | cosmetic: preview tint |
| 3 | `BuildTool.TryBuild(int)` (BuildTool.cs:307) | postfix | **the real work**: upper-cell replacement fallback — creates the replacement plan when native `TryBuild` produced nothing |
| 4 | `Assets.AddBuildingDef(BuildingDef)` | postfix | metadata: for every door def, adds `ObjectLayer.Building` to `ReplacementCandidateLayers` (log: `AddBuildingDef: деф=... — плитка: добавлен ObjectLayer.Building в ReplacementCandidateLayers, ReplacementTags = shared list`) |
| 5 | `BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool)` — 6-arg canonical | postfix | "HasDoor-bypass": for **wall/tile defs only** (non-door), flips the single failing `Grid.HasDoor[cell]` block (BuildingDef.cs:756-759) so wall-over-door / tile-over-door placement passes |
| 6 | `BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string)` — int core (BuildingDef.cs:1221) | postfix | construction-start re-check (Constructable.PlaceDiggables, Constructable.cs:747): re-runs the shared gate for replacement plans |
| 7 | `BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int)` | **prefix** | same-PrefabID door-over-door guard: if any cell of the dragged door's area already holds a live door of the **same** PrefabID, the prefix makes the method return null (skip) |

### Scope gates (static def tests)

```csharp
// Mod.cs:335 — what counts as a door def (composite test, covers all native doors)
private static bool IsDoorDef(BuildingDef def)
{
    GameObject go = def.BuildingComplete;
    if (go == null) return false;
    CopyBuildingSettings cbs = go.GetComponent<CopyBuildingSettings>();
    if (cbs != null && cbs.copyGroupTag == GameTags.Door) return true;
    bool hasDoorComponent = go.GetComponent<Door>() != null;
    return hasDoorComponent;
}

// Mod.cs:361 — wall-like defs (ExteriorWall, GlassExteriorWall, ThermalBlock)
private static bool IsWallDef(BuildingDef def)
    => def.ObjectLayer == ObjectLayer.Backwall && def.BuildLocationRule == BuildLocationRule.NotInTiles;

// Mod.cs:381 — foundation tile defs (Tile, MetalTile, InsulationTile, ...; also the 4 airlock doors, which are Tile-rule + FoundationTile)
private static bool IsTileDef(BuildingDef def)
    => def != null && def.BuildLocationRule == BuildLocationRule.Tile && def.TileLayer == ObjectLayer.FoundationTile;
```

No allow-list of building IDs anywhere: the mod keys on component/tag/def-property signals (Door component, `copyGroupTag == GameTags.Door`, `Replaceable`, `CanReplace`).

### The build decision — `BuildTool_TryBuild_DoorReplacement__Patch.Postfix` (Mod.cs:~750-948)

Runs after native `BuildTool.TryBuild(int)` returns (with `gameObject == null` for the failing case). Flow:

1. Gate: def must be a door (`IsDoorDef`), `visualizer` non-null, `def.ReplacementLayer != ObjectLayer.NumLayers`, `selectedElements` non-empty.
2. Area-aware candidate search: `def.RunOnArea(cell, buildingOrientation, c => def.GetReplacementCandidate(c))` over the whole door area (a door is 1x2 — candidate may be in EITHER cell); candidate must have `BuildingComplete` with `Replaceable` def and pass `def.CanReplace(candidate)` (the shared tag gate). Log lines: `TryBuild postfix: клетка {0} — найден кандидат {1}` / `кандидат уже найден ({1})`.
3. Per-cell `def.IsReplacementLayerOccupied(c)` check (must be free everywhere in the area).
4. Native element gate (BuildTool.cs:366-371): proceed only when `candidate.Def != def || selectedElements[0] != candidateElementTag` (with the snow-tag hash 1542131326 quirk substitution).
5. **Create the plan exactly like the native fallback tail (BuildTool.cs:376-377):**

```csharp
// Mod.cs:905-910
Vector3 pos = Grid.CellToPosCBC(anchorCell, Grid.SceneLayer.Building);
GameObject plan = def.TryReplaceTile(visualizer, pos, orientation, selected, __instance.facadeID);
Grid.Objects[anchorCell, (int)def.ReplacementLayer] = plan;
#if DEBUG
PUtil.LogDebug("def.TryReplaceTile({0},{1},{2},{3}, {4}) => plan={5}".F(...));
#endif
```
If `plan != null` it then mirrors `PostProcessBuild` master-priority assignment (BuildTool.cs:440-448). If `plan` is **null**, nothing is done — no further log, no error. This is exactly the log signature `=> plan=` in the failing section.

### The 6-arg "gate" (hover/tint) — `BuildingDef_IsValidPlaceLocation6_DoorReplacement__Patch`

`TryReplacementGate(def, cell, orientation, source_go)`: re-runs the shared candidate search (`TryFindReplacementCandidate`, Mod.cs:571) and then the native 6-arg check `def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason)`; logs `gate hover/tint: def={0} cell={1} orient={2} => PASS/FAIL {reason}`. For **door defs** the gate only flips the `Grid.HasDoor` block for the reverse (wall/tile-over-door) case — a door-over-door placement passes the 6-arg check only if the native check passes natively. (The HasDoor flip is the only native block a wall def hits on a door cell; door defs are not flipped here.)

### Same-PrefabID prefix — `BuildingDef_TryReplaceTile_DoorReplacement__Patch` (Mod.cs:1315-1354)

For a door def, scans the dragged def's FULL area with `GetReplacementCandidate`; if any cell holds a live door with the **same PrefabID** (`IsSameDoorPrefab`), the prefix returns null, skipping the native `TryReplaceTile` entirely. (Purpose: avoid replacing a door with an identical door; same-def + same-element is also skipped by the element gate in the TryBuild postfix.)

### Assets.AddBuildingDef postfix (Mod.cs)

For every def added by the game: if `IsDoorDef(def)` → log + add `ObjectLayer.Building` to `def.ReplacementCandidateLayers` (shared `ReplacementTags` list of door tags), making `GetReplacementCandidate(cell)` able to find a live door in the `Building` layer slot under a door def.

## Q3. Game side (decompiled `~/code/ONI_MODS/lib_sources/Assembly-CSharp/`)

### Placement funnel (all checks land in one 6-arg method)

- `BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool replace_tile, out string fail_reason, bool restrictToActiveWorld)` — **BuildingDef.cs:1120-1182**. After per-rule foundation checks it always ends with:
```csharp
// BuildingDef.cs:1181
return IsAreaClear(source_go, cell, orientation, ObjectLayer, TileLayer, replace_tile, restrictToActiveWorld, out fail_reason, !PreventBuildOverPlants);
```
- `BuildingDef.TryReplaceTile(GameObject src_go, Vector3 pos, Orientation orientation, IList<Tag> selected_elements, int layer = 0)` — **BuildingDef.cs:487-506**:
```csharp
public GameObject TryReplaceTile(GameObject src_go, Vector3 pos, Orientation orientation, IList<Tag> selected_elements, int layer = 0)
{
    GameObject gameObject = null;
    if (IsValidPlaceLocation(src_go, pos, orientation, replace_tile: true, out var _))
    {
        Constructable component = BuildingUnderConstruction.GetComponent<Constructable>();
        component.IsReplacementTile = true;
        gameObject = Instantiate(pos, orientation, selected_elements, layer);
        ...
    }
    return gameObject;   // null when IsValidPlaceLocation fails — fail_reason is discarded
}
```
- `BuildingDef.TryPlace(...)` — **BuildingDef.cs:464-485**: same shape but with `replace_tile: false` (BuildingDef.cs:467).
- Facade overload `TryReplaceTile(src_go, pos, orientation, selected_elements, string facadeID, int layer = 0)` — **BuildingDef.cs:508-527**, delegates to the 5-arg one.
- `BuildTool.TryBuild(int cell)` — **BuildTool.cs:307-388**:
  - line 323: `gameObject = def.TryPlace(visualizer, pos, buildingOrientation, selectedElements, facadeID);` (normal placement)
  - lines 350-386: **native replacement fallback** (only when `gameObject == null && def.ReplacementLayer != ObjectLayer.NumLayers`):
```csharp
GameObject replacementCandidate = def.GetReplacementCandidate(cell);          // anchor cell ONLY
def.RunOnArea(cell, buildingOrientation, (offset_cell) =>
    { if (def.IsReplacementLayerOccupied(offset_cell)) replacementLayerOccupied = true; });
if (replacementCandidate != null && !replacementLayerOccupied)
{
    BuildingComplete component = replacementCandidate.GetComponent<BuildingComplete>();
    if (component != null && component.Def.Replaceable && def.CanReplace(replacementCandidate))
    {
        Tag tag = replacementCandidate.GetComponent<PrimaryElement>().Element.tag;
        if (tag.GetHash() == 1542131326) tag = SimHashes.Snow.CreateTag();
        if (component.Def != def || selectedElements[0] != tag)
        {
            if (!flag)   // non-instant-build
            {
                gameObject = def.TryReplaceTile(visualizer, pos, buildingOrientation, selectedElements, facadeID);  // line 376
                Grid.Objects[cell, (int)def.ReplacementLayer] = gameObject;                                       // line 377
            }
            else if (def.IsValidBuildLocation(visualizer, pos, buildingOrientation, replace_tile: true)
                     && def.IsValidPlaceLocation(visualizer, pos, buildingOrientation, replace_tile: true, out fail_reason2))
                gameObject = InstantBuildReplace(cell, pos, replacementCandidate);
        }
    }
}
PostProcessBuild(flag, pos, gameObject);
```
  Note: native fallback uses `GetReplacementCandidate(cell)` on the **anchor cell only** (line 352); the mod's postfix re-runs the search over the whole door area — that is the "upper-cell" fix.

### IsAreaClear — where the checks actually fail — BuildingDef.cs:547-790

Per placement offset (rotated by orientation), then the final AND of port checks:

```csharp
// BuildingDef.cs:580-583
if (replace_tile)
{
    gameObject = GetReplacementCandidate(num);
}
```
- Occupancy of the target layer: BuildingDef.cs:586-614 — a building in the target layer blocks placement **unless** `gameObject` (the replacement candidate at this cell) is that same object (line 602: `gameObject2 != source_go && (gameObject == null || gameObject != gameObject2)`); tile-layer block at 608-613 likewise skips when the candidate occupies it.
- `BuildLocationRule.Tile` → `IsValidTileLocation(source_go, num, replace_tile, ref fail_reason)` (BuildingDef.cs:661-668, method at 792).
- `BuildLocationRule.NotInTiles` (wall defs) → **BuildingDef.cs:749-787**:
```csharp
GameObject gameObject7 = Grid.Objects[cell, 9];
if (!replace_tile && gameObject7 != null && gameObject7 != source_go) { flag = false; }
else if (Grid.HasDoor[cell])          // <-- BuildingDef.cs:756-759: hard block on door cells
{
    flag = false;
}
...
if (!flag) fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_NOT_IN_TILES;
```
- Final line of IsAreaClear — **BuildingDef.cs:789**:
```csharp
return flag && ArePowerPortsInValidPositions(source_go, cell, orientation, out fail_reason)
         && AreConduitPortsInValidPositions(source_go, cell, orientation, out fail_reason)
         && AreLogicPortsInValidPositions(source_go, cell, out fail_reason);
```

### The logic-port (automation port) check — BuildingDef.cs:1558-1619

```csharp
private bool AreLogicPortsInValidPositions(GameObject source_go, int cell, out string fail_reason)
{
    fail_reason = null;
    if (source_go == null) return true;
    List<ILogicUIElement> visElements = Game.Instance.logicCircuitManager.GetVisElements();
    LogicPorts component = source_go.GetComponent<LogicPorts>();
    if (component != null)
    {
        component.HackRefreshVisualizers();
        if (DoLogicPortsConflict(component.inputPorts, visElements) || DoLogicPortsConflict(component.outputPorts, visElements))
        {
            fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED;   // "Automation ports cannot overlap"
            return false;
        }
    }
    else { /* LogicGateBase variant, same fail string */ }
    return true;
}
```
- `DoLogicPortsConflict(ports_a, ports_b)` (BuildingDef.cs:1588-1606): true when two distinct `ILogicUIElement`s have the same `GetLogicUICell()`.
- String: `STRINGS/UI.cs:6646` → `HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED = "Automation ports cannot overlap"` (RU localization: «Порты автоматизации не могут совпадать»).
- `visElements` = `Game.Instance.logicCircuitManager.GetVisElements()` (LogicCircuitManager.cs:128-131) — the list `uiVisElements`, which contains ports of **all** logic buildings in the world:
  - Physical completed buildings: `LogicPorts.CreatePhysicalPorts()` (LogicPorts.cs:198-258) calls `Game.Instance.logicCircuitManager.AddVisElem(logicEventSender/logicEventHandler)` for every port (lines 221/256).
  - Ghosts/visualizers: `CreateVisualizers()` (LogicPorts.cs:135+) registers visualizer elements the same way.

### Port configuration per door def (the asymmetry behind the bug)

| Def | size | `LogicInputPorts` | other ports | file |
|-----|------|-------------------|-------------|------|
| `Door` (пневматическая дверь) | 1x2 | `CreateSingleInputPortList(new CellOffset(0, 0))` — input port on **anchor cell** | — | DoorConfig.cs:19 |
| `PressureDoor` (механический шлюз) | 1x2 | `DoorConfig.CreateSingleInputPortList(new CellOffset(0, 0))` — input port on **anchor cell** | power (requires power input), `IsFoundation = true`, `TileLayer = FoundationTile` | PressureDoorConfig.cs:23 |
| `ManualPressureDoor` (ручной шлюз) | 1x2 | **none** | `IsFoundation = true`, `TileLayer = FoundationTile` | ManualPressureDoorConfig.cs:10-23 |
| `BunkerDoor` (дверь бункера) | 4x1 | `CreateSingleInputPortList(new CellOffset(-1, 0))` | — | BunkerDoorConfig.cs:21 |
| `InsulatedDoor` | 1x2 | (not seen in grep; has utility/power offsets at (0,0)) | — | InsulatedDoorConfig.cs:22-31 |
| `WoodenDoor` | 1x2 | (not seen in grep; utility/power offsets at (0,0)) | — | WoodenDoorConfig.cs:18-27 |

`DoorConfig.CreateSingleInputPortList` (DoorConfig.cs:25-28) creates one input `LogicPorts.Port` with id `Door.OPEN_CLOSE_PORT_ID` at the given offset.

### Door footprint / anchor

`BuildingDef.GenerateOffsets(width, height)` (BuildingDef.cs:1795-1814): for 1x2 doors `PlacementOffsets = [(0,0), (0,1)]` — anchor is the first cell, second cell is +1 Y (Neutral orientation).

### Replacement-candidate helpers

- `BuildingDef.GetReplacementCandidate(int cell)` (BuildingDef.cs:324-345): iterates `ReplacementCandidateLayers` (set per def; doors get `ObjectLayer.Building` added by the mod's AddBuildingDef patch) and returns the `BuildingComplete` object in that cell; falls back to `TileLayer` lookup.
- `BuildingDef.CanReplace(GameObject go)` (BuildingDef.cs:278-285): `go.GetComponent<KPrefabID>().HasAnyTags(ReplacementTags)`.
- `BuildingDef.IsReplacementLayerOccupied(int cell)` (BuildingDef.cs:305-322): `Grid.Objects[cell, ReplacementLayer] != null` (+ equivalent layers).

### Construction start re-check

`Constructable.PlaceDiggables` re-checks `IsValidBuildLocation(base.gameObject, pos, orientation, IsReplacementTile)` (Constructable.cs:747 per mod comment; int core overload at BuildingDef.cs:1221) — a replacement plan whose re-check fails gets cancelled ("Место не подходит для стройки"). The mod patches that overload for its replacement plans (Bug A fix, Mod.cs:88-106).

### Fact summary of the failing mechanism (no solution designed, just the chain)

1. User drags `PressureDoor` (mechanical airlock, 1x2, logic input port at its anchor cell (0,0)) onto a cell of a live `Door` (pneumatic door, 1x2, logic input port at ITS anchor cell (0,0)).
2. Native `BuildTool.TryBuild`: `TryPlace` fails (occupied); native replacement fallback finds a candidate (when anchor is on the door) and calls `def.TryReplaceTile(visualizer, ...)`.
3. `TryReplaceTile` (BuildingDef.cs:490) re-checks `IsValidPlaceLocation(src_go = visualizer, replace_tile: true)`.
4. `IsAreaClear`'s final port check `AreLogicPortsInValidPositions` (BuildingDef.cs:789 → 1558) compares the dragged visualizer's logic port cell against ALL `logicCircuitManager` vis elements, which include the live door's still-registered physical port (LogicPorts.cs:221/256). Same cell → `DoLogicPortsConflict` true → `HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED` → check fails → `TryReplaceTile` returns null.
5. The mod's `TryBuild` postfix (Mod.cs:909) calls the same `def.TryReplaceTile` with the same visualizer → same null result → `Grid.Objects[anchorCell, ReplacementLayer] = null` → nothing built, no further log. The log's `=> plan=` (empty) is exactly this.
6. `ManualPressureDoor` has no logic ports at all (ManualPressureDoorConfig.cs) → no conflict → manual airlock over a door works, matching the user report and the log's successful `ManualPressureDoorUnderConstruction` plan.
7. The hover gate log for cell 97234 directly shows the native reason: `native IsValidPlaceLocation(replace_tile:true) failed: Порты автоматизации не могут совпадать`; neighboring cell 97233 (anchor on the door's non-anchor cell / offset) PASSes the gate, consistent with port-cell dependence.
