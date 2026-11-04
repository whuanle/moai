/**
 * 沙箱禁用命令模式校验，与后端 SandboxCommandPatterns 语义一致：
 * 每行一条模式，默认按全词匹配（rm 不误杀 confirm），regex: 前缀为原生正则（须可编译）；
 * 单条不超过 200 字符、每清单不超过 100 条，大小写不敏感去重。
 */

export const MAX_COMMAND_PATTERNS = 100
export const MAX_COMMAND_PATTERN_LENGTH = 200
export const COMMAND_REGEX_PREFIX = 'regex:'

/** 拆行归一：去空白行、trim、忽略大小写去重、截断到上限 */
export function parseCommandPatterns(value: string): string[] {
  const result: string[] = []
  const seen = new Set<string>()
  for (const raw of value.split('\n')) {
    const pattern = raw.trim()
    if (!pattern || pattern.length > MAX_COMMAND_PATTERN_LENGTH || seen.has(pattern.toLowerCase())) continue
    seen.add(pattern.toLowerCase())
    result.push(pattern)
    if (result.length >= MAX_COMMAND_PATTERNS) break
  }
  return result
}

/** 校验单条模式：非空、长度合规、regex: 前缀须可编译 */
export function isValidCommandPattern(pattern: string): boolean {
  const text = pattern.trim()
  if (!text || text.length > MAX_COMMAND_PATTERN_LENGTH) return false
  if (!text.toLowerCase().startsWith(COMMAND_REGEX_PREFIX)) return true
  const body = text.slice(COMMAND_REGEX_PREFIX.length).trim()
  if (!body) return false
  try {
    // 与后端一致按忽略大小写编译；仅校验可编译性，不参与匹配
    void new RegExp(body, 'i')
    return true
  } catch {
    return false
  }
}

/** 校验换行分隔清单：全部合法且不超过上限 */
export function isValidCommandPatternList(value: string): boolean {
  const seen = new Set<string>()
  for (const raw of value.split('\n')) {
    const pattern = raw.trim()
    if (!pattern || seen.has(pattern.toLowerCase())) continue
    seen.add(pattern.toLowerCase())
    if (seen.size > MAX_COMMAND_PATTERNS) return false
    if (!isValidCommandPattern(pattern)) return false
  }
  return true
}
