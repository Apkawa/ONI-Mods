# `mod_info.yaml` и `mod.yaml`: полная схема

Актуальная схема по декомпилированному коду игры (dll билда **744825**,
`KleiVersion.ChangeList`; это новее Aquatic-релиза 731233 из `Directory.Build.props`).

**Источники в `lib_sources/`:**

| Файл                                       | Что описывает                                                                                     |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| `Assembly-CSharp/KMod/Mod.cs`              | `PackagedModInfo` (поля mod_info), `GetModInfoForFolder`, `GetMostSuitableArchive` (выбор версии) |
| `Assembly-CSharp/KMod/KModHeader.cs`       | поля `mod.yaml`                                                                                   |
| `Assembly-CSharp/KMod/KModUtil.cs`         | чтение `mod.yaml` и fallback-значения                                                             |
| `Assembly-CSharp-firstpass/DlcManager.cs`  | валидные DLC-ID, логика required/forbidden                                                        |
| `Assembly-CSharp-firstpass/Klei/YamlIO.cs` | парсинг YAML (строгость, ошибки)                                                                  |
| `Assembly-CSharp/ModsScreen.cs`            | как поля показываются в UI                                                                        |

Оба файла парсятся `YamlIO.Parse<T>` (обёртка над YamlDotNet).

---

## 1. `mod.yaml` (опциональный, но желательный)

Парсится в **`KModHeader`** — всего 3 поля:

```yaml
title: "Your Mod Title" # string — название мода в списке
description: "About your mod" # string — описание в менеджерe модов
staticID: "yourSuperCoolMod" # string — стабильный идентификатор мода
```

| Поле          | Тип      | Поведение при отсутствии/пустом                                                                                                                                                                                                  |
| ------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `title`       | `string` | Берётся значение по умолчанию (название из Steam Workshop / имя папки). Если в `mod.yaml` другое — игра логит «using that from now on» и подменяет.                                                                              |
| `description` | `string` | `STRINGS.UI.FRONTEND.MODS.NO_DESCRIPTION` («No description found in mod.yaml»).                                                                                                                                                  |
| `staticID`    | `string` | `defaultStaticID = <label.id>.<distribution_platform>` (например `1234567890.Steam`). Свой `staticID` из `mod.yaml` переопределяет его — это и есть «настоящий» id мода, по которому игра сопоставляет версии (Steam/Local/Dev). |

Прочие ключи в `mod.yaml` игнорируются (см. §5). Файл может вообще отсутствовать —
мод всё равно загрузится, но с дефолтным названием/описанием/staticID.

---

## 2. `mod_info.yaml` (обязательный)

Парсится в **`KMod.Mod.PackagedModInfo`** (`KMod/Mod.cs:33-60`):

```yaml
minimumSupportedBuild: 731233 # int — минимальный билд игры для этой версии мода
version: 1.0.0 # string — версия МОДА (не игры!), показывается в списке
APIVersion: 2 # int — 0 | 2 (1 — устаревшее, отбраковывается)

# DLC-ограничения (современный формат):
requiredDlcIds: # string[] — все перечисленные DLC должны быть куплены/включены
  - EXPANSION1_ID
forbiddenDlcIds: # string[] — ни один из перечисленных DLC не должен быть куплен/включён
  - COSMETIC1_ID

# Устаревшее (deprecated, но игра всё ещё читает):
supportedContent: ALL # string — читается ТОЛЬКО если оба поля *DlcIds не заданы
lastWorkingBuild: 0 # int — deprecated, см. §3
```

### 2.1 `supportedContent` (deprecated)

`[Obsolete]`. Игра использует его **только если `requiredDlcIds == null` и
`forbiddenDlcIds == null`** (`KMod/Mod.cs:442`), при этом пишет devlog-предупреждение.
Конвертация (строка приводится к верхнему регистру):

| Значение                           | Результат                                                                                        |
| ---------------------------------- | ------------------------------------------------------------------------------------------------ |
| `ALL`                              | оба поля `null` (мод работает везде)                                                             |
| `VANILLA_ID`                       | `forbiddenDlcIds = [EXPANSION1_ID]` (только vanilla)                                             |
| `EXPANSION1_ID` (без `VANILLA_ID`) | `requiredDlcIds = [EXPANSION1_ID]`                                                               |
| комбинации/прочие `*_ID`-токены    | распознаётся только `EXPANSION1_ID`; всё остальное — «found a DLC it didn't recognize, ignoring» |

### 2.2 `requiredDlcIds` / `forbiddenDlcIds`

