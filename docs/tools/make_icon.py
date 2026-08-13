"""Composes the Typing Me app icon from the dragon sprite.

1024x1024, Big-Sur-style rounded rect with transparent margins, so the same
file serves as Unity's single Default Icon for both macOS and Windows.
"""
from PIL import Image, ImageDraw, ImageFilter

SIZE = 1024
MARGIN = 100                 # Apple template: content ~824px centred in 1024.
RADIUS = 185
CONTENT = SIZE - MARGIN * 2

GOLD = (242, 180, 55)        # HudUI.RankColour(S) — the dragon's gold.
BG_TOP = (11, 15, 24)
BG_BOTTOM = (5, 7, 12)

SPRITE = "Assets/Art/Bosses/S-ajdar.png"
OUT = "Assets/Art/Icon/AppIcon.png"


def rounded_mask() -> Image.Image:
    mask = Image.new("L", (SIZE, SIZE), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle(
        (MARGIN, MARGIN, SIZE - MARGIN, SIZE - MARGIN), radius=RADIUS, fill=255)
    return mask


def gradient_plate() -> Image.Image:
    plate = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    for y in range(MARGIN, SIZE - MARGIN):
        t = (y - MARGIN) / CONTENT
        colour = tuple(
            round(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3))
        ImageDraw.Draw(plate).line(
            [(MARGIN, y), (SIZE - MARGIN, y)], fill=colour + (255,))
    return plate


def grid_overlay() -> Image.Image:
    grid = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(grid)
    step = 96
    for x in range(MARGIN, SIZE - MARGIN + 1, step):
        draw.line([(x, MARGIN), (x, SIZE - MARGIN)], fill=GOLD + (22,), width=2)
    for y in range(MARGIN, SIZE - MARGIN + 1, step):
        draw.line([(MARGIN, y), (SIZE - MARGIN, y)], fill=GOLD + (22,), width=2)
    return grid


def soft(layer: Image.Image, blur: float) -> Image.Image:
    return layer.filter(ImageFilter.GaussianBlur(blur))


def main() -> None:
    content = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    content.alpha_composite(gradient_plate())
    content.alpha_composite(grid_overlay())

    # Warm glow rising from the bottom, like the boss underglow.
    glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((212, 660, SIZE - 212, 1010), fill=GOLD + (70,))
    content.alpha_composite(soft(glow, 90))

    # Grounding shadow, then the dragon itself — nearest-neighbour keeps the pixels crisp.
    shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((292, 700, SIZE - 292, 800), fill=(0, 0, 0, 150))
    content.alpha_composite(soft(shadow, 30))

    dragon = Image.open(SPRITE).convert("RGBA").resize((560, 560), Image.NEAREST)
    content.alpha_composite(dragon, ((SIZE - 560) // 2, 178))

    # The bottom line every word races toward.
    line = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(line).rounded_rectangle((342, 806, SIZE - 342, 818), radius=6,
                                           fill=GOLD + (255,))
    content.alpha_composite(soft(line, 8))   # halo first…
    content.alpha_composite(line)            # …then the crisp line on top.

    # Clip to the plate, add the rim, and mount on the transparent canvas.
    mask = rounded_mask()
    icon = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    icon.paste(content, (0, 0), mask)

    rim = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(rim).rounded_rectangle(
        (MARGIN, MARGIN, SIZE - MARGIN, SIZE - MARGIN),
        radius=RADIUS, outline=GOLD + (110,), width=4)
    icon.alpha_composite(rim)

    icon.save(OUT)
    print(f"saved {OUT}")


if __name__ == "__main__":
    main()
