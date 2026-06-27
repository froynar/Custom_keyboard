from __future__ import annotations

import argparse
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Inches, Mm, Pt, RGBColor
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "report_assets"
OUTPUT = ROOT / "output" / "Bao_cao_du_an_Custom_Keyboard_Builder_full_diagrams.docx"
COVER = ASSETS / "sample_cover-01.png"


FIGURES = {
    "fig_2_1": ("2.1", "ERD tổng quát của hệ thống Custom Keyboard Builder"),
    "fig_2_2": ("2.2", "ERD chi tiết theo mô hình dữ liệu kit-based và QC"),
    "fig_2_3": ("2.3", "Use case UC-01 Buyer tạo build từ kit và gửi request"),
    "fig_2_4": ("2.4", "Use case UC-02 Seller xử lý request build"),
    "fig_2_5": ("2.5", "Use case UC-03 Admin quản trị hệ thống"),
    "fig_2_6": ("2.6", "Use case UC-04 Device/QC Station kiểm tra keyboard"),
    "fig_2_7": ("2.7", "Sequence diagram SD-R01 Account and Role Routing"),
    "fig_2_8": ("2.8", "Sequence diagram SD-R02 Buyer tạo build và gửi request"),
    "fig_2_9": ("2.9", "Sequence diagram SD-R03 Seller xử lý request và QC summary"),
    "fig_2_10": ("2.10", "Sequence diagram SD-R04 Admin Management"),
    "fig_2_11": ("2.11", "Sequence diagram SD-R05 Seller Application Approval"),
    "fig_2_12": ("2.12", "Sequence diagram SD-R06 Chat Between Users"),
    "fig_2_13": ("2.13", "Activity diagram AD-01 Tài khoản"),
    "fig_2_14": ("2.14", "Activity diagram AD-02 Buyer tạo và gửi build"),
    "fig_2_15": ("2.15", "Activity diagram AD-03 Seller xử lý request"),
    "fig_2_16": ("2.16", "Activity diagram AD-04 Admin quản trị"),
    "fig_2_17": ("2.17", "Activity diagram AD-05 Chat Buyer-Seller và Seller-Admin"),
    "fig_2_18": ("2.18", "Activity diagram AD-06 Device/QC kiểm tra keyboard"),
    "fig_3_1": ("3.1", "Giao diện đăng nhập của ứng dụng"),
    "fig_3_2": ("3.2", "Giao diện dashboard Buyer và khu vực cấu hình build"),
}

TABLES = {
    "tbl_0_1": ("0.1", "Phân công nhiệm vụ nhóm"),
    "tbl_1_1": ("1.1", "Yêu cầu chức năng chính của hệ thống"),
    "tbl_1_2": ("1.2", "Yêu cầu phi chức năng và ràng buộc triển khai"),
    "tbl_2_1": ("2.1", "Công nghệ và thư viện sử dụng"),
    "tbl_2_2": ("2.2", "Nhóm bảng chính trong cơ sở dữ liệu"),
    "tbl_2_3": ("2.3", "Đối chiếu giữa diagram và thành phần hệ thống"),
    "tbl_3_1": ("3.1", "Đoạn mã giao diện đăng nhập LoginView.xaml"),
    "tbl_3_2": ("3.2", "Đoạn mã xử lý đăng nhập LoginViewModel.cs"),
    "tbl_3_3": ("3.3", "Đoạn mã kiểm tra cấu hình build BuildService.cs"),
    "tbl_3_4": ("3.4", "Kết quả kiểm thử tiêu biểu"),
}


def font(name: str, size: int):
    try:
        return ImageFont.truetype(name, size)
    except OSError:
        return ImageFont.load_default()


def make_placeholder(path: Path, title: str, subtitle: str, w: int = 1500, h: int = 820):
    img = Image.new("RGB", (w, h), "#B91C1C")
    draw = ImageDraw.Draw(img)
    border = 28
    draw.rectangle([border, border, w - border, h - border], outline="#FEE2E2", width=8)

    title_font = font("arialbd.ttf", 64)
    sub_font = font("arial.ttf", 34)
    note_font = font("arial.ttf", 28)
    lines = [title, subtitle, "THAY BANG ANH DIAGRAM THAT"]
    fonts = [title_font, sub_font, note_font]
    heights = []
    for line, fnt in zip(lines, fonts):
        bbox = draw.textbbox((0, 0), line, font=fnt)
        heights.append(bbox[3] - bbox[1])

    total_h = sum(heights) + 60
    y = (h - total_h) // 2
    for line, fnt, line_h in zip(lines, fonts, heights):
        bbox = draw.textbbox((0, 0), line, font=fnt)
        x = (w - (bbox[2] - bbox[0])) // 2
        draw.text((x, y), line, font=fnt, fill="#FFFFFF")
        y += line_h + 30

    img.save(path)


