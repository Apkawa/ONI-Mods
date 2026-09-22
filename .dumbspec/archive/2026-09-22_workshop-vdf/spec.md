# 2026-09-22_workshop-vdf: генерация workshop_build.vdf (+ README в BBCode) при Release-сборке

**Created:** 2026-09-22T13:35:00+03:00

## Scope

В scope:
- MSBuild-таргет `GenerateSteamVdf`, выполняемый при **Release**-сборке каждого мода, который генерирует `workshop_build.vdf` в папку артефактов мода (`$(TargetDir)` — плоская `<Mod>/bin/`).
- Поля VDF: `appid` (457140), `publishedfileid`, `contentfolder`, `previewfile`, `visibility`, `title`, `description`, `changenote`.
- Новый MSBuild-таргет: генерация `README.txt` (Steam BBCode) в папку артефактов мода (`$(TargetDir)`, рядом с vdf) из `README.md` мода, через NuGet-dotnet-тулзу `Converter.MarkdownToBBCodeNM.Tool` 1.0.0.29 (CLI `markdown_to_bbcodesteam`), встроленную в сборку по паттерну `dotnet-ilrepack` (PackageReference с `ExcludeAssets=all` + `Exec` dll из `NuGetPackageRoot`).
- `description` в vdf — содержимое **сгенерированного `README.txt`** (BBCode), а не сырого Markdown.
- `title` — из `$(ModName)`; `changenote` — `Version: $(Version)`.
- `publishedfileid` — из нового свойства csproj `<WorkshopItemId>` (дефолт `0` = первая публикация; реальный id подставляется после).

Out of scope:
- Сам процесс публикации (steamcmd / Oxygen Not Included Uploader) — vdf лишь их вход.
- Конвертация/обработка preview.png (файлы пока не существуют — таргет просто пишет путь).
- Копирование vdf/README.txt в `.tmp/build_mod_dir/` — оба файла живут только в `$(TargetDir)`.
- Флаг `--disableextended` у тулзы — используется дефолтный режим (для текущих README разница нулевая, проверено).

## Approach

- Таргет `GenerateSteamVdf` добавляется в **корневой `Directory.Build.targets`** — туда, где уже живут все shared-таргеты мода (`GenerateModYaml`, `GenerateModInfoYaml`, `CopyModsToDevFolder`), чтобы следовать существующему паттерну проекта.
- Условия таргета: `'$(Configuration)' == 'Release'` и `'$(DoNotBuildAsMod)' != 'true'` (как у `GenerateModYaml`) — так `UtilLibs` не получает vdf.
- `AfterTargets="Build"` (как в примере из черновика).
- Новое свойство csproj `<WorkshopItemId>0</WorkshopItemId>` на каждый мод:
  - `0` — первая публикация: steamcmd/Uploader создаёт новый элемент и дописывает присвоенный `publishedfileid` обратно в vdf;
  - после первой публикации владелец прописывает реальный id в csproj — дальнейшие сборки пишут vdf с этим id, т.е. повторные загрузки обновляют тот же элемент.
  - vdf при Release генерируется **всегда** (гейт по пустому id не нужен).
- `visibility` — свойство `<WorkshopVisibility>`, дефолт `2` (private) задаётся в самом таргете и переопределяется в csproj при необходимости. Значения: 0=public, 1=friends-only, 2=private, 3=unlisted (по `ERemoteStoragePublishedFileVisibility`).
- Кодирование: **без BOM**. Атрибут `Encoding` в `WriteLinesToFile` в MSBuild 17 (dotnet SDK) вообще не задаём: `UTF8NoBOM` как строка отклоняется (MSB3098), а `UTF-8` дописал бы BOM, который может сломать парсер VDF; дефолт MSBuild 17 — UTF-8 без BOM (проверено по первым байтам файла: `22 77 6f…`, без `EF BB BF`).
- Генерация `README.txt` (BBCode):
  - Зависимость: `PackageReference Include="Converter.MarkdownToBBCodeNM.Tool" Version="1.0.0.29"` с `<PrivateAssets>all</PrivateAssets>` + `<ExcludeAssets>all</ExcludeAssets>` в `Directory.Build.props`, `Condition="'$(IsPacked)' == 'true'"` — точный паттерн `dotnet-ilrepack` (в restore-проверке чисто восстанавливается).
  - Путь dll: `$([System.IO.Path]::Combine('$(NuGetPackageRoot)', 'converter.markdowntobbcodenm.tool', '1.0.0.29', 'tools', 'net8.0', 'any', 'Converter.MarkdownToBBCodeNM.Tool.dll'))` (имя пакета на диске — нижний регистр; TFM **net8.0** — на машине .NET SDK 8; в пакете есть и net10, но он здесь не запускается).
  - Новый таргет `GenerateReadmeBbcode` (`AfterTargets="Build"`, те же условия, что у `GenerateSteamVdf`): `Exec Command="dotnet &quot;$(BbcodeToolPath)&quot; -i &quot;$(ProjectDir)README.md&quot; -o &quot;$(TargetDir)README.txt&quot;"` + high-importance message.
  - `GenerateSteamVdf` становится зависимым от `GenerateReadmeBbcode` (цепочка `AfterTargets`), чтобы README.txt гарантированно существовал до генерации vdf.
