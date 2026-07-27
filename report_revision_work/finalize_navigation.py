from __future__ import annotations

import re
import sys
from pathlib import Path

import pdfplumber
from docx import Document

from restructure_report import (
    FIGURE_ENTRIES,
    TABLE_ENTRIES,
    TOC_ENTRIES,
    add_nav_line_before,
    find_paragraph,
    find_paragraph_startswith,
    remove_between,
    replace_paragraph_text,
)


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip().casefold()


def build_page_map(pdf_path: Path) -> tuple[int, dict[str, int]]:
    targets: dict[str, str] = {
        anchor: text for anchor, _level, text in TOC_ENTRIES
    }
    targets.update(dict(FIGURE_ENTRIES))
    targets.update(dict(TABLE_ENTRIES))

    page_map: dict[str, int] = {}
    with pdfplumber.open(pdf_path) as pdf:
        for page_number, page in enumerate(pdf.pages, start=1):
            page_text = normalize(page.extract_text() or "")
            for anchor, label in targets.items():
                if normalize(label) in page_text:
                    # The final occurrence wins, so body headings/captions override
                    # their copies in the front-matter lists.
                    page_map[anchor] = page_number
        page_count = len(pdf.pages)

    missing = [f"{anchor}: {label}" for anchor, label in targets.items() if anchor not in page_map]
    if missing:
        raise RuntimeError("Không tìm thấy mục trong PDF:\n" + "\n".join(missing))
    return page_count, page_map


def rebuild_navigation(doc: Document, page_map: dict[str, int]) -> None:
    toc_title = find_paragraph(doc, "MỤC LỤC")
    figure_title = find_paragraph(doc, "DANH MỤC HÌNH VẼ")
    table_title = find_paragraph(doc, "DANH MỤC BẢNG BIỂU")
    abbreviation_title = find_paragraph(doc, "DANH MỤC KÝ HIỆU VÀ CHỮ VIẾT TẮT")

    remove_between(toc_title, figure_title)
    for anchor, level, text in TOC_ENTRIES:
        add_nav_line_before(
            doc,
            figure_title,
            text,
            anchor,
            str(page_map[anchor]),
            level=level,
            bold=level == 1,
        )

    remove_between(figure_title, table_title)
    for anchor, text in FIGURE_ENTRIES:
        add_nav_line_before(
            doc,
            table_title,
            text,
            anchor,
            str(page_map[anchor]),
            level=1,
            size=11.5,
        )

    remove_between(table_title, abbreviation_title)
    for anchor, text in TABLE_ENTRIES:
        add_nav_line_before(
            doc,
            abbreviation_title,
            text,
            anchor,
            str(page_map[anchor]),
            level=1,
            size=11.5,
        )


def main(draft_docx: Path, rendered_pdf: Path, output_docx: Path) -> None:
    page_count, page_map = build_page_map(rendered_pdf)
    doc = Document(draft_docx)
    rebuild_navigation(doc, page_map)
    replace_paragraph_text(
        find_paragraph_startswith(doc, "Báo cáo gồm 00 trang"),
        f"Báo cáo gồm {page_count} trang, 3 chương chính, 12 hình vẽ, 11 bảng biểu và 25 khối mã nguồn:",
    )
    doc.settings.update_fields_on_open = True
    output_docx.parent.mkdir(parents=True, exist_ok=True)
    doc.save(output_docx)

    print(f"pages={page_count}")
    for anchor, level, text in TOC_ENTRIES:
        print(f"TOC\t{page_map[anchor]}\t{text}")
    for anchor, text in FIGURE_ENTRIES:
        print(f"FIG\t{page_map[anchor]}\t{text}")
    for anchor, text in TABLE_ENTRIES:
        print(f"TABLE\t{page_map[anchor]}\t{text}")
    print(output_docx)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("Usage: finalize_navigation.py DRAFT.docx RENDERED.pdf OUTPUT.docx")
    main(Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]))
