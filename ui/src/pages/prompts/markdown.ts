/**
 * Markdown 工具栏编辑内核：纯函数，输入原文与选区，输出新文本与新选区。
 * 供提示词编辑器等场景复用；占位文案由调用方按 i18n 传入。
 */

export interface MarkdownSelection {
  start: number
  end: number
}

export interface MarkdownEditResult {
  content: string
  selectionStart: number
  selectionEnd: number
}

export type MarkdownAction =
  | 'bold'
  | 'italic'
  | 'strike'
  | 'inlineCode'
  | 'link'
  | 'h1'
  | 'h2'
  | 'h3'
  | 'quote'
  | 'ul'
  | 'ol'
  | 'task'
  | 'codeBlock'
  | 'table'
  | 'hr'

/** 工具栏插入的占位文案（调用方按语言填充） */
export interface MarkdownStrings {
  text: string
  code: string
  linkText: string
  linkUrl: string
  tableHeader: string
  tableCell: string
}

const HEADING_RE = /^#{1,6}\s/
const LIST_PREFIX_RE = /^(- \[[ x]\] |\d+\. |> |- )/
const FENCED_RE = /^```[^\n]*\n([\s\S]*?)\n```$/

function clamp(value: number, len: number): number {
  return Math.max(0, Math.min(value, len))
}

function insertAt(
  content: string,
  start: number,
  end: number,
  inserted: string,
  selStart: number,
  selEnd: number,
): MarkdownEditResult {
  return {
    content: content.slice(0, start) + inserted + content.slice(end),
    selectionStart: selStart,
    selectionEnd: selEnd,
  }
}

export function applyMarkdownEdit(
  content: string,
  selection: MarkdownSelection,
  action: MarkdownAction,
  s: MarkdownStrings,
): MarkdownEditResult {
  const len = content.length
  let start = clamp(selection.start, len)
  let end = clamp(selection.end, len)
  if (end < start) [start, end] = [end, start]
  const selected = content.slice(start, end)

  // 行内包裹语法：加粗/斜体/删除线/行内代码，选中已包裹时反选解除
  const wrap: Partial<Record<MarkdownAction, { token: string; placeholder: string }>> = {
    bold: { token: '**', placeholder: s.text },
    italic: { token: '*', placeholder: s.text },
    strike: { token: '~~', placeholder: s.text },
    inlineCode: { token: '`', placeholder: s.code },
  }
  const wrapSpec = wrap[action]
  if (wrapSpec) {
    const { token, placeholder } = wrapSpec
    if (selected.startsWith(token) && selected.endsWith(token) && selected.length >= token.length * 2) {
      const inner = selected.slice(token.length, selected.length - token.length)
      return insertAt(content, start, end, inner, start, start + inner.length)
    }
    const inner = selected || placeholder
    return insertAt(content, start, end, token + inner + token, start + token.length, start + token.length + inner.length)
  }

  if (action === 'link') {
    const m = /^\[([\s\S]*)\]\(([^()]*)\)$/.exec(selected)
    if (m) {
      const inner = m[1]
      return insertAt(content, start, end, inner, start, start + inner.length)
    }
    const text = selected || s.linkText
    const inserted = `[${text}](${s.linkUrl})`
    const urlStart = start + 1 + text.length + 2
    return insertAt(content, start, end, inserted, urlStart, urlStart + s.linkUrl.length)
  }

  // 行级前缀语法：标题/引用/列表/任务，对选区内每一行整体添加或取消
  const prefixes: Partial<Record<MarkdownAction, string>> = {
    h1: '# ',
    h2: '## ',
    h3: '### ',
    quote: '> ',
    ul: '- ',
    ol: '1. ',
    task: '- [ ] ',
  }
  const prefix = prefixes[action]
  if (prefix) {
    const lineStart = content.lastIndexOf('\n', start - 1) + 1
    let lineEnd = content.indexOf('\n', end)
    if (lineEnd === -1) lineEnd = len
    const lines = content.slice(lineStart, lineEnd).split('\n')
    const allHave = lines.every((line) => line.startsWith(prefix))
    const next = lines
      .map((line) => {
        if (allHave) return line.slice(prefix.length)
        // 添加时先清掉旧标题/列表前缀，避免叠加出非法嵌套
        return prefix + line.replace(HEADING_RE, '').replace(LIST_PREFIX_RE, '')
      })
      .join('\n')
    return insertAt(content, lineStart, lineEnd, next, lineStart, lineStart + next.length)
  }

  // 以下动作在光标处整块插入；行中插入时先补换行保证独占成块
  const atLineStart = start === 0 || content[start - 1] === '\n'
  const lead = atLineStart ? '' : '\n'

  if (action === 'codeBlock') {
    const m = FENCED_RE.exec(selected)
    if (m) {
      const inner = m[1]
      return insertAt(content, start, end, inner, start, start + inner.length)
    }
    if (selected) {
      const inserted = `${lead}\`\`\`\n${selected}\n\`\`\``
      const innerStart = start + lead.length + 4
      return insertAt(content, start, end, inserted, innerStart, innerStart + selected.length)
    }
    const inserted = `${lead}\`\`\`\n${s.code}\n\`\`\``
    const innerStart = start + lead.length + 4
    return insertAt(content, start, end, inserted, innerStart, innerStart + s.code.length)
  }

  if (action === 'table') {
    const h = s.tableHeader
    const c = s.tableCell
    const inserted = `${lead}| ${h} | ${h} | ${h} |\n| --- | --- | --- |\n| ${c} | ${c} | ${c} |`
    return insertAt(content, start, end, inserted, start + lead.length, start + lead.length + inserted.length)
  }

  // hr
  const inserted = `${lead}---`
  const cursor = start + inserted.length
  return insertAt(content, start, end, inserted, cursor, cursor)
}
