#!/usr/bin/env python3
"""Generate a square OXI mod cover PNG from a screenshot with centered stroked text.

Usage (from the repo root):
    python3 scripts/generate_cover.py input.jpg "Size \\nIn Tooltip" out.png --size 900

Layout rules:
  * The title text is split on the literal two-character sequence backslash-n
    (written "\\\\n" in the shell); if it contains none, the fallback is one
    word per line.
  * Lines are laid out horizontally centered, with their centers evenly
    distributed vertically from top to bottom between asymmetric margins:
    20% from the top edge and 10% from the bottom edge.
  * Each line is rendered in white with a black outline: Pillow's
    stroke_width emulates boldness, since the GRAYSTROKE regular font has no
    Bold variant.
  * The font starts large and is autoscaled down (x0.95 per step, floor 8 px)
    until the widest line fits within 85% of the cover width and every line
    fits vertically.
  * The input image is center-cropped to a square and resized with Lanczos to
    exactly `size x size` (capped at 900 px), then saved as an RGB PNG.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path
from typing import List, Optional, Sequence, Tuple

from PIL import Image, ImageDraw, ImageFont

FONT_PATH: str = str(Path(__file__).resolve().parent / "GRAYSTROKE REGULAR.otf")

# Line break marker: the literal two-character sequence backslash + n
# (passed from the shell as "Text \\nMore").
LINE_BREAK: str = "\\n"

# --- Style / layout constants -------------------------------------------------
MAX_SIZE: int = 900                # output is at most 900x900
TOP_MARGIN_RATIO: float = 0.15       # first line center sits 15% in from the top edge
BOTTOM_MARGIN_RATIO: float = 0.10    # last line center sits 10% in from the bottom edge
MAX_WIDTH_RATIO: float = 0.85      # widest rendered line must fit within 85% of `size`
STROKE_RATIO: float = 0.04         # stroke width ~4% of the font size
MIN_STROKE_PX: int = 2             # stroke never thinner than 2 px
MIN_FONT_SIZE: int = 8             # floor that guarantees the autoscale loop terminates
SHRINK_FACTOR: float = 0.95        # font shrink factor per autoscale step
TEXT_FILL: Tuple[int, int, int] = (255, 255, 255)  # text color: white
STROKE_FILL: Tuple[int, int, int] = (0, 0, 0)      # outline color: black
TEXT_ANCHOR: str = "mm"            # "middle-middle": anchor at the line's center point
OUTPUT_FORMAT: str = "PNG"         # output image format


def split_lines(text: str) -> List[str]:
    """Split the cover text into lines.

    A literal two-character backslash-n ("\\n") is an explicit line break.
    If the text contains none, fall back to one word per line.
    Lines are stripped; empty lines are dropped.
    """
    if LINE_BREAK in text:
        parts = text.split(LINE_BREAK)
    else:
        parts = text.split()
    return [part.strip() for part in parts if part.strip()]


def _clamp_size(size: int) -> int:
    """Clamp `size` to a positive integer no greater than MAX_SIZE."""
    size = int(size)
    if size > MAX_SIZE:
        size = MAX_SIZE
    if size <= 0:
        raise ValueError(f"size must be positive, got {size}")
    return size


def _prepare_background(input_path: str, size: int) -> Image.Image:
    """Open `input_path`, center-crop it to a square, resize to size x size."""
    image = Image.open(input_path).convert("RGB")
    width, height = image.size
    side = min(width, height)
    left = (width - side) // 2   # horizontally center the square crop
    top = (height - side) // 2   # vertically center the square crop
    image = image.crop((left, top, left + side, top + side))
    return image.resize((size, size), Image.LANCZOS)


def _vertical_limit(size: int, n_lines: int) -> float:
    """Max ink extent above/below a line center, in px.

    One line: centered on the canvas, must stay inside the margin band.
    Many lines: the top line is limited by the top margin, the bottom line
    by the bottom margin, and adjacent line centers are `span / (n - 1)`
    apart, so half that gap per side.
    """
    top_margin = size * TOP_MARGIN_RATIO
    bottom_margin = size * BOTTOM_MARGIN_RATIO
    span = size - top_margin - bottom_margin
    return size / 2.0 if n_lines == 1 else min(
        top_margin, bottom_margin, span / (2 * (n_lines - 1))
    )


def _base_font_size(size: int, n_lines: int) -> int:
    """Start large for a few short lines, smaller as the line count grows."""
    return max(MIN_FONT_SIZE, size // max(2, n_lines))  # never start at full canvas size


def _stroke_width(font_size: int) -> int:
    """Outline thickness for `font_size`, never thinner than MIN_STROKE_PX."""
    return max(MIN_STROKE_PX, int(round(font_size * STROKE_RATIO)))


def _line_metrics(
    draw: ImageDraw.ImageDraw,
    line: str,
    font: ImageFont.FreeTypeFont,
    stroke_width: int,
) -> Tuple[float, float, float]:
    """Ink extents of `line` when drawn centered on its anchor point.

    Returns (width, top_extent, bottom_extent); top_extent is the distance
    of the ink above the line center (positive up), bottom_extent below.
    """
    ascent, descent = font.getmetrics()
    mid = (ascent + descent) / 2.0  # the anchor's "mm" line center, in text coords
    left, top, right, bottom = draw.textbbox(
        (0, 0), line, font=font, stroke_width=stroke_width
    )
    return right - left, top - mid, bottom - mid


def _fit_font(
    draw: ImageDraw.ImageDraw,
    font_path: str,
    lines: Sequence[str],
    max_width: float,
    max_extent: float,
    size: int,
) -> Tuple[ImageFont.FreeTypeFont, int]:
    """Shrink the font until every line fits; return (font, stroke_width).

    Fits means: widest line ink width <= `max_width` and the tallest ink
    extent above/below any line center <= `max_extent`. Shrinks by
    SHRINK_FACTOR per step until the MIN_FONT_SIZE floor.
    """
    font_size = _base_font_size(size, len(lines))
    while True:
        font = ImageFont.truetype(font_path, font_size)
        stroke_width = _stroke_width(font_size)
        widest = 0.0
        tallest = 0.0
        for line in lines:
            line_width, top_extent, bottom_extent = _line_metrics(
                draw, line, font, stroke_width
            )
            widest = max(widest, line_width)
            tallest = max(tallest, abs(top_extent), abs(bottom_extent))
        if (widest <= max_width and tallest <= max_extent) or font_size <= MIN_FONT_SIZE:
            break
        font_size = max(MIN_FONT_SIZE, int(font_size * SHRINK_FACTOR))
    return font, stroke_width


def _draw_lines(
    draw: ImageDraw.ImageDraw,
    lines: Sequence[str],
    font: ImageFont.FreeTypeFont,
    stroke_width: int,
    size: int,
) -> None:
    """Draw `lines` (white fill, black outline), evenly spread top to bottom.

    Line centers run from the top margin to `size - bottom_margin`, equally
    spaced (a single line sits dead center).
    """
    n = len(lines)
    top_margin = size * TOP_MARGIN_RATIO
    bottom_margin = size * BOTTOM_MARGIN_RATIO
    span = size - top_margin - bottom_margin
    for i, line in enumerate(lines):
        if n == 1:
            y = size / 2.0
        else:
            y = top_margin + span * i / (n - 1)
        draw.text(
            (size / 2.0, y),
            line,
            font=font,
            fill=TEXT_FILL,
            stroke_width=stroke_width,
            stroke_fill=STROKE_FILL,
            anchor=TEXT_ANCHOR,
        )


def generate(
    input_path: str,
    text: str,
    output_path: str,
    size: int = MAX_SIZE,
) -> None:
    """Render the cover and save it as an RGB PNG of exactly size x size.

    `size` is clamped to a positive value no greater than MAX_SIZE.
    """
    size = _clamp_size(size)
    image = _prepare_background(input_path, size)

    lines = split_lines(text)
    if not lines:
        raise ValueError("text produced no lines")

    draw = ImageDraw.Draw(image)
    font, stroke_width = _fit_font(
        draw,
        FONT_PATH,
        lines,
        max_width=size * MAX_WIDTH_RATIO,
        max_extent=_vertical_limit(size, len(lines)),
        size=size,
    )
    _draw_lines(draw, lines, font, stroke_width, size)

    out_dir = os.path.dirname(os.path.abspath(output_path))
    os.makedirs(out_dir, exist_ok=True)
    image.save(output_path, format=OUTPUT_FORMAT)


def main(argv: Optional[Sequence[str]] = None) -> int:
    """CLI entry point: argparse setup and a call to generate()."""
    parser = argparse.ArgumentParser(
        description="Generate a square OXI mod cover PNG with centered stroked text."
    )
    parser.add_argument("input", help="input screenshot image (jpg/png)")
    parser.add_argument(
        "text",
        help='title text; literal "\\n" (shell: "\\\\n") forces a line break, '
        "otherwise words are split one per line",
    )
    parser.add_argument("output", help="output PNG path")
    parser.add_argument(
        "--size",
        type=int,
        default=MAX_SIZE,
        help=f"cover edge in px (capped at {MAX_SIZE}; default {MAX_SIZE})",
    )
    args = parser.parse_args(argv)
    generate(args.input, args.text, args.output, size=args.size)
    return 0


if __name__ == "__main__":
    sys.exit(main())
