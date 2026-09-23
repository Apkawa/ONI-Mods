# Research — 2026-09-22_spaceoverlay (SpaceOverlay)

Superficial context collection for the SpaceOverlay mod spec (dependencies, files, fixed constraints, existing solutions).
Findings are appended here as they are made.

## Questions

1. Repo structure: how existing mods are organized (csproj, sln, UserMod2, PLib usage, README layout).
2. `UpdatedOniTemplate/` — what a new mod project looks like.
3. Asset handling: `example_mods/Sgt_Imalas-Oni-Mods/BlueprintsV2/ModAssets.cs` pattern; existing SVG→PNG (`rsvg-convert`) flow in the repo.
4. Game internals: where the "Воздействие космоса" tooltip comes from; how space tiles are rendered; whether the game has existing toggleable overlay mechanisms.
5. PLib: helpers relevant to overlay/drawing or settings/toggles.

## Findings

### Q1. Repo structure

- `ONI-mods.sln` (repo root) lists 5 projects, all `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` C# SDK type:
  `BuildDoorOverWall`, `UtilLibs`, `SizeInTooltip`, `ReplaceBuildingMaterial`, `BestBuildDryWall`
  (each with Debug|Any CPU + Release|Any CPU ActiveCfg/Build lines). Disabled projects are
  REMOVED from the file (sln parser has no comments; MSB4121). CONTRIBUTING.md holds the
  re-enable lines for `example` and `DebugButton`.
- Mod project layout (`BuildDoorOverWall/`): `BuildDoorOverWall.csproj` + `Mod.cs` + `README.md`
  (+ optional `AGENTS.md`). csproj key properties:
  - `<PackageId>BuildDoorOverWall</PackageId>`, `<Version>0.0.1</Version>`, `<TargetFramework>net48</TargetFramework>`
  - `<AssemblyName>$(PackageId)</AssemblyName>`, `<RootNamespace>$(PackageId)</RootNamespace>`
  - `<IsMod>true</IsMod>`, `<GenerateMetadata>true</GenerateMetadata>`, `<IsPacked>true</IsPacked>`
  - Mod Info block: `<ModName>`, `<ModDescription>`, `<WorkshopItemId>0</WorkshopItemId>`
  - Release sets `<OutDir>bin</OutDir>` (no net48 subfolder in Release).
  - `ProjectReference` to `..\UtilLibs\UtilLibs.csproj`.
- `UtilLibs/UtilLibs.csproj`: `<IsMod>false</IsMod>`, `<DoNotBuildAsMod>true</DoNotBuildAsMod>`,
  `<GenerateMetadata>false</GenerateMetadata>`, `<IsPacked>false</IsPacked>`, and
  `<PackageReference Include="PLib" Version="4.19.0" />`.
- Mod code: single `Mod.cs`, `namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2`,
  overrides `public override void OnLoad(Harmony harmony)`; calls `base.OnLoad(harmony)` first
  (runs Harmony PatchAll on attribute-bound patches). PLib usage: `using PeterHan.PLib.Core;`,
  logging via `PUtil.LogDebug(...)`, format via `"...".F(args)` extension.
  BuildDoorOverWall attaches patches programmatically via
  `PatchUtil.TryPatch(harmony, typeof(T), "Method", paramTypes[], desc, postfix: new HarmonyMethod(typeof(Cls), nameof(Cls.Postfix)))`
  (PatchUtil is in UtilLibs).
