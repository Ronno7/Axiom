"""Draws the application icon from the shared palette: the title's A, in Earth Orbiter Bold, on Void.

    python make_app_icon.py

Writes Assets/UI/AppIcon.png at 1024 x 1024. Needs Pillow.

The font has only capitals. Its "a" slot holds an alternate A, open at the lower left with a foot, and the
title ("aXiom", see ../Title/make_title.py) uses that one, so the icon does too.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
PALETTE = json.loads((HERE.parent / "Palette" / "palette.json").read_text(encoding="utf-8"))
COLORS = {c["name"]: c["hex"] for c in PALETTE["colors"]}
FONT = HERE.parent / "earth-orbiter" / "earthorbiterbold.ttf"
GLYPH = "a"

SIZE = 1024
BIG = SIZE * 4                 # Drawn at four times the size, then reduced.
UNIT = BIG / 64                # The icon's 64 unit grid.
GLYPH_HEIGHT = 34 * UNIT       # Cap height of the A.
CENTRE_Y = 28 * UNIT           # The A sits above centre so the A and the bar below it are centred together.

image = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((0, 0, BIG - 1, BIG - 1), radius=12 * UNIT, fill=COLORS["Void"])

# Size the font so the glyph's ink is GLYPH_HEIGHT tall, then centre the ink (not the advance box).
probe = ImageFont.truetype(str(FONT), 1000)
left, top, right, bottom = probe.getbbox(GLYPH)
font = ImageFont.truetype(str(FONT), round(1000 * GLYPH_HEIGHT / (bottom - top)))
left, top, right, bottom = font.getbbox(GLYPH)
x = BIG / 2 - (left + right) / 2
y = CENTRE_Y - (top + bottom) / 2
draw.text((x, y), GLYPH, font=font, fill=COLORS["Light"])

# The cyan bar under the mark, centred on the icon.
bar_y = y + bottom + 6 * UNIT
draw.rectangle((29 * UNIT, bar_y, 35 * UNIT, bar_y + 3 * UNIT), fill=COLORS["Cyan"])

output = HERE.parents[1] / "Assets" / "UI" / "AppIcon.png"
image.resize((SIZE, SIZE), Image.LANCZOS).save(output)
print(output)
