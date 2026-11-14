import type { SubAgentChatEventPayload } from '@/api/agentChat'
import type { DisplayMessage } from './ChatMessageList'
import type { ToolCallDisplay } from './ToolCallCard'

/** 通用子代理派发工具名（未绑定团队智能体或智能体无子智能体时的兜底） */
export const SPAWN_SUBAGENT_TOOL_NAME = 'spawn_subagent'

/** 具名子智能体派发工具名前缀（subagent_{key}） */
const SUBAGENT_TOOL_PREFIX = 'subagent_'

/** 通用子代理在过程事件中的固定 key */
const GENERAL_AGENT_KEY = 'general'

/** 子智能体派发卡内嵌活动区的过程步骤（子代理自身发起的工具调用） */
export interface SubAgentTraceStep {
  id: string
  toolName: string
  /** 结果摘要（未回填表示执行中） */
  result?: string
}

/** 子智能体派发卡的实时过程状态（moai.subagent 自定义事件累积） */
export interface SubAgentTraceState {
  traceId: string
  agentKey: string
  agentName: string
  status: 'running' | 'done' | 'timeout' | 'failed'
  /** 子代理正文流式累积 */
  text: string
  steps: SubAgentTraceStep[]
  /** 结束文本：completed 为最终结论、timeout 为部分结果、failed 为错误信息 */
  finalText?: string
  elapsedMilliseconds?: number
}

/** 判断工具调用卡是否为子代理派发卡 */
export function isSubAgentToolName(name: string): boolean {
  return name === SPAWN_SUBAGENT_TOOL_NAME || name.startsWith(SUBAGENT_TOOL_PREFIX)
}

/** 由过程事件的 agentKey 反推对应派发卡的工具名 */
function dispatchToolName(agentKey: string): string {
  return agentKey === GENERAL_AGENT_KEY ? SPAWN_SUBAGENT_TOOL_NAME : `${SUBAGENT_TOOL_PREFIX}${agentKey}`
}

/** 派发卡可接收 started 事件的状态：执行中或已批准（审批模式批准后进入执行） */
function isDispatchableStatus(status: ToolCallDisplay['status']): boolean {
  return status === 'running' || status === 'approved'
}

/** 创建派发卡的初始 trace（started 事件） */
function createTrace(evt: SubAgentChatEventPayload): SubAgentTraceState {
  return {
    traceId: evt.traceId,
    agentKey: evt.agentKey,
    agentName: evt.agentName || evt.agentKey,
    status: 'running',
    text: '',
    steps: [],
  }
}

/** 把一条过程事件归并进 trace（text 累积正文 / tool 步骤增改 / 终态收敛） */
function reduceTrace(trace: SubAgentTraceState, evt: SubAgentChatEventPayload): SubAgentTraceState {
  switch (evt.kind) {
    case 'text':
      return { ...trace, text: trace.text + (evt.text ?? '') }
    case 'toolCall':
      return {
        ...trace,
        steps: [...trace.steps, { id: crypto.randomUUID(), toolName: evt.toolName || 'tool' }],
      }
    case 'toolResult': {
      if (!evt.toolName) return trace
      // 结果归属最近一个同名且未回填结果的步骤（子代理内工具顺序执行）
      const steps = [...trace.steps]
      for (let i = steps.length - 1; i >= 0; i -= 1) {
        if (steps[i].toolName === evt.toolName && steps[i].result == null) {
          steps[i] = { ...steps[i], result: evt.result ?? '' }
          break
        }
      }
      return { ...trace, steps }
    }
    case 'completed':
    case 'timeout':
    case 'failed':
      return {
        ...trace,
        status: evt.kind === 'completed' ? 'done' : evt.kind,
        finalText: evt.finalText ?? trace.finalText,
        elapsedMilliseconds: evt.elapsedMilliseconds ?? trace.elapsedMilliseconds,
      }
    default:
      return trace
  }
}

/**
 * 把 moai.subagent 过程事件应用到会话消息流：
 * started 绑定到首个同名、执行中且未绑定 trace 的派发卡；后续事件按 traceId 归并（返回原引用表示无变化）。
 */
export function applySubAgentEvent(
  messages: DisplayMessage[],
  assistantId: string,
  evt: SubAgentChatEventPayload,
): DisplayMessage[] {
  let changed = false
  const next = messages.map((m) => {
    if (m.id !== assistantId) return m
    const calls = m.toolCalls ?? []
    let touched = false
    const nextCalls = calls.map((tc) => {
      if (!isSubAgentToolName(tc.name)) return tc
      if (evt.kind === 'started') {
        if (tc.trace || !isDispatchableStatus(tc.status) || tc.name !== dispatchToolName(evt.agentKey)) return tc
        touched = true
        return { ...tc, trace: createTrace(evt) }
      }
      if (!tc.trace || tc.trace.traceId !== evt.traceId) return tc
      touched = true
      return { ...tc, trace: reduceTrace(tc.trace, evt) }
    })
    if (!touched) return m
    changed = true
    return { ...m, toolCalls: nextCalls }
  })
  return changed ? next : messages
}

/** 从派发工具名解析子智能体 key（spawn_subagent → general） */
function agentKeyFromToolName(name: string): string {
  return name === SPAWN_SUBAGENT_TOOL_NAME ? GENERAL_AGENT_KEY : name.slice(SUBAGENT_TOOL_PREFIX.length)
}

/** 从派发卡参数中提取任务描述（{task, guidance} 契约） */
function parseTaskFromArgs(argsJson?: string): string | undefined {
  if (!argsJson) return undefined
  try {
    const args = JSON.parse(argsJson) as Record<string, unknown>
    const task = typeof args.task === 'string' ? args.task.trim() : ''
    return task || undefined
  } catch {
    return undefined
  }
}

/** 从历史回放的 tool 行结果中提取最终结论（AppToolResult JSON {success, data, error}） */
function parseResultText(result?: string): string | undefined {
  if (!result) return undefined
  try {
    const parsed = JSON.parse(result) as Record<string, unknown>
    if (typeof parsed.data === 'string' && parsed.data.trim()) return parsed.data
    if (parsed.success === false && typeof parsed.error === 'string' && parsed.error.trim()) {
      return parsed.error
    }
  } catch {
    // 非 JSON（纯文本结果）直接展示
    if (result.trim()) return result
  }
  return undefined
}

/**
 * 历史回放合成 trace：无实时过程（moai.subagent 事件不持久化），仅派发卡 + 任务描述 + 最终结论.
 */
export function buildHistoryTrace(
  toolCall: ToolCallDisplay,
  profiles?: Record<string, { name?: string; avatar?: string }>,
): SubAgentTraceState {
  const agentKey = agentKeyFromToolName(toolCall.name)
  const profile = profiles?.[agentKey]
  return {
    traceId: '',
    agentKey,
    agentName: profile?.name || agentKey,
    status: toolCall.status === 'timeout' ? 'timeout' : 'done',
    text: '',
    steps: [],
    finalText: parseResultText(toolCall.result),
  }
}

/** 从派发卡参数中提取任务描述（供展示层直接使用） */
export function taskFromToolCall(toolCall: ToolCallDisplay): string | undefined {
  return parseTaskFromArgs(toolCall.argsJson)
}
