# Research: PrinterEasyInfo

Superficial context collection. Append findings as they are discovered.

- Game root: `~/ONI/game/` (ro); decompiled sources: `/home/apkawa/code/ONI_MODS/lib_sources/Assembly-CSharp/` and `Assembly-CSharp-firstpass/`.
- Mod workspace: `/home/apkawa/code/ONI_MODS/Apkawa_ONI_Mods`; build: `dotnet build ONI-mods.sln` (net48, game 0Harmony v2, PLib from UtilLibs).
- Template: `UpdatedOniTemplate/` (not in sln, never build).

## Findings

### Clarification (user)

- The book icon goes ONLY in non-duplicant columns (care-package columns); duplicant columns have the pencil rename icon, care-package ones don't. Orientation criterion: presence/absence of the rename icon.

### Scheme selection window ("Выберите схему" / "Select a Blueprint")

- Window root class: **`ImmigrantScreen`** (`lib_sources/Assembly-CSharp/ImmigrantScreen.cs`), subclass of **`CharacterSelectionController : KModalScreen`** (`CharacterSelectionController.cs`). Singleton `ImmigrantScreen.instance`. Entry API: `ImmigrantScreen.InitializeImmigrantScreen(Telepad)`.
- Opened by `TelepadSideScreen.cs` (`viewImmigrantsBtn.onClick` → `ImmigrantScreen.InitializeImmigrantScreen(targetTelepad)`).
- Window always shows **4 columns**: `numberOfCarePackageOptions` = 1–2, `numberOfDuplicantOptions` = 4 − that (see `CharacterSelectionController` — care package count random 1 or 2 when CarePackages setting enabled, else 3 duplicants + 0 care packages). Columns created in `CharacterSelectionController.InitializeContainers()` (protected virtual) via `Util.KInstantiateUI<CharacterContainer>(containerPrefab.gameObject, containerParent)` / `Util.KInstantiateUI<CarePackageContainer>(...)`.
- Duplicant column class: **`CharacterContainer`** (`CharacterContainer.cs`), `: KScreen, ITelepadDeliverableContainer`. Header = `characterNameTitle` (`EditableTitleBar`) — name label + pencil `editNameButton` KButton (sprite assigned in prefab, asset `icon_pencil`). Rename: `EditableTitleBar.OnEndEdit` → `OnNameChanged` → `CharacterContainer.OnNameChanged` → `stats.Name = newName; stats.personality.Name = newName`.
- Care-package column class: **`CarePackageContainer`** (`CarePackageContainer.cs`) — not yet researched in detail.
- Data per column: duplicant columns hold **`MinionStartingStats : ITelepadDeliverable`** (`MinionStartingStats.cs`); no ItemScheme/item id in duplicant columns.
- Proceed: `ImmigrantScreen.OnProceed()` → `telepad.OnAcceptDelivery(selectedDeliverables[0])` → `MinionStartingStats.Deliver(Vector3)`.
- Build-specific: no `KPrefab` class in this build (2026 build U59-744825-SCRPAN); prefab identity = `KPrefabID`/`KPrefabIDTracker` (Assembly-CSharp-firstpass). `KScreen` in firstpass, `screenName = GetType().ToString()`.
- `MinionSelectScreen : CharacterSelectionController` reuses the same layout for new-game crew pick (out of scope).

### Care-package columns (`CarePackageContainer.cs`)

- Class: `CarePackageContainer : KScreen, ITelepadDeliverableContainer`; 0–2 per window; instantiated in `CharacterSelectionController.InitializeContainers()` (70% → 1, else 2; only when CarePackages setting enabled and not starter screen).
- Item identity = **`CarePackageInfo.id`** (plain `string`: element tag string *or* prefab/config id), public via `container.Info.id` (`public CarePackageInfo Info => info`) and `container.carePackageInstanceData.info.id`. Quantity = `Info.quantity`; optional `facadeID`.
- **Header of a care-package column:** only `characterName` (LocText) + `titleBar` (Image, selection color) + `reshuffleButton` (KButton). **No TitleBar/EditableTitleBar, no pencil, no dice** — the pencil slot is empty.
- Duplicant header: `EditableTitleBar characterNameTitle` (pencil `editNameButton` + dice `randomNameButton` + input field).
- Generation: `OnSpawn → DelayedGeneration (WaitForEndOfFrame) → GenerateCharacter(bool)` → `Immigration.Instance.RandomCarePackage()`; `Reshuffle(bool)` private reruns generation (wired via prefab `reshuffleButton` + `controller.OnReshuffleEvent`).
- Best Harmony targets: postfix `CarePackageContainer.GenerateCharacter(bool)` (covers initial + reshuffle, `info` set on return); `CharacterSelectionController.InitializeContainers()` (protected virtual); `CarePackageContainer.SetController(...)`.
- Delivery: `CarePackageInfo.Deliver` → `CarePackageConfig.ID` prefab → `CarePackage.SpawnContents()` spawns `Assets.GetPrefab(info.id)` / `ElementLoader.GetElement(info.id.ToTag())` — the game's own id→kind branch (prefab/item vs element) is what `CarePackageContainer.SetAnimator()` uses.
- Tech-database mapping: item-payload ids match tech-tree item ids (`Db.Get().Techs.TryGetTechForTechItem(id)`, `Db.Get().TechItems.TryGet(id)`, `TechItem.parentTechId`); **element-payload ids have no Item entry** — mapping must branch prefab vs element like `SetAnimator()` does. NOTE: `TechTree.cs`, `Item.cs`, `TechDatabaseUI.cs` are absent from this decompilation.