Валидные значения — **ключи `DlcManager.DLC_PACKS` + `EXPANSION1_ID`**
(`DlcManager.IsDlcId`):

| ID              | DLC                                   |
| --------------- | ------------------------------------- |
| `EXPANSION1_ID` | Spaced Out                            |
| `DLC2_ID`       | The Frosty Planet Pack                |
| `DLC3_ID`       | The Bionic Booster Pack               |
| `DLC4_ID`       | The Prehistoric Planet Pack           |
| `DLC5_ID`       | The Aquatic Planet Pack               |
| `COSMETIC1_ID`  | Neutronium Cosmetics Pack (косметика) |

Правила:

- регистр не важен — значения приводятся к `ToUpperInvariant` при чтении;
- пустая строка `""` (vanilla) — **невалидно** в этих полях: «без DLC» = отсутствие поля;
- нераспознанный ID → devlog-предупреждение «unrecognized DLC in requiredDlcIds/…», поле не очищается (в `requiredDlcIds` это фактически делает мод никогда не загружаемым);
- семантика проверки (`DlcManager.IsCorrectDlcSubscribed`): **required = AND** (все должны быть), **forbidden = OR** (ни один не должен быть);
- порядок элементов имеет косметическое значение: `GetMostSignificantDlc` берёт **последний** элемент для цвета DLC-баннера — писать в порядке релиза.

### 2.3 `APIVersion`

Константы (`KMod/Mod.cs:62-68`): `NONE = 0`, `HARMONY1 = 1`, `HARMONY2 = 2`.

- **`2`** — актуальный (0Harmony v2; игра ссылается на **свой** `0Harmony.dll`);
- **`0`** — контентный мод без DLL (допустим);
- **`1`** — legacy Harmony 1: при выборе версии такие архивы **отбраковываются**
  (`Where(v => APIVersion == 2 || APIVersion == 0)`), а если DLL всё же есть и
  `APIVersion != 2` — мод помечается `ModContentCompatability.OldAPI` и не
  включается.

### 2.4 `version`

Произвольная строка — версия **самого мода**, а не игры. Показывается в списке
модов (`ModsScreen.cs:254-274`): префикс нормализуется — `V1.0` → `v1.0`,
без префикса добавляется `v`. При статусе `ReinstallPending` вместо неё
показывается «Installed version newer than loaded version».

---

## 3. `lastWorkingBuild` vs `minimumSupportedBuild`

Оба — `int`, это **game build number** (changelist, например `731233`).

|                      | `lastWorkingBuild`                                                                                                                                                                                                                                | `minimumSupportedBuild`                                                                |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Смысл                | **Последний** билд, на котором мод проверен (верхняя граница: «работает до N»)                                                                                                                                                                    | **Минимальный** билд, с которого версия мода работает (нижняя граница: «работает с N») |
| Статус               | **`[Obsolete("Use minimumSupportedBuild instead!")]`**                                                                                                                                                                                            | текущий, единственный, который реально используется игрой                              |
| Поведение при чтении | Если ≠ 0 — devlog **error**: «is using `lastWorkingBuild`, please upgrade this to `minimumSupportedBuild`»; если при этом `minimumSupportedBuild == 0` — значение копируется в `minimumSupportedBuild` (обратная совместимость со старыми модами) | Участвует в выборе версии (§4)                                                         |

Код (`KMod/Mod.cs:504-511`): поле `lastWorkingBuild` **нигде больше не
читается** — только эта миграция. Исторически (`lastWorkingBuild`) это был
единственный build-маркер: «мод работал на билде N». Когда появилась поддержка
нескольких версий мода под разные билды, роль разделилась: нижняя граница —
`minimumSupportedBuild`. Старый верхний маркер устарели и оставили для
совместимости.

**Практически:** в новых модах `lastWorkingBuild` не писать вообще. Писать
только `minimumSupportedBuild` — билд игры, с которого данная версия мода
поддерживается (в репозитории это `$(TargetGameVersion)` из `Directory.Build.props`).

---

## 4. Как игра выбирает версию мода (`archived_versions`)

Структура: корневая папка мода + опционально `archived_versions/<имя>/`, где у
каждой версии **своя `mod_info.yaml`** (и свой `mod.yaml`). Алгоритм
`GetMostSuitableArchive` (`KMod/Mod.cs:328-405`):

1. Считываются все версии (корень + `archived_versions/*`);
2. Фильтр 1: `DlcManager.IsCorrectDlcSubscribed(info)` — DLC-ограничения версии
   совпадают с купленными/включёнными DLC игрока;
