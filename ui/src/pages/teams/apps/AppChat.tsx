import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowLeftOutlined,
  ArrowUpOutlined,
  CheckOutlined,
  CloseOutlined,
  ControlOutlined,
  DeleteOutlined,
  FireFilled,
  LoadingOutlined,
  MenuOutlined,
  PaperClipOutlined,
  PlusOutlined,
  RobotFilled,
  SafetyCertificateOutlined,
  StopOutlined,
  ThunderboltFilled,
  UserSwitchOutlined,
} from '@ant-design/icons'
import { Button, Input, Popconfirm, Tag, Tooltip, theme } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl, uploadChatFile } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import {
  createAppSession,
  decideAppSessionToolApproval,
  deleteAppSession,
  extractChatAttachment,
  getAppDetail,
  getAppSessionMessages,
  getAppSessions,
  getAppUserConfig,
  saveAppUserConfig,
  updateAppSessionPrompt,
  type AppSessionItem,
} from '@/api/app'
import { getMyPrompts, getTeamPrompts, getTopUsedPrompts, type PromptItem } from '@/api/prompt'
import { abortAppChat, createAppChatAgent, runAppChat, type ToolApprovalMode } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage, type ToolCallDisplay } from './chat/ChatMessageList'
import { useWorkflowRunSteps } from './chat/useWorkflowRunSteps'
import { WorkflowRunSteps } from './chat/WorkflowRunSteps'
import { AppUserSettings } from './chat/AppUserSettings'
import { chatCssVars } from './chat/chatCssVars'
import {
  buildOutgoingText,
  formatAttachmentSize,
  getAttachmentFileIcon,
  hasPendingAttachment,
  isImageAttachment,
  isSupportedAttachment,
  MAX_ATTACHMENTS,
  MAX_ATTACHMENT_SIZE,
  type ChatAttachmentDraft,
} from './chat/attachment'
import './app-chat.css'

interface AppDetailLite {
  name?: string | null
  avatarPath?: string | null
  appType?: string | null
  publishStatus?: number | null
  openingStatement?: string | null
  openingStatementEnabled?: boolean | null
  quickInputs?: string[] | null
}

/** 开场白为前端本地展示消息，不入会话历史，id 固定以便复用 */
const OPENING_MESSAGE_ID = 'opening-statement'

/** 审批卡豁免工具的兜底规则（后端 userconfig 未返回时使用，与 AppToolApprovalContract 保持一致） */
const DEFAULT_EXEMPT_NAMES = ['search_knowledge_base']
const DEFAULT_EXEMPT_PREFIXES = ['skill_']

/**
 * 判定工具调用在审批模式下是否无需展示审批卡：
 * 只读检索/技能装载豁免（后端契约常量），以及应用审批策略自动放行的工具（白名单插件/沙箱，后端直接执行）.
 */
interface ApprovalExemptRef {
  names: string[]
  prefixes: string[]
  autoNames: string[]
  autoPrefixes: string[]
}