### Book icon in item properties (= Codex entry button)

- In this build the item-properties window is **`DetailsScreen`** (`DetailsScreen.cs`), a `KTabMenu` with singleton `DetailsScreen.Instance`. `ItemProperties`/`TechDatabaseUI`/`TechTree`/`Item` do NOT exist (verified against fresh decompile of the real DLL); the "tech database" is now the **Codex** (`CodexScreen`, `CodexCache`).
- Book icon = `[SerializeField] private KButton CodexEntryButton` (`DetailsScreen.cs:197-198`), wired in `OnSpawn()`: `CodexEntryButton.onClick += CodexEntryButton_OnClick` (line 384).
- Callback: `CodexEntryButton_OnClick()` → `string id = CodexEntryButton_GetCodexId(); if (id != "") ManagementMenu.Instance.OpenCodexToEntry(id);` (DetailsScreen.cs:556-563).
- Id resolver `CodexEntryButton_GetCodexId()` (DetailsScreen.cs:502-547): element ids via `ICellSelectionProxy.Element.id`, then `CodexEntryRedirector.CodexID`, then prefab ids (creatures via `KSelectable.PrefabID()` with "BABY" stripped, `PlantableSeed.PrefabID()`), then `UI.ExtractLinkID(GetProperName())` / `PrefabID()` fallback; everything wrapped in `CodexCache.FormatLinkID`; returns `""` if no entry exists.
- Canonical opener: **`ManagementMenu.OpenCodexToEntry(string id, ContentContainer targetContainer = null)`** (ManagementMenu.cs:741-749): toggles Codex open if needed, `codexScreen.ChangeArticle(id)`, `FocusContainer`. `CodexScreen.ChangeArticle` (CodexScreen.cs:600): unknown id → `PAGENOTFOUND` article; disabled entry → `PAGENOTFOUND`.
- Button state/tooltip pattern: `CodexEntryButton_Refresh()` sets `isInteractable = id != ""` and tooltip `UI.TOOLTIPS.OPEN_CODEX_ENTRY` / `UI.TOOLTIPS.NO_CODEX_ENTRY`.
- Icon sprite: **`OverviewUI_database_icon`** (Sprite in `sharedassets0.assets`), assigned in the `DetailsScreen` prefab (`CodexButton/Image` node), NOT in code.
- Existing mod reusing exactly this: `lib_sources/peterhaneve_ONIMods/FastTrack/UIPatches/DetailsPanelWrapper.cs` — reads `screen.CodexEntryButton`, Harmony-patches `DetailsScreen.CodexEntryButton_OnClick`, re-dispatches to `ManagementMenu.Instance.OpenCodexToEntry(codexLink)`.
- For care-package ids: the resolver logic must be replicated for a plain id string — element ids via `ElementLoader`/`CodexCache.FormatLinkID(element.id)`, item/prefab ids via `Assets.GetPrefab(id)` → `GetProperName()` link extraction / `PrefabID()` — same two-way branch as `CarePackageContainer.SetAnimator()`.

### Repo structure for a new mod

