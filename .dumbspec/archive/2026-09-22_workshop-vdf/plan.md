# 2026-09-22_workshop-vdf: implementation plan (workshop_build.vdf on Release builds)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Capture raw input into `draft.md` (verbatim)
- [x] Research: project scan (sln, csproj properties, existing targets in `Directory.Build.targets`, README/preview presence, docs, git history)
- [x] Research: web facts on `workshop_build.vdf` (visibility enum, VDF escaping, how ONI consumes it, local-artifact nature)
- [x] Spec v1 written; open questions resolved with the user (visibility=2 private; `WorkshopItemId` default 0 for first publish; `contentfolder`=`$(TargetDir)`; description = full README; `UTF-8NoBOM`)
- [x] Plan written

**Criterion:** `draft.md`, `research.md`, `spec.md` (v1), `plan.md` exist in `.dumbspec/current/2026-09-22_workshop-vdf/` and are committed on branch `2026-09-22_workshop-vdf`.
**Commit:** `docs(spec): research, spec v1 and plan for workshop_build.vdf release target`

## Stage 1 — `GenerateSteamVdf` MSBuild target
- [x] Red: baseline check — Release build today produces **no** `workshop_build.vdf` in any mod's `bin/` (run `dotnet build ONI-mods.sln -c Release`, `ls` the mod `bin/` dirs) — confirmed 2026-09-22 (build OK, zero .vdf files repo-wide)
- [x] Implement shared target `GenerateSteamVdf` in root `Directory.Build.targets` (done 2026-09-22; deviations proven by MSBuild run: no `Encoding` attr — MSBuild 17 rejects `UTF8NoBOM`, default is no-BOM; README via `ReadAllText` property function — `@(items)` expansion in property context `;`-joins + percent-escapes):
  - `AfterTargets="Build"`, condition `'$(Configuration)' == 'Release' and '$(DoNotBuildAsMod)' != 'true'`, README existence guard
  - properties: `ReadmePath=$(ProjectDir)README.md`, `VdfOutputPath=$(TargetDir)workshop_build.vdf`, `PreviewName` default `preview.png`, `WorkshopVisibility` default `2`
  - `ReadLinesFromFile` → join lines with raw newlines → escape `"` → `\"` (same pattern as `GenerateModYaml` line 29)
  - VDF body: `appid 457140`, `publishedfileid $(WorkshopItemId)`, `contentfolder $(TargetDir)`, `previewfile $(ProjectDir)$(PreviewName)`, `visibility $(WorkshopVisibility)`, `title $(ModName)`, `description <README>`, `changenote "Version: $(Version)"`
  - `WriteLinesToFile` `Overwrite=true`, `Encoding=UTF-8NoBOM`; high-importance `Message` with the output path
- [x] Add `<WorkshopItemId>0</WorkshopItemId>` to the 4 mod csproj files (BuildDoorOverWall, SizeInTooltip, ReplaceBuildingMaterial, BestBuildDryWall) — done 2026-09-22, all 4 well-formed
- [x] Green: `dotnet build ONI-mods.sln -c Release` passes; every mod's flat `bin/` now contains `workshop_build.vdf` — confirmed 2026-09-22 (build OK, 4 vdfs, 4 high-importance messages, no new warnings)
- [x] Verify content of one generated vdf: structure, appid, contentfolder/previewfile absolute paths, title/changenote, README text present with escaped quotes, no BOM (hexdump first bytes) — BuildDoorOverWall vdf: all 8 checks PASS (no BOM `22 77 6f`, exact metadata, description == README.md, 0 percent-escapes, 0 unescaped quotes; note: README has no `"` chars, so escape branch unexercised end-to-end)
- [x] Negative checks: Debug build produces no vdf; `UtilLibs` Release produces no vdf — confirmed 2026-09-22 (Debug build OK, zero vdf in Debug folders, zero under UtilLibs, debug artifacts intact)
- [x] Refactor: align property style with existing targets; keep build invariants (net48, ILRepack, Publicizer untouched) — done 2026-09-22 (1 style fix: Message attr order; invariants verified by diff — pure insertions only)

**Criterion:** Release build of all 4 mods emits a well-formed `workshop_build.vdf` in `<Mod>/bin/` (no BOM, fields per spec); Debug/UtilLibs do not; build invariants unregressed.
**Commit:** `feat(build): generate workshop_build.vdf for mods on Release builds`

