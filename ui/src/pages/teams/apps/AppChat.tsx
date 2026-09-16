import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowLeftOutlined,
  BulbOutlined,
  CheckOutlined,
  CloseOutlined,
  ControlOutlined,
  DeleteOutlined,
  MenuOutlined,
  PlusOutlined,
  RobotFilled,
  SearchOutlined,
  SendOutlined,
  SettingOutlined,
  StopOutlined,
  ThunderboltFilled,
  UserSwitchOutlined,
} from '@ant-design/icons'
import { Badge, Button, Input, Popconfirm, Tag, Tooltip, theme } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
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
  getAppUserConfig,
  updateAppSessionPrompt,
  type AppSessionItem,
} from '@/api/app'
import { getMyPrompts, getTeamPrompts, type PromptItem } from '@/api/prompt'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage } from './chat/ChatMessageList'
import { AppUserSettings } from './chat/AppUserSettings'
import { chatCssVars } from './chat/chatCssVars'
import './app-chat.css'

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

  const [expertsOpen, setExpertsOpen] = useState(false)
  const [experts, setExperts] = useState<PromptItem[]>([])
  const [expertSearch, setExpertSearch] = useState('')
  const [selectedPromptId, setSelectedPromptId] = useState(0)
  const [promptSaving, setPromptSaving] = useState(false)

  // 用户级应用配置：新会话默认专家（跨会话复用），技能在服务端自动并集生效
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [savedPromptId, setSavedPromptId] = useState(0)

  const appName = detail?.name ?? ''
  const appAvatar = resolveStorageUrl(detail?.avatarPath ?? null)
  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  const cssVars = useMemo(() => chatCssVars(token), [token])

  const selectedExpert = useMemo(
    () => experts.find((e) => e.promptId === selectedPromptId),
    [experts, selectedPromptId],
  )

  const visibleExperts = useMemo(() => {
    const kw = expertSearch.trim().toLowerCase()
    if (!kw) return experts
    return experts.filter(
      (e) => (e.name ?? '').toLowerCase().includes(kw) || (e.description ?? '').toLowerCase().includes(kw),
    )
  }, [experts, expertSearch])

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
    // 加载用户级应用配置，作为新会话的默认专家；技能由服务端在对话时自动并集生效
    getAppUserConfig(appId)
      .then((cfg) => {
        setSavedPromptId(cfg.promptId)
        setSelectedPromptId((prev) => (prev === 0 ? cfg.promptId : prev))
      })
      .catch(() => undefined)
    return () => {
      if (agentRef.current) abortAppChat(agentRef.current)
    }
  }, [appId, loadSessions])

  useEffect(() => {
    if (!teamId) return
    // 可用专家 = 本人个人提示词 + 当前团队提示词；加载失败静默，侧边栏展示空态
    Promise.all([getMyPrompts(), getTeamPrompts(teamId).catch(() => [])])
      .then(([mine, team]) => setExperts([...mine, ...team]))
      .catch(() => undefined)
  }, [teamId])

  useEffect(() => {
    const node = scrollRef.current
    if (node) node.scrollTop = node.scrollHeight
  }, [messages])

  const selectSession = useCallback(async (sessionId: string, promptId?: number) => {
    setActiveSessionId(sessionId)
    setSelectedPromptId(promptId ?? 0)
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
    // 新会话默认使用用户配置的专家（应用设置中保存的偏好）
    setSelectedPromptId(savedPromptId)
    setSidebarOpen(false)
    inputRef.current?.focus()
  }, [savedPromptId])

  // 选择/取消专家：已有会话即时绑定，未发送过消息时先记在本地，首轮发送时随会话一并创建
  const toggleExpert = useCallback(
    async (promptId: number) => {
      const next = selectedPromptId === promptId ? 0 : promptId
      const sessionId = activeSessionId
      if (!sessionId) {
        setSelectedPromptId(next)
        return
      }
      setPromptSaving(true)
      try {
        await updateAppSessionPrompt(sessionId, next)
        setSelectedPromptId(next)
        setSessions((prev) => prev.map((s) => (String(s.sessionId) === sessionId ? { ...s, promptId: next } : s)))
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setPromptSaving(false)
      }
    },
    [activeSessionId, selectedPromptId],
  )

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
        // 首轮消息：会话与会话绑定的专家提示词一并创建，避免首条消息丢失专家设定
        sessionId = await createAppSession(appId, undefined, selectedPromptId)
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
  }, [activeSessionId, appId, input, loadSessions, selectedPromptId, sending, t])

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
      {(sidebarOpen || expertsOpen || settingsOpen) && (
        <div
          className={`moai-chat__overlay${expertsOpen || settingsOpen ? ' is-visible' : ''}`}
          onClick={() => { setSidebarOpen(false); setExpertsOpen(false); setSettingsOpen(false) }}
        />
      )}
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
                  onClick={() => void selectSession(id, session.promptId ?? 0)}
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

      <aside className={`moai-chat__experts${expertsOpen ? ' is-open' : ''}`}>
        <div className="moai-chat__sidebar-head">
          <span className="moai-chat__sidebar-title">{t('appChat.experts')}</span>
          <button type="button" className="moai-chat__experts-close" onClick={() => setExpertsOpen(false)} aria-label={t('appChat.close')}>
            <CloseOutlined />
          </button>
        </div>
        <div className="moai-chat__experts-hint">{t('appChat.expertsHint')}</div>
        <div className="moai-chat__experts-search">
          <Input
            allowClear
            size="small"
            prefix={<SearchOutlined />}
            placeholder={t('appChat.expertSearchPlaceholder')}
            value={expertSearch}
            onChange={(e) => setExpertSearch(e.target.value)}
          />
        </div>
        <div className="moai-chat__experts-list">
          {visibleExperts.length === 0 ? (
            <div className="moai-chat__sessions-empty">{t('appChat.expertsEmpty')}</div>
          ) : (
            visibleExperts.map((item) => {
              const id = Number(item.promptId ?? 0)
              const active = id === selectedPromptId
              const avatar = resolveStorageUrl(item.avatarPath ?? null)
              return (
                <div
                  key={id}
                  className={`moai-chat__expert${active ? ' is-active' : ''}`}
                  onClick={() => void toggleExpert(id)}
                >
                  <div className="moai-chat__expert-avatar">
                    {avatar ? (
                      <img src={avatar} alt={item.name ?? ''} style={{ width: 30, height: 30, objectFit: 'cover' }} />
                    ) : (
                      <div className="moai-chat__expert-avatar-fallback">{(item.name ?? '?').slice(0, 1).toUpperCase()}</div>
                    )}
                  </div>
                  <div className="moai-chat__expert-body">
                    <div className="moai-chat__expert-name">
                      <span className="moai-chat__expert-name-text">{item.name || t('appChat.untitled')}</span>
                      <Tag className={`moai-chat__expert-source${(item.teamId ?? 0) > 0 ? ' is-team' : ''}`}>
                        {t((item.teamId ?? 0) > 0 ? 'appChat.expertTeam' : 'appChat.expertPersonal')}
                      </Tag>
                    </div>
                    {item.description && <div className="moai-chat__expert-desc">{item.description}</div>}
                  </div>
                  {active && <CheckOutlined className="moai-chat__expert-check" />}
                </div>
              )
            })
          )}
        </div>
      </aside>

      <AppUserSettings
        open={settingsOpen}
        appId={appId}
        teamId={teamId}
        experts={experts}
        onClose={() => setSettingsOpen(false)}
        onSaved={(promptId) => {
          setSavedPromptId(promptId)
          // 尚未进入会话时（正在准备新对话），立即应用为当前专家
          if (!activeSessionId) setSelectedPromptId(promptId)
        }}
      />

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
          <Tooltip title={t('appChat.experts')}>
            <Badge dot={selectedPromptId > 0} offset={[-2, 4]}>
              <Button
                type={expertsOpen ? 'primary' : 'text'}
                icon={<UserSwitchOutlined />}
                aria-label={t('appChat.experts')}
                onClick={() => { setSettingsOpen(false); setExpertsOpen((v) => !v) }}
              />
            </Badge>
          </Tooltip>
          <Tooltip title={t('appChat.userSettings')}>
            <Badge dot={false} offset={[-2, 4]}>
              <Button
                type={settingsOpen ? 'primary' : 'text'}
                icon={<ControlOutlined />}
                aria-label={t('appChat.userSettings')}
                onClick={() => { setExpertsOpen(false); setSettingsOpen((v) => !v) }}
              />
            </Badge>
          </Tooltip>
          <Tooltip title={t('appManage.manage')}>
            <Button
              type="text"
              icon={<SettingOutlined />}
              onClick={() => navigate(`/team/${teamId}/app/${appId}`)}
            />
          </Tooltip>
        </header>

        <div className="moai-chat__scroll" ref={scrollRef}>
          {loading ? null : (
            <ChatMessageList
              messages={messages}
              sending={sending}
              appAvatar={appAvatar}
              userName={userName}
              userAvatar={userAvatar}
              onCopy={(text) => void copyMessage(text)}
              emptyState={
                <div className="moai-chat__stream">
                  <div className="moai-chat__hero">
                    <div className="moai-chat__hero-badge">
                      <ThunderboltFilled />
                    </div>
                    <h2 className="moai-chat__hero-title">{t('appChat.welcomeTitle')}</h2>
                    <p className="moai-chat__hero-subtitle">{t('appChat.welcomeSubtitle', { name: appName || t('appChat.title') })}</p>
                    <div className="moai-chat__suggestions">
                      {suggestions.map((text) => (
                        <button key={text} type="button" className="moai-chat__suggestion" onClick={() => applySuggestion(text)}>
                          <BulbOutlined className="moai-chat__suggestion-icon" />
                          {text}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              }
            />
          )}
        </div>

        <div className="moai-chat__composer-wrap">
          <div className="moai-chat__composer-inner">
            {selectedExpert && (
              <div className="moai-chat__expert-chip">
                <UserSwitchOutlined className="moai-chat__expert-chip-icon" />
                <span className="moai-chat__expert-chip-label">{t('appChat.currentExpert')}</span>
                <span className="moai-chat__expert-chip-name">{selectedExpert.name}</span>
                <button
                  type="button"
                  className="moai-chat__expert-chip-clear"
                  disabled={promptSaving}
                  onClick={() => void toggleExpert(selectedPromptId)}
                  aria-label={t('appChat.clearExpert')}
                >
                  <CloseOutlined />
                </button>
              </div>
            )}
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
