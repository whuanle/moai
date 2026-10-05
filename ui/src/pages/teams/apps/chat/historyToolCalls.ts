import type { ToolCallDisplay } from './ToolCallCard'

/**
 * 历史消息的 toolCalls 字段（后端存 JSON 数组，Kiota 可能给数组或字符串）解析为展示模型，
 * 全部按已完成状态展示；无有效调用返回 undefined.
 */
export function parseHistoryToolCalls(raw: unknown): ToolCallDisplay[] | undefined {
  let list: unknown = raw
  if (typeof raw === 'string' && raw.trim()) {
    try {
      list = JSON.parse(raw)
    } catch {
      return undefined
    }
  }
  if (!Array.isArray(list)) return undefined
  const calls = list
    .filter((x): x is Record<string, unknown> => typeof x === 'object' && x !== null)
    .map((x) => ({
      id: typeof x.id === 'string' && x.id ? x.id : crypto.randomUUID(),
      name: typeof x.name === 'string' ? x.name : '',
      argsJson:
        typeof x.arguments === 'string'
          ? x.arguments
          : x.arguments != null
            ? JSON.stringify(x.arguments)
            : undefined,
      status: 'done' as const,
    }))
    .filter((x) => x.name)
  return calls.length > 0 ? calls : undefined
}
