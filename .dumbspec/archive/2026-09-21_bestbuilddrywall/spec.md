# 2026-09-21_bestbuilddrywall: BestBuildDryWall

**Created:** 2026-09-21T22:14:16+03:00

## Scope

Входит:
- Новый мод `BestBuildDryWall` в этом репозитории (папка + csproj по шаблону `BuildDoorOverWall` + запись в `ONI-mods.sln` + README).
- **Функция 1 — Shift-прямоугольник плана гипсокартона.** Активен, когда в режим постройки выбран Drywall (buildable `ExteriorWall`) и зажат **Shift**:
  - тянешь мышью прямоугольник; во время перетаскивания отрисовка: рамка-«призрак» области и размер «W x H / N tiles» (как в инструментах копать/отмена) **плюс** размноженный призрак гипсокартона на каждой клетке прямоугольника (клон `BuildingPreview` с учётом выбранной схемы);
  - план размещается **в момент отпускания мыши**: по каждой валидной клетке прямоугольника ставится план Drywall (`BuildingDef.TryPlace`).
- **Функция 2 — быстрая перекраска без перестройки.** Если инструмент Drywall со схемой (или со «схемой по умолчанию») наносится на уже **построенный** Drywall того же материала (тот же `BuildingDef` + тот же материал/элемент), но с другим текущим стилем, то вместо того, что сейчас ничего не происходит, выполняется **пакетная перекраска** существующего гипсокартона нужным стилем (мгновенно, без перестройки, сохраняется в сейв). Работает и в обычном режиме кисти, и в Shift-режиме (по клеткам прямоугольника).
- Если материал другой — поведение как обычно (игра сама решает: замена/поверх/ничего).

Не входит (v1):
- Другие постройки, кроме `ExteriorWall` (в т.ч. `GlassExteriorWall` — отдельный buildable).
- Режим InstantBuild/Sandbox — оставляется штатное поведение игры (копает и перестраивает).
- Новые горячие клавиши, звуки, UI-экраны; i18n-файлы (у мода нет пользовательских строк).

## Approach

Harmony-патчи в доме стиле репозитория (программная регистрация через `UtilLibs.PatchUtil.TryPatch`, без атрибутов; логирование `PUtil`, детальные логи под `#if DEBUG`).

- **Patch A — `DragTool.GetMode()` (prefix):** пока активен `BuildTool` с def `ExteriorWall` и зажат Shift → возвращаем `Mode.Box`. Это бесплатно даёт штатную механику Box-режима: при нажатии мышки показывается рамка `areaVisualizer` и текст `NameDisplayScreen.AddAreaText` с форматом `"{0} x {1}\n{2} tiles"` (как у DigTool/CancelTool), при движении — обновление рамки/размера, при отпускании — обход клеток прямоугольника через `OnDragTool(cell, dist)`.
- **Patch B — сессия Shift-режима:** postfix `DragTool.OnLeftClickDown` (старт сессии, если условия выполнены), postfix `DragTool.OnMouseMove` (обновление «множественных призраков»: словарь cell → клон `BuildingPreview`, конфиг как в `BuildTool.OnActivateTool`: `GameUtil.KInstantiate(def.BuildingPreview, Grid.CellToPosCBC(cell, Grid.SceneLayer.Ore), null, layer "Place")` + `KBatchedAnimController` (Always/movable/`def.GetVisualizerOffset()`) + применение схемы инструмента; для больших прямоугольников — потолок и разреженная выборка), prefix `DragTool.OnLeftClickUp` (уничтожение призраков, конец сессии; дальше штатный обход прямоугольника).
- **Patch C — `BuildTool.OnDragTool(int, int)` (prefix):**
  - общий случай перекраски (функция 2): объект клетки — построенный `BuildingComplete` с тем же `Def` и тем же элементом, схема инструмента ≠ `BuildingFacade.CurrentFacade` → `ApplyBuildingFacade(...)` / `ApplyDefaultFacade()` и `return false`; в режиме InstantBuild/Sandbox — пропускаем (даём игре штатное поведение);
  - если активна Shift-сессия: для клетки вызываем `def.TryPlace(visualizer, pos, orientation, selectedElements, facadeID)` (план); если план не встал — проверяем случай перекраски; `return false` (запрещаем штатный `TryBuild`, чтобы не дублировать).
  - иначе (обычная кисть): если случай перекраски — красим и `return false`; иначе даём штатный `TryBuild`.
- **Мод:** `namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2`, `OnLoad(Harmony)` → `base.OnLoad` + `PatchUtil.TryPatch(...)`; csproj — точная копия шаблона `BuildDoorOverWall` с `PackageId=BestBuildDryWall`; добавление в sln (новый GUID + 4 строки конфигураций); README мода на русском.

## Constraints

- TFM **net48**; Harmony — игровая `$(GameLibsFolder)/0Harmony.dll` v2 (не NuGet); Publicizer 0.4.3 (assets `build; contentfiles`); ILRepack 2.0.45; PLib 4.19.0 через `UtilLibs`.
- Точка сборки: `Aquatic 731233`; `SupportedContent=ALL`, `APIVersion=2`.
- Дамп декompiled кода неполный (`BuildingDef.IsAreaValid`/`CheckFoundation` отсутствуют) — не опираться на их внутренности.
- Верификация: только `dotnet build ONI-mods.sln -c Debug`; приёмка — ручная в игре пользователем. Копия в `.tmp/build_mod_dir` падает (read-only, MSB3027) — ожидаемо, артефакты в `bin/`.
- Публичные поля/методы игра доступны через публикатор (private-члены читаются напрямую).

## Open questions

- Точный API определения зажатого Shift (`KInputManager` против `Input.GetKey`) — уточняется первым шагом реализации (Stage 2/3).
- Производительность «множественных призраков» на огромных прямоугольниках — решено: потолок количества призраков + разреженная выборка (деталь реализации).
- Поведение при отпускании мыши с уже отпущенным Shift (между нажатием и отпусканием) — допустимо деградировать к штатному Box/Brush-поведению; чистка призраков гарантируется.

## Changes

- 2026-09-21T22:14:16+03:00 -- v1, initial spec (draft + research)
