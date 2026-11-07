import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowRightOutlined,
  BulbOutlined,
  DeleteOutlined,
  EditOutlined,
  MessageOutlined,
  PlusOutlined,
  ThunderboltFilled,
} from '@ant-design/icons'
import { Alert, Popconfirm, Tooltip, theme } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import type { HttpAgent } from '@ag-ui/client'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
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
import { ChatComposer } from './chat/ChatComposer'
import { useChatAttachments } from './chat/useChatAttachments'
import { useWorkflowRunSteps } from './chat/useWorkflowRunSteps'
import { WorkflowRunSteps } from './chat/WorkflowRunSteps'
import { chatCssVars } from './chat/chatCssVars'
import { buildOutgoingText, hasPendingAttachment } from './chat/attachment'
import type { AppDetail } from './AppConfigSection'
import './app-chat.css'
import './app-workflow-debug.css'

/** 开场白为前端本地展示消息，不入会话历史，id 固定以便复用 */
const OPENING_MESSAGE_ID = 'opening-statement'

/**
 * 流程应用「调试」分区：左侧会话列表 + 右侧流式对话（内嵌于工作台全屏外壳）。
 * 一轮消息 = 一次流程执行（按最新草稿，免发布；实例在运行历史按「调试」类型记录）：
 * 界面与真实对话页（AppChat）同构 —— 欢迎态（徽标 + 开场白简报卡 + 输入卡 + 快捷输入胶囊）
 * 与对话态（气泡 + 完整输入卡含附件）复用 moai-chat 系列样式与共享组件，所见即发布后效果。
 */
