# Research: BestBuildDryWall

## R1: Drywall plan placement API (decompiled facts)

Source root for all paths below: `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/` (Assembly-CSharp-firstpass checked too).

**Version note (critical):** in this decompiled build the classic `PlanController` / `Plan` / `BuildingPlan` / `Building.SetPlan` / `SetBuilt` / `HasPlan` / `GetPlanAt` identifiers do NOT exist (verified via rg in both source trees and raw DLL byte search). The plan system is: `BuildTool` (tool) → `BuildingDef.TryPlace` → `BuildingUnderConstruction` GameObject with a `Constructable` workable → later `BuildingDef.Build` creates the `BuildingComplete` object.

### Drywall identity
- `ExteriorWallConfig.ID = "ExteriorWall"` — `ExteriorWallConfig.cs:8`. 1×1, `BuildLocationRule.NotInTiles`, `ObjectLayer.Backwall`, `DLC?` (standard). Glass variant: `GlassExteriorWallConfig.ID = "GlassExteriorWall"` (DLC5).
- UI name "Drywall": `STRINGS/BUILDINGS.cs:3006` (`STRINGS.BUILDINGS.EXTERIORWALL.FACADES.DEFAULT_EXTERIORWALL.NAME`); build-menu tab "Tiles and Drywall": `STRINGS/UI.cs:12478`.
- Build menu entry: `BuildMenu.cs:142` — `new BuildingInfo("ExteriorWall", Action.BuildMenuKeyD)` (Tiles category).

### Programmatic placement path
- `BuildingDef.TryPlace(GameObject src_go, Vector3 pos, Orientation orientation, IList<Tag> selected_elements, string facadeID, bool restrictToActiveWorld, int layer = 0)` — `BuildingDef.cs:464`:
  - checks `IsValidPlaceLocation(...)`; if OK: `Instantiate(pos, orientation, selected_elements, layer)`;
  - if `facadeID != null && facadeID != "DEFAULT_FACADE"`: `gameObject.GetComponent<BuildingFacade>().ApplyBuildingFacade(Db.GetBuildingFacades().Get(facadeID))` + `KBatchedAnimController.Play("place")`.
- `BuildingDef.Instantiate(Vector3 pos, Orientation orientation, IList<Tag> selected_elements, int layer = 0)` — `BuildingDef.cs:529`: `GameUtil.KInstantiate(BuildingUnderConstruction, pos, Grid.SceneLayer.Front, ...)`; sets `PrimaryElement.ElementID`, `Constructable.SelectedElementsTags`.
- Prefab fields: `BuildingDef.BuildingComplete / BuildingPreview / BuildingUnderConstruction` — `BuildingDef.cs:237-241`.
- Minimal programmatic placement: `Assets.GetBuildingDef("ExteriorWall")` → `def.TryPlace(visualizer, Grid.CellToPosCBC(cell, Grid.SceneLayer.Building), orientation, selectedElements, "DEFAULT_FACADE")`.
- `BuildTool.TryBuild(int cell)` — `BuildTool.cs:307`: computes `pos = Grid.CellToPosCBC(cell, Grid.SceneLayer.Building)`; normal path → `def.TryPlace(visualizer, pos, buildingOrientation, selectedElements, facadeID)`; instant-build path (DebugHandler.InstantBuildMode / sandbox InstantBuild) → `def.Build(cell, orientation, null, selectedElements, Mathf.Min(def.Temperature, b), facadeID, false, GameClock.Instance.GetTime())`.

### Plan lifecycle
- `BuildingUnderConstruction : Building` — `BuildingUnderConstruction.cs:3` (layer "Construction", block-tile renderer `isBlueprint: true`).
- `Constructable : Workable` — `Constructable.cs:12`; `OnCompleteWork` (line 124) → `FinishConstruction` (line 223):
  - `building.Def.Build(cell, orientation, storage, selectedElementsTags, initialTemperature, GetComponent<BuildingFacade>().CurrentFacade, true, GameClock.Instance.GetTime())`;
  - consumes storage, `finished = true`, `this.DeleteObject()`.
- Built object: `BuildingDef.Build(int cell, Orientation orientation, Storage resource_storage, IList<Tag> selected_elements, float temperature, string facadeID, bool playsound = true, float timeBuilt = -1f)` — `BuildingDef.cs:397` → `Create(..., BuildingComplete)` → `MarkArea(cell, orientation, ObjectLayer, gameObject)` → `Grid.Objects[cell, (int)ObjectLayer.Backwall]`.

### Validation failure over an existing BUILT Drywall (why "nothing happens" today)
- Chain: `BuildTool.TryBuild` → `BuildingDef.TryPlace` → `IsValidPlaceLocation` (`BuildingDef.cs:1120`) → `IsAreaClear` (`BuildingDef.cs:547`).
- (a) generic per-offset occupancy check (lines 584-614): built drywall is not Uprootable / not a bridge rule → `fail_reason = UI.TOOLTIPS.HELP_BUILDLOCATION_OCCUPIED`.
- (b) `BuildLocationRule.NotInTiles` case (lines 749-786): checks `Grid.Objects[cell, 9]` (FoundationTile), `Grid.HasDoor[cell]`, and `Grid.Objects[cell, (int)ObjectLayer]` (Backwall): existing built backwall ≠ source_go → fails (`HELP_BUILDLOCATION_NOT_IN_TILES`).
- Red preview: `BuildTool.UpdateVis` (`BuildTool.cs:177-190`) calls `IsValidPlaceLocation` every frame and tints the visualizer.
- Decompilation gap: `BuildingDef.IsAreaValid` / `CheckFoundation` definitions not found in the dump (dropped members) — avoid relying on their exact logic.

