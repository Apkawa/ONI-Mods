# Apkawa ONI Mods

Oxygen Not Included (ONI) mods. Currently building: **BuildDoorOverWall** (+ shared **UtilLibs**).

## Requirements

* .NET SDK **8.0** (`sudo apt install -y dotnet-sdk-8.0` on Ubuntu 24.04).
  Full .NET Framework is *not* required: the net48 targeting pack comes from the
  `Microsoft.NETFramework.ReferenceAssemblies.*` NuGet packages (already wired in
  `Directory.Build.props` for non-Windows).
* The game installed, so `~/ONI/dlls` exists — the build references the game's own
  DLLs (`Assembly-CSharp`, `Assembly-CSharp-firstpass`, **0Harmony v2**, UnityEngine, Newtonsoft.Json).
* Optional: `dotnet tool install --global ilspycmd` (decompile game/mod assemblies).

## Quick build

From the repo root:

For debug:
* `dotnet build`
* For sandbox: 
```sh
export NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache"
dotnet build ONI-mods.sln -c Debug
```

For release:

```sh
dotnet build -c Release
```

NuGet caches live in the repo under `.cache/` (gitignored, together with `.tmp/` scratch).

Result:

* `BuildDoorOverWall/bin/Debug/net48/BuildDoorOverWall.dll` — **packed** single dll
  (UtilLibs + PeterHan.PLib inlined), plus generated `mod.yaml` / `mod_info.yaml`.
* `CopyModsToDevFolder` then copies dll + pdb + yamls to the per-configuration
  folder — `~/ONI/mods/BuildDoorOverWall_dev/` for Debug,
  `~/ONI/mods/BuildDoorOverWall_release/` for Release — ready for the in-game
  mod manager. (Inside a sandbox that step may fail with
  "Read-only file system"; the artifacts remain in `bin/` and can be copied by hand.)
  The Debug build advertises itself as `BuildDoorOverWall [DEBUG]`
  (`staticID: BuildDoorOverWall_dev`) so the installed flavor is always visible;
  Release keeps the plain title/staticID. Enable only ONE flavor at a time —
  both active would run the mod logic (and patches) twice.
* Release builds use `<OutDir>bin</OutDir>` → output lands in `BuildDoorOverWall/bin/`
  directly (no `net48/` subfolder).

## Project layout

| Path | What |
|---|---|
| `BuildDoorOverWall/` | the mod (`IsMod=true`, `IsPacked=true`) |
| `UtilLibs/` | shared code + **PeterHan.PLib 4.19.0**; not a mod (`DoNotBuildAsMod`) |
| `example/`, `DebugButton/` | disabled mods (already `net48`, removed from the sln — see below) |
| `UpdatedOniTemplate/` | project template — intentionally **not** in the sln |
| `Directory.Build.props` | game refs, publicizer, ref-assemblies, ILRepack tooling |
| `Directory.Build.targets` | Clean, yaml generation, ILRepack, copy to mods folder |
| `Directory.Build.props.user` | machine paths (`GameLibsFolder`, `ModFolder`, `RefasmerInstalled`); `.default` is the fallback |
| `PublicisedAssembly/` | legacy publicized dlls — unused (in-process publicizer replaced them) |
| `tile_rep.dll` | stray file at repo root — never reference it |

## Key build facts (learned the hard way)

1. **TFM is `net48`, not net471.** The game's `0Harmony.dll` targets .NET 4.8; with
   net471, MSBuild drops `Assembly-CSharp` (MSB3275) and *every* game type becomes
   CS0246 — with no CS0006 at all.
2. **Harmony v2 comes from the game.** Reference `$(GameLibsFolder)/0Harmony.dll`
   (0Harmony 2.4.2). Do **not** add NuGet `Harmony`: that package is 0Harmony **1.x**
   (`Harmony.dll`) — different type identity, and `UserMod2.OnLoad(Harmony)` expects
   the game's v2 Harmony.
3. **The game loads every `*.dll` in a mod folder** (`KMod/DLLLoader`): up to one
   `UserMod2` subclass per assembly, one `new Harmony(id)` per mod folder. So the
   packed single dll is a convenience, not a requirement — side-by-side dependency
   dlls would work too.
