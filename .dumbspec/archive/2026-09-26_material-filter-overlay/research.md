# Research: MaterialFilterOverlay

Findings from decompiled game source (`/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/`) and the mod repo.

## 1. The overlay class and its category list

- The "Схема материалов" overlay (F4) is the **`TileMode`** mode, nested in `OverlayModes` (`lib_sources/Assembly-CSharp/OverlayModes.cs` line ~3412).
  - `STRINGS.INPUT_BINDINGS.ROOT.OVERLAY4` = "Materials Overlay" → RU "Схема материалов"; F4 = `Action.Overlay4`.
- `OverlayModes.cs:3412`:

```csharp
public class TileMode : Mode          // nested inside public abstract class OverlayModes
{
    public static readonly HashedString ID = "TileMode";
    private HashSet<PrimaryElement> layerTargets = new HashSet<PrimaryElement>();
    private HashSet<Tag> targetIDs = new HashSet<Tag>();
    private int targetLayer;
    private int cameraLayerMask;
    public override HashedString ViewMode() { return ID; }
}
```

- **`OverlayScreen`** (`OverlayScreen.cs`): `public static OverlayScreen Instance`; `private void RegisterModes()` (line 143); public `void ToggleOverlay(HashedString newMode, bool allowSound = true)` (line 182).
- **`OverlayLegend`** (`OverlayLegend.cs`): `KScreen`, `public static OverlayLegend.Instance`; `[SerializeField] private List<OverlayInfo> overlayInfoList` (prefab-serialized). `public void SetLegend(OverlayModes.Mode mode, bool refreshing = false)` (line 203) → for programmatically-populated modes builds the filter menu in `PopulateGeneratedLegend` (line ~407): instantiates `toolParameterMenuPrefab` (a serialized prefab field), `filterMenu.PopulateMenu(currentMode.legendFilters)`, `filterMenu.onParametersChanged += OnFiltersChanged`. `private void OnFiltersChanged()` (line 419) → `currentMode.OnFiltersChanged(); PopulateGeneratedLegend(..., isRefresh:true); Game.Instance.ForceOverlayUpdate();`.
- **Category list is hardcoded** in `TileMode.CreateDefaultFilters()` (`OverlayModes.cs` line ~3510): 11 `ToolParameterMenu.ToggleData` entries: `ALL, METAL, BUILDABLE, FILTER, CONSUMABLEORE, ORGANICS, FARMABLE, LIQUIFIABLE, GAS, LIQUID, MISC`.
- **`ToolParameterMenu`** (`ToolParameterMenu.cs` line 7):
  - `public class FILTERLAYERS` (line 9): **string constants** (`METAL="METAL"`, ...).
  - `public class ToggleData { public string name; public bool isToggleInclusive; public ToggleState state; }`; `public enum ToggleState { On, Off, Disabled }`.
  - `public void PopulateMenu(ToggleData[])` (line 151); `public event Action onParametersChanged`.
  - Labels/tooltips from localization: `Strings.Get("STRINGS.UI.TOOLS.FILTERLAYERS." + name + ".NAME"/".TOOLTIP")` (line ~177) → **custom toggle names render empty labels unless localization is injected**.
  - **Mutual exclusivity**: non-inclusive toggles are single-select in `ChangeToSetting`; `isToggleInclusive: true` stays independent (needed for multi-select sub-filters).
- F4 bar entry: `OverlayMenu.InitializeToggles()` hardcodes `OverlayToggleInfo(..., OverlayModes.TileMode.ID, "", Action.Overlay4, ...)`; `OverlayToggleInfo`/`overlayToggleInfos` are private.

## 2. Highlight pipeline

