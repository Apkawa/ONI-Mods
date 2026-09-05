# research.md — fix_door_wall_render (context gathering, Ralph round 1)

Goal: gather ALL facts (no fix design, no code changes) for the bug: placing a door with its LOWER cell in air and UPPER cell in a wall/foundation breaks wall rendering (wall "splits into blocks", borders/edges visible); cancel does not fix rendering. Broken doors: ManualPressureDoor, InsulatedDoor, PressureDoor. Working: Door (Pneumatic), WoodenDoor.

Source roots:
- Mod: `BuildDoorOverWall/` (repo root /home/apkawa/code/ONI_MODS/Apkawa_ONI_Mods)
- Decompiled game: `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/` and `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp-firstpass/`
- In-game log: `.tmp/game_logs_2.md`

## 1. Mod patches (BuildDoorOverWall/Mod.cs, 601 lines, read in full round 1)

`namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2`. All FOUR patch classes are attached PROGRAMMATICALLY in `OnLoad` via `harmony.Patch(MethodBase, ...)`; none carries `[HarmonyPatch]` attributes (PatchAll ignores them). Targets:

1. `BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch` — postfix on the 4-arg overload `BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)` (BuildingDef.cs:1098). Cosmetic only (hover card + UpdateVis tint). Postfix body (Mod.cs:342-356):
   ```csharp
   public static void Postfix(BuildingDef __instance, GameObject __0, Vector3 __1, Orientation __2, ref bool __result)
   {
       if (__result) { return; }
       if (IsReplacementPlacementPossible(__instance, __0, Grid.PosToCell(__1), __2))
       {
           __result = true;
       }
       else { }
   }
   ```
2. `BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch` — postfix on `BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` (preview tint input, only caller BuildTool.cs:178). Postfix body (Mod.cs:372-386):
   ```csharp
   public static void Postfix(BuildingDef __instance, Vector3 pos, Orientation orientation, ObjectLayer replace_layer, ObjectLayer obj_layer, ref bool __result)
   {
       if (__result || !IsDoorDef(__instance)) { return; }
       if (IsReplacementPlacementPossible(__instance, null, Grid.PosToCell(pos), orientation))
       {
           __result = true;
       }
       else { }
   }
   ```
3. `BuildTool_TryBuild_DoorReplacement__Patch` — postfix on private `BuildTool.TryBuild(int cell)` (Mod.cs:407-599). Full exact code:
   ```csharp
   public static class BuildTool_TryBuild_DoorReplacement__Patch
   {
       public static void Postfix(BuildTool __instance, int __0)
       {
           BuildingDef def = __instance.def;
           if (def == null || !IsDoorDef(def))
           {
               return; // (DEBUG log)
           }
           if (def.ReplacementLayer == ObjectLayer.NumLayers)
           {
               return; // (DEBUG log)
           }
           // Mirror TryBuild's early-return guard (BuildTool.cs:309).
           GameObject visualizer = __instance.visualizer;
           if (visualizer == null)
           {
               return; // (DEBUG log)
           }
           if (Grid.PosToCell(visualizer) != __0 && (def.BuildingComplete.GetComponent<LogicPorts>() != null || def.BuildingComplete.GetComponent<LogicGateBase>() != null))
           {
               return; // (DEBUG log: logic building, visualizer in other cell)
           }
           // A replacement plan already at the anchor means placement already happened.
           if (Grid.Objects[__0, (int)def.ReplacementLayer] != null)
           {
               return; // (DEBUG log)
           }
           // Instant-build mode: do not interfere.
           if (DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild))
           {
               return; // (DEBUG log)
           }
           IList<Tag> selected = __instance.selectedElements;
           if (selected == null || selected.Count == 0)
           {
               return; // (DEBUG log)
           }
           // Area-aware candidate with the native gate (BuildTool.cs:352-364).
           GameObject candidate = null;
           def.RunOnArea(__0, __instance.buildingOrientation, (c) =>
           {
               if (candidate != null) { return; }
               GameObject local = def.GetReplacementCandidate(c);
               if (local == null) { return; }
               BuildingComplete complete = local.GetComponent<BuildingComplete>();
               if (complete == null || !complete.Def.Replaceable) { return; }
               if (!def.CanReplace(local)) { return; }
               candidate = local;
           });
           if (candidate == null) { return; }
           // The replacement layer must be unoccupied in every door cell (BuildTool.cs:354-360).
           bool occupied = false;
           def.RunOnArea(__0, __instance.buildingOrientation, (c) =>
           {
               if (def.IsReplacementLayerOccupied(c)) { occupied = true; }
           });
           if (occupied) { return; }
           // Native element gate (BuildTool.cs:366-371); 1542131326 hash = native snow-tag quirk.
           Tag tag = candidate.GetComponent<PrimaryElement>().Element.tag;
           if (tag.GetHash() == 1542131326) { tag = SimHashes.Snow.CreateTag(); }
           if (candidate.GetComponent<BuildingComplete>().Def == def && selected[0] == tag)
           {
               return;
           }
           // Create the plan exactly like the native fallback tail (BuildTool.cs:377-379).
           Vector3 pos = Grid.CellToPosCBC(__0, Grid.SceneLayer.Building);
           GameObject plan = def.TryReplaceTile(visualizer, pos, __instance.buildingOrientation, selected, __instance.facadeID);
           Grid.Objects[__0, (int)def.ReplacementLayer] = plan;
           // The native PostProcessBuild already ran with a null build result, so mirror its
           // master-priority assignment (BuildTool.cs:440-448); placement sound skipped.
           if (plan != null)
           {
               Prioritizable prioritizable = plan.GetComponent<Prioritizable>();
               if (prioritizable != null)
               {
                   if (BuildMenu.Instance != null) { prioritizable.SetMasterPriority(BuildMenu.Instance.GetBuildingPriority()); }
                   if (PlanScreen.Instance != null) { prioritizable.SetMasterPriority(PlanScreen.Instance.GetBuildingPriority()); }
               }
           }
           // (all return branches and success paths carry #if DEBUG PUtil.LogDebug lines; stripped above)
       }
   }
   ```
   Facts: fires when native `TryBuild` produced no replacement plan at the ANCHOR (lower door cell). `pos` passed to `TryReplaceTile` is the anchor's CBC position (`Grid.CellToPosCBC(__0, Grid.SceneLayer.Building)`). The plan GameObject is written ONLY to `Grid.Objects[anchor, (int)def.ReplacementLayer]`. Postfix does NOT touch `Grid.Objects` of the wall cell, does NOT destroy the candidate, does NOT call PostProcessBuild (only mirrors the master-priority assignment).
