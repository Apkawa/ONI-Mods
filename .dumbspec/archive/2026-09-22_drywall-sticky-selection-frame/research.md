# Research

Task: BestBuildDryWall — white selection frame sticks to the mouse cursor after LMB release when shift was released mid-drag.

(To be filled as findings come in.)

## 2026-09-22 — subagent research report (read-only scan)

### Files & layout

- Project: `BestBuildDryWall/` — the whole mod is one file: `BestBuildDryWall/Mod.cs` (767 lines): `Mod : UserMod2` entry + all Harmony patch classes + `ShiftRectSession` ghost/rect state holder.
- Vanilla decompiled sources (`lib_sources/Assembly-CSharp/`):
  - `DragTool.cs` — base mouse down/move/up logic; the native white frame = `areaVisualizer`.
  - `BuildTool.cs` — the wall tool; `GetMode()` always Brush; owns `UpdateVis` which moves the tool's own transform to the cursor cell each mouse move (`BuildTool.cs:198`).

### Selection frame mechanics

- The white frame is the game's native `areaVisualizer` (`DragTool.cs:28`, a world-space SpriteRenderer GameObject, white tint `areaColour = (1,1,1,0.5)` at `:34`). The mod does NOT create it — it *borrows* one (`ShiftRectSession.EnsureBoxVisualizer`, `Mod.cs:268-321`, cached statically from another tool's donor, only SetActive-toggled, never Destroy'd).
- State: `areaVisualizerSpriteRenderer.size` (W×H) + `areaVisualizer.transform` position; text handle `areaVisualizerText` (`DragTool.cs:36-38`).
- Shown: `DragTool.OnLeftClickDown` Box branch (`DragTool.cs:170-175`).
- Updated on drag: `DragTool.OnMouseMove` Box branch (`DragTool.cs:366-399`).
- Hidden on normal mouse-up: ONLY `DragTool.OnLeftClickUp` (`DragTool.cs:239-243`):
  ```csharp
  Mode mode = GetMode();                                   // :234
  if ((mode != Mode.Box && mode != Mode.Line) || !(areaVisualizer != null))  // :239
      return;                                             // :240-242  <-- early return
  areaVisualizer.SetActive(value: false);                 // :243  <-- frame hidden, ONLY reached if mode==Box/Line
  ```
  Other hide sites: `CancelDragging` :207-210, `OnCmpDisable` :119-122, `SetMode` Brush :499-501. `RemoveCurrentAreaText()` at :233 runs BEFORE the gate (text is cleared even on the early return).

### Mouse / shift handling (mod handlers in `BestBuildDryWall/Mod.cs`)

| Handler | Mod.cs | What it does |
|---|---|---|
| `BuildTool_GetMode__Patch.Prefix` | 590-599 | Forces `Mode.Box` + skip original only when `def=="ExteriorWall" && ShiftHeld() && EnsureBoxVisualizer(...)`. Otherwise returns true → original `BuildTool.GetMode` → `Mode.Brush`. |
| `DragTool_OnLeftClickDown__Patch.Postfix` | 642-656 | Starts ghost session + backfills size text (only when Shift held). |
| `DragTool_OnMouseMove__Patch.Postfix` | 689-707 | If session active and `!ShiftHeld()` → `End("shift released")` (destroys ghosts). |
| `DragTool_OnLeftClickUp__Patch.Prefix` | 723-730 | Resets `bt.lastDragCell = -1`; always `return true` (does NOT end the session — deliberate, see Fix #3 below). |
| `DragTool_OnLeftClickUp__Patch.Postfix` | 732-738 | If session active → `End("mouse up")` (destroys ghosts only — never touches `areaVisualizer`). |
| `DragTool_SnapToLine__Patch.Prefix` | 614-623 | Suppresses straight-line snapping while session active. |
| `DragTool_CancelDragging__Patch.Postfix` | 745-751 | `End("cancel")`. |
| `DragTool_OnCmpDisable__Patch.Postfix` | 758-765 | `End("tool disabled")`. |
| `ShiftRectSession.ShiftHeld()` | 243-251 | `Input.GetKey(...GetInputForAction(Action.DragStraight))`. |

**Case 1 (LMB held → Shift released → LMB released):** after Shift release the next `OnMouseMove` postfix ends the session (ghosts die); on `OnLeftClickUp`, `GetMode()` → Brush (Shift not held) → the `DragTool.cs:239` gate early-returns → `SetActive(false)` at `:243` is SKIPPED → frame left active. The mod's postfix only ends the session (already inactive) and never hides the frame.

**Case 2 (drag with Shift → Shift released → keep dragging with LMB):** same mechanism; mode falls back to Brush, Brush branch of `OnMouseMove` doesn't hide the frame; LMB-up → frame left active. Side effect noted: Brush branch runs `AddDragPoints` (brush-painting) during the continued drag.

**Why it sticks to the cursor:** `areaVisualizer` is a child of the tool transform (`DragTool.cs:103`) and `BuildTool.UpdateVis` moves the tool transform to the cursor cell every mouse move (`BuildTool.cs:198`); while Box mode is active the frame is repositioned each frame, after Shift release it is carried along by the tool transform with frozen local size/offset.

**Must-not-change behavior:** re-pressing Shift without moving works today because `GetMode()` is re-derived from a live `ShiftHeld()` check on every handler call; the Box branch resumes on the still-active frame. Any fix must keep the frame (and its session text) usable when Shift is pressed again during an in-progress LMB hold.

### Prior constraints from task #2 (archived research)

- R6 Bug 2: `GetMode` prefix must `return false` to skip original (implemented at `Mod.cs:596`).
- R6 Bug 3: rect uses the game's conditional per-axis swap (`Mod.cs:391-398`).
- R7: wall-tool prefab has no `areaVisualizer` → NRE risk; mod borrows one lazily and gates the GetMode flip on success.
- R7 Fix #3 (KEY): session must stay ACTIVE through the original `OnLeftClickUp` — `End("mouse up")` was deliberately moved from prefix to postfix. If it ended in the prefix, releasing Shift before mouse-up would make `GetMode()` → Brush → early-return → nothing placed. That same early-return also skips the native frame hide — the mod compensates for ghosts but NOT for the frame hide.

### Remaining unknowns

- Exact world-vs-local semantics of `TransformExtensions.SetPosition` for the "stuck to cursor" feel is inferred (child-of-tool-transform mechanism), not fully proven; the hide-gap itself is certain.
- No Player.log trace for this specific bug; cases reasoned from code paths.