1. `TileMode.OnFiltersChanged()` (`OverlayModes.cs:~3528`) — checkbox → tags into **`Game.Instance.tileOverlayFilters`** (`public List<Tag>`, `Game.cs:508`), e.g. METAL → `GameTags.Metal + GameTags.RefinedMetal`; MISC → `GameTags.Other`; etc. Then `Game.Instance.ForceOverlayUpdate()`.
2. `TileMode.Update()` (line ~3475) — visits `GameScenePartitioner.Instance.pickupablesLayer` + `completeBuildings` with `TryAddObject`.
3. `TileMode.TryAddObject(PrimaryElement pe, Vector2I min, Vector2I max)` (line ~3488, **private**): for each tag in `Game.Instance.tileOverlayFilters`, if `pe.Element.HasTag(tag)` → `AddTargetIfVisible(pe, min, max, layerTargets, targetLayer)`.
4. `Mode.UpdateHighlightTypeOverlay<T>` (line ~2143, protected): first `ColorHighlightCondition` match → `HighlightColour`; TileMode colours by `primary_element.Element.substance.uiColour`.
- `Mode.InFilter(string layer, ToggleData[] filter)` is **protected**; `Mode.legendFilters` **public** (`OverlayModes.cs:1882`).
- `TileMode.Enable()` (line ~3454) precomputes `targetIDs.UnionWith(Assets.GetPrefabTagsWithComponent<PrimaryElement>())`, switches camera layer masks.
- Discovery state is **not** checked in `TryAddObject` — undiscovered materials still highlight.

## 3. Materials and categories

- `Element` (`Element.cs`): `public Tag tag` (30), `public Substance substance` (110), **`public Tag materialCategory`** (112), `public Tag GetMaterialCategoryTag()` (288).
- `ElementLoader` (`ElementLoader.cs`): **`public static List<Element> elements`** (line 14, all elements from JSON), `public static Element GetElement(Tag)`, `public static List<Element> FindElements(Func<Element,bool> filter)` (line 212). `materialCategory` comes from the element JSON `materialCategory` field (must be in `GameTags.MaterialCategories`).
- `GameTags.MaterialCategories` (`GameTags.cs:1055`) = `TagSet { Alloy, Metal, RefinedMetal, BuildableRaw, BuildableProcessed, Filter, Liquifiable, Liquid, Breathable, Unbreathable, ConsumableOre, Sublimating, Organics, Farmable, Agriculture, Other, ManufacturedMaterial, CookingIngredient, RareMaterials }`.
- **All materials of a category**: `ElementLoader.FindElements(e => e.materialCategory == cat)`. No `MaterialInfo` class, no `IsOfType` API.
- Not to be confused: `MaterialSelector`/`MaterialSelectionPanel` are the recipe ingredient picker.

## 4. Existing mods + PLib

- Solution `ONI-mods.sln` (currently only BuildDoorOverWall + UtilLibs). Shared `Directory.Build.props` provides game-dll refs; TFM **net48**; game's own **0Harmony.dll v2** (never NuGet Harmony); ILRepack pack; `UtilLibs` carries PLib 4.19.0 (NuGet).
- **BuildDoorOverWall** pattern: `namespace OxygenNotIncluded.Mods { public class Mod : UserMod2 { OnLoad(Harmony) → PatchUtil.TryPatch(...) } }`; `UtilLibs.PatchUtil.TryPatch(harmony, type, name, paramTypes, feature, prefix/postfix/transpiler)`.
- **SpaceOverlay** — working example of overlay-mode extension:
  - `public class SpaceOverlayMode : OverlayModes.Mode` with `public static readonly HashedString ID`.
  - Registration via `PatchUtil.TryPatch`: postfix on `OverlayScreen.RegisterModes` (private) then `AccessTools.Method(typeof(OverlayScreen), "RegisterMode")` invoke; postfix on `OverlayMenu.InitializeToggles`; postfix on `OverlayScreen.OnSpawn` to re-activate after level load; postfix on `SimDebugView.OnPrefabInit` for `getColourFuncs`; writes `StatusItem.overlayBitfieldMap` via `AccessTools.Field` to stop hover-card log spam.
  - Icon loading: `PUIUtils.LoadSpriteFile(path)`.
