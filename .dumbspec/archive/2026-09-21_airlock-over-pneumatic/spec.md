# 2026-09-21_airlock-over-pneumatic: механический шлюз поверх пневматической двери

**Created:** 2026-09-21T19:02:26+03:00

## Scope
В scope:
- Починить BuildDoorOverWall так, чтобы **механический шлюз (`PressureDoor`) ставился поверх пневматической двери (`Door`)** и симметрично — **`Door` поверх `PressureDoor`** (тот же байпас покрывает оба направления).
- Добавить в отладочный лог (`#if DEBUG`, `PUtil.LogDebug`) причину отказа нативного `IsValidPlaceLocation` в пути мода (сейчас `plan=` пустой без причины — видно только в hover-гейте).

Out of scope:
- «Та же дверь поверх той же двери» (`PressureDoor` поверх `PressureDoor`, `Door` поверх `Door`) — намеренно отключено префиксом мода, не трогаем.
- Другие модификации поведения мода (стены/тайлы поверх двери, цвета, hover-тексты).

## Approach
Корневая причина (из research.md): `Door` и `PressureDoor` оба объявляют входной порт автоматизации на **якорной клетке** (`LogicInputPorts = CreateSingleInputPortList(new CellOffset(0,0))`). Живая пневматическая дверь уже зарегистрировала свой физический порт в `logicCircuitManager`. Когда мы тащим призрак `PressureDoor` на клетку с живым портом, нативная проверка `BuildingDef.AreLogicPortsInValidPositions` (внутри `IsValidPlaceLocation(replace_tile:true)`) находит конфликт («Порты автоматизации не могут совпадать») → `TryReplaceTile` возвращает null → план не создаётся, попытка молча не работает. `ManualPressureDoor` портов не имеет — поэтому он работает.

Решение — в стиле существующих патчей мода (Harmony, без смены архитектуры):
- **Расширить существующий postfix `IsValidPlaceLocation`** (6-аргументная версия): если нативная проверка упала на `fail_reason == STRINGS.UI.TOOLTIPS.HELP_BUILDLOCATION_LOGIC_PORTS_OBSTRUCTED`, кандидат на замену — дверь (`IsDoorDef` + `Replaceable`) и `replace_tile == true` — сбросить `fail_reason` и вернуть `true`. Конфликт на клетке кандидата может исходить только от здания, его занимающего, т.е. от самой заменяемой двери; чужие порты (соседние здания) при этом продолжают блокировать.
- В пути `TryBuild` перед `def.TryReplaceTile` вызывать `def.IsValidPlaceLocation(..., replace_tile: true, out fail_reason)` и при неудаче писать `PUtil.LogDebug` с причиной (только `#if DEBUG`), чтобы подобные отказы были видны в логе.

## Constraints
- net48; Harmony — только game's 0Harmony v2; патчи вешаются программно в `Mod.cs` (7 патчей сейчас, таблица в research.md).
- Единственная автоматическая проверка — `dotnet build ONI-mods.sln`; приёмка — вручную пользователем в игре.
- Стилистика: лог через `PUtil.LogDebug/LogWarning/LogError` + `.F(...)`, имя мода в сообщении не дублировать; диагностика в `#if DEBUG`.
- Файл лога игры: `~/ONI/logs/Oxygen Not Included/Player.log`; песочница `~/ONI` read-only — артефакты в `bin/`.
- Гейм-факт: `AreLogicPortsInValidPositions` сравнивает порты призрака с `logicCircuitManager.GetVisElements()` (все живые логические здания мира) — конфликт с **любой** чужой дверью/устройством должен продолжать блокировать (байпас только для самой заменяемой двери).

## Open questions
none (разрешены при ревью: см. Changes)

## Changes

- 2026-09-21T19:02:26+03:00 -- v1, initial spec (draft + research): определена корневая причина (конфликт портов автоматизации на якорной клетке), задан scope и подход
- 2026-09-21T19:05:00+03:00 -- пользователь подтвердил: в scope симметричный кейс `Door` поверх `PressureDoor`; причина отказа пишется через `PUtil.LogDebug` в `#if DEBUG`; выбран подход «расширить существующий postfix `IsValidPlaceLocation`»
