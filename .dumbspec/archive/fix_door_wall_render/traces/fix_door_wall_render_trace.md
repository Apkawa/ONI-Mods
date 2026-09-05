# Deep code trace — BuildDoorOverWall broken-orientation completion, fix mechanics, design confirmation

Scope: READ-ONLY trace. All paths relative to repo root; decompiled game sources under `lib_sources/Assembly-CSharp/` (and `Assembly-CSharp-firstpass/`). Every claim carries a `file:line` citation. Context doc: `.dumbspec/current/fix_door_wall_render/research.md` (its §4.4 "completion is self-healing" claim is **wrong** — see §A.3 correction).

Setup recap (broken placement): 1×2 door def with `IsTilePiece == true` (e.g. `ManualPressureDoor`), placed with the **anchor (lower) cell in air** and the **upper cell on a foundation wall** (`TileComplete`, e.g. `MetalWall`). The mod's `BuildTool.TryBuild` postfix (`BuildDoorOverWall/Mod.cs:409-598`) calls `def.TryReplaceTile(...)` (Mod.cs:554), so the plan is created exactly like the native replacement flow.

Key def facts (broken set: `ManualPressureDoor`, `PressureDoor`, `InsulatedDoor`):
- `TileLayer = ObjectLayer.FoundationTile` (= 9): `ManualPressureDoorConfig.cs:15`, `PressureDoorConfig.cs:18`, `InsulatedDoorConfig.cs:45`. `IsTilePiece => TileLayer != ObjectLayer.NumLayers` → true (`BuildingDef.cs:276`).
- `ReplacementLayer` defaults to `ObjectLayer.NumLayers` (`BuildingDef.cs:127`); **the mod sets it** to `ReplacementTile` (= 11) on every door def at registration via its `Assets.AddBuildingDef` postfix (`BuildDoorOverWall/Mod.cs:167-179`).
- **`ReplacementCandidateLayers` is mod-set**: no vanilla door config assigns it (only `BuildingTemplates.CreateFoundationTileDef`/`CreateLadderDef` do, and nothing calls them), but the same mod postfix sets it to `{FoundationTile, Backwall}` (`Mod.cs:180-184`). So at runtime the door defs take the **gated** branch of `GetReplacementCandidate` (see A.2) — never the ungated `TileLayer` fallback (`BuildingDef.cs:340-342`), which only applies when `ReplacementCandidateLayers == null`.
- `PermittedRotations = R90` → the plan gets a `Rotatable` (`DoorConfig.cs:17`, `ManualPressureDoorConfig.cs:17`, `PressureDoorConfig.cs:20`; `BuildingLoader.cs:176-180`) → `MarkArea` runs in `OnSpawn`, not `OnPrefabInit`.
- Doors have **no `BlockTileAtlas`** (no config sets it) → `RegisterBlockTileRenderer` is a no-op for the plan and the completed door (`Building.cs:237-251`, gate at :239); doors are KAnim-rendered. The wall (`TileComplete`) **does** have a block-tile atlas and owns the `BlockTileRenderer.RenderInfo` for its cells.

---

## §A — Completion trace in the broken orientation (definitive destruction count)

### A.1 What the plan GO is

The plan is a clone of the `BuildingDef.BuildingUnderConstruction` prefab built from the *construction template*, not from `baseTemplate`:

- `BuildingLoader.CreateTemplate` (`BuildingLoader.cs:29-40`): `KPrefabID`, `KSelectable`, `StateMachineController`, `PrimaryElement`.
- `CreateConstructionTemplate` (`BuildingLoader.cs:49-59`): adds `BuildingUnderConstruction`, `Constructable`, `Storage`, `Prioritizable`, `Notifier`, `SaveLoadRoot`.
- `CreateBuildingUnderConstruction` (`BuildingLoader.cs:173-244`): adds `BuildingFacade` (:178), `Rotatable` (:176-177), `Cancellable` (default `Cancellable = true`, `BuildingDef.cs:99`), KAnim components (`Add2DComponents` :91-114), `KBoxCollider2D` (:116+), LogicPorts for logic buildings (:329-331).
- `KPrefabID` tag: `<PrefabID>UnderConstruction` (`BuildingLoader.cs:191-197`) + `GameTags.UnderConstruction` (:214). **No** `FloorTiles`/`Backwall`/`Ladders` tags.
- **No `BuildingComplete`**: that component lives only on `baseTemplate`, which is used exclusively for the *completed* prefab (`BuildingConfigManager.cs:28-48` declares it; `RegisterBuilding` → `CreateBuildingComplete` at :86-91). The plan's only `Building`-derived component is `BuildingUnderConstruction` (`BuildingUnderConstruction.cs:3`), so `Constructable.building` (`[MyCmpReq]`, `Constructable.cs:30-31`) resolves to it, and `constructable.Def` is the door def.
- `IsReplacementTile` is set on the *template* before cloning (`BuildingDef.cs:492-493`) and reset on the template after (`BuildingDef.cs:495`); the clone keeps it `true`. It is `[Serialize]` (`Constructable.cs:63-64`), so it survives save/load.

