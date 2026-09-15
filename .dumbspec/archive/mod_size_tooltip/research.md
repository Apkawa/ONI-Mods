# Research — mod_size_tooltip

## Q1 — Tooltip construction

All drag-tool mouse tooltips are built by **`HoverTextConfiguration`** (and its per-tool subclasses). RU-localized strings live in the game's `STRINGS` LocString table (decompiled in `Assembly-CSharp/STRINGS/UI.cs`), NOT in C# source — the C# code references them by key (`Strings.Get(...)` / `UI.TOOLS.*`).

### Base class: `HoverTextConfiguration` (Assembly-CSharp/HoverTextConfiguration.cs)
- `ToolNameStringKey`, `ActionStringKey` (string keys into LocString table); `ToolName`, `ActionName`, `backStr` (resolved runtime strings).
- `ConfigureHoverScreen()` — resolves `ActionName = Strings.Get(ActionStringKey)`, `backStr = UI.TOOLS.GENERIC.BACK.ToString().ToUpper()` (RU «НАЗАД»), calls `ConfigureTitle`.
- `ConfigureTitle(HoverTextScreen)` (virtual) — `ToolName = Strings.Get(ToolNameStringKey).String.ToUpper()` (RU «КОМАНДА "КОПАТЬ"»).
- `DrawTitle(...)` — draws `ToolName` (the «КОМАНДА "КОПАТЬ"» header).
- `DrawInstructions(...)` — draws the mouse-button hint line: `icon_mouse_left` + `ActionName` («ПЕРЕТАЩИТЬ»), `icon_mouse_right` + `backStr` («НАЗАД»).
- `UpdateHoverElements(List<KSelectable>)` — **virtual**, the per-frame entry point that assembles the whole tooltip (BeginShadowBar → DrawTitle → DrawInstructions → tool-specific → EndShadowBar → EndDrawing).

### Per-frame driver: `InterfaceTool` (Assembly-CSharp/InterfaceTool.cs)
- `OnSpawn()`: `hoverTextConfiguration = GetComponent<HoverTextConfiguration>()` (line 127).
- `LateUpdate()` (line ~295): each frame → `GetSelectablesUnderCursor(hits)` → **`UpdateHoverElements(hits)`** (line ~306, protected wrapper at line 287 that calls `hoverTextConfiguration.UpdateHoverElements(hits)`).
- `ShowHoverUI()` (virtual) — DragTool overrides it to force-true while dragging.

### Shared vs per-tool: **ONE shared base path** (`HoverTextConfiguration.UpdateHoverElements` / `DrawTitle` / `DrawInstructions`) with **per-tool `UpdateHoverElements` overrides** that call the same `DrawTitle`+`DrawInstructions` then add tool-specific rows. Relevant per-tool cards (all Assembly-CSharp/):
- `DigToolHoverTextCard.cs` (Dig)
- `CancelToolHoverTextCard.cs` (Cancel — overrides `ConfigureTitle` for filter)
- `DeconstructToolHoverTextCard.cs` (Deconstruct/Demolish — overrides `ConfigureTitle` for filter)
- `BuildToolHoverTextCard.cs`, `MopToolHoverTextCard.cs`, `HarvestToolHoverTextCard.cs`, `PrioritizeToolHoverTextCard.cs`, `EmptyPipeToolHoverTextCard.cs`, `PlaceToolHoverTextCard.cs`, `PrebuildToolHoverTextCard.cs`, `MoveToLocationToolHoverTextCard.cs`, `AttackToolHoverTextCard.cs`, `SandboxStoryTraitToolHoverTextCard.cs`, `SelectToolHoverTextCard.cs` (default select, NOT a drag tool), `ClusterMapSelectToolHoverTextCard.cs`.

### String keys (STRINGS/UI.cs, `UI.TOOLS` namespace)
- `UI.TOOLS.GENERIC.BACK = "Back"` (RU «НАЗАД»).
- Per-tool `NAME`/`TOOLNAME`/`TOOLACTION`/`TOOLACTION_DRAG` (e.g. `UI.TOOLS.DIG.NAME="Dig"` RU «КОМАНДА "КОПАТЬ"», `TOOLACTION="DRAG"` RU «ПЕРЕТАЩИТЬ»). These keys are what `ToolNameStringKey`/`ActionStringKey` point at (resolved via `Strings.Get`).
- `UI.TOOLS.CAPITALS`, `UI.TOOLS.FILTER_HOVERCARD_HEADER` used by Cancel/Deconstruct title override.
- `UI.TOOLS.TOOL_AREA_FMT = "{0} x {1}\n{2} tiles"` (line 13708) — the **in-rectangle** size label format (see Q2).

