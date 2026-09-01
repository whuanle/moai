#!/usr/bin/env python3
"""DOC-STANDARD 全量审计：Gherkin 规范/场景编号/四件互链/篇幅/链接有效性"""
import os, re, sys

ROOT = '/Users/wen/project/maomi/moai'
DIRS = []
for base in ['docs', 'ui/docs']:
    for d in sorted(os.listdir(os.path.join(ROOT, base))):
        p = os.path.join(base, d)
        if os.path.isdir(os.path.join(ROOT, p)):
            DIRS.append(p)

total_scen = 0
problems = []
for d in DIRS:
    dp = os.path.join(ROOT, d)
    files = os.listdir(dp)
    four = [f for f in ['sdd.md', 'bdd.md', 'tdd.md', 'sop.md'] if f in files]
    if len(four) < 4:
        print(f'[SKIP] {d}: 仅 {four}')
        continue

    bdd = open(os.path.join(dp, 'bdd.md'), encoding='utf-8').read()
    # 1) 所有 Scenario 有编号标签
    scen_blocks = re.findall(r'((?:@[\w:-]+[^\n]*\n)*)\s*Scenario(?: Outline)?:', bdd)
    for i, blk in enumerate(scen_blocks, 1):
        if not re.search(r'@[\w-]+-S\d+', blk or ''):
            problems.append(f'{d}/bdd.md: 第{i}个 Scenario 缺编号标签')
        if not re.search(r'@auto:|@manual', blk or ''):
            problems.append(f'{d}/bdd.md: 第{i}个 Scenario 缺自动化标签(@auto/@manual)')
    # 2) Given/When/Then 完整性
    for m in re.finditer(r'Scenario(?: Outline)?:[^\n]*\n(.*?)(?=\n\s*@|\n\s*Scenario|\n\s*Feature|\Z)', bdd, re.S):
        body = m.group(1)
        steps = len(re.findall(r'^\s*(?:Given|When|Then)\b', body, re.M))
        if steps < 2 or not re.search(r'^\s*Then\b', body, re.M):
            problems.append(f'{d}/bdd.md: 场景缺 Given/When/Then :: {m.group(0)[:60]!r}')
    total_scen += len(scen_blocks)

    # 3) 四件互链（每件引用另外三件）
    for f in four:
        txt = open(os.path.join(dp, f), encoding='utf-8').read()
        others = [x for x in four if x != f and (f'./{x}' in txt or f']({x})' in txt or f'({x}' in txt)]
        if len(others) < 3:
            problems.append(f'{d}/{f}: 仅互链 {len(others)}/3 -> {others}')
        # 4) 篇幅约束
        n = sum(1 for _ in txt.splitlines())
        limit = {'sdd.md': 130, 'tdd.md': 110, 'sop.md': 110, 'bdd.md': 10 ** 6}[f]
        if n > limit:
            problems.append(f'{d}/{f}: {n} 行超限 {limit}')
        # 5) 相对链接有效性（../ 与 ./）
        base_dir = os.path.dirname(os.path.join(ROOT, d, f))
        for link in re.findall(r'\]\((\.\.?/[^)#]+)', txt):
            target = os.path.normpath(os.path.join(base_dir, link))
            if not os.path.exists(target):
                problems.append(f'{d}/{f}: 悬空链接 {link}')
        # 6) 锚点引用有效性（@XX-Sn → bdd 中存在该标签）
        for tag in set(re.findall(r'\[(@[\w-]+-S\d+)\]', txt)):
            if tag.strip('@').lower() not in bdd.lower():
                problems.append(f'{d}/{f}: 引用了不存在的场景 {tag}')

print(f'\n=== 审计范围 {len([d for d in DIRS if len([f for f in ["sdd.md","bdd.md","tdd.md","sop.md"] if f in os.listdir(os.path.join(ROOT,d))])==4])} 个模块，场景总数 {total_scen} ===')
if problems:
    print(f'发现 {len(problems)} 个问题：')
    for p in problems:
        print(' -', p)
    sys.exit(1)
print('全部通过 ✅')
