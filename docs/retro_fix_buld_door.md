# Ретроспектива: BuildDoorOverWall (задача `fix_buld_door`)

Почему две «простые» Harmony-патчи подряд крашили игру, и какие неочевидные вещи
показалось по пути. Таймлайн: Stage 0–1 (метадаต้า замены, работает в игре),
Stage 2 (исправление ложного hover-предупреждения в survival) — две доработки
после двух крашей на загрузке мода. Коммиты: `ee4609b` (Stage 1),
`792c568` + `e44c83b` (Stage 2).

## 1. `out string` нельзя выразить в атрибуте `[HarmonyPatch]` (краш №1)

**Симптом:** `HarmonyException: Undefined target method ... Postfix` при загрузке
мода. Метод на месте, сигнатура `IsValidPlaceLocation(GameObject, int,
Orientation, bool, out string, bool)` — но атрибут `[HarmonyPatch]` с
`typeof(string)` его не находит.

**Неочевидное:**
- Игра использует **собственный билд 0Harmony v2** (`~/ONI/dlls/0Harmony.dll`),
  а не NuGet Harmony. Его резолвер целевого метода —
  `Type.GetMethod(name, allDeclared, null, paramTypes, [])`, где
  `paramTypes` — константы из атрибута.
- Параметр `out string` в метаданных — это `string&` (ByRef). Голый
  `typeof(string)` **не совпадает** с `string&`: `Type.GetMethod` требует
  точного совпадения типов. Совпадает только `typeof(string).MakeByRefType()`.
- А `MakeByRefType()` — **нелегальное константное выражение в позиции
  атрибута** (CS0182). То есть выразить out-параметр в атрибуте в принципе
  невозможно — ни «как надо», ни «как лень».
- Сообщение об ошибке («Undefined target») вводит в заблуждение: оно звучит
  как «метод отсутствует», а на самом деле тихо ломается byref-сопоставление.

**Вывод:** для методов с byref-параметрами патчить **программатически**:
`harmony.Patch(original, postfix: new HarmonyMethod(...))`, где `original`
находим вручную с нормализацией `IsByRef → GetElementType()` (см.
`FindMethod` в `BuildDoorOverWall/Mod.cs`). Эмпирическая проверка поведения
`Type.GetMethod` — в `.tmp/sigdump` (net8-репро).

*Постфактум:* в `Mod.cs:17` осталась твоя собственная заметка — ты независимо
пришёл к тому же программатическому обходу, «это было то же самое».

## 2. В игровом Harmony нет конвенции `__out_` (краш №2)

**Симптом:** `System.Exception: Parameter __out_fail_reason does not contain
a valid index` при `OnLoad` мода — игра падает до запуска.

**Неочевидное:**
- Современный Harmony (1.2+/2.x из NuGet) поддерживает `__out_<имя>` в
  postfix для записи обратно в out-параметр. **Игровой билд 0Harmony v2 её
  не имеет** — это конвенция, добавленная позже.
- В `EmitCallParameter` (декомпилированный `0Harmony.decompiled.cs` ~:4444)
  **любое** имя с префиксом `__`, не попавшее в список спецназваний
  (`__instance`, `__originalMethod`, `__args`, `__result`, `__resultRef`,
  `__state`, `__exception`, `__runOriginal`), разбирается как
  **позиционный индекс** через `int.TryParse`. `__out_fail_reason` →
  `TryParse("out_fail_reason")` не сработал → исключение на этапе генерации
  кода.
- Даже с «правильным» именем запись в out-параметр из postfix в этом билде
  **невозможна по генерации кода**: ветка both-byref выдаёт только
  `Ldarg` (загрузка значения), `Ldarga` (адрес) — лишь когда исходный
  параметр — простой value-type. То есть из postfix можно только *почитать*
  out-значение, не переопределив его.
- **Беспроигрышный вариант именования** в этом билде — позиционные `__0`,
  `__1`, `__2` (индексы исходных параметров) и `__result`; совпадение по
  имени (`GetArgumentIndex`, ~:5255) работает только при точном совпадении
  с именами параметров в рантайм-dll.

**Решение:** не писать в out-параметр, а переопределить **bool-результат**:
postfix `(BuildingDef __instance, GameObject __0, Vector3 __1,
Orientation __2, ref bool __result)` с `__result = true` под door-gate.
Hover-карточка (`BuildToolHoverTextCard.cs:50`) рисует текст только когда
метод вернул `false`, поэтому при `true` предупреждение не показывается —
значение строки `fail_reason` caller уже не читает.

## 3. Overload-ловушка: косметический путь и drag-путь — разные методы

`BuildingDef.IsValidPlaceLocation` имеет несколько перегрузок
(`BuildingDef.cs:1098` 4-arg Vector3, `:1104` 5-arg, `:1110` 3-arg int,
`:1114` 4-arg int, `:1120` каноническая 6-arg).