4. `Assets_AddBuildingDef_DoorReplacement__Patch` — postfix on `Assets.AddBuildingDef(BuildingDef)` (Assets.cs:670, static; sole caller `BuildingConfigManager.RegisterBuilding`, BuildingConfigManager.cs:114). Exact code (Mod.cs:167-191):
   ```csharp
   public static void Postfix(BuildingDef __0)
   {
       if (__0 == null || !IsDoorDef(__0)) { return; }
       // Idempotent: never override a def that already carries its own replacement metadata.
       if (__0.ReplacementLayer != ObjectLayer.NumLayers) { return; }
       __0.ReplacementLayer = ObjectLayer.ReplacementTile;
       __0.ReplacementCandidateLayers = new List<ObjectLayer>()
       {
           ObjectLayer.FoundationTile,
           ObjectLayer.Backwall
       };
       __0.ReplacementTags = new List<Tag>()
       {
           GameTags.FloorTiles,
           GameTags.Backwall,
           GameTags.Ladders
       };
   }
   ```

`IsDoorDef` helper (Mod.cs:222-236) — exact:
```csharp
private static bool IsDoorDef(BuildingDef def)
{
    GameObject go = def.BuildingComplete;
    if (go == null) { return false; }
    CopyBuildingSettings cbs = go.GetComponent<CopyBuildingSettings>();
    if (cbs != null && cbs.copyGroupTag == GameTags.Door) { return true; }
    bool hasDoorComponent = go.GetComponent<Door>() != null;
    return hasDoorComponent;
}
```

Helper `IsReplacementPlacementPossible(BuildingDef def, GameObject source_go, int cell, Orientation orientation)` (Mod.cs:246-307): gates = IsDoorDef, ReplacementLayer != NumLayers && CandidateLayers != null, area-aware candidate search (RunOnArea + single-cell GetReplacementCandidate, BuildingComplete.Def.Replaceable, def.CanReplace), per-cell `def.IsReplacementLayerOccupied(c)`, then 6-arg `def.IsValidPlaceLocation(source_go, cell, orientation, replace_tile: true, out fail_reason, restrictToActiveWorld: false)`.

`FindMethod(Type, string, params Type[])` (Mod.cs:116-149): resolves method by name + per-parameter underlying type, normalizing byref (`out T` reported as T& → GetElementType), BindingFlags Public|NonPublic|Instance|Static|DeclaredOnly.

OnLoad (Mod.cs:17-104) resolves the 4 targets and patches each via `harmony.Patch(method, postfix: new HarmonyMethod(typeof(...), nameof(...Postfix)))`; logs Debug.LogError and skips when a target is not found (game build mismatch guard).

## 2. Known Harmony pitfalls (from fix_buld_door plan.md + research.md — do NOT re-learn)

From `.dumbspec/current/fix_buld_door/plan.md` (rework records, both caused in-game crashes at mod load) and Mod.cs comments:

1. **No `[HarmonyPatch]` attributes for targets with `out`/`ref` parameters.** The game's Harmony v2 resolves attribute targets via `Type.GetMethod(name, allDeclared, null, paramTypes, [])` (0Harmony AttributePatch/DeclaredMethod, `.tmp/harmony_decomp` ~:9461); plain `typeof(string)` does NOT match an `out string` parameter — only `typeof(string).MakeByRefType()` matches, and a byref Type is not a legal attribute constant expression (CS0182). Solution used: programmatic `harmony.Patch(MethodBase, postfix: new HarmonyMethod(...))` in OnLoad, with a byref-normalizing manual method matcher (isByRef → GetElementType; `FindMethod` in Mod.cs:116).
2. **Postfix parameter naming: only positional `__N` works for original args.** In this Harmony build's `EmitCallParameter` (~:4444) any `__`-prefixed parameter name that is not one of the special names (`__instance`, `__originalMethod`, `__args`, `__result`, `__resultRef`, `__state`, `__exception`, `__runOriginal`) is parsed as a POSITIONAL index via `int.TryParse` — a name like `__out_fail_reason` crashes with "does not contain a valid index". There is NO `__out_` postfix convention in this Harmony version, and the both-byref emission branch only emits a value load (`Ldarg`), so **an `out` parameter cannot be written back from a postfix at all**. Use `__0/__1/...` (original parameter indices) and only flip `ref bool __result` when you need to change the return value.
3. **PatchAll only processes types carrying a Harmony attribute** (PatchAllUncategorized filters by HasHarmonyAttribute, 0Harmony ~:6829) — attribute-less classes are invisible to `Harmony.PatchAll`; this is why every patch in the current Mod.cs is attached programmatically and OnLoad must resolve each target by reflection (private methods like `BuildTool.TryBuild` included; `BindingFlags.Static` was needed for the static `Assets.AddBuildingDef` target).
4. **Programmatic patching is the safe form for nontrivial signatures** (private methods, out/byref params, static targets). A loud `Debug.LogError` + skip when the target is not found is the established pattern (Mod.cs:49-103).
5. Game 0Harmony is v2 and targets .NET 4.8 — reference the game's `0Harmony.dll`, never NuGet Harmony (type identity mismatch with `UserMod2.OnLoad`).

## 3. Door defs: the five configs vs the three broken / two working

All five door configs are VANILLA (decompiled, `lib_sources/Assembly-CSharp/`); the mod
(`BuildDoorOverWall/Mod.cs`, 601 lines) defines NO door defs of its own.

