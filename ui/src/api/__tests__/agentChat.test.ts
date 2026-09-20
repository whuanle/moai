import { describe, expect, it, vi } from 'vitest'
import { runAppChat } from '@/api/agentChat'
import type { HttpAgent } from '@ag-ui/client'

/** 用假 agent 驱动 AG-UI 订阅回调，验证多段文本按 messageId 累积而非互相覆盖 */
function fakeAgent(script: (subscriber: Record<string, (p: unknown) => void>) => void) {
  return {
    setMessages: vi.fn(),
    runAgent: vi.fn(async (_params: unknown, subscriber: Record<string, (p: unknown) => void>) => {
      script(subscriber)
    }),
  } as unknown as HttpAgent
}

describe('runAppChat', () => {
  it('单段文本逐步累积为完整回复', async () => {
    const onDelta = vi.fn()
    const agent = fakeAgent((subscriber) => {
      subscriber.onTextMessageStartEvent({ event: { messageId: 'm1' } })
      subscriber.onTextMessageContentEvent({ event: { messageId: 'm1' }, textMessageBuffer: '你好' })
      subscriber.onTextMessageContentEvent({ event: { messageId: 'm1' }, textMessageBuffer: '你好，世界' })
      subscriber.onTextMessageEndEvent({ event: { messageId: 'm1' }, textMessageBuffer: '你好，世界' })
    })

    await runAppChat(agent, 'hi', { onDelta })

    expect(onDelta).toHaveBeenLastCalledWith('你好，世界')
  })

  it('多段文本（文本→工具→文本）拼接而非丢弃前段', async () => {
    const onDelta = vi.fn()
    const onToolCall = vi.fn()
    const agent = fakeAgent((subscriber) => {
      subscriber.onTextMessageStartEvent({ event: { messageId: 'm1' } })
      subscriber.onTextMessageContentEvent({ event: { messageId: 'm1' }, textMessageBuffer: '我先查一下。' })
      subscriber.onTextMessageEndEvent({ event: { messageId: 'm1' }, textMessageBuffer: '我先查一下。' })
      subscriber.onToolCallStartEvent({ event: { toolCallName: 'search_knowledge_base' } })
      subscriber.onTextMessageStartEvent({ event: { messageId: 'm2' } })
      subscriber.onTextMessageContentEvent({ event: { messageId: 'm2' }, textMessageBuffer: '答案如下。' })
      subscriber.onTextMessageEndEvent({ event: { messageId: 'm2' }, textMessageBuffer: '答案如下。' })
    })

    await runAppChat(agent, 'hi', { onDelta, onToolCall })

    expect(onDelta).toHaveBeenLastCalledWith('我先查一下。答案如下。')
    expect(onToolCall).toHaveBeenCalledWith('search_knowledge_base')
  })

  it('call_tool 参数流结束时回调解析后的完整参数（含真实工具名）', async () => {
    const onToolCallEnd = vi.fn()
    const agent = fakeAgent((subscriber) => {
      subscriber.onToolCallStartEvent({ event: { toolCallId: 'tc-1', toolCallName: 'call_tool' } })
      subscriber.onToolCallEndEvent({
        event: { toolCallId: 'tc-1', toolCallName: 'call_tool' },
        toolCallName: 'call_tool',
        toolCallArgs: { toolName: 'sandbox_run_code', argumentsJson: '{"language":"python"}' },
      })
    })

    await runAppChat(agent, 'hi', { onToolCallEnd })

    expect(onToolCallEnd).toHaveBeenCalledWith({
      id: 'tc-1',
      name: 'call_tool',
      args: { toolName: 'sandbox_run_code', argumentsJson: '{"language":"python"}' },
    })
  })

  it('仅发送本轮用户消息（历史由服务端管理）', async () => {
    const agent = fakeAgent(() => {})
    await runAppChat(agent, '本轮问题', { onDelta: vi.fn() })

    const setMessages = agent.setMessages as unknown as ReturnType<typeof vi.fn>
    expect(setMessages).toHaveBeenCalledTimes(1)
    const messages = setMessages.mock.calls[0][0]
    expect(messages).toHaveLength(1)
    expect(messages[0]).toMatchObject({ role: 'user', content: '本轮问题' })
  })
})
