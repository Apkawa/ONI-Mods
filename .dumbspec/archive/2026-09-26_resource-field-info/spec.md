# 2026-09-26_resource-field-info: ResourceFieldInfo

**Created:** 2026-09-26T01:19:59+03:00

## Scope

**В скоупе:**
- Новый мод `ResourceFieldInfo`. При наведении курсора на клетку материала **при зажатом Ctrl** в тултип добавляются две строки о непрерывном «месторождении» (бассейне) под курсором:
  - число клеток в области,
  - суммарная масса всех клеток области (в кг/т, формат игры).
- Считается для любого элемента: вода, магма, твёрдые натуральные ресурсы, газы.
- Работает только при **неактивных** инструментах и режимах постройки (только дефолтный/выборочный инструмент).
- i18n — **максимальное переиспользование** существующих строк игры (без новых ключей .po).

**Вне скоупа:**
- Любые изменения игровой механики (масса/объём игры не трогаются; только просмотр).
- Визуальная подсветка области на карте.
- Конфигурация/настройки, клавиши-привязки, UI-кнопки.
- Отдельный учёт «искусственных» vs «натуральных» материалов.

## Approach

- Мод на `UserMod2` + 0Harmony v2 (dll игры) — стандартный паттерн репозитория (шаблон `BuildDoorOverWall`, ProjectReference → `UtilLibs`).
- **Точка подключения:** **prefix на `HoverTextDrawer.EndDrawing()`** (`lib_sources/Assembly-CSharp/HoverTextDrawer.cs:189`). Вызывается ровно раз за кадр активной hover-картой, после того как карта нарисовала все строки и до того, как drawer скрывает лишние виджеты — prefix может сделать `NewLine()` + `DrawText(...)` ещё раз. Postfix не подходит (EndDrawing уже отработал). Альтернатива (transpiler в `SelectToolHoverTextCard.UpdateHoverElements`, паттерн Futility `ThermalTooltips`) — только как фолбэк, если prefix окажется невалидным в рантайме.
- **Защитные условия (guard) внутри prefix:**
  - `PlayerController.Instance.IsUsingDefaultTool()` — нет активного инструмента/режима постройки;
  - Ctrl зажат: `Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)`;
  - клетка под курсором валидна: `int cell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()))` + `Grid.IsValidCell(cell)`;
  - клетка — «сырой» материал (элементный блок тултипа показывается, а не здание/объект) — условие воспроизводится из `SelectToolHoverTextCard.UpdateHoverElements` (видимая клетка, не solid-building, активный мир), чтобы строки появлялись ровно когда появляется элементный тултип.
- **Сопредельность области: 8 соседей (4 кардинальных + 4 диагонали).** Причина: в игре жидкости/газы распространяются по 8 соседям, поэтому «бассейн/месторождение» фактически 8-связное; 4-соседний `FloodFill` игры недоучитывает диагональные перемычки. В игре нет 8-соседнего flood-fill, поэтому строим собственный BFS на примитивах `Grid` (`CellLeft/CellRight/CellAbove/CellBelow` + `CellUpLeft/CellUpRight/CellDownLeft/CellDownRight`), с проверкой `Grid.ElementIdx[c] == startIdx`.
- **Масса:** суммируем `Grid.Mass[c]` по клеткам области (авторитетный кг/клетку). Число клеток = размер области.
- **Формат вывода (переиспользование строк игры, без новых .po):**
  - строка 1: `STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME` + ": " + count → «Клетки: 100» / «Tiles: 100»;
  - строка 2: `STRINGS.UI.ALLRESOURCESSCREEN.TOTAL` + ": " + `GameUtil.GetFormattedMass(totalMass)` → «Всего: 100т» / «Total: 100t».
- **Производительность:** BFS может быть большим (бассейны магмы — десятки тысяч клеток), а hover пересчитывается каждый кадр. Кэшируем результат области (count+mass) **по ключу стартовой клетки** и пересчитываем только когда клетка под курсором меняется; при смене элемента/клетки кэш сбрасывается. Это убирает повторный BFS каждый кадр.
- **Ошибки:** try/catch + однократный `PUtil.LogError` (паттерн `SizeInTooltip`); verbose-лог в `#if DEBUG`.

## Constraints

- Игра — сборка «Aquatic»: **нет** классов `Screen`, `ScreenBuilding`, `ScreenMining`, `KCell`, `Tooltip`, `Material` (проверено по списку типов DLL). Актуальные имена: `Material`→`Element`; данные клетки в `Grid` (unsafe-массивы); тултипы рисует `HoverText*`.
- `Grid` (static, `lib_sources/Assembly-CSharp/Grid.cs`): `Grid.ElementIdx` (ushort*), `Grid.Mass` (float*, кг/клетку), `Grid.Element[]`, `Grid.IsValidCell`, `Grid.PosToCell`, соседи `CellLeft/Right/Above/Below`, диагонали `CellUpLeft/UpRight/DownLeft/DownRight`.
- `FloodFill` игры — строго 4-соседний, 8-соседнего нет → собственный BFS.
- `GameUtil.GetFormattedMass(float, TimeSlice=None, MetricMassFormat.UseThreshold, includeSuffix=true, ...)` — формат массы для тултипов (тот же, что в `HoverTextHelper.MassStringsReadOnly`).
- `PlayerController.Instance.IsUsingDefaultTool()` — единственный каноничный способ «нет активного инструмента»; активного `Screen`/`GetActiveScreen` нет.
- Строки: EN-база скомпилирована в `STRINGS.*` (нет EN .po). RU-ключи: `STRINGS.UI.TOOLS.FILTERLAYERS.TILES.NAME`→«Клетки», `STRINGS.UI.ALLRESOURCESSCREEN.TOTAL`→«Всего». Мод-строки живут в `<mod>/strings/*.po` (last file wins) — **в этом моде новые .po не нужны**, строки берутся напрямую из `STRINGS.*`.
- Build-инварианты репозитория: TFM **net48**; ссылка на **игровой** `0Harmony.dll` (v2); Publicizer assets только `build; contentfiles`; ILRepack `dotnet-ilrepack` 2.0.45; кэши в `.cache/`, черновики в `.tmp/`. Сборка: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` (шаг `CopyModsToDevFolder` падает read-only в песочнице — ожидаемо).
- `ONI-mods.sln` — без поддержки комментариев: проект включается добавлением строки `Project(` + 4 строки конфигураций (не комментариями).

## Open questions

- **8 vs 4 соседи:** решено — **8** (см. Approach). Зафиксировано как осознанное отклонение от `FloodFill` игры ради корректности «бассейна»; если нужно строго по игре (4), меняется одна константа.
- **Точное условие «клетка — сырой материал»:** воспроизводится из `SelectToolHoverTextCard.UpdateHoverElements` в ходе реализации (имплементатор читает метод и дублирует условие видимости/`flag2`), чтобы строки совпадали с появлением элементного тултипа.

## Changes

- 2026-09-26T01:19:59+03:00 -- v1, первичная спецификация (draft + research)
