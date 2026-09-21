# 2026-09-21_extract-findmethod: research

## FindMethod — definitions

Two identical-by-body private copies, one per mod. Both signatures:

`private static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)`

1. **BuildDoorOverWall** — `BuildDoorOverWall/Mod.cs:230` (body lines 230–263)
2. **ReplaceBuildingMaterial** — `ReplaceBuildingMaterial/Mod.cs:237` (body lines 237–270)

Body (verbatim, both copies):
```csharp
private static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)
{
    foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (m.Name != name)
        {
            continue;
        }
        ParameterInfo[] ps = m.GetParameters();
        if (ps.Length != underlyingTypes.Length)
        {
            continue;
        }
        bool match = true;
        for (int i = 0; i < ps.Length; i++)
        {
            Type pt = ps[i].ParameterType;
            if (pt.IsByRef)
            {
                pt = pt.GetElementType();
            }
            if (pt != underlyingTypes[i])
            {
                match = false;
                break;
            }
        }
        if (match)
        {
            return m;
        }
    }
    return null;
}
```

Behavior: scans `DeclaredOnly` methods (public+nonpublic, instance+static) of the given type, matches exact name, exact arity, and per-parameter underlying type (byref `T&` normalized to `T`). First full match wins; returns `null` if none — callers log a skip instead of crashing.

## FindMethod — call sites

All call sites are inside the two mods' `Mod.cs` files (Harmony target resolution in `OnLoad`). No other project or file references the name (aside from a Russian-language retro doc mentioning it).

**BuildDoorOverWall/Mod.cs:**
- :54 — `FindMethod(typeof(BuildingDef), "IsValidPlaceLocation", ...)` → `validPlace`
- :70 — `FindMethod(typeof(BuildingDef), "IsValidReplaceLocation", ...)` → `validReplace`
- :88 — `FindMethod(typeof(BuildTool), "TryBuild", typeof(int))` → `tryBuild`
- :100 — `FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef))` → `addBuildingDef`
- :120 — `FindMethod(typeof(BuildingDef), "IsValidPlaceLocation", ...)` (6-parameter overload) → `validPlace6`
- :155 — `FindMethod(typeof(BuildingDef), "IsValidBuildLocation", ...)` (5 params) → `validBuild`
- :173 — `FindMethod(typeof(Constructable), "FinishConstruction", typeof(UtilityConnections), typeof(WorkerBase))` → `finishConstruction`
- :202 — `FindMethod(typeof(BuildingDef), "TryReplaceTile", ...)` (5 params) → `tryReplaceTile`
- (comments referencing it at :50, :98, :118, :152, :172, :200)

**ReplaceBuildingMaterial/Mod.cs:**
- :38 — `FindMethod(typeof(Assets), "AddBuildingDef", typeof(BuildingDef))` → `addBuildingDef`
- :61 — `FindMethod(typeof(BuildTool), "TryBuild", typeof(int))` → `tryBuild`
- :75 — `FindMethod(typeof(BuildTool), "OnDragTool", typeof(int), typeof(int))` → `onDragTool`
- :94 — `FindMethod(typeof(BuildingDef), "IsValidReplaceLocation", ...)` → `isValidReplaceLocation`
- :116 — `FindMethod(typeof(BuildingDef), "IsValidPlaceLocation", ...)` (4-arity byref overload) → `isValidPlaceLocation4`
- :140 — `FindMethod(typeof(BuildingDef), "ArePowerPortsInValidPositions", typeof(GameObject), typeof(int), typeof(Orientation), typeof(string))` → `arePowerPorts`
- :157 — `FindMethod(typeof(BuildingDef), "AreConduitPortsInValidPositions", typeof(GameObject), typeof(int), typeof(Orientation), typeof(string))` → `areConduitPorts`
- :175 — `FindMethod(typeof(BuildingDef), "AreLogicPortsInValidPositions", typeof(GameObject), typeof(int), typeof(string))` → `areLogicPorts`
- :191 — `FindMethod(typeof(BuildingDef), "MarkOverlappingPorts", typeof(GameObject), typeof(GameObject))` → `markOverlappingPorts`
- :216 — `FindMethod(typeof(Constructable), "FinishConstruction", typeof(UtilityConnections), typeof(WorkerBase))` → `finishConstruction`
- (comments at :27, :54, :93, :114, :213)

**Docs (non-code mentions, Russian retro doc):**
- `docs/retro_fix_buld_door.md:33` — refers to `FindMethod` in `BuildDoorOverWall/Mod.cs`
- `docs/retro_fix_buld_door.md:115`, `:149` — `FindMethod` fallback → `Debug.LogError`

## FindMethod — differences between copies

- **Signature: identical** in both (`private static MethodInfo FindMethod(Type, string, params Type[])`).
- **Code body: byte-identical** (verified line-by-line).
- **Only the XML doc comment differs**, verbatim:
  - BuildDoorOverWall/Mod.cs:226-228: "BindingFlags.Static was added (Stage 2.2) so the declared-only scan also finds the static Assets.AddBuildingDef target; instance lookups are unaffected — no C# signature is both static and instance."
  - ReplaceBuildingMaterial/Mod.cs:233-235: "BindingFlags.Static included so the declared-only scan also finds the static Assets.AddBuildingDef target; instance lookups are unaffected — no C# signature is both static and instance."
  (i.e. "was added (Stage 2.2)" vs "included" — purely historical wording.)
- They live in different classes (one `private` per mod; see class listing below).

## UtilLibs project structure