- Root build files:
  - `Directory.Build.props`: `SolutionRoot`; imports `Directory.Build.props.user` (exists here)
    or `.default` (fallback); `Optimize`, `AllowUnsafeBlocks`, `LangVersion=preview`,
    `WarningsAsErrors CS0618,CS0612`; `TargetGameVersion=$(Aquatic)=731233`;
    `MinimumSupportedBuild`, `SupportedContent=ALL`, `APIVersion=2`, `requiredDlcIds`/`forbiddenDlcIds`;
    Author=Apkawa; References to game dlls `$(GameLibsFolder)/Assembly-CSharp.dll` (with
    `<Publicize>true</Publicize>`), `Assembly-CSharp-firstpass.dll` (publicize), `0Harmony.dll`,
    `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `Newtonsoft.Json.dll` — all `<Private>False</Private>`.
    `BepInEx.AssemblyPublicizer.MSBuild 0.4.3` with `<IncludeAssets>build; contentfiles</IncludeAssets>`
    when `IsMod=true`; `dotnet-ilrepack 2.0.45` when `IsPacked=true`;
    `Converter.MarkdownToBBCodeNM.Tool 1.0.0.29` when `IsPacked=true`.
  - `Directory.Build.targets`: `Clean` before PreBuildEvent; `GenerateModYaml` + `GenerateModInfoYaml`
    write `mod.yaml` (title/description/staticID; Debug appends " [DEBUG]" + `_dev` staticID) and
    `mod_info.yaml` (minimumSupportedBuild/version/APIVersion/supportedContent);
    `GenerateReadmeBbcode` (Release, converts README.md → README.txt BBCode); `ILRepack` target
    (AfterTargets Build, `IsPacked=true`): packs `$(TargetDir)\*.dll` excluding
    `$(TargetPath); **/*Harmony.dll; **/Splat.dll; **/Assembly-*; **_public.dll; **Newtonsoft.Json; **/System.*; **/Microsoft.*; **/Unity*`
    into `$(TargetPath)` with `/lib:"$(GameLibsFolder)"`;
    `CopyModsToDevFolder` copies dll+pdb+yamls(+README.txt) to
    `$(ModFolder)\$(TargetName)_$(ModBuildFlavor)\` (dev/release) plus everything under `.\ModAssets\`
    (recursive, `%(RecursiveDir)` preserved — i.e. a mod's assets folder is `ModAssets/` next to the csproj);
    `GenerateSteamVdf` (Release) writes `workshop_build.vdf`.
  - `Directory.Build.props.user`: `GameLibsFolder=$(HOME)/ONI/dlls`, `ModFolder=$(SolutionRoot).tmp/build_mod_dir`, `RefasmerInstalled=0`.
- Installed mod folders (in sandbox): `.tmp/build_mod_dir/BuildDoorOverWall_dev/`, `..._release/`, etc.
  (one folder per mod+flavor; DLLLoader loads every *.dll in it).
- README convention: root `README.md` has a 2-column table `| Mod | Description |` with a link
  `ModName/README.md`; new mods must be added there. Mod READMEs written in English (mod-readme skill).
- Build invariants (AGENTS.md + CONTRIBUTING.md): TFM net48 everywhere; reference the game's
  `0Harmony.dll` (v2), never NuGet `Harmony`; Publicizer 0.4.3 assets `build; contentfiles` ONLY
  (adding `runtime` leaks AsmResolver/*.dll into bin/); ILRepack `dotnet-ilrepack` 2.0.45 run as
  `dotnet ILRepackTool.dll` (2.0.46+ need .NET 10); sln parser has no comment support; UtilLibs
  carries PeterHan.PLib 4.19.0; `KMod/DLLLoader` loads every `*.dll` in a mod folder, ≤1
  `UserMod2` subclass per assembly, one `Harmony` per mod folder.
- `scripts/` exists at repo root (mod cover generator, Pillow-based per git log — not rsvg).

### Q2. Template — UNRESOLVED: `UpdatedOniTemplate/` DOES NOT EXIST

- `CONTRIBUTING.md` (line 58) and `AGENTS.md` both state `UpdatedOniTemplate/` is a project
  template intentionally not in the sln, but the directory is absent from the working tree,
  from `git ls-files`, and from `fd -H -t d -i template` across `/home/apkawa/code`.
- Therefore a new mod project must be copied from an existing mod in this repo
  (smallest complete example: `BuildDoorOverWall/` — csproj + Mod.cs + README.md).
  External reference template: https://github.com/O-n-y/OxygenNotIncludedModTemplate (CONTRIBUTING.md).

### Q3. Assets

- `example_mods/Sgt_Imalas-Oni-Mods/BlueprintsV2/ModAssets.cs`: class `ModAssets` (internal, namespace `BlueprintsV2`).
  - Static `Sprite` fields for tool icons/visualizers (`BLUEPRINTS_CREATE_ICON_SPRITE`, `..._VISUALIZER_SPRITE`, etc.)
    plus static `Color` constants (`UIUtils.rgb(...)` / `new Color32(r,g,b,a)`).
  - `public static void LoadAssets()` loads a PLATFORM-SPECIFIC Unity asset bundle:
    `AssetUtils.LoadAssetBundle("blueprints_ui", platformSpecific: true)` then `bundle.LoadAsset<GameObject>("Assets/UIs/blueprintSelector.prefab")`.
    The `AssetUtils` used there is Sgt_Imalas' OWN helper
    (`example_mods/Sgt_Imalas-Oni-Mods/UtilLibs/AssetUtils.cs`) — NOT PLib and not the game's `Assets` class.
  - Icon sprites are NOT loaded from loose PNGs: `BlueprintsV2/Patches/SpritePatch.cs` assigns them via
    `InjectionMethods.AddSpriteToAssets(__instance, sprite)` in a patch (i.e. sprites come from the
    bundle / injected game assets).
- `rsvg-convert` / SVG→PNG: **NO existing usage anywhere** in `/home/apkawa/code/ONI_MODS/`
  (searched all file types: cs/csproj/props/targets/sh/md/xml/yaml; `fd -e svg` finds only Harmony doc
  SVGs). `scripts/generate_cover.py` is a Pillow-based workshop-cover generator (no SVG input).
  So there is no in-repo precedent/pattern for an rsvg-convert build step — it would be a new
  script/target (e.g. a bash script or an MSBuild `Exec` target converting `icon.svg` → `ModAssets/...png`).
- How loose PNG assets actually ship (this repo): the `CopyModsToDevFolder` target in
  `Directory.Build.targets` copies `.\ModAssets\**\*.*` from the project dir into
  `$(ModTargetFolder)\%(RecursiveDir)` — i.e. a project-level `ModAssets/` folder is the asset root
  (see Q1). A PNG placed in `ModAssets/` ends up at `<modfolder>/...` and can be loaded at runtime.
- PLib asset loading API (verified by decompiling the NuGet PLib 4.19.0 dll, `.tmp/plib4190_decomp/`):
  `PeterHan.PLib.UI.PUIUtils` (decompiled `PeterHan.PLib.UI/PUIUtils.cs`):
  - `public static Sprite LoadSprite(string path, Vector4 border = default, bool log = true)` —
    loads from the CALLING ASSEMBLY's manifest resources (`assembly.GetManifestResourceStream(path)`),
    PNG/DXT5/JPG, `ImageConversion.LoadImage` + `Sprite.Create(..., 100f, 0u, SpriteMeshType, border)`;
    throws `ArgumentException` if missing.
  - `public static Sprite LoadSpriteFile(string path, Vector4 border = default)` — same but from a
    FILE SYSTEM path; returns `null` on failure (does not throw).
  - PLib 4.19.0 has **no AssetLibrary/AssetBundle classes** (checked decompiled list: 151 classes in
    namespaces Core/Actions/Buildings/Database/Lighting/Options/UI/PatchManager/AVC).
  - Game-side asset access available to mods (decompiled Assembly-CSharp): `Assets` static class
    (`Assets.GetSprite(string)`, `Assets.LoadTexture`? — see Q4 notes) and `KAssetBundle`/asset-bundle
    loaders are the game's own; PLib does not wrap them in 4.19.0.

### Q4. Game internals (part 1: tooltip)

- The tooltip text «Воздействие космоса» = localization key `STRINGS.MISC.STATUSITEMS.SPACE.NAME`:
  - EN: `Assembly-CSharp/STRINGS/MISC.cs` — nested `public class SPACE` inside
    `public class STATUSITEMS` inside `STRINGS.MISC`:
    `public static LocString NAME = "Space exposure";`
    `public static LocString TOOLTIP = "This region is exposed to the vacuum of space and will result in the loss of Gas and Liquid resources";`
  - RU confirmation: `~/ONI/game/.../StreamingAssets/strings/strings_preinstalled_ru_klei.po` lines 85967-85970:
    `msgctxt "STRINGS.MISC.STATUSITEMS.SPACE.NAME" / msgid "Space exposure" / msgstr "Воздействие космоса"`.
- Where it is DRAWN: `Assembly-CSharp/SelectToolHoverTextCard.cs` line 733:
  in the cell hover-text builder, `if (CellSelectionObject.IsExposedToSpace(num)) { ... DrawText(MISC.STATUSITEMS.SPACE.NAME, Styles_BodyText.Standard); }`
  (i.e. the "space exposure" line appears in the SELECT tool's cell hover card).
- `CellSelectionObject.IsExposedToSpace(int cell)` — `Assembly-CSharp/CellSelectionObject.cs` line 266:
  `public static bool IsExposedToSpace(int cell)` (used at line 235 too).
- Related grid API: `Assembly-CSharp/Grid.cs`:
  - `public static bool IsCellOpenToSpace(int cell)` (line 1402)
  - `public static bool IsCellBiomeSpaceBiome(int cell)` (line 1419):
    `return World.Instance.zoneRenderData.GetSubWorldZoneType(cell) == SubWorld.ZoneType.Space;`

### Q4. Game internals (part 2: space rendering)

- There is NO class named `SpaceTile` in decompiled sources (searched `Assembly-CSharp`,
  `Assembly-CSharp-firstpass`). Space ("vacuum" cells) rendering facts so far:
- `Assembly-CSharp/Rendering.World/TileRenderer.cs`: `public abstract class TileRenderer : KMonoBehaviour`
  (tile grid of `Tile` objects + `Brush`/`Mask` atlas system; abstract
  `public abstract void LoadBrushes()`; `public abstract void MarkDirty(ref Tile tile, Brush[] brush_array, int[] brush_grid)`;
  `public void Clear(ref Tile, Brush[], int[])`; `LateUpdate()` refreshes dirty brushes / calls `Brush.Render()`).
  Only concrete subclass found: `Assembly-CSharp/Rendering.World/LiquidTileOverlayRenderer.cs`
  (`public class LiquidTileOverlayRenderer : TileRenderer`).
- `Assembly-CSharp/SubworldZoneRenderData.cs`: `public class SubworldZoneRenderData : KMonoBehaviour`
  (on `World`). Builds two per-cell `Texture2D`s (RGB24, Grid.WidthInCells×HeightInCells):
  `colourTex` ("SubworldRegionColourData", bilinear) and `indexTex` ("SubworldRegionIndexData", point).
  - Space cells are the sentinel: `zoneColours[7] = new Color32(byte.MaxValue,0,0,7)` and
    `rawTextureData2[num*3] = (zoneType == SubWorld.ZoneType.Space) ? byte.MaxValue : zoneTextureArrayIndices[...]`.
  - `OnShadersReloaded()`: `Shader.SetGlobalTexture("_WorldZoneTex", colourTex);` and
    `Shader.SetGlobalTexture("_WorldZoneIndexTex", indexTex);` — so the SHADERS sample these
    global textures to color zones; space = index 255.
  - `public SubWorld.ZoneType GetSubWorldZoneType(int cell)` (line 220) — per-cell zone type query.
  - `public void GenerateTexture()` (line 151) fills the textures; `public void OnActiveWorldChanged()` (line 96).
  - `worldZoneTypes[i] = SubWorld.ZoneType.Space` is the DEFAULT for cells outside overworld polygons.
- Still to pin down (open): which shader draws the black "void" on the MAIN map for vacuum cells
  (candidates: the world tile shader sampling `_WorldZoneTex`, or a per-cell vacuum tile visualizer),
  and how the MINIMAP renders space (uses `_WorldZoneTex` too).
  NOTE: the decompiled `lib_sources` tree is PARTIAL (4409 .cs files; no `Main.cs`,
  no `Map`/`Minimap`/`GridRenderer` classes) — missing classes must be decompiled from
  `~/ONI/dlls/Assembly-CSharp.dll` with ilspycmd.

### Q4. Game internals (part 3: existing toggleable overlay mechanism — YES, it exists)

The game's map overlay system (the in-map "tech view" overlay button row):

- `Assembly-CSharp/OverlayScreen.cs`: `public class OverlayScreen : KMonoBehaviour`
  - `public static OverlayScreen Instance;`
  - `private Dictionary<HashedString, ModeInfo> modeInfos;` (ModeInfo{ OverlayModes.Mode mode })
  - `private void RegisterModes()` — in `OnSpawn()` registers each mode via
    `RegisterMode(new OverlayModes.Oxygen())` etc. Full list: None, Oxygen, Power, Temperature,
    ThermalConductivity, Light, LiquidConduits, GasConduits, Decor, Disease, Crop, Harvest,
    Priorities, HeatFlow, Rooms, Suit, Logic, SolidConveyor, TileMode, Radiation.
  - `public void ToggleOverlay(HashedString newMode, bool allowSound = true)` —
    calls `SimDebugView.Instance.SetMode(newMode)`, then `mode.Disable()`/`mode.Enable()`,
    fires `public Action<HashedString> OnOverlayChanged` delegate, updates legend via
    `OverlayLegend.Instance.SetLegend(mode)`.
  - `public HashedString GetMode();`
  - `private void ActivateLegend();`
  - `Shader.SetGlobalVector("_OverlayParams", Vector4.zero)` in OnSpawn.
- `Assembly-CSharp/OverlayModes.cs`: `public abstract class OverlayModes` with nested
  `public abstract class Mode` (line 1880):
  - `public abstract HashedString ViewMode();`
  - `public virtual void Enable() { }` / `public virtual void Update() { }` / `public virtual void Disable() { }`
  - `public virtual List<LegendEntry> GetCustomLegendData() => null;`
  - `public static void Clear();` plus helpers `PopulatePartition<T>`, `GetDefaultDepth`,
    `ResetDisplayValues`, `RemoveOffscreenTargets`...
  - Each concrete mode: `public class X : Mode` with `public static readonly HashedString ID = "X"`
    (e.g. `OverlayModes.Oxygen.ID`, `OverlayModes.Radiation.ID`) and `public override HashedString ViewMode() { return ID; }`.
  - `OverlayModes.None.ID` is the "no overlay" mode; `OverlayScreen.ToggleOverlay(OverlayModes.None.ID)` clears.
  - Conduit-mode example (`ConduitMode.Enable`): sets camera cullingMask to include
    `MaskedOverlay`/`MaskedOverlayBG` layers, moves targeted SaveLoadRoots to overlay depth,
    `SelectTool.Instance.SetLayerMask(...)`, `GridCompositor.Instance.ToggleMinor(on:false)` —
    i.e. overlays work by re-layering/repainting specific object types, not by a single full-map pass.
- `Assembly-CSharp/OverlayMenu.cs`: `public class OverlayMenu : KIconToggleMenu`
  - `public static OverlayMenu Instance;`
  - `private List<ToggleInfo> overlayToggleInfos;` built in `private void InitializeToggles()`
    as `new OverlayToggleInfo(UI.OVERLAYS.<X>.BUTTON, "overlay_<name>", OverlayModes.<X>.ID,
    required_tech_item, Action.Overlay<n>, UI.TOOLTIPS.<X>OVERLAYSTRING, UI.OVERLAYS.<X>.BUTTON)`.
    Icon names: "overlay_oxygen", "overlay_power", "overlay_temperature", "overlay_materials",
    "overlay_lights", "overlay_liquidvent", "overlay_gasvent", "overlay_decor", "overlay_disease",
    "overlay_farming", "overlay_rooms", "overlay_suit", "overlay_logic", "overlay_conveyor",
    "overlay_radiation" (radiation added only `if (Sim.IsRadiationEnabled())`).
  - Hotkeys: `Action.Overlay1`..`Action.Overlay15`.
  - `private void OnToggleSelect(ToggleInfo toggle_info)` — if current mode == toggle's simView →
    `OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID)` (toggle off); else
    `OverlayScreen.Instance.ToggleOverlay(((OverlayToggleInfo)toggle_info).simView)`.
  - `private void OnOverlayChanged(object overlay_data)` — syncs each `toggle.isOn` to
    `OverlayScreen`'s current mode (Game.Instance.Subscribe(1798162660, OnOverlayChanged)).
  - Tech gating: `OverlayToggleInfo.IsUnlocked()` checks `Db.Get().Techs.IsTechItemComplete(requiredTechItem)`
    (or SandboxModeActive / InstantBuildMode bypass).
  - `OnKeyDown`/`OnKeyUp`: Escape / right-click closes the active overlay (→ None).
- Tooltip strings: `STRINGS/UI.cs` `UI.OVERLAYS.*.BUTTON` and `UI.TOOLTIPS.*OVERLAYSTRING`.
- Related UI: `Assembly-CSharp/OverlayLegend.cs` (`public class OverlayLegend : KScreen` with
  nested `OverlayInfo`/`OverlayInfoUnit`), `Assembly-CSharp/SimDebugView.cs`
  (`public class SimDebugView` — `public static class OverlayModes` nested enum + `SetMode(HashedString)`).
- MOD PLUG-IN SURFACE (facts, no design): a mod can (a) subclass/extend `OverlayModes.Mode` and
  register it via Harmony on `OverlayScreen.RegisterModes` (private method) or by calling
  `OverlayScreen.Instance` at runtime; (b) add an `OverlayToggleInfo`-style entry by patching
  `OverlayMenu.InitializeToggles` (private, builds the list) or by post-processing
  `OverlayMenu.overlayToggleInfos` / calling `Setup` again; (c) react to the game's
  `OverlayScreen.OnOverlayChanged` delegate / `Game.Instance` event 1798162660.
  No mod-facing API exists: `UserMod2` has no overlay hooks (checked `Assembly-CSharp/KMod/UserMod2.cs` —
  only `OnLoad(Harmony)`, `OnAllModsLoaded(Harmony, IReadOnlyList<Mod>)`,
  `assembly`/`path`/`mod` properties — no overlay hooks; no `OnUnload` exists.
  Full `KMod/UserMod2.cs` (23 lines): base `OnLoad` does `harmony.PatchAll(assembly)`.
  `KMod/DLLLoader.cs` lives in `Assembly-CSharp/KMod/` (loads all *.dll in mod folders).

### Q4. Game internals (part 4: how space/vacuum is actually drawn)

- `Assembly-CSharp/World.cs`: `public class World : KMonoBehaviour`
  - `public static World Instance { get; private set; }`
  - `public BlockTileRenderer blockTileRenderer;` (a `Rendering.BlockTileRenderer : MonoBehaviour` —
    835-line mesh renderer for solid block tiles; contains NO vacuum/space handling)
  - `public GroundRenderer groundRenderer;` (`[MyCmpGet]`)
  - `public SubworldZoneRenderData zoneRenderData { get; private set; }`
    (`GetComponent<SubworldZoneRenderData>()` in OnPrefabInit).
- `Assembly-CSharp/GroundRenderer.cs` (`public class GroundRenderer : KMonoBehaviour`):
  chunked per-element mesh builder (16×16 chunks). Two static `BiomeMaskCheck`s:
  1. solid tiles: element `Grid.Element[cell]` when `(cell == Grid.InvalidCell || Grid.RenderedByWorld[cell]) && e.IsSolid` → opaque/alpha quad;
  2. backwalls: element `BackwallManager.At(cell).Element ?? ElementLoader.FindElementByHash(SimHashes.Vacuum)`,
     check `e.id != SimHashes.Vacuum` → backwall quad.
  **i.e. vacuum cells are simply NOT MESHED** (backwall check excludes `SimHashes.Vacuum`);
  the black void on the main map is whatever is behind the world geometry (world background /
  shader), not per-cell geometry drawn by C# code.
- `Grid.RenderedByWorld` — `public static VisFlagsRenderedByWorldIndexer RenderedByWorld;`
  (Grid.cs line 662; bit 4 of per-cell `VisMasks`).
- `_WorldZoneTex` / `_WorldZoneIndexTex` (set by `SubworldZoneRenderData.OnShadersReloaded`) are
  consumed by compiled shader code only (no other C# references in the whole decompiled tree;
  shaders are compiled inside asset bundles — no `.shader` source files on disk;
  `ShaderReloader` is an empty stub, all methods are no-ops).
  The MINIMAP colors come from these same global zone textures (space = index 255,
  sentinel colour `zoneColours[7] = (255,0,0,7)`).
- Type lookups in the REAL `~/ONI/dlls/Assembly-CSharp.dll` (ilspycmd `-t`):
  `Map`, `MapScreen`, `Minimap`, `MinimapScreen`, `MiniMap`, `Klei.Minimap`, `Klei.Map`,
  `Klei.MapScreen` — **none exist**. There is no C# `Minimap`/`Map` class; the minimap and
  zone coloring are shader/prefab-driven via the global zone textures.
  (ilspycmd `-l types` output is truncated at ~1029 entries in the installed 8.2.0 build —
  treat its negatives with care, but direct `-t` lookups are reliable.)
- `Grid.cs` space API (already listed): `IsCellOpenToSpace(int cell)`, `IsCellBiomeSpaceBiome(int cell)`.
- `CellSelectionObject.IsExposedToSpace(int cell)` (line 266) is the hover-tooltip predicate.

### Q4. Game internals (part 5: Main / GameOptions / ModsManager overlay search)

- `GameOptions.cs` does NOT exist in the decompiled tree; `GameOptions` type also not in the
  real dll. What exists is `public class GameOptionsScreen : KModalButtonMenu`
  (decompiled from real dll, 213 lines) — contains ONLY sandbox-mode buttons
  (`OnUnlockSandboxMode`, `SetSandboxModeActive`) + a `SaveConfigurationScreen`
  reference. **No overlay-related settings, no mod hooks.**
- `Main` / `Klei.Main` types do NOT exist (ilspycmd `-t` lookup). `MainMenu` exists
  (756 lines, the pause menu) — no "overlay" references. `Game.cs` — no "overlay" references.
- `Assembly-CSharp/KMod/` (mod manager: `Manager.cs`, `DLLLoader.cs`, `Mod.cs`,
  `ModErrorsScreen.cs`, ...) — **zero** case-insensitive "overlay" matches in the whole folder.
  There is no game-side mod API for registering overlays; everything goes through Harmony.
- So the ONLY toggleable-overlay mechanisms in the game are the
  `OverlayScreen`/`OverlayModes`/`OverlayMenu` trio (part 3) — 15 built-in toggles,
  hotkeys `Action.Overlay1..15`, and no public registration API.

## Findings (Q5: PLib helpers)

Sources: decompiled NuGet PLib 4.19.0 (`.tmp/plib4190_decomp/`, from
`UtilLibs`'s `<PackageReference Include="PLib" Version="4.19.0" />`) and repo
`lib_sources/peterhaneve_ONIMods/PLib*`.

### Q5a. Drawing / tile overlays — PLib has NONE for tiles

- PLib UI is strictly dialog/screen widgets: classes in `PeterHan.PLib.UI` are
  `PButton`, `PCheckBox`, `PComboBox`, `PContainer`, `PDialog`, `PGridLayoutGroup`,
  `PGridPanel`, `PRelativePanel`, `PScrollPane`, `PLabel`, `PPanel`, `PSliderSingle`,
  `PSpacer`, `PTextArea`, `PTextField`, `PTextComponent`, plus layout classes
  (`BoxLayoutGroup`, `GridColumnSpec`, `GridComponent`, `PUIAnchoring`...).
- `PUIElements.CreateUI(GameObject parent, string name, bool canvas = true,
  PUIAnchoring horizAnchor = PUIAnchoring.Stretch, PUIAnchoring vertAnchor = PUIAnchoring.Stretch)`
  (PeterHan.PLib.UI/PUIElements.cs line 69) + `SetAnchors`, `SetText`, `SetToolTip`,
  `SetAnchorOffsets`, `AddSizeFitter`.
- `PUIUtils` (extension helpers): `AddTo(IUIComponent, GameObject parent, int index = -2)`,
  `ForceLayoutRebuild`, `SetFlexUISize`, `SetMinUISize`, `SetUISize` — sizing/layout only.
- `PUITuning` static built-in sprites: `Arrow`, `BoxBorder`, `BoxBorderWhite`,
  `ButtonBorder`, `CheckBorder`, `Checked`, `Close`, `Contract`, `Expand`, `Partial`,
  `ScrollBorder/HandleHorizontal/Vertical` (PeterHan.PLib.UI/PUITuning.cs).
- **No tile/overlay/mesh drawing helpers anywhere in PLib** (151 classes checked;
  no AssetLibrary, no tile renderer). Tile overlay drawing is purely a game-side
  mechanism (`OverlayModes.Mode`, `TileRenderer`, `Shader` globals — see Q4).

### Q5b. Settings toggles (PLibOptions)

- Namespace `PeterHan.PLib.Options` (decompiled `PeterHan.PLib.Options/`):
  - `POptions` (class, implements `IOptions`):
    - `public POptions()`
    - `public static string GetConfigFilePath(Type optionsType)`
    - `public static void ShowDialog(Type optionsType, Action<object> onClose = null)`
    - `public void RegisterOptions(UserMod2 mod, Type optionsType)` — registers an
      options class (fields annotated with `[Option]`) to the mod.
  - `OptionsDialog` (`OptionsDialog.cs`): `public void ShowDialog()`,
    `public Action<object> OnClose { get; set; }` — the in-game mod settings window.
  - Field entries (one per field type): `CheckboxOptionsEntry(string field, IOptionSpec spec)`
    (bool fields → checkbox), `IntOptionsEntry`, `NullableIntOptionsEntry`,
    `FloatOptionsEntry`, `NullableFloatOptionsEntry`, `LogFloatOptionsEntry`,
    `StringOptionsEntry`, `Color32OptionsEntry`, `ColorOptionsEntry`,
    `SelectOneOptionsEntry`, `TextBlockOptionsEntry`, `CompositeOptionsEntry`,
    `ButtonOptionsEntry` (via `ButtonOptionsEntry.cs`), `SlidingBaseOptionsEntry`,
    base `OptionsEntry`.
  - Attributes: `[Option(title, tooltip = null, category = null)]` (`OptionAttribute :
    IOptionSpec`, also has `Format` property), `[RestartRequired]`
    (`RestartRequiredAttribute`), `[RequireDLC(...)]` (`RequireDLCAttribute`),
    `[Limit]` (`LimitAttribute`), `[ConfigFile(...)]` (`ConfigFileAttribute`),
    `[ModInfo]` (`ModInfoAttribute`), `ModDialogInfo`.
  - `IOptions` interface: `void OnOptionsChanged();` (called when options are written).
  - `SingletonOptions` (static registry of registered options instances).
  - Pattern: an options class (e.g. `class ModOptions { [Option("Space overlay", "...")] public bool enabled; }`)
    + `new POptions().RegisterOptions(mod, typeof(ModOptions))` in `OnLoad`,
    then `POptions.ShowDialog(typeof(ModOptions))` from a UI button.
    Config persists in a per-mod file (path from `GetConfigFilePath`).

### Q5c. Assets in PLib

- Confirmed: **no AssetLibrary/AssetBundle/asset-registration API in PLib 4.19.0**
  (verified against the full decompiled dll — 151 classes; asset work is just the
  two sprite loaders below).
- `PUIUtils.LoadSprite(string path, Vector4 border = default, bool log = true)`
  (PeterHan.PLib.UI/PUIUtils.cs line 698): loads from
  `Assembly.GetCallingAssembly().GetManifestResourceStream(path)` — i.e. the image
  must be EMBEDDED in the calling assembly as a manifest resource; decodes PNG/DXT5/JPG
  via `ImageConversion.LoadImage`; returns `Sprite.Create(tex, rect, (0.5,0.5), 100f ppu, 0,
  SpriteMeshType.FullRect, border)`; throws `ArgumentException` on failure; logs
  `LogUIDebug("Loaded sprite: {0} ({1:D}x{2:D}, {3:D} bytes)")` when `log`.
- `PUIUtils.LoadSpriteFile(string path, Vector4 border = default)` (line 756):
  loads from the file system (`FileStream`), same decode path, returns `null` on failure.
- Repo asset-shipping mechanism (from Q1): mod csproj + `Directory.Build.targets`
  copy `.\ModAssets\**\*.*` → `$(ModTargetFolder)\%(RecursiveDir)` into the build
  mod dir — so project-level `ModAssets/` folders ship loose files next to the dll.
  To use `PUIUtils.LoadSprite` the image must instead be embedded as a
  `<EmbeddedResource>` (that is what "calling assembly manifest resource" means).

---

## Stage 1 — deep research

> Scope: produce a concrete, implementable Harmony patch design for `SpaceOverlay`
> (a muted translucent-red tint over vacuum/space cells, toggled from the overlay menu).
> Research only — no mod code written. All facts below carry exact file paths and
> signatures (game decompiled sources at `/home/apkawa/code/ONI_MODS/lib_sources/`).

### 1. GroundRenderer internals

File: `lib_sources/Assembly-CSharp/GroundRenderer.cs` (766 lines). `public class GroundRenderer : KMonoBehaviour` (line 8).

**Chunk/mesh data structures**
- Nested `private struct Materials` (line 11): `Material opaque, alpha, backwall, backwallAlpha`.
- Nested `private class ElementChunk` (line 30): `SimHashes element; RenderData alpha, opaque, backwall, backwallAlpha; int tileCount;`. Ctor `ElementChunk(SimHashes element, Dictionary<SimHashes, Materials> materials)` (line 146) — one `RenderData` per material slot, pulled from `materials[element]`.
- Nested `private class ElementChunk.RenderData` (line 32): `Material material; Mesh mesh; List<Vector3> pos; List<Vector2> uv; List<int> indices;` — **no vertex-color list** (color comes from the material's UV atlas, not per-vertex color). `mesh` is `MarkDynamic()`, named `"ElementChunk"`.
  - `public void AddQuad(int x, int y, GroundMasks.UVData uvs)` (line 106): appends one cell quad — verts at `(x-0.5, y-0.5, 0)`, `(x+0.5, y-0.5, 0)`, `(x-0.5, y+0.5, 0)`, `(x+0.5, y+0.5, 0)`; index pattern `(n, n+1, n+3, n, n+3, n+2)`; 4 UVs.
  - `public void Render(Vector3 position, int layer)` (line 125): `Graphics.DrawMesh(mesh, position, Quaternion.identity, material, layer, null, 0, null, ShadowCastingMode.Off, receiveShadows: false, null, useLightProbes: false)`.
- Nested `private struct WorldChunk` (line ~170): `int x, y; List<ElementChunk> elementChunks;` + a per-element `Dictionary<SimHashes, bool> dirty`.
  - `public void Rebuild(GroundMasks.BiomeMaskData[] biomeMasks, Dictionary<SimHashes, Materials> materials)` (line ~304) is the **per-chunk build**: iterates the 16×16 cells; for each of the 2 `biomeChecks` it computes a 4-bit mask of solid neighbors around the cell; if the mask `> 0` it picks a UV variation and calls `AddQuad` on the `opaque` `RenderData` (fully enclosed) or `alpha` `RenderData` (edge). Then `Build()` flushes `pos/uv/indices` into each `RenderData.mesh`.

**Where a vacuum cell falls out**
- The per-element solid predicate is `biomeChecks[0].elementCheck = (Element e, int cell) => (cell == Grid.InvalidCell || Grid.RenderedByWorld[cell]) && e.IsSolid`. **Vacuum's `Element.IsSolid == false` → never passes → no quad is ever emitted for it.** The backwall check additionally excludes `SimHashes.Vacuum`. So vacuum cells produce **no geometry**; the black void is the world background/shader, not per-cell geometry. This is the "fall-out point": the `e.IsSolid` test inside `WorldChunk.Rebuild`'s neighbor-mask computation.

**Shader / material**
- Chunk materials come from `element.substance.material` (a per-substance Unity `Material`, assigned in the game's Unity assets). **It is not created via `Shader.Find` anywhere in C#, so the exact shader name cannot be pinned statically.** Configured by `InitOpaqueMaterial` / `InitAlphaMaterial` / `InitBackwallMaterial` / `InitBackwallAlphaMaterial` using `element.substance.propertyBlock` + uniforms: `_AlphaTestMap`, `_IsBackwallEdge`, `_SrcAlpha`, `_DstAlpha`, `_ZWrite`, `_ShineMask`, `_ShineColour`; the `propertyBlock` carries `_MainTex`, `_MainTex2`, `_HeightTex2`, `_WorldUVScale`, `_Frequency`, `_ShineColour`, `_ColourTint` (see `Substance.cs:195-215` `RefreshPropertyBlock`).
- Keywords: `OPAQUE`/`ALPHA`, `SHINY`/`MATTE`. Render queues: `RenderQueues.WorldOpaque` (=2000), `RenderQueues.WorldTransparent` (=3500), `RenderQueues.NaturalBackwall`, `RenderQueues.BackwallTransparent` (`RenderQueues.cs`).
- **No per-vertex color support** in these chunk meshes; tinting is via material color uniforms / the substance UV atlas. So we cannot simply recolor an existing per-element chunk mesh to red — a separate material/mesh is required.

**Dirty / rebuild API**
- `public void MarkDirty(int cell)` (line ~615): marks one 16×16 chunk (and edge-adjacent chunks) dirty via `dirtyChunks[i, j] = true`.
- `public void RenderAll()` (line 518) → `Render(vis_min=(0,0), vis_max=(full), forceVisibleRebuild: true)`: rebuilds the entire grid.
- `public void Render(Vector2I vis_min, Vector2I vis_max, bool forceVisibleRebuild)` (line 644): each frame, builds dirty chunks in the given range and `Render(...)`-draws each `RenderData` on the `"World"` layer at `Grid.GetLayerZ(Grid.SceneLayer.Ground)`.
- `private void RebuildDirtyChunks()` (line 627).
- **Callers:** `World.LateUpdate()` (`World.cs:135`) calls `groundRenderer.Render(visibleArea.Min, visibleArea.Max)` every frame; `World.cs:127` calls `RenderAll()` in timelapse; `World.UpdateCellInfo` (`World.cs:100`) calls `groundRenderer.MarkDirty(cell)` for **every solid-substance change**.
- **Instance access:** `World.groundRenderer` is a **public field** (`World.cs:18`); precedent `IridescenceEffect.cs:14` uses `World.Instance.groundRenderer`.

**Feasibility of appending our own vacuum quads inside the chunk build**
- Technically possible via a postfix on `WorldChunk.Rebuild`, but **awkward**: `WorldChunk`/`ElementChunk`/`RenderData` are private nested types with private fields/lists; there is no "Vacuum" key in `elementMaterials` and no spare `RenderData` per chunk to hold red quads. Doing it cleanly needs reflection (`AccessTools`) to reach `elementChunks` and to inject/store an extra `RenderData` per chunk, plus per-frame rebuild wiring. **Conclusion: a separate, self-drawn mesh is cleaner** (chosen in Q5).

### 2. Alternative — TileRenderer / brush approach

Files: `lib_sources/Assembly-CSharp/Rendering.World/TileRenderer.cs` (152 lines), `lib_sources/Assembly-CSharp/Rendering.World/LiquidTileOverlayRenderer.cs` (127 lines).

- `public abstract class TileRenderer : KMonoBehaviour`. Key fields: `Tile[] TileGrid`, `int[] BrushGrid`, `protected int TileGridWidth/Height`, `protected Brush[] Brushes`, `protected Mask[] Masks`, `protected List<Brush> DirtyBrushes`, `protected List<Brush> ActiveBrushes`, `VisibleAreaUpdater`, **`public TextureAtlas Atlas`**. `OnSpawn()` (line 33) builds a `(W+1)×(H+1)` tile grid (4 sub-tiles per tile) and calls `LoadBrushes()`.
- Per-cell state lives in the `Tile` grid; `BrushGrid` maps each of a tile's 4 sub-tiles to a brush index. `public void MarkDirty(int cell)` (line 110) → `VisibleAreaUpdater.UpdateCell(cell)` (visible-area tracking adds/removes tiles as the camera moves: `UpdateInsideView`/`UpdateOutsideView`).
- **Render loop:** `private void LateUpdate()` (line 115) — processes `ClearTiles`/`DirtyTiles`, `VisibleAreaUpdater.Update()`, refreshes `DirtyBrushes`, and renders every `ActiveBrush` (`Brush.Render()` draws its quads into a per-brush mesh).
- `public abstract void MarkDirty(ref Tile tile, Brush[] brush_array, int[] brush_grid)` (line 139) — the subclass decides which brush (if any) to assign to each of the 4 sub-tiles of a tile.
- `LiquidTileOverlayRenderer` (the only built-in subclass): `LoadBrushes()` (line 50) creates one brush per **liquid** substance × 3 `Mask`s, each brush built from `element.substance.material`; `InitAlphaMaterial` (line 70) sets `renderQueue = RenderQueues.BlockTiles + element.substance.idx` and `_Colour = element.substance.colour`. `MarkDirty` (line 109) assigns a brush **only when a liquid sits on a solid** (`RenderLiquid`), i.e. it renders liquid top-edges.

**Viability / limitations of a custom TileRenderer for a vacuum tint**
- It *could* work: one brush with a red material + a full-rect (opaque) `Mask`, and `MarkDirty` assigning it to any vacuum cell. It self-manages visible area + per-cell dirty/refresh in `LateUpdate`.
- **Limitations (factual):**
  1. It is a `KMonoBehaviour` that lives **on the World prefab** → we'd need **prefab injection** (add the component to the world prefab, or inject it at world spawn).
  2. It **requires a `public TextureAtlas Atlas`** (a tile atlas containing the `Mask` tiles) to build `Mask`s and `Brush` meshes — extra asset requirement.
  3. Brushes render on the **BlockTiles layer** (`RenderQueues.BlockTiles` = 4500+, `+ substance.idx`), which is **above ground** (and above objects/creatures) — a higher composite layer than the ground, so the tint would sit on top of world geometry rather than in the void.
  4. The brush/mask system is designed for **bordered/edge tiles**; filling a whole cell uniformly is doable (full-rect mask) but a stretch of the intended mechanism.
- **Assessment: viable but heavier** (prefab + atlas + brush/mask setup, higher render layer) than the self-drawn mesh chosen in Q5.

### 3. Overlay wiring details

Files: `OverlayMenu.cs`, `KIconToggleMenu.cs`, `OverlayScreen.cs`, `OverlayModes.cs` (all in `lib_sources/Assembly-CSharp/`); `Assets` (decompiled from dll); `example_mods/Sgt_Imalas-Oni-Mods/UtilLibs/{InjectionMethods,AssetUtils}.cs`; `example_mods/Sgt_Imalas-Oni-Mods/BlueprintsV2/BlueprintsV2/Patches/SpritePatch.cs`.

**`OverlayToggleInfo`** (`OverlayMenu.cs:38`, `: ToggleInfo`)
- Fields: `public HashedString simView; public string requiredTechItem; public string originalToolTipText;`
- Ctor: `public OverlayToggleInfo(string text, string icon_name, HashedString sim_view, string required_tech_item = "", Action hotKey = Action.NumActions, string tooltip = "", string tooltip_header = "")` → calls base `ToggleInfo(text, icon_name, null, hotKey, tooltip, tooltip_header)`.
- `public bool IsUnlocked()`: `true` when `requiredTechItem` is empty (or `DebugHandler.InstantBuildMode`).

**`ToggleInfo`** (`KIconToggleMenu.cs:10`)
- Fields: `text`, `userData`, `icon` (string), `tooltip`, `tooltipHeader`, `KToggle toggle`, `Action hotKey`, `getTooltipText`, **`Func<Sprite> getSpriteCB`**, `prefabOverride`, `instanceOverride`. Ctor default `hotkey = Action.NumActions`.

**How `KIconToggleMenu` resolves the icon** (`KIconToggleMenu.cs:164-171`, inside `RefreshButtons()`)
- `if (toggleInfo.getSpriteCB != null) kToggle.fgImage.sprite = toggleInfo.getSpriteCB();`
- `else if (toggleInfo.icon != null) kToggle.fgImage.sprite = Assets.GetSprite(toggleInfo.icon);`
- `Assets.GetSprite(HashedString name)` (`Assets.cs:410`) returns the `Assets.Sprites` dict entry, **`null` if missing** → the toggle renders with **no icon (blank)**.
- **=> We can bypass `Assets` entirely** by setting the public `getSpriteCB` field to return our own `Sprite`. This is the preferred path (no asset injection).

**`InjectionMethods.AddSpriteToAssets`** (community helper, NOT in the game)
- Definition: `example_mods/Sgt_Imalas-Oni-Mods/UtilLibs/InjectionMethods.cs:254` → `public static Sprite AddSpriteToAssets(Assets instance, string spriteid, bool overrideExisting = false)` (delegates to `AssetUtils`).
- `AssetUtils.AddSpriteToAssets` (`UtilLibs/AssetUtils.cs:44`): `public static Sprite AddSpriteToAssets(Assets instance, string spriteid, bool overrideExisting = false, TextureWrapMode mode = TextureWrapMode.Repeat)` — loads `<ModPath>/assets/<spriteid>.png`, `Sprite.Create(...)`, sets `sprite.name = spriteid`, and **appends to `instance.SpriteAssets`** (`List<Sprite>`). The `Assets.Sprites` (`public static Dictionary<HashedString, Sprite>`, `Assets.cs:95`) is populated from `SpriteAssets` during `Assets.OnPrefabInit`.
- **BlueprintsV2 hook** (`.../BlueprintsV2/Patches/SpritePatch.cs`): `[HarmonyPatch(typeof(Assets), "OnPrefabInit")]` with `public static void Prefix(Assets __instance)` (`HarmonyPriority.LowerThanNormal`) — calls `InjectionMethods.AddSpriteToAssets(__instance, "<name>")` **before** `OnPrefabInit` builds the `Sprites` dict.

**Built-in `overlay_*` icon sizes**
- The built-in `overlay_*` sprites live in the game's UI atlas (AssetBundle); their exact pixel size **cannot be determined statically** from the decompiled C# or game data (they're only referenced by name via `Assets.GetSprite(...)`).
- However `KToggle.fgImage` is a UI `Image` that **stretches the sprite to fill its `RectTransform`**, so the sprite's pixel size does **not** drive layout — any square PNG works.
- **Recommended default: 48×48 PNG**, muted-red "space/void" glyph, shipped in `ModAssets/`, loaded via `PUIUtils.LoadSpriteFile`. (Layout is governed by the KToggle fgImage rect, not the sprite pixels.)

**Hotkeys**
- `OverlayMenu.InitializeToggles()` (`OverlayMenu.cs:139`) adds 14 unconditional toggles using `Action.Overlay1..Overlay14`, then adds **Radiation** (`OverlayMenu.cs:160`) using `Action.Overlay15` — so **Overlay1..Overlay15 are all consumed** (14 always + radiation conditional).
- `KIconToggleMenu.OnKeyDown` (`KIconToggleMenu.cs:237`) does `if (hotKey == Action.NumActions || !e.TryConsume(hotKey)) continue;` — so a 16th entry can safely use the default **`Action.NumActions`** ("no hotkey"), which is skipped.
- **Decision: the new toggle uses `Action.NumActions`** (mouse-only, no key). `Action.Overlay16` is not wired to an input binding (and may not exist) — do not use it.

### 4. Toggle mechanics

- **Per-frame draw hook:** `OverlayScreen.LateUpdate()` (`OverlayScreen.cs:177`) calls `currentModeInfo.mode.Update()` **every frame** while that mode is the active mode → `SpaceOverlayMode.Update()` is the per-frame callback (same mechanism every built-in overlay uses, e.g. `Temperature.Update`).
- **Registration / dispatch:**
  - `OverlayScreen.RegisterModes()` (`OverlayScreen.cs:143`, private; called from `OnSpawn:139`) clears `modeInfos` and registers the 19 built-in modes into `modeInfos` (`Dictionary<HashedString, ModeInfo>`).
  - `OverlayScreen.ToggleOverlay(HashedString newMode, bool allowSound = true)` (`OverlayScreen.cs:182`): `currentModeInfo.mode.Disable()` → `SimDebugView.Instance.SetMode(newMode)` → `modeInfos.TryGetValue(newMode, out currentModeInfo)` (**falls back to `OverlayModes.None` if our mode is not registered**) → `currentModeInfo.mode.Enable()`.
  - **=> our mode MUST be registered (postfix `RegisterModes`)** or toggling silently does nothing.
- **World access / lifecycle:** `World.Instance` (`static`, `World.cs:26`), `World.groundRenderer` (public field, `World.cs:18`), `World.OnSolidChanged` (`public Action<int>`, `World.cs:10`), `World.OnLiquidChanged` (`World.cs:12`).
- **Cell change → ground dirty:** `World.UpdateCellInfo` (`World.cs:63`) recomputes `Grid.RenderedByWorld[cell]` and calls `groundRenderer.MarkDirty(cell)` (`World.cs:100`) for **every solid-substance change**, and fires `OnSolidChanged(cell)` (`World.cs:78`). This is the natural signal that the vacuum set changed (digging/building).
- **World-load / new-world event:** `ClusterManager.SetActiveWorld` (`ClusterManager.cs:307`) triggers `Game.Instance.Trigger(1983128072, ...)`; `GameHashes.ActiveWorldChanged = 1983128072` (`GameHashes.cs:441`). Precedent: `AllResourcesScreen.cs:164` subscribes `Game.Instance.Subscribe(1983128072, Populate)`. **=> subscribe `Game.Instance` to `1983128072` (ActiveWorldChanged)** to rebuild the tint per world and re-apply the enabled state after a load. (The `Mode` base also offers `RegisterSaveLoadListeners()` / `OnSaveLoadRootRegistered/Unregistered` for save-specific handling.)
- **Vacuum detection (Grid statics):** `Grid.WidthInCells`/`HeightInCells` (`Grid.cs:618`/`620`), `Grid.Element` (`public static Element[]`, `Grid.cs:732`), `Grid.IsValidCell` (`Grid.cs:1366`), `Grid.XYToCell` (`Grid.cs:1457`), `Grid.GetLayerZ(SceneLayer)` (`Grid.cs:1571`), `Grid.SceneLayer.Ground` (`Grid.cs:604`). A cell is vacuum iff `Grid.IsValidCell(c) && Grid.Element[c].id == SimHashes.Vacuum` (`SimHashes.Vacuum = 758759285`, `SimHashes.cs:212`). (`Grid.RenderedByWorld` is a custom `VisFlagsRenderedByWorldIndexer`, not a `bool[]` — irrelevant for vacuum, since vacuum is never "rendered by world".)
- **Rebuild-all (if ever needed to force the game's ground):** `GroundRenderer.RenderAll()` (public) / `Render(min, max, forceVisibleRebuild: true)` / `MarkDirty(cell)`.

**Enable / Disable / Update flow (final)**
- `Enable()`: `enabled = true; needsRebuild = true; EnsureAssets(); subscribe Game.Instance to 1983128072`.
- `Update()` (per frame): guard `enabled && World.Instance != null && World.Instance.groundRenderer != null`; if `needsRebuild` → `RebuildMesh()`; then `Graphics.DrawMesh(...)` the vacuum mesh at the ground layer.
- `Disable()`: `enabled = false;` unsubscribe (leave mesh cached for cheap re-enable).
- `OnActiveWorldChanged`: if `enabled` → `needsRebuild = true` (and re-activate the mode if it was reset to `None` after the load).
- Live cell-sync: a **postfix on `GroundRenderer.MarkDirty`** sets a static "dirty" flag; `Update()` converts it to `needsRebuild = true` so newly dug/built vacuum is tinted next frame (avoids patching the private chunk builder).

### 5. Final patch design (decision)

**Chosen mechanism: (a) self-drawn full-grid vacuum mesh**, drawn with `Graphics.DrawMesh` inside `SpaceOverlayMode.Update()`.

**Why (facts):**
- (b) TileRenderer needs World-**prefab injection** + a `TextureAtlas` + brush/mask setup and renders on the higher **BlockTiles** layer.
- Patching GroundRenderer's private `WorldChunk.Rebuild` to append quads needs **reflection into private nested types** and has no per-chunk slot to hold extra red quads.
- A self-drawn mesh is minimal (no prefab, no atlas), a **single draw call**, auto-syncs via the `MarkDirty` flag, and re-applies via the `ActiveWorldChanged` event. It renders in the `"World"` layer at the ground z, compositing over the black void.

**New type**
```
SpaceOverlayMode : OverlayModes.Mode
  public static readonly HashedString ID        = "SpaceOverlay";
  public static readonly Color      SpaceTint   = new Color(0.55f, 0.16f, 0.16f, 0.28f); // muted translucent red (tunable)
  // fields: Mesh mesh; Material material; Texture2D whiteTex; bool enabled; bool needsRebuild; Sprite icon;
  override HashedString ViewMode()       => ID;
  override string     GetSoundName()     => "Off";
  override void  Enable()  { enabled = true; needsRebuild = true; EnsureAssets(); SubscribeWorldChanged(); }
  override void  Disable(){ enabled = false; UnsubscribeWorldChanged(); }
  override void  Update() { if (!enabled || World.Instance == null || World.Instance.groundRenderer == null) return;
                            if (needsRebuild) RebuildMesh();
                            DrawMesh(); }
  RebuildMesh();     // walk grid, one quad per vacuum cell, mesh.SetVertices/SetIndices, needsRebuild=false
  DrawMesh();        // Graphics.DrawMesh at Grid.GetLayerZ(Grid.SceneLayer.Ground) on the "World" layer
  OnActiveWorldChanged(object data); // if enabled -> needsRebuild = true (re-activate if reset to None)
