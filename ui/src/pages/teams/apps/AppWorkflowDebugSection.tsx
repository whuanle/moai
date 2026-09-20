import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { DeleteOutlined, PlusOutlined, SendOutlined, StopOutlined, ThunderboltFilled } from '@ant-design/icons'
import { Alert, Button, Input, Popconfirm, theme } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import {
  createAppSession,
  deleteAppSession,
  getAppSessionMessages,
  getAppSessions,
  type AppSessionItem,
} from '@/api/app'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage } from './chat/ChatMessageList'
import { chatCssVars } from './chat/chatCssVars'
import type { AppDetail } from './AppConfigSection'
import './app-chat.css'
import './app-workflow-debug.css'

interface AppDetailLite {
  name?: string | null
  avatarPath?: string | null
  publishStatus?: number | null
  openingStatement?: string | null
  openingStatementEnabled?: boolean | null
}

/** 开场白为前端本地展示消息，不入会话历史，id 固定以便复用 */
const OPENING_MESSAGE_ID = 'opening-statement'

/**
 * 流程应用「调试」分区：左侧会话历史列表 + 右侧流式对话（内嵌于应用工作台）。
 * 一轮消息 = 一次已发布流程执行：用户问题注入开始节点 question，sys.history 由服务端压缩后注入；
 * 复用 app-chat.css 的流式气泡与会话样式，容器高度按工作台内嵌场景计算.
 */
