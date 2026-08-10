"""Renders the session transcript to PDF.

Markdown-lite on purpose: the transcript uses headings, bullets, tables, inline
code and horizontal rules, and nothing else. A full markdown engine would be a
dependency to install and a second thing to be wrong about.

Korean needs an embedded font. ReportLab's built-in fonts have no Hangul, and
the failure is silent — every Korean glyph comes out as a black box, which
looks like a corrupt PDF rather than a missing font.
"""

import io
import re
import sys

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, HRFlowable, KeepTogether, PageTemplate,
    Paragraph, Preformatted, Spacer, Table, TableStyle,
)

BODY, BOLD = "Malgun", "MalgunBd"
pdfmetrics.registerFont(TTFont(BODY, r"C:\Windows\Fonts\malgun.ttf"))
pdfmetrics.registerFont(TTFont(BOLD, r"C:\Windows\Fonts\malgunbd.ttf"))
pdfmetrics.registerFontFamily(BODY, normal=BODY, bold=BOLD, italic=BODY,
                              boldItalic=BOLD)

INK = colors.HexColor("#1a1a1a")
MUTED = colors.HexColor("#6b6b6b")
RULE = colors.HexColor("#d8d8d8")
CODE_BG = colors.HexColor("#f4f4f4")

styles = {
    "title": ParagraphStyle("title", fontName=BOLD, fontSize=26, leading=32,
                            textColor=INK, spaceAfter=4),
    "subtitle": ParagraphStyle("subtitle", fontName=BODY, fontSize=10.5,
                               leading=16, textColor=MUTED, spaceAfter=2),
    "h2": ParagraphStyle("h2", fontName=BOLD, fontSize=16, leading=21,
                         textColor=INK, spaceBefore=20, spaceAfter=8),
    "h3": ParagraphStyle("h3", fontName=BOLD, fontSize=11.5, leading=16,
                         textColor=INK, spaceBefore=12, spaceAfter=5),
    "body": ParagraphStyle("body", fontName=BODY, fontSize=9.8, leading=15.6,
                           textColor=INK, alignment=TA_LEFT, spaceAfter=7),
    "bullet": ParagraphStyle("bullet", fontName=BODY, fontSize=9.8,
                             leading=15.2, textColor=INK, leftIndent=13,
                             bulletIndent=3, spaceAfter=3),
    "cell": ParagraphStyle("cell", fontName=BODY, fontSize=8.8, leading=13,
                           textColor=INK),
    "cellhead": ParagraphStyle("cellhead", fontName=BOLD, fontSize=8.8,
                               leading=13, textColor=INK),
}


def inline(text):
    """`code`, **bold**, and the XML escaping ReportLab needs."""
    text = (text.replace("&", "&amp;").replace("<", "&lt;")
                .replace(">", "&gt;"))
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    text = re.sub(
        r"`(.+?)`",
        r'<font face="Courier" size="8.8" backColor="#f0f0f0">\1</font>',
        text)
    return text


def split_row(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


def build_table(rows, width):
    head, body = rows[0], rows[1:]
    data = [[Paragraph(inline(c), styles["cellhead"]) for c in head]]
    data += [[Paragraph(inline(c), styles["cell"]) for c in r] for r in body]
    cols = max(len(r) for r in data)
    data = [r + [Paragraph("", styles["cell"])] * (cols - len(r)) for r in data]

    table = Table(data, colWidths=[width / cols] * cols, hAlign="LEFT")
    table.setStyle(TableStyle([
        ("FONTNAME", (0, 0), (-1, -1), BODY),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 7),
        ("RIGHTPADDING", (0, 0), (-1, -1), 7),
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f0f0f0")),
        ("LINEBELOW", (0, 0), (-1, 0), 0.7, RULE),
        ("LINEBELOW", (0, 1), (-1, -2), 0.3, colors.HexColor("#ececec")),
        ("BOX", (0, 0), (-1, -1), 0.7, RULE),
    ]))
    return table


