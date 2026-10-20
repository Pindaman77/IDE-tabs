"""Generate the IDE-tabs application and tray icon: three project tabs beside a sidebar rail.

Run with Python and Pillow. Keep every ICO frame: Windows uses the small ones for the
notification area. 40 px and up are drawn at 1024 px and scaled down; 16-32 px are placed
on the pixel grid so the three tabs stay apart in the tray.
"""

from pathlib import Path
from io import BytesIO
import struct
from PIL import Image, ImageDraw


HERE = Path(__file__).resolve().parent
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)

BLUE, BLUE_DEEP = (59, 130, 246), (37, 99, 235)
PURPLE_LEFT, PURPLE = (99, 102, 241), (139, 92, 246)
SLATE, SLATE_TOP = (15, 23, 42), (30, 41, 59)
GRAY = (71, 85, 105)
RAIL_TOP, RAIL_BOTTOM = (148, 163, 184), (71, 85, 105)
BORDER = (51, 65, 85)


def gradient(size, start, end, horizontal=False):
    w, h = size
    steps = w if horizontal else h
    line = Image.new("RGBA", (steps, 1) if horizontal else (1, steps))
    for i in range(steps):
        t = i / max(1, steps - 1)
        line.putpixel((i, 0) if horizontal else (0, i), tuple(round(a + (b - a) * t) for a, b in zip(start, end)) + (255,))
    return line.resize((w, h), Image.Resampling.NEAREST)


def paste_rounded(canvas, box, fill, radius):
    x0, y0, x1, y1 = [round(v) for v in box]
    size = (x1 - x0, y1 - y0)
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=round(radius), fill=255)
    layer = fill if isinstance(fill, Image.Image) else Image.new("RGBA", size, fill + (255,))
    canvas.paste(layer, (x0, y0), mask)


def large(size=1024):
    """The full design on a 256-unit grid."""
    s = size / 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    paste_rounded(img, (0, 0, size, size), BORDER, 58 * s)
    inner = round(size - 4 * s)
    paste_rounded(img, (2 * s, 2 * s, 2 * s + inner, 2 * s + inner), gradient((inner, inner), SLATE_TOP, SLATE), 56 * s)
    paste_rounded(img, (38 * s, 34 * s, 47 * s, 222 * s), gradient((round(9 * s), round(188 * s)), RAIL_TOP, RAIL_BOTTOM), 4.5 * s)
    paste_rounded(img, (64 * s, 40 * s, 188 * s, 92 * s), gradient((round(124 * s), round(52 * s)), BLUE, BLUE_DEEP), 14 * s)
    paste_rounded(img, (64 * s, 102 * s, 212 * s, 154 * s), gradient((round(148 * s), round(52 * s)), PURPLE_LEFT, PURPLE, horizontal=True), 14 * s)
    paste_rounded(img, (64 * s, 164 * s, 188 * s, 216 * s), GRAY, 14 * s)
    ImageDraw.Draw(img).ellipse((79 * s, 119 * s, 97 * s, 137 * s), fill=(255, 255, 255, 255))
    return img


# Small sizes, in pixels: corner radius, rail (x, top, bottom), tab left edge, rows as
# (top, bottom), right edges of the short and wide tabs, and the active dot (x, y, radius).
SMALL = {
    32: dict(r=7, rail=(6, 5, 27), left=9, rows=[(6, 12), (13, 19), (20, 26)], short=24, wide=27, dot=(12.5, 16, 1.6)),
    24: dict(r=5, rail=(4, 4, 20), left=7, rows=[(5, 9), (10, 14), (15, 19)], short=18, wide=20, dot=(9.5, 12, 1.2)),
    20: dict(r=4, rail=(3, 3, 17), left=6, rows=[(4, 8), (9, 13), (14, 18)], short=15, wide=17, dot=(8, 11, 1.0)),
    16: dict(r=3, rail=(2, 2, 14), left=4, rows=[(3, 6), (7, 10), (11, 14)], short=12, wide=14, dot=None),
}


def small(size):
    """Every edge on a whole pixel; drawn at 8x and averaged down so the corners stay smooth."""
    k = 8
    spec = SMALL[size]
    img = Image.new("RGBA", (size * k, size * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, size * k - 1, size * k - 1), radius=spec["r"] * k, fill=BORDER + (255,))
    d.rounded_rectangle((k, k, (size - 1) * k - 1, (size - 1) * k - 1), radius=(spec["r"] - 1) * k, fill=SLATE + (255,))
    x, y0, y1 = spec["rail"]
    d.rectangle((x * k, y0 * k, (x + 1) * k - 1, y1 * k - 1), fill=RAIL_TOP + (255,))
    for (top, bottom), colour, right in zip(spec["rows"], [BLUE, PURPLE, GRAY], [spec["short"], spec["wide"], spec["short"]]):
        d.rounded_rectangle((spec["left"] * k, top * k, right * k - 1, bottom * k - 1), radius=k if size >= 20 else k // 2, fill=colour + (255,))
    if spec["dot"]:
        cx, cy, r = spec["dot"]
        d.ellipse(((cx - r) * k, (cy - r) * k, (cx + r) * k, (cy + r) * k), fill=(255, 255, 255, 255))
    else:
        top, _ = spec["rows"][1]
        d.rectangle(((spec["left"] + 1) * k, (top + 1) * k, (spec["left"] + 2) * k - 1, (top + 2) * k - 1), fill=(255, 255, 255, 255))
    return img.resize((size, size), Image.Resampling.BOX)


def frame(size):
    return small(size) if size in SMALL else large().resize((size, size), Image.Resampling.LANCZOS)


def write_icon(name, images):
    payloads = []
    for image in images:
        stream = BytesIO()
        image.save(stream, format="PNG")
        payloads.append(stream.getvalue())
    offset = 6 + 16 * len(SIZES)
    with (HERE / name).open("wb") as icon:
        icon.write(struct.pack("<HHH", 0, 1, len(SIZES)))
        for size, payload in zip(SIZES, payloads):
            icon.write(struct.pack("<BBBBHHII", size % 256, size % 256,
                                   0, 0, 1, 32, len(payload), offset))
            offset += len(payload)
        for payload in payloads:
            icon.write(payload)


if __name__ == "__main__":
    write_icon("IDE-tabs.ico", [frame(size) for size in SIZES])