export function AppWorkflowDebugSection({
  appId,
  detail,
}: {
  teamId: number
  appId: string
  detail: AppDetail | null
  canManage: boolean
}) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const cssVars = useMemo(() => chatCssVars(token), [token])
  const userInfo = useAppStore((state) => state.userInfo)

  const agentRef = useRef<HttpAgent | null>(null)
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<TextAreaRef | null>(null)

  const [sessions, setSessions] = useState<AppSessionItem[]>([])
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [activeSessionId, setActiveSessionId] = useState('')
  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)

  const appName = detail?.name ?? ''
  const appAvatar = resolveStorageUrl(detail?.avatarPath ?? null)
  const published = detail?.publishStatus === 1
  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  // 应用配置的开场白：仅新会话开始时本地展示，不参与会话历史与服务端上下文（详情响应含扩展字段）
  const openingMessage = useMemo<DisplayMessage | null>(() => {
    const lite = (detail ?? {}) as AppDetailLite
    const text = (lite.openingStatement ?? '').trim()
    if (!lite.openingStatementEnabled || !text) return null
    return { id: OPENING_MESSAGE_ID, role: 'assistant', content: text }
  }, [detail])

  const loadSessions = useCallback(async () => {
    if (!appId) return
    setSessionsLoading(true)
    try {
      setSessions(await getAppSessions(appId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSessionsLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void loadSessions()
    return () => {
      if (agentRef.current) abortAppChat(agentRef.current)
    }
  }, [loadSessions])

  useEffect(() => {
    const node = scrollRef.current
    if (node) node.scrollTop = node.scrollHeight
  }, [messages])

  const selectSession = useCallback(async (sessionId: string) => {
    setActiveSessionId(sessionId)
    try {
      const items = await getAppSessionMessages(sessionId)
      setMessages(
        items
          .filter((m) => m.role === 'user' || (m.role === 'assistant' && (m.content ?? '').trim().length > 0))
          .map((m) => ({
            id: String(m.messageId ?? crypto.randomUUID()),
            role: m.role === 'user' ? 'user' : 'assistant',
            content: m.content ?? '',
          })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  const newSession = useCallback(() => {
    setActiveSessionId('')
    setMessages(openingMessage ? [openingMessage] : [])
    inputRef.current?.focus()
  }, [openingMessage])

  const handleDelete = useCallback(
    async (sessionId: string) => {
      try {
        await deleteAppSession(sessionId)
        if (sessionId === activeSessionId) {
          setActiveSessionId('')
          setMessages([])
        }
        await loadSessions()
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    },
    [activeSessionId, loadSessions],
  )

  const send = useCallback(async () => {
    const text = input.trim()
    if (!text || sending) return

    let sessionId = activeSessionId
    if (!sessionId) {
      try {
        sessionId = await createAppSession(appId)
        setActiveSessionId(sessionId)
      } catch {
        return
      }
    }

    const userMessage: DisplayMessage = { id: crypto.randomUUID(), role: 'user', content: text }
    const assistantId = crypto.randomUUID()
    setMessages((prev) => [...prev, userMessage, { id: assistantId, role: 'assistant', content: '' }])
    setInput('')
    setSending(true)

    agentRef.current = createAppChatAgent(appId, sessionId, { workflowDraft: true })

    try {
      await runAppChat(agentRef.current, text, {
        onDelta: (buffer) => {
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m)))
        },
        onError: (message) => {
          setMessages((prev) =>
            prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appChat.runError') } : m)),
          )
        },
      })
    } catch {
      setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appChat.runError') } : m)))
    } finally {
      setSending(false)
      void loadSessions()
    }
  }, [activeSessionId, appId, input, loadSessions, sending, t])

  const stop = useCallback(() => {
    if (agentRef.current) abortAppChat(agentRef.current)
    setSending(false)
  }, [])

  const copyMessage = useCallback(
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
    <div className="wfd-chat" style={cssVars}>
      <aside className="moai-chat__sidebar" style={{ width: 240 }}>
        <div className="moai-chat__sidebar-head">
          <span className="moai-chat__sidebar-title">{t('appChat.sessions')}</span>
          <button type="button" className="moai-chat__new-btn" onClick={newSession}>
            <PlusOutlined />
            {t('appChat.newChat')}
          </button>
        </div>
        <div className="moai-chat__sessions">
          {!sessionsLoading && sessions.length === 0 ? (
            <div className="moai-chat__sessions-empty">{t('appChat.sessionsEmpty')}</div>
          ) : (
            sessions.map((session) => {
              const id = String(session.sessionId ?? '')
              const active = id === activeSessionId
              return (
                <div
                  key={id}
                  className={`moai-chat__session${active ? ' is-active' : ''}`}
                  onClick={() => void selectSession(id)}
                >
                  <div className="moai-chat__session-body">
                    <div className="moai-chat__session-title">{session.title || t('appChat.untitled')}</div>
                    <div className="moai-chat__session-time">{formatDateTime(session.lastMessageTime)}</div>
                  </div>
                  <div className="moai-chat__session-del">
                    <Popconfirm
                      title={t('appChat.deleteConfirm')}
                      onConfirm={() => void handleDelete(id)}
                      onCancel={(e) => e?.stopPropagation()}
                      okText={t('appManage.confirm')}
                      cancelText={t('appManage.cancel')}
                    >
                      <Button
                        type="text"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                        onClick={(e) => e.stopPropagation()}
                      />
                    </Popconfirm>
                  </div>
                </div>
              )
            })
          )}
        </div>
      </aside>

      <main className="moai-chat__main">
        {!published && (
          <Alert type="info" showIcon message={t('appWorkspace.debugDraftHint')} style={{ borderRadius: 0 }} />
        )}
        <div className="moai-chat__scroll" ref={scrollRef}>
          <ChatMessageList
            messages={messages}
            sending={sending}
            appAvatar={appAvatar}
            userName={userName}
            userAvatar={userAvatar}
            onCopy={(text) => void copyMessage(text)}
            emptyState={
              <div className="moai-chat__stream">
                <div className="moai-chat__landing-hero">
                  <div className="moai-chat__landing-badge">
                    <ThunderboltFilled />
                  </div>
                  <h2 className="moai-chat__landing-title">{appName || t('appChat.title')}</h2>
                  <p className="moai-chat__landing-subtitle">
                    {t('appChat.welcomeSubtitle', { name: appName || t('appChat.title') })}
                  </p>
                </div>
              </div>
            }
          />
        </div>

        <div className="moai-chat__composer-wrap">
          <div className="moai-chat__composer-inner">
            <div className="moai-chat__composer">
              <Input.TextArea
                ref={inputRef}
                variant="borderless"
                autoSize={{ minRows: 1, maxRows: 8 }}
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onPressEnter={(e) => {
                  if (!e.shiftKey) {
                    e.preventDefault()
                    void send()
                  }
                }}
                placeholder={t('appChat.placeholderSendTo', { name: appName || t('appChat.title') })}
              />
              <div className="moai-chat__composer-footer">
                <span />
                <div className="moai-chat__composer-actions">
                  {sending ? (
                    <button type="button" className="moai-chat__send" onClick={stop} aria-label={t('appChat.stop')}>
                      <StopOutlined />
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="moai-chat__send"
                      onClick={() => void send()}
                      disabled={!input.trim()}
                      aria-label={t('appChat.send')}
                    >
                      <SendOutlined />
                    </button>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}
