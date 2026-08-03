"""Draws a coordinate grid and the current props onto the sandbox overview."""

import io
import re

from PIL import Image, ImageDraw

MIN_X, MAX_X = -28.0, 52.0
MIN_Z, MAX_Z = -22.0, 50.0
PPM = 12
MARGIN = 46

base = Image.open('Logs/sandbox-overview.png').convert('RGB')
w, h = base.size

out = Image.new('RGB', (w + MARGIN * 2, h + MARGIN * 2), (24, 25, 28))
out.paste(base, (MARGIN, MARGIN))
d = ImageDraw.Draw(out, 'RGBA')


def to_px(x, z):
    """World to image. North is +z and the image's rows run the other way, so
    the flip happens once, here."""
    px = MARGIN + (x - MIN_X) * PPM
    py = MARGIN + (MAX_Z - z) * PPM
    return px, py


# --- grid every 8 m, labelled at the edges ---
step = 8
x = int(MIN_X)
while x <= MAX_X:
    if (x - int(MIN_X)) % step == 0:
        px, _ = to_px(x, 0)
        d.line([(px, MARGIN), (px, MARGIN + h)], fill=(255, 255, 255, 40))
        d.text((px - 10, MARGIN - 18), f'{x:g}', fill=(200, 205, 215))
        d.text((px - 10, MARGIN + h + 6), f'{x:g}', fill=(200, 205, 215))
    x += 1

z = int(MIN_Z)
while z <= MAX_Z:
    if (z - int(MIN_Z)) % step == 0:
        _, py = to_px(0, z)
        d.line([(MARGIN, py), (MARGIN + w, py)], fill=(255, 255, 255, 40))
        d.text((6, py - 6), f'{z:g}', fill=(200, 205, 215))
        d.text((MARGIN + w + 6, py - 6), f'{z:g}', fill=(200, 205, 215))
    z += 1

# --- what is already placed ---
rows = io.open('Logs/sandbox-placements.txt', encoding='utf-8').read().split('\n')
counts = {}
for line in rows:
    m = re.match(r'(\S.*?)\s+x\s+(-?[\d.]+)\s+z\s+(-?[\d.]+)', line.strip())
    if not m:
        continue
    name, wx, wz = m.group(1).strip(), float(m.group(2)), float(m.group(3))
    if name.startswith('Block ') or name.endswith('Boundary'):
        continue
    px, py = to_px(wx, wz)
    if name.startswith('env_street_lamp'):
        colour, tag = (255, 214, 90), 'L'
    elif name.startswith('env_tree'):
        colour, tag = (120, 235, 130), 'T'
    else:
        colour, tag = (255, 130, 130), '?'
    counts[tag] = counts.get(tag, 0) + 1
    d.ellipse([px - 7, py - 7, px + 7, py + 7],
              outline=colour, width=3)
    d.text((px - 3, py - 6), tag, fill=colour)

legend = (f"grid 8 m   north is up (+z)   "
          f"L = street lamp ({counts.get('L', 0)})   "
          f"T = tree ({counts.get('T', 0)})")
d.text((MARGIN, 8), legend, fill=(235, 238, 245))

out.save('Logs/sandbox-plan.png')
print('written', out.size, counts)
