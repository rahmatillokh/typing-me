"""Builds the WebGL page's static assets from art the repo already has.

- TemplateData/favicon.png  — the app icon at 256 px
- TemplateData/preview.jpg  — a 1200×630 Open Graph card cut from the gameplay screenshot,
                              so a shared link unfurls with the game rather than a blank tile
Run from the repo root: python3 docs/tools/make_web_assets.py
"""
from PIL import Image

ICON = "Assets/Art/Icon/AppIcon.png"
SHOT = "docs/screenshots/gameplay.png"
OUT_DIR = "Assets/WebGLTemplates/TypingMe/TemplateData"


def favicon() -> None:
    Image.open(ICON).convert("RGBA").resize((256, 256), Image.LANCZOS).save(f"{OUT_DIR}/favicon.png")


def preview() -> None:
    shot = Image.open(SHOT).convert("RGB")
    target_w, target_h = 1200, 630

    # Crop to the card's aspect from the centre, then scale — no letterboxing on social cards.
    scale = max(target_w / shot.width, target_h / shot.height)
    scaled = shot.resize((round(shot.width * scale), round(shot.height * scale)), Image.LANCZOS)
    left = (scaled.width - target_w) // 2
    top = (scaled.height - target_h) // 2
    scaled.crop((left, top, left + target_w, top + target_h)).save(
        f"{OUT_DIR}/preview.jpg", quality=88, optimize=True)


if __name__ == "__main__":
    favicon()
    preview()
    print(f"wrote {OUT_DIR}/favicon.png and preview.jpg")
