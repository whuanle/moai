import { HttpAgent } from '@ag-ui/client'
import { Env } from '@/config/env'
import { useAppStore } from '@/store/app'

/** 工具审批模式：auto=自动执行；approval=重要工具挂起等待人工批准（与后端契约一致） */
export type ToolApprovalMode = 'auto' | 'approval'

/** AG-UI 工具调用信息：name 为元工具名（call_tool），args 为解析后的参数对象 */
export interface AgentToolCallInfo {
  id: string
  name: string
  args?: Record<string, unknown>
}

/** AG-UI 自定义事件名：流程应用执行过程（后端 WorkflowProgressContent 映射） */
export const WORKFLOW_CHAT_EVENT_NAME = 'moai.workflow'

/** AG-UI 自定义事件名：子智能体派发过程（后端 SubAgentChatStreamContract 映射，经派发器旁路合并注入） */
export const SUBAGENT_CHAT_EVENT_NAME = 'moai.subagent'

/** 流程应用执行过程事件负载（与后端 WorkflowAppChatClient 映射契约一致） */
export interface WorkflowChatEventPayload {
  event: 'started' | 'node' | 'suspended' | 'completed'
  /** started 事件：流程实例 id */
  instanceId?: string
  nodeKey?: string
  nodeName?: string
  nodeType?: string
  nodeState?: 'pending' | 'running' | 'completed' | 'failed' | 'skipped'
  errorMessage?: string | null
  elapsedMilliseconds?: number | null
  attempt?: number
  /** suspended 事件：挂起原因 */
  message?: string
}

/** 子智能体派发过程事件负载（与后端 SubAgentChatEvent 契约一致，camelCase；kind 为枚举 camelCase 字符串） */
export interface SubAgentChatEventPayload {
  kind: 'started' | 'text' | 'toolCall' | 'toolResult' | 'completed' | 'timeout' | 'failed'
  /** 一次派发的追踪 id（同轮多次派发各自独立） */
  traceId: string
  /** 子智能体 key（具名派发为 team_agent.subs 项 key；通用子代理固定 general） */
  agentKey: string
  agentName: string
  /** text 事件：正文增量 */
  text?: string
  /** tool_call / tool_result 事件：工具名（call_tool 已解析为内层真实工具名） */
  toolName?: string
  /** tool_result 事件：结果摘要（截断） */
  result?: string
  /** completed/timeout/failed 事件：结束文本（结论/部分结果/错误） */
  finalText?: string
  elapsedMilliseconds?: number
}

/**
 * AG-UI 对话回调：流式增量、工具调用（含解析后参数）、流程执行过程、结束与错误。
 * 服务端以 threadId 作为会话 id，历史由服务端管理，因此每轮只发送最新用户消息。
 */
export interface AgentChatHandlers {
  onDelta?: (text: string) => void
  /** 工具调用开始（此时参数未必完整）：id 用于流式占位卡与参数流结束后的更新去重 */
  onToolCall?: (info: { id: string; name: string }) => void
  /** 工具调用参数流结束：args 为完整解析对象（call_tool 时含真实 toolName/argumentsJson） */
  onToolCallEnd?: (info: AgentToolCallInfo) => void
  /** 流程应用执行过程事件（仅流程应用产生；Agent 应用对话不触发） */
  onWorkflowEvent?: (evt: WorkflowChatEventPayload) => void
  /** 子智能体派发过程事件（开启子代理协作且派发时产生，实时嵌套活动卡数据源） */
  onSubAgentEvent?: (evt: SubAgentChatEventPayload) => void
  onDone?: () => void
  onError?: (message: string) => void
}

/** 构建指向应用对话 AG-UI 端点（/api/agent/{appId}/chat，SSE）的客户端，threadId 即会话 id */
export function createAppChatAgent(
  appId: string,
  threadId: string,
  options?: { toolApprovalMode?: ToolApprovalMode; workflowDraft?: boolean; uiTools?: boolean; modelId?: string | null; teamAgentId?: string | null },
): HttpAgent {
  const token = useAppStore.getState().userInfo?.accessToken
  const url = `${Env.serverUrl}/api/agent/${appId}/chat`

  return new HttpAgent({
    url,
    threadId,
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      // 审批模式随每轮对话请求下发（未携带时后端按 auto 处理）
      'X-Moai-Tool-Approval': options?.toolApprovalMode ?? 'auto',
      // 流程应用「调试」Tab 携带：按最新草稿执行（免发布，仅团队管理员；未携带按已发布快照）
      ...(options?.workflowDraft ? { 'X-Moai-Workflow-Draft': '1' } : {}),
      // 前端展示工具（ui_ 前缀，侧边栏渲染）：默认启用；嵌入组件未适配侧边栏前不携带即关闭
      ...(options?.uiTools !== false ? { 'X-Moai-Ui-Tools': '1' } : {}),
      // 用户所选对话模型随每轮下发（ai_model.id uuid）：后端校验须在应用可选列表内且团队可用，否则回退默认模型
      ...(options?.modelId ? { 'X-Moai-Model-Id': options.modelId } : {}),
      // 用户所选团队智能体随每轮下发（team_agent.id uuid）：后端校验须在应用绑定列表内，否则回退默认智能体
      ...(options?.teamAgentId ? { 'X-Moai-Team-Agent-Id': options.teamAgentId } : {}),
    },
  })
}

/** 发送一轮用户消息并消费 AG-UI 事件流 */
export async function runAppChat(
  agent: HttpAgent,
  text: string,
  handlers: AgentChatHandlers,
): Promise<void> {
  agent.setMessages([{ id: crypto.randomUUID(), role: 'user', content: text }])

  // textMessageBuffer 是「按 messageId」的缓冲：模型一轮内可能产出多段文本
  // （文本 → 工具调用 → 文本），必须按 messageId 累积再拼接，否则只会显示最后一段。
  const segments = new Map<string, string>()
  const emit = () => handlers.onDelta?.([...segments.values()].join(''))

  await agent.runAgent(
    {},
    {
      onTextMessageStartEvent: ({ event }) => {
        segments.set(event.messageId, '')
        emit()
      },
      onTextMessageContentEvent: ({ event, textMessageBuffer }) => {
        segments.set(event.messageId, textMessageBuffer)
        emit()
      },
      onTextMessageEndEvent: ({ event, textMessageBuffer }) => {
        segments.set(event.messageId, textMessageBuffer)
        emit()
      },
      onToolCallStartEvent: ({ event }) => handlers.onToolCall?.({ id: event.toolCallId, name: event.toolCallName }),
      onToolCallEndEvent: ({ event, toolCallName, toolCallArgs }) =>
        handlers.onToolCallEnd?.({ id: event.toolCallId, name: toolCallName, args: toolCallArgs }),
      onCustomEvent: ({ event }) => {
        if (event.name === WORKFLOW_CHAT_EVENT_NAME) {
          handlers.onWorkflowEvent?.(event.value as WorkflowChatEventPayload)
        } else if (event.name === SUBAGENT_CHAT_EVENT_NAME) {
          handlers.onSubAgentEvent?.(event.value as SubAgentChatEventPayload)
        }
      },
      onRunErrorEvent: ({ event }) => handlers.onError?.(event.message),
      onRunFinishedEvent: () => handlers.onDone?.(),
    },
  )
}

/** 中止当前对话流 */
export function abortAppChat(agent: HttpAgent): void {
  agent.abortRun()
}
