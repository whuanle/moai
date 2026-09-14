import { useCallback, useEffect, useRef, useState } from 'react'
import { ClearOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Button, Input, Popconfirm, Tooltip, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { fontSize, radius, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { createDebugSession } from '@/api/app'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage } from './ChatMessageList'
import { chatCssVars } from './chatCssVars'

const { Text } = Typography

// 与后端 AppAgentConstants.SessionResolveErrorMarker 保持一致
const SESSION_NOT_FOUND_MARKER = '[[moai:session-not-found]]'

export interface AppDebugChatProps {
  appId: string
  appAvatar?: string
}

/**
 * 调试对话面板：进入即创建 Redis 调试会话（不落库、不计用量），
 * 页面刷新即放弃会话 id 并重新创建；后端注册表过期时自动重建一次并重试。
 */
export function AppDebugChat({ appId, appAvatar }: AppDebugChatProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const userInfo = useAppStore((state) => state.userInfo)
  const agentRef = useRef<HttpAgent | null>(null)
  const sessionIdRef = useRef('')
  const cancelledRef = useRef(false)
  const expiredRef = useRef(false)

  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)

  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  const ensureSession = useCallback(async (): Promise<string> => {
    if (sessionIdRef.current) return sessionIdRef.current
    const id = await createDebugSession(appId)
    sessionIdRef.current = id
    return id
  }, [appId])

  useEffect(() => {
    cancelledRef.current = false
    sessionIdRef.current = ''
    setMessages([])
    void ensureSession().catch(() => undefined)
    return () => {
      cancelledRef.current = true
      if (agentRef.current) abortAppChat(agentRef.current)
    }
  }, [appId, ensureSession])

  const runOnce = useCallback(
    async (text: string, assistantId: string, sessionId: string) => {
      agentRef.current = createAppChatAgent(appId, sessionId)
      await runAppChat(agentRef.current, text, {
        onDelta: (buffer) => {
          if (buffer.includes(SESSION_NOT_FOUND_MARKER)) {
            expiredRef.current = true
          }
          const content = buffer.split(SESSION_NOT_FOUND_MARKER).join('')
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content } : m)))
        },
        onToolCall: (name) =>
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, toolCalls: [...(m.toolCalls ?? []), name] } : m))),
        onError: (message) =>
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appDebug.runError') } : m))),
      })
    },
    [appId, t],
  )

  const send = useCallback(async () => {
    const text = input.trim()
    if (!text || sending) return

    const assistantId = crypto.randomUUID()
    setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: 'user', content: text }, { id: assistantId, role: 'assistant', content: '' }])
    setInput('')
    setSending(true)
    expiredRef.current = false

    const resetAssistant = () =>
      setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: '', toolCalls: [] } : m)))

    try {
      const sessionId = await ensureSession()
      if (cancelledRef.current) return
      await runOnce(text, assistantId, sessionId)

      // 注册表过期：重建一次调试会话后重试一次
      if (expiredRef.current && !cancelledRef.current) {
        expiredRef.current = false
        resetAssistant()
        sessionIdRef.current = ''
        const retryId = await ensureSession()
        if (cancelledRef.current) return
        await runOnce(text, assistantId, retryId)
      }
    } catch {
      if (!cancelledRef.current) {
        setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appDebug.runError') } : m)))
      }
    } finally {
      if (!cancelledRef.current) setSending(false)
    }
  }, [ensureSession, input, runOnce, sending, t])

  const stop = useCallback(() => {
    if (agentRef.current) abortAppChat(agentRef.current)
    setSending(false)
  }, [])

  const clear = useCallback(async () => {
    if (agentRef.current) abortAppChat(agentRef.current)
    sessionIdRef.current = ''
    setMessages([])
    setSending(false)
    await ensureSession().catch(() => undefined)
  }, [ensureSession])

  const copy = useCallback(
    async (text: string) => {
      try {
        await navigator.clipboard.writeText(text)
        feedback.success(t('appChat.copied'))
      } catch {
        // 忽略剪贴板不可用
      }
    },
    [t],
  )

  return (
    <div style={{ ...chatCssVars(token), display: 'flex', flexDirection: 'column', height: '100%', minHeight: 420 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm, marginBottom: spacing.sm }}>
        <div>
          <Text strong>{t('appDebug.title')}</Text>
          <div>
            <Text type="secondary" style={{ fontSize: fontSize.xs }}>
              {t('appDebug.hint')}
            </Text>
          </div>
        </div>
        <Tooltip title={t('appDebug.clear')}>
          <Popconfirm
            title={t('appDebug.clearConfirm')}
            onConfirm={() => void clear()}
            okText={t('appManage.confirm')}
            cancelText={t('appManage.cancel')}
          >
            <Button type="text" size="small" icon={<ClearOutlined />} />
          </Popconfirm>
        </Tooltip>
      </div>

      <div style={{ flex: 1, overflow: 'auto', border: `1px solid ${token.colorBorderSecondary}`, borderRadius: radius.default, padding: spacing.sm }}>
        <ChatMessageList
          messages={messages}
          sending={sending}
          appAvatar={appAvatar}
          userName={userName}
          userAvatar={userAvatar}
          onCopy={(text) => void copy(text)}
          emptyState={
            <div style={{ padding: spacing.lg, textAlign: 'center' }}>
              <Text type="secondary">{t('appDebug.empty')}</Text>
            </div>
          }
        />
      </div>

      <div style={{ display: 'flex', gap: spacing.sm, marginTop: spacing.sm }}>
        <Input.TextArea
          autoSize={{ minRows: 1, maxRows: 6 }}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onPressEnter={(e) => {
            if (!e.shiftKey) {
              e.preventDefault()
              void send()
            }
          }}
          placeholder={t('appDebug.placeholder')}
        />
        {sending ? (
          <Button icon={<StopOutlined />} onClick={stop}>
            {t('appDebug.stop')}
          </Button>
        ) : (
          <Button type="primary" icon={<SendOutlined />} disabled={!input.trim()} onClick={() => void send()}>
            {t('appDebug.send')}
          </Button>
        )}
      </div>
    </div>
  )
}
