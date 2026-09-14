"""docx_writer 技能脚本：根据大纲生成 Word 文档.

依赖 python-docx；沙箱镜像未预装时自动 pip 安装。
用法（沙箱 python）:
    exec(open('skills/docx_writer/generate_docx.py', encoding='utf-8').read())
    doc = build_docx(title='标题', outline=[...], out_path='output.docx')
"""

import importlib
import re
import subprocess
import sys
from datetime import date


def _ensure(pkg, import_name=None):
    try:
        return importlib.import_module(import_name or pkg)
    except ImportError:
        # code-interpreter 镜像为 PEP 668 externally-managed 环境，需 --break-system-packages；
        # 沙箱一次性使用，安全风险可接受
        subprocess.check_call([sys.executable, '-m', 'pip', 'install', '--break-system-packages', '-q', pkg])
        return importlib.import_module(import_name or pkg)


docx = _ensure('python-docx', 'docx')
Document = docx.Document
WD_ALIGN_PARAGRAPH = docx.enum.text.WD_ALIGN_PARAGRAPH

_BOLD_RE = re.compile(r'\*\*(.+?)\*\*')


def _add_rich_paragraph(container, text):
    """写入段落，支持 **加粗** 行内标记."""
    paragraph = container.add_paragraph()
    pos = 0
    for match in _BOLD_RE.finditer(text):
        if match.start() > pos:
            paragraph.add_run(text[pos:match.start()])
        run = paragraph.add_run(match.group(1))
        run.bold = True
        pos = match.end()
    if pos < len(text):
        paragraph.add_run(text[pos:])
    return paragraph


def _render_section(document, item):
    level = min(int(item.get('level', 1) or 1), 4)
    text = str(item.get('text', '') or '').strip()
    if text:
        document.add_heading(text, level=level)
    for para in item.get('paragraphs') or []:
        _add_rich_paragraph(document, str(para))
    for child in item.get('children') or []:
        _render_section(document, child)


def build_docx(title, outline, out_path='output.docx'):
    """根据大纲生成 Word 文档并保存.

    :param title: 文档标题.
    :param outline: 大纲列表，每项支持 level/text/children/paragraphs.
    :param out_path: 输出文件路径.
    """
    document = Document()

    heading = document.add_heading(str(title), level=0)
    heading.alignment = WD_ALIGN_PARAGRAPH.CENTER
    meta = document.add_paragraph(str(date.today()))
    meta.alignment = WD_ALIGN_PARAGRAPH.CENTER

    for item in outline or []:
        _render_section(document, item)

    document.save(out_path)
    return document