**Conclusion A.1: the plan GO carries no `BuildingComplete`, no `SimCellOccupier`, no `Deconstructable`, no `Conduit`, no `KAnimGraphTileVisualizer` (doors are not anim tiles; `isKAnimTile` unset). It is a `BuildingUnderConstruction`+`Constructable` GO.**

### A.2 The exact completion gate — `GetReplacementCandidate`

`BuildingDef.GetReplacementCandidate(cell)` (`BuildingDef.cs:324-345`):

```csharp
public GameObject GetReplacementCandidate(int cell)
{
    if (ReplacementCandidateLayers != null)
    {
        foreach (ObjectLayer replacementCandidateLayer in ReplacementCandidateLayers)
        {
            if (Grid.ObjectLayers[(int)replacementCandidateLayer].ContainsKey(cell))
            {
                GameObject gameObject = Grid.ObjectLayers[(int)replacementCandidateLayer][cell];
                if (gameObject != null && gameObject.GetComponent<BuildingComplete>() != null)  // :333
                {
                    return gameObject;
                }
            }
        }
    }
    else if (Grid.ObjectLayers[(int)TileLayer].ContainsKey(cell))   // :340  ← DOORS GO HERE
    {
        return Grid.ObjectLayers[(int)TileLayer][cell];            // :342  ← RAW GO, NO COMPONENT GATE
    }
    return null;
}
```

Because the mod's `Assets.AddBuildingDef` postfix gives every door def a non-null `ReplacementCandidateLayers = {FoundationTile, Backwall}` (`Mod.cs:180-184`), the **gated loop** (first `if` branch, :326-338) is taken — with the `BuildingComplete` component gate at :333 and **no tag check and no def-match**. (The ungated `TileLayer` fallback at :340-342 would apply only if the metadata postfix had failed to attach; then the TryBuild postfix's own guard at Mod.cs:419 would bail first, so the fallback is never live in practice.)

This is the pivot of the whole trace: the plan GO sits in *both* door cells' layer-9 slots after the buggy `MarkArea` (see §B.1), and the plan **lacks `BuildingComplete`** (A.1) — so the gate at :333 rejects the plan in *both* cells and `GetReplacementCandidate` returns **null** at both the anchor and the upper cell, even though the live wall is standing in the upper cell. (The research doc's §4.4 correctly anticipated null candidates but wrongly concluded the completion then "destroys the upper-cell candidate" — there is no candidate to destroy.)

### A.3 Broken orientation: wall is NOT destroyed at all (option **b** — hidden second bug)

