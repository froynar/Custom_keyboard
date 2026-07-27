#!/usr/bin/env python3
"""Replace selected OOXML parts in a DOCX for compatibility diagnostics."""

from __future__ import annotations

import argparse
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("base", type=Path)
    parser.add_argument("donor", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("parts", nargs="+")
    args = parser.parse_args()

    with ZipFile(args.base, "r") as package:
        files = {name: package.read(name) for name in package.namelist()}
    with ZipFile(args.donor, "r") as donor:
        for part in args.parts:
            files[part] = donor.read(part)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(args.output, "w", ZIP_DEFLATED) as package:
        for name, data in files.items():
            package.writestr(name, data)
    print(f"Wrote {args.output}; replaced: {', '.join(args.parts)}")


if __name__ == "__main__":
    main()
