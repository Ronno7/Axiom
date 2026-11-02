"""Package the v13 high quality renders as sheets and H.264 review videos."""
import json
import subprocess
import zipfile
from pathlib import Path

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / 'reviews/v13/presentation'
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
FONT = 'C:/Windows/Fonts/arial.ttf'
BG = '#07151B'
FG = '#E6FFFF'
MUTED = '#B7C9D1'


def font(size):
    return ImageFont.truetype(FONT, size)


def sheet(filename, title, subtitle, tiles, columns, width=2400):
    tile_w = (width - 120) // columns
    tile_h = tile_w * 9 // 16
    row_h = tile_h + 72
    rows = (len(tiles) + columns - 1) // columns
    canvas = Image.new('RGB', (width, 190 + rows * row_h + 40), BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((60, 32), title, font=font(44), fill=FG)
    draw.text((60, 98), subtitle, font=font(23), fill=MUTED)
    for i, (file, label) in enumerate(tiles):
        x, y = 60 + (i % columns) * tile_w, 175 + (i // columns) * row_h
        with Image.open(OUT / file) as im:
            canvas.paste(im.resize((tile_w, tile_h), Image.Resampling.LANCZOS), (x, y))
        draw.text((x + 12, y + tile_h + 16), label, font=font(23), fill=FG)
    canvas.save(OUT / filename)


frames = sorted((OUT / 'frames').glob('sequence-*.png'))
assert len(frames) == 72
stills = sorted(OUT.glob('ship-*.png')) + sorted(OUT.glob('stage-*.png'))
assert len(stills) == 10
for path in stills:
    with Image.open(path) as im:
        assert im.size == (3840, 2160)
        im.verify()
for i, path in enumerate(frames, 1):
    assert path.name == 'sequence-%03d.png' % i
    with Image.open(path) as im:
        assert im.size == (1920, 1080)
        im.verify()
videos = []
for name, rate, seconds in [('sequence-normal.mp4', 24, 3), ('sequence-slow.mp4', 6, 12)]:
    subprocess.run([FFMPEG, '-y', '-v', 'error', '-framerate', str(rate),
                    '-i', str(OUT / 'frames' / 'sequence-%03d.png'), '-an', '-c:v', 'libx264',
                    '-preset', 'slow', '-crf', '16', '-pix_fmt', 'yuv420p', '-r', '24',
                    '-movflags', '+faststart', str(OUT / name)], check=True)
    # Decode the complete deliverable and validate dimensions, duration and frame count.
    decoded = imageio_ffmpeg.read_frames(str(OUT / name), pix_fmt='rgb24')
    metadata = next(decoded)
    count = sum(1 for _ in decoded)
    assert tuple(metadata['size']) == (1920, 1080)
    assert count == seconds * 24
    assert abs(metadata['duration'] - seconds) < .05
    videos.append(dict(file=name, decoded_frames=count, seconds=seconds, size=[1920,1080]))
sheet('ship-sheet.jpg', 'AXIOM / SWEPT DART', 'Approved low-poly hull / 4K studio renders', [
    ('ship-front.png', '01 / Front three-quarter'),
    ('ship-rear.png', '02 / Rear three-quarter'),
    ('ship-underside.png', '03 / Underside')], 1, 1920)
stages = [(6, 'Control seeds'), (28, 'Mechanical contours'), (40, 'First physical regions'),
          (46, 'Edges inward / center outward'), (54, 'Joining fronts'),
          (62, 'Shell consolidation'), (72, 'Complete')]
sheet('sequence-sheet.jpg', 'AXIOM / MATERIALIZATION',
      'Approved v13 / 3 seconds / 24 fps / overlapping construction and solidification', [
          ('stage-%03d.png' % f, '%02d / %.2f s / %s' % (i+1, (f-1)/24, label))
          for i, (f, label) in enumerate(stages)], 2)
# A lightweight inline animation; MP4s retain full resolution and continuous color.
gif_frames = []
for path in frames:
    with Image.open(path) as im:
        gif_frames.append(im.resize((960, 540), Image.Resampling.LANCZOS).quantize(colors=256))
durations = [round((i+1)*100/24)*10-round(i*100/24)*10 for i in range(72)]
durations[-1] += 1000
gif_frames[0].save(OUT/'sequence-preview.gif', save_all=True, append_images=gif_frames[1:],
                   duration=durations, loop=0, disposal=2)
(OUT/'media-checks.json').write_text(json.dumps({
    'png_frames_verified': len(frames), 'four_k_stills_verified': len(stills), 'videos': videos,
    'preview_gif_final_hold_seconds': 1, 'source_timing_unchanged': True}, indent=2))
with zipfile.ZipFile(OUT/'SweptDart-v13-renders.zip', 'w', zipfile.ZIP_STORED) as archive:
    for pattern in ('ship-*.png', 'stage-*.png', '*.mp4', '*-sheet.jpg', 'sequence-preview.gif',
                    'render-manifest-*.json', 'media-checks.json'):
        for path in sorted(OUT.glob(pattern)):
            archive.write(path, path.name)
print(json.dumps(videos, indent=2))
