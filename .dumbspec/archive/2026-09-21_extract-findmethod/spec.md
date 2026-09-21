# 2026-09-21_extract-findmethod: вынос FindMethod в UtilLibs

**Created:** 2026-09-21T13:04:00+03:00

## Scope

**В скоупе (этап 1, выполнен):**
- Вынести `FindMethod` в отдельный класс `ReflectionUtil` в `UtilLibs` (единственный источник правды).
- Удалить приватные копии из `BuildDoorOverWall/Mod.cs` и `ReplaceBuildingMaterial/Mod.cs`, переписать 18 call sites на `ReflectionUtil.FindMethod(...)`.
- Успешный `dotnet build ONI-mods.sln -c Debug`.

**В скоупе (этап 2, новый — по решению пользователя после исследования):**
- Единый утилитарный вызов «resolve + unified log + patch»: метод в `UtilLibs` вида `TryPatch(harmony, type, name, paramTypes, feature, prefix, postfix, transpiler)`, который сам делает resolve (через `ReflectionUtil.FindMethod`), при `null` пишет единый `LogError("could not resolve <sig> — <feature> skipped (game build mismatch?)")`, иначе вызывает `harmony.Patch(...)` и пишет `#if DEBUG`-лог.
- Рефакторинг 18 call sites в `BuildDoorOverWall` и `ReplaceBuildingMaterial` на этот единый вызов.
- Рефакторинг 1 call site в `SizeInTooltip` (сейчас `AccessTools.Method` + тот же ручной шаблон) на тот же единый вызов.
- Успешный `dotnet build ONI-mods.sln -c Debug` после каждого шага.

**Вне скоупа:**
- Замена `FindMethod` на `AccessTools.Method` — исследование показало, что `AccessTools.Method` не нормализует byref-параметры (BCL `GetMethod`, точное сравнение типов), и **7 из 18 call sites** (все с `out string`) молча сломались бы. Решение: оставить `ReflectionUtil.FindMethod` как единственный резолвер (см. Open questions — подтверждено пользователем).
- Изменения в `docs/retro_fix_buld_door.md`.
- Обновление устаревших описаний в AGENTS.md (sln-состав, IsPacked) — только зафиксировать.
- Transpiler/finalizer-патчи (их нет в текущих 18+1 call sites; если появятся — расширяем API).

## Approach

- **Этап 1 (выполнен):** `UtilLibs/ReflectionUtil.cs` — `public static class ReflectionUtil`, метод `FindMethod(Type, string, params Type[])` (declared-only, byref-нормализация, `null` при отсутствии). Мода вызывают `ReflectionUtil.FindMethod(...)`.
- **Этап 2:** новый публичный статический класс `PatchUtil` в `UtilLibs` с методом `TryPatch(...)`:
  - resolve через `ReflectionUtil.FindMethod` (поэтому byref-сайты продолжают работать);
  - строка сигнатуры собирается автоматически: `<TypeName>.<name>(<arg1>, <arg2>, ...)`;
  - `null` → `PUtil.LogError("could not resolve {0} — {1} skipped (game build mismatch?)".F(sig, feature))` + `return false` (плюс `#if DEBUG`-лог);
  - иначе → `harmony.Patch(resolved, prefix, postfix, transpiler)` (существующие сайты используют только prefix/postfix), `#if DEBUG`-лог об успешной привязке, `return true`.
  - Call site сокращается с ~10 строк до ~3 (один вызов с `feature`-фразой и HarmonyMethod-чашками).
- Пакетирование и референсы не меняются: `UtilLibs` уже компилируется против game `0Harmony.dll`/`Assembly-CSharp.dll` (неусловный ItemGroup в `Directory.Build.props`), ILRepack сливает UtilLibs в dll мода.
- Порядок: сначала утилита в UtilLibs (собирается), затем мод за модом (BuildDoorOverWall → ReplaceBuildingMaterial → SizeInTooltip), после каждого — сборка.

## Constraints

- TFM `net48` везде; сборка: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug` из корня репозитория.
- В sln 4 проекта: BuildDoorOverWall, UtilLibs, SizeInTooltip, ReplaceBuildingMaterial.
- `UtilLibs` сам `IsPacked=false` — пакуется в dll мода через ILRepack на стороне мода (не трогать).
- Harmony — только game `0Harmony.dll` (v2); ILRepack закреплён на 2.0.45; ILRepack **исключает** Harmony из merge-листа (Harmony остаётся внешним — в игре уже загружен).
- Все game-dll референсы приходят из неусловного ItemGroup в `Directory.Build.props` — `UtilLibs` уже видит `Harmony`/`HarmonyMethod`; менять csproj не нужно.
- `AccessTools.Method` (0Harmony) — не byref-совместим (7/18 сайтов), в единственном резолвере не используется; `SizeInTooltip` переезжает на общий резолвер.
- Единственная автоматическая проверка — успешная сборка; приёмка — вручную в игре пользователем.
- Текущий шаблон 19 call sites: 14 postfix-only, 3 prefix-only, 1 prefix+postfix, 1 prefix-only (SizeInTooltip); transpiler/finalizer — нигде нет.

## Open questions

- none (решены: оставить `ReflectionUtil.FindMethod`; класс `PatchUtil`; единый английский `#if DEBUG`-шаблон; `feature` — свободная фраза на сайте)

## Changes

- 2026-09-21T13:04:00+03:00 -- v1, initial spec (draft + research)
- 2026-09-21T13:39:29+03:00 -- пользователь утвердил имя класса `ReflectionUtil` и оставление логирования skip в модах
- 2026-09-21T16:45:06+03:00 -- пользователь добавил задачу: проверить замену FindMethod на AccessTools.Method и обернуть «resolve + log + patch» в единый вызов с унифицированным логом. Исследование: AccessTools.Method не нормализует byref (7/18 сайтов сломаются) → оставить FindMethod; добавлен этап 2 (единый TryPatch в UtilLibs + рефакторинг 18+1 call sites, включая SizeInTooltip)
- 2026-09-21T17:03:28+03:00 -- ревью этапа 2: оставить `ReflectionUtil.FindMethod`; класс `PatchUtil`; единый английский `#if DEBUG`-шаблон (заменяет 6 русских DEBUG-логов); `feature` — свободная фраза на сайте