```

**Material / shader strategy**
- `material = new Material(Shader.Find("Sprites/Default"))` (built-in, always present), white 1×1 `_MainTex`, `material.color = SpaceTint`, `material.renderQueue = RenderQueues.WorldTransparent` (=3500).
- Draw: `Graphics.DrawMesh(mesh, new Vector3(0, 0, Grid.GetLayerZ(Grid.SceneLayer.Ground) + 0.0005f), Quaternion.identity, material, LayerMask.NameToLayer("World"), null, 0, null, ShadowCastingMode.Off, receiveShadows: false, null, useLightProbes: false)`.
- **Fallback:** if `Sprites/Default` does not composite over the void as expected, use a minimal unlit alpha shader, or clone a ground alpha material (`new Material(element.substance.material)`) and set a flat color.
- **Muted-red RGBA constant** lives once in `SpaceOverlayMode.SpaceTint` = `(0.55, 0.16, 0.16, 0.28)` — tunable.

**Concrete Harmony patch list**

| # | Target | Kind | Signature / args | Action |
|---|--------|------|------------------|--------|
| 1 | `OverlayScreen.RegisterModes` (`private void RegisterModes()`) | **postfix** | `(OverlayScreen __instance)` | `AccessTools.Method<OverlayScreen, OverlayModes.Mode>("RegisterMode").Invoke(__instance, new object[]{ new SpaceOverlayMode() });` |
| 2 | `OverlayMenu.InitializeToggles` (`private void InitializeToggles()`) | **postfix** | `(OverlayMenu __instance)` | build `OverlayToggleInfo info = new OverlayToggleInfo("Space", "overlay_space", SpaceOverlayMode.ID, "", Action.NumActions, <tooltip>, <header>);`; set `info.getSpriteCB = () => Icon;`; add to private list via `AccessTools.Field<OverlayMenu, List<ToggleInfo>>("overlayToggleInfos").Value.Add(info)`. |
| 3 | `GroundRenderer.MarkDirty` (`public void MarkDirty(int cell)`) | **postfix** | `(GroundRenderer __instance, int cell)` | set static `SpaceOverlayOverlayState.dirty = true;` (live cell-sync; `Update()` turns it into `needsRebuild`). |

Notes:
- `InitializeToggles` runs in `OverlayMenu.OnPrefabInit` **before** `Setup(overlayToggleInfos)`, so a postfix of it adds the entry **before** the `KToggle` buttons are built → it gets its button + icon (via `getSpriteCB`).
- The `RegisterModes` postfix runs after the 19 built-ins; `currentModeInfo` is assigned to `None` after `RegisterModes` returns, so no conflict. Re-spawns of `OverlayScreen` re-run both postfixes (idempotent add of one mode / one toggle each time).

**Overlay registration flow (end to end)**
1. Mod `UserMod2.OnLoad` → `new Harmony(id).Patch(...)`: apply patches #1, #2, #3.
2. `RegisterModes` postfix registers `SpaceOverlayMode` → `OverlayScreen.ToggleOverlay("SpaceOverlay")` dispatches `Enable/Disable/Update`.
3. `InitializeToggles` postfix adds the menu entry (icon `overlay_space` via `getSpriteCB`; hotkey `Action.NumActions`).
4. User clicks the toggle → `OverlayMenu.OnToggleSelect` → `OverlayScreen.Instance.ToggleOverlay(SpaceOverlayMode.ID)` → `Enable()` → per-frame `Update()` builds (once) + draws the red vacuum mesh.
5. `ActiveWorldChanged` (Game event `1983128072`) re-applies the tint per world and re-activates if still enabled.

**Icon**
- Name: `overlay_space`. Size: **48×48** PNG, muted-red void glyph (e.g. a dark rounded square with a small red dot/planet). Shipped in `ModAssets/`.
- Loaded at runtime via `PUIUtils.LoadSpriteFile(<modDir>/overlay_space.png)` (null on fail) and exposed through `ToggleInfo.getSpriteCB` — **no `Assets`/`InjectionMethods` injection required**. (The `InjectionMethods.AddSpriteToAssets` + `Assets.OnPrefabInit` prefix path is a documented alternative if we ever need the sprite in `Assets.GetSprite` for other consumers.)

**Hotkey handling**
- Built-ins consume `Action.Overlay1..Overlay15`. The new 16th toggle uses **`Action.NumActions`** (no hotkey, safely skipped by `KIconToggleMenu.OnKeyDown`); mouse-only.

### Deviations during Stage 3

All deviations below are compile/API-shape corrections confirmed against the decompiled game + 0Harmony; none change the designed mechanism.

1. **`GameHashes` is an enum, not a static class of ints.** `GameHashes.ActiveWorldChanged` is an enum member (`public enum GameHashes { ... ActiveWorldChanged = 1983128072 ... }`), so the event id constant needs an explicit cast: `public static readonly int ActiveWorldChangedEventId = (int)GameHashes.ActiveWorldChanged;`.
2. **`Graphics.DrawMesh` 12-arg overload verified exactly as designed**, but `ShadowCastingMode` lives in the **`UnityEngine.Rendering`** namespace — a `using UnityEngine.Rendering;` is required (it is not in `UnityEngine`).
3. **`Mesh.SetIndices(List<int>, MeshTopology)` has no 2-arg overload** in this Unity build; `submesh` is a required parameter: `SetIndices(quads, MeshTopology.Quads, 0)`.
4. **The game's 0Harmony v2 has no generic `AccessTools.Field<T,TField>` / `Method<T,TO>` overloads.** Only `FieldInfo AccessTools.Field(Type, string)` and `MethodInfo AccessTools.Method(Type, string)` exist. The toggle registration therefore uses `FieldInfo.GetValue/SetValue` (with null-guards + `PUtil.LogWarning`) instead of the accessor style from the design sketch.
5. **Icon runtime path.** The task spec said load `Path.Combine(modPath, "ModAssets", "overlay_space.png")`, but this repo's `CopyModsToDevFolder` copies `ModAssets/*` **flat** into the mod-folder root (`%(RecursiveDir)` is empty for direct children — verified in the build output: `.tmp/build_mod_dir/SpaceOverlay_dev/overlay_space.png`). This matches the Stage-1 finding ("a PNG placed in `ModAssets/` ends up at `<modfolder>/...`"). `LoadIcon` therefore tries `<modFolder>/overlay_space.png` first and falls back to `<modFolder>/ModAssets/overlay_space.png` (for manually copied mod folders); both miss → `PUtil.LogWarning`, registration continues.
6. **`Dirty` flag placement.** The design sketch mentioned a separate `SpaceOverlayOverlayState` type; per the Stage-3 spec the flag is a static field on the mode class itself: `public static bool SpaceOverlayMode.Dirty`.
7. **Toggle button label** uses `STRINGS.MISC.STATUSITEMS.SPACE.NAME` ("Space exposure") for the label and tooltip header, and `STRINGS.MISC.STATUSITEMS.SPACE.TOOLTIP` for the tooltip (localized strings; allowed by the design).
8. **Icon constant naming.** The tint constant is named `SpaceOverlayMode.Tint` (design called it `SpaceTint`); same value `(0.55, 0.16, 0.16, 0.28)`.

### Stage 4 review findings

Independent audit of the Stage-3 implementation against the decompiled game sources (`lib_sources/`) and the Stage-1 design. Verdicts: 1 PASS, 2 FIXED, 3 PASS (+hardened), 4 FIXED, 5 PASS, 6 PASS, 7 PASS.

**1. Cell iteration & quad geometry — PASS (no change).**
- `cell < Grid.CellCount` is exactly W*H (`GridSettings.Set`: `Grid.CellCount = width * height`; WorldGen asserts the same); `Grid.IsValidCell` is bounds-only.
- `x = cell % width`, `y = cell / width` matches `Grid.CellColumn`/`CellRow`/`XYToCell`.
- The corner-based mapping (cell (x,y) spans world `[x, x+1] × [y, y+1]`) is canonical, confirmed by independent anchors: `Grid.PosToCell` truncation, `Grid.CellToPos`, `CellSelectionObject` (cursor = `CellToPos(cell) + (0.5, 0.5)`), and decisively `Rendering.BlockTileRenderer.AddVertexInfo` (vertices at (x,y) and (x+1,y+1)). `GroundRenderer.AddQuad` centering its quads on (x,y) is a one-off anomaly inside that single renderer; the tint follows the canonical cell bounds.

**2. Z / layer / draw call — FIXED (doubled z).**
- Before: `RebuildMesh` baked `z = Grid.GetLayerZ(Ground) + MeshZOffset` (≈ −29.4995) into every vertex, and `Draw()` passed the same z again as the DrawMesh position → final world z ≈ **−58.999**, i.e. ~29.5 m below the ground plane (ground plane at −29.5, block tiles at −30.5). The overlay rendered far below everything.
- After: vertices are in local space (z = 0) — the same convention the game's own `GroundRenderer.AddQuad` uses; the layer z is applied once, via the DrawMesh position in `Draw()`. Single source of truth; final z = −29.4995, just above the ground plane.
- 12-arg `Graphics.DrawMesh` overload and layer `"World"` match the game's own usage.

**3. Material — PASS (+hardened: assets now static).**
- `Shader.Find("Sprites/Default")` null-guarded with `LogWarning`; material created once, color/queue set once.
- Hardening: mesh + material are now **static** (one per session) instead of per-instance — a per-instance asset would be orphaned on every save load (new OverlayScreen/mode instance per level; the old managed instance is GC'd but its native `Mesh`/`Material` objects are never `Destroy`'d → native leak per load). Static assets are rebuilt (`SetVertices`) for the new grid and reused — zero leaks.

**4. Lifecycle — FIXED (re-activation, unguarded `ToggleOverlay`, orphan instances).**
- Before: the `ActiveWorldChanged` handler called `screen.ToggleOverlay(ID)` unguarded — `ToggleOverlay` derefs `currentModeInfo.mode` (NRE around level transitions), `ManagementMenu.Instance`, `SimDebugView.Instance` — and the re-activation path was dead code: the event fires only on in-cluster world switches (`ClusterManager.SetActiveWorld`), **not** on save loads (`activeWorldIdx` is `[Serialize]`-restored directly), while on in-cluster switches the mode is already current (`GetMode() == ID`). Orphaned old instances were never `Disable()`'d (`enabled` stayed true; if the Game's `EventSystem` survived a load, orphan handlers could re-enable the overlay without a user request).
- After:
  - new static `SpaceOverlayMode.Active` (desired state): `Enable()` → true, `Disable()` → false;
  - the event handler is now exception-free: `if (!Active) return; Dirty = true;` — no screen calls (the current instance's `Update` consumes the flag);
  - **new patch**: postfix on `OverlayScreen.OnSpawn` (declared/overridden on `OverlayScreen` itself, so resolvable by `UtilLibs.PatchUtil`'s `DeclaredOnly` lookup): after the screen has finished spawning (`currentModeInfo` set to `None`, so `ToggleOverlay` is safe — it is **not** safe during `RegisterModes`), `if (Active && GetMode() != ID) ToggleOverlay(ID)` wrapped in try/catch + `LogExcWarn` (for the `ManagementMenu.Instance`/`SimDebugView.Instance` null window). This deterministically re-activates the overlay after save loads.
  - No recursion: OnSpawn postfix → `ToggleOverlay` → `Enable` → `Subscribe` only.
- Subscriptions: per-instance `Game.Instance?.Subscribe/Unsubscribe`; `EventSystem.Unsubscribe` is a silent no-op when the handler is absent, and the Game object (and its `EventSystem`) is per-level, so old subscriptions die with the old scene — no stacking. In the theoretical case where they did survive, the handler is now idempotent (only sets `Dirty`), so no behavioral bug.

**5. Rebuild triggers — PASS.**
- `needsRebuild` on `Enable`; static `Dirty` (from the `MarkDirty` postfix and the world-change handler) is consumed exactly once by the current instance's `Update` (cleared on read); no per-frame rebuild — `Draw()` only. `Grid.CellCount <= 0` guards pre-grid frames.

**6. Registration — PASS (verified against decompiled sources).**
- Patches (3 → 4 with the OnSpawn postfix) attached once in `OnLoad` via `PatchUtil.TryPatch` (resolves private methods with `DeclaredOnly`; `OnSpawn` is an override declared on `OverlayScreen` itself → resolvable).
- `OverlayToggleInfo` ctor parameter order `(string text, string icon_name, HashedString sim_view, string required_tech_item, Action hotKey, string tooltip, string tooltip_header)` matches the reflection call exactly.
- Private `overlayToggleInfos` field confirmed; `RefreshButtons()` **hard-casts every entry to `OverlayToggleInfo`** (on every menu `OnSpawn`/`Refresh`/research complete) — the injected element is a genuine `OverlayToggleInfo` (nested private-type ctor), so the cast holds.
- `getSpriteCB` confirmed as a public `Func<Sprite>` field on `KIconToggleMenu.ToggleInfo`, consumed in `KIconToggleMenu.RefreshButtons` (`kToggle.fgImage.sprite = toggleInfo.getSpriteCB()`); `GetIcon` is idempotent (cached static sprite), so repeated invocations are safe.
- `STRINGS.MISC.STATUSITEMS.SPACE` exists in the game's STRINGS (NAME "Space exposure").
- `OnToggleSelect` → `OverlayScreen.Instance.ToggleOverlay(simView)` and `OnOverlayChanged` → `toggle.isOn` both work for the injected entry (the toggle is assigned via `SetToggle` in `RefreshButtons`).

**7. Style — PASS.**
- Constants centralized in `SpaceOverlayMode`; logging via `PUtil` (mod name auto-prefixed — no repetition); verbose logs in `#if DEBUG`; no `Console.WriteLine`; no per-frame logging.

**Runtime risks (not verifiable from sources; confirm in-game)**
1. **`Sprites/Default` shader availability** — if the game build renames/moves that shader, `Shader.Find` returns null, a warning is logged, and the overlay silently stops drawing.
2. **Camera culling of layer "World"** — the tint shares the ground's layer; if the camera far-clip culls it at distance, the tint disappears at distance (behaves identically to the ground itself, so expected fine).
3. **Null window in the OnSpawn postfix** — if `ManagementMenu.Instance`/`SimDebugView.Instance` are not yet ready when `OverlayScreen.OnSpawn` runs, the try/catch logs the failure and the overlay stays off until the user toggles it manually (`Active` stays true, so a later level load retries).
4. **Icon load timing** — `getSpriteCB` is invoked during `RefreshButtons` (menu `OnSpawn`/every refresh); a missing PNG leaves a blank icon (warning logged at `OnLoad`).

**Build:** `NUGET_PACKAGES=… dotnet build ONI-mods.sln -c Debug` → **0 errors** (30 pre-existing warnings from other mods).

### Stage 6 — in-game bugfix findings

**Reported symptom:** enabling the overlay darkened the whole view but no red was visible; the log additionally flooded `ViewMode 0xBFF8590A has no StatusItemOverlay value` every frame.

**Diagnosis (verified against decompiled sources):**

1. **Tint too dark for the background it is drawn over.** Vacuum cells are rendered over the black space background, so the effective colour is ≈ (0.154, 0.045, 0.045) with the old `Tint = (0.55, 0.16, 0.16, 0.28)` — the mesh *was* drawn; the "darkening" the user saw was the mesh itself, with an imperceptibly red tint.
2. **Not using the game's standard coloured overlay rendering.** Built-in overlay modes (e.g. `OverlayModes.Disease`, `OverlayModes.cs` ~line 785) and reference mods (`PipPlantOverlay`) do in `Enable()`: `CameraController.Instance.ToggleColouredOverlayView(true)` and `Camera.main.cullingMask |= LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG")`, and the inverse in `Disable()`. This is what mutes the world and makes overlay colours stand out. We skipped all of it, so the tint was a dim layer on top of the unmuted world.
3. **Warning flood.** `SelectToolHoverTextCard.ShowStatusItemInCurrentOverlay` (~line 850) calls `StatusItem.GetStatusItemOverlayBySimViewMode(mode)` per hover frame; our mode id was missing from the private static `Dictionary<HashedString, StatusItemOverlays> overlayBitfieldMap` (`StatusItem.cs` line 83) → `Debug.LogWarning` per frame. The game itself maps simple visual modes to `StatusItemOverlays.None` (`OverlayModes.Oxygen.ID`, `OverlayModes.TileMode.ID`).

**Changes:**

- `SpaceOverlay/SpaceOverlayMode.cs`:
  - `Tint` → `new Color(1.0f, 0.25f, 0.25f, 0.4f)` — clearly red, still muted; single place for the value (the constant).
  - New `public static readonly int OverlayLayer = LayerMask.NameToLayer("MaskedOverlay")`; mesh is drawn on this layer instead of `World` (the `WorldLayer` constant is removed). Z logic unchanged: `Grid.GetLayerZ(Grid.SceneLayer.Ground) + MeshZOffset`.
  - New `public static readonly int CameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG")`.
  - `Enable()`: after the existing logic — `CameraController.Instance.ToggleColouredOverlayView(true)`, then `Camera.main.cullingMask |= CameraLayerMask` (null-guarded).
  - `Disable()`: `Camera.main.cullingMask &= ~CameraLayerMask` (null-guarded), then `ToggleColouredOverlayView(false)` — the inverse of the game's own modes.
  - All existing behaviour kept: lazy mesh rebuild, `Dirty` sync via the `MarkDirty` postfix, `Active`/OnSpawn re-activation, icon.
- `SpaceOverlay/Mod.cs` (`OnLoad`, after the patches): reflect into `StatusItem.overlayBitfieldMap` via `AccessTools.Field` (0Harmony v2 has no generic overloads — same limitation hit in Stage 3) and set `map[SpaceOverlayMode.ID] = StatusItem.StatusItemOverlays.None`, killing the per-frame hover warning. Missing field → warning; any other failure → warning + `LogExcWarn`.

**Assumptions / deviations from the task sketch:**

- `StatusItemOverlays` is nested in `StatusItem` (`StatusItem.cs` line 16) → referenced as `StatusItem.StatusItemOverlays`.
- `PUtil.LogExcWarn(ex, msg)` does not exist in PLib 4.19 (single-arg only) → replaced by `LogWarning("failed to register status-item overlay mapping")` + `LogExcWarn(ex)`.
- `CameraController.Instance` is dereferenced unguarded, matching every reference mode (non-null in-world).
- `Camera.main` null-guarded on both `cullingMask` writes (menu/scene edges) — a deviation from PipPlantOverlay's style only in the extra guard; behaviour identical when the camera exists.
- DrawMesh layer changed from `World` to `MaskedOverlay`: the mesh is only *visible* while the overlay is enabled (the camera gains the overlay layers in `Enable()`), which is the intended visibility window.

**Build:** `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` → **0 errors** (26 pre-existing warnings from other mods). In-game acceptance is pending — the user must verify the red is visible, the world mutes like other overlays, and the log is clean.


## Rewriting to the built-in overlay mechanism (2026-09-23 user request)

### 0. Path note
- `lib_sources/` is NOT inside this workspace — it lives at `/home/apkawa/code/ONI_MODS/lib_sources/` (sibling of `Apkawa_ONI_Mods`). All game-source references below use that root.
- No decompilation was needed: every referenced game class exists in the existing decompiled tree.

### 1. `class Mode` — `lib_sources/Assembly-CSharp/OverlayModes.cs` lines 1880–2257 (inside `public abstract class OverlayModes`)
Exact member inventory of `Mode` itself:
- `public ToolParameterMenu.ToggleData[] legendFilters;` — **public** field, settable from subclasses (PipPlantOverlay pokes it via reflection in `InitDefaultFilters()`).
- `public abstract HashedString ViewMode();`
- `public virtual void Enable() {}` — **empty** base.
- `public virtual void Update() {}` — **empty** base (no desaturation, no drawing — modes do everything themselves).
- `public virtual void Disable() {}` — **empty** base.
- `public virtual void DisableOverlay() {}` — additional empty hook.
- `public abstract string GetSoundName();` — consumed by `OverlayScreen.UpdateOverlaySounds()` → `GlobalAssets.GetSound(name)` + `KMonoBehaviour.PlaySound`; `"Off"` = silence (see `OverlayModes.None`).
- `public virtual List<LegendEntry> GetCustomLegendData() { return null; }` — consumed by `OverlayLegend.SetLegend(OverlayModes.Mode mode, bool refreshing = false)` (called from `OverlayScreen.ActivateLegend()` on every toggle).
- `public virtual ToolParameterMenu.ToggleData[] CreateDefaultFilters() { return null; }`
- `public virtual void OnFiltersChanged() {}`
- `protected bool InFilter(string layer, ToolParameterMenu.ToggleData[] filter);` — matches `ToolParameterMenu.FILTERLAYERS.ALL` first, then the layer name.
- `public static void Clear();` — clears the shared `workingTargets` list; called from `OverlayScreen.OnLoadLevel`.
- Per-**object** highlight helpers (these tint *objects*, not cells):
  - `public void RegisterSaveLoadListeners();` / `public void UnregisterSaveLoadListeners();` — subscribe `OnSaveLoadRootRegistered/Unregistered(SaveLoadRoot)` (both `protected virtual`) to `SaveLoader.Instance.saveManager.onRegister/onUnregister`.
  - `protected void ProcessExistingSaveLoadRoots();`
  - `protected static UniformGrid<T> PopulatePartition<T>(ICollection<Tag> tags) where T : IUniformGridObject;`
  - `protected static void ResetDisplayValues<T>(ICollection<T> targets) where T : MonoBehaviour;` and overload `protected static void ResetDisplayValues(KBatchedAnimController)` (sets `SetLayer(0)`, `HighlightColour = Color.clear`, `TintColour = Color.white`, then `SetLayer(KPrefabID.defaultLayer)`).
  - `protected static void RemoveOffscreenTargets<T>(ICollection<T> targets, Vector2I min, Vector2I max, Action<T> on_removed = null) where T : KMonoBehaviour;` (+ a `IUniformGridObject` overload with a working-targets list).
  - `protected static void ClearOutsideViewObjects<T>(...) where T : KMonoBehaviour / IUniformGridObject` (two overloads; culls by `Grid.PosToXY` against visible extents + `ClusterManager.Instance.activeWorldId`).
  - `protected static float GetDefaultDepth(KMonoBehaviour cmp);` — `BuildingComplete.Def.SceneLayer` → `Grid.GetLayerZ(...)`, else `Grid.SceneLayer.Creatures`.
  - `protected void UpdateHighlightTypeOverlay<T>(Vector2I min, Vector2I max, ICollection<T> targets, ICollection<Tag> item_ids, ColorHighlightCondition[] highlights, BringToFrontLayerSetting bringToFrontSetting, int layer) where T : KMonoBehaviour;` — per frame, per visible target: picks first condition where `highlight_condition(target)` is true, sets `KBAC.HighlightColour = highlight_color(target)`, and `KBAC.SetLayer(layer)` (Constant) / `SetLayer(0 or layer)` per `BringToFrontLayerSetting`; skips cells failing `Grid.IsValidCell && Grid.IsVisible && min<=pos<=max`. No KAnim bring-to-front call, no TintColour write in this method.
  - `protected void DisableHighlightTypeOverlay<T>(ICollection<T> targets);` — `HighlightColour = Color.clear`, `SetLayer(0)`, clears the collection.
  - `protected void AddTargetIfVisible<T>(T instance, Vector2I vis_min, Vector2I vis_max, ICollection<T> targets, int layer, Action<T> on_added = null, Func<KMonoBehaviour, bool> should_add = null) where T : IUniformGridObject;` — **fog-of-war aware**: adds target only if some covered cell has `Grid.Visible[num] > 20 && Grid.WorldIdx[num] == ClusterManager.Instance.activeWorldId` **or** `!PropertyTextures.IsFogOfWarEnabled`; sets `KBAC.SetLayer(layer)` on add.
- Nested types (file-level, inside `OverlayModes`):
  - `public class ColorHighlightCondition { public Func<KMonoBehaviour, Color> highlight_color; public Func<KMonoBehaviour, bool> highlight_condition; public ColorHighlightCondition(Func<KMonoBehaviour,Color>, Func<KMonoBehaviour,bool>); }` (a **class**, not struct; no `None` member).
  - `public enum BringToFrontLayerSetting { None, Constant, Conditional }`.
  - `public class ModeUtil { public static float GetHighlightScale(); }`.
- **Negative fact (important):** `Mode` has **no per-cell rendering hook of any kind** — no mesh/quad API, no calls into `SimDebugView`/`PropertyTextures`. Per-cell rendering lives entirely in `SimDebugView` (section 3). The base hooks are empty; world desaturation (`CameraController.ToggleColouredOverlayView`) is invoked by individual modes themselves (Power Enable/Disable, Temperature.OnFiltersChanged, PipPlantOverlay Enable/Disable), not by the framework.
- `OverlayModes.None.ID == HashedString.Invalid`; built-in minimal cell-only mode example: `public class Oxygen : Mode` (lines 1759–1786) — only `ViewMode()`, `GetSoundName() => "Oxygen"`, `Enable()` = `SelectTool.Instance.SetLayerMask(GetDefaultLayerMask() | LayerMask.GetMask("MaskedOverlay"))`, `Disable()` = `SelectTool.Instance.ClearLayerMask()`. Everything else (the per-cell colour itself) comes from SimDebugView.

### 2. What a Mode gets for free (surrounding classes)
- `OverlayScreen` (`lib_sources/Assembly-CSharp/OverlayScreen.cs`, 256 lines):
  - `protected override void OnSpawn()` → `RegisterModes()` (private; registers None, Oxygen, Power, Temperature, ThermalConductivity, Light, LiquidConduits, GasConduits, Decor, Disease, Crop, Harvest, Priorities, HeatFlow, Rooms, Suit, Logic, SolidConveyor, TileMode, Radiation) then `currentModeInfo = modeInfos[OverlayModes.None.ID]`.
  - `private void RegisterMode(OverlayModes.Mode mode)` → `modeInfos[mode.ViewMode()] = new ModeInfo { mode = mode };` (**private** — mods must patch via reflection/Detour).
  - `public void ToggleOverlay(HashedString newMode, bool allowSound = true)` → `currentModeInfo.mode.Disable()` → **`SimDebugView.Instance.SetMode(newMode)`** → `currentModeInfo.mode.Enable()` → sound (`GetSoundName()`) → `OnOverlayChanged` action → `OverlayLegend.Instance.SetLegend(mode)`.
  - `private void LateUpdate()` → **every frame: `currentModeInfo.mode.Update();`** (and `public void Refresh()` = manual LateUpdate).
  - `public HashedString GetMode();` and `public HashedString mode => currentModeInfo.mode.ViewMode();`
  - `OnLoadLevel()` → `OverlayModes.Mode.Clear()`, `modeInfos = null`, fresh screen per level (this is why the current SpaceOverlay `OnSpawn` re-activation patch exists; `RegisterModes` runs in `OnSpawn` again, so a RegisterModes-postfix re-registers the mode on every level load).
  - `public Action<HashedString> OnOverlayChanged;` — and `SimDebugView.SetMode` fires `Game.Instance.gameObject.BoxingTrigger(1798162660, mode)`, which `OverlayMenu.OnPrefabInit` subscribes to (keeps the menu button in sync).
- `OverlayMenu` (`lib_sources/Assembly-CSharp/OverlayMenu.cs`):
  - `private class OverlayToggleInfo : ToggleInfo` with fields `HashedString simView; string requiredTechItem; string originalToolTipText;` and constructor:
    `public OverlayToggleInfo(string text, string icon_name, HashedString sim_view, string required_tech_item = "", Action hotKey = Action.NumActions, string tooltip = "", string tooltip_header = "")`
  - `private List<ToggleInfo> overlayToggleInfos;` — filled in `InitializeToggles()` (called from `OnPrefabInit`); `RefreshButtons()` **hard-casts each entry to `OverlayToggleInfo`** (so a custom entry must be exactly that private nested type); `IsUnlocked()` gates the button on `requiredTechItem` tech.
  - Built-in entries example: `new OverlayToggleInfo(UI.OVERLAYS.RADIATION.BUTTON, "overlay_radiation", OverlayModes.Radiation.ID, "", Action.Overlay15, UI.TOOLTIPS.RADIATIONOVERLAYSTRING, UI.OVERLAYS.RADIATION.BUTTON)`.
- `OverlayLegend.SetLegend(OverlayModes.Mode mode, bool refreshing = false)` (`lib_sources/Assembly-CSharp/OverlayLegend.cs:203`) — displays `mode.GetCustomLegendData()`; `OverlayLegend.OverlayInfo { infoUnits, isProgrammaticallyPopulated, mode, name }` can be added programmatically (PipPlantOverlay patches `OverlayLegend.OnSpawn` prefix).
- `CameraController.ToggleColouredOverlayView(bool enabled)` (`lib_sources/Assembly-CSharp/CameraController.cs:271`) → `mrt.ToggleColouredOverlayView(enabled)` → `MultipleRenderTarget` — world desaturation; **opt-in per mode**.

### 3. The built-in per-cell pipeline — `SimDebugView` (`lib_sources/Assembly-CSharp/SimDebugView.cs`, 1255 lines)
- `public static SimDebugView Instance;`
- `private Dictionary<HashedString, Func<SimDebugView, int, Color>> getColourFuncs` — **the modder hook**. Pre-populated per overlay ID: `Temperature→GetNormalizedTemperatureColourMode`, `Oxygen→GetOxygenMapColour`, `Light→GetLightColour`, `Radiation→GetRadiationColour`, `Rooms→GetRoomsColour`, `TileMode→GetTileColour`, `GameGrid→GetGameGridColour`, `StateChange→GetStateChangeColour`, … and `Suit/Priorities/Crop/Harvest→GetBlack` (modes without cell colours).
- `private Dictionary<HashedString, Action<SimDebugView, Texture>> dataUpdateFuncs` — per-mode texture setup: `Temperature/Oxygen/Decor→SetDefaultBilinear`, `TileMode→SetDefaultPoint`, `Disease→SetDisease`; fallback `SetDefaultPoint` (Point filter, plane renderer `sharedMaterial = instance.material`, `mainTexture = instance.tex`).
- `OnReset()` builds the render plane: `plane = CreatePlane("SimDebugView", base.transform)` + `tex = CreateTexture(out texBytes, Grid.WidthInCells, Grid.HeightInCells)` (RGBA32, Point filter); plane local **z = -6** (6 m above ground, toward camera); plane object on Unity layer `"SimDebugView"` (`CreatePlane` does `SetLayerRecursively(LayerMask.NameToLayer(layer))`; quad spans `Grid.WidthInMeters × 2*HeightInCells`).
- `private void Update()` — every frame: `plane.SetActive(mode != OverlayModes.None.ID)`; `SimDebugViewCompositor.Instance.Toggle(flag && !GameUtil.IsCapturingTimeLapse())`; when active → **`UpdateData(tex, texBytes, mode, 192)`** — i.e. the per-cell texture is **recomputed every frame while the mode is active; there is no dirty flag anywhere in this path**.
- `public void UpdateData(Texture2D texture, byte[] textureBytes, HashedString viewMode, byte alpha)` — `Grid.GetVisibleExtents`, work items in 16-row bands, `GlobalJobManager.Run(...)` (threaded), then `texture.LoadRawTextureData(textureBytes); texture.Apply();`
- `UpdateSimViewWorkItem.Run` (thread worker): per cell in visible extent → `Color color = value(instance, j);` (looked up in `getColourFuncs[simViewMode]`, fallback `GetBlack`) → RGBA32 bytes; **cells with `!Grid.IsActiveWorld(j)` are zeroed** — active-world bounding is built in. (No explicit fog-of-war check visible here — see open questions.)
- `public void SetMode(HashedString mode);` / `public HashedString GetMode();` — called by `OverlayScreen.ToggleOverlay`; `SetMode` also fires boxing trigger 1798162660 (menu sync).
- `public bool hideFOW;` field exists (fog-of-war related on the sim view).
- `OnPrefabInit` exists (line 450) — **the patch target PipPlantOverlay uses** to add its colour func (private field access via Harmony `___getColourFuncs`).
- Consequence: a mode ID registered in `OverlayScreen.RegisterMode` that also has a `getColourFuncs` entry renders **automatically every frame** while `SimDebugView.GetMode() == that ID` — no mesh, no DrawMesh, no dirty flags, no event subscriptions.

### 4. `PropertyTextures` (for the record — the *ground*-material per-cell path, distinct from SimDebugView)
- `lib_sources/Assembly-CSharp/PropertyTextures.cs` (1007 lines): `public class PropertyTextures : KMonoBehaviour, ISim200ms`, `public static PropertyTextures instance;`, `public static bool IsFogOfWarEnabled => FogOfWarScale < 1f;`, `public enum Property { StateChange, GasPressure, GasColour, GasDanger, FogOfWar, Flow, SolidDigAmount, SolidLiquidGasMass, WorldLight, Liquid, Temperature, ExposedToSunlight, FallingSolid, Radiation, LiquidData, MaterialData, SolidLiquidGasMassForLight, Num }`.
- Per-property textures are updated by private work items (`UpdateTextureThreaded`, `UpdateFogOfWar`, `UpdateGasColour`, …). No public generic "add a per-cell texture" hook visible in the class surface — treat as game-internal. PipPlantOverlay does **not** use it for its cell effect (uses SimDebugView instead).
- `Mode.AddTargetIfVisible` references `PropertyTextures.IsFogOfWarEnabled` — the one cross-link.

### 5. `MaskedOverlay` / `MaskedOverlayBG`
- **Not classes** — Unity layer names only (no `class MaskedOverlay` in the decompiled tree). Used by object/conduit overlay modes: targets' `KBatchedAnimController.SetLayer(LayerMask.NameToLayer("MaskedOverlay"))`, camera `Camera.main.cullingMask |= LayerMask.GetMask("MaskedOverlay","MaskedOverlayBG")`, selection `SelectTool.Instance.SetLayerMask(...)`. Relevant only for per-**object** overlays; the per-cell SimDebugView path needs none of it.

### 6. PipPlantOverlay mod — how it uses the built-in machinery
Files: `PipPlantOverlay/PipPlantOverlay.cs` (321 ln), `PipPlantOverlayPatches.cs` (213 ln), `PipPlantOverlayStrings.cs`, `PipPlantOverlayTests.cs`, `PipPlantOverlay.csproj` (plain SDK csproj; `pip.png` as `<EmbeddedResource>`), assets.
- Mode: `public class PipPlantOverlay : OverlayModes.Mode` with `public static readonly HashedString ID = new HashedString("PIPPLANT");` and `internal static PipPlantOverlay Instance { get; private set; }`.
- **Per-cell rendering — the built-in mechanism:**
```csharp
internal static Color GetColor(SimDebugView _, int cell) {
    ... var reason = Instance.cells[cell];
    switch (reason) { case CanPlant: shade = colors.cropGrown; ... }
    return shade;
}
// PipPlantOverlayPatches.cs:
[HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
internal static void Postfix(IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs) {
    ___getColourFuncs[PipPlantOverlay.ID] = PipPlantOverlay.GetColor;
}
```
  The mode's `Update()` (called every frame by `OverlayScreen.LateUpdate`) only recomputes the per-cell *reason* array `cells[cell] = PipPlantOverlayTests.CheckCell(cell)` for visible cells (because "SimDebugView is updated on a background thread, so ... plant checking must be done on the FG thread, it is updated here") — the actual pixel writing is the game's.
- **Per-object (plants) rendering:** built-in `Mode` helpers:
```csharp
public override void Update() {
    var intersecting = HashSetPool<Uprootable, PipPlantOverlay>.Allocate();
    base.Update();
    Grid.GetVisibleExtents(out var min, out var max);
    RemoveOffscreenTargets(layerTargets, min, max, null);
    partition.GetAllIntersecting(new Vector2(x1, y1), new Vector2(x2, y2), intersecting);
    foreach (var uprootable in intersecting)
        AddTargetIfVisible(uprootable, min, max, layerTargets, targetLayer);
    for (int y = y1; y <= y2; y++)
        for (int x = x1; x <= x2; x++) {
            int cell = Grid.XYToCell(x, y);
            if (Grid.IsValidCell(cell))
                cells[cell] = PipPlantOverlayTests.CheckCell(cell);
        }
    UpdateHighlightTypeOverlay(min, max, layerTargets, plants, conditions,
        OverlayModes.BringToFrontLayerSetting.Constant, targetLayer);
    intersecting.Recycle();
}
```
  with `targetLayer = LayerMask.NameToLayer("MaskedOverlay")`, `cameraLayerMask = LayerMask.GetMask("MaskedOverlay","MaskedOverlayBG")`, `selectionMask = LayerMask.GetMask("MaskedOverlay")`, `partition = PopulatePartition<Uprootable>(plants)` in `Enable()`.
- **Enable/Disable:**
```csharp
public override void Enable() {
    var camera = Camera.main; base.Enable();
    RegisterSaveLoadListeners();
    partition = PopulatePartition<Uprootable>(plants);
    CameraController.Instance.ToggleColouredOverlayView(true);
    if (camera != null) camera.cullingMask |= cameraLayerMask;
    SelectTool.Instance.SetLayerMask(selectionMask);
}
public override void Disable() {
    var camera = Camera.main;
    UnregisterSaveLoadListeners();
    DisableHighlightTypeOverlay(layerTargets);
    CameraController.Instance.ToggleColouredOverlayView(false);
    if (camera != null) camera.cullingMask &= ~cameraLayerMask;
    partition?.Clear(); layerTargets.Clear();
    SelectTool.Instance.ClearLayerMask();
    base.Disable();
}
```
- **Registration (PipPlantOverlayPatches.cs, a `KMod.UserMod2`):**
  - Mode: `private delegate void RegisterMode(OverlayScreen screen, OverlayModes.Mode mode);` + `typeof(OverlayScreen).Detour<RegisterMode>()` (PLib `Detour` = private-method handle), then `[HarmonyPatch(typeof(OverlayScreen), "RegisterModes")] ... internal static void Postfix(OverlayScreen __instance) { REGISTER_MODE.Invoke(__instance, new PipPlantOverlay()); }` — same private method the current SpaceOverlay patch hits via `AccessTools.Method`.
  - Menu button: `[HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")] Postfix(ICollection<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos)` — instantiates the private nested `OverlayToggleInfo` via reflection (`typeof(OverlayMenu).GetNestedType("OverlayToggleInfo", ...)`), 7 args `(text, iconName, simView, "", openKey, tooltip, text)`, then `___overlayToggleInfos.Add(info)` — same approach as current SpaceOverlay (which additionally sets the `KIconToggleMenu.ToggleInfo.getSpriteCB` field with a `Func<Sprite>`).
  - Legend: `[HarmonyPatch(typeof(OverlayLegend), "OnSpawn")] Prefix(ICollection<OverlayLegend.OverlayInfo> ___overlayInfoList)` adds `new OverlayLegend.OverlayInfo { infoUnits = new List<OverlayLegend.OverlayInfoUnit>(1) { new OverlayLegend.OverlayInfoUnit(Assets.GetSprite(icon), desc, Color.white, Color.white) }, isProgrammaticallyPopulated = true, mode = PipPlantOverlay.ID, name = ... }`.
  - Status items: in `OnLoad`, `PPatchTools.TryGetFieldValue<IDictionary<HashedString, StatusItemOverlays>>(typeof(StatusItem), "overlayBitfieldMap", out var overlayBits)` → `overlayBits.Add(PipPlantOverlay.ID, StatusItemOverlays.Farming);` (current SpaceOverlay does the same with `StatusItemOverlays.None`).
  - Icon: `[PLibMethod(RunAt.AfterDbInit)]` → `Assets.Sprites.Add(OVERLAY_ICON, PUIUtils.LoadSprite("PeterHan.PipPlantOverlay.pip.png") ?? Assets.GetSprite("overlay_farming"));` (embedded-resource sprite, not a disk file — contrast with current SpaceOverlay's `PUIUtils.LoadSpriteFile`).
  - Hotkey: `PActionManager().CreateAction(...)` passed as the toggle's `openKey`.
  - `GetSoundName() => "Harvest"` (reuses a built-in sound).
  - `GetCustomLegendData()` returns a hand-built `List<LegendEntry>` (`new LegendEntry(name, tooltip, colour, ...)`).
  - `InitDefaultFilters()` pokes the public `legendFilters` field via `GetFieldSafe`/`GetMethodSafe(CreateDefaultFilters)` even without overriding it.

### 7. Current SpaceOverlay inventory (committed state) → built-in replacement candidates
Files: `SpaceOverlay/Mod.cs` (206 ln), `SpaceOverlay/SpaceOverlayMode.cs` (268 ln).

| # | Homegrown piece (where) | Built-in API that could replace it |
|---|---|---|
| 1 | Per-cell tint mesh: one quad per tinted cell, `RebuildMesh()` (SpaceOverlayMode.cs 193–239), static `Mesh` with `IndexFormat.UInt32` | `SimDebugView.getColourFuncs` entry: `static Color GetColor(SimDebugView, int cell)` returning tint or transparent — game writes per-cell RGBA texture every frame |
| 2 | `EnsureAssets()`: `Shader.Find("Sprites/Default")` + `Texture2D.whiteTexture` + `RenderQueues.WorldTransparent` + static `Material` (SpaceOverlayMode.cs 148–174) | SimDebugView's own `material`/`tex` + `dataUpdateFuncs` fallback `SetDefaultPoint` — nothing to provide |
| 3 | z-offset: `MeshZOffset = -0.01f` + `Grid.GetLayerZ(Grid.SceneLayer.Ground)` in `Draw()` (SpaceOverlayMode.cs 44, 252) | SimDebugView plane fixed at local z = -6 — nothing to provide |
| 4 | `Draw()`: `Graphics.DrawMesh(mesh, pos, quat, material, OverlayLayer="Overlay", Camera.main, ...)` (SpaceOverlayMode.cs 241–266) | SimDebugView plane renderer (layer "SimDebugView"), drawn by the game — nothing to provide |
| 5 | `Dirty` static flag + `needsRebuild` + lazy rebuild in `Update()` (SpaceOverlayMode.cs 47, 73, 106–126) | Continuous per-frame `SimDebugView.Update()` → `UpdateData(tex, texBytes, mode, 192)` while active — no dirty tracking needed |
| 6 | `GroundRenderer.MarkDirty(int cell)` postfix (Mod.cs 38–40, 149–152) | Unneeded — sim view recomputes from live grid state every frame |
| 7 | `Grid.OnReveal` handler + `Game.Instance.Subscribe(ActiveWorldChanged)` (`OnRevealHandler`, `OnActiveWorldChanged`, SpaceOverlayMode.cs 62–70, 85–86, 100, 134–146) | Mostly unneeded — `UpdateSimViewWorkItem` reads live `Grid.IsActiveWorld`/cell state every frame (fog-of-war caveat: open question 1 below) |
| 8 | `Active` static + `OnSpawn` re-activation postfix (Mod.cs 46–48, 154–177) | Not replaced by the built-in path — `OverlayScreen.OnLoadLevel`/`OnSpawn` still resets to `None` per level (PipPlantOverlay does not handle save-load persistence either); keep or re-evaluate |
| 9 | Mode registration: `OverlayScreen.RegisterModes` postfix via `AccessTools.Method` + `Invoke` (Mod.cs 28–30, 76–92) | Same built-in private `OverlayScreen.RegisterMode(Mode)`; PipPlantOverlay uses PLib `Detour<RegisterMode>` — equivalent, interchangeable |
| 10 | Menu toggle: `OverlayMenu.InitializeToggles` postfix, reflection into private `OverlayToggleInfo` (7-arg ctor), `getSpriteCB` field, `overlayToggleInfos` list (Mod.cs 33–35, 94–147) | Same pattern as PipPlantOverlay; ctor confirmed: `OverlayToggleInfo(string text, string icon_name, HashedString sim_view, string required_tech_item = "", Action hotKey = Action.NumActions, string tooltip = "", string tooltip_header = "")`; `RefreshButtons()` hard-casts to that type |
| 11 | Icon loading from disk `PUIUtils.LoadSpriteFile` (Mod.cs 185–204) via `getSpriteCB` | PipPlantOverlay instead does `Assets.Sprites.Add(name, PUIUtils.LoadSprite("ns.embedded.png"))` in `AfterDbInit` and passes `icon_name` to the toggle ctor (how `icon_name` resolves: open question 2) |
| 12 | `StatusItem.overlayBitfieldMap` registration (Mod.cs 54–71) | PipPlantOverlay identical pattern (maps its ID to a `StatusItemOverlays` value) — keep |
| 13 | No legend: no `OverlayLegend` patch, no `GetCustomLegendData()` override | Built-in way = override `GetCustomLegendData()` (returns `List<LegendEntry>`) and/or patch `OverlayLegend.OnSpawn` prefix adding an `OverlayInfo` with `isProgrammaticallyPopulated = true` (PipPlantOverlay does both) |
| 14 | `GetSoundName() => "Off"` (SpaceOverlayMode.cs 77) | Keep — built-in hook, `"Off"` = silence |
| 15 | World desaturation: none (tint drawn on "Overlay" layer via Camera.main above desaturated world) | If the SimDebugView path is adopted, desaturation comes from `CameraController.Instance.ToggleColouredOverlayView(bool)` called in Enable/Disable (PipPlantOverlay pattern) — fact, mapping decision out of scope |

Zone predicate (the part that must move into the colour func, unchanged): `Grid.IsActiveWorld(cell) && Grid.IsVisible(cell) && Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell) == ProcGen.SubWorld.ZoneType.Space` (SpaceOverlayMode.cs 186–191; `GetSubWorldZoneType` usage confirmed at multiple game call sites).

### 8. Open questions (deeper research needed before the rewrite, if any)
1. **Fog of war:** `UpdateSimViewWorkItem` zeroes only `!Grid.IsActiveWorld` cells — no `Grid.IsCellRevealed` check visible; `SimDebugView.hideFOW` exists. Need to confirm how built-in cell modes (e.g. Oxygen) look correct under FOW (per-cell reveal check inside the colour funcs? material/compositor fog? or does the plane show hidden cells as transparent by design?).
2. **`OverlayToggleInfo.icon_name` resolution:** how does the menu turn the `icon_name` string into a sprite (likely `Assets.GetSprite(icon_name)` inside `KIconToggleMenu`/`RefreshButtons` — unverified); determines whether `Assets.Sprites.Add` + `icon_name` suffices or `getSpriteCB` is still required.
3. **`ToolParameterMenu.ToggleData` / filter menu API surface** — only needed if the mode exposes user filters (`CreateDefaultFilters`/`OnFiltersChanged`); PipPlantOverlay's `InitDefaultFilters()` suggests filters can exist even without visible submodes.
4. **`PropertyTextures.instance` public surface** — confirm there is truly no public per-cell-texture hook (evidence so far: private work items only; SimDebugView is the modder path).
5. **Save-load persistence** — whether the built-in path changes the `OnSpawn` re-activation story (currently no: `OverlayScreen` resets to None per level regardless; PipPlantOverlay doesn't handle it).

### Deep research decisions (2026-09-23)

Answers to the open questions above. All facts from decompiled sources (`lib_sources/Assembly-CSharp/`, plus the fresh `tmp-decompiled/` full ilspycmd run — same 4409-file set) and compiled-shader string extraction from `~/ONI/game/OxygenNotIncluded_Data/sharedassets0.assets`.

#### Q1 — Fog of war: the pipeline never masks unrevealed cells

- `SimDebugView.hideFOW` (`SimDebugView.cs:174`) is a **dead field**: the declaration is the only occurrence of `hideFOW` in the entire decompilable game code (re-verified with a fresh full decompile; caveat: `Main`/`Map`/`Minimap`/`GridRenderer` classes are missing from both decompilable trees, so "only occurrence" holds within decompilable evidence).
- `UpdateSimViewWorkItem.Run` (`SimDebugView.cs:124–154`) is the only per-cell gate in the whole pipeline: `if (Grid.IsActiveWorld(j)) { Color color = value(shared_data.instance, j); ... } else { /* zero bytes */ }`. No `Grid.Visible`, `PropertyTextures`, or fog reference in the work item, in `UpdateData` (`:599–625`), or anywhere in the cell-colouring path.
- `Grid.GetVisibleExtents` (`Grid.cs:1725–1770`) is **camera-viewport** based (`Camera.main.ViewportToWorldPoint`), not FOW based — it only limits the work region.
- **No** built-in colour func consults `Grid.Visible`/fog: Oxygen `GetOxygenMapColour` (`SimDebugView.cs:941–957`), Radiation `:815–819` (`Grid.Radiation[cell]`), Light `:802–813` (`Grid.LightIntensity[cell]`, `LightGridManager.previewLux[cell]`, Perlin noise), Disease `:776–790` (`Grid.DiseaseIdx[cell]`, `Db.Get().Diseases[...]`), Rooms `:821–839` (`Game.Instance.roomProber.GetCavityForCell(cell)`).
- Compositor shader `Klei/PostFX/SimDebugViewCompositor` (strings at offset 66707820 in `sharedassets0.assets`): globals are `_ColouredOverlayParameters`, `_MRT2`, `_MainTex`, `_SimDebugViewTex` — **no `_FogOfWarTex`/`_FogOfWarScale`** → the sim-view composite does not mask fog.
- Fog is a separate screen-space pass: `FogOfWarPostFX.cs` (`OnRenderImage` blit with `_UVOffsetScale` from `SetupUVs()`) using shader `Klei/PostFX/FogOfWar` (offset 66659340: samples `_MainTex` + `_FogOfWarTex`, scaled by `_FogOfWarScale`). World ground shaders also sample `_FogOfWarTex` (global set at `PropertyTextures.cs:420`; scale global at `:559`; name from `GetShaderPropertyName`, `:349–352`; `IsFogOfWarEnabled => FogOfWarScale < 1f` at `:323`).
- `Grid.IsVisible(cell)` (`Grid.cs:1772–1779`):
  ```csharp
  public static bool IsVisible(int cell)
  {
      if (Visible[cell] <= 0) return !PropertyTextures.IsFogOfWarEnabled;
      return true;
  }
  ```
  one byte read + one static float read, and it handles the FOW-disabled case (all cells count visible when FOW is off).
- The relative order of the fog screen pass vs the `SimDebugViewCompositor` pass is set by camera-prefab component order — not visible from decompiled C#.
- **Conclusion:** the FOW term must live in our colour func. `Grid.IsVisible(cell)` is the cheap, correct check. Note the `Grid.IsActiveWorld(cell)` term in our predicate is redundant — the work-item loop already zeroes non-active cells before any colour func is called.

#### Q2 — Thread safety: built-in colour funcs read sim arrays directly on the background thread

- `UpdateData` (`SimDebugView.cs:599–625`) builds 16-row strips (`UpdateSimViewWorkItem`) and runs them via `GlobalJobManager.Run(updateSimViewWorkItems)` (`:622`) — multi-threaded.
- Built-in colour funcs read sim data directly, off-thread, inside the func (exact quotes):
  - `GetOxygenMapColour` (`:941–957`): `if (!Grid.IsLiquid(cell) && !Grid.Solid[cell]) { if (Grid.Mass[cell] > minimumBreathable && (Grid.Element[cell].id == SimHashes.Oxygen || ...)) { float time = Mathf.Clamp((Grid.Mass[cell] - minimumBreathable) / optimallyBreathable, 0f, 1f); result = instance.breathableGradient.Evaluate(time); } ... }`
  - `GetRadiationColour` (`:815–819`): reads `Grid.Radiation[cell]`.
  - `GetLightColour` (`:802–813`): reads `Grid.LightIntensity[cell]`, `LightGridManager.previewLux[cell]`, `PerlinSimplexNoise.noise`.
  - `GetDiseaseColour` (`:776–790`): reads `Grid.DiseaseIdx[cell]`, `Db.Get().Diseases[...].uiColour`.
  - `GetRoomsColour` (`:821–839`): calls `Game.Instance.roomProber.GetCavityForCell(cell)` — a heavier object-graph query, still done off-thread.
- `Grid.Visible` is `public static byte[]` (`Grid.cs:718`); `Grid.WorldIdx` is `public static byte[]` (`Grid.cs:728`) → `Grid.IsVisible`/`Grid.IsActiveWorld` are flat array reads — same category as the game's own off-thread reads.
- `WorldRenderData.zoneRenderData.GetSubWorldZoneType(cell)` (`SubworldZoneRenderData.cs:220–227`):
  ```csharp
  public SubWorld.ZoneType GetSubWorldZoneType(int cell)
  {
      if (cell >= 0 && cell < worldZoneTypes.Length)
      {
          return worldZoneTypes[cell];
      }
      return SubWorld.ZoneType.Sandstone;
  }
  ```
  bounds check + plain per-cell array index — safe off-thread (same pattern as the game's own use in `MinionBrain.cs:102`, `DiggerMonitor.cs:72`, `LargeImpactorCrashStamp.cs:79`).
- **Conclusion:** all three predicate reads (`Grid.Visible` byte, `Grid.IsActiveWorld` byte, `GetSubWorldZoneType` array index) are safe direct background-thread reads; no precomputation is required for correctness. PipPlantOverlay precomputes per frame because its `CheckCell` is a heavy multi-object rule check, not because of array safety:
  - `PipPlantOverlay.Update()` (`PipPlantOverlay.cs:293–315`) recomputes per frame: `Grid.GetVisibleExtents(out var min, out var max, ...)`, refreshes the on-screen plant list (`RemoveOffscreenTargets` + `partition.GetAllIntersecting` + `AddTargetIfVisible`), then
    ```csharp
    for (int y = y1; y <= y2; y++)
        for (int x = x1; x <= x2; x++) {
            int cell = Grid.XYToCell(x, y);
            if (Grid.IsValidCell(cell))
                cells[cell] = PipPlantOverlayTests.CheckCell(cell);
        }
    ```
    over the camera-visible extents, plus `UpdateHighlightTypeOverlay(...)`.
  - Its colour func `GetColor` (`PipPlantOverlay.cs:41–62`) reads only `Instance.cells[cell]` (precomputed enum array) and `GlobalAssets.Instance.colorSet`.

#### Q3 — Camera / desaturation: the plane is composited from its own RT, not crushed

- `CameraController.OnPrefabInit` (`CameraController.cs:296–349`):
  - `baseCamera` (copy of `Camera.main`, z −100) renders the world; `AddComponent<CameraRenderTexture>().TextureName = "_LitTex"` (`:315`); RT is ARGB32 point-sampled (`CameraRenderTexture.cs:33–40`).
  - **`simOverlayCamera` is dedicated** (`:322–330`): `cullingMask = LayerMask.GetMask("SimDebugView")` only, `clearFlags = CameraClearFlags.Color`, `depth = baseCamera.depth + 1f`, `TextureName = "_SimDebugViewTex"`.
  - `overlayCamera` = `Camera.main` renamed "Overlay" (depth `baseCamera.depth + 3f`, cullingMask PlaceWithDepth|Overlay|Construction, `CameraReferenceTexture.referenceCamera = baseCamera` draws `_LitTex` full-screen, `:331–348`).
- So our per-cell colours travel through `_SimDebugViewTex` and never pass through the world MRT buffers (`_MRT0/_MRT1/_MRT2`, `MultipleRenderTargetProxy.cs:27–97`).
- `ToggleColouredOverlayView(bool)` (`CameraController.cs:271–274` → `MultipleRenderTarget.cs:49–55` → `MultipleRenderTargetProxy.cs:27–31`) only toggles the extra `_MRT2` world desaturation buffer; `_ColouredOverlayParameters` global is set by `Infrared.SetMode` (`Infrared.cs:64–83`: zero for Disabled, `Vector4(1,0,0,0)` for Infrared/Disease).
- Final composite shader `Klei/PostFX/SimDebugViewCompositor` mixes `_MainTex` + `_MRT2` + `_ColouredOverlayParameters` + `_SimDebugViewTex` → the sim-view texture is composited from its own RT; the desaturation pass acts on the world image only.
- `ToggleColouredOverlayView` callers: Disease overlay Enable/Disable (`OverlayModes.cs:785–798` / `:828–864`, together with `Infrared.Instance.SetMode`) and the Temperature overlay per `temperatureOverlayMode` (`:3378–3409`). **The Oxygen overlay does NOT call it** — its red/green cell colours work fine without world desaturation.
- **Conclusion:** our tint is composited on top from `_SimDebugViewTex`; it will not be desaturated to gray. For a pure per-cell colour mode the minimal Enable/Disable is the base `Mode.Enable()`/`Disable()` (empty virtuals, `OverlayModes.cs:1893–1903`). `ToggleColouredOverlayView`, `Camera.main.cullingMask` changes, and `SelectTool` layer masks are PipPlantOverlay-specific (plant highlight quads + picking), not required.
- PipPlantOverlay exact code for reference — `Enable()` (`PipPlantOverlay.cs:167–176`):
  ```csharp
  public override void Enable() {
      var camera = Camera.main;
      base.Enable();
      RegisterSaveLoadListeners();
      partition = PopulatePartition<Uprootable>(plants);
      CameraController.Instance.ToggleColouredOverlayView(true);
      if (camera != null)
          camera.cullingMask |= cameraLayerMask;
      SelectTool.Instance.SetLayerMask(selectionMask);
  }
  ```
  (`cameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG")`, `selectionMask = LayerMask.GetMask("MaskedOverlay")`.)
  `Disable()` (`:154–165`) mirrors it: `UnregisterSaveLoadListeners()`, `DisableHighlightTypeOverlay(layerTargets)`, `ToggleColouredOverlayView(false)`, `camera.cullingMask &= ~cameraLayerMask`, `partition?.Clear()`, `layerTargets.Clear()`, `SelectTool.Instance.ClearLayerMask()`, `base.Disable()`.