- `description`: читать **сгенерированный `README.txt`** (BBCode) через `$([System.IO.File]::ReadAllText(...).Replace('"','\"').Trim())` → **сырые переносы строк** внутри одной VDF-строки (проверенная на живых workshop-файлах форма). Расширение item-листа в property-контексте в этом проекте склеивает строки `;` и percent-эскейтит текст (`**`→`%2a%2a`) — поэтому именно `ReadAllText`.
  - Примечание: BBCode-текст не содержит raw-кавычек `"` в маркерах (`[b]`, `[list]` и т.д.), но экранирование кавычек в тексте описания остаётся на месте.
- `contentfolder` = **store-папка мода `$(TargetFolder)`** (`.tmp/build_mod_dir/<Mod>_<flavor>/` — там только зашитый `<Mod>.dll`, `.pdb`, yamls), нормализованный абсолютный путь (бэксласы → слэши, без хвостового разделителя). Сама vdf-файл по-прежнему пишется в `$(TargetDir)`; таргет `GenerateSteamVdf` запускается **после** `CopyModsToDevFolder` (чтобы папка к моменту записи vdf уже существовала и была заполнена). `previewfile` = `$(ProjectDir)$(PreviewName)` с дефолтом `preview.png`.
- VDF пишется через `WriteLinesToFile` в `$(TargetDir)workshop_build.vdf`, `Overwrite=true`, `Encoding=UTF-8`, с high-importance message о пути.

## Constraints

Факты из ресёрча (фиксированные):
- TFM **net48** везде; shared-таргеты модов живут в корневом `Directory.Build.targets` и гейтятся по `DoNotBuildAsMod`/`IsPacked`/`Configuration`.
- Release: `<OutDir>bin</OutDir>` → плоская `<Mod>/bin/` (`$(TargetDir)`); Debug — `bin/Debug/net48/`.
- `ModName`/`Version` уже есть в каждом csproj (`Version` = 0.0.1 у всех 4 модов); `ModDescription` есть, но пуст.
- `README.md` есть у всех 4 модов, нет у `UtilLibs`. Preview-изображений нигде нет.
- `workshop_build.vdf` — **локальный publish-артефакт**: игра его никогда не читает (проверено по decompiled `KMod/Steam.cs`/`Local.cs`); это вход для `steamcmd +workshop_build_item` или Oxygen Not Included Uploader (appid 636750). Пути в vdf — локальные абсолютные пути машины сборки.
- VDF-escaping: кавычки `\"`, обратные слэши `\\` (важно для Windows-путей, если vdf будет генерироваться/использоваться под Windows).
- Единственная автоматическая проверка — `dotnet build` проходит; приёмка — ручная, пользователем.

## Open questions

none (все разрешены на review 2026-09-22):

- `visibility = 2` (private) — намеренно, как в примере пользователя.
- `WorkshopItemId` — дефолт `0` (первая публикация создаст новый элемент); реальные id подставит владелец позже.
- `contentfolder = $(TargetFolder)` (store-папка мода) — решение 2026-09-22: в публикуемый zip должен попадать только зашитый `<Mod>.dll` + yamls, без лишних `PLib.dll`/`UtilLibs.dll` из `bin/`.
- Описание — README.md целиком.

Известные ограничения (приняты, не ошибки):
- Markdown не конвертируется в Steam BBCode (TODO).
- В `description` экранируются только кавычки; обратные слэши в тексте README (например Windows-пути) не экранируются — если появятся, VDF может сломаться.
- Пути `contentfolder`/`previewfile` — абсолютные пути машины сборки: vdf работает только там, где собирался.

## Changes

- 2026-09-22T13:35:00+03:00 -- v1, initial spec (draft + research)
- 2026-09-22T13:42:00+03:00 -- review: разрешены open questions (visibility=2, WorkshopItemId=0 на первую публикацию, contentfolder=TargetDir, описание = README целиком); добавлены UTF-8NoBOM и описание ограничений
- 2026-09-22T14:45:00+03:00 -- Stage 1 (реализация таргета): отклонения, доказанные прогоном MSBuild — (1) атрибут `Encoding` убран (MSBuild 17 отклоняет `UTF8NoBOM`, дефолт и так без BOM); (2) README читается через `ReadAllText` property-function вместо `ReadLinesFromFile`+`@(ReadmeLines)` — item-expansion в property-контексте склеивает строки `;` и percent-эскейтит текст
- 2026-09-22T15:45:00+03:00 -- расширение scope по решению пользователя (в рамках текущей задачи/ветки, без новой ветки): генерация `README.txt` (Steam BBCode) в `$(TargetDir)` через NuGet-dotnet-тулзу `Converter.MarkdownToBBCodeNM.Tool` 1.0.0.29 (PackageReference по паттерну dotnet-ilrepack, запуск dll из `NuGetPackageRoot`, TFM net8.0); `description` в vdf теперь берётся из `README.txt` (BBCode), а не из сырого `README.md`; флаг `--disableextended` не используется (дефолт; для текущих README оба режима дают идентичный вывод)
- 2026-09-22T17:05:00+03:00 -- `contentfolder` в vdf: `$(TargetDir)` → **`$(TargetFolder)`** (store-папка `.tmp/build_mod_dir/<Mod>_<flavor>/`, содержащая только зашитый dll/pdb/yamls) — чтобы в workshop-zip не уходили лишние `PLib.dll`/`UtilLibs.dll` из `bin/`. Таргет `GenerateSteamVdf` переносится после `CopyModsToDevFolder`; путь нормализуется (бэксласы → слэши, без хвостового разделителя), т.к. `$(TargetFolder)` в targets собран с `\`
