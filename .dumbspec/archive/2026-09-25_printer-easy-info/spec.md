# 2026-09-25_printer-easy-info: PrinterEasyInfo

**Created:** 2026-09-25T13:42:40+03:00

## Scope

Новый мод `PrinterEasyInfo`. В окне выбора схемы печати телепада («Выберите схему» / «Select a Blueprint») в колонках, которые **НЕ дубликанты** (колонки care package — у них нет иконки переименования (карандаша)), добавить иконку-кнопку «книжку» в шапке колонки — в позиции, как у иконки редактирования имени в колонках дубликантов.

При клике по иконке открывается запись базы данных (Codex) по предмету, который содержит эта care package — та же иконка (игровой спрайт `OverviewUI_database_icon`) и та же логика, что у иконки-книжки в окне свойств предмета (`DetailsScreen.CodexEntryButton` → `ManagementMenu.OpenCodexToEntry`).

Out of scope:
- колонки дубликантов (у них иконка уже есть/не нужна);
- окно выбора персонажа для новой игры (`MinionSelectScreen`);
- изменения самого Codex/базы данных;
- создание новых спрайтов (реузим игровой).

## Approach

- Новый проект `PrinterEasyInfo` по шаблону репозитория: `net48`, `IsMod/GenerateMetadata/IsPacked=true`, `ProjectReference` на `UtilLibs`; ссылки на DLL игры, publicizer и ILRepack наследуются из корневых `Directory.Build.props`/`targets`. Добавить проект в `ONI-mods.sln` (строка `Project(...)` + 4 строки конфигураций — комментарии в sln не поддерживаются).
- Harmony-патч программным стилем репозитория через `PatchUtil.TryPatch`: postfix на **`CarePackageContainer.GenerateCharacter(bool)`** (private; срабатывает и при первом появлении, и при каждом reshuffle). В postfix: из `__instance.Info.id` вычислить codex id (ветка prefab vs элемент, как в `CarePackageContainer.SetAnimator()`), обновить/создать кнопку-книжку в шапке колонки.
- Кнопка создаётся в коде (KButton + KImage со спрайтом `Assets.GetSprite("OverviewUI_database_icon")` + ToolTip) и размещается в позиции карандаша в шапке колонки (рядом с заголовком `characterName`, справа). При reshuffle старая кнопка заменяется новой (item меняется).
- Если для предмета нет записи в Codex (`CodexCache.entries.ContainsKey(id) || CodexCache.FindSubEntry(id) != null` — проверка как в `DetailsScreen.CodexEntryButton_Refresh`) — кнопка не показывается/не кликабельна.
- Логика открытия: `ManagementMenu.Instance.OpenCodexToEntry(codexId)` (публичный метод).
- Локализация/тексты: при необходимости PLib STRINGS + локализация (tooltip можно переиспользовать строку игры `UI.TOOLTIPS.OPEN_CODEX_ENTRY`).
- Документация: README мода на английском (по skill `mod-readme`) + строка в таблице модов в корневом `README.md`; changelog-запись.
- Логирование: `PUtil.LogDebug/LogWarning/LogError` + `.F(...)`, verbose в `#if DEBUG`.

## Constraints

- TFM `net48` везде; Harmony — **игровой** `$(GameLibsFolder)/0Harmony.dll` (v2), не NuGet `Harmony`.
- `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3, assets `build; contentfiles` только; ILRepack `dotnet-ilrepack` 2.0.45 (CLI `dotnet ILRepackTool.dll`) для `IsPacked`.
- В этой сборке игры (2026 build U59-744825-SCRPAN) **нет** классов `ItemProperties`/`TechDatabaseUI`/`TechTree`/`Item`; «база данных» = **Codex** (`CodexScreen`/`CodexCache`). Окно свойств предмета = `DetailsScreen`.
- Каркас окна: `ImmigrantScreen : CharacterSelectionController : KModalScreen`; колонки: `CharacterContainer` (дубликанты, с `EditableTitleBar`+карандаш) и `CarePackageContainer` (без карандаша — слот пуст).
- ID предмета care package = `CarePackageInfo.id` (строка: либо тег элемента, либо id префаба), доступен публично через `container.Info.id`; quantity — `Info.quantity`.
- Спрайт иконки — `OverviewUI_database_icon` (в игре назначен в префабе, в коде — `Assets.GetSprite(...)`).
- Сборка: `NUGET_PACKAGES="$PWD/.cache/nuget/packages" ... dotnet build ONI-mods.sln -c Debug`; `CopyModsToDevFolder` в песочнице падает (read-only) — ожидаемо, артефакты остаются в `bin/`.
- Единственная автоматическая проверка — успешная сборка; приёмка — ручной прогон в игре пользователем.

## Open questions

- Точный якорь RectTransform для кнопки в шапке care-package колонки уточняется в игре при приёмке (первый вариант: правый край шапки, рядом с заголовком).
- Показывать ли кнопку для элементов (вода/руды) — только если у элемента есть запись в Codex; при отсутствии записи — без кнопки.

## Changes

- 2026-09-25T13:42:40+03:00 -- v1, начальная спецификация (draft + research)
- 2026-09-25 -- v1.1 (bugfix после приёмки): при клике по иконке сначала закрывается окно «Выберите схему» (`CharacterSelectionController.OnPressBack()`, синхронно, как штатный ESC/крестик), только затем открывается запись в Codex — в игре не допускается два модальных окна одновременно (Codex открывался поверх/за окном выбора).
