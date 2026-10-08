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
import { parseHistoryToolCalls } from './chat/historyToolCalls'
import {
  applyWorkflowEvent,
  createWorkflowRunState,
  finishWorkflowRun,
  type WorkflowRunState,
} from './chat/useWorkflowRunSteps'
import { WorkflowRunSteps } from './chat/WorkflowRunSteps'
import { AppUserSettings } from './chat/AppUserSettings'
import { ChatSidePanel } from './chat/ChatSidePanel'
import { isUiToolName, parseUiToolPayload, toUiPanelItem, type UiPanelItem } from './chat/uiTools'
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

/** 会话消息分桶的空态占位（保持引用稳定） */
const NO_MESSAGES: DisplayMessage[] = []

/**
 * 自主导航回声窗口（毫秒）：react-router 的导航状态经 startTransition 提交，渲染可能晚于
 * 用户的下一步操作（如发首条消息后立刻点「新对话」）；窗口内到达的、且目标属于本页近期发起
 * 导航过的地址段变化视为自身回声，不触发 URL 恢复副作用。窗口外的地址段变化（前进/后退/深链）
 * 照常恢复对应会话
 */
const SELF_NAV_ECHO_WINDOW_MS = 500

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
 * 消息/流式态/流程执行过程按会话 id 分桶：多会话可并行对话，切走不打断、切回恢复现场。
 */
