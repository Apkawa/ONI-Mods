# 2026-09-22_mod-cover-script: implementation plan (mod cover generator)

Spec — [spec.md](./spec.md); research — [research.md](./research.md).

**Format:** each stage = TDD (red test → green implementation → refactor) + a commit at the end.
This file is a **living progress journal** — update statuses as work proceeds.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done.

---

## Stage 0 — Research + spec + plan
- [x] Capture draft.md (raw input + user's test-screenshot note)
- [x] Pre-checks: moved untracked `.tmp/` screenshot, created branch `2026-09-22_mod-cover-script`
- [x] Refine draft/spec with user answers (style, colors, crop, autoscale, PNG)
- [x] Research (single synchronous subagent): repo structure, toolchain, font, Pillow API, test image
- [x] Spec v1 (Created timestamp + Changes)
- [x] Review spec: user added explicit `\n` line-break support
- [x] Write plan

**Criterion:** draft.md, research.md, spec.md (v1), plan.md all exist in `.dumbspec/current/2026-09-22_mod-cover-script/`; spec reviewed by user.
**Commit:** `docs(dumbspec): spec and plan for mod cover generator script`

## Stage 1 — Cover generator script (TDD)
- [x] Red: write `scripts/tests/test_generate_cover.py` (pytest or plain assert) covering: output is 900x900 PNG; center-crop of non-square input; explicit `\n` split vs auto word-split fallback; long-word autoscale (word fits <=85% width); white text with black stroke present (pixel check). Test uses `.tmp/preview-raw.jpg` as fixture input (copy into test's tmp area so test is self-contained).
- [x] Green: implement `scripts/generate_cover.py` — argparse (image, text, output, `--size` default 900 max), center-crop + Lanczos resize, `ImageFont.truetype("~/.fonts/GRAYSTROKE REGULAR.otf")`, rows from literal `\n` or whitespace split, even vertical distribution, `anchor="mm"`, white fill + black `stroke_fill` with `stroke_width` ~4% of font size, autoscale font down until max word width <= 85% of canvas.
- [x] Move font next to the script (`scripts/GRAYSTROKE REGULAR.otf` from `~/.fonts/`) and test image next to the test (`scripts/tests/` fixture); script and tests must not depend on `~/.fonts/` or `.tmp/`; rerun tests + acceptance image
- [x] Refactor: layout constants (margins, stroke ratio, width cap) as named module constants with brief comments; `main()` split into small functions; `python3 -m py_compile` clean.
- [x] Acceptance run: generate a real cover from `scripts/tests/preview-raw.jpg` with text `Size \nIn Tooltip` into `.tmp/cover-preview.png`; user reviewed the image, asked for a bigger top margin (TOP_MARGIN_RATIO 0.15 vs 0.10 bottom), and approved.

**Criterion:** `python3 scripts/tests/test_generate_cover.py` passes; `.tmp/cover-preview.png` is 900x900 PNG with the title laid out top-to-bottom, white with black outline, and the user visually approves it.
**Commit:** `feat(scripts): add OXI mod cover generator (Pillow, GRAYSTROKE)`

## Stage 2 — Archive
- [x] Move task dir `.dumbspec/current/2026-09-22_mod-cover-script/` → `.dumbspec/archive/2026-09-22_mod-cover-script/`
- [x] (optional, ask user) switch back to `master`, merge branch — done via git-merge (squash into master)

**Criterion:** task archived under `.dumbspec/archive/`; no open questions in spec.
**Commit:** `chore(dumbspec): archive mod-cover-script task`