### Candidate hook points for injecting «РАЗМЕР:»
1. `HoverTextConfiguration.DrawInstructions` (shared; runs for every tool) — inject before/after the mouse-button line. But it has no access to drag rectangle state (that lives in `DragTool`).
2. `HoverTextConfiguration.UpdateHoverElements` (shared virtual) — inject; again no direct drag state.
3. `InterfaceTool.LateUpdate` / `InterfaceTool.UpdateHoverElements` (shared driver) — has access to the tool instance (can downcast to `DragTool` to read drag state).
4. Each drag tool's `UpdateHoverElements` override (Dig/Cancel/Deconstruct/Build/Mop/...) — per-tool, but each can reach its own `DragTool` instance.
5. `DragTool.OnMouseMove` (where the in-rectangle size text is computed) — best place to CAPTURE the W/H/cell state into a static, so any tooltip hook can read it.


## Q2 — Selection-size data source

The in-rectangle size label is computed and drawn in **`DragTool.OnMouseMove`** (Assembly-CSharp/DragTool.cs, lines ~332–403), Box/Line mode branch:

```csharp
Vector2 input = Vector3.Max(downPos, cursorPos);   // top-right (world meters)
Vector2 input2 = Vector3.Min(downPos, cursorPos);  // bottom-left
input = GetWorldRestrictedPosition(input);        // clamp to active world bounds
input2 = GetWorldRestrictedPosition(input2);
input = GetRegularizedPos(input, minimize: false); // snap to cell corner
input2 = GetRegularizedPos(input2, minimize: true);
Vector2 vector = input - input2;                    // size in METERS
Vector2 vector2 = (input + input2) * 0.5f;          // center
areaVisualizer.transform.SetPosition(...);          // the drawn rectangle (SpriteRenderer)
areaVisualizerSpriteRenderer.size = vector;
Vector2I vector2I = new Vector2I(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.y)); // W, H in CELLS
LocText component = NameDisplayScreen.Instance.GetWorldText(areaVisualizerText).GetComponent<LocText>();
component.text = string.Format(UI.TOOLS.TOOL_AREA_FMT, vector2I.x, vector2I.y, vector2I.x * vector2I.y);
```

### State holding the drag rectangle (`DragTool`, Assembly-CSharp/DragTool.cs)
- `downPos` (Vector3, private) — world pos of left-click-down.
- `dragging` (bool, private; public `Dragging => dragging`).
- `mode` (Mode: Brush/Box/Line; default Box; `GetMode()` virtual).
- `areaVisualizer` (GameObject, `[SerializeField]`) + `areaVisualizerSpriteRenderer` (SpriteRenderer; `size` = rect size in meters).
- `areaVisualizerText` (Guid, protected) — world-space text id; created in `OnLeftClickDown` via `NameDisplayScreen.Instance.AddAreaText("", areaVisualizerTextPrefab)`; removed in `OnLeftClickUp`/`CancelDragging`/`OnDeactivateTool` via `RemoveCurrentAreaText()`.
- `previousCursorPos` (private Vector3).
- Final cell iteration in `OnLeftClickUp`: `Grid.PosToXY(downPos, out x, out y)` … `Grid.PosToXY(cursor_pos, ...)` (swapped to min/max), then `Grid.XYToCell(j, i)` per cell, `OnDragTool(cell, dist)`, `OnDragComplete(downPos, cursor_pos)`.
- Line mode: `SnapToLine(cursorPos)` constrains to 1 axis.

