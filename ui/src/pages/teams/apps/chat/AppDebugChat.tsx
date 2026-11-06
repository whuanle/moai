import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { ArrowRightOutlined, BulbOutlined, ClearOutlined, EditOutlined } from '@ant-design/icons'
import { Button, Popconfirm, Tooltip, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { createDebugSession, decideAppSessionToolApproval, getAppAgentConfig, getAppUserConfig, type AppUserTeamAgentOption } from '@/api/app'
import { getTeamAvailableModels, type TeamAvailableModelItem } from '@/api/aichannel'
import { abortAppChat, createAppChatAgent, runAppChat, type ToolApprovalMode } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage, type ToolCallDisplay } from './ChatMessageList'
import { ChatComposer } from './ChatComposer'
import { applySubAgentEvent } from './subAgentTrace'
import { useChatAttachments } from './useChatAttachments'
import { ChatSidePanel } from './ChatSidePanel'
import { buildOutgoingText, hasPendingAttachment } from './attachment'
import { isUiToolName, parseUiToolPayload, toUiPanelItem, type UiPanelItem } from './uiTools'
import { chatCssVars } from './chatCssVars'
// 复用真实对话界面（AppChat）的 moai-chat__landing/composer 系列样式，调试预览即最终效果
import '../app-chat.css'

// 与后端 AppAgentConstants.SessionResolveErrorMarker 保持一致
const SESSION_NOT_FOUND_MARKER = '[[moai:session-not-found]]'

/** 开场白为本地展示消息（不入调试会话历史），固定 id 用于从消息流中过滤 */
const OPENING_MESSAGE_ID = 'opening-statement'

/** 审批卡豁免/自动放通工具清单：随 userconfig 下发（与真实对话页 AppChat 同口径） */
interface ApprovalExemptRef {
  names: string[]
  prefixes: string[]
  autoNames: string[]
  autoPrefixes: string[]
}

export interface AppDebugChatProps {
  appId: string
  /** 应用名：欢迎态标题与输入框占位「发消息给 {name}」，与真实对话界面一致 */
  appName?: string
  appAvatar?: string
  /** 应用配置的对话开场白（已按启用状态过滤），欢迎态以 Markdown 简报卡展示 */
  openingStatement?: string
  /** 应用配置的快捷输入（保存前的草稿实时预览），欢迎态点击即发送 */
  quickInputs?: string[]
  /** 标题右侧扩展区（如「进入对话」入口），由调用方按状态拼装 */
  headerExtra?: ReactNode
}

/**
 * 调试对话面板：进入即创建 Redis 调试会话（不落库、不计用量），
 * 页面刷新即放弃会话 id 并重新创建；后端注册表过期时自动重建一次并重试。
 * 界面一比一复用真实对话页（AppChat）：欢迎态与输入卡布局一致，输入卡为共享组件
 * ChatComposer（附件/审批模式切换/模型切换器/发送停止全量呈现）；审批模式下重要工具
 * 调用挂起审批卡，批准/拒绝走调试会话决策链路（后端按调试注册表校验归属）。
 * 与真实对话页的差异：会话不落库（无上下文用量环）、调试面板切换审批模式仅本地生效
 * （不写用户级应用配置，避免调试面板悄悄改动真实对话端偏好）、专家绑定暂不支持（@AP-S147 已知边界）。
 */