**Неочевидное:** drag (выживание) идёт через
`BuildTool.TryBuild → TryPlace →` **6-arg** `replace_tile:false`
(`BuildingDef.cs:467`) → replacement-fallback → `TryReplaceTile`
(`:490`, `replace_tile:true`); тогда как hover-текст
(`BuildToolHoverTextCard.cs:50`) и тинт визуализатора
(`BuildTool.UpdateVis:177`) идут через **4-arg Vector3**.

Значит:
- патчить нужно **только 4-arg** перегрузку — если бы мы вернули `true`
  в канонической 6-arg, `TryPlace` посчитал бы клетку стены нормальной
  стройплощадкой и ушёл бы в обычный `def.Build` — т.е. в **исходный баг**;
- 4-arg перегрузку не трогают ни drag, ни `TryReplaceTile` — переопределение
  результата чисто косметическое (текст + тинт), drag-роутинг не меняется.

Побочный эффект, записанный как carry-over в план (Stage 3): sandbox-ветка
InstantBuild (`BuildTool.cs:325`) **тоже** потребляет 4-arg перегрузку —
поэтому «починка hover» в режиме мгновенной постройки влияет на поток
`def.Build` vs replacement-fallback; там же fallback дополнительно гейтится
`IsValidBuildLocation(replace_tile:true)` (`:379`), которого нет в shared-гейте.

## 4. «Замена метаданных» — не только про дверь

Гейт «у def заданы `ReplacementLayer != NumLayers` и
`ReplacementCandidateLayers != null`» **не является door-scoped**: в ваниле
также это задают `ExteriorWallConfig`, `FacilityBackWallWindowConfig`,
`GlassExteriorWallConfig`, `ThermalBlockConfig`, `BuildingTemplates`
(плиты/лестницы). Без явной проверки `def.PrefabID != DoorConfig.ID` наш
postfix начал бы переопределять hover-результат и для внешних стен/
окон/теплоблоков/молдингов — изменение ванильного поведения, которое
никто не заказывал. В helper (`IsReplacementPlacementPossible`) проверка
PrefabID теперь **первый** чек.

## 5. `PatchAll` молча игнорирует классы без атрибутов

`Harmony.PatchAll(assembly)` (игровой 0Harmony ~:6811) обрабатывает **только**
типы с `HasHarmonyAttribute()`. Класс с postfix-методом, но без
`[HarmonyPatch]`-атрибутов, не даёт ни ошибки, ни патча — **тихий no-op**.
Поэтому подключаем postfix'ы программатически в `OnLoad`, и если `FindMethod`
не нашёл цель — пишем `Debug.LogError`, а не молчим: иначе «не работает
предупреждение» без единой строки в логе.

## 6. Инфраструктура

- **ilspycmd** в песочнице требует `DOTNET_ROLL_FORWARD=Major`
  (иначе не стартует рантайм):
  `DOTNET_ROLL_FORWARD=Major ~/.dotnet/tools/ilspycmd <dll> -o <out>`.
- **Сборка:** `NUGET_PACKAGES="$PWD/.cache/nuget/packages"
  NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache"
  dotnet build ONI-mods.sln -c Debug`. MSB3027/MSB3021
  (CopyModsToDevFolder в read-only `~/ONI`) — ожидаемо, не ошибка.
- **Сталый dll в папке мода:** `~/ONI/mods/BuildDoorOverWall_dev/` не
  обновляется сборкой (read-only), и песочница тоже туда не пишет —
  игра тихо грузит **старый** dll. Если после «сборки» поведение не
  изменилось — первым делом сверить, что в папке мода лежит свежий
  артефакт (`BuildDoorOverWall/bin/Debug/net48/BuildDoorOverWall.dll`).
- **Логи крашей:** `~/ONI/logs/Oxygen Not Included/Player.log` — полный
  стек трейса мода при падении на `OnLoad`; смотреть его первым делом.
- **Сверка с реальностью:** перед написанием патча — декомпилировать
  **тот самый** `0Harmony.dll` игры (в `./lib_sources/0Harmony/`), а не
  опираться на документацию NuGet Harmony. Оба краша были бы видны
  заранее по `EmitCallParameter`/`AttributePatch`.

## Чек-лист на будущее (Harmony в ONI)

1. byref-параметры в цели → только программный `harmony.Patch`, никакой
   атрибутовой привязки.
2. В postfix — только спецназвания (`__instance`, `__result`) или
   позиционные `__N`; никаких `__out_*`, никаких `ref`-out параметров.
3. Патчить конкретную перегрузку, которой ходит целевой caller; сначала
   переписать всех callers метода (поиск по декомпиляции Assembly-CSharp).
4. Гейтить по `PrefabID` нужного def, а не по «наличию метаданных замены».
5. `FindMethod`-фолбэк → `Debug.LogError`, не молчание.
6. Проверить, что свежий dll реально в `~/ONI/mods/<mod>/`.