## Stage 2 — Independent acceptance
- [x] Fresh Release build (clean first) of the whole sln; confirm vdf present in all 4 mods' `bin/` with correct content (independent check, not the implementing agent's self-report) — ACCEPTANCE PASSED 2026-09-22 (clean build, 4 no-BOM vdfs, all fields verified, description byte-equal to README per mod, zero Debug/UtilLibs vdf, baseline warnings only)
- [x] Confirm no stray changes to other artifacts (dll, yamls, pdb) and `CopyModsToDevFolder` behaviour unchanged (sandbox MSB3027 expected if it fired — it did not; copy went to workspace `.tmp/build_mod_dir/`) — confirmed 2026-09-22 (release folders contain exactly dll/pdb/yamls, no vdf there, vdf only in the 4 bin/ dirs, standard Release yaml values)
- [x] Report results to the user for manual acceptance (local publish test via steamcmd/Uploader is the user's step)

**Criterion:** Independent agent confirms the Stage 1 Criterion from a clean build; user is given the report.
**Commit:** `docs(spec): acceptance passed; plan complete`

## Stage 3: generate README.txt (Steam BBCode) and source the vdf description from it

- [x] Add `PackageReference Converter.MarkdownToBBCodeNM.Tool 1.0.0.29` (PrivateAssets/ExcludeAssets=all, `Condition="'$(IsPacked)' == 'true'"`) to `Directory.Build.props` (the ilrepack pattern) — done 2026-09-22 (props lines 225–234, mirrors the ILRepack block)
- [x] Add target `GenerateReadmeBbcode` to `Directory.Build.targets` (Run the net8.0 dll via `dotnet`, `-i README.md -o $(TargetDir)README.txt`, same conditions as the vdf); `GenerateSteamVdf` → `AfterTargets` chain from it, `description` is read from `README.txt` — done 2026-09-22 (target at lines 60–75; vdf `AfterTargets="GenerateReadmeBbcode"`, `ReadmePath=$(TargetDir)README.txt`)
- [x] Clean Release build: all 4 mods' `bin/` have `README.txt` (BBCode: `[b]`, `[list]`, `[size=…]`), and the vdf `description` is byte-for-byte identical to `README.txt` (after quote unescaping + trim); Debug and `UtilLibs` are not affected; baseline warnings unchanged — verified 2026-09-22 (`.tmp/bbcode_build.log`)
- [x] Independent acceptance: fresh clean Release build, verify the above from scratch (not the implementing agent's self-report) — STAGE 3 ACCEPTANCE PASSED 2026-09-22 (clean build; manual re-run of the embedded tool byte-identical to bin/README.txt; vdf fields exact, no BOM, raw newlines, description==README.txt after trim; Debug/UtilLibs clean; warning set byte-identical to baseline; CopyModsToDevFolder unchanged)
- [x] Acceptance test report to the user (publishing via steamcmd/Uploader is still a manual step)

**Criterion:** All 4 mods' `bin/` have a `README.txt` with valid BBCode, and the vdf description is BBCode (not Markdown); no other artifact changes.
**Commit:** `feat(build): generate BBCode README.txt for mods and use it in the workshop vdf`

## Stage 4: point vdf `contentfolder` at `$(TargetFolder)` (store mod folder)

- [ ] `GenerateSteamVdf`: `AfterTargets="CopyModsToDevFolder"` + `DependsOnTargets="GenerateReadmeBbcode"`; `contentfolder` = normalized `$(TargetFolder)` (backslashes → slashes, no trailing separator); vdf itself still written to `$(TargetDir)`
- [ ] Clean Release build: for all 4 mods the vdf `contentfolder` == absolute `.tmp/build_mod_dir/<Mod>_release` path (no backslashes, no trailing slash), that folder exists at vdf-write time and contains exactly dll/pdb/mod.yaml/mod_info.yaml; vdf still in `bin/`; all other fields unchanged; baseline warnings unchanged
- [ ] Independent acceptance: fresh clean Release build, verify the above from scratch
- [ ] Report results to the user

**Criterion:** vdf `contentfolder` points at the populated store mod folder; no other changes.
**Commit:** `fix(build): workshop vdf contentfolder points at the store mod folder`