export function AppDebugChat({ appId, appName, appAvatar, openingStatement, quickInputs, headerExtra }: AppDebugChatProps) {
  const { t } = useTranslation()
  const params = useParams()
  const teamId = Number(params.teamId)
  const { token } = theme.useToken()
  const userInfo = useAppStore((state) => state.userInfo)
  const agentRef = useRef<HttpAgent | null>(null)
  const sessionIdRef = useRef('')
  const cancelledRef = useRef(false)
  const expiredRef = useRef(false)

  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  // 调试对话模型切换：应用草稿配置的可选模型（经团队可用性过滤）与当前选择（null=未选择）
  const [chatModelOptions, setChatModelOptions] = useState<TeamAvailableModelItem[]>([])
  const [selectedChatModelId, setSelectedChatModelId] = useState<string | null>(null)
  // 调试团队智能体切换：应用绑定多选（≥2）时展示切换器，单选由后端直接生效（null=无切换）
  const [chatTeamAgents, setChatTeamAgents] = useState<AppUserTeamAgentOption[]>([])
  const [selectedChatTeamAgentId, setSelectedChatTeamAgentId] = useState<string | null>(null)
  // 工具审批模式（auto/approval）：随每轮对话请求头下发；初始取用户级配置，面板内切换仅本地生效
  const [approvalMode, setApprovalMode] = useState<ToolApprovalMode>('auto')
  const exemptRef = useRef<ApprovalExemptRef>({ names: [], prefixes: [], autoNames: [], autoPrefixes: [] })
  // 输入卡附件：上传 → 提取（文档）→ ready，随下一条消息一起发送（与真实对话页同一状态机）
  const { attachments, handleFiles, removeAttachment, reset: resetAttachments } = useChatAttachments()
  // 前端展示工具（ui_ 前缀）产出内容的右侧侧边栏：多项标签集合 + 当前项 + 展开状态
  const [uiPanel, setUiPanel] = useState<{ items: UiPanelItem[]; activeKey: string | null; open: boolean }>({
    items: [],
    activeKey: null,
    open: false,
  })

  const userName = userInfo?.nickName || userInfo?.userName || 'U'
  const userAvatar = resolveStorageUrl(userInfo?.avatar ?? null)
  // 子智能体嵌套活动卡档案：key → 展示名/头像（userconfig 绑定列表；未绑定 key 回退 key 显示）
  const subAgentProfiles = useMemo(() => {
    const profiles: Record<string, { name?: string; avatar?: string }> = {}
    for (const agent of chatTeamAgents) {
      if (!agent.key) continue
      profiles[agent.key] = { name: agent.name, avatar: resolveStorageUrl(agent.avatarObjectKey ?? null) ?? undefined }
    }
    return profiles
  }, [chatTeamAgents])

  // 开场白为本地展示消息，不入调试会话历史
  const openingMessage = useMemo<DisplayMessage | null>(() => {
    const text = (openingStatement ?? '').trim()
    return text ? { id: OPENING_MESSAGE_ID, role: 'assistant', content: text } : null
  }, [openingStatement])

  // 欢迎态快捷输入（管理员配置草稿，点击即发送）
  const suggestions = useMemo(() => (quickInputs ?? []).map((x) => x.trim()).filter(Boolean), [quickInputs])

  // 会话视图消息：开场白只在欢迎态简报卡展示，不混入消息流（与真实对话页同口径）
  const viewMessages = useMemo(() => messages.filter((m) => m.id !== OPENING_MESSAGE_ID), [messages])
  const isWelcome = viewMessages.length === 0

  const ensureSession = useCallback(async (): Promise<string> => {
    if (sessionIdRef.current) return sessionIdRef.current
    const id = await createDebugSession(appId)
    sessionIdRef.current = id
    return id
  }, [appId])

  useEffect(() => {
    cancelledRef.current = false
    sessionIdRef.current = ''
    setMessages(openingMessage ? [openingMessage] : [])
    void ensureSession().catch(() => undefined)
    return () => {
      cancelledRef.current = true
      if (agentRef.current) abortAppChat(agentRef.current)
    }
    // openingMessage 随配置加载/保存晚于挂载到达，此处仅按 appId 重建会话，开场白由下方补齐
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appId, ensureSession])

  // 配置异步加载完成后（消息尚未开始时）补展示开场白，不覆盖已开始的调试对话
  useEffect(() => {
    setMessages((prev) => (prev.length === 0 && openingMessage ? [openingMessage] : prev))
  }, [openingMessage])

  // 调试会话按最新草稿装配（后端同口径）：切换器选项 = 草稿可选模型 ∩ 团队可用 conversation 模型；
  // 初始选中=应用默认模型（userconfig.defaultModelId）；审批模式初始取用户级配置，
  // 豁免/自动放通清单随 userconfig 下发（审批卡判定与真实对话页同口径）
  useEffect(() => {
    if (!appId) return
    getAppUserConfig(appId)
      .then((cfg) => {
        setApprovalMode(cfg.toolApprovalMode)
        exemptRef.current = {
          names: cfg.toolApprovalExemptNames,
          prefixes: cfg.toolApprovalExemptPrefixes,
          autoNames: cfg.toolApprovalAutoApprovedNames,
          autoPrefixes: cfg.toolApprovalAutoApprovedPrefixes,
        }
        const selectableIds = new Set(cfg.modelIds ?? [])
        if (selectableIds.size > 0) {
          getTeamAvailableModels(teamId)
            .then((models) => {
              const options = models.filter((m) => m.modelKind === 'conversation' && selectableIds.has(String(m.aiModelId)))
              setChatModelOptions(options)
              setSelectedChatModelId((prev) => {
                if (prev && options.some((m) => String(m.aiModelId) === prev)) return prev
                const preferred = cfg.defaultModelId || cfg.modelIds?.[0] || ''
                if (preferred && options.some((m) => String(m.aiModelId) === preferred)) return preferred
                return options[0] ? String(options[0].aiModelId) : null
              })
            })
            .catch(() => undefined)
        } else {
          setChatModelOptions([])
          setSelectedChatModelId(null)
        }
        // 团队智能体切换器不取 userconfig（快照口径）：调试对话按最新草稿装配，选项由下方草稿配置 effect 提供
      })
      .catch(() => undefined)

    // 调试团队智能体切换器：调试对话按最新草稿装配（后端同口径），选项与默认取应用草稿绑定的启用中智能体；
    // 多选（≥2）才出切换器，单选由后端直接生效；正式对话页按发布快照口径（userconfig），与此有意区分
    getAppAgentConfig(appId)
      .then((cfg) => {
        const options: AppUserTeamAgentOption[] = (cfg.teamAgents ?? [])
          .filter((a) => a.enabled !== false && a.id)
          .map((a) => ({
            id: String(a.id),
            key: a.key ?? null,
            name: a.name ?? '-',
            description: a.description ?? null,
            avatarObjectKey: a.avatarObjectKey ?? null,
          }))
        setChatTeamAgents(options)
        setSelectedChatTeamAgentId((prev) => {
          if (options.length < 2) return null
          if (prev && options.some((a) => a.id === prev)) return prev
          const preferred = cfg.defaultTeamAgentId || options[0]?.id || ''
          return options.some((a) => a.id === preferred) ? preferred : (options[0]?.id ?? null)
        })
      })
      .catch(() => undefined)
  }, [appId, teamId])

  /** 审批模式下无需审批卡的工具：审批策略自动放通（六类绑定资源白名单/沙箱开关解析出的工具名与前缀） */
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

  const runOnce = useCallback(
    async (outgoing: string, assistantId: string, sessionId: string) => {
      // 用户所选对话模型与审批模式随每轮下发（后端校验须在可选列表内，否则回退默认模型）；未切换时不携带模型
      agentRef.current = createAppChatAgent(appId, sessionId, { toolApprovalMode: approvalMode, modelId: selectedChatModelId ?? undefined, teamAgentId: selectedChatTeamAgentId ?? undefined })
      // 会话级写入闭包：流式回调一律写发送时锁定的助手消息
      const patchMessages = (updater: (prev: DisplayMessage[]) => DisplayMessage[]) =>
        setMessages((prev) => updater(prev))
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

      await runAppChat(agentRef.current!, outgoing, {
        onDelta: (buffer) => {
          if (buffer.includes(SESSION_NOT_FOUND_MARKER)) {
            expiredRef.current = true
          }
          const content = buffer.split(SESSION_NOT_FOUND_MARKER).join('')
          patchMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content } : m)))
          resolveToolCalls()
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
            if (item && (item.content ?? item.code ?? item.chart) != null) {
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
              // 审批模式下非豁免的重要工具挂起审批卡，等待批准/拒绝（豁免判定与真实对话页同口径）
              status:
                approvalMode === 'approval' && realName && !isExemptTool(realName) ? 'awaiting' : 'running',
            })
          } else if (info.name) {
            upsertToolCall({ id: info.id || crypto.randomUUID(), name: info.name, status: 'running' })
          }
        },
        onSubAgentEvent: (evt) => {
          // 子智能体派发过程：started 绑定到同名运行中派发卡，后续事件按 traceId 归并进该卡 trace
          patchMessages((prev) => applySubAgentEvent(prev, assistantId, evt))
        },
        onError: (message) =>
          patchMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appDebug.runError') } : m))),
      })
      resolveToolCalls()
    },
    [appId, approvalMode, isExemptTool, openUiPanelItem, selectedChatModelId, selectedChatTeamAgentId, t],
  )

  const send = useCallback(
    async (overrideText?: string) => {
      const text = (overrideText ?? input).trim()
      if ((!text && attachments.length === 0) || sending) return
      // 附件全部就绪才能发送（上传/提取中等待，失败项需移除）
      if (hasPendingAttachment(attachments) || attachments.some((a) => a.status === 'failed')) return

      // 发送文本 = 用户输入 + 附件标记块（文档为提取文本，图片为链接）；气泡渲染时解析回 chip
      const outgoing = buildOutgoingText(text, attachments)
      const assistantId = crypto.randomUUID()
      setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: 'user', content: outgoing }, { id: assistantId, role: 'assistant', content: '' }])
      setInput('')
      resetAttachments()
      setSending(true)
      expiredRef.current = false

      const resetAssistant = () =>
        setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: '', toolCalls: [] } : m)))

      try {
        const sessionId = await ensureSession()
        if (cancelledRef.current) return
        await runOnce(outgoing, assistantId, sessionId)

        // 注册表过期：重建一次调试会话后重试一次
        if (expiredRef.current && !cancelledRef.current) {
          expiredRef.current = false
          resetAssistant()
          sessionIdRef.current = ''
          const retryId = await ensureSession()
          if (cancelledRef.current) return
          await runOnce(outgoing, assistantId, retryId)
        }
      } catch {
        if (!cancelledRef.current) {
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appDebug.runError') } : m)))
        }
      } finally {
        if (!cancelledRef.current) setSending(false)
      }
    },
    [attachments, ensureSession, input, resetAttachments, runOnce, sending, t],
  )

  const stop = useCallback(() => {
    if (agentRef.current) abortAppChat(agentRef.current)
    setSending(false)
  }, [])

  // 审批决策：批准/拒绝挂起的工具调用；missing 表示后端无匹配记录（已自动执行）。
  // 调试会话决策走同端点，后端按调试注册表校验归属（@AP-S149）
  const decideTool = useCallback(
    async (messageId: string, toolCall: ToolCallDisplay, approved: boolean) => {
      const sessionId = sessionIdRef.current
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
    [],
  )

  // 审批模式切换：仅本地生效（随下轮请求头下发）；不持久化，避免调试面板改动真实对话端的用户级偏好
  const toggleApprovalMode = useCallback(() => {
    setApprovalMode((prev) => (prev === 'approval' ? 'auto' : 'approval'))
  }, [])

  const clear = useCallback(async () => {
    if (agentRef.current) abortAppChat(agentRef.current)
    sessionIdRef.current = ''
    setMessages(openingMessage ? [openingMessage] : [])
    setInput('')
    resetAttachments()
    setUiPanel({ items: [], activeKey: null, open: false })
    setSending(false)
    await ensureSession().catch(() => undefined)
  }, [ensureSession, openingMessage, resetAttachments])

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

  const placeholder = t('appChat.placeholderSendTo', { name: appName || t('appChat.title') })

  return (
    <div style={{ ...chatCssVars(token), display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <div className="moai-chat-debug__head">
        <div>
          <div className="moai-chat-debug__head-title">{t('appDebug.title')}</div>
          <div className="moai-chat-debug__head-hint">{t('appDebug.hint')}</div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {headerExtra}
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
      </div>

      <div className="moai-chat-debug__body">
        <div className="moai-chat-debug__main">
          {isWelcome ? (
            // 欢迎态：与真实对话页同布局（徽标 + 标题 + 开场白简报卡 + 输入卡 + 快捷输入）
            <div className="moai-chat__landing moai-chat-debug__landing">
              <div className="moai-chat__landing-hero">
                <div className="moai-chat__landing-badge">
                  {appAvatar ? (
                    <img src={appAvatar} alt={appName ?? ''} />
                  ) : (
                    (appName ?? '?').slice(0, 1).toUpperCase()
                  )}
                </div>
                <h1 className="moai-chat__landing-title">{appName || t('appChat.title')}</h1>
                {!openingMessage && <p className="moai-chat__landing-subtitle">{t('appChat.landingSubtitle')}</p>}
              </div>
              {openingMessage && (
                <div className="moai-chat__landing-briefing">
                  <div className="moai-chat__markdown moai-chat__landing-briefing-body">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{openingMessage.content}</ReactMarkdown>
                  </div>
                </div>
              )}
              <div className="moai-chat__landing-composer">
                <ChatComposer
                  value={input}
                  onValueChange={setInput}
                  onSubmit={() => void send()}
                  sending={sending}
                  onStop={stop}
                  placeholder={placeholder}
                  attachments={attachments}
                  onPickFiles={(files) => void handleFiles(files)}
                  onRemoveAttachment={removeAttachment}
                  approvalMode={approvalMode}
                  onToggleApprovalMode={toggleApprovalMode}
                  modelOptions={chatModelOptions}
                  selectedModelId={selectedChatModelId}
                  onModelChange={setSelectedChatModelId}
                  teamAgentOptions={chatTeamAgents}
                  selectedTeamAgentId={selectedChatTeamAgentId}
                  onTeamAgentChange={setSelectedChatTeamAgentId}
                />
                {suggestions.length > 0 && (
                  <div className="moai-chat__suggestions">
                    <span className="moai-chat__suggestions-label">
                      <BulbOutlined />
                      {t('appChat.quickAsk')}
                    </span>
                    <div className="moai-chat__suggestions-grid">
                      {suggestions.map((text) => (
                        <button key={text} type="button" className="moai-chat__suggestion" onClick={() => void send(text)}>
                          <EditOutlined className="moai-chat__suggestion-icon" />
                          <span className="moai-chat__suggestion-text">{text}</span>
                          <ArrowRightOutlined className="moai-chat__suggestion-arrow" />
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          ) : (
            <>
              <div className="moai-chat__scroll moai-chat-debug__scroll">
                <ChatMessageList
                  messages={viewMessages}
                  sending={sending}
                  appAvatar={appAvatar}
                  userName={userName}
                  userAvatar={userAvatar}
                  subAgentProfiles={subAgentProfiles}
                  onCopy={(text) => void copy(text)}
                  onToolApprove={(messageId, toolCall) => void decideTool(messageId, toolCall, true)}
                  onToolReject={(messageId, toolCall) => void decideTool(messageId, toolCall, false)}
                  onOpenUiTool={(_, toolCall) => {
                    const item = toUiPanelItem(toolCall)
                    if (item) openUiPanelItem(item)
                  }}
                  activeUiToolId={uiPanel.open ? uiPanel.activeKey ?? undefined : undefined}
                />
              </div>
              <div className="moai-chat__composer-wrap moai-chat-debug__composer-wrap">
                <div className="moai-chat__composer-inner">
                  <ChatComposer
                    value={input}
                    onValueChange={setInput}
                    onSubmit={() => void send()}
                    sending={sending}
                    onStop={stop}
                    placeholder={placeholder}
                    attachments={attachments}
                    onPickFiles={(files) => void handleFiles(files)}
                    onRemoveAttachment={removeAttachment}
                    approvalMode={approvalMode}
                    onToggleApprovalMode={toggleApprovalMode}
                    modelOptions={chatModelOptions}
                    selectedModelId={selectedChatModelId}
                    onModelChange={setSelectedChatModelId}
                    teamAgentOptions={chatTeamAgents}
                    selectedTeamAgentId={selectedChatTeamAgentId}
                    onTeamAgentChange={setSelectedChatTeamAgentId}
                  />
                </div>
              </div>
            </>
          )}
        </div>

        {/* 前端展示工具侧边栏：与真实对话页同组件，窄屏时浮层锚定本面板 */}
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
    </div>
  )
}