#### Q4 — OnPrefabInit postfix shape confirmed

- `SimDebugView.OnPrefabInit()` (`SimDebugView.cs:450`, no parameters, `protected override void`): body is only
  ```csharp
  Instance = this;
  material = UnityEngine.Object.Instantiate(material);
  diseaseMaterial = UnityEngine.Object.Instantiate(diseaseMaterial);
  ```
- `getColourFuncs` is a **private field populated by a field initializer at construction** (`SimDebugView.cs:270`):
  ```csharp
  private Dictionary<HashedString, Func<SimDebugView, int, Color>> getColourFuncs = new Dictionary<HashedString, Func<SimDebugView, int, Color>>
  {
      { global::OverlayModes.ThermalConductivity.ID, GetThermalConductivityColour },
      ...
  }
  ```
  — all built-ins are registered **before** `OnPrefabInit`, so a postfix on `OnPrefabInit` runs after every built-in. This field initializer is the only population site in the decompilable game code.
- Lookup happens per work item: `shared_data.instance.getColourFuncs.TryGetValue(shared_data.simViewMode, out var value)` (`SimDebugView.cs:126`), with fallback `SetDefaultPoint` in `UpdateData` (`:601–604`).
- PipPlantOverlay's exact patch (`PipPlantOverlayPatches.cs:203–211`) — the identical shape we planned:
  ```csharp
  [HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
  public static class SimDebugView_OnPrefabInit_Patch {
      internal static void Postfix(IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs) {
          ___getColourFuncs[PipPlantOverlay.ID] = PipPlantOverlay.GetColor;
      }
  }
  ```
