# spec.md — fix_buld_door (BuildDoorOverWall)

Версия 2 — уточнена по результатам исследования (`research.md`). Первичная версия была составлена только из `draft.md`.

## Контекст

Мод **BuildDoorOverWall** (в этом репозитории, `BuildDoorOverWall/Mod.cs`): позволяет строить дверь поверх стены. Сейчас запускается и что-то делает, но:

1. Код признан непригодным — задача: **полный перезаписывания** (не заплатки).
2. **Баг 1 (песочница):** при постановке дверь ставится *под/вместе с* стеной, а не *вместо* стены.
3. **Баг 2 (песочница):** при постановке двери на план строительства (например, план стены) план не удаляется и не отменяется; потом он становится «сломанным» и его нельзя отменить.
4. Тестовые остатки (`LadderConfig`-патч, лог-патчи, дебаг-принты) — удалить.

## Цель (минимальный функционал)

Дать возможность **заменить стену дверью**:

1. **Режим выживания:** стена ставится в очередь на удаление, дверь — в очередь на постройку (как если бы дверь поставили в породу).
2. **Режим песочницы (sandbox, instant build):** дверь сразу заменяет стену.

Это — **самый минимум**; остальное вне скоупа. Мод остаётся `BuildDoorOverWall` (staticID не меняется).

## Что выяснило исследование (исправление фактов draft)

1. **В этой сборке игры нет класса `BuildPlan` и нет `PlacementSystem`.** «План» = GameObject `BuildingUnderConstruction` (компонент `Constructable : Workable`). Точка входа размещения игрока — `BuildTool : DragTool` (`BuildTool.cs:7`).
2. **«LadderConfig» в моде — это не класс мода, а патч игрового класса** `LadderConfig.CreateBuildingDef` (Mod.cs:46–59), который переписывает replacement-теги лестниц. Удаляется целиком вместе со всем остальным тестовым кодом.
3. **Донорский мод `tile_rep` на 100 % tag-based**: только Postfix'ы, добавляющие `GameTags.FloorTiles`/`Replaceable=true` в конфиги тайлов. Логика замены у него отсутствует — всю работу делает **нативный replacement-механизм игры**. Занимать из него нужно именно эту идею: не писать свою логику уничтожения/спавна, а включить нативный flow через метаданные `BuildingDef`.
4. **Нативный replacement-механизм игры (BuildTool + BuildingDef + Constructable):**
   - `BuildTool.TryBuild` (BuildTool.cs:307): обычный путь (TryPlace/def.Build) не сработал → fallback `def.ReplacementLayer != ObjectLayer.NumLayers` (350) → `GetReplacementCandidate(cell)` (352) → гейт `candidate.BuildingComplete.Def.Replaceable && def.CanReplace(candidate)` (364), где `CanReplace` = кандидат несёт любой из `def.ReplacementTags` → **survival**: `def.TryReplaceTile` (376) + запись в очередь `Grid.Objects[cell, ReplacementLayer]` (377); **sandbox**: `InstantBuildReplace` (381).
   - `InstantBuildReplace` (BuildTool.cs:390) уже умеет мультиселльные дефы: ветка `PlacementOffsets.Length > 1` (392–416) уничтожает кандидатов во всех соседних клетках, затем уничтожает основной тайл и сразу `def.Build` (421–429).
   - Завершение в survival: `Constructable.OnCompleteWork` (158–215) уничтожает кандидата в своей клетке, ветка `PlacementOffsets.Length > 1` в `FinishConstruction` (228–257) — в остальных клетках; затем `Def.Build` спавнит готовое здание.