def make_ui_mock(path: Path, kind: str):
    w, h = 1500, 900
    img = Image.new("RGB", (w, h), "#F4F7FA")
    draw = ImageDraw.Draw(img)
    title_font = font("arialbd.ttf", 40)
    sidebar_title_font = font("arialbd.ttf", 32)
    text_font = font("arial.ttf", 26)
    small_font = font("arial.ttf", 20)
    accent = "#2563EB"
    ink = "#0F172A"
    muted = "#64748B"

    if kind == "login":
        draw.rounded_rectangle([260, 120, 850, 780], radius=18, fill="#FFFFFF", outline="#CBD5E1", width=3)
        draw.rounded_rectangle([320, 180, 375, 235], radius=10, fill=accent)
        draw.text((395, 185), "Custom Keyboard Builder", font=title_font, fill=ink)
        draw.text((320, 275), "Dang nhap", font=title_font, fill=ink)
        draw.text((320, 335), "Email hoac username", font=small_font, fill=muted)
        draw.rounded_rectangle([320, 365, 790, 425], radius=8, fill="#F8FAFC", outline="#CBD5E1", width=2)
        draw.text((320, 455), "Mat khau", font=small_font, fill=muted)
        draw.rounded_rectangle([320, 485, 790, 545], radius=8, fill="#F8FAFC", outline="#CBD5E1", width=2)
        draw.rounded_rectangle([320, 590, 790, 655], radius=8, fill=accent)
        draw.text((505, 607), "Dang nhap", font=text_font, fill="#FFFFFF")
        draw.rounded_rectangle([890, 120, 1240, 430], radius=18, fill="#FFFFFF", outline="#CBD5E1", width=3)
        draw.text((930, 170), "Quick login", font=title_font, fill=ink)
        for i, label in enumerate(["Buyer", "Seller", "Admin"]):
            y = 245 + i * 58
            draw.rounded_rectangle([930, y, 1200, y + 42], radius=8, fill="#EFF6FF", outline="#BFDBFE", width=2)
            draw.text((960, y + 8), label, font=small_font, fill=accent)
    else:
        draw.rectangle([0, 0, 310, h], fill="#0F2537")
        draw.text((40, 64), "Custom KB", font=sidebar_title_font, fill="#FFFFFF")
        for i, label in enumerate(["Trang chu", "Build cua toi", "Tao build moi", "Request da gui", "Chat"]):
            y = 150 + i * 70
            fill = accent if i == 2 else "#1E3A4C"
            draw.rounded_rectangle([32, y, 278, y + 48], radius=10, fill=fill)
            draw.text((55, y + 11), label, font=small_font, fill="#FFFFFF")
        draw.text((370, 70), "Buyer Dashboard", font=title_font, fill=ink)
        draw.text((370, 122), "Cau hinh build ban phim tu kit", font=text_font, fill=muted)
        draw.rounded_rectangle([370, 180, 1070, 780], radius=16, fill="#FFFFFF", outline="#CBD5E1", width=3)
        draw.text((410, 220), "Configurator", font=title_font, fill=ink)
        labels = ["Ten build", "Keyboard kit", "Switch", "Keycap", "Stabilizer"]
        for i, label in enumerate(labels):
            y = 290 + i * 82
            draw.text((410, y), label, font=small_font, fill=muted)
            draw.rounded_rectangle([410, y + 28, 830, y + 78], radius=8, fill="#F8FAFC", outline="#CBD5E1", width=2)
        draw.rounded_rectangle([900, 300, 1020, 360], radius=10, fill=accent)
        draw.text((927, 317), "Luu", font=text_font, fill="#FFFFFF")
        draw.rounded_rectangle([1120, 180, 1420, 520], radius=16, fill="#FFFFFF", outline="#CBD5E1", width=3)
        draw.text((1160, 220), "Tong gia", font=text_font, fill=ink)
        draw.text((1160, 280), "368.60 USD", font=title_font, fill=accent)
        draw.text((1160, 355), "Seller verified", font=small_font, fill=muted)
        draw.rounded_rectangle([1160, 400, 1380, 462], radius=10, fill=accent)
        draw.text((1212, 417), "Gui request", font=text_font, fill="#FFFFFF")

    img.save(path)


def create_assets():
    ASSETS.mkdir(parents=True, exist_ok=True)
    placeholders = [
        ("diagram_erd_overview.png", "ERD TONG QUAT", "Account - Catalog - Build - Request - Chat - QC"),
        ("diagram_erd_detail.png", "ERD CHI TIET", "21 bang / 33 quan he / SQL Server"),
        ("diagram_usecase_uc01.png", "UC-01 USE CASE", "Buyer tao build tu kit va gui request"),
        ("diagram_usecase_uc02.png", "UC-02 USE CASE", "Seller xu ly request build"),
        ("diagram_usecase_uc03.png", "UC-03 USE CASE", "Admin quan tri he thong"),
        ("diagram_usecase_uc04.png", "UC-04 USE CASE", "Device QC station kiem tra keyboard"),
        ("diagram_sequence_sdr01.png", "SD-R01 SEQUENCE", "Account and role routing"),
        ("diagram_sequence_sdr02.png", "SD-R02 SEQUENCE", "Buyer create build and send request"),
        ("diagram_sequence_sdr03.png", "SD-R03 SEQUENCE", "Seller process request and QC summary"),
        ("diagram_sequence_sdr04.png", "SD-R04 SEQUENCE", "Admin management"),
        ("diagram_sequence_sdr05.png", "SD-R05 SEQUENCE", "Seller application approval"),
        ("diagram_sequence_sdr06.png", "SD-R06 SEQUENCE", "Chat between users"),
        ("diagram_activity_ad01.png", "AD-01 ACTIVITY", "Tai khoan"),
        ("diagram_activity_ad02.png", "AD-02 ACTIVITY", "Buyer tao va gui build"),
        ("diagram_activity_ad03.png", "AD-03 ACTIVITY", "Seller xu ly request"),
        ("diagram_activity_ad04.png", "AD-04 ACTIVITY", "Admin quan tri"),
        ("diagram_activity_ad05.png", "AD-05 ACTIVITY", "Chat Buyer-Seller va Seller-Admin"),
        ("diagram_activity_ad06.png", "AD-06 ACTIVITY", "Device QC kiem tra keyboard"),
    ]
    for filename, title, subtitle in placeholders:
        make_placeholder(ASSETS / filename, title, subtitle)

    make_ui_mock(ASSETS / "ui_login_mock.png", "login")
    make_ui_mock(ASSETS / "ui_buyer_dashboard_mock.png", "buyer")


def set_cell_shading(cell, fill: str):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_margins(cell, top=80, start=120, bottom=80, end=120):
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for m, v in [("top", top), ("start", start), ("bottom", bottom), ("end", end)]:
        node = tc_mar.find(qn(f"w:{m}"))
        if node is None:
            node = OxmlElement(f"w:{m}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(v))
        node.set(qn("w:type"), "dxa")


def set_cell_width(cell, width_dxa: int):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_w = tc_pr.find(qn("w:tcW"))
    if tc_w is None:
        tc_w = OxmlElement("w:tcW")
        tc_pr.append(tc_w)
    tc_w.set(qn("w:w"), str(width_dxa))
    tc_w.set(qn("w:type"), "dxa")


def set_table_borders(table, color="AAB7C4", size="6"):
    tbl_pr = table._tbl.tblPr
    borders = tbl_pr.first_child_found_in("w:tblBorders")
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ["top", "left", "bottom", "right", "insideH", "insideV"]:
        tag = f"w:{edge}"
        element = borders.find(qn(tag))
        if element is None:
            element = OxmlElement(tag)
            borders.append(element)
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), size)
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)


