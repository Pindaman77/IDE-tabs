"""Generate the IDE-tabs README logos (light and dark) and the GitHub social preview.

Run with Python and Pillow on Windows: the wordmark is set in Segoe UI from the system's
fonts folder, which is not redistributed here. The icon comes from the app's own generator.
"""

import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[1] / "WtProgram" / "Resources"))
from generate_icons import large  # noqa: E402

FONTS = Path("C:/Windows/Fonts")
BOLD, REGULAR = str(FONTS / "segoeuib.ttf"), str(FONTS / "segoeui.ttf")
BLUE, INK, WHITE = (59, 130, 246), (15, 23, 42), (255, 255, 255)
TAG_LIGHT, TAG_DARK = (71, 85, 105), (203, 213, 225)
TAGLINE = "One workspace. All your projects."


def wordmark(height, dark):
    """The icon, then "IDE-" and "tabs" with the tagline under them, `height` pixels tall."""
    icon = large().resize((height, height), Image.Resampling.LANCZOS)
    name = ImageFont.truetype(BOLD, round(height * 0.62))
    tag = ImageFont.truetype(REGULAR, round(height * 0.17))
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    first = probe.textlength("IDE-", font=name)
    width = height + round(height * 0.22) + round(max(first + probe.textlength("tabs", font=name),
                                                      probe.textlength(TAGLINE, font=tag))) + round(height * 0.05)
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    img.paste(icon, (0, 0), icon)
    d = ImageDraw.Draw(img)
    x = height + round(height * 0.22)
    d.text((x, round(height * 0.02)), "IDE-", font=name, fill=(WHITE if dark else INK) + (255,), anchor="lt")
    d.text((x + first, round(height * 0.02)), "tabs", font=name, fill=BLUE + (255,), anchor="lt")
    d.text((x + round(height * 0.02), round(height * 0.74)), TAGLINE, font=tag,
           fill=(TAG_DARK if dark else TAG_LIGHT) + (255,), anchor="lt")
    return img


def social():
    """1280x640, the size GitHub shows for a repository's link preview."""
    img = Image.new("RGBA", (1280, 640), INK + (255,))
    mark = wordmark(220, dark=True)
    img.paste(mark, ((1280 - mark.width) // 2, (640 - mark.height) // 2 - 20), mark)
    ImageDraw.Draw(img).text((640, 520), "One Windows workspace for all your Cursor projects",
                             font=ImageFont.truetype(REGULAR, 26), fill=(148, 163, 184, 255), anchor="mm")
    return img


if __name__ == "__main__":
    # Twice the size the README shows, for sharp text on scaled displays.
    wordmark(160, dark=False).save(HERE / "logo-light.png")
    wordmark(160, dark=True).save(HERE / "logo-dark.png")
    social().convert("RGB").save(HERE / "social-preview.png")
    large().resize((256, 256), Image.Resampling.LANCZOS).save(HERE / "icon.png")