### Mouse input plumbing
- `BuildTool : DragTool : InterfaceTool` — `BuildTool.cs:7` (singleton `BuildTool.Instance`); `Activate(BuildingDef, IList<Tag>[, facadeID])` at `BuildTool.cs:116/126`; `GetMode()` = `DragTool.Mode.Brush` (`BuildTool.cs:480`); `canChangeDragAxis = false`.
- `DragTool.OnLeftClickDown(Vector3)` (`DragTool.cs:125`): `dragging = true`, `downPos = cursor_pos`; Brush mode → immediate `AddDragPoint`.
- `DragTool.OnMouseMove(Vector3)` (`DragTool.cs:332`): Brush mode → `AddDragPoints(cursorPos, previousCursorPos)` (`DragTool.cs:428`), steps of `Grid.CellSizeInMeters * 0.25f` → `AddDragPoint` (`DragTool.cs:418`): `cell = Grid.PosToCell(cursorPos)`; if `Grid.IsValidCell && Grid.IsVisible` → `OnDragTool(cell, 0)`.
- `BuildTool.OnDragTool(int cell, int distFromOrigin)` (`BuildTool.cs:302`): `TryBuild(cell)` — builds DURING drag.
- `DragTool.OnLeftClickUp(Vector3)` (`DragTool.cs:214`): ends dragging; in `Mode.Box`/`Mode.Line` iterates the rect (`Grid.PosToXY` → `Grid.XYToCell`) and calls `OnDragTool(cell, manhattanDist)` per cell.
- Coord helpers: `Grid.PosToCell(Vector3)`, `Grid.CellToPosCBC(cell, Grid.SceneLayer)`, `Grid.PosToXY`, `Grid.XYToCell`, `Grid.IsValidCell`, `Grid.IsVisible`, `Grid.IsValidBuildingCell` (`Grid.cs:1338/1366`).
- Tool rotation: `BuildTool.OnKeyDown` consumes `Action.RotateBuilding` → `TryRotate()` (`BuildTool.cs:267`); `SetToolOrientation(Orientation)` (`BuildTool.cs:531`).

### Facade ("Схема") facts seen so far
- Facade applied at placement: `BuildingFacade.ApplyBuildingFacade(Db.GetBuildingFacades().Get(facadeID))`; sentinel `facadeID == "DEFAULT_FACADE"` means no facade applied.
- Facade ids are data-driven: `Db.GetBuildingFacades()` (`Db.cs:132`); default facade string pattern: `STRINGS.BUILDINGS.PREFABS.{prefabID}.FACADES.DEFAULT_{prefabID}.NAME` (`CosmeticsPanel.cs:155`).
- Plan screen activation passes `ProductInfoScreen.FacadeSelectionPanel.SelectedFacade` (`PlanScreen.cs:1799-1821`).

## R4: Repo structure — how to add a new mod (facts)

Repo root: `/home/apkawa/code/ONI_MODS/Apkawa_ONI_Mods`.