| Property | Door (WORKS) `DoorConfig.cs` | WoodenDoor (WORKS) `WoodenDoorConfig.cs` | ManualPressureDoor (BROKEN) `ManualPressureDoorConfig.cs` | InsulatedDoor (BROKEN) `InsulatedDoorConfig.cs` | PressureDoor (BROKEN) `PressureDoorConfig.cs` |
|---|---|---|---|---|---|
| `CreateBuildingDef` call | `:13` — 1x2, `BuildLocationRule.Tile`, TIER2, `MATERIALS.ALL_METALS` | `:13` — 1x2, `Tile`, TIER2, `MATERIALS.WOODS` | `:12` — 1x2, `Tile`, TIER3, `ALL_METALS` | `:24` — 1x2, `Tile`, TIER5 mass, `BuildableRaw`+`BuildingFiber` | `:12` — 1x2, `Tile`, TIER4, `ALL_METALS` |
| `IsFoundation` | false (`:15`) | false (`:40`) | **true** (`:14`) | **true** (`:44`) | **true** (`:16`) |
| `TileLayer` | unset → default `ObjectLayer.NumLayers` (`BuildingDef.cs:125`) | unset → `NumLayers` | **`FoundationTile`** (`:15`) | **`FoundationTile`** (`:45`) | **`FoundationTile`** (`:18`) |
| `IsTilePiece` (`BuildingDef.cs:276`: `TileLayer != ObjectLayer.NumLayers`) | **false** | **false** | **true** | **true** | **true** |
| `ObjectLayer` (the def's Building layer) | `Building` (default, `BuildingDef.cs:123`) | `Building` | `Building` | `Building` | `Building` |
| `Entombable` | true (`:13`) | true (`:37`) | **false** (`:13`) | **false** (`:41`) | **false** (`:15`) |
| `Replaceable` | default true (`BuildingDef.cs:68`) | false (`:30`) | default true | false (`:34`) | default true |
| `LogicInputPorts` | single input port (0,0) (`:19`) | none | none | none | `DoorConfig.CreateSingleInputPortList` (`:23`) |
| `DragBuild` | false (default, `BuildingDef.cs:89`) | true (`:29`) | false | true (`:33`) | false |
| `RequiresPowerInput` | unset/false | false (`:20`) | unset/false | false (`:24`) | **true** (`:12`) |
| `SceneLayer` | default | default | `TileMain` (`:18`) | `TileMain` (`:18`) | `TileMain` (`:21`) |
| `ForegroundLayer` | `InteriorWall` (`:18`) | `InteriorWall` (`:41`) | `InteriorWall` (`:19`) | `InteriorWall` (`:17`) | `InteriorWall` (`:22`) |
| `Door.doorType` | `Internal` (`:34`) | `Internal` (`:58`) | `ManualPressure` (`:31`) | `ManualPressure` (`:55`) | (Mechanized Airlock; `doorType` not set in `DoPostConfigureComplete`) |
| `copyGroupTag = GameTags.Door` | `:38` | `:60` | `:36` | `:62` | `:41` |
| `ReplacementLayer` / `ReplacementCandidateLayers` / `ReplacementTags` | **not set natively** → defaults `NumLayers` / `null` / `null` (`BuildingDef.cs:127`, `:113`) | same | same | same | same |

**Native replacement fields do not exist on any door.** The whole "door-over-wall" feature is the mod's:
its `Assets.AddBuildingDef` postfix (Mod.cs:167-191) sets, on EVERY door def at registration (idempotent —
skips defs that already have `ReplacementLayer != NumLayers`):
`ReplacementLayer = ObjectLayer.ReplacementTile`, `ReplacementCandidateLayers = { FoundationTile, Backwall }`,
`ReplacementTags = { GameTags.FloorTiles, GameTags.Backwall, GameTags.Ladders }`. After that, the game's own
`BuildTool.TryBuild` native replacement fallback (`BuildTool.cs:350-386`) becomes reachable for doors, and the
mod's `TryBuild` postfix (Mod.cs:407-599) handles the case its anchor-only native gate misses (candidate in the
non-anchor cell). `IsDoorDef` (Mod.cs:222-236) = `CopyBuildingSettings.copyGroupTag == GameTags.Door` OR has a
`Door` component — all five configs satisfy both.

**Diff conclusion:** the shared delta of the three broken doors vs the two working doors is
`IsFoundation=true` + `TileLayer=ObjectLayer.FoundationTile` (⇒ `IsTilePiece=true`). `Entombable=false` also
correlates but is not what drives the rendering machinery. **LogicPorts is NOT the differentiator** — Door and
PressureDoor both have logic input ports; WoodenDoor and InsulatedDoor have none. `DragBuild`, power, tier and
elements also differ without correlating. The `IsTilePiece` branch is the one that routes these defs into the
TileLayer / `Grid.Objects[*, FoundationTile]` / `TileVisualizer` / `BlockTileRenderer` machinery the working doors
never touch (Constructable.cs:441-461, BuildingComplete.cs:163-170).

Vanilla foundation tile (the "wall" in the bug; log shows candidate = `TileComplete`), `TileConfig.cs:11-55`:
`BuildingTemplates.CreateBuildingDef("Tile", 1, 1, "floor_basic_kanim", 100, 3f, TIER3, RAW_MINERALS, 1600f,
BuildLocationRule.Tile, ...)` (`:11`) + `BuildingTemplates.CreateFoundationTileDef(obj)` (`:12`;
BuildingTemplates.cs:46-64: `IsFoundation=true`, `TileLayer=FoundationTile`, `ReplacementLayer=ReplacementTile`,
`ReplacementCandidateLayers={FoundationTile, LadderTile, Backwall}`, `ReplacementTags={FloorTiles, Ladders,
Backwall}`, `EquivalentReplacementLayers={ReplacementLadder}`), `Entombable=false` (`:20`),
`BlockTileAtlas = "tiles_solid"` (`:24`), `isKAnimTile=true` (`:26`), `DragBuild=true` (`:29`),
`SimCellOccupier.doReplaceElement = true` (`:38-42`), `KAnimGridTileVisualizer` (`:44`), **no Uprootable component**,
and `DoPostConfigureComplete` adds `GameTags.FloorTiles` (`:52`) — which is why `CanReplace` (the door's
`ReplacementTags` includes FloorTiles) accepts the wall.

---

## 4. Game flow, end to end (citations = decompiled `lib_sources/Assembly-CSharp/`)

### 4.1 BuildTool.TryBuild (`BuildTool.cs:307-388`)
- `:309` guard — early return if `visualizer == null`, or same cell+orientation as last drag, or
  `Grid.PosToCell(visualizer) != cell && (def.BuildingComplete.GetComponent<LogicPorts>() != null || ...LogicGateBase() != null)`.
- `:319` `flag = DebugHandler.InstantBuildMode || (SandboxModeActive && settings.InstantBuild)`.
- `:321-324` survival (non-instant): `gameObject = def.TryPlace(visualizer, pos, buildingOrientation, selectedElements, facadeID)`.
- `:325-349` instant/sandbox path: `def.IsValidBuildLocation(...) && def.IsValidPlaceLocation(...)` then uproots (`Uprootable.CompleteWork`) / digs backwalls, then `def.Build(...)` directly.
- `:350-386` native replacement fallback (only if `gameObject == null && def.ReplacementLayer != ObjectLayer.NumLayers`
  — for doors this condition is true ONLY after the mod's `Assets.AddBuildingDef` postfix sets the fields,
  Mod.cs:179-190; a vanilla door def has no replacement metadata at all):
  - `:352` `replacementCandidate = def.GetReplacementCandidate(cell)` — **anchor cell only**.
  - `:353-360` `replacementLayerOccupied` = any area cell with `def.IsReplacementLayerOccupied(offset_cell)`.
  - `:361-384` requires `candidate != null && !replacementLayerOccupied`, candidate has `BuildingComplete` with
    `component.Def.Replaceable && def.CanReplace(replacementCandidate)`, and `component.Def != def || selectedElements[0] != tag`;
    non-instant: `gameObject = def.TryReplaceTile(visualizer, pos, buildingOrientation, selectedElements, facadeID);`
    then `Grid.Objects[cell, (int)def.ReplacementLayer] = gameObject;` (`:376-377`).
  - instant: `InstantBuildReplace` (`:390-431`) destroys non-anchor candidates + anchor tile via `SimCellOccupier.DestroySelf`, then `def.Build`.