5. **Дверь 1×2** (`DoorConfig.cs:12`, `PlacementOffsets = [(0,0),(0,1)]`), `Replaceable=false`, `IsFoundation=false`, **нет** `ReplacementLayer`/`ReplacementCandidateLayers`/`ReplacementTags` → нативный replacement fallback для двери в ванилле **никогда не срабатывает**. Это и есть то, что мы должны включить.
6. **Почему текущий мод даёт баг 1:** программный префикс на `BuildingDef.IsValidPlaceLocation` (Mod.cs:113–119) принудительно возвращает `true` и пропускает оригинал → проходит *обычный* гейт (BuildTool.cs:325) и сразу идёт `def.Build(cell, ...)` (348) — дверь встаёт в ту же клетку, что и стена («под стеной»); replacement-ветка вообще не достигается.
7. **Почему текущий мод даёт баг 2:** префикс на `BuildTool.InstantBuildReplace` (Mod.cs:138–174) делает «сырой» `Object.Destroy` содержимого клеток (включая планы `BuildingUnderConstruction`) без отмены (без `GameHashes.Cancel` → `Constructable.OnCancel`) → остаются осиротевшие Diggable/chores → план «ломается» и не отменяется. Плюс у префикса дефект precedence без скобок (Mod.cs:145).
8. Отдельный файл донора `Tile_rep_patch.cs` — исходник, декомпилированный из dll; проект-донор на net472 и сам по себе не собирается (не наш репозиторий — не трогаем).

## Подход (дизайн)

**Полная перезапись `BuildDoorOverWall/Mod.cs`** вокруг нативного replacement-flow игры:

1. **Патч `DoorConfig.CreateBuildingDef` (Postfix):** выставить на deф двери replacement-метаданные:
   - `ReplacementLayer = ObjectLayer.ReplacementTile`;
   - `ReplacementCandidateLayers = { FoundationTile, Backwall }` (стена = фундамент или Backwall);
   - `ReplacementTags = { GameTags.FloorTiles, GameTags.Backwall, GameTags.Ladders }` (кандидаты, которые дверь замещает).
2. **Убрать все хаки:** force-valid префикс `IsValidPlaceLocation`, префикс `InstantBuildReplace`, `AddTag(FloorTiles)` на дверь (дверь не должна выглядеть как пол для других замен), LadderConfig-патч, все логи, `Harmony.DEBUG`, шаблонные дебаг-принты, имя `ExampleMod`.
3. **Возможно понадобится** (уточняется на этапах 2–3 по источнику):
   - `Replaceable = true` на дефах фундаментов/Backwall, если гейт `candidate.Def.Replaceable` в ванилле для них не проходит (в донорском моде для TilePOI делали именно это);
   - аккуратная отмена плана (`BuildingUnderConstruction`) при постановке двери поверх него: событие `GameHashes.Cancel` (2127324410) → `Constructable.OnCancel`, затем удаление — без осиротевших Diggables.
4. **Двухклеточность двери** покрывается нативными ветками мультиселльных дефов (`InstantBuildReplace` 392–416, `FinishConstruction` 228–257) — свой код на эту тему писать не планируется, только верификация.

## Открытые вопросы (решаются на этапах реализации, см. plan.md)

1. `Replaceable` в ваниlli = true или false у дефов фундаментов и Backwall? (решает, нужен ли доп. патч)
2. Есть ли активный `BuildingComplete` у `BuildingUnderConstruction` (для гейта `component.Def.Replaceable`), когда кандидат = план?
3. Какие ещё клетки/слои должна закрывать `ReplacementCandidateLayers` (LadderTile?), чтобы замена двери «стены из лестниц» работала? (По draft — фундамент + Backwall достаточно; Ladder — опционально.)

## Ограничения и факты окружения

- ТФМ **net48**, игровая `0Harmony.dll` v2, Publicizer 0.4.3 (`build;contentfiles`), ILRepack `dotnet-ilrepack` 2.0.45, `UtilLibs`+PLib пакуются в dll мода (`IsPacked=true`). Не нарушать (AGENTS.md).
- Собирается: `NUGET_PACKAGES=... dotnet build ONI-mods.sln -c Debug` из корня репо. `CopyModsToDevFolder` упадёт (read-only `~/ONI`) — ожидается, артефакты остаются в `bin/`.
- **В песочнице/агенте игру запустить нельзя** — критерии приёмки этапов = сборка + статическая верификация code-level (trace по исходникам игры); финальная ручная проверка в игре — за пользователем (чек-лист будет в финальном коммите).
- Scratch → `./.tmp/`, кэши → `./.cache/`.