- **Conclusion:** postfix on `SimDebugView.OnPrefabInit` with the injected `___getColourFuncs` field is the correct, game-proven pattern.

#### Q5 — No cost when the mode is off

- `SimDebugView.Update()` (`SimDebugView.cs:553–573`):
  ```csharp
  bool flag = mode != global::OverlayModes.None.ID;
  plane.SetActive(flag);
  SimDebugViewCompositor.Instance.Toggle(flag && !GameUtil.IsCapturingTimeLapse());
  ...
  if (flag) { UpdateData(tex, texBytes, mode, 192); ... }
  ```
  → while our mode is off (`mode == None.ID`) the plane is deactivated, the compositor is off, and no `UpdateData`/colour-func work happens; residual cost is only `SetActive(false)` + `Toggle(false)`.
- Even while another mode is active, `getColourFuncs.TryGetValue` resolves only the active mode's func — ours is never called.
- `OverlayScreen.LateUpdate()` (`OverlayScreen.cs:177–180`) calls `currentModeInfo.mode.Update()` for the **current mode only** → our `Mode.Update()` is never invoked while inactive.
- Toggle flow (`OverlayScreen.ToggleOverlay`, `:182–223`): `currentModeInfo.mode.Disable()` → `SimDebugView.Instance.SetMode(newMode)` (`SimDebugView.cs:637–641`, also fires a game event) → `modeInfos.TryGetValue(newMode, ...)` (private `RegisterMode(Mode)`, `OverlayScreen.cs:169–175`; built-ins registered at `:131–167`) → `Enable()`. An unregistered ID falls back to the None mode (`:195–198`) → our mode must be registered via `RegisterMode` for the toggle button to work.
- **Conclusion:** zero per-frame cost when off; everything is gated on `mode != None.ID`.