- **PLib UI helpers** (`PeterHan.PLib.UI`): `PPanel` (AddChild, Build, PanelDirection, Spacing, SetKleiBlue/PinkColor), `PCheckBox` (`OnChecked(GameObject, int state)`, `InitialState`, `STATE_UNCHECKED/CHECKED/PARTIAL`), `PLabel`, `PButton`, `PScrollPane`, `PDialog`, `PUIUtils.LoadSpriteFile`.

## 5. Element display-name localization (verified)

- **`Element.name` is ALREADY the localized display name** — `ElementLoader.Load` (`ElementLoader.cs:81`): `element.name = Strings.Get(item.localizationID);`, where `localizationID` comes from the element YAML (e.g. `localizationID: STRINGS.ELEMENTS.COPPER.NAME` in `~/ONI/game/OxygenNotIncluded_Data/StreamingAssets/elements/solid.yaml`).
- Key format: **`STRINGS.ELEMENTS.<ELEMENT_ID_UPPERCASE>.NAME`** (variants `.DESC`, `.BUILD_DESC`); dynamic form: `Strings.Get("STRINGS.ELEMENTS." + id.ToString().ToUpper() + ".NAME")`.
- RU examples: `STRINGS.ELEMENTS.COPPER.NAME` → `<link="COPPER">Медь</link>`, `STRINGS.ELEMENTS.GOLD.NAME` → `<link="GOLD">Золото</link>`.
- **The value contains `<link="ID">…</link>` markup** — strip it (or let LocText render it) when putting it in a plain PLabel.
- Internal identifier is the YAML `elementId` string (e.g. "Copper"); the tag is `element.tag` (SimHash).

## Pitfalls / constraints

- Private members needing AccessTools/patches: `OverlayScreen.RegisterMode(s)`, `OverlayScreen.modeInfos`, `OverlayMenu.InitializeToggles`, `OverlayLegend.overlayInfoList` / `filterMenu` / `toolParameterMenuPrefab` / `diagramsParent` / `PopulateGeneratedLegend`, `TileMode.layerTargets/targetIDs/TryAddObject`, `SimDebugView.getColourFuncs`, `ElementLoader.elementTable/elementTagTable`. No publicizer needed at runtime — AccessTools + Harmony postfixes suffice (SpaceOverlay pattern).
- Public and directly usable: `OverlayModes.Mode` (public abstract; override `CreateDefaultFilters`/`OnFiltersChanged`/`Update`/`ViewMode`), `Mode.legendFilters`, `ToolParameterMenu.PopulateMenu/ClearMenu/onParametersChanged`, `Game.tileOverlayFilters`, `Game.ForceOverlayUpdate`, `OverlayScreen.ToggleOverlay`, `OverlayLegend.SetLegend/ClearLegend/GetOverlayInfo`, `ElementLoader.elements`, `Element.materialCategory`.
- **Localization**: custom `ToggleData.name` → empty labels unless `STRINGS.UI.TOOLS.FILTERLAYERS.<name>.NAME`/`.TOOLTIP` exist (inject via KMod localization or patch `Strings.Get`).
- **Mutual exclusivity**: use `isToggleInclusive: true` for the multi-select sub-filters.
- **OverlayScreen/OverlayLegend/OverlayMenu are recreated per level** — re-register + re-activate after load (postfix `OnSpawn`/`RegisterModes`).
- TileMode highlights whole `PrimaryElement` objects/buildings from `GameScenePartitioner` layers, not raw cells.
- `ToolParameterMenu.OnPrefabInit` calls `ClearMenu()`; the legend's `toolParameterMenuPrefab` is serialized on the OverlayLegend prefab.
- Build invariants: sln has no comment support; net48 only; `tile_rep.dll` never globbed; only BuildDoorOverWall + UtilLibs in sln currently.

## Stage 6 diagnosis (2026-09-27, radio dot/border rendering bug)