def set_font(run, name="Times New Roman", size: float | None = None, bold=None, color: str | None = None):
    run.font.name = name
    run._element.rPr.rFonts.set(qn("w:ascii"), name)
    run._element.rPr.rFonts.set(qn("w:hAnsi"), name)
    run._element.rPr.rFonts.set(qn("w:cs"), name)
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if color is not None:
        run.font.color.rgb = RGBColor.from_string(color)


def style_paragraph(paragraph, align=None, before=0, after=6, line=1.25, keep_next=False):
    paragraph.paragraph_format.space_before = Pt(before)
    paragraph.paragraph_format.space_after = Pt(after)
    paragraph.paragraph_format.line_spacing = line
    paragraph.paragraph_format.keep_with_next = keep_next
    if align is not None:
        paragraph.alignment = align


def add_bookmark(paragraph, name: str, bookmark_id: int):
    start = OxmlElement("w:bookmarkStart")
    start.set(qn("w:id"), str(bookmark_id))
    start.set(qn("w:name"), name)
    end = OxmlElement("w:bookmarkEnd")
    end.set(qn("w:id"), str(bookmark_id))
    # Empty bookmark anchors are enough for internal hyperlinks and keep the
    # paragraph property order valid for Word.
    paragraph._p.append(start)
    paragraph._p.append(end)


def add_internal_hyperlink(paragraph, text: str, anchor: str):
    hyperlink = OxmlElement("w:hyperlink")
    hyperlink.set(qn("w:anchor"), anchor)
    new_run = OxmlElement("w:r")
    r_pr = OxmlElement("w:rPr")
    color = OxmlElement("w:color")
    color.set(qn("w:val"), "0563C1")
    underline = OxmlElement("w:u")
    underline.set(qn("w:val"), "single")
    r_pr.append(color)
    r_pr.append(underline)
    new_run.append(r_pr)
    text_element = OxmlElement("w:t")
    text_element.text = text
    new_run.append(text_element)
    hyperlink.append(new_run)
    paragraph._p.append(hyperlink)


def add_ref_sentence(doc: Document, before: str, ref_text: str, anchor: str, after: str = ""):
    p = doc.add_paragraph()
    style_paragraph(p, after=6, keep_next=True)
    run = p.add_run(before)
    set_font(run)
    add_internal_hyperlink(p, ref_text, anchor)
    if after:
        run = p.add_run(after)
        set_font(run)
    return p


def add_body_paragraph(doc: Document, text: str, keep_next: bool = False):
    p = doc.add_paragraph()
    style_paragraph(p, align=WD_ALIGN_PARAGRAPH.JUSTIFY, after=6, line=1.25, keep_next=keep_next)
    run = p.add_run(text)
    set_font(run, size=13)
    return p


def add_heading(doc: Document, text: str, level: int):
    p = doc.add_paragraph()
    before = 14 if level == 1 else 8
    after = 8 if level == 1 else 5
    style_paragraph(p, before=before, after=after, line=1.15, keep_next=True)
    if level == 1:
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = p.add_run(text.upper())
        set_font(run, size=16, bold=True, color="1F4D78")
    elif level == 2:
        run = p.add_run(text)
        set_font(run, size=14, bold=True, color="1F4D78")
    else:
        run = p.add_run(text)
        set_font(run, size=13, bold=True, color="1F4D78")
    return p


def add_table_caption(doc: Document, key: str, bookmark_id: int):
    num, text = TABLES[key]
    p = doc.add_paragraph()
    style_paragraph(p, align=WD_ALIGN_PARAGRAPH.CENTER, before=4, after=4, line=1.15, keep_next=True)
    run = p.add_run(f"Bảng {num}. {text}")
    set_font(run, size=12, bold=True)
    add_bookmark(p, key, bookmark_id)
    return p


def add_figure_caption(doc: Document, key: str, bookmark_id: int):
    num, text = FIGURES[key]
    p = doc.add_paragraph()
    style_paragraph(p, align=WD_ALIGN_PARAGRAPH.CENTER, before=3, after=8, line=1.15)
    run = p.add_run(f"Hình {num}. {text}")
    set_font(run, size=12, bold=True)
    add_bookmark(p, key, bookmark_id)
    return p


