import { describe, expect, it } from 'vitest'
import { applyMarkdownEdit, type MarkdownStrings } from '../markdown'

const s: MarkdownStrings = {
  text: '文字',
  code: '代码',
  linkText: '链接文字',
  linkUrl: 'https://',
  tableHeader: '表头',
  tableCell: '内容',
}

function apply(content: string, start: number, end: number, action: Parameters<typeof applyMarkdownEdit>[2]) {
  return applyMarkdownEdit(content, { start, end }, action, s)
}

describe('applyMarkdownEdit', () => {
  it('加粗：包裹选中文本，光标落在语法内', () => {
    const r = apply('hello 世界', 6, 8, 'bold')
    expect(r.content).toBe('hello **世界**')
    expect(r.selectionStart).toBe(8)
    expect(r.selectionEnd).toBe(10)
  })

  it('加粗：再次对已包裹文本操作时解除包裹', () => {
    const r = apply('**世界**', 0, 6, 'bold')
    expect(r.content).toBe('世界')
  })

  it('无选区时在光标处插入占位文案并选中它', () => {
    const r = apply('ab', 2, 2, 'italic')
    expect(r.content).toBe('ab*文字*')
    expect(r.selectionStart).toBe(3)
    expect(r.selectionEnd).toBe(5)
  })

  it('行内代码用反引号包裹', () => {
    const r = apply('x = 1', 0, 5, 'inlineCode')
    expect(r.content).toBe('`x = 1`')
  })

  it('链接：选中文字插入并选中 url 部分', () => {
    const r = apply('看这个', 0, 3, 'link')
    expect(r.content).toBe('[看这个](https://)')
    expect(r.content.slice(r.selectionStart, r.selectionEnd)).toBe('https://')
  })

  it('标题：多行整体加 H2 前缀，再次操作取消', () => {
    const add = apply('第一行\n第二行', 0, 7, 'h2')
    expect(add.content).toBe('## 第一行\n## 第二行')

    const removed = applyMarkdownEdit(add.content, { start: 0, end: add.content.length }, 'h2', s)
    expect(removed.content).toBe('第一行\n第二行')
  })

  it('标题：添加时替换旧级别而不是叠加', () => {
    const r = apply('# 旧标题', 0, 5, 'h3')
    expect(r.content).toBe('### 旧标题')
  })

  it('任务列表：无选区时作用于当前行', () => {
    const r = apply('待办事项\n下一行', 2, 2, 'task')
    expect(r.content).toBe('- [ ] 待办事项\n下一行')
  })

  it('代码块：包裹选中文本', () => {
    const r = apply('console.log(1)', 0, 14, 'codeBlock')
    expect(r.content).toBe('```\nconsole.log(1)\n```')
    expect(r.content.slice(r.selectionStart, r.selectionEnd)).toBe('console.log(1)')
  })

  it('表格：光标处插入示例表格（行中先补换行）', () => {
    const r = apply('前文', 2, 2, 'table')
    expect(r.content).toBe('前文\n| 表头 | 表头 | 表头 |\n| --- | --- | --- |\n| 内容 | 内容 | 内容 |')
  })

  it('分隔线：光标处插入', () => {
    const r = apply('上一段', 3, 3, 'hr')
    expect(r.content).toBe('上一段\n---')
  })
})