Rendering pipeline per category row (PCheckBox, PLib 4.19.0):
- `CheckBorder` image (child of the 16x16 "CheckBox" image): restyled by `MakeRadioStyle` to the generated ring; **independent of toggle state** — this image alone determines the unchecked look.
- `CheckMark` image (`MultiToggle.toggle_image`, 12x12, rendered on top of the border): `MultiToggle.ChangeState` sets `sprite = states[i].sprite`, `color = states[i].color`. State 0 (unchecked) has color AND hover color fully transparent (`PCheckBox.GenerateStates`, PCheckBox.cs:119-122) → **state-0 sprites can never render**; state 1/2 are white with the generated dot sprite.
- `MultiToggle.OnPointerClick` never advances its own state; state changes come from the mod's `ApplySelection`.
- PLib `UIDetours.CHANGE_STATE`/`SetCheckState` are **silent no-ops at runtime** (empirical, documented in commit `45128ea` ForceCheckState docstring); HEAD therefore uses `ForceCheckState` (direct `ChangeState(state, forceRefreshState: true)` + direct sprite/color assignment, LegendPanel.cs:655-681) — verified working by in-game DEBUG log and screenshot (selected row = filled white disc).

Root cause of the user's regression:
- Inverting `hollow` in `MakeCircleSprite` swaps the two cached sprites: `GetRadioBorderSprite()` (hollow:true) becomes a filled disc, `GetRadioDotSprite()` (hollow:false) becomes a ring.
- Unchecked row: border image = filled white disc → plain white circle (state-0 mark sprite is invisible regardless).
- Checked row: border image = filled disc + mark = white ring on top of white disc → zero contrast, "no dot".
- `else { state.sprite = border; }` is dead code (state 0 never renders) and conceptually wrong.

Fix decision: **fully revert the uncommitted LegendPanel.cs diff** (`git checkout -- MaterialFilterOverlay/LegendPanel.cs`); keep HEAD `ForceCheckState` path. Expected result: unchecked = hollow ring, checked = ring + filled dot (reads as filled disc at 12px-over-16px).

Residual uncertainty: if the "no dot on selected" symptom reproduces on a fresh HEAD build, suspect the swallowing try/catch inside `MultiToggle.ChangeState` (MultiToggle.cs:91-110) — but `ForceCheckState`'s unwrapped direct assignment should apply the sprite regardless; settle by user's in-game check.

Note: dot (12px) overlaps ring stroke (16px), so "checked" reads as a filled disc of the ring size — sizing is a design choice, not a bug.

## Stage 6 follow-up (2026-09-27): user screenshot pixel analysis

User reported "dot should be on Металл only, empty elsewhere" after the revert build. Pixel analysis of their screenshot (`.tmp/radio2.png`):
- 11 circles at row centers y = 51, 77, 102, 123, 143, 163, 184, 204, 225, 245, 266.
- Only the row at y=77 (the pink-highlighted row with the "+" button, i.e. selected "Металл") has a FILLED white center; the other 10 have hollow centers (center pixel = pink square (135,69,102) showing through the ring hole).
- Conclusion: the build renders exactly the intended design (selected = filled dot, unselected = hollow ring). The user perceived rings as dots because at ~14px circle size the ~7px hole is hard to see in a small screenshot. No code change required; cosmetic option offered: enlarge the ring hole (smaller 0.55 factor / thinner ring) for better contrast.

## Stage 7: vanilla marker style reference (user-provided example)

Pixel analysis of the vanilla «СХЕМА МИКРОБОВ» tab screenshot (`.tmp/vanilla_example.png`):
- Marker = white square (~17px) on every row, always visible.
- Selected row ("Все"): centered BLACK dot (0,0,0) inside the white square (~6px wide, from x=288..295 of a 284..300 square).
- Unselected rows: plain white square, empty center (no dot).
- User's semantics for the mod's radio rows: «белый кружок без точки» = unselected, «с точкой» = selected. A solid white disc does NOT read as "with a dot".

Design decision (Stage 7): mirror vanilla with circles — `CheckBorder` = solid white disc (always visible); checked state mark = small BLACK dot on transparent bg (centered, ~1/3 of marker width); unchecked state = no mark (transparent, per PCheckBox state-0 colors). `ForceCheckState`/`ApplySelection` unchanged.
