# 2026-09-22_spaceoverlay: SpaceOverlay — оверлей «Воздействие космоса»

**Created:** 2026-09-22T21:49:55+03:00

## Scope

**В scope:**
- Новый мод `SpaceOverlay`: переключаемый оверлей в строке оверлеев игры (ряд иконок «tech view») — при включении космос (клетки в зоне «Space» внутри активной карты, включая здания у границы космоса) подсвечивается приглушённым полупрозрачным красным. Полезно для маленьких карт в вакууме, где космос за блоками сейчас виден только по тултипу.
- **v2:** переписать отрисовку подсветки с самодельного меша на **встроенный механизм игры** (паттерн из `lib_sources/peterhaneve_ONIMods/PipPlantOverlay/PipPlantOverlay.cs`): функция цвета на клетку через `SimDebugView.getColourFuncs`. Предикат, иконка и регистрация оверлея остаются прежними.
- Иконка оверлея: SVG → PNG через `rsvg-convert`, доставка через `ModAssets/`, загрузка через `PUIUtils.LoadSpriteFile` (PLib).
- README мода на английском (по skill `mod-readme`) + запись в таблице корневого `README.md`.

**Out of scope:**
- Изменение механики «Воздействия космоса» (урон, потеря газа/жидкости, тепло).
- Подсветка космоса на миникарте (только основная карта).
- Отдельный options-диалог мода (PLib Options) — переключение только через встроенный механизм оверлеев игры.
- Конфигурация цвета/прозрачности пользователем (значения хардкодятся, докручиваются по факту после первой сборки).
- Пользовательские фильтры режима (`CreateDefaultFilters`/`InFilter`) — PipPlantOverlay их не использует.

## Approach

- **Проект:** структура копируется с `BuildDoorOverWall/` (csproj: `IsMod=true`, `GenerateMetadata=true`, `IsPacked=true`, net48, `ProjectReference → UtilLibs`; `Mod.cs` с `class Mod : UserMod2`), проект в `ONI-mods.sln`.
- **Оверлей (регистрация, не меняется):**
  - `SpaceOverlayMode : OverlayModes.Mode` со `static readonly HashedString ID`.
  - Harmony-патч `OverlayScreen.RegisterModes` (private) — регистрация режима.
  - Harmony-патч `OverlayMenu.InitializeToggles` (private) — `OverlayToggleInfo` с нашей иконкой (файл → `getSpriteCB`, текущий рабочий вариант оставляем).
  - Запись в `StatusItem.overlayBitfieldMap` (убирает flood `no StatusItemOverlay value`).
  - Постфикс `OverlayScreen.OnSpawn` — восстановление режима после загрузки сохранения (встроенный механизм persistence не даёт).
- **Подсветка космоса (v2, встроенный механизм):**
  - Harmony-патч `SimDebugView.OnPrefabInit` (postfix, private-поле `getColourFuncs` через `AccessTools`) → регистрация `ID → Func<SimDebugView, int, Color>`.
  - `GetColor(SimDebugView, int cell)` = предикат (cell внутри активной карты && зона `Space` && [FOW — см. open questions]) ? `Tint` : прозрачный цвет.
  - Игра сама пересчитывает per-cell текстуру **каждый кадр**, пока режим активен (через `SimDebugView.Update` → `UpdateData`, в фоновых потоках) — самодельные dirty-флаги, `MarkDirty`-постфикс и `Grid.OnReveal`-трекинг **убираются**.
  - Функция цвета вызывается в фоновом потоке → чтение симуляционных данных из неё должно быть потокобезопасным (или предвычисляться в `Mode.Update()` на FG-потоке — решение в deep-research этапа).
  - `CameraController.Instance.ToggleColouredOverlayView(bool)` в Enable/Disable (стандартный вид игры: мир десатурируется, оверлей поверх) — по паттерну PipPlantOverlay.
- **Убирается («наш велосипед»):** `Mesh`/`RebuildMesh`/`Draw`/`EnsureAssets` (runtime-материал, `MeshZOffset`, `IndexFormat.UInt32`), отрисовка через `Graphics.DrawMesh(Camera.main, …, "Overlay")`, `Dirty`/`needsRebuild`, постфикс `GroundRenderer.MarkDirty`, хендлер `Grid.OnReveal`.
- **Легенда (опционально):** `GetCustomLegendData()` / патч `OverlayLegend.OnSpawn` (паттерн PipPlantOverlay) — решается в deep-research.

## Constraints

- TFM **net48** везде; Harmony — только `0Harmony.dll` из игры (NuGet `Harmony` запрещён); Publicizer 0.4.3 с asset-диапазоном `build; contentfiles`; ILRepack `dotnet-ilrepack` 2.0.45; PLib 4.19.0 через `ProjectReference → UtilLibs` (`IsPacked=true`).
- Игра: нет mod-facing API для оверлеев — только `OverlayScreen`/`OverlayModes`/`OverlayMenu`/`SimDebugView`; патчи через Harmony (`PatchUtil.TryPatch`).
- **`class Mode` не даёт никакой per-cell отрисовки** — базовые `Enable()`/`Update()`/`Disable()` пустые. Per-cell рендер живёт в `SimDebugView`: private-словарь `getColourFuncs: Dictionary<HashedString, Func<SimDebugView,int,Color>>`, полный пересчёт текстуры каждые кадры при активном режиме (тредится по видимым extents, `!Grid.IsActiveWorld` обнуляется), quad-плоскость на слое "SimDebugView", local z = −6.
- **Функция цвета вызывается в фоновом потоке** (комментарий PipPlantOverlay) — прямое чтение мутируемых данных симуляции из неё нежелательно.
- Десатурация мира не бесплатна: режим сам вызывает `ToggleColouredOverlayView`.
- `SimDebugView.hideFOW` существует; взаимодействие встроенного per-cell механизма с fog of war **не проверено** (open question).
- Логирование: `PUtil.LogDebug/LogWarning/LogError` + `.F(...)`; имя мода в сообщении не дублировать.
- GitHub issue: Apkawa/ONI-Mods#5.

## Open questions

- **Fog of war:** маскирует ли встроенный механизм незрелые клетки (`hideFOW`/`Grid.Visible` в `UpdateSimViewWorkItem`), или `Grid.IsVisible(cell)` нужно проверить прямо в функции цвета (рекомпутируется каждый кадр, так что live-расширение будет работать без трекинга) — deep-research в плане.
- Легенда: нужна ли запись (пользователем не просилась) — решение в deep-research.
- Десатурация: подтвердить, что `ToggleColouredOverlayView` + cullingMask дают стандартный вид (как у Oxygen-оверлея) — решение в deep-research.

## Changes

- 2026-09-22T21:49:55+03:00 -- v1, initial spec (draft + research)
- 2026-09-23T18:09:43+03:00 -- v2, по просьбе пользователя: отрисовка подсветки переписывается на встроенный механизм игры (функция цвета через `SimDebugView.getColourFuncs`, паттерн PipPlantOverlay); самодельный меш/dirty-трекинг убирается. Незакоммиченный fog-shader эксперимент (runtime-шейдер `_FogOfWarTex`) отброшен по решению пользователя.
- 2026-09-23T18:50:00+03:00 -- косметика по просьбе пользователя: иконка — планета заливается красным, белая обводка без изменений, вокруг всей белой обводки дополнительная чёрная обводка.