3. Фильтр 2: `APIVersion == 2 || APIVersion == 0`;
4. Фильтр 3: `minimumSupportedBuild <= <текущий билд игры>` (константа
   `744825` = `KleiVersion.ChangeList`, вшитая в собранный билд игры);
5. Из оставшихся берётся версия с **максимальным** `minimumSupportedBuild`.

Если ни одна не подходит → «No archive supports this game version», мод не
включается (`ModContentCompatability.DoesntSupportDLCConfig`).

Дальше в `ScanContent` (`KMod/Mod.cs:256-326`) сканируется содержимое выбранной
папки:

- есть `*.dll` → контент `DLL`; требует `APIVersion == 2`, иначе `OldAPI`;
- `*.po` → `Translation`; папки `strings/`, `codex/`, `elements/`, `templates/`,
  `worldgen/` → слойные файлы; `anim/`, `buildingfacades/` → анимации;
- **мод без `mod_info.yaml`** загрузится, только если содержит `*.po`
  (translation-only, `minimumSupportedBuild = 0`); иначе — «is missing a
  mod_info.yaml file and will not be loaded».

Итоговые статусы (`ModContentCompatability`): `OK`, `DoesntSupportDLCConfig`,
`NoContent`, `OldAPI` — показываются в списке модов с тултипами (включая
список требуемых/запрещённых DLC).

---

## 5. Нюансы парсинга (`Klei/YamlIO.cs`)

- **Табы заменяются на 4 пробела** до парсинга — в YAML не использовать табы;
- **неизвестные ключи** → ошибка _Recoverable_ (лог-предупреждение), поле
  просто игнорируется (YamlDotNet `IgnoreUnmatchedProperties`) — парсинг
  не падает;
- **фатальная ошибка** (сломанный YAML, несовместимость типов) → `default(T)`:
  - `mod_info.yaml` → версия отклоняется («Failed to parse mod_info.yaml»);
  - `mod.yaml` → используются дефолтные title/description/staticID;
- числа парсятся в `int`/`long`, строки — в `string`, списки — в `string[]`
  (YAML-последовательности `- value`).

---

## 6. Готовые примеры

**`mod.yaml`:**

```yaml
title: "BuildDoorOverWall"
description: "Allows building doors over walls"
staticID: BuildDoorOverWall
```

**`mod_info.yaml`** (минимальный, DLL-мод на Harmony 2, все DLC):

```yaml
minimumSupportedBuild: 731233
version: 0.0.1
APIVersion: 2
```

**`mod_info.yaml`** (мод только для Frosty Planet + Aquatic, запрещает косметику):

```yaml
minimumSupportedBuild: 731233
version: 0.0.1
APIVersion: 2
requiredDlcIds:
  - DLC2_ID
  - DLC5_ID
forbiddenDlcIds:
  - COSMETIC1_ID
```

---

## 7. Как это генерируется в этом репозитории

`Directory.Build.targets`, target `GenerateModInfoYaml`:

| Строка mod_info.yaml        | MSBuild-свойство                              |
| --------------------------- | --------------------------------------------- |
| `minimumSupportedBuild: …`  | `$(TargetGameVersion)`                        |
| `version: …`                | `$(Version)`                                  |
| `APIVersion: 2`             | хардкод                                       |
| `supportedContent: …`       | `$(SupportedContent)` (deprecated!)           |
| `requiredDlcIds:` + список  | `$(RequiredDlcIds)` — значения через запятую  |
| `forbiddenDlcIds:` + список | `$(ForbiddenDlcIds)` — значения через запятую |

По умолчанию в `Directory.Build.props`: `SupportedContent=ALL`,
`requiredDlcIds`/`forbiddenDlcIds` пустые. DLC-ID вынесены в отдельные
свойства (`SpacedOutDlcId`, `FrostyPlanetDlcId`, `BionicBoosterDlcId`,
`PrehistoricDlcId`, `AquaticDlcId`, `NeutroniumCosmeticsDlcId`), чтобы в
`.csproj` писать `<requiredDlcIds>$(FrostyPlanetDlcId),$(AquaticDlcId)</requiredDlcIds>`
вместо «магических» строк.

⚠️ Пока `SupportedContent` непустой, генерится deprecated-строка, и при
непустых `requiredDlcIds`/`forbiddenDlcIds` она игрой **игнорируется** (игра
предпочитает новые поля). Для чистоты при переводе мода на `*DlcIds`
`SupportedContent` лучше оставить пустым.
