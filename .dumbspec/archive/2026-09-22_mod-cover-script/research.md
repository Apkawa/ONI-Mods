# Research: 2026-09-22_mod-cover-script

Collected 2026-09-22. Single-item task, gathered via one synchronous subagent.

## Project structure

- `scripts/` directory does NOT exist yet in the repo root. Top-level: `BestBuildDryWall/`, `BuildDoorOverWall/`, `PublicisedAssembly/`, `ReplaceBuildingMaterial/`, `SizeInTooltip/`, `UtilLibs/`, `docs/`.
- No tracked Python files anywhere in the repo (`git ls-files '*.py'` empty). The only `.py` files are untracked scratch in `.tmp/` — no CLI convention to follow; we define our own.
- No `tests/` / `fixtures/` dirs; no test-suite convention (AGENTS.md: only `dotnet build` is automated verification).
- `.gitignore` ignores `.tmp/` and `.cache/`; `scripts/` would be tracked by default.

## Toolchain (fixed facts)

- Python 3.12.3 (`python3`).
- Pillow 10.2.0, package path `/usr/lib/python3/dist-packages/PIL`.

## Font (fixed facts)

- `~/.fonts/GRAYSTROKE REGULAR.otf` — exists, 10,304 bytes.
- `~/.fonts/` contains only two fonts: `GRAYSTROKE REGULAR.otf`, `GRAYSTROKE ITALIC.otf`. **No Bold variant** — "bold" must be simulated (stroke acts as thickening).

## Pillow 10.2.0 API (verified from installed source)

- `ImageFont.truetype(font=None, size=10, index=0, encoding="", layout_engine=None)` — `size` in pixels.
- `ImageDraw.text(xy, text, fill=None, font=None, anchor=None, spacing=4, align="left", direction=None, features=None, language=None, stroke_width=0, stroke_fill=None, embedded_color=False, ...)`
  - `stroke_width` default 0; `stroke_fill` default None (falls back to text fill ink).
  - `stroke_width` also available on `multiline_text`.

## Test input

- `.tmp/preview-raw.jpg`: JPEG, RGB, **685x566** (non-square — center crop to square required), 96 DPI, 77,788 bytes.

## Existing image-generation code

- None in tracked files. Only incidental binary match: `docs/assets/Compatibility.xcf` (GIMP asset, not code).

## Open questions resolved by research

- No Bold font → boldness via stroke/thickening (open question 8 partially answered).
- CLI style: no existing convention; argparse is the natural choice (new directory, new script).