### World-px ↔ cell conversion (`Grid`, Assembly-CSharp/Grid.cs)
- `Grid.PosToCell(Vector2/Vector3)` (lines 1424/1432), `Grid.PosToXY(Vector3, out int x, out int y)` (line 1440), `Grid.XYToCell(x, y)`.
- `Grid.CellSizeInMeters`, `Grid.HalfCellSizeInMeters` (static fields, line 626 area).
- `InterfaceTool.GetRegularizedPos(input, minimize)` (Assembly-CSharp/InterfaceTool.cs line 252): snaps a world pos to the cell's corner using `Grid.CellToPosCCC(Grid.PosToCell(input), Grid.SceneLayer.Background) ± (HalfCellSizeInMeters, HalfCellSizeInMeters, 0)`.
- `InterfaceTool.GetWorldRestrictedPosition(input)` (line 258): clamps to `ClusterManager.Instance.activeWorld` min/max bounds.

### String format
- `UI.TOOLS.TOOL_AREA_FMT = "{0} x {1}\n{2} tiles"` (STRINGS/UI.cs line 13708) — RU renders as «15x4» + «60 (в клетках)». `UI.TOOLS.TOOL_LENGTH_FMT = "{0}"` (line 13710) is the Brush-mode length label.

### DragTool subclasses (Box/rect tools)
- `DigTool`, `CancelTool`, `DeconstructTool`, `DisconnectTool`, `EmptyPipeTool`, `PrioritizeTool` extend `FilteredDragTool` (Assembly-CSharp/FilteredDragTool.cs, adds only layer filters — no OnMouseMove override).
- `BuildTool` overrides `GetMode()` → `Mode.Brush` (NO rectangle; uses `TOOL_LENGTH_FMT`-style length via `GetDragLength()`), and overrides `OnMouseMove`.
- Other DragTool subclasses: `MopTool`, `PlaceTool`, `DisinfectTool`, `HarvestTool`, `ClearTool`, `DebugTool`, `AttackTool`, `CopySettingsTool`, `BaseUtilityBuildTool`, `CaptureTool`.
- `NameDisplayScreen` (Assembly-CSharp/NameDisplayScreen.cs): `AddAreaText(string, GameObject)` line 184, `GetWorldText(Guid)` line 194, `RemoveWorldText(Guid)` line 208.

## Q3 — Existing mod structure to follow

