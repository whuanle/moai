#!/usr/bin/env python3
"""把 moai 仓库 docs 按类型分层结构重组进 Obsidian vault，并重写互链为 wikilink."""
import os, re, shutil, subprocess

REPO = '/Users/wen/project/maomi/moai'
VAULT = '/Users/wen/Documents/Obsidian Vault/MoAI'

# 模块清单：(源目录, 中文名, 序号)。后端 01-11，前端 21-30。
MODULES = [
    ('docs/auth',            '认证',        '01'),
    ('docs/account',         '账号自助',     '02'),
    ('docs/user-management', '用户管理',     '03'),
    ('docs/settings',        '系统设置',     '04'),
    ('docs/oauthconnect',    '第三方登录连接器', '05'),
    ('docs/common',          '公共信息',     '06'),
    ('docs/storage',         '文件存储',     '07'),
    ('docs/hangfire',        '后台任务',     '08'),
    ('docs/infra',           '基础设施',     '09'),
    ('docs/database-scaffold', '数据库脚手架', '10'),
    ('docs/deployment',      '部署',        '11'),
    ('ui/docs/auth-flow',        '认证流',       '21'),
    ('ui/docs/api-layer',        'API层',       '22'),
    ('ui/docs/theme',            '主题',        '23'),
    ('ui/docs/components-base',  '基础组件',     '24'),
    ('ui/docs/components-form',  '表单组件',     '25'),
    ('ui/docs/layout-routing',   '布局路由',     '26'),
    ('ui/docs/store-i18n',       '状态与国际化',  '27'),
    ('ui/docs/page-account',     '账号设置页',    '28'),
    ('ui/docs/page-admin',       '管理页',      '29'),
    ('ui/docs/dashboard-testing','Dashboard与测试', '30'),
]

TYPE_DIRS = {'sdd': 'SDD-规范驱动', 'bdd': 'BDD-交互驱动', 'tdd': 'TDD-测试驱动', 'sop': 'SOP-端到端场景'}

# 特殊文件：(源路径, 目标路径)
SPECIALS = [
    ('docs/DOC-STANDARD.md',              'BDD-交互驱动/00-Gherkin规范与文档分层.md'),
    ('docs/cqrs-conventions.md',          'SDD-规范驱动/00-CQRS架构约定.md'),
    ('docs/settings.md',                  'SDD-规范驱动/90-Setting表机制.md'),
    ('docs/storage-file-layout.md',       'SDD-规范驱动/91-对象存储路径布局.md'),
    ('ui/docs/frontend-conventions.md',   'SDD-规范驱动/92-前端架构约定.md'),
    ('ui/docs/design-system/README.md',   'SDD-规范驱动/93-设计系统总览.md'),
    ('ui/docs/design-system/theming.md',  'SDD-规范驱动/94-设计系统主题.md'),
    ('ui/docs/design-system/components.md','SDD-规范驱动/95-设计系统组件.md'),
    ('ui/docs/design-system/pages.md',    'SDD-规范驱动/96-设计系统页面.md'),
    ('ui/docs/design-system/feedback.md', 'SDD-规范驱动/97-设计系统反馈.md'),
    ('docs/rounds-log.md',                '97-熟悉文档化验证台账.md'),
    ('docs/user-management/manual.html',  'SOP-端到端场景/用户管理图文操作手册.html'),
]

# ---------- 1. 建 源绝对路径 → 目标绝对路径 映射 ----------
src2dst = {}
module_docs = []  # (序号, 中文名, {type: 源路径})
for srcdir, cname, num in MODULES:
    entry = {'num': num, 'name': cname}
    for t, tdir in TYPE_DIRS.items():
        src = os.path.join(REPO, srcdir, f'{t}.md')
        if os.path.isfile(src):
            dst = os.path.join(VAULT, tdir, f'{num}-{cname}.md')
            src2dst[src] = dst
            entry[t] = src
    module_docs.append(entry)
for src, dst in SPECIALS:
    src2dst[os.path.join(REPO, src)] = os.path.join(VAULT, dst)

# ---------- 2. 链接重写 ----------
WIKI_CACHE = {}

def target_wikilink(dst_path):
    """目标文件的 wikilink 目标（相对 vault 根，不带 .md）。"""
    rel = os.path.relpath(dst_path, VAULT)
    return rel[:-3] if rel.endswith('.md') else rel

def rewrite_link(m, source_file):
    text, href = m.group(1), m.group(2)
    # 去掉锚点
    path_part = href.split('#')[0]
    if not path_part or path_part.startswith(('http://', 'https://', 'mailto:')):
        return m.group(0)
    abs_src = os.path.normpath(os.path.join(os.path.dirname(source_file), path_part))
    dst = src2dst.get(abs_src)
    if dst:
        # 文本本身是路径样式（如 ../auth/sdd.md、./sdd.md）时，换成目标文件简称
        alias = text
        if text.endswith('.md') or '/sdd' in text or text.startswith(('./', '../')):
            alias = os.path.basename(dst)[:-3]
        return f'[[{target_wikilink(dst)}|{alias}]]'
    # 指向仓库内非文档资源（脚本/图片等）→ 代码体标注仓库路径
    rel_repo = os.path.relpath(abs_src, REPO)
    return f'`{text}（仓库 {rel_repo}）`'

link_re = re.compile(r'\[([^\]]+)\]\(([^)]+)\)')

count = 0
for src, dst in src2dst.items():
    if not os.path.isfile(src):
        print(f'!! 缺源文件: {src}')
        continue
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(src, encoding='utf-8') as f:
        content = f.read()
    out, pos = [], 0
    for m in link_re.finditer(content):
        out.append(content[pos:m.start()])
        out.append(rewrite_link(m, src))
        pos = m.end()
    out.append(content[pos:])
    new_content = ''.join(out)
    with open(dst, 'w', encoding='utf-8') as f:
        f.write(new_content)
    count += 1

print(f'转换文档: {count} 篇')

# ---------- 3. 列出四个类型文件夹的最终文件清单（供总览生成用）----------
listing = {}
for tdir in TYPE_DIRS.values():
    files = sorted(os.listdir(os.path.join(VAULT, tdir)))
    listing[tdir] = files
    print(f'{tdir}: {len(files)} 篇')