- Project file: `UtilLibs/UtilLibs.csproj`
- Files: `UtilLibs/Class1.cs`, `UtilLibs/Properties/AssemblyInfo.cs`
- (csproj details — TFM, references, IsPacked/ILRepack — recorded in next section)

## Solution membership & how mods reference UtilLibs

`ONI-mods.sln` currently contains **4 projects** (Format Version 12.00, GUID type FAE04EC0 = C#):

1. `BuildDoorOverWall` — `{D298ADB7-CCBD-4EC0-A42C-EA03FBF00B78}`
2. `UtilLibs` — `{3606AD3B-C8FA-40E7-833E-49F3FAC20061}`
3. `SizeInTooltip` — `{9E88A8DC-1A59-4339-BA4E-F2246D3BD181}`
4. `ReplaceBuildingMaterial` — `{D75547EA-D35D-4217-97AE-348CC2E99918}`

Each with standard Debug|Any CPU + Release|Any CPU configuration lines. (Note: repo-root AGENTS.md says only BuildDoorOverWall + UtilLibs are in the sln — the actual sln also includes SizeInTooltip and ReplaceBuildingMaterial; see "Surprises".)

**How a mod references UtilLibs** — every mod csproj has the same `ItemGroup` (verbatim, in all three mods `BuildDoorOverWall`, `ReplaceBuildingMaterial`, `SizeInTooltip`):

```xml
<ItemGroup>
    <ProjectReference Include="..\UtilLibs\UtilLibs.csproj" />
</ItemGroup>
```

Mod csproj packing-related lines (identical across the three mods):

```xml
<IsMod>true</IsMod>
<GenerateMetadata>true</GenerateMetadata>
<!--        Enable if added external dll like PLib -->
<IsPacked>true</IsPacked>
```

UtilLibs' own csproj:

```xml
<IsMod>false</IsMod>
<DoNotBuildAsMod>true</DoNotBuildAsMod>
<GenerateMetadata>false</GenerateMetadata>
<!--        Enable if added external dll like PLib -->
<IsPacked>false</IsPacked>

<PackageReference Include="PLib" Version="4.19.0" />
```

So: UtilLibs itself is NOT packed (`IsPacked=false`); the **mod** projects set `IsPacked=true`, and `Directory.Build.targets` defines the `ILRepack` target (`AfterTargets="Build"`, `Condition="'$(IsPacked)' == 'true'"`) which runs `dotnet ILRepackTool.dll` (from the `dotnet-ilrepack` 2.0.45 NuGet package, `$(NuGetPackageRoot)/dotnet-ilrepack/2.0.45/tools/net8.0/any/ILRepackTool.dll`) merging `$(TargetDir)\*.dll` (excluding Harmony, Splat, Assembly-*, _public, Newtonsoft.Json, System.*, Microsoft.*, Unity*) into the mod dll — that is how UtilLibs.dll (and PLib) end up packed into the mod assembly.

## Build constraints from AGENTS.md (repo root) — fixed constraints

- **TFM:** `net48` everywhere (invariant: game `0Harmony.dll` targets 4.8; net471 drops Assembly-CSharp, MSB3275 → CS0246s). Confirmed in every csproj: `<TargetFramework>net48</TargetFramework>`.
- **Build command** (from repo root, caches in `.cache/`):
  `NUGET_PACKAGES="$PWD/.cache/nuget/packages" NUGET_HTTP_CACHE_PATH="$PWD/.cache/nuget/http-cache" dotnet build ONI-mods.sln -c Debug`
- ILRepack: `dotnet-ilrepack` **2.0.45** pinned (2.0.46+ need .NET 10); run as `dotnet ILRepackTool.dll`; old `ILRepack.Lib.MSBuild.Task` must not return (MSB4062).
- Harmony: reference game's `$(GameLibsFolder)/0Harmony.dll` (v2), never NuGet `Harmony`.
- Publicizer: `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3, assets `build; contentfiles` only.
- sln has no comment support — disable projects by removing the `Project(` line + 4 configuration lines.
- Only automated verification is that `dotnet build` succeeds; acceptance is manual in-game by the user.

## UtilLibs project structure (full)

- Project file: `UtilLibs/UtilLibs.csproj` (SDK-style `Microsoft.NET.Sdk`, `net48`, `RootNamespace UtilLibs`, `AssemblyName UtilLibs`, `IsMod=false`, `DoNotBuildAsMod=true`, `IsPacked=false`, NuGet `PLib` 4.19.0).
- Source files (only 2; `Properties/**` removed from compile):
  - `UtilLibs/Class1.cs` — namespace `UtilLibs`, single type: `public class Class1` — **empty body** (no members).
  - `UtilLibs/Properties/AssemblyInfo.cs` — standard assembly-info (excluded from compilation items).
- Main public types: effectively none yet — `UtilLibs.Class1` is a placeholder.

## FindMethod containing classes (context)

- `BuildDoorOverWall/Mod.cs` — `namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2` (class at line 15); `FindMethod` is a `private static` member of `Mod`.
- `ReplaceBuildingMaterial/Mod.cs` — `namespace OxygenNotIncluded.Mods`, `public class Mod : UserMod2` (class at line 16); `FindMethod` is a `private static` member of `Mod`.

## Surprises / notable

1. **AGENTS.md is stale about sln membership:** it says "Currently only BuildDoorOverWall + UtilLibs are in the sln", but the actual `ONI-mods.sln` lists 4 projects (BuildDoorOverWall, UtilLibs, SizeInTooltip, ReplaceBuildingMaterial).
2. **AGENTS.md's UtilLibs note is imprecise:** it says "keep the ProjectReference and IsPacked=true" for UtilLibs, but `UtilLibs/UtilLibs.csproj` has `IsPacked=false`; the `IsPacked=true` + ILRepack lives on the *mod* side, which is what actually packs UtilLibs.dll into the mod dll.
3. **FindMethod is duplicated** (copy-paste) across two mods with byte-identical bodies — an obvious candidate for extraction into UtilLibs (out of scope for this scan).
4. **UtilLibs is an empty shell:** its only type is an empty `Class1`; it currently serves mainly as a PLib carrier / future shared library.
5. `SizeInTooltip` (in sln, no csproj-level differences) does not use FindMethod at all.
6. The retro doc `docs/retro_fix_buld_door.md` references FindMethod behavior (fallback → `Debug.LogError`, not silence), which matches the `null`-return + caller-skip pattern.

## AccessTools.Method — decompiled algorithm

Source: `lib_sources/0Harmony/HarmonyLib/AccessTools.cs` (0Harmony v2 decompiled). Overload at line 552:

```csharp
public static MethodBase Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
```

Full algorithm (AccessTools.cs line numbers):

1. Guards: `type == null` → `FileLog.Error` + `return null` (554–558); `name == null || name.Length == 0` → `FileLog.Error` + `return null` (559–563).
2. `ParameterModifier[] modifiers = new ParameterModifier[0];` (564).
3. **`parameters == null` branch** (565–578):
   - `try { methodInfo = FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all)); }` (570)
   - `catch (AmbiguousMatchException)` → retry `FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all, null, Array.Empty<Type>(), modifiers))` (573–574); if that is null → `throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {type}:{name}")` (575–577).
4. **`parameters != null` branch** (580–583): `methodInfo = FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all, null, parameters, modifiers));`
5. `if (methodInfo == null) { FileLog.Debug(...); return null; }` (585–589).
6. `generics != null` → `methodInfo = methodInfo.MakeGenericMethod(generics);` (590–593).
7. `return methodInfo;` (594).

Helper `FindIncludingBaseTypes` (133–146): iterates `type`, then `for (Type currentType = type.BaseType; currentType != typeof(object); currentType = currentType.BaseType)` calling the predicate on each, first non-null wins (derived → base order).

**a. BindingFlags / inherited methods.** The lookup uses the field `all` (line 29): `BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty`. **`DeclaredOnly` is NOT present.** Inherited methods ARE included — both because the flags omit `DeclaredOnly` and because `FindIncludingBaseTypes` (133–146, used at 570 and 583) walks the base-type chain up to `object`, so a public/protected member declared on a base type can be returned. (Non-public inherited members are still not visible, per BCL `GetMethods` semantics: nonpublic members only come from their declaring type.)

**b. Parameter-type comparison — NO byref normalization.** With `parameters != null`, matching is fully delegated to the BCL: `t.GetMethod(name, all, null, parameters, modifiers)` (583). `AccessTools.Method` performs no `IsByRef`/`GetElementType` normalization anywhere — the only `IsByRef` in the whole `AccessTools.cs` is at line 1314, inside `MethodDelegate` (delegate generation), not in the `Method` lookup. BCL `Type.GetMethod` with `binder == null` compares parameter types by exact `Type` equality (no covariance/boxing), and a byref parameter's type (`T&`) is not equal to `T`. In-repo evidence (BuildDoorOverWall/Mod.cs:36–48): the mod comments state Harmony v2's attribute resolution also goes through `Type.GetMethod(name, allDeclared, null, paramTypes, [])` and that "plain `typeof(string)` does not match the `out string` parameter — only `string&` (ByRef) does". So `AccessTools.Method(typeof(BuildingDef), "IsValidPlaceLocation", typeof(GameObject), typeof(Vector3), typeof(Orientation), typeof(string))` resolves to **null** for the `out string` overload (game BuildingDef.cs:1098).

**c. Match order / first-match.** No explicit sort. Per type, the BCL `Type.GetMethod` walks the type's methods in metadata (declaration) order and returns the first exact-signature match; `FindIncludingBaseTypes` (135–145) tries the given type first, then base types up the chain. First exact full signature wins.

**d. No-match behavior.** With `parameters != null`: **returns null** (585–589) after a `FileLog.Debug` — this is the null contract SizeInTooltip relies on (Mod.cs:52). With `parameters == null`: does NOT return null on ambiguity — throws `AmbiguousMatchException` if the name has no parameterless match (575–577). Null `type`/`name` inputs also return null (554–563).

**e. Static vs instance / generics.** No static-vs-instance difference: `all` (line 29) includes both `Instance | Static`. Generics: only the optional `generics` parameter is special — it calls `MakeGenericMethod(generics)` (590–593); without it, a generic method definition is returned unbound, and its `T` parameters will not match concrete argument types.

## SizeInTooltip AccessTools usage

File: `SizeInTooltip/Mod.cs`. Usings (lines 1–8): `System`, `System.Reflection`, **`HarmonyLib` (line 3 — yes, imported)**, `KMod`, `STRINGS`, `UnityEngine`, `PeterHan.PLib.Core`.

Exactly **one** `AccessTools.Method` call site — line 51:

```csharp
MethodInfo drawInstructions = AccessTools.Method(typeof(HoverTextConfiguration), nameof(HoverTextConfiguration.DrawInstructions), new[] { typeof(HoverTextScreen), typeof(HoverTextDrawer) });
```

Surrounding pattern (51–62):

- Target: `HoverTextConfiguration.DrawInstructions(HoverTextScreen, HoverTextDrawer)` — `protected` in the game source (decompiled HoverTextConfiguration.cs:72), but the game assembly is publicized at build time (Directory.Build.props `Publicize=true`), so it is public in the compiled reference (comment Mod.cs:47–50). Instance, non-generic, no byref parameters.
- Null check (52–55): `if (drawInstructions == null) { PUtil.LogError("could not resolve HoverTextConfiguration.DrawInstructions(HoverTextScreen, HoverTextDrawer) — patch skipped (game build mismatch?)"); }` — confirms the null-return contract.
- Patch (58): `harmony.Patch(drawInstructions, prefix: new HarmonyMethod(typeof(HoverTextConfiguration_DrawInstructions_SizeInTooltip__Patch), nameof(HoverTextConfiguration_DrawInstructions_SizeInTooltip__Patch.Prefix)));` — **prefix only**; no postfix/suffix/transpiler.
- `#if DEBUG` log (59–61): `PUtil.LogDebug("size prefix attached to {0}".F(drawInstructions));`

## 18 call-site requirements table

Argument types = what the call passes to `FindMethod` (plain types, no `&`). "Byref" = game parameter is `out T`/`ref T` but the call passes plain `typeof(T)` (relies on FindMethod's `IsByRef` → `GetElementType` normalization, ReflectionUtil.cs:35–38). Visibility/statics verified against decompiled game source (`lib_sources/Assembly-CSharp/`).

**BuildDoorOverWall/Mod.cs (8 sites):**

| # | line | target | args passed | byref reliance | non-public | static |
|---|------|--------|-------------|---------------|------------|--------|
| 1 | 55 | `BuildingDef.IsValidPlaceLocation` | GameObject, Vector3, Orientation, string | **YES** — 4th is `out string` (BuildingDef.cs:1098) | no (public) | no |
| 2 | 71 | `BuildingDef.IsValidReplaceLocation` | Vector3, Orientation, ObjectLayer, ObjectLayer | no | no (public, BuildingDef.cs:1184) | no |
| 3 | 89 | `BuildTool.TryBuild` | int | no | **YES private** (BuildTool.cs:307) | no |
| 4 | 101 | `Assets.AddBuildingDef` | BuildingDef | no | no (public) | **YES** (Assets.cs:670) |
| 5 | 121 | `BuildingDef.IsValidPlaceLocation` (6-arg) | GameObject, int, Orientation, bool, string, bool | **YES** — 5th is `out string` (BuildingDef.cs:1120) | no (public) | no |
| 6 | 156 | `BuildingDef.IsValidBuildLocation` | GameObject, int, Orientation, bool, string | **YES** — 5th is `out string` (BuildingDef.cs:1221) | no (public) | no |
| 7 | 174 | `Constructable.FinishConstruction` | UtilityConnections, WorkerBase | no | **YES private** (Constructable.cs:223) | no |
| 8 | 203 | `BuildingDef.TryReplaceTile` | GameObject, Vector3, Orientation, IList<Tag>, int | no | no (public, BuildingDef.cs:487) | no |

**ReplaceBuildingMaterial/Mod.cs (10 sites):**

| # | line | target | args passed | byref reliance | non-public | static |
|---|------|--------|-------------|---------------|------------|--------|
| 9 | 39 | `Assets.AddBuildingDef` | BuildingDef | no | no (public) | **YES** (Assets.cs:670) |
| 10 | 62 | `BuildTool.TryBuild` | int | no | **YES private** (BuildTool.cs:307) | no |
| 11 | 76 | `BuildTool.OnDragTool` | int, int | no | **YES protected** override (BuildTool.cs:302) | no |
| 12 | 95 | `BuildingDef.IsValidReplaceLocation` | Vector3, Orientation, ObjectLayer, ObjectLayer | no | no (public) | no |
| 13 | 117 | `BuildingDef.IsValidPlaceLocation` (4-arg) | GameObject, Vector3, Orientation, string | **YES** — `out string` (BuildingDef.cs:1098) | no (public) | no |
| 14 | 141 | `BuildingDef.ArePowerPortsInValidPositions` | GameObject, int, Orientation, string | **YES** — `out string` (BuildingDef.cs:1391) | **YES private** | no |
| 15 | 158 | `BuildingDef.AreConduitPortsInValidPositions` | GameObject, int, Orientation, string | **YES** — `out string` (BuildingDef.cs:1423) | **YES private** | no |
| 16 | 176 | `BuildingDef.AreLogicPortsInValidPositions` | GameObject, int, string | **YES** — `out string` (BuildingDef.cs:1558) | **YES private** | no |
| 17 | 192 | `BuildingDef.MarkOverlappingPorts` | GameObject, GameObject | no | no (public, BuildingDef.cs:941) | no |
| 18 | 217 | `Constructable.FinishConstruction` | UtilityConnections, WorkerBase | no | **YES private** (Constructable.cs:223) | no |

Totals: 7 sites rely on byref normalization; 9 sites rely on non-public visibility (3 private instance in BDO + 6 in RBM incl. 1 protected); 2 sites are static; 0 sites are generic.

## FindMethod vs AccessTools.Method — divergence

| Aspect | FindMethod (UtilLibs/ReflectionUtil.cs) | AccessTools.Method (AccessTools.cs) |
|--------|------------------------------------------|--------------------------------------|
| DeclaredOnly / inherited | **DeclaredOnly** — only methods declared on the passed type (ReflectionUtil.cs:20) | **No DeclaredOnly**; `all` flags (29) + `FindIncludingBaseTypes` walks base types to `object` (133–146, 570, 583) — inherited public/protected members can match |
| Byref normalization | **Normalizes**: `pt.IsByRef → pt.GetElementType()` before equality (ReflectionUtil.cs:35–38) — caller passes plain `typeof(X)` for `out X`/`ref X` | **No normalization**: BCL `Type.GetMethod(name, all, null, parameters, modifiers)` exact `Type` equality (AccessTools.cs:583); plain `typeof(string)` ≠ `string&` (in-repo evidence BuildDoorOverWall/Mod.cs:39–42) |
| No-match behavior | Returns `null` (ReflectionUtil.cs:50); caller logs skip | `parameters != null` → `null` (585–589, same contract); **`parameters == null` + no parameterless match → throws `AmbiguousMatchException`** (575–577); null type/name → log + `null` (554–563) |
| Match order | `type.GetMethods(...)` metadata order, first full match (ReflectionUtil.cs:20–48) | Per type: BCL `GetMethod` metadata order; across types: derived → base via `FindIncludingBaseTypes` (135–145); first exact match |
| Static / non-public | Both (`Public|NonPublic|Instance|Static`, ReflectionUtil.cs:20) | Both (`all` = `Instance|Static|Public|NonPublic|...`, line 29) |
| Generics | None | Optional `generics` → `MakeGenericMethod` (590–593) |

**Which of the 18 call sites could behave differently if switched to AccessTools.Method:**

- **Byref-reliant — 7 sites WOULD BREAK (return null instead of the method; the mod logs the skip and its patch silently disappears):**
  - BDO :55 (`IsValidPlaceLocation` 4-arg, `out string`), :121 (`IsValidPlaceLocation` 6-arg, `out string`), :156 (`IsValidBuildLocation`, `out string`);
  - RBM :117 (`IsValidPlaceLocation` 4-arg, `out string`), :141 (:141 = `ArePowerPortsInValidPositions`, `out string`), :158 (`AreConduitPortsInValidPositions`, `out string`), :176 (`AreLogicPortsInValidPositions`, `out string`).
  - Why: `AccessTools.Method` would need the byref types themselves (`typeof(string).MakeByRefType()`), which the current call sites do not pass.
- **Non-public sites (BDO :89, :174; RBM :62, :76, :141, :158, :176) — NO difference:** `AccessTools.Method`'s `all` includes `NonPublic` (line 29), so private/protected methods resolve identically. (Same for SizeInTooltip :51, whose protected target is publicized anyway.)
- **Static sites (BDO :101, RBM :39) — NO difference:** both implementations include `Static`.
- **DeclaredOnly/inherited — NO difference for any of the 18 sites:** every target method is verified to be declared on the exact type passed (no inherited member among the targets). The difference would only bite if a target were an inherited public/protected member — then FindMethod misses it while AccessTools.Method finds it.
- **Match order — NO difference for any of the 18 sites:** each call's argument list uniquely identifies its overload (e.g. 4-arg vs 6-arg `IsValidPlaceLocation` differ in arity), so no ambiguity or ordering effect.
- **AmbiguousMatchException risk — none of the 18 sites affected:** all 18 pass a non-empty `parameters` array, so they take the null-return branch (585–589), never the throwing `parameters == null` branch.
- **SizeInTooltip :51 — NO difference:** no byref, no non-public (publicized), not static, not inherited, non-generic → identical result under both implementations.

## Game-dll reference mechanics

All repo-root build files: `Directory.Build.props`, `Directory.Build.props.default`,
`Directory.Build.props.user`, `Directory.Build.targets`. No per-project `*.props`/`*.targets`
exist under the four project folders (verified with `find`; only NuGet/tool files under
`.cache/` and `.tmp/`). The root props/targets therefore apply implicitly to **every**
project in the repo (including `UtilLibs`), since no csproj sets
`DisableImplicitProjectDirectoryImport`.

### The one Reference/HintPath ItemGroup — Directory.Build.props:92-125

Unconditional ItemGroup (no `Condition` attribute):

```xml
    <!-- Game libs refs-->
    <ItemGroup>
        <!-- The main game dlls. <Publicize> makes BepInEx.AssemblyPublicizer.MSBuild
             rewrite them (in-process, into obj/.../publicized/) so internal members
             become public for our Harmony patches. -->
        <Reference Include="Assembly-CSharp">
            <HintPath>$(GameLibsFolder)/Assembly-CSharp.dll</HintPath>
            <Publicize>true</Publicize>
            <PublicizeCompilerGenerated>false</PublicizeCompilerGenerated>
            <Private>False</Private>
        </Reference>
        <Reference Include="Assembly-CSharp-firstpass">
            <HintPath>$(GameLibsFolder)/Assembly-CSharp-firstpass.dll</HintPath>
            <Publicize>true</Publicize>
            <PublicizeCompilerGenerated>false</PublicizeCompilerGenerated>
            <Private>False</Private>
        </Reference>
        <Reference Include="0Harmony">
            <HintPath>$(GameLibsFolder)/0Harmony.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <Reference Include="UnityEngine">
            <HintPath>$(GameLibsFolder)/UnityEngine.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <Reference Include="UnityEngine.CoreModule">
            <HintPath>$(GameLibsFolder)/UnityEngine.CoreModule.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <!--        Optional-->

        <Reference Include="Newtonsoft.Json">
            <HintPath>$(GameLibsFolder)/Newtonsoft.Json.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <!--
                <Reference Include="netstandard"> ... </Reference>  (commented out:
                netstandard, FMODUnity, Unity.TextMeshPro, UnityEngine.AssetBundleModule,
                UnityEngine.PhysicsModule, UnityEngine.Physics2DModule,
                UnityEngine.ParticleSystemModule, UnityEngine.ImageConversionModule,
                UnityEngine.TextRenderingModule, UnityEngine.UI, UnityEngine.UIModule,
                UnityEngine.WebRequestModule, com.rlabrecque.steamworks.net)
        -->
    </ItemGroup>
```

`$(GameLibsFolder)` is user-configurable:
- `Directory.Build.props.user:6` → `<GameLibsFolder>$(HOME)/ONI/dlls</GameLibsFolder>` (active on this machine)
- `Directory.Build.props.default:6` → `X:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed` (used only when no `.user` file exists — `Directory.Build.props:8-9`)

### Conditions present in the root files (none gate the game refs)

| File:line | Condition | What it gates | Effect on UtilLibs (`IsMod=false`, `DoNotBuildAsMod=true`, `IsPacked=false`) |
| --- | --- | --- | --- |
| `Directory.Build.props:187-195` | `'$(IsMod)' == 'true'` | `PackageReference BepInEx.AssemblyPublicizer.MSBuild 0.4.3` (build;contentfiles assets) | UtilLibs does NOT get the publicizer package (its `Publicize` metadata is inert — no task to run it) |
| `Directory.Build.props:204-211` | `'$(OS)' != 'Windows_NT'` | `Microsoft.NETFramework.ReferenceAssemblies.net471` + `.net48` 1.0.3 | Applies (Linux) |
| `Directory.Build.props:218-223` | `'$(IsPacked)' == 'true'` | `PackageReference dotnet-ilrepack 2.0.45` | UtilLibs does NOT get it |
| `Directory.Build.targets:22` | `'$(DoNotBuildAsMod)' != 'true'` | `GenerateModYaml` target | Skipped for UtilLibs |
| `Directory.Build.targets:40` | `'$(DoNotBuildAsMod)' != 'true'` | `GenerateModInfoYaml` target | Skipped for UtilLibs |
| `Directory.Build.targets:66` | `'$(IsPacked)' == 'true'` | `ILRepack` target (merges bin dlls, excludes `**/*Harmony.dll; **/Splat.dll; **/Assembly-*; ...`) | Skipped for UtilLibs |
| `Directory.Build.targets:89` | `'$(DoNotBuildAsMod)' != 'true'` | `CopyModsToDevFolder` target | Skipped for UtilLibs |
| `Directory.Build.targets:153-158` | `'$(GameLibsFolder)' != '../Lib' and '$(RefasmerInstalled)' != '0' and '$(IsMod)' == 'true'` | `GenerateRefAssemblies` target (Refasmer copy into `../Lib`) | Skipped for UtilLibs (`IsMod=false`) |

The `Directory.Build.targets:126-152` `GameRefAssemblies`/`RefAssemblies` ItemGroup defines only
custom MSBuild items (not `Reference` items) feeding the Refasmer target — it adds no
compiler references.

### Per-project references (csprojs)

None of the four csprojs declares any game-dll `Reference` themselves; all game refs come from
the shared root props. Project-level `ItemGroup`s:

| Project | Its own references |
| --- | --- |
| `UtilLibs/UtilLibs.csproj:26-28` | `PackageReference PLib 4.19.0` (only); properties: `net48`, `IsMod=false` (line 19), `DoNotBuildAsMod=true` (line 20), `IsPacked=false` (line 23) |
| `BuildDoorOverWall/BuildDoorOverWall.csproj:37-39` | `ProjectReference ..\UtilLibs\UtilLibs.csproj` (only); `IsMod=true`, `IsPacked=true` |
| `ReplaceBuildingMaterial/ReplaceBuildingMaterial.csproj:37-39` | `ProjectReference ..\UtilLibs\UtilLibs.csproj` (only); `IsMod=true`, `IsPacked=true` |
| `SizeInTooltip/SizeInTooltip.csproj:37-39` | `ProjectReference ..\UtilLibs\UtilLibs.csproj` (only); `IsMod=true`, `IsPacked=true` |

So every project gets: Assembly-CSharp, Assembly-CSharp-firstpass, 0Harmony (game v2),
UnityEngine, UnityEngine.CoreModule, Newtonsoft.Json (all `Private=False`), plus whatever the
PLib package transitively brings for UtilLibs.

### Conclusion — UtilLibs game-dll references

**Yes.** `UtilLibs` currently references the game's `0Harmony.dll` and `Assembly-CSharp.dll`
indirectly, via the shared, **unconditional** root props ItemGroup. The deciding lines:

- `Directory.Build.props:92` — `<ItemGroup>` (no Condition → applies to every imported project)
- `Directory.Build.props:96-101` — `<Reference Include="Assembly-CSharp">` with `<HintPath>$(GameLibsFolder)/Assembly-CSharp.dll</HintPath>`
- `Directory.Build.props:108-111` — `<Reference Include="0Harmony">` with `<HintPath>$(GameLibsFolder)/0Harmony.dll</HintPath>`

No condition anywhere (`IsMod`, `DoNotBuildAsMod`, `IsPacked`) excludes any project from that
ItemGroup — the `IsMod` condition at `Directory.Build.props:187` only controls the
publicizer PackageReference, and `DoNotBuildAsMod`/`IsPacked` appear only in the targets.
UtilLibs' own csproj carries zero game references and zero `DisableImplicitProjectDirectoryImport`,
so the root props import is implicit and effective. Note: `HarmonyLib` types are therefore
compilable inside UtilLibs today (it simply never uses them — no `Harmony` matches in its source),
but the reference is present in its compile input.

## 18 call-site patch shapes & null logs

All 18 sites follow the same shape: `MethodInfo <var> = ReflectionUtil.FindMethod(...)` →
`if (<var> == null) { [optional #if DEBUG PUtil.LogDebug] PUtil.LogError("could not resolve <sig> — <feature> <part> skipped (game build mismatch?)"); } else { harmony.Patch(<var>, ...) }`.
Every null path logs unconditionally with `PUtil.LogError`; there is no prefix/postfix/suffix/
transpiler/finalizer anywhere beyond what is listed below (no transpilers, no suffixes, no
finalizers in either OnLoad). 8 sites in `BuildDoorOverWall/Mod.cs` (lines 55, 71, 89, 101, 121,
156, 174, 203), 10 in `ReplaceBuildingMaterial/Mod.cs` (lines 39, 62, 76, 95, 117, 141, 158,
176, 192, 217).

| # | mod | local var | resolved method | patch parts used | exact null/skip log (unconditional) | #if DEBUG log |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | BuildDoorOverWall | `validPlace` (Mod.cs:55) | `BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)` (4-arg) | postfix: `BuildingDef_IsValidPlaceLocation_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) — hover-text/visualizer postfix skipped (game build mismatch?)");` | `PUtil.LogDebug("BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) не найдена — hover-text/visualizer postfix пропущен");` |
| 2 | BuildDoorOverWall | `validReplace` (Mod.cs:71) | `BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` | postfix: `BuildingDef_IsValidReplaceLocation_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) — preview-tint postfix skipped (game build mismatch?)");` | `PUtil.LogDebug("BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) не найдена — preview-tint postfix пропущен");` |
| 3 | BuildDoorOverWall | `tryBuild` (Mod.cs:89) | `BuildTool.TryBuild(int)` | postfix: `BuildTool_TryBuild_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve BuildTool.TryBuild(int) — upper-cell replacement fallback postfix skipped (game build mismatch?)");` | none |
| 4 | BuildDoorOverWall | `addBuildingDef` (Mod.cs:101) | `Assets.AddBuildingDef(BuildingDef)` | postfix: `Assets_AddBuildingDef_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve Assets.AddBuildingDef(BuildingDef) — all-door replacement metadata postfix skipped (game build mismatch?)");` | none |
| 5 | BuildDoorOverWall | `validPlace6` (Mod.cs:121) | `BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool)` (6-arg) | postfix: `BuildingDef_IsValidPlaceLocation6_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool) — 6-arg HasDoor-bypass postfix skipped (game build mismatch?)");` | `PUtil.LogDebug("BuildingDef.IsValidPlaceLocation(GameObject, int, Orientation, bool, out string, bool) не найдена — HasDoor-bypass postfix пропущен");` |
| 6 | BuildDoorOverWall | `validBuild` (Mod.cs:156) | `BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string)` (core int overload) | postfix: `BuildingDef_IsValidBuildLocation_DoorReplacement__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string) — construction-recheck postfix skipped (game build mismatch?)");` | `PUtil.LogDebug("BuildingDef.IsValidBuildLocation(GameObject, int, Orientation, bool, out string) не найдена — construction-recheck postfix пропущен");` |
| 7 | BuildDoorOverWall | `finishConstruction` (Mod.cs:174) | `Constructable.FinishConstruction(UtilityConnections, WorkerBase)` | prefix: `Constructable_FinishConstruction_DoorReplacement__Patch.Prefix` | `PUtil.LogError("could not resolve Constructable.FinishConstruction(UtilityConnections, WorkerBase) — duplicate-candidate prefix skipped (game build mismatch?)");` | `PUtil.LogDebug("Constructable.FinishConstruction(UtilityConnections, WorkerBase) не найдена — duplicate-candidate prefix пропущен");` |
| 8 | BuildDoorOverWall | `tryReplaceTile` (Mod.cs:203) | `BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int)` (working 5-arg overload) | prefix: `BuildingDef_TryReplaceTile_SameDoorPrefab__Patch.Prefix` | `PUtil.LogError("could not resolve BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int) — same-PrefabID door-over-door prefix skipped (game build mismatch?)");` | `PUtil.LogDebug("BuildingDef.TryReplaceTile(GameObject, Vector3, Orientation, IList<Tag>, int) не найдена — same-PrefabID door-over-door prefix пропущен");` |
| 9 | ReplaceBuildingMaterial | `addBuildingDef` (Mod.cs:39) | `Assets.AddBuildingDef(BuildingDef)` | postfix: `Assets_AddBuildingDef_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve Assets.AddBuildingDef(BuildingDef) — replacement metadata postfix skipped (game build mismatch?)");` | none |
| 10 | ReplaceBuildingMaterial | `tryBuild` (Mod.cs:62) | `BuildTool.TryBuild(int)` | prefix + postfix: `BuildTool_TryBuild_ReplaceBuildingMaterial__Patch.Prefix` + `.Postfix` | `PUtil.LogError("could not resolve BuildTool.TryBuild(int) — shifted/rotated drag-rejection prefix skipped (game build mismatch?)");` | none |
| 11 | ReplaceBuildingMaterial | `onDragTool` (Mod.cs:76) | `BuildTool.OnDragTool(int, int)` (protected override) | postfix: `BuildTool_OnDragTool_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildTool.OnDragTool(int, int) — drag-entry log skipped (game build mismatch?)");` | none |
| 12 | ReplaceBuildingMaterial | `isValidReplaceLocation` (Mod.cs:95) | `BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer)` | postfix: `BuildingDef_IsValidReplaceLocation_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidReplaceLocation(Vector3, Orientation, ObjectLayer, ObjectLayer) — preview-tint postfix skipped (game build mismatch?)");` | none |
| 13 | ReplaceBuildingMaterial | `isValidPlaceLocation4` (Mod.cs:117) | `BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string)` (4-arg) | postfix: `BuildingDef_IsValidPlaceLocation_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.IsValidPlaceLocation(GameObject, Vector3, Orientation, out string) — hover-text postfix skipped (game build mismatch?)");` | none |
| 14 | ReplaceBuildingMaterial | `arePowerPorts` (Mod.cs:141) | `BuildingDef.ArePowerPortsInValidPositions(GameObject, int, Orientation, out string)` (private) | postfix: `BuildingDef_ArePowerPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.ArePowerPortsInValidPositions(GameObject, int, Orientation, out string) — power-port overlap fix skipped (game build mismatch?)");` | none |
| 15 | ReplaceBuildingMaterial | `areConduitPorts` (Mod.cs:158) | `BuildingDef.AreConduitPortsInValidPositions(GameObject, int, Orientation, out string)` (private) | postfix: `BuildingDef_AreConduitPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.AreConduitPortsInValidPositions(GameObject, int, Orientation, out string) — conduit-port overlap fix skipped (game build mismatch?)");` | none |
| 16 | ReplaceBuildingMaterial | `areLogicPorts` (Mod.cs:176) | `BuildingDef.AreLogicPortsInValidPositions(GameObject, int, out string)` (private, 3 params — no Orientation) | postfix: `BuildingDef_AreLogicPortsInValidPositions_ReplaceBuildingMaterial__Patch.Postfix` | `PUtil.LogError("could not resolve BuildingDef.AreLogicPortsInValidPositions(GameObject, int, out string) — logic-port overlap fix skipped (game build mismatch?)");` | none |
| 17 | ReplaceBuildingMaterial | `markOverlappingPorts` (Mod.cs:192) | `BuildingDef.MarkOverlappingPorts(GameObject, GameObject)` | prefix: `BuildingDef_MarkOverlappingPorts_ReplaceBuildingMaterial__Patch.Prefix` | `PUtil.LogError("could not resolve BuildingDef.MarkOverlappingPorts(GameObject, GameObject) — stale-port-tag suppression skipped (game build mismatch?)");` | none |
| 18 | ReplaceBuildingMaterial | `finishConstruction` (Mod.cs:217) | `Constructable.FinishConstruction(UtilityConnections, WorkerBase)` (private) | prefix: `Constructable_FinishConstruction_ReplaceBuildingMaterial__Patch.Prefix` | `PUtil.LogError("could not resolve Constructable.FinishConstruction(UtilityConnections, WorkerBase) — completion fix-up prefix skipped (game build mismatch?)");` | none |

**Message uniformity:** all 18 unconditional skip logs follow one English template —
`"could not resolve <MethodSignature> — <feature phrase> <prefix|postfix> skipped (game build
mismatch?)"` — with only the signature and feature phrase varying. The 6 `#if DEBUG`
counterparts (only in BuildDoorOverWall) mirror the same structure in Russian:
`"<MethodSignature> не найдена — <feature phrase> <postfix|prefix> пропущен"`. The 2
ReplaceBuildingMaterial-duplicated targets in BuildDoorOverWall (`BuildTool.TryBuild(int)`,
`Constructable.FinishConstruction`) and the 4 shared signatures across the two mods
(`Assets.AddBuildingDef`, `BuildTool.TryBuild`, `BuildingDef.IsValidReplaceLocation`,
`BuildingDef.IsValidPlaceLocation(4-arg)`, `Constructable.FinishConstruction`) carry
mod-specific feature phrases, so the messages are uniform in shape but varied in content.

Distinct example quotes (uniform shape):
- `PUtil.LogError("could not resolve Assets.AddBuildingDef(BuildingDef) — all-door replacement metadata postfix skipped (game build mismatch?)");`
- `PUtil.LogError("could not resolve BuildTool.OnDragTool(int, int) — drag-entry log skipped (game build mismatch?)");`
- `PUtil.LogError("could not resolve BuildingDef.MarkOverlappingPorts(GameObject, GameObject) — stale-port-tag suppression skipped (game build mismatch?)");`

## State update (verified this session) — FindMethod extraction completed

The repo changed between the first half of this document and now: the two
byte-identical `private static FindMethod` copies described in "FindMethod —
definitions" / "differences between copies" / "FindMethod containing classes"
(those sections were written against the pre-extraction tree) no longer exist.
Both mods now call the shared static method `UtilLibs.ReflectionUtil.FindMethod`
(`UtilLibs/ReflectionUtil.cs`, same DeclaredOnly scan, same byref normalization,
same null-return contract):

- `BuildDoorOverWall/Mod.cs` — `using UtilLibs;` (line 7); calls at :55, :71, :89,
  :101, :121, :156, :174, :203 (8 sites).
- `ReplaceBuildingMaterial/Mod.cs` — `using UtilLibs;` (line 9); calls at :39, :62,
  :76, :95, :117, :141, :158, :176, :192, :217 (10 sites).

Verified by grep: no `FindMethod` definition remains in either `Mod.cs` (only the
18 calls and comments). Everything else in this document — call-site signatures,
patch shapes, null-log texts, and the `Game-dll reference mechanics` section — was
re-checked against the current sources this session and is accurate.