export function AppWorkflowDebugSection({
  teamId,
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
  const { attachments, handleFiles, removeAttachment, reset: resetAttachments } = useChatAttachments()

  // 执行过程实时状态（AG-UI CustomEvent 推送）
  const { steps: runSteps, instanceId: runInstanceId, running: runRunning, error: runError, reset: runReset, finish: runFinish, handleEvent: runHandleEvent } = useWorkflowRunSteps()

  const appName = detail?.name ?? ''
  const appAvatar = resolveStorageUrl(detail?.avatarPath ?? null)
  const published = detail?.publishStatus === 1
  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  // 应用配置的快捷输入（草稿值实时预览）：欢迎态展示为可点击胶囊，点击即直接发送
  const suggestions = useMemo(
    () => (detail?.quickInputs ?? []).map((x) => x.trim()).filter(Boolean),
    [detail],
  )

  // 应用配置的开场白：仅新会话开始时本地展示，不参与会话历史与服务端上下文（详情响应含扩展字段）
  const openingMessage = useMemo<DisplayMessage | null>(() => {
    const text = (detail?.openingStatement ?? '').trim()
    if (!detail?.openingStatementEnabled || !text) return null
    return { id: OPENING_MESSAGE_ID, role: 'assistant', content: text }
  }, [detail])

  // 会话视图消息：开场白不混入消息流（欢迎态简报卡单独展示）
  const viewMessages = useMemo(() => messages.filter((m) => m.id !== OPENING_MESSAGE_ID), [messages])
  const isLanding = viewMessages.length === 0

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
    setMessages(openingMessage ? [openingMessage] : [])
    void loadSessions()
    return () => {
      if (agentRef.current) abortAppChat(agentRef.current)
    }
    // 开场白随 detail 异步刷新晚于挂载到达，此处仅按 appId 重建现场；开场白由下方补齐
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appId, loadSessions])

  // 开场白随配置加载完成后补展示（消息尚未开始时），不覆盖已开始的调试对话
  useEffect(() => {
    setMessages((prev) => {
      if (prev.length > 0) return prev
      if (!openingMessage && activeSessionId) return prev
      return openingMessage ? [openingMessage] : []
    })
  }, [openingMessage, activeSessionId])

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
          setMessages(openingMessage ? [openingMessage] : [])
        }
        await loadSessions()
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    },
    [activeSessionId, loadSessions, openingMessage],
  )

  const send = useCallback(
    async (override?: string) => {
      const text = (override ?? input).trim()
      if ((!text && attachments.length === 0) || sending) return
      // 附件全部就绪才能发送（上传/提取中等待，失败项需移除）
      if (hasPendingAttachment(attachments) || attachments.some((a) => a.status === 'failed')) return

      let sessionId = activeSessionId
      if (!sessionId) {
        try {
          sessionId = await createAppSession(appId)
          setActiveSessionId(sessionId)
        } catch {
          return
        }
      }

      // 发送文本 = 用户输入 + 附件标记块（文档为提取文本，图片为链接）；气泡渲染时解析回 chip
      const outgoing = buildOutgoingText(text, attachments)
      const userMessage: DisplayMessage = { id: crypto.randomUUID(), role: 'user', content: outgoing }
      const assistantId = crypto.randomUUID()
      setMessages((prev) => [
        ...(prev.length > 0 ? prev : openingMessage ? [openingMessage] : []),
        userMessage,
        { id: assistantId, role: 'assistant', content: '' },
      ])
      setInput('')
      resetAttachments()
      setSending(true)
      runReset()

      // 调试分区按最新草稿执行（后端校验团队管理员；实例按「调试」类型入运行历史）
      agentRef.current = createAppChatAgent(appId, sessionId, { workflowDraft: true })

      try {
        await runAppChat(agentRef.current, outgoing, {
          onDelta: (buffer) => {
            setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m)))
          },
          onWorkflowEvent: runHandleEvent,
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
        runFinish()
        void loadSessions()
      }
    },
    [activeSessionId, appId, attachments, input, loadSessions, openingMessage, resetAttachments, runFinish, runHandleEvent, runReset, sending, t],
  )

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

  // 快捷输入：点击即直接发送，无需先填入输入框（与真实对话页同交互）
  const applySuggestion = useCallback(
    (text: string) => {
      void send(text)
    },
    [send],
  )

  const placeholder = t('appChat.placeholderSendTo', { name: appName || t('appChat.title') })

  // 输入卡（欢迎态居中 / 对话态停靠底部共用）：流程应用无审批模式与模型切换，仅附件 + 发送/停止
  const composerNode = (
    <ChatComposer
      value={input}
      onValueChange={setInput}
      onSubmit={() => void send()}
      sending={sending}
      onStop={stop}
      placeholder={placeholder}
      inputRef={inputRef}
      attachments={attachments}
      onPickFiles={(files) => void handleFiles(files)}
      onRemoveAttachment={removeAttachment}
    />
  )

  const suggestionsNode =
    suggestions.length > 0 && isLanding ? (
      <div className="moai-chat__suggestions">
        <span className="moai-chat__suggestions-label">
          <BulbOutlined />
          {t('appChat.quickAsk')}
        </span>
        <div className="moai-chat__suggestions-grid">
          {suggestions.map((text) => (
            <button key={text} type="button" className="moai-chat__suggestion" onClick={() => applySuggestion(text)}>
              <EditOutlined className="moai-chat__suggestion-icon" />
              <span className="moai-chat__suggestion-text">{text}</span>
              <ArrowRightOutlined className="moai-chat__suggestion-arrow" />
            </button>
          ))}
        </div>
      </div>
    ) : null

  return (
    <div className="wfd-chat" style={cssVars}>
      <aside className="moai-chat__sidebar wfd-chat__sidebar">
        <div className="moai-chat__sidebar-head">
          <span className="moai-chat__sidebar-title">{t('appChat.sessions')}</span>
          <button type="button" className="moai-chat__new-btn" onClick={newSession}>
            <PlusOutlined />
            {t('appChat.newChat')}
          </button>
        </div>
        <div className="moai-chat__sessions">
          {!sessionsLoading && sessions.length === 0 ? (
            <div className="moai-chat__sessions-empty">
              <MessageOutlined className="moai-chat__sessions-empty-icon" />
              <span>{t('appChat.sessionsEmpty')}</span>
            </div>
          ) : (
            sessions.map((session) => {
              const id = String(session.sessionId ?? '')
              const active = id === activeSessionId
              return (
                <div key={id} className={`moai-chat__session${active ? ' is-active' : ''}`}>
                  {/* 点击区放在会话主体：Popconfirm 弹层是行的 React 子节点，事件沿 React 树冒泡回行，
                      若 onClick 挂在行容器上，点「确定/取消」会同时触发选中该会话 */}
                  <div className="moai-chat__session-body" onClick={() => void selectSession(id)}>
                    <div className="moai-chat__session-title">
                      <span className="moai-chat__session-title-text">{session.title || t('appChat.untitled')}</span>
                    </div>
                    <div className="moai-chat__session-time">{formatDateTime(session.lastMessageTime)}</div>
                  </div>
                  <Popconfirm
                    title={t('appChat.deleteConfirm')}
                    onConfirm={() => void handleDelete(id)}
                    onCancel={(e) => e?.stopPropagation()}
                    okText={t('appManage.confirm')}
                    cancelText={t('appManage.cancel')}
                  >
                    <Tooltip title={t('appChat.deleteSession')}>
                      <button
                        type="button"
                        className="moai-chat__session-del"
                        onClick={(e) => e.stopPropagation()}
                        aria-label={t('appChat.deleteSession')}
                      >
                        <DeleteOutlined />
                      </button>
                    </Tooltip>
                  </Popconfirm>
                </div>
              )
            })
          )}
        </div>
      </aside>

      <main className="moai-chat__main">
        {!published && (
          <Alert
            type="info"
            showIcon
            message={t('appWorkspace.debugDraftHint')}
            style={{ borderRadius: 0, flexShrink: 0 }}
          />
        )}
        {isLanding ? (
          <div className="moai-chat__landing wfd-chat__landing">
            <div className="moai-chat__landing-hero">
              <div className="moai-chat__landing-badge">
                {appAvatar ? (
                  <img src={appAvatar} alt={appName} />
                ) : (
                  <ThunderboltFilled />
                )}
              </div>
              <h1 className="moai-chat__landing-title">{appName || t('appChat.title')}</h1>
              {!openingMessage && <p className="moai-chat__landing-subtitle">{t('appChat.welcomeSubtitle', { name: appName || t('appChat.title') })}</p>}
            </div>
            {openingMessage && (
              <div className="moai-chat__landing-briefing">
                <div className="moai-chat__markdown moai-chat__landing-briefing-body">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>{openingMessage.content}</ReactMarkdown>
                </div>
              </div>
            )}
            <div className="moai-chat__landing-composer">
              {composerNode}
              {suggestionsNode}
            </div>
          </div>
        ) : (
          <>
            <div className="moai-chat__scroll wfd-chat__scroll" ref={scrollRef}>
              <ChatMessageList
                messages={viewMessages}
                sending={sending}
                appAvatar={appAvatar}
                userName={userName}
                userAvatar={userAvatar}
                onCopy={(text) => void copyMessage(text)}
              />
              {(runSteps.length > 0 || runError) && (
                <WorkflowRunSteps
                  steps={runSteps}
                  running={runRunning}
                  error={runError}
                  instanceId={runInstanceId}
                  teamId={teamId}
                  appId={appId}
                />
              )}
            </div>
            <div className="moai-chat__composer-wrap wfd-chat__composer-wrap">
              <div className="moai-chat__composer-inner">{composerNode}</div>
            </div>
          </>
        )}
      </main>
    </div>
  )
}