Grid state right before the worker finishes (established by the plan's `MarkArea`, see §B.1):

- layer 11 (`ReplacementTile`): anchor = plan, upper = plan (`Constructable.cs:446-447`, `BuildingDef.cs:842`; anchor slot re-asserted by Mod.cs:555).
- layer 9 (`FoundationTile`): anchor = **plan** (anchor was air → guard at `Constructable.cs:452` passed), upper = **plan** (wall's entry **overwritten** at `BuildingDef.cs:842`, walls are not `Uprootable` so nothing was moved to layer 5 — `BuildingDef.cs:838-841`).
- `Grid.IsTileUnderConstruction[anchor] = true` (`Constructable.cs:460`).
- Wall GO: alive, but **orphaned** — no layer-9 key, still in its `BlockTileRenderer` `RenderInfo.occupiedCells`.

Completion (`Constructable.OnCompleteWork`, `Constructable.cs:124-221`), cell = anchor (`:160`):

1. `replacementCandidate = building.Def.GetReplacementCandidate(anchor)` (`:161`) → gated loop: FoundationTile layer **contains the anchor key** (the plan) but `plan.GetComponent<BuildingComplete>() == null` (`:333`, A.1) → rejected; Backwall layer: no key at the air anchor → **returns null**.
2. `replacementCandidate == null` → `flag2` stays **true**; the entire candidate branch (`:162-214` — no `DestroySelf`, no refund, no `ObjectReplaced` trigger, no `DeleteObject`) is **never entered**.
3. `:216-218`: `FinishConstruction(connections, worker)` is called **directly** (the plain completion path).

`FinishConstruction(connections, worker)` (`Constructable.cs:223-291`):

1. `IsReplacementTile && PlacementOffsets.Length > 1` (`:228`) → `RunOnArea(anchor, orientation, ...)` (`:230-256`), only `offset_cell != cell` (`:232`), i.e. **the upper cell**:
   - `GetReplacementCandidate(upper)` (`:234`) → gated loop: FoundationTile layer contains the key (the **plan**) but the plan lacks `BuildingComplete` (`:333`) → rejected; Backwall layer: no key (standard scenario) → **returns null**. **The live wall in the upper cell is therefore never even found** — the plan sitting in the same slot shadows it.
   - `null` candidate → the destroy/refund/trigger block (`:235-254`) is skipped entirely: **no `DestroySelf`, no `Deconstructable.SpawnItemsFromConstruction` refund, no `ObjectReplaced` trigger, no `DeleteObject`**.
2. `UnmarkArea()` (`:258`) → `unmarked` guard (`:465-466` false) → removes the plan from **layer 11** in both cells (`BuildingDef.UnmarkArea`, `BuildingDef.cs:976+`), `Grid.IsTileUnderConstruction[anchor] = false` (`:471-473`), `ClearPendingUproots` (`:475`).
3. `building.Def.Build(cell, orientation, storage, ...)` (`:259`) → completed door instantiated (`BuildingDef.Build`, `BuildingDef.cs:397-452`): `MarkArea(ObjectLayer = 12)` for both cells (`:416`) and, `IsTilePiece` → `MarkArea(TileLayer = 9)` for **both** cells (`:419`) → layer-9[anchor] = door, **layer-9[upper] = door (overwriting the plan's entry; the wall's entry was already gone)**; `TileVisualizer.RefreshCell` per cell (`:420-423`). No occupant destruction in `Build` (verified `BuildingDef.cs:397-452`).
4. `storage.ConsumeAllIgnoringDisease()` (`:288`) → plan's materials consumed (no refund anywhere).
5. `this.DeleteObject()` (`:290`) → plan marked for destruction (third and final).

End of frame: plan `OnCleanUp` (`Constructable.cs:547-603`) → `UnmarkArea()` (`:571`) no-op (`unmarked` true), partitioners freed, `SaveLoadRoot` unregistered, diggables cleaned. **The wall's `OnCleanUp` never runs, because the wall was never destroyed.**

**Result (broken orientation, pre-fix):**
- **Wall destruction count: ZERO.** Option **(b)**: the wall is NOT destroyed at all — no `DestroySelf`, no `Deconstructable.SpawnItemsFromConstruction` refund, no `ObjectReplaced` trigger, no `UnmarkArea`, no `UnregisterBlockTileRenderer`, no `RoomProber` event. (Corrects research.md §4.4: completion is *not* self-healing — the candidate found in both cells is the plan itself, not the wall.)
- Door completes and overwrites both layer-9 slots (`BuildingDef.cs:419` → `:842`).
- Wall GO stays alive as an orphan: no layer-9 key; still listed in its block-tile `RenderInfo.occupiedCells` (no `RemoveBlock` ever ran) → its wall block mesh keeps rendering inside the door's upper cell forever, with connection bits computed against live layer 9 (now the door, `MatchesDef` false, `BlockTileRenderer.cs:572-579,581-628`) → the wall draws a full bordered block on the door cell; the original inset artifact on neighboring wall borders persists as described in the bug report.
- Sim corruption: the upper cell now holds **two** live `PrimaryElement`s (completed door + wall) in one sim cell.
- The plan GO is destroyed exactly once, at `FinishConstruction`'s `this.DeleteObject()` (`Constructable.cs:290`; deferred `Object.Destroy`, `TracesExtesions.cs:5-13`); its `OnCleanUp` then runs `UnmarkArea()` (`Constructable.cs:571`, no-op via the `unmarked` flag) and frees its partitioners.

### A.4 Native working orientations (known-good reference): each wall destroyed exactly once

**Case (i): whole door in wall** (anchor = lower wall cell, upper = upper wall cell). Plan spawn: layer-9[anchor] already = wall → guard `Constructable.cs:452` **fails** → no clobber; layer-9[upper] never touched. Both walls intact.

Completion `OnCompleteWork` (anchor):
- `GetReplacementCandidate(anchor)` → gated loop → layer-9[anchor] = **wall GO** (key present, non-null, `BuildingComplete` present → returned, `BuildingDef.cs:330-336`).
- `SimCellOccupier` null → `Conduit` null → **`BuildingComplete` exists** (`:183-190`): `wall.Subscribe(-21016276 /* GameHashes.BuildingCompleteDestroyed, GameHashes.cs:333 */, () => FinishConstruction(...))`.
- `Deconstructable` on wall exists → **refund** `SpawnItemsFromConstruction(worker)` (`:202-206`).
- `wall.Trigger(1606648047 /* ObjectReplaced */)` (`:211`) → `BuildingComplete.OnObjectReplaced` sets `replacingTileLayer = door.TileLayer` (`BuildingComplete.cs:121-125`).
- `wall.DeleteObject()` (`:213`) → deferred destroy; end of frame `BuildingComplete.OnCleanUp` (`BuildingComplete.cs:250-301`): `WasReplaced()` true and `replacingTileLayer (9) == Def.TileLayer (9)` → both unmark branches skipped (the door's `MarkArea` at `BuildingDef.cs:416,419` clobbers both stale wall entries — this is why no unmark is needed); `UnregisterBlockTileRenderer()` (`:298`) → `RemoveBlock` → wall cell leaves its `RenderInfo` (`BlockTileRenderer.cs:766-773`); `Trigger(-21016276)` (`:300`) → fires the plan's subscription → **`FinishConstruction` runs exactly once**.
- `FinishConstruction` `RunOnArea` upper cell (`:230-256`): `GetReplacementCandidate(upper)` → gated loop → layer-9[upper] = **upper wall** (plan never wrote layer 9 in this orientation, so the wall's own entry is intact; `BuildingComplete` gate passes) → `Deconstructable` refund (`:242-246`), `Trigger(1606648047)` (`:251`), `upperWall.DeleteObject()` (`:253`). **Upper wall destroyed exactly once.**
- `Build` (:259) marks the door into both cells; plan deleted (:290).

**Case (ii): anchor = wall, upper = air** (lower door half in wall): identical at the anchor (wall destroyed once via the subscribe path); `RunOnArea` upper cell → `GetReplacementCandidate(upper)` → no key → null → nothing. Exactly once.

**Case (iii): whole door in air**: candidate null everywhere → `flag2` stays true → direct `FinishConstruction` (`:216-218`); `RunOnArea` upper → null → nothing. Trivially fine.

**Conclusion A.4: in every native orientation every wall is destroyed exactly once, refunded exactly once, and its renderer cell is removed via `OnCleanUp → UnregisterBlockTileRenderer`. The plan is destroyed exactly once (only at `:290`, since the candidate there is the wall, not the plan).**

---

## §B — Fix mechanics

### B.1 Timing proof: the layer-9 overwrite completes *inside* `TryReplaceTile`, before the postfix resumes

Call chain (all synchronous):

1. `def.TryReplaceTile(visualizer, pos, orientation, selected, facadeID)` — 6-arg facade overload (`BuildingDef.cs:508-527`) → 5-arg core (`:487-506`).
2. Core: `IsValidPlaceLocation(..., replace_tile: true)` (`:490`) → sets `templateConstructable.IsReplacementTile = true` (`:492-493`) → `Instantiate(pos, orientation, selected, 0)` (`:494`, def :529-540).
3. `Instantiate` → **`GameUtil.KInstantiate(BuildingUnderConstruction, pos, Front, null, layer)`** (`:533`) — `KInstantiate` runs the clone's `OnPrefabInit` and `OnSpawn` **synchronously inside the call** (`GameUtil.cs`, `KInstantiate` → `OnPrefabInit`/`OnSpawn` dispatch).
4. Clone `Constructable.OnSpawn` (`Constructable.cs:322-427`): `rotatable != null` (door is R90) → **`MarkArea()` at :345** (`:343-345`; the `rotatable == null` variant runs in `OnPrefabInit` at :301 — not the door case).
5. `MarkArea` (`Constructable.cs:441-461`):
   - `layer = def.ReplacementLayer` (11, because `IsReplacementTile`) (`:446-447`) → `def.MarkArea(anchor, orientation, 11, plan)` (`:447`) → **unconditional** `Grid.Objects[c, 11] = plan` for **both** cells (`BuildingDef.cs:829-843`, overwrite at `:842`; walls are not `Uprootable` so no layer-5 move, `:838-841`).
   - `IsTilePiece` true → guard `Grid.Objects[anchor, 9] == null` (`:452`) — **true in the broken orientation (anchor is air)** → `def.MarkArea(anchor, orientation, 9, plan)` (`:454`) → **overwrites layer-9[anchor] and layer-9[upper] with the plan** (wall's layer-9 entry erased at `:842`) → `RunOnArea → TileVisualizer.RefreshCell(c, 9, 11)` per cell (`:455-458`) → `BlockTileRenderer.Rebuild(9, c)` (`BlockTileRenderer.cs:775-784`) → wall `RenderInfo` rebuilt with `GetConnectionBits` reading live layer 9 (= plan, `MatchesDef` false, `:581-628`) → **the block-border insets appear here**.
   - `Grid.IsTileUnderConstruction[anchor] = true` (`:460`).
6. Back in `TryReplaceTile`: `IsReplacementTile` reset on template (`:495`), `Rotatable.SetOrientation` if orientation != 0 (`:496-503`). The 6-arg overload then applies the facade (`:513-516`) and re-applies orientation (`:517-524`). **Nothing after `Instantiate` touches layer 9.**
7. Back in the postfix: `Grid.Objects[__0, 11] = plan` (Mod.cs:555 — redundant re-assertion, same value) and the `Prioritizable` block (Mod.cs:560-591). No layer-9 access.

**Conclusion B.1: by the time `TryReplaceTile` returns, the layer-9 clobber of *both* cells is fully executed (inside the synchronous `KInstantiate` → `OnSpawn` → `MarkArea`). The "capture before / restore after" window inside the postfix is therefore exact and race-free.**

### B.2 Restore effect

Fix operation (per §D): after `plan != null`, restore the pre-capture layer-9 GO(s) into their cells and refresh.

- Restoring `Grid.Objects[upper, 9] = wall` puts the live wall back in its own cell — the exact native "plan adjacent to wall" state (cf. native case (i): layer-9 = {anchor: …, upper: wall}).
- **One** `TileVisualizer.RefreshCell(upper, ObjectLayer.FoundationTile, ObjectLayer.ReplacementTile)` suffices: `RefreshCell` refreshes the cell + its 4 orthogonal neighbors (`TileVisualizer.cs:23-33`), which **includes the anchor cell (directly below)**.
- `RefreshCellInternal` (`TileVisualizer.cs:5-21`): reads live `Grid.Objects[cell, 9]` → for the wall cell now the **wall** → `World.Instance.blockTileRenderer.Rebuild(ObjectLayer.FoundationTile, cell)` (`BlockTileRenderer.cs:775-784`) dirties every `RenderInfo` with `def.TileLayer == 9` → wall `RenderInfo.Rebuild` (`:223-281`) → per occupied cell `GetConnectionBits(x, y, queryLayer=9)` (`:581-628`) reads live layer-9: own cell = wall (`MatchesDef` self true via `:572-579`), neighbors = walls → **correct connection bits, no insets**.
- The anchor cell's own refresh (via the 4-neighbor sweep) rebuilds the same `RenderInfo`s; its layer-9 slot legitimately holds the **plan** (kept, not restored) — same as the native "foundation tile planned in air next to a wall" state. The plan has no `RenderInfo` (no `BlockTileAtlas`), so its presence only affects the wall bits in adjacent cells through `MatchesDef` (door def ≠ wall def → correct non-connection on the shared edge — exactly what native shows for a plan next to a wall).

**Conclusion B.2: restore wall GO into `Grid.Objects[upper, 9]` + a single `TileVisualizer.RefreshCell(upper, FoundationTile, ReplacementTile)` fully repairs the wall rendering immediately; the anchor slot must keep the plan (restoring null there would desynchronize the plan from its own grid slot and its replacement-layer bookkeeping).**

### B.3 Every reader of `Grid.Objects[cell, 9]` between plan creation and completion (broken vs fixed vs native)

Layer-9 = `FoundationTile` (enum value 9, `ObjectLayer.cs:12`; `Grid.ObjectLayers` is `Dictionary<int,GameObject>[45]`, `Grid.cs:634`, `GridSettings.cs:31`). Readers, with assessment in the broken placement scenario (anchor=air, upper=wall):

| Reader | Location | Broken state (pre-fix) | Fixed state (post-fix) | Native (case i) |
|---|---|---|---|---|
| `Grid.Objects[cell,9]` null-check, sim visibility (`RenderedByWorld`) | `World.cs:99` | wall cell shows plan → sim in wall cell hidden while plan there; after completion door there → hidden | wall in own cell → wall's sim rendered normally | same as fixed |
| `AutoMiner` door lookup (`ObjectLayers[9]` has key → door search) | `AutoMiner.cs:131,134,358,361` | no `Door` component on plan → same as native | no `Door` component on wall → same | same |
| Nav `IsMatch`: layer-9 key ⇒ cell non-navigable | `IdleStates.cs:73` | key present (plan) → non-navigable | key present (wall) → non-navigable | key present (wall) → non-navigable — identical |
| `SlipperyMonitor` (`PreventsSlipping` tag on cell below) | `SlipperyMonitor.cs:211` | plan: tag check on plan GO | wall: tag check on wall GO | wall |
| `SteppedInMonitor` (`Carpeted` tag below) | `SteppedInMonitor.cs:118` | plan | wall | wall |
| `EntombedItemManager` (non-null layer-9 ⇒ not entombed) | `EntombedItemManager.cs:71` | non-null | non-null | non-null |
| Melting path: destroy layer-9 object + `Trigger(675471409)` | `Game.cs:1292` | would destroy the **plan** (wrong) | destroys the **wall** (native behavior) | destroys the wall |
| `DestroyCell` adds layer-9 object to destroy list | `GameUtil.cs:3414` | plan | wall | wall |
| `NotInTiles` placement validation | `BuildingDef.cs:751,1287` | reads slot | reads slot | reads slot |
| `CheckFoundation` optional-foundation tag | `BuildingDef.cs:1702` | plan | wall | wall |
| World damage → `BuildingHP` on layer-9 object | `WorldDamage.cs:69` | damages plan | damages wall (native) | damages wall |
| Comet window/bunker damage tags | `Comet.cs:415` | plan | wall | wall |
| HEPPassThrough | `HighEnergyParticle.cs:354` | plan | wall | wall |
| TravelTubeBridge link | `UtilityNetworkTubesManager.cs:72-73` | plan | wall | wall |
| Travel-tube nav link | `NavGrid.cs:193-194` | plan | wall | wall |
| Space-zone check | `CellSelectionObject.cs:268` | plan | wall | wall |
| `SensitiveFeet` | `SensitiveFeet.cs:45` | plan | wall | wall |
| Dev tool (`DevToolSimDebug`, `MakeBaseSolid`, `DebugTool`) | `DevToolSimDebug.cs:227`, `MakeBaseSolid.cs:46,74`, `DebugTool.cs:173` | dev-only | dev-only | dev-only |
| Build-tool preview: nulls layer-9 only if `visualizer ==` entry; writes visualizer into empty anchor layer-9 while dragging | `BuildTool.cs:141-166,204-207` | sees plan in anchor | sees plan in anchor (unchanged) | wall |
| `NotInTiles` path check | `BaseUtilityBuildTool.cs:158` | plan | wall | wall |
| `FilteredDragTool` label "Tiles" (string only) | `FilteredDragTool.cs:197` | cosmetic | cosmetic | cosmetic |
| `Constructable.OnCleanUp` kAnimTile remove-block | `Constructable.cs:549-557` | door: `isKAnimTile` false → skipped | same | same |
| `Constructable.OnCompleteWork` / `FinishConstruction` candidate lookup (gated loop, `BuildingDef.cs:326-338`) | `Constructable.cs:161,234` | **null at both cells** (plan shadows the slot, lacks `BuildingComplete`) → **wall orphaned** | null at anchor (plan), **wall at upper → wall destroyed+refunded once** | wall at both cells |

**Conclusion B.3: the fixed state (`{anchor: plan, upper: wall}`) is behaviorally identical to the native "plan adjacent to wall" state for every layer-9 reader. The broken state diverges on exactly the readers that act on the slot's *contents* (damage, melting, foundation checks, completion candidate) — and the completion reader is the one that orphans the wall.**

### B.4 Consumers of `Grid.IsTileUnderConstruction[anchor] = true` for an AIR anchor

Set at `Constructable.cs:460` (anchor only — never the upper cell), cleared at `:471-473`. Full consumer list (7 hits):

- Declaration: `Grid.cs:676` (`NavValidatorFlagsUnderConstructionIndexer`).
- `World.cs:99` — `RenderedByWorld` computation: flagged cell forces sim rendering (so you see the gas inside the construction site).
- `SafeCellQuery.cs:52` — `if (Grid.IsTileUnderConstruction[cell] || Grid.IsTileUnderConstruction[num]) return (SafeFlags)0;` → flagged cell **and the cell above it** are unsafe for minion resting/standing queries.
- `AbsorbCellQuery.cs:83` — analogous gate for absorb queries.
- `DevToolSimDebug.cs:227` — dev tool.
- Set/clear: `Constructable.cs:460` / `:473`.

Impact for an AIR anchor: the anchor cell and (via the "cell above" term) a query at the anchor's *lower* neighbor are flagged-unsafe. **This is byte-for-byte what native does for any foundation tile planned in air** (a floor plan sets the same single anchor flag, `Constructable.cs:460`). No additional consumers, no misbehavior; the flag is cleared at completion (`:473`) in both broken and fixed flows.

### B.5 Save/load (short)

- `Grid.ObjectLayers` is **not serialized**: `Grid` has no `SaveLoad` (nothing in `Grid.cs` serializes the dictionaries); on load a fresh grid is allocated (`SaveLoader.cs:406` `GridSettings.Reset`). Save writes sim cells + per-object data: `SaveLoader.Save` (`SaveLoader.cs:336-344`) = `Sim.Save(writer,0,0)` + `saveManager.Save(writer)` (all registered `SaveLoadRoot`/`ISaveLoadable` objects — the plan has one, `BuildingLoader.cs:57`; the wall's completed prefab gets one from `baseTemplate`, `BuildingConfigManager.cs:28-48`). The plan persists `IsReplacementTile` (`[Serialize]`, `Constructable.cs:63-64`).
- On load, layer entries are **re-established by each object's `OnSpawn`**: wall → `BuildingComplete.OnSpawn` → `Def.MarkArea(ObjectLayer)` + `MarkArea(TileLayer)` (`BuildingComplete.cs:162-169`); plan → `Constructable.OnSpawn` → `MarkArea` (`Constructable.cs:345,441-461`).
- **Mid-construction save in the broken placement is order-dependent**: if the wall's `OnSpawn` runs *before* the plan's, the plan's anchor guard (`Constructable.cs:452`) passes (anchor air) and its `MarkArea` **re-clobbers the wall's layer-9 entry on load** → the broken grid state recurs (and completion will orphan the wall again, §A.3). If the plan loads first, the wall's `OnSpawn` restores its own cell → the fixed grid state. The in-session fixed state (`{anchor: plan, upper: wall}`) saves identically to the broken one at the object level — the difference exists only in the unserialized in-memory map. This nondeterminism is a **pre-existing property of `Constructable.OnSpawn`**, unaffected by the fix.
- (Pre-fix, post-completion save: the upper cell holds *two* live `PrimaryElement`s — door + orphan wall — i.e. one sim cell is double-claimed; the fix prevents reaching that state.)

---

## §C — Consumers & save/load

(Consolidated from §B.3 / §B.4 / §B.5; the tables above are the definitive list. Nothing additional beyond them.)

---

## §D — Final design confirmation

**The planned fix shape is confirmed, with one clarification and one explicit behavioral note.**

Exact spec (inside the existing `BuildTool_TryBuild_DoorReplacement__Patch.Postfix`, `Mod.cs:409-598`), around the call at `Mod.cs:554`:

1. **Capture BEFORE `def.TryReplaceTile`** — for **every area cell** (anchor + upper):
   ```csharp
   // cell = __0 (anchor); second cell = Grid.OffsetCell(__0, 0, 1) for the 1x2 vertical door,
   // or generically via def.RunOnArea(__0, __instance.buildingOrientation, ...)
   GameObject capturedAnchor = Grid.Objects[__0, (int)def.TileLayer];      // air in the broken case
   GameObject capturedUpper  = Grid.Objects[upper, (int)def.TileLayer];   // the wall
   ```
   - Use `def.TileLayer` (== `FoundationTile` for the broken doors) — the same layer `Constructable.MarkArea` overwrites (`Constructable.cs:452-454`).
   - Guard: **skip the whole capture/restore block when `def.TileLayer == ObjectLayer.NumLayers`** (non-tile-piece door, e.g. internal `Door` — `TileLayer` defaults to `NumLayers`, `BuildingDef.cs:125`; such defs never touch layer 9).
2. **AFTER `plan != null`** (`Mod.cs:554-555`): for each captured **non-null** GO, restore it:
   ```csharp
   if (capturedUpper != null)  Grid.Objects[upper, (int)def.TileLayer] = capturedUpper;
   // anchor: capturedAnchor == null in the broken case → restore nothing → plan stays in the anchor slot
   ```
   - **Keep the plan in the anchor layer-9 slot — do NOT restore null there.** The anchor slot is the plan's own legitimate claim (`Constructable.cs:452-454` wrote it and owns it until completion). At completion the anchor candidate lookup returns **null** for the plan (gated loop, `BuildingDef.cs:333` — the plan lacks `BuildingComplete`), so `OnCompleteWork` takes the direct-`FinishConstruction` path (`:216-218`) exactly as in the broken case; the restored wall is then found and handled at the **upper** cell (§A.4, step "RunOnArea upper").
3. **Refresh**: exactly one call
   ```csharp
   TileVisualizer.RefreshCell(upper, def.TileLayer, def.ReplacementLayer);
   ```
   — its 4-neighbor sweep (`TileVisualizer.cs:23-33`) covers the anchor cell as well (§B.2). (Refreshing the anchor cell explicitly is optional/redundant; one call is sufficient.)
4. Everything else in the postfix stays as is.

**Why this is correct (evidence summary):** the clobber completes synchronously inside `TryReplaceTile` (§B.1), so capture-before/restore-after brackets it exactly; the restored state is *identical* to the native "plan adjacent to wall" state for all ~25 layer-9 readers (§B.3) and for `IsTileUnderConstruction` (§B.4); rendering self-heals through one `RefreshCell` (§B.2).

**Behavioral note (intended change, must be documented):** pre-fix, completion in the broken orientation orphans the wall (never destroyed, never refunded — §A.3). Post-fix, completion's `FinishConstruction` `RunOnArea` finds the **restored wall** at the upper cell (gated loop returns it — it has `BuildingComplete` — `Constructable.cs:234-254`) → the wall is **destroyed exactly once, refunded once, with the `ObjectReplaced` trigger** — matching native case (i) (§A.4). The door's own completion path is otherwise unchanged: anchor candidate null → direct `FinishConstruction` (`Constructable.cs:216-218`), plan's materials consumed (`:288`), plan deleted at `:290`.

**Risks:**
1. **Completion behavior change**: the wall refund appears where previously nothing was refunded — intended, but it is a gameplay-visible difference from the current buggy build (materials returned to the worker's inventory at completion).
2. **Mid-construction save/load nondeterminism** (§B.5): the fixed in-memory state is not serialized; on reload the plan's `MarkArea` can re-clobber the wall's layer-9 entry if the plan's `OnSpawn` runs after the wall's, reviving the orphaned-wall completion behavior for that session. Pre-existing, low probability, worth a debug log at reload-time (optional).
3. **Post-fix completion does a real wall destroy at the upper cell**: that is the intended new behavior, but it is a *different code path* than the pre-fix run (the `:235-254` destroy/refund/trigger block, previously never entered, now runs on a live wall GO): deferred `DeleteObject` (`:253`) → end-of-frame `BuildingComplete.OnCleanUp` (`BuildingComplete.cs:250-301`) → `UnregisterBlockTileRenderer`/`RemoveBlock` (`:298`, `BlockTileRenderer.cs:766-773`). The plan GO itself is still destroyed exactly once, at `:290` (the anchor candidate is null, so the candidate-branch `DeleteObject` at `:213` is never reached). No double-destroy or NRE anywhere in the sequence.
4. **Scope**: the fix targets `def.TileLayer == FoundationTile` doors (`ManualPressureDoor`, `PressureDoor`, `InsulatedDoor`); `Door`/`Pneumatic`/`WoodenDoor`-style non-tile-piece or non-replacement defs are excluded by the existing postfix guards (Mod.cs:412,419) and the §D.1 `NumLayers` guard. (Do not patch `GetReplacementCandidate` to make it area-aware — `Mod.cs:400-403` documents why that would let the anchor branch double-handle the upper cell.)
