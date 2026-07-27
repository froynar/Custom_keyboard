from __future__ import annotations

import math
import sys
import glob
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    output = Path(sys.argv[1])
    paths = []
    for value in sys.argv[2:]:
        matches = sorted(glob.glob(value)) if any(char in value for char in "*?[") else [value]
        paths.extend(Path(match) for match in matches)
    images = [Image.open(path).convert("RGB") for path in paths]
    thumb_width = 360
    label_height = 30
    columns = 4
    rows = math.ceil(len(images) / columns)
    thumbs = []
    max_height = 0
    for image in images:
        height = round(image.height * thumb_width / image.width)
        thumb = image.resize((thumb_width, height))
        thumbs.append(thumb)
        max_height = max(max_height, height)

    sheet = Image.new("RGB", (columns * thumb_width, rows * (max_height + label_height)), "white")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for index, (path, thumb) in enumerate(zip(paths, thumbs)):
        x = (index % columns) * thumb_width
        y = (index // columns) * (max_height + label_height)
        sheet.paste(thumb, (x, y + label_height))
        draw.text((x + 8, y + 8), path.stem, fill="black", font=font)
    sheet.save(output)


if __name__ == "__main__":
    main()
