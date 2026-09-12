import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import {
  ArrowLeftOutlined,
  BulbOutlined,
  CopyOutlined,
  DeleteOutlined,
  MenuOutlined,
  PlusOutlined,
  RobotFilled,
  SendOutlined,
  SettingOutlined,
  StopOutlined,
  ThunderboltFilled,
} from '@ant-design/icons'
import { Button, Input, Popconfirm, Tag, Tooltip, theme } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import {
  createAppSession,
  deleteAppSession,
  getAppDetail,
  getAppSessionMessages,
  getAppSessions,
  type AppSessionItem,
} from '@/api/app'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import './app-chat.css'

interface DisplayMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  toolCalls?: string[]
}

interface AppDetailLite {
  name?: string | null
  avatarPath?: string | null
  publishStatus?: number | null
}

/**
 * Agent 应用对话页（沉浸式）：左侧会话列表，右侧流式对话。
 * threadId 即会话 id，历史由服务端管理，每轮仅发送最新用户消息。
 */
export function AppChat() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''
  const { token } = theme.useToken()
  const userInfo = useAppStore((state) => state.userInfo)

  const agentRef = useRef<HttpAgent | null>(null)
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<TextAreaRef | null>(null)

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetailLite | null>(null)
  const [sessions, setSessions] = useState<AppSessionItem[]>([])
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [activeSessionId, setActiveSessionId] = useState('')
  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const appName = detail?.name ?? ''
  const appAvatar = resolveStorageUrl(detail?.avatarPath ?? null)
  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  const cssVars = useMemo(
    () =>
      ({
        '--mc-primary': token.colorPrimary,
        '--mc-primary-bg': token.colorPrimaryBg,
        '--mc-primary-border': token.colorPrimaryBorder,
        '--mc-info': token.colorInfo,
        '--mc-bg': token.colorBgContainer,
        '--mc-bg-layout': token.colorBgLayout,
        '--mc-bg-elevated': token.colorBgElevated,
        '--mc-text': token.colorText,
        '--mc-text-secondary': token.colorTextSecondary,
        '--mc-text-tertiary': token.colorTextTertiary,
        '--mc-border': token.colorBorderSecondary,
        '--mc-border-strong': token.colorBorder,
        '--mc-fill': token.colorFillQuaternary,
        '--mc-fill-secondary': token.colorFillTertiary,
        '--mc-code-bg': token.colorFillQuaternary,
        '--mc-radius': `${token.borderRadiusLG}px`,
        '--mc-radius-sm': `${token.borderRadius}px`,
        '--mc-shadow': token.boxShadow,
        '--mc-shadow-lg': token.boxShadowSecondary,
      }) as CSSProperties,
    [token],
  )

  const suggestions = useMemo(
    () => [t('appChat.suggestion1'), t('appChat.suggestion2'), t('appChat.suggestion3')],
    [t],
  )

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
    if (!appId) return
    setLoading(true)
    getAppDetail(appId)
      .then((res) => setDetail(res as unknown as AppDetailLite))
      .catch(() => undefined)
      .finally(() => setLoading(false))
    void loadSessions()
    return () => {
      if (agentRef.current) abortAppChat(agentRef.current)
    }
  }, [appId, loadSessions])

  useEffect(() => {
    const node = scrollRef.current
    if (node) node.scrollTop = node.scrollHeight
  }, [messages])

  const selectSession = useCallback(async (sessionId: string) => {
    setActiveSessionId(sessionId)
    setSidebarOpen(false)
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

  const newSession = useCallback(async () => {
    setActiveSessionId('')
    setMessages([])
    setSidebarOpen(false)
    inputRef.current?.focus()
  }, [])

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

    agentRef.current = createAppChatAgent(appId, sessionId)

    try {
      await runAppChat(agentRef.current, text, {
        onDelta: (buffer) => {
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m)))
        },
        onToolCall: (name) => {
          setMessages((prev) =>
            prev.map((m) => (m.id === assistantId ? { ...m, toolCalls: [...(m.toolCalls ?? []), name] } : m)),
          )
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

  const applySuggestion = useCallback((text: string) => {
    setInput(text)
    inputRef.current?.focus()
  }, [])

  return (
    <div className="moai-chat" style={cssVars}>
      {sidebarOpen && <div className="moai-chat__overlay" onClick={() => setSidebarOpen(false)} />}
      <aside className={`moai-chat__sidebar${sidebarOpen ? ' is-open' : ''}`}>
        <div className="moai-chat__sidebar-head">
          <span className="moai-chat__sidebar-title">{t('appChat.sessions')}</span>
          <button type="button" className="moai-chat__new-btn" onClick={() => void newSession()}>
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
        <header className="moai-chat__topbar">
          <Button
            className="moai-chat__menu-btn"
            type="text"
            icon={<MenuOutlined />}
            onClick={() => setSidebarOpen((v) => !v)}
          />
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(`/team/${teamId}/apps`)}
          />
          <div className="moai-chat__brand">
            <div className="moai-chat__brand-avatar">
              {appAvatar ? (
                <img src={appAvatar} alt={appName} style={{ width: 36, height: 36, objectFit: 'cover' }} />
              ) : (
                <div
                  style={{
                    width: 36,
                    height: 36,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    background: 'linear-gradient(135deg, var(--mc-primary), var(--mc-info))',
                    color: '#fff',
                    fontSize: 17,
                  }}
                >
                  {appName.slice(0, 1).toUpperCase() || <RobotFilled />}
                </div>
              )}
            </div>
            <div style={{ minWidth: 0 }}>
              <div className="moai-chat__brand-name">{appName || t('appChat.title')}</div>
            </div>
            {detail?.publishStatus === 1 && <Tag color="green">{t('appManage.published')}</Tag>}
          </div>
          <div className="moai-chat__topbar-spacer" />
          <Tooltip title={t('appManage.manage')}>
            <Button
              type="text"
              icon={<SettingOutlined />}
              onClick={() => navigate(`/team/${teamId}/app/${appId}`)}
            />
          </Tooltip>
        </header>

        <div className="moai-chat__scroll" ref={scrollRef}>
          <div className="moai-chat__stream">
            {loading ? null : messages.length === 0 ? (
              <div className="moai-chat__hero">
                <div className="moai-chat__hero-badge">
                  <ThunderboltFilled />
                </div>
                <h2 className="moai-chat__hero-title">{t('appChat.welcomeTitle')}</h2>
                <p className="moai-chat__hero-subtitle">{t('appChat.welcomeSubtitle', { name: appName || t('appChat.title') })}</p>
                <div className="moai-chat__suggestions">
                  {suggestions.map((text) => (
                    <button
                      key={text}
                      type="button"
                      className="moai-chat__suggestion"
                      onClick={() => applySuggestion(text)}
                    >
                      <BulbOutlined className="moai-chat__suggestion-icon" />
                      {text}
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              messages.map((m, index) => {
                const isLast = index === messages.length - 1
                const streaming = sending && isLast && m.role === 'assistant'
                return (
                  <div key={m.id} className={`moai-chat__row moai-chat__row--${m.role}`}>
                    {m.role === 'assistant' ? (
                      <div className="moai-chat__avatar moai-chat__avatar--ai">
                        {appAvatar ? (
                          <img src={appAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} />
                        ) : (
                          <RobotFilled />
                        )}
                      </div>
                    ) : (
                      <div className="moai-chat__avatar moai-chat__avatar--user">
                        {userAvatar ? (
                          <img src={userAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} />
                        ) : (
                          userName.slice(0, 1).toUpperCase()
                        )}
                      </div>
                    )}

                    <div className="moai-chat__msg">
                      {m.role === 'assistant' ? (
                        <>
                          {(m.toolCalls?.length ?? 0) > 0 && (
                            <div className="moai-chat__toolbar">
                              {m.toolCalls!.map((name, i) => (
                                <span key={`${name}-${i}`} className="moai-chat__tool-chip">
                                  <ThunderboltFilled />
                                  {name}
                                </span>
                              ))}
                            </div>
                          )}
                          <div className="moai-chat__assistant">
                            {m.content ? (
                              <div className="moai-chat__markdown">
                                <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>
                              </div>
                            ) : streaming ? (
                              <span className="moai-chat__typing">
                                <span />
                                <span />
                                <span />
                              </span>
                            ) : null}
                            {streaming && m.content && <span className="moai-chat__caret" />}
                          </div>
                          {m.content && !streaming && (
                            <div className="moai-chat__actions">
                              <Tooltip title={t('appChat.copy')}>
                                <Button
                                  type="text"
                                  size="small"
                                  icon={<CopyOutlined />}
                                  onClick={() => void copyMessage(m.content)}
                                />
                              </Tooltip>
                            </div>
                          )}
                        </>
                      ) : (
                        <div className="moai-chat__bubble">{m.content}</div>
                      )}
                    </div>
                  </div>
                )
              })
            )}
          </div>
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
                placeholder={t('appChat.placeholder')}
              />
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
            <div className="moai-chat__hint">{t('appChat.enterHint')}</div>
          </div>
        </div>
      </main>
    </div>
  )
}
