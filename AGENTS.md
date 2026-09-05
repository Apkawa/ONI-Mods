# Paths 

- **Game dlls:** `~/ONI/dlls`
- **Location for store mods:** `./.tmp/build_mod_dir/`
- **Location for game log:** `~/ONI/logs/Oxygen Not Included/Player.log`
- In the agent sandbox `~/ONI` is **read-only** (workspace-write policy): the
  `CopyModsToDevFolder` step then fails with "Read-only file system" (MSB3027).
  That is expected — artifacts stay in `bin/`, never escalate for this.

# Tools

- `DOTNET_ROLL_FORWARD=Major ~/.dotnet/tools/ilspycmd ~/ONI/dlls/Assembly-CSharp.dll -o ./Assembly-CSharp -p` (decompile game code)
- Build (from repo root; caches MUST stay in `.cache/`):
  `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug`
- Scratch files → `./.tmp/`, caches → `./.cache/` (both gitignored)

# Build invariants (do not regress)

- TFM is **net48** everywhere: the game `0Harmony.dll` targets 4.8, and on net471
  MSBuild drops Assembly-CSharp (MSB3275) → all game types CS0246.
- Harmony: reference the **game's** `$(GameLibsFolder)/0Harmony.dll` (v2). Never add
  NuGet `Harmony` (that is 0Harmony 1.x — wrong type identity for `UserMod2.OnLoad`).
- Publicizer: `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3, assets `build; contentfiles`
  ONLY (adding `runtime` leaks AsmResolver/*.dll into bin/).
- ILRepack: `dotnet-ilrepack` **2.0.45** package, run as `dotnet ILRepackTool.dll`
  (pinned: 2.0.46+ need .NET 10). The old `ILRepack.Lib.MSBuild.Task` does NOT load
  under the dotnet SDK (MSB4062) — do not bring it back.
- `ONI-mods.sln`: MSBuild's sln parser has **no comment support** — disable projects
  by removing their `Project(` + 4 configuration lines, never by commenting them out.
- Currently only **BuildDoorOverWall** + **UtilLibs** are in the sln; `example` /
  `DebugButton` are net48-ready but excluded (re-enable lines are in README.md).
- `UpdatedOniTemplate/` is a template — not in the sln, never build it.
- `tile_rep.dll` at repo root is a stray file — never glob it into builds.
- `UtilLibs` carries **PeterHan.PLib 4.19.0** (shared UI/util code the mod uses);
  keep the ProjectReference and `IsPacked=true` so it gets packed into the mod dll.
- Game runtime fact: `KMod/DLLLoader` loads **every** `*.dll` in a mod folder, finds
  ≤1 `UserMod2` subclass per assembly, and creates one `Harmony` per mod folder.

# Docs and example mods

root: /home/apkawa/code/ONI_MODS/

## Library source

- ./lib_sources/Assembly-CSharp/ - decompiled Assembly-CSharp.dll 
- ./lib_sources/Assembly-CSharp-firstpass/ - decompiled Assembly-CSharp-firstpass.dll 
- ./lib_sources/0Harmony/ - decompiled 0Harmony.dll 
- ./lib_sources/peterhaneve_ONIMods/PLib* - PLib sources


## Docs and guides

- ./example_mods/Oxygen-Not-Included-Modding_wiki
- ./example_mods/Oxygen-Not-Included-Modding


## Mods (for example)

- ./example_mods/ - A collection for examples
- ./lib_sources/peterhaneve_ONIMods/
