#!/usr/bin/env python3
"""Create a diagnostic DOCX containing only a prefix of body blocks."""

from __future__ import annotations

import argparse
from copy import deepcopy
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from lxml import etree


W_NS = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--limit", type=int, required=True)
    args = parser.parse_args()

    with ZipFile(args.input, "r") as source:
        files = {name: source.read(name) for name in source.namelist()}

    root = etree.fromstring(files["word/document.xml"])
    body = root.find(f"{{{W_NS}}}body")
    assert body is not None
    blocks = [child for child in body if etree.QName(child).localname != "sectPr"]
    section = next(child for child in body if etree.QName(child).localname == "sectPr")
    for child in list(body):
        body.remove(child)
    for child in blocks[: args.limit]:
        body.append(deepcopy(child))
    body.append(deepcopy(section))
    files["word/document.xml"] = etree.tostring(
        root, xml_declaration=True, encoding="UTF-8", standalone="yes"
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(args.output, "w", ZIP_DEFLATED) as target:
        for name, data in files.items():
            target.writestr(name, data)

    print(f"Wrote {args.output} with {min(args.limit, len(blocks))}/{len(blocks)} body blocks")


if __name__ == "__main__":
    main()