- `:387` `PostProcessBuild(flag, pos, gameObject)`.

### 4.2 BuildingDef.TryPlace / TryReplaceTile / GetReplacementCandidate / CanReplace
- `TryPlace` overloads at `BuildingDef.cs:454,459,464` — validates via `IsValidPlaceLocation` then instantiates a
  `Constructable` prefab (the normal "plan" path). `IsValidPlaceLocation` (6-arg canonical, `BuildingDef.cs:1098`
  overload chain, final gate `IsAreaClear(source_go, cell, orientation, ObjectLayer, TileLayer, replace_tile, ...)`
  at `:1181`; `ObjectLayer` default = `ObjectLayer.Building`, `BuildingDef.cs:123`) FAILS in ANY door-over-wall
  orientation: in `IsAreaClear` (`BuildingDef.cs:547-641`), each area cell's `ObjectLayer.Building` occupant is
  tolerated ONLY when its `Def.BuildLocationRule` is in the enum range 8-10 (`(uint)(rule - 8) <= 2u`,
  `:593-598` — Conduit/LogicBridge/WireBridge per `BuildLocationRule.cs:1-24`); a wall (`BuildLocationRule.Tile` = 6,
  TileConfig.cs:11) is not in that range, and `Uprootable.CanUproot(wall)` is false (Tile has no Uprootable) ⇒
  `HELP_BUILDLOCATION_OCCUPIED` (`:600-607`). The `replace_tile`/`GetReplacementCandidate` exception (`:580-583`) only
  applies on the `TryReplaceTile` path (`replace_tile: true`), not on plain `TryPlace`. So with a wall in either door
  cell, plain `TryPlace` never produces a plan — the plan in ALL door-over-wall orientations comes from the
  replacement fallback (native or mod postfix), and in the broken orientation (anchor in air) the native fallback's
  anchor-only candidate search (`BuildTool.cs:352`) finds nothing ⇒ the mod's `TryBuild` postfix (Mod.cs:407-599)
  creates the plan with an area-aware candidate search.
- `TryReplaceTile` (`BuildingDef.cs:487-506` and facadeID overload `:508+`):
  gate `IsValidPlaceLocation(src_go, pos, orientation, replace_tile: true, out _)` (`:1104` overload), then
  on the `BuildingUnderConstruction` prefab template sets `Constructable.IsReplacementTile = true`,
  `Instantiate(pos, orientation, selected_elements, layer)`, resets it to false. It does NOT destroy the candidate,
  does NOT remove it from its layer, does NOT touch `Grid.Objects` of the wall cell.
- `GetReplacementCandidate` (`BuildingDef.cs:324-345`): for each layer in `ReplacementCandidateLayers`, read
  `Grid.Objects[cell, layer]`; if the go's `KPrefabID.HasAnyTags(ReplacementTags)` and it matches the
  `EquivalentReplacementLayers`/`ReplacementCandidateLayers` rules → return it.
  ROUND-2 CORRECTION (verified verbatim against BuildingDef.cs:324-345): the method does NO tag check — it
  returns the first non-null go in any `ReplacementCandidateLayers` entry that has a `BuildingComplete`
  component; tag filtering is the CALLER's job via `CanReplace`. (Fallback branch when
  `ReplacementCandidateLayers == null`: returns the cell's own `TileLayer` entry, no BuildingComplete check.)
- `CanReplace` (`BuildingDef.cs:278-285`): `KPrefabID.HasAnyTags(ReplacementTags)` (false if
  `ReplacementTags == null`).
- **LogicPorts question (task item 2), round-2 verified:** `TryReplaceTile` (`BuildingDef.cs:487-527`) contains
  **no LogicPorts/logic code at all** — it only gates on `IsValidPlaceLocation(replace_tile: true)`, flips
  `Constructable.IsReplacementTile` on the `BuildingUnderConstruction` template, calls `Instantiate`
  (BuildingDef.cs:529-540: KInstantiate at `pos + (0,0,-0.15)`, `Grid.SceneLayer.Front`, sets PrimaryElement and
  `SelectedElementsTags`, SetActive), then applies facade/orientation. The ONLY logic-related gate in the whole
  path is the `BuildTool.TryBuild` :309 guard (`LogicPorts`/`LogicGateBase` check, BuildTool.cs:308-312), which
  the mod's postfix mirrors (Mod.cs:59-62): logic buildings refuse to build while the visualizer sits in another
  cell — unrelated to wall replacement.

### 4.3 Plan spawn: Constructable (the state mutation that breaks rendering)
`Constructable.cs`:
- `:64` `[Serialize] public bool IsReplacementTile;`
- `OnSpawn` (`:322-461`):
  - `:326-329` reachability table: `IsTilePiece ? OffsetGroups.InvertedStandardTableWithCorners : InvertedStandardTable`.
  - `:345` `if (rotatable != null) MarkArea();` — runs **at spawn**.
  - `:375-407` (IsReplacementTile branch, anchor cell only): if `ReplacementLayer != NumLayers`,
    `cell = Grid.PosToCell(base.transform.GetPosition())` (anchor); if `Grid.Objects[cell, ReplacementLayer]` is null
    or already this object → assign it; `if (Def.isKAnimTile && GetComponent<SimCellOccupier>() != null)` →
    `World.Instance.blockTileRenderer.AddBlock(Overlay, Def, IsReplacementTile, SimHashes.Void, cell, isBlueprint: true)`
    (`:388`); `TileVisualizer.RefreshCell(cell, Def.TileLayer, Def.ReplacementLayer)` (`:390`).
- `MarkArea()` (`Constructable.cs:441-461`):
  ```
  layer = IsReplacementTile ? def.ReplacementLayer : def.ObjectLayer;
  def.MarkArea(num, orientation, layer, go);            // whole door area → layer 11 (both cells)
  if (!def.IsTilePiece) return;                          // :450-452
  if (Grid.Objects[num, (int)def.TileLayer] == null)    // :453 — ANCHOR cell check only
  {
      def.MarkArea(num, orientation, def.TileLayer, go); // whole door area → layer 9 (both cells!)
      def.RunOnArea(num, orientation, c => TileVisualizer.RefreshCell(c, def.TileLayer, def.ReplacementLayer));
  }
  Grid.IsTileUnderConstruction[num] = true;             // :460 — LAST statement (round-2 verified order)
  ```
  Round-2 note (source-verified): `MarkArea` = Constructable.cs:441-461; `IsTileUnderConstruction[num] = true` is
  the FINAL line (:460), set for every IsTilePiece plan even when the second MarkArea is skipped. `UnmarkArea`
  (:463-477) is guarded by an `unmarked` bool flag and ends with `ClearPendingUproots()` (Constructable.cs:479+).
  **In the broken orientation the anchor (lower) cell is air ⇒ `Grid.Objects[anchor, FoundationTile] == null` ⇒ the
  second MarkArea overwrites `Grid.ObjectLayers[FoundationTile][upperWallCell]`** — which held the wall's
  `BuildingComplete` — with the door plan GameObject.