- **Template** `UpdatedOniTemplate/` does not exist as a folder; concrete copy = `example_mods/Sgt_Imalas-Oni-Mods/UpdatedOniTemplate.zip` (extracted to `.tmp/updatedoni_template/`). Template csproj: `net48`, `IsMod/GenerateMetadata/IsPacked=true`, single `ProjectReference` to `UtilLibs`; game DLL refs, publicizer, ILRepack come from root `Directory.Build.props`/`Directory.Build.targets`. Template `Mod.cs`: `UserMod2` + `[HarmonyPatch]` attributes + `base.OnLoad(harmony)` (PatchAll).
- **Solution** `ONI-mods.sln` currently has **7 projects** (BuildDoorOverWall, UtilLibs, SizeInTooltip, ReplaceBuildingMaterial, BestBuildDryWall, SpaceOverlay, ResourceRemain) — AGENTS.md "only 2" is stale. MSBuild sln has **no comment support**: add/remove = `Project(...)` line + `EndProject` + 4 `ProjectConfigurationPlatforms` lines (never comments). Convention documented in `CONTRIBUTING.md` (lines 98-127). New mods get a row in the root `README.md` table.
- **Repo Harmony style is programmatic**: `UtilLibs/PatchUtil.TryPatch(harmony, type, name, paramTypes, feature, prefix:, postfix:, transpiler:)` — reflection-based, patches **private** methods (proven by `BuildTool.TryBuild(int)` postfix in BuildDoorOverWall). Example mods also use attribute style (`[HarmonyPatch(typeof(CarePackageContainer), "GetSpawnableQuantityOnly")]` in `example_mods/aki-art_ONI-Mods/PrintingPodRecharge/Patches/CarePackageContainerPatch.cs`).
- **Adding a KButton to an existing prefab in code** (canonical example `example_mods/Sgt_Imalas-Oni-Mods/ClusterTraitGenerationManager/Patches.cs:145-150`): anchor via an existing transform, `Util.KInstantiateUI(prefab, parentGO, worldPositionStays:true)`, `UIUtils.TryFindComponent<Image>(t,"FG").sprite = Assets.GetSprite("icon_gear")`, wire `ToolTip` + `KButton.onClick`.
- **Sprite by name**: game-native `Assets.GetSprite(HashedString)` (`Assets.cs:410`) — `Assets.GetSprite("OverviewUI_database_icon")`; PLib alternative `PUITuning.Images.GetSpriteByName(name)`.
- **Deployment**: `<Mod>/bin/Debug/net48/<Mod>.dll` (packed, UtilLibs+PLib inlined); `CopyModsToDevFolder` → `$(ModFolder)/<Mod>_dev` (fails read-only in sandbox — expected, artifacts stay in bin). `Directory.Build.props.user`: `GameLibsFolder=$(HOME)/ONI/dlls`, `ModFolder=$(SolutionRoot).tmp/build_mod_dir`.
- **Logging**: `PUtil.LogDebug/LogWarning/LogError` + `.F(...)`, no mod-name prefix (auto-prepended), verbose in `#if DEBUG`.

### Column prefab hierarchies (sharedassets2.assets)

- `CharacterContainer` header: `CharacterContainer → TitleBar` (hosts `EditableTitleBar` component) → children `BG` (root `titleBar` field), `CharacterName` (root `characterName` LocText), **`RenameButton` (the pencil, KButton; child `Image` holds glyph)**, `LabelGroup` (`Label`, `LocTextInputField`, `RandomizeButton`). Pencil parent chain: `CharacterContainer → TitleBar → RenameButton`.
- `CarePackageContainer` header: `CarePackageContainer → TitleBar` (plain node, no EditableTitleBar) → children `BG` (root `titleBar` Image field), `LabelGroup → Label` (root `characterName` LocText). **No pencil node exists** — the pencil slot is empty; `ShuffleDupeButton` (root `reshuffleButton`) sits outside the title bar.
- Best anchor for the new book button: parent under `container.transform.Find("TitleBar")` in the care-package column (deep-copy approach: clone `CharacterContainer → TitleBar → RenameButton` there — it is absolutely laid out inside the same header row → lands at the pencil's visual position; no overlap since care-package `TitleBar` has only `BG` + `LabelGroup`). Runtime fallback if `Find("TitleBar")` is null: `characterName.transform.parent.parent` (Label → LabelGroup → TitleBar).
- No hardcoded `transform.Find` paths in `CarePackageContainer.cs`/`CharacterContainer.cs`/`EditableTitleBar.cs`. Example mod precedent: `example_mods/aki-art_ONI-Mods/PrintingPodRecharge/Patches/ImmigrantScreenPatch.cs` resolves `"Details/PortraitContainer/BG"` from the KScreen instance transform and adds a button next to a serialized button.
- Note: the pencil KButton's sprite is assigned in the prefab (not in code) — a cloned node keeps its sprite automatically.

### Modal close for the selection window (z-order bugfix, v1.0.1)

- `KScreen` has NO `Parent` property and NO `CloseScreen()` method (verified: full read of `KScreen.cs`; no `ScreenUtil` close coroutine in the game assemblies).
- Columns are parented under the controller's `[SerializeField] private GameObject containerParent` (`CharacterSelectionController.cs:15,94`) — the window must be found by walking `transform.parent` up until a `CharacterSelectionController` is found; that controller IS the window (`CharacterSelectionController : KModalScreen`, `ImmigrantScreen : CharacterSelectionController`).
- The game's own non-destructive close for this window is synchronous: `CharacterSelectionController.OnPressBack()` → `Show(show: false)` (`CharacterSelectionController.cs:103-114`; same call from the window's close button and `ImmigrantScreen.Deactivate`). `KScreen.Deactivate` (the generic `KModalScreen` ESC path) DESTROYS the object — avoided.
- `ManagementMenu.OpenCodexToEntry(id, targetContainer = null)` (`ManagementMenu.cs:738`) works regardless of which screen is open (ToggleCodex + ChangeArticle) — so close-then-open in one click event is correct; both the "codex on top, double-ESC" and "codex behind" symptoms stem from two modals being open at once.


Add a "book" icon to each of the 4 columns in the print scheme selection window ("Выберите схему"), at the same position as the name-editing pencil icon; clicking it opens the tech database entry for the scheme's item (same icon/logic as the book icon in item properties). New mod: `PrinterEasyInfo`.
