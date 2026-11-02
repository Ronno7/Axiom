"""Compose existing Blender renders into review sheets and a looping GIF (Pillow).

python compose_review.py v02
"""
import sys
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / 'reviews' / sys.argv[1]
if not OUT.exists():
    OUT = Path(__file__).resolve().parents[1] / 'archive' / sys.argv[1]
BG = '#0C141F'
TEXT = '#E6FFFF'
MUTED = '#8198A6'


def font(size):
    try:
        return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)
    except OSError:
        return ImageFont.load_default(size=size)


def sheet(filename, title, subtitle, entries, cols, cell=(600, 400)):
    rows = (len(entries) + cols - 1) // cols
    w, h = cell
    result = Image.new('RGB', (cols * w, 110 + rows * (h + 42) + 40), BG)
    draw = ImageDraw.Draw(result)
    draw.text((28, 16), title, font=font(31), fill=TEXT)
    draw.text((28, 62), subtitle, font=font(18), fill=MUTED)
    for i, (name, label) in enumerate(entries):
        im = Image.open(OUT / (name + '.png')).convert('RGB')
        im.thumbnail((w, h), Image.Resampling.LANCZOS)
        x, y = i % cols * w, 110 + i // cols * (h + 42)
        result.paste(im, (x + (w - im.width) // 2, y + (h - im.height) // 2))
        draw.text((x + 22, y + h + 6), label, font=font(19), fill=TEXT)
    draw.text((28, result.height - 28), 'Blender studio study / 2026-10-31 / ' + OUT.name,
              font=font(15), fill=MUTED)
    result.save(OUT / filename)


manifest = json.loads((OUT / 'manifest.json').read_text())
bounds = manifest['hull_bounds_unity']
size = [b-a for a,b in zip(bounds['min'], bounds['max'])]
sheet('review-sheet.png', 'AXIOM / SWEPT DART',
      '%s | %.2f m span | %.3f m hull length | %.3f m hull height' %
      (manifest['stage'].capitalize(), size[0], size[2], size[1]),
      [('front', '01 / Front three-quarter'), ('rear-three-quarter', '02 / Rear three-quarter'),
       ('top', '03 / Top'), ('side', '04 / Side'),
       ('underside', '05 / Underside'), ('rear', '06 / Rear')], 2)
sheet('flight-comparison.png', 'AXIOM / CHASE VIEW COMPARISON',
      'Same framing; placeholder hull only. Blender approximation, not a Unity capture.',
      [('chase-placeholder', '01 / Original hull / 62 degree vertical FOV'),
       ('chase-new', '02 / Swept Dart / 62 degree vertical FOV'),
       ('chase-boost-fov', '03 / Swept Dart / 72 degree vertical FOV'),
       ('collider', '04 / Existing smaller gameplay collider guide')], 2, (640, 360))
if (OUT / 'bottom.png').exists():
    sheet('underside-sheet.png', 'AXIOM / VENTRAL STRUCTURE',
          'Shaped keel / recessed service panels / swept wing spars / drive vents',
          [('underside', '01 / Forward underside'), ('belly-rear', '02 / Rear underside'),
           ('bottom', '03 / Bottom orthographic'), ('belly-close', '04 / Belly detail')], 2)
    sheet('construction-sheet.png', 'AXIOM / AUTHORED CONSTRUCTION PATHS',
          'Separate editable curves; placement study for a later ship-local reveal.',
          [('construction-lines', '01 / Spine, silhouette and structural sections'),
           ('construction-overlay', '02 / Paths on the hull')], 2)
if (OUT / 'junction.png').exists():
    sheet('junction-sheet.png','AXIOM / CONTINUOUS HULL',
          'Welded roots / integral recesses / fitted lights / assembly intersection checks',
          [('junction','01 / Spine, wing roots and drive shoulders'),
           ('junction-clay','02 / Surface continuity in clay'),
           ('belly-close','03 / Integrated underside panels'),
           ('belly-clay','04 / Closed belly and drive vents')],2)

frames = [Image.open(p).convert('RGB') for p in sorted(OUT.glob('turntable-*.png'))]
assert len(frames) == 24, 'Render all turntable views before composing.'
# Shared palette prevents color flicker between independently quantized GIF frames.
atlas = Image.new('RGB', (640 * 6, 432 * 4))
for i, frame in enumerate(frames):
    atlas.paste(frame, (i % 6 * 640, i // 6 * 432))
palette = atlas.quantize(colors=256)
indexed = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
indexed[0].save(OUT / 'turntable.gif', save_all=True, append_images=indexed[1:],
                duration=160, loop=0, disposal=2)
print('Composed review-sheet.png, flight-comparison.png and turntable.gif in', OUT)