/**
 * Agent 应用对话页（沉浸式）：左侧会话列表，右侧流式对话。
 * 新会话为居中欢迎态（应用问候 + 输入卡 + 热门专家 + 快捷输入），发送后切换为常规对话布局；
 * 审批模式下沙箱/插件等重要工具调用前挂起，经输入卡的「批准/拒绝」放行。
 * 流程应用发布后共用本页：后端流程分支不装配专家提示词/技能/工具，
 * 因此隐藏专家选择、技能勾选与审批模式等 Agent 专属 UI，仅保留会话/开场白/快捷输入/附件。
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
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetailLite | null>(null)
  const [sessions, setSessions] = useState<AppSessionItem[]>([])
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [activeSessionId, setActiveSessionId] = useState('')
  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  // 输入卡附件：上传 → 提取（文档）→ ready，随下一条消息一起发送
  const [attachments, setAttachments] = useState<ChatAttachmentDraft[]>([])

  const [experts, setExperts] = useState<PromptItem[]>([])
  const [selectedPromptId, setSelectedPromptId] = useState(0)
  const [promptSaving, setPromptSaving] = useState(false)

  // 用户级应用配置：专家选择（新会话默认 + 会话内切换），技能勾选在服务端取默认集交集生效
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [savedPromptId, setSavedPromptId] = useState(0)
  const savedPromptIdRef = useRef(0)
  const savedSkillsRef = useRef<string[]>([])

  // 工具审批模式（auto/approval）：随每轮对话请求头下发，并持久化到用户级应用配置
  const [approvalMode, setApprovalMode] = useState<ToolApprovalMode>('auto')
  const exemptRef = useRef<ApprovalExemptRef>({
    names: DEFAULT_EXEMPT_NAMES,
    prefixes: DEFAULT_EXEMPT_PREFIXES,
    autoNames: [],
    autoPrefixes: [],
  })

  // 输入框下方推荐：使用次数最多的专家提示词（top10）
  const [topExperts, setTopExperts] = useState<PromptItem[]>([])

  const appName = detail?.name ?? ''
  const appAvatar = resolveStorageUrl(detail?.avatarPath ?? null)
  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)

  // 流程应用分支：一轮对话 = 一次流程执行，无专家提示词/技能/工具（后端不装配），相关请求与 UI 一并裁剪
  const isWorkflow = detail?.appType === 'workflow'
  const agentFeatures = detail !== null && !isWorkflow

  const cssVars = useMemo(() => chatCssVars(token), [token])

  const selectedExpert = useMemo(
    () => experts.find((e) => e.promptId === selectedPromptId),
    [experts, selectedPromptId],
  )

  // 应用配置的快捷输入（管理员自定义）：欢迎态展示为可点击胶囊，点击即直接发送
  const suggestions = useMemo(
    () => (detail?.quickInputs ?? []).map((x) => x.trim()).filter(Boolean),
    [detail],
  )

  // 应用配置的对话开场白：仅新会话开始时展示，不参与会话历史与服务端上下文
  const openingMessage = useMemo<DisplayMessage | null>(() => {
    const text = (detail?.openingStatement ?? '').trim()
    if (!detail?.openingStatementEnabled || !text) return null
    return { id: OPENING_MESSAGE_ID, role: 'assistant', content: text }
  }, [detail])

  // 欢迎态（居中输入卡）：除本地开场白外无任何消息
  const isLanding = !loading && messages.filter((m) => m.id !== OPENING_MESSAGE_ID).length === 0

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

  // 用户级应用配置：作为新会话的默认专家与工具审批模式；技能勾选在服务端取默认集交集生效（流程应用不适用）
  useEffect(() => {
    if (!appId || !agentFeatures) return
    getAppUserConfig(appId)
      .then((cfg) => {
        setSavedPromptId(cfg.promptId)
        savedPromptIdRef.current = cfg.promptId
        savedSkillsRef.current = cfg.skills
        setSelectedPromptId((prev) => (prev === 0 ? cfg.promptId : prev))
        setApprovalMode(cfg.toolApprovalMode)
        exemptRef.current = {
          names: cfg.toolApprovalExemptNames.length > 0 ? cfg.toolApprovalExemptNames : DEFAULT_EXEMPT_NAMES,
          prefixes: cfg.toolApprovalExemptPrefixes.length > 0 ? cfg.toolApprovalExemptPrefixes : DEFAULT_EXEMPT_PREFIXES,
          autoNames: cfg.toolApprovalAutoApprovedNames,
          autoPrefixes: cfg.toolApprovalAutoApprovedPrefixes,
        }
      })
      .catch(() => undefined)
  }, [agentFeatures, appId])

  useEffect(() => {
    if (!teamId || !agentFeatures) return
    // 可用专家 = 本人个人提示词 + 当前团队提示词；加载失败静默，应用设置面板展示空态
    Promise.all([getMyPrompts(), getTeamPrompts(teamId).catch(() => [])])
      .then(([mine, team]) => setExperts([...mine, ...team]))
      .catch(() => undefined)
    // 热门专家 = 范围内使用次数最多的前 10 个提示词，仅在欢迎态输入框下方展示
    getTopUsedPrompts(teamId, 10)
      .then((items) => setTopExperts(items))
      .catch(() => undefined)
  }, [teamId, agentFeatures])

  useEffect(() => {
    const node = scrollRef.current
    if (node) node.scrollTop = node.scrollHeight
  }, [messages])

  const selectSession = useCallback(async (sessionId: string, promptId?: number) => {
    setActiveSessionId(sessionId)
    setSelectedPromptId(promptId ?? 0)
    setAttachments([])
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
    setAttachments([])
    // 新会话默认使用用户配置的专家（应用设置中保存的偏好）
    setSelectedPromptId(savedPromptId)
    setSidebarOpen(false)
    inputRef.current?.focus()
  }, [savedPromptId])

  // 应用详情加载完成（或当前会话被删除）回到新会话态时，补展示开场白；不覆盖已有消息/历史
  useEffect(() => {
    if (loading || !detail || activeSessionId || !openingMessage) return
    setMessages((prev) => (prev.length === 0 ? [openingMessage] : prev))
  }, [loading, detail, activeSessionId, openingMessage])

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

  /** 审批模式下无需审批卡的工具：只读/技能装载豁免，以及审批策略自动放行（白名单插件/沙箱） */
  const isExemptTool = useCallback(
    (name: string) =>
      exemptRef.current.names.includes(name) ||
      exemptRef.current.prefixes.some((p) => name.startsWith(p)) ||
      exemptRef.current.autoNames.includes(name) ||
      exemptRef.current.autoPrefixes.some((p) => name.startsWith(p)),
    [],
  )

  const addToolCall = useCallback((messageId: string, toolCall: ToolCallDisplay) => {
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, toolCalls: [...(m.toolCalls ?? []), toolCall] } : m)),
    )
  }, [])

  // 流式推进/回合结束：执行中或已批准的工具调用标记完成，残留的挂起卡片按已自动执行收敛
  const resolveToolCalls = useCallback((messageId: string) => {
    setMessages((prev) =>
      prev.map((m) =>
        m.id === messageId
          ? {
              ...m,
              toolCalls: (m.toolCalls ?? []).map((tc) =>
                tc.status === 'running' || tc.status === 'approved' || tc.status === 'awaiting'
                  ? { ...tc, status: 'done' as const }
                  : tc,
              ),
            }
          : m,
      ),
    )
  }, [])

  // 流程应用对话的执行过程（AI 节点流式/节点状态经 AG-UI CustomEvent 推送；Agent 应用无事件，不渲染）
  const { steps: runSteps, instanceId: runInstanceId, running: runRunning, error: runError, reset: runReset, finish: runFinish, handleEvent: runHandleEvent } = useWorkflowRunSteps()

  const send = useCallback(async (override?: string) => {
    const text = (override ?? input).trim()
    if ((!text && attachments.length === 0) || sending) return
    // 附件全部就绪才能发送（上传/提取中等待，失败项需移除）
    if (hasPendingAttachment(attachments) || attachments.some((a) => a.status === 'failed')) return

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

    // 发送文本 = 用户输入 + 附件标记块（文档为提取文本，图片为链接）；气泡渲染时解析回 chip
    const outgoing = buildOutgoingText(text, attachments)
    const userMessage: DisplayMessage = { id: crypto.randomUUID(), role: 'user', content: outgoing }
    const assistantId = crypto.randomUUID()
    setMessages((prev) => [...prev, userMessage, { id: assistantId, role: 'assistant', content: '' }])
    setInput('')
    setAttachments([])
    setSending(true)
    runReset()

    agentRef.current = createAppChatAgent(appId, sessionId, { toolApprovalMode: approvalMode })

    try {
      await runAppChat(agentRef.current, outgoing, {
        onDelta: (buffer) => {
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m)))
          resolveToolCalls(assistantId)
        },
        onWorkflowEvent: runHandleEvent,
        onToolCall: (name) => {
          // 渐进披露下工具调用统一为 call_tool 元工具，真实工具在参数里、由 onToolCallEnd 解析
          if (name !== 'call_tool') {
            addToolCall(assistantId, { id: crypto.randomUUID(), name, status: 'running' })
          }
        },
        onToolCallEnd: (info) => {
          if (info.name === 'call_tool') {
            const realName = typeof info.args?.toolName === 'string' ? info.args.toolName : ''
            const rawArgs = info.args?.argumentsJson
            const argsJson =
              typeof rawArgs === 'string' ? rawArgs : rawArgs == null ? undefined : JSON.stringify(rawArgs)
            addToolCall(assistantId, {
              id: info.id || crypto.randomUUID(),
              name: realName || 'call_tool',
              argsJson,
              status:
                approvalMode === 'approval' && realName && !isExemptTool(realName) ? 'awaiting' : 'running',
            })
          } else if (info.name) {
            addToolCall(assistantId, { id: info.id || crypto.randomUUID(), name: info.name, status: 'running' })
          }
        },
        onError: (message) => {
          setMessages((prev) =>
            prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appChat.runError') } : m)),
          )
        },
      })
      resolveToolCalls(assistantId)
    } catch {
      setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appChat.runError') } : m)))
    } finally {
      setSending(false)
      runFinish()
      void loadSessions()
    }
  }, [activeSessionId, addToolCall, appId, approvalMode, attachments, input, isExemptTool, loadSessions, resolveToolCalls, selectedPromptId, sending, t, runFinish, runHandleEvent, runReset])

  // 选择附件：直传存储 →（文档）文本提取 → 就绪；失败标记在 chip 上由用户移除
  const handleFiles = useCallback(
    async (files: FileList | null) => {
      if (!files || files.length === 0) return
      const list = Array.from(files)
      const accepted = list.filter((f) => {
        if (!isSupportedAttachment(f.name)) {
          feedback.error(t('appChat.attachmentUnsupportedType', { name: f.name }))
          return false
        }
        if (f.size > MAX_ATTACHMENT_SIZE) {
          feedback.error(t('appChat.attachmentTooLarge', { name: f.name }))
          return false
        }
        return true
      })
      if (accepted.length === 0) return

      const slots = MAX_ATTACHMENTS - attachments.length
      if (slots <= 0) {
        feedback.warning(t('appChat.attachmentTooMany', { count: MAX_ATTACHMENTS }))
        return
      }
      const drafts: ChatAttachmentDraft[] = accepted.slice(0, slots).map((f) => ({
        id: crypto.randomUUID(),
        name: f.name,
        size: f.size,
        objectKey: '',
        url: '',
        isImage: isImageAttachment(f.name),
        status: 'uploading',
        markdown: '',
        truncated: false,
      }))
      const overflow = accepted.length - drafts.length
      if (overflow > 0) feedback.warning(t('appChat.attachmentTooMany', { count: MAX_ATTACHMENTS }))
      setAttachments((prev) => [...prev, ...drafts])

      await Promise.all(
        drafts.map(async (draft, index) => {
          const patch = (updater: (a: ChatAttachmentDraft) => ChatAttachmentDraft) => {
            setAttachments((prev) => prev.map((a) => (a.id === draft.id ? updater(a) : a)))
          }
          const file = accepted[index]
          try {
            const uploaded = await uploadChatFile(file)
            if (draft.isImage) {
              patch((a) => ({ ...a, ...uploaded, status: 'ready' }))
              return
            }
            patch((a) => ({ ...a, ...uploaded, status: 'extracting' }))
            const extracted = await extractChatAttachment(uploaded.objectKey, file.name)
            patch((a) => ({ ...a, status: 'ready', markdown: extracted.markdown, truncated: extracted.truncated }))
          } catch {
            patch((a) => ({ ...a, status: 'failed' }))
          }
        }),
      )
    },
    [attachments.length, t],
  )

  const removeAttachment = useCallback((id: string) => {
    setAttachments((prev) => prev.filter((a) => a.id !== id))
  }, [])

  const stop = useCallback(() => {
    if (agentRef.current) abortAppChat(agentRef.current)
    setSending(false)
  }, [])

  // 审批决策：批准/拒绝挂起的工具调用；missing 表示后端无匹配记录（已自动执行）
  const decideTool = useCallback(
    async (messageId: string, toolCall: ToolCallDisplay, approved: boolean) => {
      const sessionId = activeSessionId
      if (!sessionId) return
      try {
        const status = await decideAppSessionToolApproval(sessionId, toolCall.name, approved)
        const next =
          status === 'missing'
            ? 'done'
            : approved
              ? 'approved'
              : 'rejected'
        setMessages((prev) =>
          prev.map((m) =>
            m.id === messageId
              ? {
                  ...m,
                  toolCalls: (m.toolCalls ?? []).map((tc) => (tc.id === toolCall.id ? { ...tc, status: next } : tc)),
                }
              : m,
          ),
        )
      } catch {
        // 错误已由全局请求中间件统一提示，卡片保持挂起可重试
      }
    },
    [activeSessionId],
  )

  // 审批模式切换：立即生效（随下轮请求头下发）并持久化到用户级应用配置
  const toggleApprovalMode = useCallback(() => {
    const next: ToolApprovalMode = approvalMode === 'approval' ? 'auto' : 'approval'
    setApprovalMode(next)
    void saveAppUserConfig(appId, {
      promptId: savedPromptIdRef.current,
      skills: savedSkillsRef.current,
      toolApprovalMode: next,
    }).catch(() => undefined)
  }, [appId, approvalMode])

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

  // 快捷输入：点击即直接发送，无需先填入输入框
  const applySuggestion = useCallback(
    (text: string) => {
      void send(text)
    },
    [send],
  )

  const placeholder = t('appChat.placeholderSendTo', { name: appName || t('appChat.title') })

  const expertChip = !isWorkflow && selectedExpert && (
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
  )

  // 附件中是否有未就绪项：发送按钮置灰并提示
  const attachmentsBlocking = hasPendingAttachment(attachments) || attachments.some((a) => a.status === 'failed')

  // 输入卡（欢迎态居中 / 对话态停靠底部共用）：文本域 + 附件 chips + 底部左侧附件/模式、右侧发送/停止
  const composerNode = (
    <div className="moai-chat__composer">
      <Input.TextArea
        ref={inputRef}
        variant="borderless"
        autoSize={{ minRows: 3, maxRows: 10 }}
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onPressEnter={(e) => {
          if (!e.shiftKey) {
            e.preventDefault()
            void send()
          }
        }}
        placeholder={placeholder}
      />
      {attachments.length > 0 && (
        <div className="moai-chat__attachments">
          {attachments.map((att) => {
            // 图片附件就绪后展示缩略图（上传中/失败无地址时回退图片图标）；文档按扩展名取类型图标
            const FileIcon = getAttachmentFileIcon(att.name)
            return (
            <div key={att.id} className={`moai-chat__attachment${att.status === 'failed' ? ' is-failed' : ''}`}>
              {att.isImage && att.url ? (
                <img src={att.url} alt="" className="moai-chat__attachment-thumb" />
              ) : (
                <span className="moai-chat__attachment-icon">
                  <FileIcon />
                </span>
              )}
              <span className="moai-chat__attachment-name" title={att.name}>
                {att.name}
              </span>
              <span className="moai-chat__attachment-meta">
                {att.status === 'uploading' || att.status === 'extracting' ? (
                  <>
                    <LoadingOutlined spin />
                    <span>{t(att.status === 'uploading' ? 'appChat.attachmentUploading' : 'appChat.attachmentExtracting')}</span>
                  </>
                ) : att.status === 'failed' ? (
                  <span>{t('appChat.attachmentFailed')}</span>
                ) : (
                  <>
                    <span>{formatAttachmentSize(att.size)}</span>
                    {att.truncated && <span>{t('appChat.attachmentTruncated')}</span>}
                  </>
                )}
              </span>
              <button
                type="button"
                className="moai-chat__attachment-remove"
                onClick={() => removeAttachment(att.id)}
                aria-label={t('appChat.attachmentRemove')}
              >
                <CloseOutlined />
              </button>
            </div>
            )
          })}
        </div>
      )}
      <div className="moai-chat__composer-footer">
        <div className="moai-chat__composer-tools">
          <input
            ref={fileInputRef}
            type="file"
            multiple
            className="moai-chat__file-input"
            accept=".jpg,.jpeg,.png,.gif,.bmp,.webp,.svg,.md,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.rtf,.odt,.ods,.odp,.csv,.json,.xml,.html,.htm,.epub,.mobi,.eml,.msg,.tex"
            onChange={(e) => {
              void handleFiles(e.target.files)
              e.target.value = ''
            }}
          />
          <Tooltip title={t('appChat.attach')}>
            <button
              type="button"
              className="moai-chat__attach"
              disabled={sending || attachments.length >= MAX_ATTACHMENTS}
              onClick={() => fileInputRef.current?.click()}
              aria-label={t('appChat.attach')}
            >
              <PaperClipOutlined />
            </button>
          </Tooltip>
          {!isWorkflow && (
            <Tooltip title={approvalMode === 'approval' ? t('appChat.modeApprovalHint') : t('appChat.modeAutoHint')}>
              <button
                type="button"
                className={`moai-chat__mode${approvalMode === 'approval' ? ' is-approval' : ''}`}
                onClick={toggleApprovalMode}
              >
                {approvalMode === 'approval' ? <SafetyCertificateOutlined /> : <ThunderboltFilled />}
                <span>{approvalMode === 'approval' ? t('appChat.modeApproval') : t('appChat.modeAuto')}</span>
              </button>
            </Tooltip>
          )}
        </div>
        <div className="moai-chat__composer-actions">
          {sending ? (
            <button type="button" className="moai-chat__send" onClick={stop} aria-label={t('appChat.stop')}>
              <StopOutlined />
            </button>
          ) : (
            <Tooltip title={attachmentsBlocking ? t('appChat.sendPendingAttachment') : ''}>
              <button
                type="button"
                className="moai-chat__send"
                onClick={() => void send()}
                disabled={(!input.trim() && attachments.length === 0) || attachmentsBlocking}
                aria-label={t('appChat.send')}
              >
                <ArrowUpOutlined />
              </button>
            </Tooltip>
          )}
        </div>
      </div>
    </div>
  )

  const hotExpertsNode = isLanding && !isWorkflow && topExperts.length > 0 && (
    <div className="moai-chat__hot-experts">
      <span className="moai-chat__hot-experts-label">
        <FireFilled />
        {t('appChat.hotExperts')}
      </span>
      <div className="moai-chat__hot-experts-list">
        {topExperts.map((item) => {
          const id = Number(item.promptId ?? 0)
          const active = id === selectedPromptId
          const avatar = resolveStorageUrl(item.avatarPath ?? null)
          return (
            <button
              key={id}
              type="button"
              className={`moai-chat__hot-expert${active ? ' is-active' : ''}`}
              onClick={() => void toggleExpert(id)}
              title={item.description || item.name || ''}
            >
              <span className="moai-chat__hot-expert-avatar">
                {avatar ? (
                  <img src={avatar} alt="" />
                ) : (
                  <span className="moai-chat__hot-expert-fallback">{(item.name ?? '?').slice(0, 1).toUpperCase()}</span>
                )}
              </span>
                <span className="moai-chat__hot-expert-body">
                  <span className="moai-chat__hot-expert-name">{item.name || t('appChat.untitled')}</span>
                  {(item.useCount ?? 0) > 0 && (
                    <span className="moai-chat__hot-expert-count">
                      {t('appChat.hotExpertUsed', { count: item.useCount ?? 0 })}
                    </span>
                  )}
                </span>
              {active && <CheckOutlined className="moai-chat__hot-expert-check" />}
            </button>
          )
        })}
      </div>
    </div>
  )

  return (
    <div className="moai-chat" style={cssVars}>
      {(sidebarOpen || settingsOpen) && (
        <div
          className={`moai-chat__overlay${settingsOpen ? ' is-visible' : ''}`}
          onClick={() => { setSidebarOpen(false); setSettingsOpen(false) }}
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

      {!isWorkflow && (
        <AppUserSettings
          open={settingsOpen}
          appId={appId}
          experts={experts}
          currentPromptId={selectedPromptId}
          onClose={() => setSettingsOpen(false)}
          onSaved={(promptId, approvalModeSaved) => {
            setSavedPromptId(promptId)
            savedPromptIdRef.current = promptId
            if (approvalModeSaved) setApprovalMode(approvalModeSaved)
            // 尚未进入会话时（正在准备新对话），立即应用为当前专家
            if (!activeSessionId) {
              setSelectedPromptId(promptId)
              return
            }
            // 已有会话且专家变化：同步切换当前会话绑定的专家
            if (promptId !== selectedPromptId) {
              setPromptSaving(true)
              void updateAppSessionPrompt(activeSessionId, promptId)
                .then(() => {
                  setSelectedPromptId(promptId)
                  setSessions((prev) =>
                    prev.map((s) => (String(s.sessionId) === activeSessionId ? { ...s, promptId } : s)),
                  )
                })
                .catch(() => undefined)
                .finally(() => setPromptSaving(false))
            }
          }}
        />
      )}

      <main className={`moai-chat__main${isLanding ? ' is-landing' : ''}`}>
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
          {!isWorkflow && (
            <Tooltip title={t('appChat.userSettings')}>
              <Button
                type={settingsOpen ? 'primary' : 'text'}
                icon={<ControlOutlined />}
                aria-label={t('appChat.userSettings')}
                onClick={() => setSettingsOpen((v) => !v)}
              />
            </Tooltip>
          )}
        </header>

        {isLanding ? (
          <div className="moai-chat__landing">
            <div className="moai-chat__landing-hero">
              <div className="moai-chat__landing-badge">
                {appAvatar ? (
                  <img src={appAvatar} alt={appName} />
                ) : (
                  appName.slice(0, 1).toUpperCase() || <RobotFilled />
                )}
              </div>
              <h1 className="moai-chat__landing-title">{appName || t('appChat.title')}</h1>
            </div>
            {openingMessage && <div className="moai-chat__landing-opening">{openingMessage.content}</div>}
            <div className="moai-chat__landing-composer">
              {expertChip}
              {composerNode}
              {hotExpertsNode}
              {suggestions.length > 0 && (
                <div className="moai-chat__suggestions">
                  {suggestions.map((text) => (
                    <button key={text} type="button" className="moai-chat__suggestion" onClick={() => applySuggestion(text)}>
                      {text}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        ) : (
          <>
            <div className="moai-chat__scroll" ref={scrollRef}>
              <ChatMessageList
                messages={messages}
                sending={sending}
                appAvatar={appAvatar}
                userName={userName}
                userAvatar={userAvatar}
                onCopy={(text) => void copyMessage(text)}
                onToolApprove={(messageId, toolCall) => void decideTool(messageId, toolCall, true)}
                onToolReject={(messageId, toolCall) => void decideTool(messageId, toolCall, false)}
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
            <div className="moai-chat__composer-wrap">
              <div className="moai-chat__composer-inner">
                {expertChip}
                {composerNode}
              </div>
            </div>
          </>
        )}
      </main>
    </div>
  )
}