- `BuildingDef.MarkArea` (`BuildingDef.cs:829-943`): for each area cell — `if (Uprootable.CanUproot(existing))` move it to
  layer 5, then **unconditionally** `Grid.Objects[cell2, (int)layer] = go;` (`:838-842`). No "already occupied" guard.
  (The wall has no Uprootable, so the layer-5 move does not happen for it.)
- `UnmarkArea()` (`Constructable.cs:463-477`):
  ```
  layer = IsReplacementTile ? def.ReplacementLayer : def.ObjectLayer;
  def.UnmarkArea(num, orientation, layer, go);          // ONLY the replacement/object layer
  if (def.IsTilePiece) Grid.IsTileUnderConstruction[num] = false;
  ```
  **Never unmarks/restores `def.TileLayer`.**
- `BuildingDef.UnmarkArea` (`BuildingDef.cs:976-1071`): per area cell, `if (Grid.Objects[cell2, layer] == go)` → null
  (`:986-989`). The wall's overwritten layer-9 entry is NOT the plan's, so even a layer-9 UnmarkArea would not restore
  it — but no layer-9 UnmarkArea is ever issued on cancel.

### 4.4 Construction completion (why the completed door itself is fine)
`Constructable.cs`:
- `OnCompleteWork` (`:124-221`): if `IsReplacementTile` — `GetReplacementCandidate(anchor)` only; if found,
  `SimCellOccupier.DestroySelf(FinishConstruction callback)` / Deconstructable refund / `Trigger(1606648047)` (replaced)
  / `DeleteObject()`; else `FinishConstruction` immediately.
- `FinishConstruction` (`:223-291`): if `IsReplacementTile && PlacementOffsets.Length > 1` — for every **other** area cell,
  `GetReplacementCandidate(offset_cell)` → destroy + refund + replaced-trigger (`:228-257`) — this is where the wall in the
  upper cell is destroyed in the mod flow. Then `UnmarkArea()` (`:258`) and `def.Build(cell, orientation, storage, ...)`
  (`:259`).
- `BuildingComplete.OnSpawn` (`BuildingComplete.cs:127-224`): if `Def.IsFoundation` → `Grid.Foundation[num] = true` +
  roomProber SolidChangedEvent for all placement cells (`:132-140`); `Def.MarkArea(cell, orientation, Def.ObjectLayer, go)`
  (`:162`); if `Def.IsTilePiece` → `Def.MarkArea(cell, orientation, Def.TileLayer, go)` + `TileVisualizer.RefreshCell(c,
  TileLayer, ReplacementLayer)` per cell (`:163-170`); `RegisterBlockTileRenderer()` (`:172`).
  So the finished door writes itself into layer 9 for both of its own cells — the (former wall) upper cell is now the
  door's upper cell, so the final grid state is consistent.
