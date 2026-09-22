# Research: 2026-09-22_workshop-vdf

Superficial context-gathering scan (no solution design). Sources: project files, docs, git history.

## 1. Solution layout

- `ONI-mods.sln` contains **5 projects** (AGENTS.md/CONTRIBUTING.md claim 2 — stale):
  - `BuildDoorOverWall`, `UtilLibs`, `SizeInTooltip`, `ReplaceBuildingMaterial`, `BestBuildDryWall` — all **net48**, both Debug and Release (Any CPU).
- Per-mod csproj (identical pattern in all 4 mods):
  ```xml
  7:         <PackageId>BuildDoorOverWall</PackageId>
  8:         <Version>0.0.1</Version>
  18:        <AssemblyName>$(PackageId)</AssemblyName>
  20:        <IsMod>true</IsMod>
  21:        <GenerateMetadata>true</GenerateMetadata>
  23:        <IsPacked>true</IsPacked>
  29:        <ModName>$(PackageId)</ModName>
  30:        <ModDescription />
  33:    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'">
  34:        <OutDir>bin</OutDir>
  35:    </PropertyGroup>
  ```
  → **Release output is flat `<Mod>/bin/`** (no `net48` subdir); Debug is standard `bin/Debug/net48/`.
- `UtilLibs.csproj`: `IsMod=false`, `DoNotBuildAsMod=true`, `GenerateMetadata=false`, `IsPacked=false` — the shared target gates off it.
- `Version` is a **hardcoded per-csproj property** (`0.0.1` for all 4 mods, `1.0.0` UtilLibs); already flows into `mod_info.yaml` (`Directory.Build.targets:43 version: $(Version)`).
- **No `SteamId` / `ModId` / `appid` / `publishedfileid` property exists anywhere** (only a commented-out `steamID` ModLine at `Directory.Build.targets:32`).

## 2. Existing targets in root `Directory.Build.targets` (the pattern to conform to)

Shared mod targets live in **root `Directory.Build.targets`**, gated by `DoNotBuildAsMod` / `IsPacked` / `Configuration`:
- `Clean` (BeforeTargets=PreBuildEvent): `RemoveDir $(TargetDir)`.
- `GenerateModYaml` (BeforeTargets=PrepareForRun, `Condition="'$(DoNotBuildAsMod)' != 'true'"`): writes `$(TargetDir)/mod.yaml` via a `@ModLines` item list + `WriteLinesToFile`; Release vs Debug title/staticID differ; quote-escaping via `.Replace('"', '\"')` (line 29).
- `GenerateModInfoYaml`: writes `$(TargetDir)/mod_info.yaml` (incl. `version: $(Version)`).
- `ILRepack` (AfterTargets=Build, `Condition="'$(IsPacked)' == 'true'"`): repacks into `$(TargetPath)` via dotnet-ilrepack 2.0.45 CLI.
- Flavour/folder properties (lines 82-86):
  ```xml
  <ModBuildFlavor Condition="'$(Configuration)' == 'Release'">release</ModBuildFlavor>
  <TargetFolder Condition="'$(GameLibsFolder)' != '../Lib'">$(ModFolder)\$(TargetName)_$(ModBuildFlavor)\</TargetFolder>
  ```
- `CopyModsToDevFolder` (AfterTargets=ILRepack, `Condition="'$(DoNotBuildAsMod)' != 'true'"`): copies `$(TargetDir)$(TargetName).dll; .pdb; mod.yaml; mod_info.yaml` (+ nonexistent `ModAssets/**`) into `$(TargetFolder)` = `.tmp/build_mod_dir/<Mod>_<dev|release>/` (from `Directory.Build.props.user`: `ModFolder=$(SolutionRoot).tmp/build_mod_dir`).
- Sandbox note (AGENTS.md): `CopyModsToDevFolder` copy to `~/ONI` fails MSB3027 read-only — expected, artifacts stay in `bin/`.

## 3. README.md / preview

- `README.md` exists at the project dir of all **4 mods** (BuildDoorOverWall, SizeInTooltip, ReplaceBuildingMaterial, BestBuildDryWall) + repo root. **Not** in `UtilLibs/`.
- README contents are Markdown, Russian (3 mods) or English (SizeInTooltip).
- **No preview image exists anywhere in the repo.** Three READMEs contain TODO screenshot placeholders, e.g. `BuildDoorOverWall/README.md:22`:
  ```
  <!-- TODO: добавить скриншот, например `![BuildDoorOverWall](screenshot.png)` -->
  ```