export function AppChat() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string; sessionId?: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''
  // 当前会话写入路由（/chat/:sessionId）：刷新/深链恢复会话；无会话段即新对话态
  const urlSessionId = params.sessionId ?? ''
  const chatBase = `/team/${teamId}/app/${appId}/chat`
  const { token } = theme.useToken()
  const userInfo = useAppStore((state) => state.userInfo)

  const agentsRef = useRef(new Map<string, HttpAgent>())
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<TextAreaRef | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetailLite | null>(null)
  const [sessions, setSessions] = useState<AppSessionItem[]>([])
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [activeSessionId, setActiveSessionId] = useState('')
  // 多会话并行：消息/流式态/流程执行过程均按会话 id 分桶，切走不打断、切回恢复现场
  const [messagesBySession, setMessagesBySession] = useState<Record<string, DisplayMessage[]>>({})
  const [sendingBySession, setSendingBySession] = useState<Record<string, boolean>>({})
  const [runStates, setRunStates] = useState<Record<string, WorkflowRunState>>({})
  const [input, setInput] = useState('')
  const [sidebarOpen, setSidebarOpen] = useState(false)
  // 前端展示工具（ui_ 前缀）产出内容的右侧侧边栏：多项标签集合 + 当前项 + 展开状态；
  // 收起只置 open=false（标签保留，点击消息卡片可再展开），切换会话时清空
  const [uiPanel, setUiPanel] = useState<{ items: UiPanelItem[]; activeKey: string | null; open: boolean }>({
    items: [],
    activeKey: null,
    open: false,
  })
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

  // 欢迎态（居中输入卡）：当前视图无任何消息（空数组常量保持引用稳定，避免滚动副作用空转）
  const viewMessages = useMemo(
    () => (activeSessionId ? messagesBySession[activeSessionId] ?? NO_MESSAGES : NO_MESSAGES),
    [activeSessionId, messagesBySession],
  )
  const activeSending = sendingBySession[activeSessionId] ?? false
  const activeRunState = activeSessionId ? runStates[activeSessionId] : undefined
  const isLanding = !loading && viewMessages.length === 0

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
    // 应用切换：中止上一应用的全部并行会话并清空现场（同应用挂载时为空操作）
    const agents = agentsRef.current
    for (const agent of agents.values()) abortAppChat(agent)
    agents.clear()
    setActiveSessionId('')
    setMessagesBySession({})
    setSendingBySession({})
    setRunStates({})
    setLoading(true)
    getAppDetail(appId)
      .then((res) => setDetail(res as unknown as AppDetailLite))
      .catch(() => undefined)
      .finally(() => setLoading(false))
    void loadSessions()
    return () => {
      for (const agent of agents.values()) abortAppChat(agent)
      agents.clear()
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
  }, [viewMessages])

  // 终止某会话的流并清理其本地现场（删除会话时使用；流收尾逻辑由 send 的 finally 兜底）
  const cleanupSessionStream = useCallback((sessionId: string) => {
    const agent = agentsRef.current.get(sessionId)
    if (agent) {
      abortAppChat(agent)
      agentsRef.current.delete(sessionId)
    }
    setSendingBySession((prev) => {
      if (!(sessionId in prev)) return prev
      const next = { ...prev }
      delete next[sessionId]
      return next
    })
    setRunStates((prev) => {
      if (!(sessionId in prev)) return prev
      const next = { ...prev }
      delete next[sessionId]
      return next
    })
    setMessagesBySession((prev) => {
      if (!(sessionId in prev)) return prev
      const next = { ...prev }
      delete next[sessionId]
      return next
    })
  }, [])

  // 自主导航登记（会话段目标 + 最近一次导航时间）：URL 恢复副作用据此区分自身回声与外部导航
  const selfNavRef = useRef<{ targets: Set<string>; at: number }>({ targets: new Set(), at: -Infinity })

  // 本页发起的会话地址导航：统一登记目标，供 URL 恢复副作用做回声判定
  const navigateSession = useCallback(
    (sessionId: string, options?: { replace?: boolean }) => {
      selfNavRef.current.targets.add(sessionId)
      selfNavRef.current.at = performance.now()
      navigate(
        sessionId ? `${chatBase}/${sessionId}` : chatBase,
        options?.replace ? { replace: true } : undefined,
      )
    },
    [chatBase, navigate],
  )

  const selectSession = useCallback(async (sessionId: string, promptId?: number) => {
    setActiveSessionId(sessionId)
    setSelectedPromptId(promptId ?? 0)
    setAttachments([])
    setSidebarOpen(false)
    setUiPanel({ items: [], activeKey: null, open: false })
    if (urlSessionId !== sessionId) {
      navigateSession(sessionId)
    }
    // 流式进行中的会话直接恢复本地现场：服务端历史不含本轮，回拉会丢实时内容
    if (agentsRef.current.has(sessionId)) return
    try {
      const items = await getAppSessionMessages(sessionId)
      // 历史返回前该会话已开始新一轮流式（用户切走又快速发消息）：以流式现场为准
      if (agentsRef.current.has(sessionId)) return
      setMessagesBySession((prev) => ({
        ...prev,
        [sessionId]: items
          .filter((m) => m.role === 'user' || (m.role === 'assistant' && ((m.content ?? '').trim().length > 0 || parseHistoryToolCalls(m.toolCalls))))
          .map((m) => ({
            id: String(m.messageId ?? crypto.randomUUID()),
            role: m.role === 'user' ? 'user' : 'assistant',
            content: m.content ?? '',
            toolCalls: parseHistoryToolCalls(m.toolCalls),
          })),
      }))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [navigateSession, urlSessionId])

  // 当前会话 ref（渲染期同步）：供 URL 会话段副作用比较，避免依赖 activeSessionId 状态造成
  // 「新对话清空 active 但 URL 尚未更新」的一帧误判而把旧会话又选回来
  const activeSessionRef = useRef('')
  activeSessionRef.current = activeSessionId
  const prevUrlSessionRef = useRef('')

  // 地址栏会话段变化（刷新/深链/前进后退）且不是当前会话时恢复该会话
  useEffect(() => {
    const prev = prevUrlSessionRef.current
    prevUrlSessionRef.current = urlSessionId
    if (!urlSessionId || urlSessionId === prev) return
    if (urlSessionId === activeSessionRef.current) return
    // 自主导航的迟到回声：目标属于本页导航过的会话段且窗口未过 → 忽略，
    // 防止把用户刚切走/新建的视图拉回旧会话
    if (
      selfNavRef.current.targets.has(urlSessionId) &&
      performance.now() - selfNavRef.current.at < SELF_NAV_ECHO_WINDOW_MS
    ) {
      return
    }
    const match = sessions.find((s) => String(s.sessionId) === urlSessionId)
    void selectSession(urlSessionId, match?.promptId ?? 0)
  }, [urlSessionId, selectSession, sessions])

  // 深链进入时会话列表可能晚于消息到达：列表就绪后按会话绑定的专家回显高亮
  useEffect(() => {
    if (!urlSessionId || sessions.length === 0) return
    const match = sessions.find((s) => String(s.sessionId) === urlSessionId)
    const promptId = match?.promptId ?? 0
    if (promptId > 0) {
      setSelectedPromptId((prev) => (prev === 0 ? promptId : prev))
    }
  }, [sessions, urlSessionId])

  // 新对话只切换当前视图：其他会话的进行中流不受影响（多会话并行）
  const newSession = useCallback(async () => {
    setActiveSessionId('')
    setAttachments([])
    // 新会话默认使用用户配置的专家（应用设置中保存的偏好）
    setSelectedPromptId(savedPromptId)
    setSidebarOpen(false)
    setUiPanel({ items: [], activeKey: null, open: false })
    if (urlSessionId) {
      navigateSession('')
    }
    inputRef.current?.focus()
  }, [navigateSession, savedPromptId, urlSessionId])

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
        // 会话已删：其中仍在进行的流一并中止并清理本地现场
        cleanupSessionStream(sessionId)
        if (sessionId === activeSessionId) {
          setActiveSessionId('')
          setUiPanel({ items: [], activeKey: null, open: false })
          // 删除的是地址栏所指会话：回到新对话态地址
          if (urlSessionId === sessionId) {
            navigateSession('', { replace: true })
          }
        }
        await loadSessions()
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    },
    [activeSessionId, cleanupSessionStream, loadSessions, navigateSession, urlSessionId],
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

  // 打开（或聚焦已存在的）侧边栏标签：同 key 内容刷新，新项追加并激活
  const openUiPanelItem = useCallback((item: UiPanelItem) => {
    setUiPanel((prev) => ({
      items: prev.items.some((x) => x.key === item.key)
        ? prev.items.map((x) => (x.key === item.key ? item : x))
        : [...prev.items, item],
      activeKey: item.key,
      open: true,
    }))
  }, [])

  // 在侧边栏打开前端展示工具产出（消息卡片点击，含历史回放）
  const openUiTool = useCallback(
    (toolCall: ToolCallDisplay) => {
      const item = toUiPanelItem(toolCall)
      if (item) openUiPanelItem(item)
    },
    [openUiPanelItem],
  )

  // 关闭单个标签：关的是当前项时切到相邻标签，全部关完面板一并收起
  const closeUiPanelTab = useCallback((key: string) => {
    setUiPanel((prev) => {
      const index = prev.items.findIndex((x) => x.key === key)
      if (index < 0) return prev
      const items = prev.items.filter((x) => x.key !== key)
      return {
        items,
        activeKey: prev.activeKey === key ? items[Math.max(0, index - 1)]?.key ?? null : prev.activeKey,
        open: prev.open && items.length > 0,
      }
    })
  }, [])

  // 流程应用对话的执行过程（AI 节点流式/节点状态经 AG-UI CustomEvent 推送；Agent 应用无事件，不渲染）
  // 状态按会话分桶于 runStates，多会话并行的流程执行互不串台

  // 草稿态创建会话的窗口互斥（防连点双开会话）
  const creatingSessionRef = useRef(false)

  const send = useCallback(async (override?: string) => {
    const text = (override ?? input).trim()
    if ((!text && attachments.length === 0) || activeSending) return
    // 附件全部就绪才能发送（上传/提取中等待，失败项需移除）
    if (hasPendingAttachment(attachments) || attachments.some((a) => a.status === 'failed')) return

    let sessionId = activeSessionId
    if (!sessionId) {
      if (creatingSessionRef.current) return
      creatingSessionRef.current = true
      try {
        // 首轮消息：会话与会话绑定的专家提示词一并创建，避免首条消息丢失专家设定
        sessionId = await createAppSession(appId, undefined, selectedPromptId)
      } catch {
        creatingSessionRef.current = false
        return
      }
      creatingSessionRef.current = false
      setActiveSessionId(sessionId)
      if (urlSessionId !== sessionId) {
        navigateSession(sessionId)
      }
      // 立即刷新侧栏（不等流结束）：新会话尽早就位，标题随后端 AI 提炼更新
      void loadSessions()
    }

    // 发送文本 = 用户输入 + 附件标记块（文档为提取文本，图片为链接）；气泡渲染时解析回 chip
    const outgoing = buildOutgoingText(text, attachments)
    const userMessage: DisplayMessage = { id: crypto.randomUUID(), role: 'user', content: outgoing }
    const assistantId = crypto.randomUUID()
    // 会话级写入闭包：流式回调一律写发送时锁定的会话，切走后写入原会话不丢内容
    const patchMessages = (updater: (prev: DisplayMessage[]) => DisplayMessage[]) =>
      setMessagesBySession((prev) => ({ ...prev, [sessionId]: updater(prev[sessionId] ?? []) }))
    const addToolCall = (toolCall: ToolCallDisplay) =>
      patchMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, toolCalls: [...(m.toolCalls ?? []), toolCall] } : m)))
    // 按 id 更新（存在）或追加（不存在）工具调用：流式开始建占位卡、参数流结束回填参数时去重
    const upsertToolCall = (toolCall: ToolCallDisplay) =>
      patchMessages((prev) =>
        prev.map((m) => {
          if (m.id !== assistantId) return m
          const calls = m.toolCalls ?? []
          const index = calls.findIndex((tc) => tc.id === toolCall.id)
          return { ...m, toolCalls: index >= 0 ? calls.map((tc) => (tc.id === toolCall.id ? toolCall : tc)) : [...calls, toolCall] }
        }),
      )
    // 流式推进/回合结束：执行中或已批准的工具调用标记完成，残留的挂起卡片按已自动执行收敛
    const resolveToolCalls = () =>
      patchMessages((prev) =>
        prev.map((m) =>
          m.id === assistantId
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

    // 首轮落位：开场白为本地展示消息，随首轮进入会话消息流（不参与服务端历史）
    patchMessages((prev) => [
      ...(prev.length > 0 ? prev : openingMessage ? [openingMessage] : []),
      userMessage,
      { id: assistantId, role: 'assistant', content: '' },
    ])
    setInput('')
    setAttachments([])
    setSendingBySession((prev) => ({ ...prev, [sessionId]: true }))
    setRunStates((prev) => ({ ...prev, [sessionId]: createWorkflowRunState(true) }))

    const agent = createAppChatAgent(appId, sessionId, { toolApprovalMode: approvalMode })
    agentsRef.current.set(sessionId, agent)

    try {
      await runAppChat(agent, outgoing, {
        onDelta: (buffer) => {
          patchMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m)))
          resolveToolCalls()
        },
        onWorkflowEvent: (evt) => {
          setRunStates((prev) => (prev[sessionId] ? { ...prev, [sessionId]: applyWorkflowEvent(prev[sessionId], evt) } : prev))
        },
        onToolCall: (info) => {
          // 前端展示工具：流式开始即建占位卡（生成中），参数流结束回填内容
          if (isUiToolName(info.name)) {
            addToolCall({ id: info.id || crypto.randomUUID(), name: info.name, status: 'running' })
            return
          }
          // 渐进披露下工具调用统一为 call_tool 元工具，真实工具在参数里、由 onToolCallEnd 解析
          if (info.name !== 'call_tool') {
            addToolCall({ id: info.id || crypto.randomUUID(), name: info.name, status: 'running' })
          }
        },
        onToolCallEnd: (info) => {
          // 前端展示工具：参数流结束即内容完整（后端桩执行瞬时完成），折叠成卡片并自动打开侧边栏标签
          if (isUiToolName(info.name)) {
            const argsJson = info.args == null ? undefined : JSON.stringify(info.args)
            upsertToolCall({ id: info.id || crypto.randomUUID(), name: info.name, argsJson, status: 'done' })
            const item = parseUiToolPayload(info.name, argsJson)
            if (item && (item.content ?? item.code ?? item.option) != null) {
              openUiPanelItem({ key: info.id, ...item })
            }
            return
          }
          if (info.name === 'call_tool') {
            const realName = typeof info.args?.toolName === 'string' ? info.args.toolName : ''
            const rawArgs = info.args?.argumentsJson
            const argsJson =
              typeof rawArgs === 'string' ? rawArgs : rawArgs == null ? undefined : JSON.stringify(rawArgs)
            addToolCall({
              id: info.id || crypto.randomUUID(),
              name: realName || 'call_tool',
              argsJson,
              status:
                approvalMode === 'approval' && realName && !isExemptTool(realName) ? 'awaiting' : 'running',
            })
          } else if (info.name) {
            upsertToolCall({ id: info.id || crypto.randomUUID(), name: info.name, status: 'running' })
          }
        },
        onError: (message) => {
          patchMessages((prev) =>
            prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appChat.runError') } : m)),
          )
        },
      })
      resolveToolCalls()
    } catch {
      patchMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appChat.runError') } : m)))
    } finally {
      agentsRef.current.delete(sessionId)
      setSendingBySession((prev) => {
        if (!(sessionId in prev)) return prev
        const next = { ...prev }
        delete next[sessionId]
        return next
      })
      setRunStates((prev) => (prev[sessionId] ? { ...prev, [sessionId]: finishWorkflowRun(prev[sessionId]) } : prev))
      void loadSessions()
    }
  }, [activeSending, activeSessionId, appId, approvalMode, attachments, input, isExemptTool, loadSessions, navigateSession, openingMessage, openUiPanelItem, selectedPromptId, t, urlSessionId])

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

  // 停止仅作用于当前会话的流（后台并行会话不受影响）；流收尾与现场清理由 send 的 finally 兜底
  const stop = useCallback(() => {
    const sessionId = activeSessionId
    const agent = agentsRef.current.get(sessionId)
    if (agent) abortAppChat(agent)
    if (sessionId) {
      setSendingBySession((prev) => {
        if (!(sessionId in prev)) return prev
        const next = { ...prev }
        delete next[sessionId]
        return next
      })
    }
  }, [activeSessionId])

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
        setMessagesBySession((prev) => ({
          ...prev,
          [sessionId]: (prev[sessionId] ?? []).map((m) =>
            m.id === messageId
              ? {
                  ...m,
                  toolCalls: (m.toolCalls ?? []).map((tc) => (tc.id === toolCall.id ? { ...tc, status: next } : tc)),
                }
              : m,
          ),
        }))
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
              disabled={activeSending || attachments.length >= MAX_ATTACHMENTS}
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
          {activeSending ? (
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
              const streaming = sendingBySession[id] ?? false
              return (
                <div key={id} className={`moai-chat__session${active ? ' is-active' : ''}${streaming ? ' is-streaming' : ''}`}>
                  {/* 点击区放在会话主体：Popconfirm 弹层是行的 React 子节点，门户事件会沿 React 树冒泡回行，
                      若 onClick 挂在行容器上，点「确定/取消」会同时触发选中该会话 */}
                  <div className="moai-chat__session-body" onClick={() => void selectSession(id, session.promptId ?? 0)}>
                    <div className="moai-chat__session-title">
                      {streaming && <span className="moai-chat__session-stream" title={t('appChat.sessionStreaming')} />}
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
                    <button
                      type="button"
                      className="moai-chat__session-del"
                      onClick={(e) => e.stopPropagation()}
                      aria-label={t('appChat.deleteSession')}
                    >
                      <DeleteOutlined />
                    </button>
                  </Popconfirm>
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
            aria-label={t('appChat.back')}
            onClick={() => navigate('/apps')}
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
                messages={viewMessages}
                sending={activeSending}
                appAvatar={appAvatar}
                userName={userName}
                userAvatar={userAvatar}
                onCopy={(text) => void copyMessage(text)}
                onToolApprove={(messageId, toolCall) => void decideTool(messageId, toolCall, true)}
                onToolReject={(messageId, toolCall) => void decideTool(messageId, toolCall, false)}
                onOpenUiTool={(_, toolCall) => openUiTool(toolCall)}
                activeUiToolId={uiPanel.open ? uiPanel.activeKey ?? undefined : undefined}
              />
              {activeRunState && (activeRunState.steps.length > 0 || activeRunState.error) && (
                <WorkflowRunSteps
                  steps={activeRunState.steps}
                  running={activeRunState.running}
                  error={activeRunState.error}
                  instanceId={activeRunState.instanceId}
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

      {/* 前端展示工具侧边栏：多标签 + 可拖宽；收起仅置 open=false，标签保留供卡片重开 */}
      {uiPanel.open && uiPanel.items.length > 0 && (
        <ChatSidePanel
          items={uiPanel.items}
          activeKey={uiPanel.activeKey}
          onSelect={(key) => setUiPanel((prev) => ({ ...prev, activeKey: key }))}
          onCloseTab={closeUiPanelTab}
          onClose={() => setUiPanel((prev) => ({ ...prev, open: false }))}
        />
      )}
    </div>
  )
}
