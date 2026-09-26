# Research: ResourceFieldInfo mod (Ctrl + hover → region cell count + total mass)

Task (research only, no design/implementation): on **Ctrl + hover over a material cell** (no tool/build mode active), append two tooltip lines:
(a) cell count of the contiguous same-material region (4- or 8-neighbor — which does the game use?),
(b) total mass of that region. Applies to water, magma, natural solids, gases.

All paths below are relative to `/home/apkawa/code/ONI_MODS/` unless absolute. Decompiled game sources: `lib_sources/Assembly-CSharp/` (Assembly-CSharp.dll), `lib_sources/Assembly-CSharp-firstpass/` (Assembly-CSharp-firstpass.dll). Full DLL type inventory was dumped to `Apkawa_ONI_Mods/.tmp/dll_types.txt` (17 129 classes, `ilspycmd -l cs ~/ONI/dlls/Assembly-CSharp.dll`).

> ⚠️ **Critical:** this game build (the "Aquatic" release) renamed/refactored the classic API. The old classes **`Screen`, `ScreenBuilding`, `ScreenMining`, `KCell`, `Tooltip`, `Material` DO NOT EXIST** in the current `Assembly-CSharp.dll` (verified by the DLL type listing, not by decompilation gaps). New names: `Material` → **`Element`**; cell data lives in **`Grid`** unsafe pointer arrays; screens extend **`KScreen`** (firstpass) / `KModalScreen` / `SideScreenContent` / `TableScreen`; tooltips are drawn by the **`HoverText*`** system. Any task brief referencing the old names is stale.

---

## 1. Mod project structure

