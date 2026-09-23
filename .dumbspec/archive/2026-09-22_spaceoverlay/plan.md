# 2026-09-22_spaceoverlay: implementation plan (SpaceOverlay — оверлей «Воздействие космоса»)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
For this repo the "test" is `dotnet build ONI-mods.sln -c Debug` (the only automated verification); in-game behavior is accepted by the user manually.
This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Capture raw input → `draft.md` (verbatim, incl. GitHub issue Apkawa/ONI-Mods#5)
- [x] Initial spec skeleton + refinement gate with the user (toggleable like game overlays; muted translucent red)
- [x] Superficial research → `research.md` (repo structure, template, assets, overlay system, space rendering, PLib)
- [x] Refine spec to v1 (Created header + Changes)
- [x] Write this plan

**Criterion:** `draft.md`, `research.md`, `spec.md` (v1), `plan.md` all exist in `.dumbspec/current/2026-09-22_spaceoverlay/`.
**Commit:** `docs(spaceoverlay): spec, research and plan for SpaceOverlay mod`

## Stage 1 — Deep research: vacuum tint + overlay wiring details
- [x] Pin down `GroundRenderer` chunk-mesh internals: exact method(s) building per-chunk vertex/mesh data, material/`ShaderMaterial` used, how to append custom quads for individual cells (decompile missing classes from `~/ONI/dlls/Assembly-CSharp.dll` with ilspycmd if the decompiled tree is incomplete)
- [x] Identify how chunks get marked dirty/rebuilt so the mod can force a redraw when the overlay toggles on/off
- [x] Verify built-in `overlay_*` icon sprite sizes in game `Assets` (match our icon size) and how `KIconToggleMenu`/`OverlayToggleInfo` resolve the icon name
- [x] Confirm `InjectionMethods.AddSpriteToAssets` is the right way to make our sprite visible to the overlay menu
- [x] Decide final patch design (target methods + signatures, material strategy, toggle on/off mechanics, no-hotkey for the 16th toggle) and append the decision to `research.md`

**Decision:** mechanism (a) — self-drawn full-grid vacuum mesh (`Graphics.DrawMesh` in `Mode.Update()`, one quad per vacuum cell, `Sprites/Default` material, muted red, `WorldTransparent` queue); patches: postfix `OverlayScreen.RegisterModes`, postfix `OverlayMenu.InitializeToggles` (icon via `getSpriteCB`, 48×48, `Action.NumActions`), postfix `GroundRenderer.MarkDirty` (rebuild flag); world-load via `Game` event 1983128072.
**Criterion:** `research.md` contains a concrete, implementable patch design (exact target methods, material strategy, toggle mechanics).
**Commit:** `docs(spaceoverlay): deep research on vacuum tint patch point`

## Stage 2 — Icon (SVG → PNG via rsvg-convert)
- [x] Draw `SpaceOverlay/icon.svg` (space-themed glyph in the style of the built-in `overlay_*` icons, 48×48 viewBox, transparent background) — Saturn-style planet with occluding ring + star sparkle, `#E6E6E6`/`#8A8A8A` line art
- [x] Add the SVG→PNG step (`SpaceOverlay/make_icon.sh`, `rsvg-convert -w 48 -h 48`) converting `icon.svg` → `SpaceOverlay/ModAssets/overlay_space.png`
- [x] Green state: PNG exists in `ModAssets/`, verified 48×48 RGBA (2408 bytes), visually verified

**Criterion:** `SpaceOverlay/ModAssets/overlay_space.png` exists and is produced from `icon.svg` by `rsvg-convert`.
**Commit:** `feat(spaceoverlay): overlay icon svg + rsvg-convert build step`

## Stage 3 — Mod project + overlay registration
- [x] Red state: copy `BuildDoorOverWall/` structure into `SpaceOverlay/` (csproj, `Mod.cs`, README placeholder), add to `ONI-mods.sln`; build fails until code in place (CS0246 `SpaceOverlayMode`)
- [x] Implement `SpaceOverlayMode : OverlayModes.Mode` (`static readonly HashedString ID = "SpaceOverlay"`, `ViewMode()`, `Enable()`/`Disable()`/`Update()`)
- [x] Harmony patches via `PatchUtil.TryPatch`: postfix `OverlayScreen.RegisterModes`, postfix `OverlayMenu.InitializeToggles` (`OverlayToggleInfo` via reflection, `getSpriteCB`, `Action.NumActions`), postfix `GroundRenderer.MarkDirty` (`Dirty` flag)
- [x] Icon wiring: `PUIUtils.LoadSpriteFile` (mod-folder root + `ModAssets/` fallback) → `getSpriteCB` on the toggle
- [x] Wire `make_icon.sh` into the build (MSBuild `ConvertIcon` target, verified to run during build)
- [x] Green state: `dotnet build ONI-mods.sln -c Debug` passes (0 errors); `.tmp/build_mod_dir/SpaceOverlay_dev/` has dll + yamls + `overlay_space.png`
- [x] Logging per repo conventions (`PUtil.*` + `.F()`, no mod-name repetition — fixed after acceptance)

**Criterion:** Build passes; the mod assembly is produced and the toggle registration code compiles against the game types.
**Commit:** `feat(spaceoverlay): project skeleton + overlay menu registration`

## Stage 4 — Red translucent tint for vacuum cells
- [x] Implement the vacuum-tint per the Stage-1 design (one quad per vacuum cell, `Sprites/Default` material, muted red, `WorldTransparent` queue, drawn in `Mode.Update()`) — shipped together with the Stage-3 mode class
- [x] Independent review vs decompiled sources: cell geometry PASS (canonical cell bounds); **FIXED double-applied z** (vertices local z=0, single z source in `Draw()`); material created once (now static, no per-load leak); lifecycle **FIXED** (static `Active` flag, exception-free world-change handler, new postfix `OverlayScreen.OnSpawn` for guarded re-activation); rebuild triggers PASS; registration reflection PASS (verified `OverlayToggleInfo` ctor, `overlayToggleInfos` field, `getSpriteCB`)
- [x] Green state: build passes (0 errors)
- [x] Refactor: constants in one place; null-guards with `PUtil.LogWarning`; runtime risks recorded in `research.md` (`### Stage 4 review findings`)

**Criterion:** Build passes; patch attaches (verified by the user in-game: space glows muted red when the overlay is on, map is clean when off).
**Commit:** `feat(spaceoverlay): red translucent tint for vacuum cells`

## Stage 5 — README + repo wiring
- [x] Write `SpaceOverlay/README.md` in English per the `mod-readme` skill (template + DLC matrix, no `# Options`, `# Limitations` with 2 items, changelog `2026-09-23: v0.0.1 Initial release`)
- [x] Root `README.md` mod table row verified (`| [SpaceOverlay](SpaceOverlay/README.md) | Highlights space (vacuum) cells with a red overlay |`)
- [x] Final build `dotnet build ONI-mods.sln -c Debug` green (0 errors)

**Criterion:** Root README lists `SpaceOverlay/README.md`; mod README matches the skill template; final build green.
**Commit:** `docs(spaceoverlay): mod README + root README entry`

---

## Stage 6 — In-game bugfix: tint not visible (user feedback 2026-09-23)

**Symptom (user):** icon shows in the overlay menu; on enable "всё затемнилось, но красного ничего не появилось" (everything darkened, no red). Log shows the mod working (`enabled`, `mesh rebuilt: 106194 vacuum quads`) + a per-frame flood of `ViewMode 0xBFF8590A has no StatusItemOverlay value`.

**Diagnosis (orchestrator, from decompiled sources):**
1. Tint `(0.55, 0.16, 0.16, 0.28)` over the black void ≈ `(0.15, 0.045, 0.045)` — nearly black; reads as darkening, not red. The darkening the user saw IS our mesh.
2. The mode does not use the game's standard overlay rendering, unlike `PipPlantOverlay` and built-in modes: no `CameraController.Instance.ToggleColouredOverlayView(true/false)`, no `MaskedOverlay` layer, no `Camera.main.cullingMask` toggle.
3. The warning flood is `StatusItem.GetStatusItemOverlayBySimViewMode` (called by `SelectToolHoverTextCard.ShowStatusItemInCurrentOverlay`) — our ID is missing from `StatusItem.overlayBitfieldMap`.

- [x] Adopt the PipPlantOverlay/built-in overlay mechanism: `CameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG")`; Enable → `ToggleColouredOverlayView(true)` + cullingMask |=; Disable → cullingMask &= ~ + `ToggleColouredOverlayView(false)`; mesh drawn on `MaskedOverlay` layer (`OverlayLayer`)
- [x] Brighten the tint to `new Color(1.0f, 0.25f, 0.25f, 0.4f)`
- [x] Register `SpaceOverlayMode.ID → StatusItem.StatusItemOverlays.None` in `StatusItem.overlayBitfieldMap` via `AccessTools.Field` in `Mod.OnLoad` (same mapping the game uses for Oxygen/TileMode)
- [x] Green build (0 errors)

**Criterion:** Build passes; user re-tests in-game: red tint clearly visible on space, world dims like other overlays, no warning flood in the log.
**Commit:** `fix(spaceoverlay): use standard overlay rendering + brighter tint + status-item mapping`

### Stage 6 follow-up (debug iteration, still open)

User re-tests so far:
1. World layer + dark tint → mesh renders (visible darkening) but tint too dark to read as red.
2. `MaskedOverlay` layer + `ToggleColouredOverlayView(true)` → world fully desaturated (pixel analysis: R=G=B); no red. `overlay layer id: 19`, 289156 all-cells quads built.
3. All-cells test on `MaskedOverlay` → still "затемнилось, красного нет".
**Conclusion:** the coloured overlay view desaturates the world and crushes the red. Reverted (commit 7947c13): mesh on the `World` layer, no camera/overlay-view changes, tint `(1.0, 0.2, 0.2, 0.45)`.
4. World layer + all-cells + no coloured view → **still nothing drawn** ("затемнение стандартное, нашего ничего не рисуется"). Log 4: `overlay layer id: 11`, 289156 quads, no shader warning.
**Root cause found:** the mesh holds a quad per grid cell (~289k quads = ~1.16M vertices), but `Mesh.indexFormat` defaults to `UInt16` (max 65535) — indices are silently truncated, the mesh is garbage, nothing renders, no log error. Fixed with `indexFormat = IndexFormat.UInt32` (commit 6cbd3ce; this Unity build names the enum `IndexFormat`, not `MeshIndexFormat`).
Corroboration: the game's own `GroundRenderer` draws flat ground quads on the `World` layer via `Graphics.DrawMesh(camera: null)` from its update path — same layer/winding as ours, so once the mesh is intact the same path should work.
5. After the index fix: **the mesh now renders** — but gray, visible over vacuum + buildings, not over rocks.
**Second root cause found:** camera sits at `z = -100` (CameraController line ~1331) looking in the +Z direction — "above the surface" is **-Z**. Our `MeshZOffset = +0.0005` put the quad 0.5mm *below* the ground surface: visible over vacuum, hidden under opaque rocks, bleeding through translucent buildings — exactly matching the user's observation. Fixed with `MeshZOffset = -0.01f` (commit fc94dd8; 1cm also clears the per-element surface offsets `0.0001 * element idx`).
The gray (non-red) appearance: the whole scene is desaturated (screenshot pixels all R=G=B, 0% non-gray) — a coloured overlay view was active, and the desaturation is applied to the **world buffer** (MRT0→`_LitTex` blit). Our quad was on the `World` layer → rendered into that buffer → crushed to gray.
Also: dirt/regolith (high `element_idx`) surfaces sit at `layerZ - 0.0001*idx`, i.e. above our 1cm lift, so they stayed uncovered.
**Fix (commit eff5717):** draw the quad on the **`Overlay`** layer via **`Camera.main`** (overlayNoDepthCamera) — that layer is already in `Camera.main`'s culling mask (`Overlay|Place`), excluded from the world camera's mask, and composited on top of the desaturated `_LitTex` (the game's own colored overlays use the same on-top pattern). Result: the red survives any desaturation AND the quad always sits above the world (fixes dirt/regolith/buried-object coverage). No shared-cullingMask mutation needed.
- [x] User re-test (run 7): **red tint works** ("заливка теперь работает") — the Overlay-layer/Camera.main fix is accepted; the `#if DEBUG` all-cells test was removed by the user (committed with the next change)
6. **Semantic fix (user request):** the check was "cell is vacuum" — wrong. The intended meaning of "Воздействие космоса" is *the cell has no backwall behind it* (a rock face at the space boundary counts; a cell behind drywall does not). The game's own tooltip status uses exactly this: `CellSelectionObject.IsExposedToSpace(cell)` (`CellSelectionObject.cs:266`) = space sub-world zone + `!BackwallManager.HasBackwall(cell)` + no objects on Grid object layers 2/9 + no door. `BackwallManager` data is simulation-computed and **building-aware** (drywall etc. change it), and `Game.cs:1304` fires `groundRenderer.MarkDirty(cell)` on every backwall-element change — already intercepted by our existing MarkDirty postfix, so buildings toggle the overlay live.
   Fix (commit 0077329): `IsVacuum` → `IsExposedToSpace`, delegating to the game method (no reimplementation).
- [x] User re-test (run 8): "примерно как я и хотел" — highlight matches the game's "Воздействие космоса" status cells; two follow-ups requested
7. **Walls/drywall at the space boundary (user request):** building a wall/drywall on a space cell removes the tint (the game's status excludes cells with a backwall building: `Grid.Objects[cell, 2]` — `ObjectLayer` enum: `Building=1, Backwall=2`, walls/drywall sit on layer 2). The simulation backwall data is not changed by the building (the cell element stays vacuum), so "space behind the wall" = `!BackwallManager.HasBackwall(cell)` + cell zone is `Space` (`world.zoneRenderData.GetSubWorldZoneType(cell) == ProcGen.SubWorld.ZoneType.Space`, `SubWorld` lives in Assembly-CSharp-firstpass).
    Fix (commit 7c7dd74): `ShouldHighlight` = game's `IsExposedToSpace` **OR** (backwall building present + `!HasBackwall` + space zone). Also muted the tint alpha 0.45 → 0.35 ("приглушить чуточку").
- [x] User re-test (run 9): drywall works ("гипсокартон красится"); user requested walls + doors too. User then **simplified the predicate themselves** — the ObjectLayer check is not needed: any cell in the Space zone highlights, which covers walls/doors (buildings don't change a cell's zone). "Работает как я и хотел."
8. **Out-of-map bug (user report):** the tint painted the infinite space *outside* the map (screenshot vs the oxygen overlay which stops at the map border). Root cause: the "Space" biome zone extends beyond the map, and the simplified check only tested the zone. The game's per-cell world-membership test is `Grid.IsActiveWorld(cell)` (`Grid.cs:1383`: `WorldIdx[cell]` == active world; outside the map `WorldIdx == byte.MaxValue`).
    Fix (commit 3d6cef9): `ShouldHighlight` = `Grid.IsActiveWorld(cell) && zone == Space`. The whole predicate is now two conditions; `CellSelectionObject`/`BackwallManager`/`ObjectLayer` no longer referenced.
- [x] User re-test (run 10): map-bound fix not re-reported (accepted); two new issues reported: crash on asteroid switch + fog-of-war behavior
9. **Crash on world switch (user report + log `.tmp/Player_spaceoverlay_6.log`):** switching to another asteroid crashed the game: `Mesh.vertices is too small. The supplied vertex array has less vertices than are referenced by the triangles array.`, stack trace in `SpaceOverlayMode:RebuildMesh`. Sequence in the log: the static mesh held 37068 quads (148272 vertices) from asteroid A; during the switch RebuildMesh ran on a half-initialized grid state and produced a quad count larger than the mesh's existing vertex capacity; Unity `SetVertices`/`SetIndices` on the reused mesh left inconsistent buffers and `SetIndices` threw; the retry frame (5259 quads, smaller) succeeded but the game quit.
    Fix (commit 504e48f): `mesh.Clear()` before every `SetVertices`/`SetIndices` in `RebuildMesh` — every rebuild starts from a clean, consistent mesh.
10. **Fog of war (user report):** (a) on a new save the tint showed over unexplored areas (space borders visible "from above" — our Overlay-layer/Camera.main draw is not masked by the game's fog); (b) the tint border is too hard. Root cause: the predicate never consulted per-cell visibility. The game's fog of war is driven by `Grid.Visible[cell]` (byte, 0 = unrevealed) with the public `Grid.IsVisible(cell)` = `Visible[cell] > 0 || !PropertyTextures.IsFogOfWarEnabled` (fog disabled in options ⇒ everything visible). Live updates: first reveal of a cell fires `Grid.OnReveal` (`Action<int>`, public static; game resets the delegate to null on world load; `World`/`PathFinder` append their own handlers).
    Fix (commit 504e48f): `ShouldHighlight` gains `Grid.IsVisible(cell)` (the tint border now coincides with the game's own revealed-terrain border); static `OnRevealHandler` appended to `Grid.OnReveal` in `Enable`/`OnActiveWorldChanged` (re-attach, since the game nulls the delegate on world load) and removed in `Disable`; it marks `Dirty` only when the revealed cell would be tinted, so rock-cell exploration does not trigger rebuilds.
- [ ] User re-test: asteroid switch no longer crashes (the original run-10 trigger) — **superseded by Stage 7** (homegrown rebuild path is being removed)
- [ ] User re-test: no tint over unexplored areas on a new save; tint expands live as duplicants explore the space boundary
- [ ] User re-test: border looks acceptable now that it follows the fog-of-war edge
- [ ] Final tune: tint strength if 0.35 is still off (option: per-cell alpha fade over the reveal gradient)

---

## Stage 7 — Rewrite rendering to the built-in mechanism (user request 2026-09-23)

Goal: replace the homegrown mesh rendering with the game's built-in per-cell overlay machinery (`SimDebugView.getColourFuncs`, PipPlantOverlay pattern) per spec v2. The uncommitted fog-shader experiment was discarded (user decision) — this stage starts from commit 274f04b.

- [x] Deep research: FOW interaction of the built-in mechanism (`SimDebugView.hideFOW`, `Grid.Visible` in `UpdateSimViewWorkItem`/`UpdateData`, how the Oxygen mode looks under FOW); thread-safety of `getColourFuncs` (background thread — direct sim reads vs precompute in `Mode.Update()` on the FG thread); whether the SimDebugView plane colour survives `ToggleColouredOverlayView` desaturation (standard game look); decide legend (`GetCustomLegendData`/`OverlayLegend.OnSpawn`) — append decisions to `research.md`
- [x] Legend: **not needed** (user decision 2026-09-23)
- [x] Red state: strip the homegrown rendering from `SpaceOverlayMode` (mesh/material/`RebuildMesh`/`Draw`/`EnsureAssets`/`MeshZOffset`/`Dirty`/`needsRebuild`) and the `MarkDirty` postfix + `Grid.OnReveal` wiring from `Mod.cs`; build compiles but the overlay paints nothing
- [x] Green state: postfix `SimDebugView.OnPrefabInit` → `getColourFuncs[SpaceOverlayMode.ID] = GetColor`; `GetColor(SimDebugView, int cell)` = predicate (`IsActiveWorld` + Space zone + FOW handling per deep research) ? `Tint` : clear (null-guard on `zoneRenderData`; FOW term `Grid.IsVisible` kept in the predicate — the pipeline does not mask fog)
- [x] Implement `Enable`/`Disable` per PipPlantOverlay: `CameraController.Instance.ToggleColouredOverlayView(bool)` (+ `Camera.main.cullingMask` if the deep research shows it is needed for the standard look) — **deep research: not needed** (Oxygen doesn't call it; SimDebugView plane renders via its own camera/RT `_SimDebugViewTex`, unaffected by world desaturation) — Enable/Disable stay minimal (static `Active` flag + debug logs)
- [x] (Optional, per deep research) legend entry: `GetCustomLegendData()` / `OverlayLegend.OnSpawn` patch — **skipped** (user decision: not needed)
- [x] Green build: `dotnet build ONI-mods.sln -c Debug` (0 errors) — independent acceptance run (7/7 PASS: build, no homegrown rendering remains, core logic intact (3-term predicate + tint `(1.0, 0.2, 0.2, 0.35)` unchanged), registration correct, lifecycle intact, logging conventions)
- [x] Refresh `SpaceOverlay/README.md` (mechanism description) + changelog entry if behaviour wording changes — **no change needed**: README describes the feature in user-visible terms only (no mechanism mention); behaviour unchanged, mod not shipped (changelog stays `v0.0.1 Initial release`)

**Criterion:** Build passes; user re-tests in-game: red tint works on space cells (like the Oxygen overlay — no world desaturation expected, per deep research), fog of war covers the tint (live-expanding as duplicants reveal), tint bounded to the active map, asteroid switch does not crash, no warning flood in the log.
**Commit:** `refactor(spaceoverlay): render via built-in SimDebugView colour func (drop homegrown mesh)`

## Stage 8 — Icon cosmetics (user request 2026-09-23)

- [x] Update `SpaceOverlay/icon.svg`: white outline stays as-is (white `#E6E6E6` w=3 on top), planet filled red `#E14C4C`, black `#000000` w=5 rim under every white stroke (planet, both ring halves) + black halo under the star sparkle; PNG regenerated via `make_icon.sh`, verified 48×48 RGBA, visually verified
- [x] Green build: `dotnet build ONI-mods.sln -c Debug` (0 errors; `ConvertIcon` target ran)
- [x] Icon iteration (user saw it in-game): planet's white outline removed (planet = red fill + black outline only); black strokes thickened (planet 5→7, ring duplicates 5→7, sparkle halo 3→5); PNG regenerated (48×48 RGBA), visually verified

**Criterion:** `SpaceOverlay/icon.svg` updated per the request; `SpaceOverlay/ModAssets/overlay_space.png` regenerated from it (48×48); build green.
**Commit:** `feat(spaceoverlay): icon — red planet fill + black outer outline`

## Acceptance (user)
- Manual in-game run: overlay toggles from the overlay menu; space behind blocks is visible as a muted red tint; icon renders correctly.
- **2026-09-23: user accepted** — «все работает как надо» (Stage 7 built-in rendering + Stage 8 icon); task archived.