### Existing mods
- `BuildDoorOverWall/` — `BuildDoorOverWall.csproj`, `Mod.cs` (1468 lines: `UserMod2` entry + ALL patch classes in one file), `AGENTS.md`, `README.md` (Russian), `bin/`, `obj/`. No ModAssets/, no strings/ i18n folder in any mod of this repo.
- `SizeInTooltip/`, `ReplaceBuildingMaterial/` — byte-for-byte same csproj template (only PackageId/ModName differ).
- All mods: `namespace OxygenNotIncluded.Mods`; entry `public class Mod : UserMod2`, `OnLoad(Harmony)` → `base.OnLoad(harmony)` (runs `PatchAll`) + **programmatic** patch registration via `UtilLibs.PatchUtil.TryPatch` (house style; reason: game 0Harmony v2 can't express `out`/byref in attributes, and private targets are easier via reflection).
- `PatchUtil.TryPatch(Harmony, Type, string name, Type[] paramTypes, string feature, HarmonyMethod prefix=null, postfix=null, transpiler=null)` — resolves via `ReflectionUtil.FindMethod` (byref normalization), logs error instead of throwing when unresolved.
- Patch signature convention: positional `__0/__1/...` + `ref bool __result` (postfix) or `bool` return (prefix).
- Logging: `using PeterHan.PLib.Core;` → `PUtil.LogDebug/LogWarning/LogError` + `.F(...)`; mod name prefixed automatically; verbose logs in `#if DEBUG`.

### BuildDoorOverWall.csproj template (verbatim)
```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <PackageId>BuildDoorOverWall</PackageId>
        <Version>0.0.1</Version>
        <TargetFramework>net48</TargetFramework>
    </PropertyGroup>
    <PropertyGroup>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>default</LangVersion>
        <AssemblyName>$(PackageId)</AssemblyName>
        <RootNamespace>$(PackageId)</RootNamespace>
        <IsMod>true</IsMod>
        <GenerateMetadata>true</GenerateMetadata>
        <IsPacked>true</IsPacked>
    </PropertyGroup>
    <PropertyGroup>
        <ModName>$(PackageId)</ModName>
        <ModDescription />
    </PropertyGroup>
    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'">
        <OutDir>bin</OutDir>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\UtilLibs\UtilLibs.csproj" />
    </ItemGroup>
</Project>
```
Game dll refs come from root `Directory.Build.props` (Assembly-CSharp/-firstpass with `<Publicize>true</Publicize>`, 0Harmony, UnityEngine, etc.).

### ONI-mods.sln (current actual state)
- Contains **four** projects: BuildDoorOverWall `{D298ADB7-CCBD-4EC0-A42C-EA03FBF00B78}`, UtilLibs `{3606AD3B-C8FA-40E7-833E-49F3FAC20061}`, SizeInTooltip `{9E88A8DC-1A59-4339-BA4E-F2246D3BD181}`, ReplaceBuildingMaterial `{D75547EA-D35D-4217-97AE-348CC2E99918}`. (Root AGENTS.md/CONTRIBUTING.md still claim only 2 — the sln is newer and is the source of truth.)
- Per project: `Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Name", "Name\Name.csproj", "{GUID}"` + `EndProject`, plus exactly 4 `ProjectConfigurationPlatforms` lines (`{GUID}.Debug|Any CPU.ActiveCfg/Build.0`, `.Release|Any CPU.ActiveCfg/Build.0`).
- MSBuild sln parser: **no comment support** — add/remove by inserting/removing lines.

### Build plumbing
- `Directory.Build.props.user` (active): `GameLibsFolder=$(HOME)/ONI/dlls`, `ModFolder=$(SolutionRoot).tmp/build_mod_dir`.
- Root props: `TargetGameVersion=731233` (Aquatic), `SupportedContent=ALL`, `APIVersion=2`, Publicizer 0.4.3 (IsMod, assets `build; contentfiles`), ILRepack `dotnet-ilrepack` 2.0.45 (IsPacked).
- `Directory.Build.targets`: generates `mod.yaml`/`mod_info.yaml` into `$(TargetDir)`; `ILRepack` target merges all output dlls into the mod dll in place; `CopyModsToDevFolder` copies dll+pdb+yamls to `$(ModFolder)/<ModName>_<dev|release>/` — in sandbox that copy fails (read-only, MSB3027), expected; artifacts stay in `bin/`.
- Build: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug`.
- `PublicisedAssembly/` — empty legacy dir, gitignored, no longer needed.
- i18n convention (docs/i18n.md): `namespace STRINGS` static `LocString` fields + `Localization.RegisterForTranslation`, `<mod>/strings/*.po`; no mod currently ships .po files.
- Mod identity: `AssemblyName == PackageId == ModName`; one folder = one mod; DLLLoader loads every *.dll, ≤1 UserMod2 per assembly, one Harmony per mod folder.
- Root `README.md` has a mod table (currently lists BuildDoorOverWall + ReplaceBuildingMaterial; SizeInTooltip missing) — add a row for a new mod.

## R3: Ghost/rect rendering + size text (decompiled facts)

### Dig / cancel tools
- `DigTool : FilteredDragTool` (`DigTool.cs`), `CancelTool : FilteredDragTool` (`CancelTool.cs`); both use inherited default `Mode.Box`. `DragTool.Mode` enum: `Brush, Box, Line` (`DragTool.cs:17-22`). `BuildTool` overrides `GetMode()` → `Mode.Brush` (`BuildTool.cs:480-483`).
- `FilteredDragTool` only adds ToolParameterMenu filter toggles, no rendering.
- `DigTool.PlaceDig(int cell, int animationDelay = 0)` (static, `DigTool.cs:45-76`): per-cell temporary ghost = `Util.KInstantiate(Assets.GetPrefab(new Tag("DigPlacer")))`, `Grid.Objects[cell, 7]` (ObjectLayer.DigPlacer), positioned `Grid.CellToPosCBC(cell, Instance.visualizerLayer)` + z -0.15, plays `"ScaleUp"` EasingAnimations. No rectangle-wide multi-cell ghost helper exists in these tools.
- `BrushTool` has its own UI-anchored `areaVisualizer` (RectTransform) — different mechanism.

### DragTool ghost rect + size text (shared by dig/cancel)
- Fields (`DragTool.cs`): `[SerializeField] private Texture2D boxCursor; [SerializeField] private GameObject areaVisualizer; [SerializeField] private GameObject areaVisualizerTextPrefab; [SerializeField] private Color32 areaColour = new Color(1,1,1,0.5); protected SpriteRenderer areaVisualizerSpriteRenderer; protected Guid areaVisualizerText;`
- `OnPrefabInit()` (`DragTool.cs:90-106`): clones serialized `areaVisualizer` KSPrefab (world-space, `SpriteRenderer`), parented to tool, tinted via `Renderer.material.color`.
- `OnLeftClickDown` (`DragTool.cs:125-178`), `Mode.Box`/`Mode.Line` branch: hides `visualizer` (single-cursor preview), shows `areaVisualizer` at cursor with `SpriteRenderer.size = (0.01,0.01)`; creates text: `areaVisualizerText = NameDisplayScreen.Instance.AddAreaText("", areaVisualizerTextPrefab)` + `LocText.color = areaColour`.
- `OnMouseMove` (`DragTool.cs:332-403`) Box/Line: regularizes corners (`GetWorldRestrictedPosition` + `GetRegularizedPos`), centers sprite at rect midpoint, `areaVisualizerSpriteRenderer.size = (w,h)` meters; text update (`DragTool.cs:392-398`):
  - `component.text = string.Format(UI.TOOLS.TOOL_AREA_FMT, w, h, w*h)` — format `"{0} x {1}\n{2} tiles"` (`STRINGS/UI.cs:13708`);
  - positioned at rect center (world space) via `TransformExtensions.SetPosition`.
  - Brush mode instead: `TOOL_LENGTH_FMT` (`"{0}"`) with `GetDragLength()`, positioned 1 m above cursor cell.
- `OnLeftClickUp` (`DragTool.cs:214-273`): `areaVisualizer.SetActive(false)`, `RemoveCurrentAreaText()`, then iterates rect: `Grid.PosToXY(downPos)` / `Grid.PosToXY(cursor_pos)` → nested x/y loop → `Grid.XYToCell(j, i)` → if `Grid.IsValidCell && Grid.IsVisible` → `OnDragTool(cell, dist)`.
- `CancelDragging()` (`DragTool.cs:189-212`) and `OnCmpDisable()` also hide ghost + text.
- `GetRegularizedPos(Vector2 input, bool minimize)` (`InterfaceTool.cs:252-257`): `Grid.CellToPosCCC(Grid.PosToCell(input), Grid.SceneLayer.Background) ± (HalfCellSizeInMeters, HalfCellSizeInMeters, 0)`.
- Drag sound: `"Tile_Drag"` per size change with FMOD param `tileCount`.

### BuildTool visualizer (single ghost)
- `InterfaceTool.visualizer` (`GameObject`, `InterfaceTool.cs:26`), `visualizerLayer = Grid.SceneLayer.Move` (line 28).
- Created in `BuildTool.OnActivateTool()` (`BuildTool.cs:51-94`): `visualizer = GameUtil.KInstantiate(def.BuildingPreview, vector, Grid.SceneLayer.Ore, null, LayerMask.NameToLayer("Place"))`; `KBatchedAnimController` set to `visibilityType = Always`, `isMovable = true`, `Offset = def.GetVisualizerOffset()`, named `..._visualizer`; facade applied via `BuildingFacade.ApplyBuildingFacade` when `facadeID != "DEFAULT_FACADE"`; `Rotatable.SetOrientation(buildingOrientation)`; layer "Place". Destroyed in `OnDeactivateTool` (`BuildTool.cs:107`).
- `GameUtil.KInstantiate(GameObject, Vector3 position, Grid.SceneLayer sceneLayer, string name = null, int gameLayer = 0)` — `GameUtil.cs:2655`.
- Tint: `BuildTool.SetColor(GameObject root, Color c, float strength)` → `KBatchedAnimController.TintColour` (`KAnimControllerBase.cs:188`); white when valid, red when both `IsValidPlaceLocation`/`IsValidReplaceLocation` fail (`BuildTool.cs:180-190`).
- Reposition: `InterfaceTool.OnMouseMove` → `visualizer.transform.SetLocalPosition(Grid.CellToPosCBC(cell, visualizerLayer) + (0,0,-0.15))`; `BuildTool.UpdateVis` → `SetPosition(Grid.CellToPosCBC(num, def.SceneLayer))`.

### Grid conversions (signatures, Grid.cs)
- `PosToCell(Vector2/Vector3)` (1424/1432), `PosToXY(Vector3, out int x, out int y)` (1440), `XYToCell(int x, int y)` (1457), `CellToPosCBC(cell, SceneLayer)` (1581, z = GetLayerZ), `CellToPosCCC(cell, SceneLayer)` (1576), `CellToPos(cell)` (1481); `CellSizeInMeters`/`HalfCellSizeInMeters` (622/626). No `CellToUV` in this build.

### Mod-design facts distilled (for the spec/plan)
- Forgetting nothing: `DragTool` mode can be forced per-call via prefixing `GetMode()`; Box mode then natively provides rect ghost + `"{w} x {h}\n{w*h} tiles"` text + per-cell `OnDragTool(cell, dist)` iteration on mouse-up.
- Per-cell drywall ghost = clone of `BuildingDef.BuildingPreview` configured as in `BuildTool.OnActivateTool`.
- Repaint = `BuildingFacade.ApplyBuildingFacade/ApplyDefaultFacade` on the existing `BuildingComplete` (instant, persisted).

### `BuildingFacade` component — `BuildingFacade.cs`
- `public class BuildingFacade : KMonoBehaviour`, `[SerializationConfig(MemberSerialization.OptIn)]`.
- State: `[Serialize] private string currentFacade;` (persisted to save; re-applied in `OnSpawn` via `ApplyBuildingFacade(Db.GetBuildingFacades().TryGet(currentFacade))` when `!IsOriginal`).
- `public string CurrentFacade => currentFacade;` · `public bool IsOriginal => currentFacade.IsNullOrWhiteSpace();`
- Change methods (runtime, instant):
  - `public void ApplyDefaultFacade(bool shouldTryAnimate = false)`
  - `public void ApplyBuildingFacade(BuildingFacadeResource facade, bool shouldTryAnimate = false)` — sets `currentFacade = facade.Id`, then private `ChangeBuilding(animFiles, name, desc, interactAnims, shouldTryAnimate, data)`:
    - instant `KBatchedAnimController.SwapAnims` on all `Building` controllers (incl. children with same `batchGroupID`) — **no rebuild, no workable**;
    - optional `BuildingFacadeAnimateIn` flash + UI click sound when `shouldTryAnimate`;
    - `BuildingFacadeCustomData.ApplyCustomData` (light color), `KSelectable.SetName` rename, `DetailsScreen.RefreshTitle()`, partitioner re-render for `AnimTileable`.
  - `public string GetNextFacade()` — next id in `GetComponent<Building>().Def.AvailableFacades`, wraps; NO in-game callers (unused in decompiled code).

### Facade data
- `Db.GetBuildingFacades()` → `Database.BuildingFacades : ResourceSet<BuildingFacadeResource>` (`Db.cs:132`, `Database/BuildingFacades.cs`); `TryGet(id)`/`Get(id)`/`Exists(id)`.
- `Database.BuildingFacadeResource : PermitResource` (`Database/BuildingFacadeResource.cs`): fields `Id`, `Name` (LocString), `Description`, `Rarity`, `public string PrefabID; public string AnimFile; public Dictionary<string,string> InteractFile; public Dictionary<string,string> Data;`.
- Linkage facade→building at DB init: `BuildingFacadeResource.Init()` → `Assets.TryGetPrefab(PrefabID).AddOrGet<BuildingFacade>()` → `def.AddFacade(Id)`.
- Per-building list: `BuildingDef.AvailableFacades` (`List<string>`, `BuildingDef.cs:260`), `def.AddFacade(string db_facade_id)` (`BuildingDef.cs:1914`).
- UI availability filter: `PermitResource.IsUnlocked()` (`Database/PermitResource.cs:38`).

### In-game UI to switch scheme of a BUILT building
- Panel: `CosmeticsPanel` (TargetPanel), DetailsScreen side tab `SidescreenTabTypes.Blueprints` (tab title key `STRINGS.UI.UISIDESCREENS.TABS.SKIN`, "Blueprint"; Russian "Схема" is in language assets). Wiring: `DetailsScreen.cs:800-817`.
- `CosmeticsPanel.OnSelectTarget(GameObject target)` (`CosmeticsPanel.cs:63`):
  - `selectionPanel.SetBuildingDef(def.PrefabID, facade.CurrentFacade)`;
  - hooks `selectionPanel.OnFacadeSelectionChanged` → if `SelectedFacade` null/`"DEFAULT_FACADE"`/destroyed → `buildingFacade.ApplyDefaultFacade(true)`, else `buildingFacade.ApplyBuildingFacade(Db.GetBuildingFacades().Get(SelectedFacade), true)`; then `Refresh()`.
- `FacadeSelectionPanel` (`FacadeSelectionPanel.cs`): `DEFAULT_FACADE_ID = "DEFAULT_FACADE"`; `SetBuildingDef(string defID, string currentFacadeID = null)`; `RefreshTogglesForBuilding()` builds toggles from `buildingDef.AvailableFacades` filtered by `PermitResource.IsUnlocked()`; toggle click → `SelectFacade(id)` → `SelectedFacade` setter fires `OnFacadeSelectionChanged`.
- No confirmation dialog; side effects only: animate-in flash, click sound, title refresh, partitioner event.
- Same `FacadeSelectionPanel` is reused in the build menu (`ProductInfoScreen.cs:66,102`) and `SelectModuleSideScreen.cs:208-215,513-515`.

### Instant? Plans too?
- Instant, synchronous; no rebuild. State serialized (`currentFacade`), re-applied on save load.
- Works on plan objects: `BuildingLoader.CreateBuildingUnderConstruction` adds `BuildingFacade` (`BuildingLoader.cs:175`), same for `CreateBuildingComplete` (line 228) and `CreateBuildingPreview` (line 308). Plan's `CurrentFacade` is carried into the built building by `Constructable.FinishConstruction` (`Constructable.cs:259`) → `BuildingDef.Build(..., facadeID, ...)` → `ApplyBuildingFacade` (`BuildingDef.cs:397-405`).

### What happens today when placing a (different-facade) plan over a BUILT drywall cell
- `ExteriorWall` specifics: `BuildLocationRule.NotInTiles`, `ObjectLayer.Backwall`, `SceneLayer.Backwall`, `ReplacementLayer = ObjectLayer.ReplacementBackwall`, `ReplacementCandidateLayers = { FoundationTile, Backwall }`, `ReplacementTags = { FloorTiles, Backwall }` (`ExteriorWallConfig.cs`).
- `BuildTool.TryBuild` flow (`BuildTool.cs:307`):
  1. `PlanScreen.Instance.LastSelectedBuildingFacade = facadeID;` (line 318)
  2. Normal: `def.TryPlace(...)` → fails over built backwall (`HELP_BUILDLOCATION_OCCUPIED`) → null.
  3. Fallback replacement path (`BuildTool.cs:350-386`):
     - `def.GetReplacementCandidate(cell)` (finds built backwall/tile in `ReplacementCandidateLayers`);
     - requires `BuildingComplete != null && component.Def.Replaceable && def.CanReplace(replacementCandidate)`;
     - **`if (component.Def != def || selectedElements[0] != tag) { def.TryReplaceTile(...); Grid.Objects[cell, (int)def.ReplacementLayer] = gameObject; }`**
     - i.e. SAME def + SAME primary element → **the condition is false → NOTHING happens** (this is exactly the user's "сейчас вообще ничего не происходит" case), and the existing building's facade is never consulted.
     - Different def/element → `TryReplaceTile` (`BuildingDef.cs:508-527`, `Constructable.IsReplacementTile = true`) with the tool's `facadeID` applied.
  4. Instant-build path (`BuildTool.cs:325-349`, InstantBuild/Sandbox only): digs the backwall (`SimMessages.Dig(offset_cell, -1, skipEvent: true, backwall: true)`) then `def.Build(..., facadeID, ...)` — old facade never carried over.
- No special facade-merge/preserve logic anywhere in the build flow.

### Mod hook candidates (facts, not design)
- Feature 2 hook point: `BuildTool.TryBuild(int cell)` — private method, reachable via publicizer; the "nothing happens" branch is `component.Def != def || selectedElements[0] != tag` being false at `BuildTool.cs:350-386`.
- Existing facade of the target cell: `Grid.Objects[cell, (int)ObjectLayer.Backwall]` → `GetComponent<BuildingFacade>().CurrentFacade`; compare with the tool's `facadeID` field of `BuildTool` (tool-side facade).
- `BuildingTool` fields: `BuildTool.TryBuild` uses member `facadeID` (set in `Activate(BuildingDef, IList<Tag>, string facadeID)`), `buildingOrientation`, `selectedElements`, `visualizer`.

## R5: DragTool/BuildTool mouse plumbing (verified, build 731233)

Source: decompiled tree `.tmp/game_decomp/` (identical to `lib_sources/Assembly-CSharp/`; `~/ONI/dlls/Assembly-CSharp/` does not exist — DLLs only).

### Virtual dispatch (critical for patch placement)
- `BuildTool` OVERRIDES all three mouse handlers: `OnMouseMove` (BuildTool.cs:168-173: `base.OnMouseMove(cursorPos); ... UpdateVis(cursorPos);`), `OnLeftClickDown` (:521-524: pure `base.` forward), `OnLeftClickUp` (:526-529: pure `base.` forward). Therefore postfix/prefix on the `DragTool` versions DO fire for a BuildTool instance (via the `base.` calls).
- `BuildTool.GetMode()` (BuildTool.cs:480-483) is a `protected override` returning `Mode.Brush`. DragTool's handlers call `GetMode()` virtually, so a prefix on `DragTool.GetMode` would NOT fire — patch **`BuildTool.GetMode`** instead: `private static bool Prefix(BuildTool __instance, ref DragTool.Mode __result)`; return `Mode.Box` when `def.PrefabID == "ExteriorWall" && Input.GetKey(LeftShift/RightShift)`.
- `Mode` enum (DragTool.cs:17-22, public, nested in DragTool): `Brush = 0, Box = 1, Line = 2`. `DragTool.mode` field is `private Mode mode = Mode.Box;` (DragTool.cs:48) — set by `SetMode`; BuildTool never calls SetMode.
- `GetMode()` call sites: DragTool.cs:156 (OnLeftClickDown), :206 (CancelDragging), :234 (OnLeftClickUp), :335/:352 (OnMouseMove), :530 (OnFocus). BuildTool: zero own call sites.

### Box-mode flow (what the mod gets for free by forcing Mode.Box)
- `OnLeftClickDown` Box branch (DragTool.cs:164-177): `visualizer.SetActive(false)`; `areaVisualizer` (scene-serialized `[SerializeField] private GameObject` field, cloned once in `OnPrefabInit` :98-105, never Destroy'd, SetActive-toggled only) shown as 0.01×0.01 at cursor. Mode-agnostic part BEFORE the switch (:151-155) always creates the area text: `areaVisualizerText = NameDisplayScreen.Instance.AddAreaText("", areaVisualizerTextPrefab)` (`AddAreaText(string, GameObject)` → **Guid**, NameDisplayScreen.cs:184-192).
- `OnMouseMove` Box branch (DragTool.cs:366-399): resizes/repositions `areaVisualizer` between regularized corners (`GetRegularizedPos(Vector2, bool)` — InterfaceTool.cs:252-256, protected; far edge = `minimize: false`, near edge = `minimize: true`), drag sound with `tileCount`, and the size text: `string.Format(UI.TOOLS.TOOL_AREA_FMT, w, h, w*h)` where w/h = `Mathf.RoundToInt` of the corner delta. NO construction during drag (no AddDragPoints/OnDragTool in Box branch).
- `OnLeftClickUp` Box path (DragTool.cs:239-272): proceeds only when `mode == Box || Line` AND `areaVisualizer != null`; hides areaVisualizer; rect from RAW `downPos`→`cursor_pos`: `Grid.PosToXY` then `(int)` cast (truncation, matches `Grid.PosToCell` convention), `Util.Swap` for min/max, nested `for i=y..y2, j=x..x2` → `cell = Grid.XYToCell(j, i)`, guard `Grid.IsValidCell(cell) && Grid.IsVisible(cell)`, `OnDragTool(cell, manhattanDist)` (virtual → `BuildTool.OnDragTool` → `TryBuild(cell)`); then `GetConfirmSound` + `OnDragComplete` (BuildTool does not override; empty).
- `BuildTool.UpdateVis` (BuildTool.cs:175-238): never `SetActive`s the visualizer; tints it (red if invalid), re-positions it every mouse move even while hidden; tile-piece bookkeeping only (`ExteriorWall` is not a tile piece). `lastCell` field updated there (tile-preview cache); `lastDragCell`/`lastDragOrientation` are the `TryBuild` dedupe pair (set by `TryBuild`/`TryRotate`), reset to -1 in `OnActivateTool`/`OnDeactivateTool`.
- `CancelDragging()` (public, DragTool.cs:189-212) and `OnCmpDisable()` (protected, :113-123): hide areaVisualizer/visualizer, `dragging = false`, remove area text — neither touches ghost clones (mod must clean up; postfix both).
- `OnFocus` (DragTool.cs:528-548, public override): Box branch re-activates `visualizer` on focus only when `!dragging`.

### Fields (declarations)
- `dragging` — `private bool dragging;` DragTool.cs:44.
- `downPos` — `protected Vector3 downPos;` DragTool.cs:56. **No `cursorPos` field exists** anywhere (method parameter only).
- `previousCursorPos` — `private Vector3` DragTool.cs:46.
- `areaVisualizer` — `[SerializeField] private GameObject` DragTool.cs:28; `areaVisualizerSpriteRenderer` — `protected SpriteRenderer` :36; `areaVisualizerText` — `protected Guid` :38.
- `visualizer` — `public GameObject` InterfaceTool.cs:26; `visualizerLayer` — `public Grid.SceneLayer = Grid.SceneLayer.Move` :28.
- Base declarations: `public virtual void OnLeftClickDown/OnLeftClickUp(Vector3)` (InterfaceTool.cs:227/231, empty), `public virtual void OnMouseMove(Vector3)` (:209-217: repositions `visualizer` if non-null and app-focused).

### Ghost-clone recipe (mirrors BuildTool.OnActivateTool, BuildTool.cs:51-94)
- `GameUtil.KInstantiate(GameObject original, Vector3 position, Grid.SceneLayer sceneLayer, string name = null, int gameLayer = 0)` (GameUtil.cs:2655). Call: `(def.BuildingPreview, Grid.CellToPosCBC(cell, def.SceneLayer), Grid.SceneLayer.Ore, null, LayerMask.NameToLayer("Place"))`.
- `KBatchedAnimController`: `visibilityType = KAnimControllerBase.VisibilityType.Always; isMovable = true; Offset = def.GetVisualizerOffset(); SetLayer(LayerMask.NameToLayer("Place"));` (else `go.SetLayerRecursively(...)`).
- Facade on ghost: `go.GetComponent<BuildingFacade>().ApplyBuildingFacade(Db.GetBuildingFacades().TryGet(facadeID), false)` when facadeID non-empty and != `"DEFAULT_FACADE"` (`BuildingPreview` gets a `BuildingFacade` in `BuildingLoader.CreateBuildingPreview`, BuildingLoader.cs:308).
- Do NOT call `GridCompositor.Instance.ToggleMajor` per ghost (global state; not needed — `Always` visibility suffices).

### Shift
- `Input.GetKey(KeyCode.LeftShift)` / `KeyCode.RightShift` (UnityEngine.Input) — game precedent DevPanel.cs:53; compiles in patched/derived context (DragTool already uses `Input.GetKey` at :235/:335).

### Consequences for the design
- Placing plans on mouse-up needs NO extra `OnDragTool` work: the vanilla Box iteration already calls `TryBuild(cell)` per valid cell, and the existing Feature-2 prefix intercepts the repaint case / falls through to `TryPlace` otherwise.
- `TryBuild` dedupe (`cell == lastDragCell && buildingOrientation == lastDragOrientation`) can swallow the first cell of a 1×1 rect (cursor cell == lastDragCell) — reset `lastDragCell = -1` (private, publicized) in the `OnLeftClickUp` prefix when a session is active.
- Ghost rect = cells whose `(int)Grid.PosToXY` floors span [downPos, cursorPos] (same truncation as the mouse-up loop) filtered by `IsValidCell && IsVisible` — guarantees ghost set == placed set.

## R6: Acceptance iteration 1 — root causes (diagnosed from `.tmp/Player_best-build-drywall_1.log` + sources)

### Bug 1 (Shift ↔ straight-line conflict): confirmed
- `Action.DragStraight` (the game's "drag in a straight line" hold-key) is bound to **Shift** in the default bindings; the game checks it via `Input.GetKey((KeyCode)Global.GetInputManager().GetDefaultController().GetInputForAction(Action.DragStraight))` at `DragTool.OnMouseMove:335-338` (while dragging) and `DragTool.OnLeftClickUp:235-238` (at release) → `cursor_pos = SnapToLine(cursor_pos)` collapses the rect to a line.
- `DragTool.SnapToLine(Vector3)` — the single choke point for both call sites. Fix: prefix on it that skips the original (`return false`) while a session is active (and sets `ref __result` to the input unchanged, since a skipped value-returning method leaves `__result` at default otherwise).

### Bug 2 (only cells under the cursor get placed): the GetMode prefix NEVER fired — classic Harmony mistake
- The shipped prefix (`Mod.cs:342-352`) set `__result = DragTool.Mode.Box` and then **`return true`** — in Harmony a `bool`-returning prefix returning `true` lets the ORIGINAL method run, and its return value overwrites `__result`. So `GetMode()` always returned `Mode.Brush`.
- Log proof: during every "drag" there are 98–169 continuous `TryBuild` calls at ~16 ms intervals (the BuildDoorOverWall TryBuild postfix counting them) = Brush-mode `AddDragPoints` during the drag, NOT the Box mouse-up per-cell loop; the Feature-2 prefix log line for the down cell precedes `box session begin` in the same millisecond (Box mode never builds on down — Brush mode does: `AddDragPoint → OnDragTool → prefix → Begin`).
- Consequences: plans appeared only along the brush path ("under the mouse"); `DragTool.OnLeftClickUp:239-242` early-returned (mode != Box) so the per-cell loop never ran; Feature-2 repaint also only under the cursor (same brush path). Ghosts still rendered because the session postfixes don't depend on the mode.
- Fix: `BuildTool_GetMode__Patch.Prefix` must `return false` (skip original) when forcing Box.

### Bug 3 (ghosts only top-right → bottom-left): blind swap in `ShiftRectSession.Update`
- `Update` used `Util.Swap` UNCONDITIONALLY (a blind swap). Net effect: exactly the diagonal where downPos is top-right (both coords decrease) ends up with x0<=x1 && y0<=y1; all other 3 directions end with an inverted loop bound → zero ghosts. Log proof: drags in the other directions log `ghosts=0` while the working direction logs 52/64.
- Fix: conditional swaps exactly like the game (`if (x1 < x0) Util.Swap(ref x0, ref x1); if (y1 < y0) Util.Swap(ref y1, ref y0);`, DragTool.cs:250-251 pattern).

### Bug 4 (no red validity tint): not implemented
- `BuildTool.UpdateVis` tints the visualizer red via `BuildTool.SetColor(GameObject, Color, float)` (BuildTool.cs:~485-492) on `KBatchedAnimController` (`TintColour` + strength).
- Reference: BlueprintsV2 (`example_mods/Sgt_Imalas-Oni-Mods/BlueprintsV2/`): per-cell validity in `Tools/CreateBlueprintTool.cs`, tint in `Visualizers/BuildingVisual.cs` — `def.IsValidPlaceLocation(...)`-style check, red for invalid.
- Design: per ghost `white` iff (a) `def.IsValidPlaceLocation(tool.visualizer, Grid.CellToPosCBC(cell, Grid.SceneLayer.Building), tool.buildingOrientation, out _)` OR (b) the cell holds a built object `def` can replace (game's replace path) OR (c) the Feature-2 repaint case; `red` otherwise. Tint at ghost creation (world is static during the drag). Verify exact `IsValidPlaceLocation` signature/side-effect-freeness and the replace-check API in `BuildingDef` before coding.

### User's temp edit to revert
- `ShiftRectSession.ShiftHeld()` currently `return true;` unconditionally (Mod.cs:198-201) — user's workaround. Revert to the real check; use the game's own action lookup (the DragStraight binding — Shift by default) so it stays consistent with the game's config, and add the SnapToLine prefix to kill the conflict.

## R7: Acceptance iteration 2 — crash diagnosis (`.tmp/Player_best-build-drywall_2_crash.log`)

### Crash evidence
- First NRE at log line 1059-1063, immediately after `box session begin: start cell=109040`: `NullReferenceException` in `(wrapper dynamic-method) ... DragTool.OnMouseMove_Patch1(DragTool, Vector3)` ← `BuildTool.OnMouseMove` ← `PlayerController.Update` — repeats once per frame (13 times) until mouse-up; then `box session end (mouse up): ghosts=1, invalid=0` and `Game.OnApplicationQuit`.
- `ghosts=1` = only the `Begin` ghost ever existed → our `OnMouseMove` postfix never ran (the original threw before it).

### Root cause (confirmed)
- The **wall-tool prefab has no `areaVisualizer`**: it is a serialized inspector asset (DragTool.cs:27-28) instantiated only if assigned (DragTool.cs:98-105); vanilla `BuildTool.GetMode` is always `Mode.Brush` (BuildTool.cs:480-483), so the game never gave the wall tool a box visualizer. Forcing Box made the game's **own** unguarded Box code path reachable: `DragTool.cs:377` `areaVisualizer.transform.SetPosition(...)` (+ `areaVisualizerSpriteRenderer.size` at 379/391) → NRE every drag frame. The 3-frame stack (inlined original body inside the Harmony dynamic method) proves it's the original code, not ours.
- `ShiftHeld()` input chain ruled out (the identical chain is the game's own per-frame drag code, ran clean all session; the session-begin log proves it succeeded at mouse-down). `IsCellActionable`/ghost creation ruled out (separate methods → would be separate frames; `Begin` demonstrably created its ghost). `SnapToLine` patch ruled out (log shows it attached; prefix demonstrably ran every frame).
- Same null also silently kills placement: `DragTool.OnLeftClickUp:239` `if ((mode != Box && mode != Line) || areaVisualizer == null) return;` — so even without the crash, nothing would ever be placed.

### Fix design (minimal)
1. **Lazily borrow the game's own box visualizer**: `ShiftRectSession.EnsureBoxVisualizer(BuildTool tool)` — if `tool.areaVisualizer == null`, find any `DragTool` in `PlayerController.Instance.tools` that HAS one (e.g. the cancel tool — native Box), cache its prefab statically, `GameUtil.KInstantiate(prefab, Vector3.zero, Grid.SceneLayer.Ore, tool.gameObject)` (4-arg overload, GameUtil.cs:2660), `SetActive(false)`, copy layer, `tool.areaVisualizerSpriteRenderer = clone.GetComponent<SpriteRenderer>()`, `clone.GetComponent<Renderer>().material.color = tool.areaColour`. Return false + `PUtil.LogWarning` if no donor (graceful fallback to Brush — no crash).
2. **Gate the GetMode prefix**: force Box only if `EnsureBoxVisualizer` succeeded. The first `GetMode()` call happens inside the original `OnLeftClickDown`'s own `switch (GetMode())` (DragTool.cs:156) → the vanilla null-GUARDED Box activation (DragTool.cs:170-175) then runs for free.
3. **Session must stay active through the original `OnLeftClickUp`** (placement correctness): with the session ended in the mouse-UP PREFIX (current code), the original's own `SnapToLine` (DragTool.cs:235-238, Shift still held at release) is no longer suppressed → rect collapses to a 1×N line; and if the user released Shift before mouse-up, `GetMode()` → Brush → early-return → nothing placed. Fix: move `ShiftRectSession.End("mouse up")` to a POSTFIX on `OnLeftClickUp` (keep the `lastDragCell = -1` reset in the prefix so `TryBuild` dedupe is avoided). Session active during the original ⇒ mode stays Box (Shift held) + snapping suppressed ⇒ full 2-D rect placed.

### Consequence
- Feature 1 was never functional end-to-end before: iteration 0 = Brush mode (Box never engaged); iteration 1 = Box engaged but null visualizer → crash + no placement. This is the iteration that makes it actually work.