def add_standard_table(doc: Document, headers: list[str], rows: list[list[str]], widths: list[int]):
    table = doc.add_table(rows=1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_table_borders(table)
    for idx, header in enumerate(headers):
        cell = table.rows[0].cells[idx]
        set_cell_shading(cell, "E8EEF5")
        set_cell_width(cell, widths[idx])
        set_cell_margins(cell)
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        p = cell.paragraphs[0]
        style_paragraph(p, after=0, line=1.1)
        run = p.add_run(header)
        set_font(run, size=11, bold=True)
    for row in rows:
        cells = table.add_row().cells
        for idx, value in enumerate(row):
            cell = cells[idx]
            set_cell_width(cell, widths[idx])
            set_cell_margins(cell)
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            p = cell.paragraphs[0]
            style_paragraph(p, after=0, line=1.15)
            run = p.add_run(value)
            set_font(run, size=11)
    doc.add_paragraph()
    return table


def add_code_table(doc: Document, title: str, code: str):
    table = doc.add_table(rows=2, cols=1)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_table_borders(table, color="B9C2CC")
    title_cell = table.rows[0].cells[0]
    code_cell = table.rows[1].cells[0]
    set_cell_width(title_cell, 9000)
    set_cell_width(code_cell, 9000)
    set_cell_shading(title_cell, "E5E7EB")
    set_cell_shading(code_cell, "F8FAFC")
    for cell in [title_cell, code_cell]:
        set_cell_margins(cell, top=80, bottom=80, start=120, end=120)
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER

    p = title_cell.paragraphs[0]
    style_paragraph(p, after=0, line=1.0)
    run = p.add_run(title)
    set_font(run, size=10.5, bold=True)

    p = code_cell.paragraphs[0]
    style_paragraph(p, after=0, line=1.0)
    for idx, line in enumerate(code.strip("\n").splitlines()):
        if idx:
            p.add_run().add_break()
        run = p.add_run(line.rstrip())
        set_font(run, name="Courier New", size=8.5)
    doc.add_paragraph()
    return table


def add_figure(doc: Document, image: Path, key: str, bookmark_id: int):
    p = doc.add_paragraph()
    style_paragraph(p, align=WD_ALIGN_PARAGRAPH.CENTER, after=0, line=1.0, keep_next=True)
    p.add_run().add_picture(str(image), width=Inches(6.25))
    add_figure_caption(doc, key, bookmark_id)


def add_footer_page_number(section):
    section.footer.is_linked_to_previous = False
    p = section.footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    style_paragraph(p, after=0, line=1.0)
    run = p.add_run("Custom Keyboard Builder")
    set_font(run, size=10)


def configure_styles(doc: Document):
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = "Times New Roman"
    normal.font.size = Pt(13)
    normal._element.rPr.rFonts.set(qn("w:ascii"), "Times New Roman")
    normal._element.rPr.rFonts.set(qn("w:hAnsi"), "Times New Roman")
    normal._element.rPr.rFonts.set(qn("w:cs"), "Times New Roman")


def build_document(page_count: str | None):
    create_assets()
    doc = Document()
    configure_styles(doc)

    # Section 1: exact cover page image from the sample PDF.
    cover_section = doc.sections[0]
    cover_section.page_width = Mm(210)
    cover_section.page_height = Mm(297)
    for side in ["top_margin", "bottom_margin", "left_margin", "right_margin"]:
        setattr(cover_section, side, Mm(0))
    cover_paragraph = doc.add_paragraph()
    style_paragraph(cover_paragraph, align=WD_ALIGN_PARAGRAPH.CENTER, before=0, after=0, line=1.0)
    if COVER.exists():
        cover_paragraph.add_run().add_picture(str(COVER), width=Mm(209))
    else:
        run = cover_paragraph.add_run("Trang bìa mẫu chưa được render.")
        set_font(run, size=16, bold=True)

    section = doc.add_section(WD_SECTION.NEW_PAGE)
    section.page_width = Mm(210)
    section.page_height = Mm(297)
    section.top_margin = Cm(2)
    section.bottom_margin = Cm(2)
    section.left_margin = Cm(3)
    section.right_margin = Cm(2)
    section.header_distance = Cm(1.2)
    section.footer_distance = Cm(1.2)
    add_footer_page_number(section)

    bookmark_id = 1

    add_heading(doc, "Mở đầu", 1)
    add_heading(doc, "Giới thiệu và lý do chọn đề tài", 2)
    add_body_paragraph(
        doc,
        "Đề tài Custom Keyboard Builder được xây dựng nhằm hỗ trợ quy trình cấu hình, đặt yêu cầu và xử lý build bàn phím cơ tùy chỉnh. "
        "Trong thực tế, một bộ bàn phím custom thường gồm nhiều thành phần như keyboard kit, switch, keycap, stabilizer, phụ kiện và các tùy chọn mod, vì vậy việc quản lý bằng ghi chú rời rạc dễ gây sai lệch cấu hình, giá và trạng thái xử lý."
    )
    add_body_paragraph(
        doc,
        "Việc lựa chọn đề tài xuất phát từ nhu cầu số hóa quy trình giữa Buyer, Seller và Admin trong một ứng dụng desktop thống nhất. "
        "Hệ thống giúp Buyer tạo build theo mô hình kit-based, gửi request cho Seller, theo dõi trạng thái, trao đổi qua chat và xem kết quả kiểm tra QC mô phỏng sau khi build được xử lý."
    )

    add_heading(doc, "Mục đích và nhiệm vụ của đề tài", 2)
    add_body_paragraph(
        doc,
        "Mục đích của đề tài là xây dựng ứng dụng WPF có khả năng quản lý cấu hình bàn phím custom, lưu dữ liệu bằng SQL Server, kiểm tra tính tương thích cơ bản của linh kiện và hỗ trợ quy trình xử lý request theo vai trò. "
        "Các nhiệm vụ chính gồm thiết kế cơ sở dữ liệu, xây dựng giao diện đăng nhập/dashboard, triển khai service nghiệp vụ, lưu trữ request/chat/QC, và kiểm thử các luồng cốt lõi."
    )

    add_table_caption(doc, "tbl_0_1", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Thành viên", "Nhiệm vụ được phân công", "Kết quả cần hoàn thành"],
        [
            ["Thành viên 1 (cập nhật)", "Phân tích yêu cầu, use case và activity diagram", "Đặc tả luồng Buyer/Seller/Admin và bảng đối chiếu FHD"],
            ["Thành viên 2 (cập nhật)", "Thiết kế ERD tổng quát, ERD chi tiết và SQL Server schema", "Bộ bảng dữ liệu kit-based, request, chat và QC"],
            ["Thành viên 3 (cập nhật)", "Triển khai WPF View/ViewModel và điều hướng theo role", "Màn hình đăng nhập, dashboard Buyer, Seller, Admin"],
            ["Thành viên 4 (cập nhật)", "Triển khai service, repository và validation", "BuildService, RequestService, AccountService, ChatService"],
            ["Thành viên 5 (cập nhật)", "Kiểm thử, seed data, tài liệu bàn giao và báo cáo", "Test matrix, runner Phase6Verification, hướng dẫn chạy"],
        ],
        [2100, 3500, 3400],
    )
    add_ref_sentence(
        doc,
        "Phần phân công ở ",
        "Bảng 0.1",
        "tbl_0_1",
        " có thể được cập nhật lại theo thông tin thành viên thật trước khi nộp báo cáo.",
    )

    add_heading(doc, "Nội dung sơ lược của báo cáo", 2)
    figure_count = len(FIGURES)
    table_count = len(TABLES)
    if page_count:
        overview = f"Bản Word hiện tại gồm {page_count} trang, 3 chương chính, {figure_count} hình vẽ và {table_count} bảng biểu."
    else:
        overview = f"Bản Word hiện tại gồm 3 chương chính, {figure_count} hình vẽ và {table_count} bảng biểu; số trang được cập nhật sau bước render cuối."
    add_body_paragraph(
        doc,
        overview
        + " Chương 1 trình bày bối cảnh, mục tiêu, phạm vi và yêu cầu hệ thống. "
        "Chương 2 trình bày phân tích thiết kế hệ thống, công nghệ sử dụng và 5 nhóm diagram: ERD tổng quát, ERD chi tiết, use case, sequence diagram và activity diagram. "
        "Chương 3 trình bày triển khai giao diện, đoạn mã tiêu biểu và kết quả kiểm thử."
    )

    add_heading(doc, "Chương 1. Tổng quan hệ thống Custom Keyboard Builder", 1)
    add_body_paragraph(
        doc,
        "Chương này trình bày bối cảnh hình thành đề tài, mục tiêu của hệ thống và các yêu cầu chức năng cần đáp ứng. "
        "Các nội dung sau được dùng làm cơ sở cho phần phân tích thiết kế ở chương tiếp theo."
    )
    add_heading(doc, "1.1. Bối cảnh và vấn đề cần giải quyết", 2)
    add_body_paragraph(
        doc,
        "Thị trường bàn phím custom có nhiều lựa chọn linh kiện và mỗi lựa chọn thường phụ thuộc vào kit nền tảng. "
        "Nếu Buyer tự chọn từng linh kiện mà không có kiểm tra tương thích, các lỗi như sai công nghệ switch, thiếu số lượng switch hoặc chọn keycap không phù hợp form factor có thể xảy ra."
    )
    add_body_paragraph(
        doc,
        "Hệ thống được định hướng theo mô hình kit-based: keyboard kit đã bao gồm case, PCB, plate và các phần đi kèm; Buyer chỉ cần chọn kit làm nền, sau đó bổ sung switch, keycap, stabilizer, accessory và mod note. "
        "Cách tiếp cận này giúp giảm độ phức tạp so với mô hình tách case/PCB/plate thành các bảng độc lập."
    )
    add_heading(doc, "1.2. Mục tiêu và phạm vi hệ thống", 2)
    add_body_paragraph(
        doc,
        "Hệ thống tập trung vào ba nhóm người dùng chính: Buyer tạo build và gửi request; Seller nhận, xử lý, cập nhật trạng thái và chạy QC mô phỏng; Admin quản lý user, seller, catalog và audit log. "
        "Cơ sở dữ liệu SQL Server được sử dụng làm nguồn dữ liệu chính, còn MQTT chỉ đóng vai trò lớp realtime tùy chọn."
    )
    add_body_paragraph(
        doc,
        "Phạm vi không bao gồm quản lý tồn kho seller riêng, không chọn case/PCB/plate rời, không triển khai phần cứng ESP32 thật và không triển khai SignalR chat realtime trong phiên bản lõi. "
        "Lớp Device/QC hiện được mô phỏng bằng DeviceSimulator, MQTT telemetry và fallback in-process."
    )

    add_heading(doc, "1.3. Yêu cầu hệ thống", 2)
    add_ref_sentence(doc, "Các nhóm yêu cầu chức năng chính được liệt kê tại ", "Bảng 1.1", "tbl_1_1", ".")
    add_table_caption(doc, "tbl_1_1", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Nhóm chức năng", "Mô tả yêu cầu", "Vai trò liên quan"],
        [
            ["Tài khoản", "Đăng ký Buyer, đăng nhập bằng username/email, đăng xuất, xem profile và chặn user bị khóa.", "Guest, Buyer, Seller, Admin"],
            ["Buyer build/request", "Tạo build từ keyboard kit, thêm linh kiện, xem cảnh báo tương thích, lưu build, chọn seller và gửi request.", "Buyer"],
            ["Seller xử lý request", "Xem request được gán, cập nhật Pending/Accepted/In_progress/Completed/Cancelled và xem payload snapshot.", "Seller"],
            ["Admin quản trị", "Quản lý user, seller profile, seller application, catalog linh kiện và audit log.", "Admin"],
            ["Chat và QC", "Lưu hội thoại Buyer-Seller/Admin-Seller; mô phỏng QC từng phím, tổng hợp pass/warning/fail.", "Buyer, Seller, Admin, Device/QC"],
        ],
        [1900, 5200, 1900],
    )

    add_ref_sentence(doc, "Các ràng buộc triển khai và chất lượng được tổng hợp tại ", "Bảng 1.2", "tbl_1_2", ".")
    add_table_caption(doc, "tbl_1_2", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Yêu cầu", "Nội dung áp dụng"],
        [
            ["Tính đúng đắn dữ liệu", "Build item phải có đúng một khóa ngoại sản phẩm; seller nhận request phải active và verified; request status tuân thủ state machine."],
            ["Bảo mật tài khoản", "Mật khẩu không lưu plain text; seed data sử dụng hash PBKDF2-SHA256 cho mật khẩu Password123."],
            ["Khả năng vận hành", "Ứng dụng vẫn chạy DB-only khi broker MQTT không khả dụng; realtime là best-effort."],
            ["Khả năng kiểm thử", "Runner Phase6Verification kiểm tra unit-style, SQL integration, invariant database và một số luồng UI smoke."],
        ],
        [2400, 6600],
    )

    add_heading(doc, "1.4. Kết luận chương", 2)
    add_body_paragraph(
        doc,
        "Chương 1 đã xác định được bối cảnh, mục tiêu, phạm vi và yêu cầu chính của hệ thống Custom Keyboard Builder. "
        "Từ các yêu cầu này, chương 2 tiếp tục phân tích thiết kế hệ thống, lựa chọn công nghệ, mô hình dữ liệu và các diagram phục vụ triển khai."
    )

    doc.add_page_break()
    add_heading(doc, "Chương 2. Phân tích thiết kế hệ thống", 1)
    add_body_paragraph(
        doc,
        "Chương này trình bày kiến trúc tổng thể, công nghệ sử dụng, thiết kế dữ liệu và các sơ đồ phân tích chính của hệ thống. "
        "Các diagram được đặt bằng ảnh màu đỏ để thuận tiện thay thế bằng ảnh diagram thật khi hoàn thiện bản nộp."
    )

    add_heading(doc, "2.1. Công nghệ và kiến trúc triển khai", 2)
    add_body_paragraph(
        doc,
        "Ứng dụng được triển khai theo mô hình WPF desktop, tổ chức theo MVVM, tách View, ViewModel, Service và Repository. "
        "SQL Server giữ vai trò source of truth; các service nghiệp vụ thực hiện validation trước khi dữ liệu được ghi xuống repository."
    )
    add_ref_sentence(doc, "Thông tin công nghệ sử dụng được trình bày tại ", "Bảng 2.1", "tbl_2_1", ".")
    add_table_caption(doc, "tbl_2_1", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Thành phần", "Công nghệ sử dụng", "Vai trò trong hệ thống"],
        [
            ["Ứng dụng desktop", "WPF trên .NET 10.0 Windows", "Xây dựng giao diện Login, Buyer/Seller/Admin Dashboard và Chat"],
            ["Cơ sở dữ liệu", "SQL Server instance KHOADZS1VN\\SQLEXPRESS, database CustomKeyboard_Refactor", "Lưu user, catalog, build, request, chat, audit log và QC"],
            ["Data access", "Microsoft.Data.SqlClient 6.1.1, repository pattern", "Thực hiện truy vấn SQL Server và tách lớp lưu trữ khỏi service"],
            ["Realtime tùy chọn", "MQTTnet 4.3.7, broker localhost:1883 nếu bật", "Gửi thông báo request/status và telemetry QC theo cơ chế best-effort"],
            ["Biểu đồ thống kê", "LiveChartsCore.SkiaSharpView.WPF 2.0.4", "Hỗ trợ các thống kê dashboard khi cần trực quan hóa dữ liệu"],
            ["Bảo mật mật khẩu", "PBKDF2-SHA256", "Hash mật khẩu người dùng, không lưu mật khẩu rõ"],
        ],
        [1900, 3300, 3800],
    )
    add_body_paragraph(
        doc,
        "Không sử dụng ESP32 hoặc phần cứng nhúng thật trong phạm vi phiên bản hiện tại. "
        "Phần kiểm tra keyboard được mô phỏng ở tầng Device/QC để lưu kết quả từng phím và tổng hợp session QC."
    )

    add_heading(doc, "2.2. Thiết kế cơ sở dữ liệu và ERD", 2)
    add_ref_sentence(doc, "ERD tổng quát được mô tả trong ", "Hình 2.1", "fig_2_1", ", thể hiện các vùng dữ liệu account, catalog, build, request, chat và QC.")
    add_figure(doc, ASSETS / "diagram_erd_overview.png", "fig_2_1", bookmark_id)
    bookmark_id += 1
    add_ref_sentence(doc, "ERD chi tiết ở ", "Hình 2.2", "fig_2_2", " phản ánh schema hiện tại gồm 21 bảng và 33 quan hệ theo file DBML/schema SQL.")
    add_figure(doc, ASSETS / "diagram_erd_detail.png", "fig_2_2", bookmark_id)
    bookmark_id += 1

    doc.add_page_break()
    add_ref_sentence(doc, "Các nhóm bảng chính được tóm tắt tại ", "Bảng 2.2", "tbl_2_2", ".")
    add_table_caption(doc, "tbl_2_2", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Nhóm dữ liệu", "Bảng tiêu biểu", "Ý nghĩa thiết kế"],
        [
            ["Account và phân quyền", "roles, users, seller_profiles, seller_applications", "Quản lý Buyer/Seller/Admin, seller verified và đơn xin trở thành Seller"],
            ["Catalog linh kiện", "brands, layouts, keyboard_kits, switches, keycap_sets, stabilizers, accessories", "Lưu dữ liệu sản phẩm, availability và thông tin tương thích cơ bản"],
            ["Build và request", "builds, build_items, build_mods, build_requests", "Lưu cấu hình build, snapshot giá, mod note và request gửi Seller"],
            ["Device/QC", "devices, device_test_sessions, device_key_test_results", "Mô phỏng kiểm tra từng phím, latency, noise và kết quả tổng hợp"],
            ["Chat và audit", "chat_conversations, chat_messages, audit_log", "Lưu trao đổi theo role và lịch sử thao tác quan trọng"],
        ],
        [2200, 3500, 3300],
    )

    add_heading(doc, "2.3. Use case diagram", 2)
    add_body_paragraph(
        doc,
        "Use case được chia theo vai trò để tránh trộn lẫn trách nhiệm. "
        "Buyer tập trung vào tạo build và gửi request, Seller tập trung xử lý request và QC, Admin tập trung quản trị hệ thống, Device/QC đóng vai trò mô phỏng dữ liệu kiểm tra. "
        "Mỗi use case chính được đặt thành một hình riêng để thuận tiện thay thế bằng diagram thật."
    )
    use_case_diagrams = [
        ("fig_2_3", "diagram_usecase_uc01.png", "Use case UC-01 mô tả luồng Buyer tạo build từ kit, lưu build, chọn seller, gửi request, theo dõi trạng thái và xem QC summary."),
        ("fig_2_4", "diagram_usecase_uc02.png", "Use case UC-02 mô tả luồng Seller xem request, cập nhật trạng thái, chạy QC và trao đổi với Buyer/Admin."),
        ("fig_2_5", "diagram_usecase_uc03.png", "Use case UC-03 mô tả nhóm chức năng Admin quản lý user, seller profile, catalog, audit log và duyệt đơn seller."),
        ("fig_2_6", "diagram_usecase_uc04.png", "Use case UC-04 mô tả Device/QC Station mô phỏng kiểm tra tín hiệu phím, latency, noise và tổng hợp kết quả."),
    ]
    for key, filename, description in use_case_diagrams:
        figure_number = FIGURES[key][0]
        add_ref_sentence(doc, description + " Vị trí thay ảnh diagram thật nằm tại ", f"Hình {figure_number}", key, ".")
        add_figure(doc, ASSETS / filename, key, bookmark_id)
        bookmark_id += 1

    add_heading(doc, "2.4. Sequence diagram", 2)
    add_body_paragraph(
        doc,
        "Sequence diagram được dùng để mô tả thứ tự tương tác giữa người dùng, UI, service và SQL Server. "
        "Bộ tài liệu có 6 sequence diagram lõi, phản ánh các luồng account, build request, seller processing, admin management, seller application và chat."
    )
    sequence_diagrams = [
        ("fig_2_7", "diagram_sequence_sdr01.png", "SD-R01 mô tả đăng ký/đăng nhập và điều hướng dashboard theo role."),
        ("fig_2_8", "diagram_sequence_sdr02.png", "SD-R02 mô tả Buyer mở configurator, lấy catalog, lưu build và gửi request cho seller."),
        ("fig_2_9", "diagram_sequence_sdr03.png", "SD-R03 mô tả Seller nhận request, cập nhật trạng thái và xem QC summary."),
        ("fig_2_10", "diagram_sequence_sdr04.png", "SD-R04 mô tả Admin mở dashboard, đọc dữ liệu quản trị và lưu thay đổi catalog/audit."),
        ("fig_2_11", "diagram_sequence_sdr05.png", "SD-R05 mô tả Buyer nộp đơn seller và Admin duyệt đơn."),
        ("fig_2_12", "diagram_sequence_sdr06.png", "SD-R06 mô tả tạo hội thoại và gửi/đọc tin nhắn giữa các user hợp lệ."),
    ]
    for key, filename, description in sequence_diagrams:
        figure_number = FIGURES[key][0]
        add_ref_sentence(doc, description + " Placeholder tương ứng được đặt tại ", f"Hình {figure_number}", key, ".")
        add_figure(doc, ASSETS / filename, key, bookmark_id)
        bookmark_id += 1

    add_heading(doc, "2.5. Activity diagram", 2)
    add_body_paragraph(
        doc,
        "Activity diagram được dùng để mô tả các quyết định nghiệp vụ, nhánh xử lý và thao tác nhìn thấy trên giao diện. "
        "Bộ activity diagram hiện có 6 sơ đồ, bao phủ tài khoản, buyer, seller, admin, chat và device/QC."
    )
    activity_diagrams = [
        ("fig_2_13", "diagram_activity_ad01.png", "AD-01 mô tả hoạt động đăng ký, đăng nhập, xem profile và đăng xuất."),
        ("fig_2_14", "diagram_activity_ad02.png", "AD-02 mô tả Buyer chọn kit, thêm linh kiện, validate, lưu build, gửi request và nộp đơn seller."),
        ("fig_2_15", "diagram_activity_ad03.png", "AD-03 mô tả Seller xem request, cập nhật trạng thái, chạy QC và hoàn thành request."),
        ("fig_2_16", "diagram_activity_ad04.png", "AD-04 mô tả Admin quản lý user, seller, catalog, audit log và duyệt đơn seller."),
        ("fig_2_17", "diagram_activity_ad05.png", "AD-05 mô tả chat Buyer-Seller và Seller-Admin/Admin-Seller theo quyền."),
        ("fig_2_18", "diagram_activity_ad06.png", "AD-06 mô tả Device/QC tạo session, nhận telemetry, lưu kết quả từng phím và tổng hợp QC."),
    ]
    for key, filename, description in activity_diagrams:
        figure_number = FIGURES[key][0]
        add_ref_sentence(doc, description + " Placeholder tương ứng được đặt tại ", f"Hình {figure_number}", key, ".")
        add_figure(doc, ASSETS / filename, key, bookmark_id)
        bookmark_id += 1

    add_ref_sentence(doc, "Mối liên hệ giữa các diagram và thành phần triển khai được đối chiếu trong ", "Bảng 2.3", "tbl_2_3", ".")
    add_table_caption(doc, "tbl_2_3", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Loại diagram", "Nguồn tham khảo trong Documents_Refactor", "Phạm vi phản ánh"],
        [
            ["ERD tổng quát", "Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml", "Nhóm entity và quan hệ chính giữa account, catalog, build, request, chat, QC"],
            ["ERD chi tiết", "CreateSchema_Refactor.sql và DBML", "Cột, khóa chính, khóa ngoại, constraint và index SQL Server"],
            ["Use case", "Custom_Keyboard_Use_Cases_Refactor.md", "4 sơ đồ UC-01 đến UC-04 theo Buyer, Seller, Admin và Device/QC"],
            ["Sequence", "Custom_Keyboard_Sequence_Diagrams_6_Core.md", "6 sơ đồ SD-R01 đến SD-R06 cho các luồng nghiệp vụ lõi"],
            ["Activity", "Custom_Keyboard_Activity_Diagrams_Refactor.md", "6 sơ đồ AD-01 đến AD-06 cho nhánh thao tác và trạng thái xử lý"],
        ],
        [2100, 3900, 3000],
    )

    add_heading(doc, "2.6. Kết luận chương", 2)
    add_body_paragraph(
        doc,
        "Chương 2 đã trình bày kiến trúc WPF - Service - Repository - SQL Server, công nghệ sử dụng và các diagram phân tích thiết kế chính. "
        "Các thiết kế này là cơ sở để chương 3 mô tả phần triển khai giao diện, logic nghiệp vụ và kiểm thử."
    )

    add_heading(doc, "Chương 3. Triển khai và kiểm thử", 1)
    add_body_paragraph(
        doc,
        "Chương này trình bày một số giao diện tiêu biểu, các đoạn mã triển khai quan trọng và kết quả kiểm thử của hệ thống. "
        "Các đoạn code được đưa vào báo cáo dưới dạng text trong bảng, không sử dụng ảnh chụp code."
    )

    add_heading(doc, "3.1. Triển khai giao diện", 2)
    add_ref_sentence(doc, "Giao diện đăng nhập được minh họa tại ", "Hình 3.1", "fig_3_1", ", gồm form nhập tài khoản/mật khẩu, nút đăng nhập và cụm quick login cho dữ liệu seed.")
    add_figure(doc, ASSETS / "ui_login_mock.png", "fig_3_1", bookmark_id)
    bookmark_id += 1
    add_ref_sentence(doc, "Dashboard Buyer tại ", "Hình 3.2", "fig_3_2", " thể hiện sidebar, vùng cấu hình build, tổng giá và nút gửi request.")
    add_figure(doc, ASSETS / "ui_buyer_dashboard_mock.png", "fig_3_2", bookmark_id)
    bookmark_id += 1

    add_ref_sentence(doc, "Đoạn XAML tiêu biểu cho màn hình đăng nhập được trình bày trong ", "Bảng 3.1", "tbl_3_1", ".")
    add_table_caption(doc, "tbl_3_1", bookmark_id)
    bookmark_id += 1
    add_code_table(
        doc,
        "Views/LoginView.xaml - bố cục form đăng nhập",
        """
<TextBlock Text="{loc:Tr Login_EmailOrUsername}"
           FontWeight="SemiBold"
           Foreground="{StaticResource TextPrimaryBrush}"/>
<TextBox AutomationProperties.AutomationId="LoginEmailOrUsernameTextBox"
         Text="{Binding EmailOrUsername, UpdateSourceTrigger=PropertyChanged}"
         Margin="0,6,0,14"/>

<PasswordBox AutomationProperties.AutomationId="LoginPasswordBox"
             behaviors:PasswordBoxBinding.Attach="True"
             behaviors:PasswordBoxBinding.BoundPassword="{Binding Password, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
             Margin="0,6,0,14"/>

<Button AutomationProperties.AutomationId="LoginSubmitButton"
        Content="{loc:Tr Login_SignIn}"
        Command="{Binding LoginCommand}"
        Style="{StaticResource PrimaryButton}"/>
        """,
    )

    doc.add_page_break()
    add_ref_sentence(doc, "Phần xử lý lệnh đăng nhập trong ViewModel được thể hiện ở ", "Bảng 3.2", "tbl_3_2", ".")
    add_table_caption(doc, "tbl_3_2", bookmark_id)
    bookmark_id += 1
    add_code_table(
        doc,
        "ViewModels/LoginViewModel.cs - xử lý LoginCommand",
        """
private async Task LoginAsync()
{
    ErrorMessage = string.Empty;
    StatusMessage = string.Empty;

    var result = await _accountService.LoginAsync(EmailOrUsername, Password);
    if (result.Succeeded && result.User is not null)
    {
        Password = string.Empty;
        _loginSucceeded(result.User);
        return;
    }

    ErrorMessage = result.Message;
}
        """,
    )

    add_heading(doc, "3.2. Triển khai logic nghiệp vụ", 2)
    add_body_paragraph(
        doc,
        "BuildService chịu trách nhiệm nạp kit và linh kiện từ catalog, kiểm tra tính hợp lệ, tính tổng giá snapshot và chuẩn hóa dữ liệu trước khi lưu. "
        "Cách triển khai này giúp validation tập trung ở service thay vì rải rác trong giao diện."
    )
    add_ref_sentence(doc, "Một đoạn kiểm tra tương thích và tổng giá trong BuildService được đưa tại ", "Bảng 3.3", "tbl_3_3", ".")
    add_table_caption(doc, "tbl_3_3", bookmark_id)
    bookmark_id += 1
    add_code_table(
        doc,
        "Services/BuildService.cs - kiểm tra item và tính snapshot",
        """
foreach (var item in build.Items)
{
    if (!item.HasExactlyOneProduct())
    {
        result.AddError(Loc.Instance["Build_ItemOneProduct"]);
        resolvedItems.Add(new ResolvedItem(item, 0m));
        continue;
    }

    var unitPrice = await ResolveItemAsync(
        item, kit, layout, result,
        quantity => switchQuantityTotal += quantity,
        () => hasKeycap = true,
        () => hasStabilizer = true,
        cancellationToken);

    resolvedItems.Add(new ResolvedItem(item, unitPrice));
    total += item.Quantity * unitPrice;
}
        """,
    )

    add_heading(doc, "3.3. Kiểm thử hệ thống", 2)
    add_body_paragraph(
        doc,
        "Kiểm thử được thực hiện bằng runner Phase6Verification, kết hợp unit-style check, SQL integration và các truy vấn invariant trên database. "
        "Kết quả tài liệu hiện tại ghi nhận 18/18 kiểm tra tự động đạt, đồng thời test matrix T01-T16 chỉ còn các phần UI/manual hoặc SignalR defer."
    )
    add_ref_sentence(doc, "Các trường hợp kiểm thử tiêu biểu được tóm tắt trong ", "Bảng 3.4", "tbl_3_4", ".")
    add_table_caption(doc, "tbl_3_4", bookmark_id)
    bookmark_id += 1
    add_standard_table(
        doc,
        ["Mã kiểm thử", "Mục tiêu", "Kết quả"],
        [
            ["T01", "Đăng ký Buyer và đăng nhập bằng tài khoản hợp lệ", "Pass"],
            ["T02", "User bị khóa không đăng nhập được", "Pass"],
            ["T04", "Buyer tạo build hợp lệ và lưu snapshot giá", "Pass"],
            ["T05", "Build sai compatibility bị cảnh báo/chặn theo mức lỗi", "Pass"],
            ["T06/T07", "Gửi request cho seller verified và chặn seller unverified", "Pass"],
            ["T08/T09", "Seller chỉ xem/cập nhật request thuộc phạm vi của mình", "Pass"],
            ["T10/T11", "State machine request đúng rule và chặn transition sai", "Pass"],
            ["T13/T14", "Chat Buyer-Seller và Seller-Admin được lưu DB", "Pass"],
            ["T16", "SignalR realtime chat", "Defer theo Phase 8A"],
        ],
        [1600, 5700, 1700],
    )

    add_heading(doc, "3.4. Kết luận chương", 2)
    add_body_paragraph(
        doc,
        "Chương 3 đã trình bày được phần triển khai giao diện, xử lý đăng nhập, validation build và kiểm thử hệ thống. "
        "Các kết quả kiểm thử cho thấy luồng MVP lõi đã đáp ứng được yêu cầu quản lý build, request, role, chat và dữ liệu QC mô phỏng."
    )

    doc.add_page_break()
    add_heading(doc, "Kết luận", 1)
    add_body_paragraph(
        doc,
        "Đề tài Custom Keyboard Builder đã hoàn thành các nhiệm vụ cốt lõi: thiết kế cơ sở dữ liệu SQL Server theo mô hình kit-based, triển khai ứng dụng WPF theo MVVM, xây dựng các service nghiệp vụ, phân quyền theo vai trò và kiểm thử các luồng chính. "
        "Các chức năng Buyer tạo build, gửi request, Seller xử lý trạng thái, Admin quản trị, chat lưu DB và QC mô phỏng đều đã có cơ sở triển khai trong mã nguồn."
    )
    add_body_paragraph(
        doc,
        "Kết quả đạt được phù hợp với mục đích ban đầu là số hóa quy trình cấu hình và xử lý build bàn phím custom. "
        "Việc dùng SQL Server làm source of truth, kết hợp validation ở service layer, giúp dữ liệu build/request ổn định hơn và dễ kiểm thử hơn."
    )
    add_body_paragraph(
        doc,
        "Một số hướng phát triển tiếp theo gồm bổ sung ảnh chụp giao diện thật, thay thế các ảnh diagram màu đỏ bằng diagram chính thức, hoàn thiện kiểm thử UI cho thao tác ẩn/khôi phục linh kiện, và cân nhắc triển khai SignalR hoặc mở rộng MQTT cho chat realtime nếu phạm vi môn học yêu cầu."
    )

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUTPUT)
    return OUTPUT


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--page-count", default=None)
    args = parser.parse_args()
    path = build_document(args.page_count)
    print(path)


if __name__ == "__main__":
    main()