## 4. Release mod folder structure today

- Observed `<Mod>/bin/` (Release): repacked `<Mod>.dll`, `.pdb`, `PLib.dll`, `UtilLibs.dll`, `UtilLibs.pdb`, `mod.yaml`, `mod_info.yaml`.
- `.tmp/build_mod_dir/<Mod>_release/` currently contains: `<Mod>.dll`, `.pdb`, `mod.yaml`, `mod_info.yaml` (the "store mod" folder per AGENTS.md).
- Generated Release `mod.yaml`:
  ```yaml
  title: 'BuildDoorOverWall'
  description: ""
  staticID: BuildDoorOverWall
  ```

## 5. Docs / git history

- `docs/`: `i18n.md`, `mod_info.md`, `retro_fix_buld_door.md`. No VDF/workshop-publishing doc. `docs/mod_info.md` only notes that mod.yaml `title` defaults to "Steam Workshop name / folder name" and `staticID` to `<label.id>.Steam`.
- AGENTS.md / README.md / CONTRIBUTING.md: no workshop/publishing workflow section. CONTRIBUTING.md line 30: `dotnet build -c Release`.
- `.dumbspec/archive/`: 9 archived tasks; no prior workshop/VDF research notes.
- `git log --oneline --all | grep -iE 'workshop|vdf|publish|steam'` → **no matches** (108 commits).

## 6. Key facts summary

- Release artifacts land in flat `<Mod>/bin/` (`OutDir=bin` override per csproj); shared mod targets live in **root `Directory.Build.targets`** and are gated by `DoNotBuildAsMod`/`IsPacked`/`Configuration`.
- `ModName`/`ModDescription`/`Version` properties already exist per csproj (Description empty); no ModId/SteamId property anywhere.
- All 4 mods have a `README.md`; no preview image exists; `UtilLibs` has no README and is gated out of mod targets.
- No prior workshop/VDF work or research exists in the repo.

---

## 7. Web research: Steam `workshop_build.vdf` format facts

Single focused web research (Steamworks docs + community examples + local decompiled game code).

### Visibility values