4. **Publicization runs in-process**: `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3 with
   `<Publicize>true</Publicize>` on the game Assembly references (copies land in
   `obj/…/publicized/`). Its PackageReference must stay
   `<IncludeAssets>build; contentfiles</IncludeAssets>` — adding `runtime` leaks
   `AsmResolver*`/`BepInEx.*.dll` into `bin/`.
5. **ILRepack is the `dotnet-ilrepack` CLI, not an MSBuild task.** The old
   `ILRepack.Lib.MSBuild.Task` 2.0.12 cannot load under the dotnet SDK (MSB4062 —
   needs `Microsoft.Build.Utilities.v4.0` / full Framework MSBuild). The build
   references the `dotnet-ilrepack` package and runs `dotnet ILRepackTool.dll`
   directly. Pinned to **2.0.45** — last release targeting net8 (2.0.46+ need .NET 10).
   Runs only for projects with `IsPacked=true`.

## Decompilation

As example use `ilspycmd`

```sh
DOTNET_ROLL_FORWARD=Major ilspycmd path/to/oni-game/OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll -o ./Assembly-CSharp -p
```

## Disabled solution projects

`ONI-mods.sln` currently contains only **BuildDoorOverWall** and **UtilLibs**.
MSBuild's sln parser has **no comment support** — a commented-out `Project(` line is
still parsed (MSB4121), so disabled projects are removed from the file, not
commented out. To re-enable, add back the `Project(...)` line *and* the four
`ProjectConfigurationPlatforms` lines:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "example", "example\example.csproj", "{31DF774A-8818-45ED-A745-C1A3254608AC}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DebugButton", "DebugButton\DebugButton.csproj", "{B430901D-43AD-4991-936F-8290599C4A8A}"
EndProject
```

plus, per GUID:

```
{31DF774A-8818-45ED-A745-C1A3254608AC}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{31DF774A-8818-45ED-A745-C1A3254608AC}.Debug|Any CPU.Build.0 = Debug|Any CPU
{31DF774A-8818-45ED-A745-C1A3254608AC}.Release|Any CPU.ActiveCfg = Release|Any CPU
{31DF774A-8818-45ED-A745-C1A3254608AC}.Release|Any CPU.Build.0 = Release|Any CPU
{B430901D-43AD-4991-936F-8290599C4A8A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{B430901D-43AD-4991-936F-8290599C4A8A}.Debug|Any CPU.Build.0 = Debug|Any CPU
{B430901D-43AD-4991-936F-8290599C4A8A}.Release|Any CPU.ActiveCfg = Release|Any CPU
{B430901D-43AD-4991-936F-8290599C4A8A}.Release|Any CPU.Build.0 = Release|Any CPU
```

(Both projects are already `net48`, so they just work. `DebugButton` has
`IsPacked=true` → same ILRepack step.)

## Game path locations:
### In-game logs

* Windows: `%HOMEPATH%\Documents\Klei\OxygenNotIncluded\Player.log`
* `C:\Users\%username%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`
* Linux: `~/.config/unity3d/Klei/Oxygen Not Included/Player.log`

### Mods location

* Windows: `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\`
* Linux: `~/.config/unity3d/Klei/Oxygen Not Included/mods/`

# Usefull mods

* [Mod Preset Manager (Light) [MPML]](https://steamcommunity.com/sharedfiles/filedetails/?id=3281716506)
* [Debug Console](https://steamcommunity.com/sharedfiles/filedetails/?id=2041219184) for windows
* [Debug Buttons](https://steamcommunity.com/sharedfiles/filedetails/?id=3120193648)

## Links

* [Cairath/Oxygen-Not-Included-Modding wiki](https://github.com/Cairath/Oxygen-Not-Included-Modding/wiki)
* [[Tutorial] How to create a basic mod for ONI
  ](https://forums.kleientertainment.com/forums/topic/107833-tutorial-how-to-create-a-basic-mod-for-oni/)
* https://github.com/O-n-y/OxygenNotIncludedModTemplate
* https://gist.github.com/EliteMasterEric/cc9f0271af9410aec32ead637efe7741
* https://harmony.pardeike.net/articles/patching-prefix.html
* https://github.com/javisar/ONI-Modloader

### Mod examples

* https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods
* https://github.com/aki-art/ONI-Mods
* https://github.com/peterhaneve/ONIMods
* https://github.com/Cairath/ONI-Mods

### Usefull c# hints 

* [Porting MSBuild Projects To XBuild](https://www.mono-project.com/archived/porting_msbuild_projects_to_xbuild/#prepostbuildevents)
* [MSBuild variables](https://learn.microsoft.com/en-us/cpp/build/reference/common-macros-for-build-commands-and-properties?view=msvc-170&source=recommendations)


# Troubleshooting

Q: `error MSB3644: The reference assemblies for framework ".NETFramework,Version=v4.x" were not found`
A: Handled automatically: `Directory.Build.props` pulls
`Microsoft.NETFramework.ReferenceAssemblies.net471/net48` (each activates only for
its own TFM) on non-Windows. Manually:
`dotnet add package Microsoft.NETFramework.ReferenceAssemblies.net48 --version 1.0.3`

Q: All game types are CS0246 "not found" (no CS0006), MSBuild log shows MSB3275
A: TFM mismatch — project is `net471` while the game dlls need net48. Bump
`<TargetFramework>` to `net48` (Key build facts #1).

Q: CS0246 for Harmony types / `UserMod2.OnLoad` mismatch at runtime
A: You referenced NuGet `Harmony` (0Harmony 1.x) instead of the game's
`0Harmony.dll` (v2). Remove the package, keep the `0Harmony` Reference
(Key build facts #2).

Q: `error MSB4062: The "ILRepack" task could not be loaded … Microsoft.Build.Utilities.v4.0`
A: Don't reintroduce `ILRepack.Lib.MSBuild.Task` — it only loads under full
Framework MSBuild (Windows VS). On the dotnet SDK the build uses the
`dotnet-ilrepack` CLI instead (Key build facts #5).

Q: `MSB3027 … Read-only file system` when copying to `~/ONI/mods/`
A: The mods folder is outside the writable area (e.g. the agent sandbox mounts
everything but the workspace read-only). The build artifacts are complete in
`<proj>/bin/…/` — copy dll + pdb + `mod.yaml` + `mod_info.yaml` to
`~/ONI/mods/<ModName>_dev/` by hand, or rerun the build in a normal shell.

Q: NuGet has error `Access to the path '/…-….tmp' is denied.`
A: ```
sudo apt install nuget

dotnet nuget locals all -c
dotnet clean
dotnet restore -v d
dotnet build
```
Or simply point `NUGET_PACKAGES` / `NUGET_HTTP_CACHE_PATH` at `.cache/` (see Quick build).
