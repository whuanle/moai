import { HttpAgent } from '@ag-ui/client'
import { Env } from '@/config/env'
import { useAppStore } from '@/store/app'

/**
 * AG-UI 对话回调：流式增量、工具调用、结束与错误。
 * 服务端以 threadId 作为会话 id，历史由服务端管理，因此每轮只发送最新用户消息。
 */
export interface AgentChatHandlers {
  onDelta?: (text: string) => void
  onToolCall?: (name: string) => void
  onDone?: () => void
  onError?: (message: string) => void
}

/** 构建指向应用对话 AG-UI 端点（/api/agent/{appId}/chat，SSE）的客户端，threadId 即会话 id */
export function createAppChatAgent(appId: string, threadId: string): HttpAgent {
  const token = useAppStore.getState().userInfo?.accessToken
  const url = `${Env.serverUrl}/api/agent/${appId}/chat`

  return new HttpAgent({
    url,
    threadId,
    headers: token ? { Authorization: `Bearer ${token}` } : {},
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
      onToolCallStartEvent: ({ event }) => handlers.onToolCall?.(event.toolCallName),
      onRunErrorEvent: ({ event }) => handlers.onError?.(event.message),
      onRunFinishedEvent: () => handlers.onDone?.(),
    },
  )
}

/** 中止当前对话流 */
export function abortAppChat(agent: HttpAgent): void {
  agent.abortRun()
}