- Solution: `Apkawa_ONI_Mods/ONI-mods.sln` — actually contains **8 projects** (AGENTS.md's "only BuildDoorOverWall + UtilLibs" claim is stale): `BuildDoorOverWall`, `UtilLibs`, `SizeInTooltip`, `ReplaceBuildingMaterial`, `BestBuildDryWall`, `SpaceOverlay`, `ResourceRemain`, `PrinterEasyInfo`. MSBuild sln parser has no comment support — a project is disabled by deleting its `Project(` line + 4 configuration lines.
- Mod project template — `Apkawa_ONI_Mods/BuildDoorOverWall/BuildDoorOverWall.csproj`: `TargetFramework=net48`, `<IsMod>true</IsMod>`, `<IsPacked>true</IsPacked>`, `<GenerateMetadata>true</GenerateMetadata>`, `WorkshopItemId`, `<ProjectReference>` → `UtilLibs`.
- Lib project — `Apkawa_ONI_Mods/UtilLibs/UtilLibs.csproj`: `IsMod=false`, `DoNotBuildAsMod=true`, `IsPacked=false`; carries `PackageReference PeterHan.PLib 4.19.0` (shared util, packed into the mod dll).
- Mod entry point pattern — `Apkawa_ONI_Mods/BuildDoorOverWall/Mod.cs`:
  ```csharp
  namespace OxygenNotIncluded.Mods {
      public class Mod : UserMod2 {
          protected override void OnLoad(Harmony harmony) {
              base.OnLoad(harmony); // Harmony PatchAll: only [HarmonyPatch]-attributed types apply
              PatchUtil.TryPatch(harmony, typeof(...), "Method", new[] { typeof(...) }, "feature",
                  prefix: null, postfix: null, transpiler: null);
          }
      }
  }
  ```
  One `UserMod2` subclass per assembly; the game's `KMod/DLLLoader` loads **every `*.dll`** in a mod folder and creates one Harmony per mod folder.
- Programmatic patch helper — `Apkawa_ONI_Mods/UtilLibs/PatchUtil.cs:33`:
  ```csharp
  public static bool TryPatch(Harmony harmony, Type type, string name, Type[] paramTypes,
      string feature, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
  ```
  (Used because `[HarmonyPatch]` attributes can't express `out`/byref parameter types.)
- Build invariants (repo `AGENTS.md`): TFM **net48** everywhere; reference the **game's** `0Harmony.dll` (v2) — never NuGet Harmony; Publicizer assets `build; contentfiles` only; ILRepack via `dotnet-ilrepack` 2.0.45 (`dotnet ILRepackTool.dll`); scratch → `./.tmp/`, caches → `./.cache/`. Build: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` (the `CopyModsToDevFolder` post-step fails read-only in the sandbox — expected, artifacts stay in `bin/`).
- Best in-repo tooltip precedent: `Apkawa_ONI_Mods/SizeInTooltip/Mod.cs` — programmatic **prefix** on `HoverTextConfiguration.DrawInstructions(HoverTextScreen, HoverTextDrawer)` (protected, publicized at build), appends extra lines via `drawer.NewLine(); drawer.DrawText(line, style);`, with try/catch + one-shot `PUtil.LogError`.
- Logging: PLib `PUtil.LogDebug/LogWarning/LogError` + `.F(...)` placeholders; mod name auto-prefixed.

## 2. Tooltip mechanism

**Full call chain (default/Select tool, i.e. plain gameplay hover):**

1. `InterfaceTool.LateUpdate()` — `lib_sources/Assembly-CSharp/InterfaceTool.cs:295`. Every frame for the **active** tool; when `populateHitsList` it gathers selectables under the cursor, then calls
2. `InterfaceTool.UpdateHoverElements(List<KSelectable> hits)` — `InterfaceTool.cs:287-292`:
   ```csharp
   protected void UpdateHoverElements(List<KSelectable> hits) {
       if (hoverTextConfiguration != null)
           hoverTextConfiguration.UpdateHoverElements(hits);
   }
   ```
   where `hoverTextConfiguration = GetComponent<HoverTextConfiguration>()` (`InterfaceTool.cs:127`) — the tool's card component (for the default tool this is `SelectToolHoverTextCard`).
3. `SelectToolHoverTextCard.UpdateHoverElements(List<KSelectable> hoverObjects)` — `lib_sources/Assembly-CSharp/SelectToolHoverTextCard.cs:164` (override of the base `HoverTextConfiguration.UpdateHoverElements` at `HoverTextConfiguration.cs:108`). It computes the hovered cell from the mouse:
   ```csharp
   int num = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos())); // line 170
   if (OverlayScreen.Instance == null || !Grid.IsValidCell(num)) return;
   HoverTextDrawer hoverTextDrawer = HoverTextScreen.Instance.BeginDrawing();
   ```
   and in its element block (≈ lines 694-820) draws, per hovered cell `num`:
   ```csharp
   Element element = Grid.Element[num];
   hoverTextDrawer.DrawText(element.nameUpperCase, Styles_Title.Standard);
   // ... category: ElementLoader.elements[Grid.ElementIdx[num]].GetMaterialCategoryTag().ProperName()
   string[] array = HoverTextHelper.MassStringsReadOnly(num);   // line 704
   // ... temperature: GameUtil.GetFormattedTemperature(Grid.Temperature[num])
   ...
   hoverTextDrawer.EndDrawing();  // line 817 — last statement of the method
   ```
   The element block is drawn only when the cell is visible/not a solid building (`flag2`) and in the active world; otherwise an "Unknown" warning line is drawn instead.
4. `HoverTextScreen` — `lib_sources/Assembly-CSharp/HoverTextScreen.cs` (`KScreen`):
   - `public static HoverTextScreen Instance` (set in `OnActivate`); `public HoverTextDrawer drawer` (line 10).
   - `public HoverTextDrawer BeginDrawing()` (line 26) — re-derives the mouse-local position each call, starts a fresh frame.
   - `Update()` (line 38): `drawer.SetEnabled(PlayerController.Instance.ActiveTool.ShowHoverUI());` — the whole hover text is disabled whenever the active tool says so.
5. `HoverTextDrawer` — `lib_sources/Assembly-CSharp/HoverTextDrawer.cs`:
   - `BeginDrawing(Vector2 root_pos)` (173), `EndDrawing()` (189) — hides pooled widgets beyond `drawnWidgets` (so lines added **before** `EndDrawing` are kept).
   - `NewLine()`, `DrawText(string text, TextStyleSetting style, Color color, bool override_color = true)`, `DrawIcon(Sprite, float size)`, `BeginShadowBar(...)`/`EndShadowBar()`, `SetEnabled(bool)`.
6. `HoverTextHelper` — `lib_sources/Assembly-CSharp/HoverTextHelper.cs`:
   ```csharp
   public static string[] MassStringsReadOnly(int cell)  // line 20
   // uses Element element = Grid.Element[cell]; float num = Grid.Mass[cell];
   // returns 4 strings (mass value / unit / per-second bits); cached
   ```

**Other hover cards** (each tool has one, all `: HoverTextConfiguration`, each ending in `hoverTextDrawer.EndDrawing()`): `BuildToolHoverTextCard.cs`, `DigToolHoverTextCard.cs`, `MopToolHoverTextCard.cs`, `PlaceToolHoverTextCard.cs`, `CancelToolHoverTextCard.cs`, `DeconstructToolHoverTextCard.cs`, `PrebuildToolHoverTextCard.cs` (multi-line splitting pattern at lines 24-33), etc. `DigToolHoverTextCard.cs:57` and `MopToolHoverTextCard.cs:52` also call `HoverTextHelper.MassStringsReadOnly`.

**`HoverTextDrawer.EndDrawing()` is called exactly once per frame by the active tool's card** (early-return paths call it too, e.g. `HoverTextConfiguration.cs:115,122`).

**External precedent:** Futility's `ThermalTooltips` mod (`lib_sources/peterhaneve_ONIMods/ThermalTooltips/ThermalTooltipsPatches.cs:148`) transpiles **exactly this method** — `[HarmonyPatch(typeof(SelectToolHoverTextCard), "UpdateHoverElements")]` with a transpiler that injects extra thermal lines before the end. That is a proven pattern for adding lines to the cell element tooltip.

**Hook options (research verdict, no design):**
- **Prefix on `HoverTextDrawer.EndDrawing()`** — cleanest: fires once per frame after the active card drew all its lines and before widgets are hidden. A mod prefix can guard (`IsUsingDefaultTool() && Ctrl held`, valid cell), recompute `int cell = Grid.PosToCell(...)`, then `NewLine(); DrawText(...)` twice. No transpiler needed; the drawer instance is public (`HoverTextScreen.Instance.drawer`).
- Transpiler on `SelectToolHoverTextCard.UpdateHoverElements(List<KSelectable>)` (Futility's approach) — more invasive (IL surgery) but injects at a precise position inside the method.
- A **postfix** on `UpdateHoverElements` is **not** viable: `EndDrawing()` has already run by then; calling `BeginDrawing()` again would start a new frame and lose the existing content.
- The in-repo `SizeInTooltip` prefix on `HoverTextConfiguration.DrawInstructions` only affects tool *instruction* lines, not the element data block — it works for the select tool only incidentally (the base class also has `DrawInstructions`).

## 3. Active tool / build mode detection

- The old API is gone: there is **no** `Screen`, `ScreenBuilding`, `ScreenMining`, and **no** `Player.GetActiveScreen()`. `Player` (`lib_sources/Assembly-CSharp/Player.cs`) only has `public GameScreenManager ScreenManager` (line 6); screens now extend `KScreen`/`KModalScreen`/`SideScreenContent`.
- The tool system is `PlayerController` + `InterfaceTool`:
  - `lib_sources/Assembly-CSharp/PlayerController.cs` — `public class PlayerController : KMonoBehaviour, IInputHandler`:
    - `public static PlayerController Instance { get; private set; }` (line 51)
    - `public InterfaceTool[] tools;` (line 15); default tool = `tools[0]`, activated in `OnSpawn` (line 76).
    - `public InterfaceTool ActiveTool => activeTool;` (line 49)
    - `public bool IsUsingDefaultTool()` (lines 196-202):
      ```csharp
      public bool IsUsingDefaultTool() {
          if (tools.Length != 0) { return activeTool == tools[0]; }
          return false;
      }
      ```
  - `lib_sources/Assembly-CSharp/InterfaceTool.cs` — `public class InterfaceTool : KMonoBehaviour` (base of all tools: `SelectTool`, `BuildTool`, `DigTool`, `DragTool`, `PlaceTool`, `CancelTool`, `DeconstructTool`, … — all verified in the DLL type list; there is no plain `Tool` class).
    - `public virtual bool ShowHoverUI()` (lines 137-154) — returns `false` when the pause/management UI is fullscreen, the mouse is outside the world, or the pointer is over a UI object. `HoverTextScreen.Update()` uses exactly this to hide the hover text, so any mod hook behind it inherits this behavior.
- **"No tool/build mode active"** therefore translates to:
  - `PlayerController.Instance.IsUsingDefaultTool()` — the default tool is the select tool (`SelectTool.Instance` is its singleton; `SelectTool.cs:4 public class SelectTool : InterfaceTool`).
  - "Build mode active" = `PlayerController.Instance.ActiveTool is BuildTool` (and analogously `DigTool` for mining, etc.).
- Note: there is no single `GetActiveTool()` method on `Game`; `PlayerController.Instance.ActiveTool` is the canonical accessor used by the game itself (e.g. `HoverTextScreen.cs:40`).

## 4. Cell mass / region flood-fill utilities

**Per-cell data — `lib_sources/Assembly-CSharp/Grid.cs`** (static class, unsafe pointer arrays sized `Grid.CellCount`, line 628):
```csharp
public unsafe static ushort* elementIdx;   // line 696
public unsafe static float* temperature;   // line 698
public unsafe static float* mass;          // line 702  ← kg per cell, authoritative
```
Indexers (same file): `public static MassIndexer Mass` (line 748; `MassIndexer` struct line 504: `public unsafe float this[int i] => mass[i];`), `ElementIdx` (742), `Temperature` (746), plus `public static Element[] Element` (line 732) — a cached per-cell `Element` reference array.
Helpers: `Grid.IsValidCell(int cell)` (1366), `Grid.PosToCell(Vector3 pos)` (1432) / `PosToCell(Vector2)` (1424), `Grid.CellLeft/CellRight/CellAbove/CellBelow(int)` (1171-1190), diagonals `CellUpLeft/CellUpRight/CellDownLeft/CellDownRight` (1207-1237), `OffsetCell(int, CellOffset)` / `OffsetCell(int, int, int)` (1294-1299), `IsCellOffsetValid` (1304-1318), `Grid.DupePassable`, `Grid.Solid`, `Grid.IsVisible`, `Grid.WorldIdx` (used by `SelectToolHoverTextCard`).

**Element (the old "Material") — `lib_sources/Assembly-CSharp/Element.cs`**:
```csharp
public class Element                       // line 12
{
    public ushort idx;                     // == Grid.ElementIdx[cell]
    public SimHashes id;
    public float maxMass;
    public Sim.PhysicsData defaultValues;  // line 105
    public bool IsLiquid/IsGas/IsSolid/IsVacuum;  // lines 146-152
    ...
}
```
- `lib_sources/Assembly-CSharp/Sim.cs`: `public struct PhysicsData` (line 39) = `{ float temperature; float mass; float pressure; }` — **`Element.defaultValues.mass` is the mass of a full (solid) cell** of that element. For fluids the per-cell mass varies, so sum `Grid.Mass[cell]` instead.
- Lookups: `ElementLoader.elements[]` indexed by `ushort idx` (as used in `SelectToolHoverTextCard`), `ElementLoader.FindElementByHash(SimHashes)`.

**Flood fill — `lib_sources/Assembly-CSharp/FloodFill.cs`** (`public static class FloodFill`):
- **The game's flood fill is strictly 4-neighbor.** `BreadthTraverse` enqueues only `Grid.CellLeft/CellRight/CellAbove/CellBelow` (lines 674-677); `DepthTraverse` is a sweepline over the same 4 cardinal rays. There is **no 8-neighbor option** anywhere in the class. If 8-neighbor behavior is wanted, the mod must add diagonals itself (`Grid.CellUpLeft` etc.).
- Key API:
  ```csharp
  public enum BoundaryCheckResult { Continue, Halt }                                  // line 93
  public readonly struct PredicateCondition : IBoundaryCondition                     // line 104
  { public PredicateCondition(Func<int, BoundaryCheckResult> predicate); }

  public static void BreadthCollect(int startCell, Func<int, BoundaryCheckResult> boundaryCondition,
                                     List<int> validCells, int maxDepth)             // line 716
  public static void BreadthCollect(int startCell, Func<int, BoundaryCheckResult> boundaryCondition,
                                     HashSet<int> visitedCells, List<int> validCells)  // line 700
  public static void DepthCollect(int startCell, Func<int, BoundaryCheckResult> boundaryCondition,
                                   List<int> validCells)                              // line 922
  public readonly struct ElementCheck : IBoundaryCondition                           // line 119
  { public ElementCheck(bool stop_at_solid, bool stop_at_liquid); }   // stops at solids/liquids — NOT same-material
  public struct NoMaxDepth : IMaxDepth;   // default (line 162)
  public struct MaxDepth(int);            // line 170
  // visit trackers (thread-local, cheap): GenerationGrid.Default(), HashSetVisitTracker.Default()
  ```
- **There is no dedicated "contiguous region of the same material" API and no region-mass utility anywhere in the game.** All in-tree `FloodFill` callers do something else: `SpaceTreeSeededComet.cs:16`, `FloodTool.cs:18`, `Comet.cs:351`, `DevAutoPlumber.cs:271` (`BreadthTraverse`), `StickerBomber.cs:149`, `ProcGenGame/MobSpawning.cs:539` — none of them sums mass.
- A same-material region therefore has to be built from pieces:
  ```csharp
  ushort startIdx = Grid.ElementIdx[cell];
  List<int> region = /* pooled List<int> */;
  FloodFill.BreadthCollect(cell,
      c => Grid.ElementIdx[c] == startIdx ? FloodFill.BoundaryCheckResult.Continue
                                           : FloodFill.BoundaryCheckResult.Halt,
      region, /* maxDepth or NoMaxDepth overload */);
  int count = region.Count;
  float totalMass = 0; for (int c : region) totalMass += Grid.Mass[c];   // authoritative
  // For pure solids, count * Grid.Element[cell].defaultValues.mass is equivalent.
  ```
- Mass formatting for tooltips: `lib_sources/Assembly-CSharp/GameUtil.cs:1464`:
  ```csharp
  public static string GetFormattedMass(float mass, TimeSlice timeSlice = TimeSlice.None,
      MetricMassFormat massFormat = MetricMassFormat.UseThreshold,
      bool includeSuffix = true, string floatFormat = "{0:0.#}")
  ```
  (the same formatter `HoverTextHelper.MassStringsReadOnly` and `SelectToolHoverTextCard` use).

## 5. i18n msgids (RU/EN .po)

Mechanics (see also repo `docs/i18n.md`):
- Game strings are compiled `LocString` statics in `STRINGS.*` classes; the key is the class path, e.g. `Strings.Get("STRINGS.UI.ALLRESOURCESSCREEN.TOTAL")`.
- Mod strings live in `<mod>/strings/*.po`, loaded sequentially by `Mod.LoadStrings()` — **last file wins per key** (Futility's multilingual approach: one .po per language in the same folder).
- Preinstalled language files in `~/ONI/game/OxygenNotIncluded_Data/StreamingAssets/strings/`: `strings_preinstalled_ko_klei.po`, `strings_preinstalled_ru_klei.po`, `strings_preinstalled_zh_klei.po`, plus `strings_template.pot`. **There is NO English .po** — English is the base language compiled into `Assembly-CSharp` (`lib_sources/Assembly-CSharp/STRINGS/`).

Exact keys/values:
- **RU** (`~/ONI/game/OxygenNotIncluded_Data/StreamingAssets/strings/strings_preinstalled_ru_klei.po`):
  ```
  msgctxt "STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME"
  msgid "Tiles"
  msgstr "Клетки"
  ```
  ```
  msgctxt "STRINGS.UI.ALLRESOURCESSCREEN.TOTAL"
  msgid "Total"
  msgstr "Всего"
  ```
- **EN base** (`lib_sources/Assembly-CSharp/STRINGS/UI.cs`):
  - `STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME` — `public static LocString NAME = "Tiles";` (line 13443; `class FILTERLAYERS` at line 13432, nested under `UI.TOOLS`).
  - `STRINGS.UI.ALLRESOURCESSCREEN.TOTAL` — `public static LocString TOTAL = "Total";` (class `ALLRESOURCESSCREEN` starts at line 9345).
- A mod's own new strings must be declared in its own `<mod>/strings/*.po` (e.g. `msgid "Cells in region"` / `msgstr "Клеток в области"`, `msgid "Total mass"` / `msgstr "Всего массы"` etc. — new keys, so no collision with preinstalled keys).

## 6. Ctrl key check example

Plain `UnityEngine.Input` is used directly in game code (no KInput wrapper required for a one-off modifier check):

- `lib_sources/Assembly-CSharp/VirtualCursorOverlayFix.cs:32`:
  ```csharp
  if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.C))
  ```
- `lib_sources/Assembly-CSharp/PlayerController.cs:134`:
  ```csharp
  if (Input.GetKeyDown(KeyCode.F12) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
  ```
- `lib_sources/Assembly-CSharp/InputBindingsScreen.cs:302` (treats both physical Ctrl keys):
  ```csharp
  modifier |= ((IsKeyDown(KeyCode.LeftControl) || IsKeyDown(KeyCode.RightControl)) ? Modifier.Ctrl : Modifier.None);
  ```

So the representative check is `Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)`.

---

## Verdicts

1. **Cleanest tooltip hook point:** **prefix on `HoverTextDrawer.EndDrawing()`** (`lib_sources/Assembly-CSharp/HoverTextDrawer.cs:189`). It fires exactly once per frame from the active tool's hover card, after all lines of the frame (including the select-tool element block) have been drawn and before the drawer hides unused pooled widgets — the prefix can then `NewLine(); DrawText(...)` twice. Guards needed inside: `PlayerController.Instance.IsUsingDefaultTool() && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))`, valid cell via `Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()))`. Fallback (proven by Futility's ThermalTooltips, `ThermalTooltipsPatches.cs:148`): transpiler on `SelectToolHoverTextCard.UpdateHoverElements(List<KSelectable>)`. A postfix on that method is NOT viable (`EndDrawing` already ran). The in-repo `SizeInTooltip` prefix-on-`DrawInstructions` pattern is a good template for the patching style but hooks the wrong place for the element block.
2. **Existing region-mass utility:** **None.** There is no same-material-region API and nothing that sums a region's mass. Build it from `FloodFill.BreadthCollect` (the game's flood fill, **4-neighbor**) + a `Grid.Mass[c]` sum (or `count * Element.defaultValues.mass` for solids). 8-neighbor is not available from `FloodFill` and would need manual diagonal handling.

---

## Round 3: invisibility with Better Info Cards installed (logs 4–5)

Diagnosis from `.tmp/Player_resource_field_4.log` (66 successful draws, zero errors, still
invisible, cursor mid-screen) + screenshot (our NewLine/DrawText did not advance
`currentPos`) + `.tmp/Player_resource_field_5_crash.log` (single
`BetterInfoCards.InterceptHoverDrawer+NewLine.Prefix` NRE on the first session hover,
caught by our try/catch — the log ends with a clean `Game.OnApplicationQuit()`, no crash).

Root cause: the user's game has the workshop mod **Better Info Cards** (Aze, workshop id
1960947963, source `github.com/AzeTheGreat/ONI-Mods`, `src/BetterInfoCards/`). Its
"intercept mode":
- postfix on `HoverTextDrawer.BeginDrawing` sets public static
  `InterceptHoverDrawer.IsInterceptMode = true` (static `curInfoCard` still null);
- `[HarmonyPriority(Priority.First)]` (1000) prefixes on `BeginShadowBar`, `DrawIcon`,
  `DrawText`, `AddIndent`, `NewLine`, `EndShadowBar` swallow the originals and record draw
  actions into `curInfoCard` — which is set ONLY inside its BeginShadowBar prefix;
- a prefix on `HoverTextDrawer.EndDrawing` re-renders the captured card with intercept OFF
  (`Process/ProcessHoverInfo.cs`), then sets `IsInterceptMode = true` again.

So our BeginDrawing-postfix draws landed in a null `curInfoCard` (NRE, first hover of a
session) or in the PREVIOUS frame's stale card (silently lost, all later frames) — explaining
both log 4 (zero errors, invisible) and log 5 (one NRE, invisible).

Fix (commit 121715a): when BIC is present and intercept is on, DEFER the two lines to a
new top-level patch `FieldInfoShadowBarPatch` — prefix on `HoverTextDrawer.BeginShadowBar`
with `[HarmonyPriority(999)]` (below BIC's 1000, so it runs after BIC set `curInfoCard`).
There the lines are intercepted into the CURRENT frame's card and BIC's re-render draws them
at the top inside the panel. Plain text lines (empty BIC export id) pass through unchanged
(`ConverterManager` has a default pass-through converter for `string.Empty`;
`TextInfo.Create` never NREs). Without BIC, the previous direct top-placement drawing is
kept (BIC probe via `Type.GetType("BetterInfoCards.InterceptHoverDrawer, BetterInfoCards")`
+ public static property `IsInterceptMode`). `Mod.OnLoad` also verifies/registers the new
prefix. Note: `PatchAll` only scans top-level types (0Harmony
`AccessTools.GetTypesFromAssembly` = `assembly.GetTypes()`), so the new patch class is
top-level, not nested.

BIC sources kept at `.tmp/oni-mods-aze/` (sparse clone, `src/BetterInfoCards` only).

## Round 4: lines visible but mis-placed by BIC (screenshot)

First successful in-game render: the two lines appeared, but "Клетки: N" was rendered
ABOVE the shadow bar and "Всего: Xт" straddled its top edge, while the element title
("ОРГАНИКА") vanished. Diagnosis: BIC treats the FIRST text action of a card as the
card title (titleDrawer in InfoCard.AddDraw / card.Draw) — our top-of-card lines displaced
the real title, so BIC's layout pushed them out of the bar.

Fix (commit 6f7b4df): drop the BeginShadowBar-deferred design. New
`FieldInfoDrawTextPatch` — PREFIX on the 4-arg `HoverTextDrawer.DrawText(string,
TextStyleSetting, Color, bool)` at class-level `[HarmonyPriority(1001)]` (above BIC's
1000). The element card draws title → disease → category → mass → temperature
(SelectToolHoverTextCard.cs:681-744); the prefix matches the incoming text against the
pending cell's expected temperature row ("N/A" iff `specificHeatCapacity == 0f`, else
`GameUtil.GetFormattedTemperature(Grid.Temperature[cell])`, card lines 718-727) and, on
match, inserts two vanilla-style rows (NewLine + `DrawIcon(card.iconDash, Color.white, 18,
2)` + DrawText) right before the temperature row — after mass, before temperature, with
dash bullets, inside the bar. With BIC: our re-entrant calls run through BIC's 1000
prefixes and are recorded in order before the temperature row is recorded → BIC re-renders
them mid-card with the real title intact. Without BIC: drawn live in place. The 2-arg
card DrawText delegates to the 4-arg (HoverTextDrawer.cs:228-231), so one prefix catches
all card text. Re-entrancy safe (pending cleared before re-entrant calls). The Ctrl
requirement is now a `s_requireCtrl` toggle, set to false for the visual-acceptance test
build (TODO(final): true). DLL: 343 552 bytes.

## Round 5 — insert on the mass→temperature NewLine (BIC is off)

User reported two layout bugs with the Better Info Cards mod DISABLED:
(1) an empty bullet (dash) row between the mass row and "Клетки: 124";
(2) temperature text glued onto the "Всего" row: "Всего: 25.2 т63.9 °C".
Since BIC was off, BIC's re-render could not be the cause — the defect was in
our own DrawText-prefix insertion point.

Root cause (no BIC needed): the element card draws its temperature row as
NewLine → DrawIcon → DrawText(temperature) (SelectToolHoverTextCard.cs:714-727).
Our prefix fired on that DrawText, i.e. AFTER the temperature row's NewLine and
DrawIcon had already executed. So: (1) the temperature row's own NewLine+DrawIcon
produced the empty dash row, and (2) after our two inserted rows the original
temperature DrawText ran at the end of our "Всего" row, gluing "63.9 °C" onto it.
The identical action order would occur with BIC on (recorded, then re-rendered),
so the fix is valid for both cases.

Fix: move the insertion to the NewLine that PRECEDES the temperature row — the
one immediately after the mass row. It is uniquely identifiable: the mass row is
up to four consecutive DrawText calls over HoverTextHelper.MassStringsReadOnly(cell)
(public static, returns a 4-element shared cached array; SelectToolHoverTextCard.cs:704-713
draws all elements with no NewLine between), and nothing else is drawn between the mass row
and that NewLine. So the last text drawn just before the NewLine equals
arr[arr.Length - 1] (the " " + GetBreathableString element). Mechanism:
FieldInfoDrawTextPatch (4-arg DrawText prefix, priority 1001) is now a pure
recorder (NoteText/LastText, never draws, never skips); new FieldInfoNewLinePatch
(NewLine(int) prefix, priority 1001) compares LastText to the expected final mass
string and, on match, clears pending and calls DrawLines with slot "new-line".
Re-entrancy is safe: pending is cleared before the re-entrant NewLine/DrawIcon/DrawText
calls. With BIC on the re-entrant calls are recorded before the card's temperature-row
actions so the re-render keeps our rows between mass and temperature; without BIC they
draw live in place. Mod.cs gained a third verify-then-register block for the NewLine prefix.
Commit 4c7aa6d. DLL: 345 088 bytes.