- `BuildingComplete.OnCleanUp` (`:241-301`): `WasReplaced()` set by `OnObjectReplaced` (`:121-125`, triggered by
  `Constructable`'s `Trigger(1606648047)` before `DeleteObject`). Non-replaced: `UnmarkArea(ObjectLayer)` + if IsTilePiece
  `UnmarkArea(TileLayer)` + refresh (`:257-268`). Replaced with `replacingTileLayer != Def.TileLayer`: UnmarkArea(TileLayer)
  (`:286-294`). Always `UnregisterBlockTileRenderer()` (`:298`). For a replaced wall (Tile, layer 9) with a door replacing it
  (also layer 9) the `replacingTileLayer != Def.TileLayer` condition is FALSE, so the wall's layer-9 entry is not unmarked —
  it had already been overwritten by the plan's MarkArea, and no one restores it.
- `Door.UpdateDoorState` (`Door.cs:737-747`): built doors set `Grid.Foundation[num] = !cleaningUp` for each placement cell —
  the Foundation *BuildFlag* (Grid.cs:25-35), unrelated to the ObjectLayer entries.

### 4.5 Cancel path (why cancel does not fix rendering)
- User-menu "action_cancel" → `Constructable.OnPressCancel` (`:856-859`) → `gameObject.Trigger(2127324410)`
  (`GameHashes.Cancel`, `GameHashes.cs:93`).
- `Cancellable.OnCancel` (`Cancellable.cs:17-20`) → `DeleteObject()` (the Constructable prefab carries Cancellable).
- `Constructable.OnCleanUp` (`:547-603`): `UnmarkArea()` — **replacement layer only**; `IsTileUnderConstruction=false`;
  uproot cleanup. No layer-9 handling.
- `BuildingUnderConstruction.OnCleanUp` (`BuildingUnderConstruction.cs:49-53`): `UnregisterBlockTileRenderer()` →
  removes the plan's own (door def, Replacement) RenderInfo cell only.
- Result after cancel: `Grid.Objects[upperWallCell, (int)FoundationTile]` still holds the **destroyed** plan GameObject;
  the wall's `BuildingComplete` is still alive, still in the wall RenderInfo's `occupiedCells`, but its grid layer-9 entry
  is gone. Nothing re-registers it. `TileVisualizer.RefreshCellInternal` (`TileVisualizer.cs:5-21`) reads
  `Grid.Objects[cell, tile_layer]` — a destroyed object is `== null` in Unity, so `Rebuild(tile_layer, cell)` is never
  re-triggered for that cell either. The stale chunk mesh (rebuilt while the plan was present) is simply never marked
  dirty again with correct data ⇒ **the border rendering persists after cancel**.

### 4.6 Why the other orientations / doors are fine
- Both of these orientations' plans are created by the **native** replacement fallback (BuildTool.cs:350-386), which is
  only reachable for doors AFTER the mod's `Assets.AddBuildingDef` postfix sets `ReplacementLayer` (Mod.cs:179-190);
  without the mod patch, a door over a wall can never be placed at all (plain `TryPlace` always fails per §4.2 and the
  fallback gate `def.ReplacementLayer != NumLayers`, BuildTool.cs:350, is false).
- Whole door in wall: anchor = wall cell ⇒ candidate found at anchor (BuildTool.cs:352) ⇒ `Grid.Objects[anchor,
  FoundationTile]` = wall (non-null) ⇒ the second `MarkArea` in `Constructable.MarkArea` is SKIPPED (`Constructable.cs`
  `if (Grid.Objects[num, TileLayer] == null)` guard) ⇒ no layer-9 overwrite.
- Lower in wall / upper in air: same — candidate at the anchor, anchor's layer-9 entry = wall ⇒ skipped.
- Door / WoodenDoor (working): `IsTilePiece == false` ⇒ `Constructable.MarkArea` returns right after the ObjectLayer/
  ReplacementLayer mark (`if (!def.IsTilePiece) return;`) ⇒ no TileLayer writes at all.
- Note: in ALL orientations the upper (wall) cell is still overwritten into **layer 11 (ReplacementTile)** by the first
  MarkArea — that layer is only read by replacement machinery, not by wall rendering, so it is harmless for rendering.

---

## 5. Wall/foundation rendering: Rendering/BlockTileRenderer.cs (835 lines)

- Per-`(BuildingDef, RenderInfoLayer{Built, UnderConstruction, Replacement})` a `RenderInfo` exists; created in
  `AddBlock` (`:754-764`) with `queryLayer = (int)(isReplacement ? def.ReplacementLayer : def.TileLayer)` (`:759`).
  `value.AddCell(cell)` (`:174-180`) bumps `occupiedCells[cell]` and `MarkDirty(chunk)`. `RemoveCell` (`:182-195`) decrements.
- `RenderInfo.Rebuild` (`:223-281`): only if `dirtyChunks[x,y]`; for each cell in `occupiedCells` (others `continue`,
  `:239-242`): `Bits connectionBits = renderer.GetConnectionBits(j, i, queryLayer)` (`:243`); pick the first atlas variant
  whose `requiredConnections ⊆ bits` and no `forbiddenConnections` overlap (`:244-254`); `AddVertexInfo` (`:283-340`):
  **for every side WITHOUT a connection bit the vertex is pushed out by 0.25** (`:289-320`) — i.e. an unconnected side is
  drawn as a visible inset edge. This is exactly the "splits into individual blocks / visible block borders" symptom.
- `GetConnectionBits(x, y, query_layer)` (`:581-628`): center def from `Grid.Objects[cell, query_layer]`'s own
  `GetComponent<Building>().Def` (`:584-585`); each of 8 neighbours tested with `MatchesDef(Grid.Objects[neighbor,
  query_layer], def)` (`:572-579` → `go.GetComponent<Building>().Def == def`).
- `Rebuild(ObjectLayer layer, int cell)` (`:775-784`): marks `cell` dirty for **every** RenderInfo whose
  `def.TileLayer == layer` — this is what `TileVisualizer.RefreshCellInternal` calls (`TileVisualizer.cs:14`).
- `LateUpdate → Render()` (`:692-724`): rebuilds only dirty chunks in the visible area.
- Registration: `Building.RegisterBlockTileRenderer` (`Building.cs:232-251`) — gate `Def.BlockTileAtlas != null` and
  PrimaryElement; `cell = Grid.PosToCell(base.transform.GetPosition())` = **anchor cell only**;
  `isReplacement = GetComponent<Constructable>()?.IsReplacementTile`. `Unregister` (`:323-337`) → `RemoveBlock`.
  `BuildingComplete.OnSpawn` `:172` registers (anchor cell); `OnCleanUp` `:298` unregisters.
  `BuildingUnderConstruction.OnSpawn` `:46` registers with `isBlueprint: true`.
- Vanilla destruction/render notification: there is no explicit "wall destroyed" event to BlockTileRenderer; the only
  path is `TileVisualizer.RefreshCell → Rebuild(layer, cell)` (dirty marking) driven by whoever changes a layer-9 entry
  (spawns, `BuildingComplete.OnCleanUp` unmark+refresh, `Door` close/open `groundRenderer.MarkDirty` etc.). A layer-9
  overwrite that is never undone produces no further dirty-marking for the affected cells ⇒ stale mesh.
- **Backwall side (round-2 check):** `rendering/BackWall.cs` (namespace `rendering`, 15 lines) is NOT a renderer — a
  `MonoBehaviour` holding `backwallMaterial` + a `Texture2DArray` and setting the material's "images" texture in
  `Awake` only. Backwall tiles are rendered per-cell by the same `BlockTileRenderer` machinery: backwall buildings
  are `BuildingComplete` on `ObjectLayer.Backwall`, `AddBlock`'s `queryLayer = isReplacement ? def.ReplacementLayer
  : def.TileLayer` (BlockTileRenderer.cs:759) picks the backwall def's TileLayer, and `Rebuild(ObjectLayer, cell)`
  (:775) targets every RenderInfo with `def.TileLayer == layer` — i.e. the identical notification API as
  foundations. (The vanilla plain-backwall def class is not in the decompiled sources — game data; `FilteredDragTool.cs:203`
  returns the `"BackWall"` id, `GameTags.cs:757` defines `GameTags.Backwall`.) The in-game log's candidate was
  `TileComplete` (foundation), so the foundation path above is the one implicated in this bug.

---

## 6. .tmp/game_logs_2.md inspection

Log (Russian annotations, mod log lines from a prior iteration of the mod; the flow matches the current Mod.cs
postfix):
- Candidate found in all cases is **`TileComplete`** — i.e. the "wall" is the vanilla **Tile** floor
  (`TileConfig.cs`) — confirming `IsFoundation=true, TileLayer=FoundationTile, BlockTileAtlas="tiles_solid",
  SimCellOccupier.doReplaceElement=true, no Uprootable`.
- Broken orientation ("нижним в воздухе, верхним в стене"): anchor cell 120075 (no candidate) / candidate TileComplete at
  120811 (upper cell) → "создан replacement-plan ManualPressureDoorUnderConstruction", priority from PlanScreen.
  This is exactly the state where `Constructable.MarkArea`'s `Grid.Objects[anchor, FoundationTile] == null` guard passes
  and the upper wall cell's layer-9 entry gets overwritten by the plan.
- Working Door run (same geometry, def=Door): identical plan creation — the only difference is the def's
  `IsTilePiece=false`, so no layer-9 write occurs.
- No error/warning lines, no cancel events recorded in this log; the log cannot show the render breakage itself but does
  corroborate the plan-creation state in all three tested orientations.

---

## 7. Final report (round 1)

### Key facts
1. **Rendering of walls/foundations is driven by `BlockTileRenderer`**, keyed per `(BuildingDef, RenderInfoLayer)`; each
   wall cell renders only if in `occupiedCells`, and its atlas variant is chosen from 8 connection bits computed by
   `GetConnectionBits` reading `Grid.Objects[neighbor, def.TileLayer]` (BuildingDef.TileLayer = `FoundationTile` = 9).
   Missing/unmatched neighbour ⇒ 0.25px-per-side visible inset edges (BlockTileRenderer.cs:283-340) = the reported
   "split into individual blocks" look.
2. **The plan (Constructable) for an IsTilePiece def writes its own GameObject into `Grid.ObjectLayers[FoundationTile]`
   for BOTH door cells** when the anchor cell is empty (Constructable.cs MarkArea `:441-461` +
   BuildingDef.MarkArea `:829-942` blind overwrite). In the broken orientation (anchor in air, wall above) this
   **overwrites the wall's layer-9 entry** while the wall's BuildingComplete stays alive and stays in the wall
   RenderInfo's `occupiedCells`.
3. **Cancel does not restore the wall's layer-9 entry**: `Constructable.UnmarkArea` (Constructable.cs:463-477) only
   unmarks the replacement layer; `BuildingDef.UnmarkArea` (BuildingDef.cs:976+) only nulls entries equal to the plan;
   the destroyed plan object remains as the layer-9 value; `TileVisualizer.RefreshCellInternal` sees null (Unity
   destroyed-object semantics) and never re-dirties the chunk. Stale mesh persists — matching "cancelling the plan does
   not fix rendering".
4. **Completion is self-healing**: `FinishConstruction` destroys the upper-cell candidate (Constructable.cs:228-257), and
   the finished door's `BuildingComplete.OnSpawn` re-marks both of its own cells into layer 9 (BuildingComplete.cs:162-170)
   and unregisters the wall's renderer entry on its OnCleanUp (BuildingComplete.cs:298) — so a built door is consistent.
5. The native (non-mod-postfix) replacement path never hits this because its candidate lookup is **anchor-only**
   (BuildTool.cs:352) ⇒ whenever it fires, the anchor's layer-9 entry is the wall (non-null) ⇒ the `IsTilePiece` second
   MarkArea is skipped (Constructable.cs guard). Same for whole-door-in-wall. (For doors this native path only exists
   because of the mod's def-patch, Mod.cs:167-191 — vanilla door defs carry no replacement metadata.) Plain `TryPlace`
   never succeeds over a wall in any orientation (IsAreaClear, BuildingDef.cs:584-614: only occupant rules 8-10 —
   Conduit/LogicBridge/WireBridge — are tolerated; wall rule `Tile`=6 is not, wall has no Uprootable).
6. Working doors (Door, WoodenDoor) have `IsTilePiece=false` ⇒ `Constructable.MarkArea` returns before any TileLayer write;
   LogicPorts presence is not the differentiator (Door has ports & works; PressureDoor has ports & breaks).

### Door diff table
See §3 table. Shared broken delta: `IsFoundation=true` + `TileLayer=FoundationTile` ⇒ `IsTilePiece=true`,
`Entombable=false`. Working: all false.

### Open questions (NOT to be solved in this task)
- Whether the mod should prevent the layer-9 overwrite at plan creation (e.g. skip the `IsTilePiece` TileLayer mark for the
  non-anchor cell, or restore the wall's layer-9 entry on UnmarkArea/cancel) — fix design is for the next stage.
- Whether `Grid.IsTileUnderConstruction[anchor]=true` set at plan spawn (and never cleared for the upper cell) has any
  other consumers that matter here (not traced).
- Whether the plan's layer-11 write into the wall cell (harmless for rendering) interacts with
  `IsReplacementLayerOccupied` checks on subsequent placements (e.g. the "replacement-plan уже есть" log lines suggest the
  mod's own re-entrancy guard saw it).
- Exact save/load behaviour of the overwritten layer-9 entry (Grid save format not traced).

### Log usefulness
`.tmp/game_logs_2.md` confirms: candidate = vanilla TileComplete; plan created at air anchor with candidate in upper cell
for the broken orientation; identical flow for working Door. No rendering-side evidence (game renders client-side), no
cancel events. Useful as state corroboration only.


## 8. Round 2 verification (Ralph round 2)

Re-verified all key citations against source this round (no repo files touched):

- **BuildTool.cs**: `TryBuild` starts :307 (rg-confirmed); :309 guard `if` line (visualizer null / same-cell /
  LogicPorts-LogicGateBase) — read verbatim, matches §4.1; survival `TryPlace` at :321; instant path incl.
  backwall dig via `SimMessages.Dig(offset_cell, -1, skipEvent: true, backwall: true)`; native fallback
  `GetReplacementCandidate(cell)` :352 (anchor-only) — rg-confirmed; `Grid.Objects[cell, (int)def.ReplacementLayer] =
  gameObject` :377 — rg-confirmed; `PostProcessBuild(flag, pos, gameObject)` :387 — rg-confirmed.
- **Constructable.cs**: `MarkArea` :441, `IsTileUnderConstruction[num] = true` :460 (LAST line — §4.3 pseudocode
  corrected), `UnmarkArea` :463, `ClearPendingUproots` :479, `FinishConstruction` :223 — all rg-confirmed;
  `MarkArea`/`UnmarkArea` bodies read verbatim and match §4.3 (with the order fix + `unmarked` flag note).
- **BuildingDef.cs**: `CanReplace` :278-285, `GetReplacementCandidate` :324-345 read verbatim — §4.2 corrected
  (no tag check inside; BuildingComplete check only; caller filters via CanReplace); `TryReplaceTile` :487-527
  read verbatim — no logic code (task item 2 answered); `Instantiate` :529-540; `IsAreaClear` 9-arg starts :547.
- **TileVisualizer.cs** :3-21 read verbatim: `RefreshCellInternal` reads `Grid.Objects[cell, tile_layer]`, only
  calls `World.Instance.blockTileRenderer.Rebuild(tile_layer, cell)` + `KAnimGraphTileVisualizer.Refresh()` when
  non-null — a destroyed plan object in the layer-9 slot ⇒ no Rebuild, no dirtying (confirms §4.5 cancel reasoning).
- **BlockTileRenderer.cs** (Rendering/, 835 lines): `RenderInfo` ctor :76/:81, `Rebuild` :223 (GetConnectionBits at
  :243), `AddVertexInfo` :283, `MatchesDef` :572, `GetConnectionBits` :581, `AddBlock` :749/:754 (queryLayer :759),
  `Rebuild(ObjectLayer, cell)` :775 — all rg-confirmed, match §5.
- **BackWall rendering**: `rendering/BackWall.cs` = material/Texture2DArray holder only; backwalls share the
  BlockTileRenderer per-cell machinery (see new bullet in §5); vanilla backwall def not in decompiled sources.
- **Mod.cs spot-checks**: `FindMethod` :116-149, `Assets_AddBuildingDef` postfix :167-191, `IsDoorDef` :222-236,
  `BuildTool_TryBuild_DoorReplacement__Patch` starts :407 and file ends :601 — all read verbatim, match §1.
- **game_logs_2.md**: pre-existing, 28 lines, re-listed only (not modified).

Corrections made to the round-1 text: (a) `MarkArea` statement order (`IsTileUnderConstruction` last, :460);
(b) `GetReplacementCandidate` has no internal tag check; (c) `UnmarkArea` `unmarked` flag + `ClearPendingUproots`
noted; (d) explicit LogicPorts answer for `TryReplaceTile`; (e) backwall-rendering note in §5.

No open context items remain from the task list: mod patches (§1), Harmony pitfalls (§2), door def comparison (§3),
game flow incl. rendering & cancellation (§4–§5), log inspection (§6), final report (§7). All six task items are
recorded with file:line citations. Remaining open questions are deep-trace/fix-design items (in §7), out of
scope for this context-gathering task.

## 9. Round 3 — regression after Stage-4 hand-off (2026-09-05: user report + prior-agent context)

### 9.1 User observation (the Stage-2 fix did not resolve the bug)
- With the FRESH dll (commit ac60c46, TryBuild capture/restore in place), the wall rendering is broken
  **BOTH while the plan is standing (during construction) AND after cancel** — for tile-piece door defs
  (lower-in-air / upper-in-wall orientation).
- NEW bug (existed pre-fix, now confirmed by user): after CANCELLING the construction, the affected cell no
  longer accepts a door plan — the plan turns **red** and a **red fail-reason text** appears on hover.
- Doors that work PERFECTLY in the same orientation: **Door, WoodenDoor, (regular) Pneumatic** — i.e. exactly
  the defs with `def.TileLayer == ObjectLayer.NumLayers` (non-tile-piece). The Stage-2 capture/restore step is
  a no-op for them (NumLayers skip) → the bug is confined to the tile-piece path, which is exactly the path
  where the Stage-2 capture/restore RUNS. So either the fix does not actually run for those placements, or
  a later write re-clobbers the restored slot, or the rendering fix mechanics assumed in the Stage-1 trace
  are wrong.

### 9.2 Grid storage model (Grid.cs:442-465)
- `Grid.Objects[cell, layer]` is a **view over per-layer dictionaries** `Grid.ObjectLayers[layer]`
  (`Dictionary<int, GameObject>` per layer).
- The setter overwrites an existing key; setting `null` removes the key.
- `ObjectLayers` is NOT serialized; entries are re-established by `OnSpawn` on load.
- Consequence: a dead (destroyed) GO left in a slot is seen as fake-null by `!= null` / `== null` checks,
  but **key-presence checks** (`ContainsKey`-style, as in `GetReplacementCandidate`) still see the stale entry.

### 9.3 Cancel path root cause (traced by prior agent; to be re-verified in the Stage-5 red trace)
- Cancel: `Cancellable.OnCancel(object _)` (`protected virtual`) → `this.DeleteObject()` (**deferred** to end of
  frame) → `OnCleanUp` → `Constructable.UnmarkArea` clears ONLY the replacement-layer (11) slot.
- The layer-9 slot of the upper (wall) cell is left with a DEAD reference to the destroyed plan GO:
  - `GetReplacementCandidate` (BuildingDef.cs:324-345) sees the key present but the value dead → candidate
    gate fails → no candidate found → the cell stops accepting replacement → **red plan + red hover text**;
  - the live wall GO fell out of the grid when the plan's `MarkArea` clobbered it at spawn and is never
    restored on cancel → connection bits read an empty slot → **wall renders as disconnected blocks**.
- Completion (with the Stage-2 fix) remains self-healing: candidate search finds the restored wall at the
  upper cell → destroyed + refunded exactly once (Stage-1 trace (a), Stage-3 audit).

### 9.4 v2 fix design (prior agent; pending red re-trace before implementation)
- Keep the existing TryBuild capture/restore.
- Register the capture in a static per-plan map `Dictionary<GameObject, DoorTileCapture>` (cells + captured
  occupants incl. nulls + `tileLayer` + `replacementLayer` ints).
- New programmatic Harmony postfix on `Cancellable.OnCancel(object)` (5th programmatic patch; no
  `[HarmonyPatch]` attributes — mod invariant): after the original runs the plan GO is still alive (destruction
  is deferred) → for each captured cell, if `Grid.Objects[cell, tileLayer] == plan` (identity, both alive),
  restore `capture.occupants[i]` (null → key removed); then `TileVisualizer.RefreshCell(cell, tileLayer,
  replacementLayer)` per restored non-null cell; remove the map entry.
- Map cleanup: `UserMod2.OnFrameUpdate` sweep of dead GO keys (signature to be verified) or lazy removal.
- Capture area should be the UNION of the spawn-orientation (Neutral) and the plan's actual orientation area
  (dedup via `HashSet<int>`) for rotated placements.

### 9.5 Open questions for the Stage-5 red re-trace
- **Q-A (blocks everything): why is the during-construction rendering STILL broken with the fix in place?**
  Hypotheses to verify line-by-line:
  (a) the plan in the broken orientation is created by the **NATIVE** `TryBuild` path (the mod postfix bails at
      its existing-plan guard) → the Stage-2 capture/restore never runs;
  (b) capture/restore runs, but a LATER write re-clobbers the upper layer-9 slot (e.g. `SetOrientation` after
      spawn re-runs `Markable`, or another `Constructable` lifecycle re-mark);
  (c) the `Grid` setter or `TileVisualizer.RefreshCell` does not do what the Stage-1 trace assumed (side
      effects, dirty-flag semantics, `RenderInfo.occupiedCells` membership);
  (d) the wall's `RenderInfo` lost the upper cell from `occupiedCells` when the slot was clobbered, so
      restoring the slot does not restore rendering membership.
- **Q-B:** exact guard of `Constructable.MarkArea` (what lets the plan overwrite a live wall vs. block it) —
  needed for (a)/(b).
- **Q-C:** confirm `UserMod2.OnFrameUpdate` exists and its exact signature in the decompiled API.
- **Q-D:** re-verify the §9.3 cancel chain and the §9.4 OnCancel-postfix feasibility (method visibility,
  deferred destruction, identity check at OnCancel time, whether OnCancel is the only user-visible cancel path).

### 9.6 User debug-log facts (in-game, 2026-09-05)
- **Normal doors** (Door, WoodenDoor, Pneumatic — the working ones): `def.TileLayer = ObjectLayer.NumLayers`,
  `def.ObjectLayer = NumLayers`.
- **Problem doors** (tile-piece: Insulated Door, Mechanized Airlock, Manual Airlock):
  `def.TileLayer = ObjectLayer.FoundationTile`, `def.ObjectLayer = NumLayers`.
- Consequence: for tile-piece door defs `def.ObjectLayer == NumLayers` while `def.TileLayer == FoundationTile`.
  Every layer-selection branch that falls back to `def.ObjectLayer` (notably `Constructable.UnmarkArea`'s
  `IsReplacementTile ? ReplacementLayer : ObjectLayer` branch) therefore resolves to `NumLayers` (a no-op layer)
  for these defs when `IsReplacementTile` is false — this must be re-verified in the Stage-5 red trace
  (correction candidate for §9.3's "UnmarkArea clears only layer 11").
- Prior traces were moved by the user to
  `.dumbspec/current/fix_door_wall_render/traces/fix_door_wall_render_trace.md`.