def convert(md, width):
    story, lines, i = [], md.split("\n"), 0
    while i < len(lines):
        line = lines[i].rstrip()

        if not line.strip():
            i += 1
            continue

        if line.startswith("```"):
            i += 1
            block = []
            while i < len(lines) and not lines[i].startswith("```"):
                block.append(lines[i])
                i += 1
            i += 1
            story.append(Preformatted(
                "\n".join(block),
                ParagraphStyle("code", fontName="Courier", fontSize=8.2,
                               leading=11.5, textColor=INK,
                               backColor=CODE_BG, borderPadding=6,
                               leftIndent=2),
            ))
            story.append(Spacer(1, 7))
            continue

        if line.startswith("|") and i + 1 < len(lines) \
                and set(lines[i + 1].replace("|", "").strip()) <= set("-: "):
            rows = [split_row(line)]
            i += 2
            while i < len(lines) and lines[i].startswith("|"):
                rows.append(split_row(lines[i]))
                i += 1
            story.append(build_table(rows, width))
            story.append(Spacer(1, 10))
            continue

        if line.startswith("---"):
            story.append(Spacer(1, 5))
            story.append(HRFlowable(width="100%", thickness=0.7, color=RULE))
            story.append(Spacer(1, 3))
            i += 1
            continue

        if line.startswith("# "):
            story.append(Paragraph(inline(line[2:]), styles["title"]))
            i += 1
            continue

        if line.startswith("## "):
            # Kept with whatever follows it, so a section never opens as the
            # last line on a page.
            head = Paragraph(inline(line[3:]), styles["h2"])
            story.append(KeepTogether([head, Spacer(1, 1)]))
            i += 1
            continue

        if line.startswith("### "):
            story.append(Paragraph(inline(line[4:]), styles["h3"]))
            i += 1
            continue

        if re.match(r"^\s*[-*] ", line):
            indent = (len(line) - len(line.lstrip())) // 2
            text = re.sub(r"^\s*[-*] ", "", line)
            style = ParagraphStyle(
                f"b{indent}", parent=styles["bullet"],
                leftIndent=13 + indent * 13, bulletIndent=3 + indent * 13)
            story.append(Paragraph(inline(text), style, bulletText="•"))
            i += 1
            continue

        if re.match(r"^\s*\d+\. ", line):
            number = re.match(r"^\s*(\d+)\. ", line).group(1)
            text = re.sub(r"^\s*\d+\. ", "", line)
            story.append(Paragraph(inline(text), styles["bullet"],
                                   bulletText=f"{number}."))
            i += 1
            continue

        # A run of plain lines is one paragraph.
        para = [line]
        i += 1
        while i < len(lines) and lines[i].strip() and not re.match(
                r"^\s*([-*#>|]|\d+\.|```)", lines[i]):
            para.append(lines[i].rstrip())
            i += 1
        story.append(Paragraph(inline(" ".join(para)), styles["body"]))

    return story


# 표지 제목과 각주. 세션마다 바뀌므로 인자로 받는다.
TITLE = "대화 기록"
FOOTER = "PawliceAndPurrglar"


def main(src, out):
    md = io.open(src, encoding="utf-8").read()

    doc = BaseDocTemplate(
        out, pagesize=A4,
        leftMargin=22 * mm, rightMargin=22 * mm,
        topMargin=20 * mm, bottomMargin=18 * mm,
        title=TITLE, author="PawliceAndPurrglar",
    )
    frame = Frame(doc.leftMargin, doc.bottomMargin,
                  doc.width, doc.height, id="body")

    def furniture(canvas, document):
        canvas.saveState()
        canvas.setFont(BODY, 8)
        canvas.setFillColor(MUTED)
        canvas.drawString(doc.leftMargin, 11 * mm,
                          FOOTER)
        canvas.drawRightString(A4[0] - doc.rightMargin, 11 * mm,
                               str(document.page))
        canvas.restoreState()

    doc.addPageTemplates([
        PageTemplate(id="main", frames=[frame], onPage=furniture)])
    doc.build(convert(md, doc.width))
    print(f"wrote {out}")


if __name__ == "__main__":
    if len(sys.argv) > 3:
        TITLE = sys.argv[3]
    if len(sys.argv) > 4:
        FOOTER = sys.argv[4]
    main(sys.argv[1], sys.argv[2])
