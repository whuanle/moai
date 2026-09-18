/**
 * cURL 命令解析：把常见 curl 命令转换为 http 节点请求配置.
 * 支持 -X/--request、-H/--header、-d/--data/--data-raw/--data-binary/--data-urlencode、
 * -u/--user（Basic 鉴权）、Bearer Token 识别与 URL 提取；其余选项忽略.
 */

import type { HttpAuthSetting, HttpKvItem } from './types'

export interface ParsedCurl {
  method: string
  url: string
  headers: HttpKvItem[]
  bodyType: 'none' | 'json' | 'form' | 'text'
  body: string
  formEntries?: HttpKvItem[]
  auth?: HttpAuthSetting
}

/** shell 词法切分：处理单引号/双引号/反斜杠转义，换行视作空白（粘贴多行 curl 场景） */
export function tokenizeShell(input: string): string[] {
  const tokens: string[] = []
  let current = ''
  let hasToken = false
  let quote: '"' | "'" | null = null
  for (let i = 0; i < input.length; i++) {
    const ch = input[i]
    if (quote) {
      if (quote === '"' && ch === '\\' && i + 1 < input.length) {
        current += input[++i]
        continue
      }

      if (ch === quote) {
        quote = null
        continue
      }

      current += ch
      continue
    }

    if (ch === '"' || ch === "'") {
      quote = ch
      hasToken = true
      continue
    }

    if (ch === '\\' && i + 1 < input.length) {
      current += input[++i]
      hasToken = true
      continue
    }

    if (/\s/.test(ch)) {
      if (hasToken || current) {
        tokens.push(current)
        current = ''
        hasToken = false
      }

      continue
    }

    current += ch
    hasToken = true
  }

  if (hasToken || current) tokens.push(current)
  return tokens
}

/** 把 cURL 参数串解析为 http 节点配置；无法提取 URL 时返回 null */
export function parseCurlCommand(input: string): ParsedCurl | null {
  if (!input || !input.trim()) return null
  let tokens = tokenizeShell(input.replace(/\r?\n/g, ' '))
  if (tokens.length === 0) return null
  // 兼容带路径的 curl 可执行名（/usr/bin/curl）或省略 curl 的裸参数
  if ((tokens[0] ?? '').toLowerCase().endsWith('curl')) tokens = tokens.slice(1)

  let method = ''
  let url = ''
  const headers: HttpKvItem[] = []
  const bodies: string[] = []
  let basicAuth: string | null = null

  for (let i = 0; i < tokens.length; i++) {
    const tok = tokens[i] ?? ''
    const next = tokens[i + 1]
    switch (tok) {
      case '-X':
      case '--request':
        if (next) {
          method = next.toUpperCase()
          i++
        }

        break
      case '-H':
      case '--header': {
        if (next === undefined) break
        i++
        const idx = next.indexOf(':')
        if (idx > 0) {
          headers.push({ name: next.slice(0, idx).trim(), value: next.slice(idx + 1).trim() })
        } else if (next.endsWith(';')) {
          // curl 语义："Name;" 表示移除默认请求头，此处映射为空值头
          headers.push({ name: next.slice(0, -1).trim(), value: '' })
        }

        break
      }
      case '-d':
      case '--data':
      case '--data-raw':
      case '--data-binary':
      case '--data-urlencode':
        if (next !== undefined) {
          bodies.push(next)
          i++
        }

        break
      case '-u':
      case '--user':
        if (next) {
          basicAuth = next
          i++
        }

        break
      case '--get':
        method = 'GET'
        break
      case '-I':
      case '--head':
        method = 'HEAD'
        break
      default:
        if (!tok.startsWith('-') && !url) url = tok
        break
    }
  }

  if (!url) return null
  if (!method) method = bodies.length > 0 ? 'POST' : 'GET'

  // Authorization: Bearer xxx 提升为鉴权配置；-u 提升为 Basic
  let auth: HttpAuthSetting | undefined
  const authHeaderIdx = headers.findIndex((h) => h.name.toLowerCase() === 'authorization')
  if (authHeaderIdx >= 0 && /^Bearer\s+/i.test(headers[authHeaderIdx]!.value)) {
    auth = { type: 'bearer', token: headers[authHeaderIdx]!.value.replace(/^Bearer\s+/i, '') }
    headers.splice(authHeaderIdx, 1)
  }

  if (basicAuth !== null) {
    const sep = basicAuth.indexOf(':')
    auth = {
      type: 'basic',
      username: sep >= 0 ? basicAuth.slice(0, sep) : basicAuth,
      password: sep >= 0 ? basicAuth.slice(sep + 1) : '',
    }
  }

  // 请求体：JSON（Content-Type 或字面量形状）优先；k=v 形态转表单字段；其余按文本
  let bodyType: ParsedCurl['bodyType'] = 'none'
  let body = ''
  let formEntries: HttpKvItem[] | undefined
  if (bodies.length > 0) {
    const contentType = headers.find((h) => h.name.toLowerCase() === 'content-type')?.value ?? ''
    const parts = bodies.flatMap((b) => String(b).split('&'))
    const formLike = !/json/i.test(contentType) && parts.every((p) => /^[^=&]+=/.test(p))
    if (/json/i.test(contentType)) {
      bodyType = 'json'
      body = parts.join('&')
    } else if (formLike) {
      bodyType = 'form'
      formEntries = parts.map((p) => {
        const idx = p.indexOf('=')
        return { name: p.slice(0, idx), value: p.slice(idx + 1) }
      })
    } else {
      body = parts.join('&')
      const trimmed = body.trim()
      bodyType = (trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']')) ? 'json' : 'text'
    }
  }

  return { method, url, headers, bodyType, body, ...(formEntries ? { formEntries } : {}), ...(auth ? { auth } : {}) }
}
