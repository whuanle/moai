"""ppt_writer 技能脚本：根据大纲生成 PPT 演示文稿.

依赖 python-pptx；沙箱镜像未预装时自动 pip 安装。
用法（沙箱 python）:
    exec(open('skills/ppt_writer/generate_pptx.py', encoding='utf-8').read())
    prs = build_pptx(title='标题', subtitle='副标题', slides=[...], out_path='output.pptx')
"""

import importlib
import subprocess
import sys


def _ensure(pkg, import_name=None):
    try:
        return importlib.import_module(import_name or pkg)
    except ImportError:
        # code-interpreter 镜像为 PEP 668 externally-managed 环境，需 --break-system-packages；
        # 沙箱一次性使用，安全风险可接受
        subprocess.check_call([sys.executable, '-m', 'pip', 'install', '--break-system-packages', '-q', pkg])
        return importlib.import_module(import_name or pkg)


pptx = _ensure('python-pptx', 'pptx')
Presentation = pptx.Presentation


def _add_bullets(body, bullets):
    first = True
    for bullet in bullets:
        text = str(bullet)
        level = 1 if text.startswith('  ') else 0
        text = text.strip()
        if first:
            paragraph = body.paragraphs[0]
            first = False
        else:
            paragraph = body.add_paragraph()
        paragraph.level = level
        run = paragraph.add_run()
        run.text = text


def build_pptx(title, subtitle='', slides=None, out_path='output.pptx'):
    """根据大纲生成演示文稿并保存.

    :param title: 封面标题.
    :param subtitle: 封面副标题.
    :param slides: 内容页列表，每项 {"title": 页标题, "bullets": ["要点", "  子要点"]}.
    :param out_path: 输出文件路径.
    """
    prs = Presentation()

    cover = prs.slides.add_slide(prs.slide_layouts[0])
    cover.shapes.title.text = str(title)
    if subtitle:
        cover.placeholders[1].text = str(subtitle)

    for item in slides or []:
        slide = prs.slides.add_slide(prs.slide_layouts[1])
        slide.shapes.title.text = str(item.get('title', '') or '')
        body = slide.placeholders[1]
        body.text_frame.clear()
        _add_bullets(body.text_frame, item.get('bullets') or [])

    prs.save(out_path)
    return prs
