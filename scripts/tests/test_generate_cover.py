"""TDD red-step tests for scripts/generate_cover.py.

Standalone runner: python3 scripts/tests/test_generate_cover.py
No pytest dependency; plain assert-based test functions. Exits nonzero on failure.
"""

import sys
import tempfile
import traceback
from pathlib import Path

from PIL import Image

# Make scripts/ importable: repo_root/scripts/
REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPTS_DIR = REPO_ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS_DIR))

import generate_cover  # noqa: E402  (fails with ModuleNotFoundError until implemented)

SOURCE_IMAGE = Path(__file__).resolve().parent / "preview-raw.jpg"


def _generate(input_path, text, output_path, size=900):
    generate_cover.generate(input_path, text, output_path, size=size)


def test_output_is_square_png():
    assert SOURCE_IMAGE.is_file(), f"missing fixture: {SOURCE_IMAGE}"
    with tempfile.TemporaryDirectory(prefix="cover_test_") as tmp:
        tmpdir = Path(tmp)
        out = tmpdir / "cover.png"
        _generate(SOURCE_IMAGE, "Size In Tooltip", out)
        assert out.is_file(), "output file was not created"
        with Image.open(out) as img:
            assert img.format == "PNG", f"expected PNG, got {img.format}"
            assert img.size == (900, 900), f"expected (900, 900), got {img.size}"
            assert img.mode == "RGB", f"expected RGB mode, got {img.mode}"


def test_explicit_newline_split():
    # Literal two-character backslash-n is a line break.
    assert generate_cover.split_lines("Size \\nIn Tooltip") == ["Size", "In Tooltip"]
    # No backslash-n -> fallback: one word per line.
    assert generate_cover.split_lines("Size In Tooltip") == ["Size", "In", "Tooltip"]


def test_autoscale_long_word():
    long_word = "A" * 40
    with tempfile.TemporaryDirectory(prefix="cover_test_") as tmp:
        tmpdir = Path(tmp)
        out = tmpdir / "cover_long.png"
        _generate(SOURCE_IMAGE, long_word, out)
        assert out.is_file(), "output file was not created"
        with Image.open(out) as img:
            assert img.format == "PNG", f"expected PNG, got {img.format}"
            assert img.size == (900, 900), f"expected (900, 900), got {img.size}"


def test_text_rendered_with_stroke():
    with tempfile.TemporaryDirectory(prefix="cover_test_") as tmp:
        tmpdir = Path(tmp)
        out = tmpdir / "cover_stroke.png"
        _generate(SOURCE_IMAGE, "AB", out)
        assert out.is_file(), "output file was not created"
        with Image.open(out) as img:
            assert img.size == (900, 900)
            assert img.mode == "RGB"
            px = img.load()
            W, H = img.size

            # White-ish fill pixels in the top half (where text lines live).
            white = []
            for y in range(H // 2):
                for x in range(W):
                    r, g, b = px[x, y][:3]
                    if r > 240 and g > 240 and b > 240:
                        white.append((x, y))
            assert white, "no white-ish fill pixels found in top half"

            # Grow the white-pixel bounding box by 20px and look for near-black
            # pixels inside it: the black stroke around the white fill.
            xs = [p[0] for p in white]
            ys = [p[1] for p in white]
            x0, x1 = max(0, min(xs) - 20), min(W - 1, max(xs) + 20)
            y0, y1 = max(0, min(ys) - 20), min(H - 1, max(ys) + 20)

            near_black = 0
            for y in range(y0, y1 + 1):
                for x in range(x0, x1 + 1):
                    r, g, b = px[x, y][:3]
                    if r < 40 and g < 40 and b < 40:
                        near_black += 1
            assert near_black > 0, (
                f"no near-black stroke pixels found around white text "
                f"(bbox {x0},{y0}..{x1},{y1})"
            )


def test_size_argument():
    with tempfile.TemporaryDirectory(prefix="cover_test_") as tmp:
        tmpdir = Path(tmp)
        out = tmpdir / "cover_500.png"
        _generate(SOURCE_IMAGE, "Size In Tooltip", out, size=500)
        assert out.is_file(), "output file was not created"
        with Image.open(out) as img:
            assert img.format == "PNG", f"expected PNG, got {img.format}"
            assert img.size == (500, 500), f"expected (500, 500), got {img.size}"


def main():
    tests = [
        test_output_is_square_png,
        test_explicit_newline_split,
        test_autoscale_long_word,
        test_text_rendered_with_stroke,
        test_size_argument,
    ]
    failed = 0
    for test in tests:
        name = test.__name__
        try:
            test()
            print(f"PASS {name}")
        except Exception:
            failed += 1
            print(f"FAIL {name}")
            traceback.print_exc()
    total = len(tests)
    print(f"\n{total - failed}/{total} passed, {failed} failed")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