Repo root: `/home/apkawa/code/ONI_MODS/Apkawa_ONI_Mods/`. Solution `ONI-mods.sln` currently contains exactly two projects:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "BuildDoorOverWall", "BuildDoorOverWall\BuildDoorOverWall.csproj", "{D298ADB7-CCBD-4EC0-A42C-EA03FBF00B78}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "UtilLibs", "UtilLibs\UtilLibs.csproj", "{3606AD3B-C8FA-40E7-833E-49F3FAC20061}"
EndProject
```
plus 4 `ProjectConfigurationPlatforms` lines per project GUID (Debug/Release × ActiveCfg/Build.0).

### Adding a project to the sln
MSBuild's sln parser has NO comment support (commented `Project(` lines still parse → MSB4121). Disabled projects are removed, not commented. To re-enable, add back the `Project(...)` line *and* the 4 configuration lines per GUID. The re-enable lines for the two previously-disabled projects (`example`, `DebugButton`) are documented in **CONTRIBUTING.md** ("Disabled solution projects", lines 75–104), NOT README.md (AGENTS.md says README.md — actual location is CONTRIBUTING.md):
- `example` GUID `{31DF774A-8818-45ED-A745-C1A3254608AC}`
- `DebugButton` GUID `{B430901D-43AD-4991-936F-8290599C4A8A}`

### BuildDoorOverWall (reference mod) — files
- `BuildDoorOverWall/BuildDoorOverWall.csproj` — SDK-style; `PackageId=BuildDoorOverWall`, `Version=0.0.1`, `<TargetFramework>net48</TargetFramework>`, `AssemblyName=$(PackageId)`, `RootNamespace=$(PackageId)`, `IsMod=true`, `GenerateMetadata=true`, `IsPacked=true`, `ModName=$(PackageId)`, `ModDescription` (empty), Release `OutDir=bin`. Has `<ProjectReference Include="..\UtilLibs\UtilLibs.csproj" />`. NO game-dll references in csproj (they come from Directory.Build.props).
- `BuildDoorOverWall/Mod.cs` — `namespace OxygenNotIncluded.Mods { public class Mod : UserMod2 { public override void OnLoad(Harmony harmony) { base.OnLoad(harmony); ... harmony.Patch(MethodInfo, postfix: new HarmonyMethod(...)) ... } } }`. Patches are attached PROGRAMMATICALLY (comment: base.OnLoad runs Harmony PatchAll which only applies types with [HarmonyPatch] attributes). Uses `FindMethod(typeof(BuildingDef), "IsValidPlaceLocation", ...)` byref-normalizing reflection (game Harmony v2 resolves out-params via Type.GetMethod paramTypes — `typeof(string)` ≠ `out string`). Also uses `PeterHan.PLib.Core.PUtil` (`LogDebug`, `.F` string ext).
- `BuildDoorOverWall/README.md` — per-mod description (listed in root README.md table).
- Build output (`bin/Debug/net48/`): `BuildDoorOverWall.dll` (packed: UtilLibs + PLib inlined), `.pdb`, `mod.yaml` (title/description/staticID), `mod_info.yaml` (minimumSupportedBuild/version/APIVersion 2/supportedContent ALL) — yamls auto-generated by Directory.Build.targets targets `GenerateModYaml` / `GenerateModInfoYaml`.

### UtilLibs (shared lib)
- `UtilLibs/UtilLibs.csproj` — `net48`, `IsMod=false`, `DoNotBuildAsMod=true`, `GenerateMetadata=false`, `IsPacked=false`, `<PackageReference Include="PLib" Version="4.19.0" />` (PeterHan.PLib). Contains only `Class1.cs` (empty namespace stub) — the real shared code is the PLib package.
- Note: AGENTS.md says UtilLibs is used "shared UI/util code the mod uses" — keep the ProjectReference and `IsPacked=true` ON THE MOD (IsPacked=true packs UtilLibs + PLib into the mod dll via ILRepack).

### Template
- `UpdatedOniTemplate/` is referenced by AGENTS.md/CONTRIBUTING.md as "a template — not in the sln, never build it", but the folder does NOT exist in this checkout (nor do `example/` or `DebugButton/` — they are referenced as disabled projects but absent from the working tree). **Unresolved: the template to copy from is missing; the only in-tree reference mod is BuildDoorOverWall.**

### Directory.Build.props (repo-level, applies to all projects)
- `GameLibsFolder` from `Directory.Build.props.user` (line 6): `$(HOME)/ONI/dlls` (fallback `.default`: a Windows Steam path).
- Game refs (lines 91–181): `Assembly-CSharp` + `Assembly-CSharp-firstpass` (both `<Publicize>true</Publicize>` → in-process BepInEx.AssemblyPublicizer, copies in `obj/.../publicized/`), `0Harmony`, `UnityEngine`, `UnityEngine.CoreModule`, `Newtonsoft.Json` — all `<HintPath>$(GameLibsFolder)/...</HintPath>`, `<Private>False</Private>`.
- `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3, `IncludeAssets=build; contentfiles` (NO runtime).
- Comment: "do NOT add NuGet Harmony (0Harmony 1.x) — game bundles 0Harmony v2".
- net48 reference-assemblies packages for non-Windows (Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3).
- `dotnet-ilrepack` 2.0.45 package (only when `IsPacked=true`).
- `MinimumSupportedBuild=$(TargetGameVersion)`; `TargetGameVersion=$(Aquatic)`=731233 (latest, line 48–49); `APIVersion=2`; `SupportedContent=ALL`.

### Directory.Build.targets (repo-level)
- `Clean` (BeforeTargets PreBuildEvent), `GenerateModYaml`, `GenerateModInfoYaml` (skip when `DoNotBuildAsMod=true`), `ILRepack` (AfterTargets Build, `IsPacked=true` only): runs `dotnet ILRepackTool.dll` (path from NuGetPackageRoot/dotnet-ilrepack/2.0.45/tools/net8.0/any/ILRepackTool.dll) with `/lib:$(GameLibsFolder)`, excludes `**/*Harmony.dll; **/Splat.dll; **/Assembly-*; **_public.dll; **Newtonsoft.Json; **/System.*; **/Microsoft.*; **/Unity*`.
- `CopyModsToDevFolder` (AfterTargets ILRepack, skip when `DoNotBuildAsMod=true`): copies dll + pdb + mod.yaml + mod_info.yaml (+ ModAssets) to `$(ModFolder)\$(TargetName)_dev\`. `ModFolder` from `Directory.Build.props.user`.

## Q4 — Build environment facts

- Game dlls: `~/ONI/dlls/` (read-only in sandbox; `Directory.Build.props.user` sets `GameLibsFolder=$(HOME)/ONI/dlls`). Contains `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `0Harmony.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `Newtonsoft.Json.dll`, etc. (verified present).
- 0Harmony: the **game's** `~/ONI/dlls/0Harmony.dll` is **v2.4.2.0** (strings on the dll; CONTRIBUTING.md "0Harmony 2.4.2"). All mods reference THIS dll (Directory.Build.props `0Harmony` Reference). NuGet `Harmony` (0Harmony 1.x) is forbidden — different type identity for `UserMod2.OnLoad(Harmony)`.
  - NOTE: the decompiled `lib_sources/0Harmony/` copy's `AssemblyInfo.cs` says `AssemblyVersion = "1.1.2"` — that folder is the decompiled 0Harmony v2 assembly (has `HarmonyLib/` with `AttributePatch.cs` etc.) but its version constant reads 1.1.2; treat the dll (2.4.2.0) as authoritative. (Unresolved: why the decompiled AssemblyInfo says 1.1.2.)
  - `lib_sources/Harmony/` is the Harmony SOURCE git checkout (AGENTS.md, CNAME, CODE_OF_CONDUCT.md, Directory.Build.props…), NOT decompiled game code.
- Build command (AGENTS.md / CONTRIBUTING.md), from repo root, caches in `.cache/`:
  `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug`
- Requires .NET SDK 8.0 (net48 targeting pack via NuGet ref-assemblies on Linux; no full Framework needed).
- Acceptance: user manual in-game run only; build success ≠ mod works. `CopyModsToDevFolder` fails with MSB3027 "Read-only file system" in the sandbox (expected; artifacts stay in `bin/`).
- Store-mods location: `./.tmp/build_mod_dir/` (per AGENTS.md "Paths"); the in-tree target is `~/ONI/mods/<ModName>_dev/` (fails in sandbox).

## Candidate hook points (consolidated, Q1)

For injecting the «РАЗМЕР:» block into drag-tool tooltips:
1. **`HoverTextConfiguration.DrawInstructions`** (Assembly-CSharp/HoverTextConfiguration.cs:72) — shared, runs for every tool card; no drag state, but runs right where the «ПЕРЕТАЩИТЬ / НАЗАД» line is drawn (inject size lines just before it, i.e. between title and instructions — matches spec layout).
2. **`HoverTextConfiguration.UpdateHoverElements`** (HoverTextConfiguration.cs:108, virtual) — shared entry; note drag tools override it, so patching the BASE only affects tools that don't override.
3. **`InterfaceTool.UpdateHoverElements(List<KSelectable>)`** (InterfaceTool.cs:287, protected wrapper) or **`InterfaceTool.LateUpdate`** (InterfaceTool.cs:295) — shared per-frame driver; has `this` (the tool) so can downcast to `DragTool` and read drag state.
4. **Each drag tool's `UpdateHoverElements` override** (DigToolHoverTextCard.cs:27, CancelToolHoverTextCard.cs:9, DeconstructToolHoverTextCard.cs:9, BuildToolHoverTextCard.cs:11, MopToolHoverTextCard.cs:27, HarvestToolHoverTextCard.cs:7, PrioritizeToolHoverTextCard.cs:7, EmptyPipeToolHoverTextCard.cs:7, PlaceToolHoverTextCard.cs:9, PrebuildToolHoverTextCard.cs:10, MoveToLocationToolHoverTextCard.cs:7, AttackToolHoverTextCard.cs:6, SandboxStoryTraitToolHoverTextCard.cs:8) — per-tool; each can reach its own `DragTool` instance via the same GameObject.
5. **`DragTool.OnMouseMove`** (Assembly-CSharp/DragTool.cs:~332) — where W/H/cell-count are computed; best to CAPTURE state (e.g. static fields / a shared component) so any tooltip hook can read them. `areaVisualizerSpriteRenderer.size` (meters) and `Grid.PosToCell` of `downPos`/cursor are the state.

Note: `populateHitsList` (InterfaceTool.cs:32) is `false` for drag tools (only SelectTool/UtilityBuildTool set it true), so `InterfaceTool.LateUpdate` takes the `else` branch → `UpdateHoverElements(null)` every frame for drag tools — the per-tool cards ARE updated every frame even without hits.
