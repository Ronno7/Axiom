"""Draws the application icon from the shared palette: the showcase page's A mark on Void.

    python make_app_icon.py

Writes Assets/UI/AppIcon.png at 1024 x 1024. Needs Pillow.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
PALETTE = json.loads((HERE.parent / "Palette" / "palette.json").read_text(encoding="utf-8"))
COLORS = {c["name"]: c["hex"] for c in PALETTE["colors"]}

SIZE = 1024
SCALE = SIZE * 4 / 64  # Drawn at four times the size, on the mark's 64 unit grid, then reduced.
MARK = [(12, 49), (32, 14), (52, 49), (39, 49), (32, 36), (25, 49), (12, 49), (32, 14)]


def units(points):
    return [(x * SCALE, y * SCALE) for x, y in points]


image = Image.new("RGBA", (SIZE * 4, SIZE * 4), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((0, 0, SIZE * 4 - 1, SIZE * 4 - 1), radius=12 * SCALE, fill=COLORS["Void"])
draw.line(units(MARK), fill=COLORS["Light"], width=round(3 * SCALE), joint="curve")
draw.line(units([(29, 54), (35, 54)]), fill=COLORS["Cyan"], width=round(3 * SCALE))

output = HERE.parents[1] / "Assets" / "UI" / "AppIcon.png"
image.resize((SIZE, SIZE), Image.LANCZOS).save(output)
print(output)