`visibility` maps to `ERemoteStoragePublishedFileVisibility` (per [ISteamUGC::SetItemVisibility](https://partner.steamgames.com/doc/api/ISteamUGC) and [Steam Workshop Implementation Guide](https://partner.steamgames.com/doc/features/workshop/implementation)):

| Value | Meaning |
|---|---|
| 0 | public (visible to everyone) |
| 1 | friends-only |
| 2 | **private** (visible to creator only) |
| 3 | unlisted (visible, excluded from global queries) |

→ The draft's `visibility = 2` means **private**, not "unlisted".

### VDF string escaping

- Valve's KeyValues doc: allowed escape sequences inside quoted strings are `\n`, `\t`, `\\`, `\"` (typo in original). SDK parser applies escapes only with `KeyValues::UsesEscapeSequences(true)` — off by default.
- Empirical proof that Steam's parser for this exact file type does backslash unescaping: the Steamworks doc example writes `"contentfolder" "D:\\Content\\workshopitem"` — **Windows paths must double their backslashes**.
- **Proven working form for multi-line description: raw newlines inside the quoted string** — a real published Project Zomboid mod's `workshop_build.vdf` has a single quoted `description` spanning many physical file lines ([example](https://github.com/cyrusduong/project-zomboid-drying-racks-fixed-b42-mp/blob/main/workshop_build.vdf)). Literal `\n` sequences are a documented escape and "very likely" to yield newlines, but the raw-newline form is what working files use.

### How ONI consumes this file

**The game never reads `workshop_build.vdf`.** Verified in local decompiled sources:
- No reference to `workshop_build`/`workshopitem`/VDF parsing in `Assembly-CSharp`/`KMod`.
- Steam mods: `KMod/Steam.cs` — subscribed item ids from `SteamUGCService`, installed as **zip** (`new ZipFile(pchFolder)`).
- Local mods: `KMod/Local.cs` — enumerates subfolders, reads `mod.yaml`/`mod_info.yaml` only.
- `appid 457140` is correct for ONI (`ModsScreen.cs` uses it in workshop browse links).
- `contentfolder`/`previewfile` are machine-local absolute paths — meaningful only to the tool consuming the VDF (steamcmd / the "Oxygen Not Included Uploader", appid 636750).

### Nature of the file

**Purely a local build/publish artifact — never uploaded with the mod, never part of workshop content.** Per the [Steam Workshop Implementation Guide](https://partner.steamgames.com/doc/features/workshop/implementation): this VDF is the input to `steamcmd +workshop_build_item <file>` ("should only be used for testing purposes", requires Steam credentials). Steam uploads the **content of `contentfolder`** (zipped, matching the game's zip load). On success steamcmd rewrites `publishedfileid` back into the VDF so subsequent runs **update** the same item. Established pattern in other ecosystems (e.g. RimWorld DeltaV keeps the vdf in the repo, copies it into the build folder, runs steamcmd in CI — the game never sees it).

Implication: to *update* an existing item the file must carry the real `publishedfileid`; a fresh item gets one assigned by steamcmd.

---

## 8. Web/tool research: `Converter.MarkdownToBBCodeNM.Tool` (2026-09-22, addition to scope)

Facts from a local probe (scratch project in `.tmp/toolprobe/`, package cache in `.cache/nuget/packages/`) + the repo's existing tool-embedding pattern.

### Package facts

- Package `Converter.MarkdownToBBCodeNM.Tool` **1.0.0.29** is a **DotnetTool-type** NuGet package (nuspec has no ToolCommandName/tool section; that belongs to the sibling Steam CLI package).
- On disk the package dir is **all-lowercase**: `.cache/nuget/packages/converter.markdowntobbcodenm.tool/1.0.0.29/`.
- Two tool TFMs in the package: `tools/net8.0/any/` and `tools/net10.0/any/`. This machine runs .NET SDK 8.0.131 (only 8.0.31 runtime installed) → the build must use the **`net8.0`** variant.
- Executable tool dll (relative to package root): `tools/net8.0/any/Converter.MarkdownToBBCodeNM.Tool.dll` (along with `Converter.MarkdownToBBCode.Shared.dll`, `Markdig.dll`, `HtmlAgilityPack.dll`, `CommandLine.dll`, `deps.json`, `runtimeconfig.json`).
- **A plain `PackageReference` with `PrivateAssets=all` + `ExcludeAssets=all` restores cleanly** (verified with a minimal net8.0 probe project — `.tmp/probe2/`); this is exactly the `dotnet-ilrepack` pattern in this repo. (An earlier probe with `DotnetToolReference` inside a plain Exe project failed with NU1212 — that probe setup, not the package, was the problem.)

### Existing pattern to mirror (verbatim)

`Directory.Build.props` lines 218–223:
```xml
<ItemGroup Condition="'$(IsPacked)' == 'true'">
    <PackageReference Include="dotnet-ilrepack" Version="2.0.45">
        <PrivateAssets>all</PrivateAssets>
        <ExcludeAssets>all</ExcludeAssets>
    </PackageReference>
</ItemGroup>
```
`Directory.Build.targets` ILRepack target: property `IlrepackToolPath` = `$([System.IO.Path]::Combine('$(NuGetPackageRoot)', 'dotnet-ilrepack', '2.0.45', 'tools', 'net8.0', 'any', 'ILRepackTool.dll'))`, run as `Exec Command="dotnet &quot;$(IlrepackToolPath)&quot; …"`.

### CLI behavior (empirical)

- Confirmed flags: `-i/--input` (raw markdown text **or** a file path), `-o/--output` (write to file instead of stdout), `-d/--disableextended` (disables two-space newline detection + HTML conversion).
- `dotnet <tool dll> -i <repo>/BuildDoorOverWall/README.md -o .tmp/probe_bbcode.txt` → exit 0, output 2977 bytes. Output is proper Steam-style BBCode: `[size=6]BuildDoorOverWall[/size]`, `[size=5]…[/size]` headings, `[b]…[/b]`, `[list] [*] …[/list]`, `«»` curly quotes preserved.
- With and without `--disableextended`: for this README the outputs are **identical** (2977 bytes, zero diff) — the current READMEs contain no two-space line breaks or HTML, so the flag makes no difference today; default (no flag) is fine and easy to change in one place later.
