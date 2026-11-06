import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { DeleteOutlined, MessageOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Avatar, Button, Checkbox, Col, Collapse, Divider, Form, Input, InputNumber, Row, Select, Space, Spin, Switch, Tag, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppAgentConfig,
  getApps,
  getCompactionDefaults,
  getSandboxLimits,
  publishApp,
  saveAppAgentConfig,
  type AgentCompactionDefaults,
  type AppAgentConfig,
  type AppKind,
  type AppItem,
  type SandboxLimits,
} from '@/api/app'
import { getTeamAvailableModels, type TeamAvailableModelItem } from '@/api/aichannel'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getTeamAgents, type TeamAgentListItem } from '@/api/team-agent'
import { getSkillOptions, type SkillOption } from '@/api/skills'
import { getWikis, type WikiItem } from '@/api/wiki'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'
import { resolveStorageUrl } from '@/utils/storage'
import { parseCpuMillicores, parseMemoryBytes } from '@/utils/sandboxQuantity'
import { MAX_RULES_PER_POLICY, isValidRulePorts, parseNetworkTarget } from '@/utils/sandboxNetwork'
import { isValidCommandPatternList, parseCommandPatterns } from '@/utils/sandboxCommand'
import { AppDebugChat } from './chat/AppDebugChat'
import { AppSecuritySettingsPanel } from './AppSecuritySection'
import { AppMemorySettingsPanel } from './AppMemoryPanel'

/** 后端 memory 静态插件无 DB 记录，pluginId 为空 Guid，不能作为绑定目标 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

/** 快捷输入上限（与后端 SaveAppAgentConfigCommand 校验一致），每条最长 200 字符由 Input maxLength 限制 */
const MAX_QUICK_INPUTS = 10

/** 审批策略：六类绑定资源的自动放通白名单（存于 executionSettings.toolApproval，键名与后端契约一致） */
interface AutoPassState {
  plugins: string[]
  workflowApps: string[]
  agentApps: string[]
  skills: string[]
  wikis: number[]
  graphs: number[]
}

const EMPTY_AUTO_PASS: AutoPassState = { plugins: [], workflowApps: [], agentApps: [], skills: [], wikis: [], graphs: [] }

/** 沙箱出站规则编辑行：portsText 为逗号分隔端口文本，保存时解析校验（与后端 per-rule 模型一致） */
interface SandboxNetworkRuleRow {
  action: 'allow' | 'deny'
  target: string
  portsText: string
}

/** 自动放通列表行：绑定资源 id、展示名与头像（无头像回退首字母） */
interface AutoPassItem {
  id: string | number
  name: string
  avatarPath?: string | null
}

interface AutoPassListProps {
  items: AutoPassItem[]
  checkedIds: Array<string | number>
  onToggle: (id: string | number, checked: boolean) => void
  /** 「全部放通」总开关：勾选=列表内全部资源自动放通，取消=全部恢复人工批准 */
  onToggleAll: (checked: boolean) => void
}

/** 自动放通列表折叠阈值：超过该条数默认只展示前 N 条，可展开全部；展开后容器限高滚动 */
const AUTO_PASS_COLLAPSE_COUNT = 6
/** 自动放通列表展开后的最大高度（px），超出内部滚动 */
const AUTO_PASS_MAX_HEIGHT = 280

/**
 * 绑定资源自动放通列表：选择器选中资源后以紧凑列表逐行展示（头像 + 名称左对齐、右侧「自动放通」勾选），
 * 表头「全部放通」总开关一键全选/全清（部分勾选时呈半选态）；绑定资源多时默认收起、可展开全部，
 * 展开后限高内部滚动——审批模式下勾选资源的工具调用直接执行，未勾选的每次调用需人工批准
 * （保存时收敛为当前绑定资源的子集）.
 */
function AutoPassList({ items, checkedIds, onToggle, onToggleAll }: AutoPassListProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [expanded, setExpanded] = useState(false)
  if (items.length === 0) return null
  const collapsible = items.length > AUTO_PASS_COLLAPSE_COUNT
  const visibleItems = collapsible && !expanded ? items.slice(0, AUTO_PASS_COLLAPSE_COUNT) : items
  const allChecked = items.every((item) => checkedIds.includes(item.id))
  const someChecked = items.some((item) => checkedIds.includes(item.id))
  return (
    <div style={{ margin: `${spacing.xs}px 0 ${spacing.md}px` }}>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'flex-end',
          padding: `${spacing.xs}px 0`,
          borderBottom: `1px solid ${token.colorSplit}`,
        }}
      >
        <Checkbox
          checked={allChecked}
          indeterminate={!allChecked && someChecked}
          onChange={(e) => onToggleAll(e.target.checked)}
        >
          {t('appManage.autoPassToggleAll')}
        </Checkbox>
      </div>
      <div style={{ maxHeight: collapsible ? AUTO_PASS_MAX_HEIGHT : undefined, overflowY: 'auto' }}>
        {visibleItems.map((item) => (
          <div
            key={String(item.id)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.sm,
              padding: `${spacing.xs}px 0`,
              borderBottom: `1px solid ${token.colorSplit}`,
            }}
          >
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
              <Avatar shape="square" size={20} src={resolveStorageUrl(item.avatarPath) || undefined} alt={item.name}>
                {item.name.slice(0, 1).toUpperCase()}
              </Avatar>
              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.name}</span>
            </span>
            <Checkbox checked={checkedIds.includes(item.id)} onChange={(e) => onToggle(item.id, e.target.checked)}>
              {t('appManage.autoPassToggle')}
            </Checkbox>
          </div>
        ))}
      </div>
      {collapsible && (
        <Button
          type="link"
          size="small"
          style={{ padding: 0, marginTop: spacing.xs }}
          onClick={() => setExpanded((prev) => !prev)}
        >
          {expanded ? t('appManage.autoPassCollapse') : t('appManage.autoPassExpand', { count: items.length })}
        </Button>
      )}
    </div>
  )
}

export interface AppDetail {
  appId?: string | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  appType?: AppKind | null
  avatarPath?: string | null
  isExternal?: boolean | null
  isAuth?: boolean | null
  isPublic?: boolean | null
  /** 分类 id，0=未分类 */
  classifyId?: number | null
  publishStatus?: number | null
  publishTime?: string | null
  /** 线上版本号（发布时快照的 int 编码），未发布为 null */
  publishVersion?: number | null
  /** 应用版本号（int 编码 aaa*10000+bb*100+cc，显示 aaa.bb.cc） */
  version?: number | null
  /** 对话开场白与启用开关、快捷输入（详情按发布快照下发，调试面板草稿预览） */
  openingStatement?: string | null
  openingStatementEnabled?: boolean | null
  quickInputs?: string[] | null
  myRole?: number | null
}

export interface AppConfigSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  loading: boolean
  canManage: boolean
  /** 配置状态（工作台持有的真值，头部「发布」与本地警告条共用）：0=草稿有未发布变更 1=一致；null/缺省回退本地加载值 */
  configStatus?: number | null
  /** 配置状态变化上报（加载/保存/发布时） */
  onConfigStatusChange?: (status: number) => void
}

/**
 * 应用「配置」分区：左栏把 Agent 配置拆为收缩卡片（基础配置/提示词/能力扩展/沙箱，默认全部展开；
 * 「内容脱敏」由原独立「安全」分区并入、默认收起；「长期记忆」由原独立「记忆」分区并入、默认收起，
 * 两者均仅管理员可见，长期记忆仅内部应用），右栏调试对话面板。能力扩展各选择器选中资源后，
 * 下方逐项列出绑定资源并按行勾选「自动放通」（审批模式策略，随执行参数 toolApproval 节保存）。
 * 「保存设置」一次提交全部卡片：主配置走 agent-config（草稿/发布快照，长期记忆 memory 节随主配置一并提交），
 * 内容脱敏走 security 接口即时生效。资源绑定只能选择该团队有权使用的模型/插件/知识库；
 * 基础信息（头像/名称/公开状态）在「信息」分区维护（AppInfoSection）。
 */
export function AppConfigSection({ teamId, appId, detail, loading, canManage, configStatus, onConfigStatusChange }: AppConfigSectionProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { token } = theme.useToken()

  /** 收缩卡片头：标题 + 一行能力摘要，整行点击展开/收起 */
  const groupHeader = (title: string, desc: string) => (
    <div>
      <div style={{ fontWeight: 600 }}>{title}</div>
      <div style={{ fontSize: 12, fontWeight: 400, color: token.colorTextTertiary }}>{desc}</div>
    </div>
  )

  /** 必填标记：标题前红 *（与「信息」分区应用名称的 antd 内建必填标记同款式）；保存与后端双重强校验 */
  const requiredLabel = (label: string) => (
    <span>
      <span aria-hidden style={{ color: token.colorError, marginInlineEnd: 4, userSelect: 'none' }}>*</span>
      {label}
    </span>
  )

  const [modelId, setModelId] = useState<string>()
  // 可选模型列表（开放给用户在对话端切换，须含默认模型；后端保存强校验与上限 20）
  const [modelIds, setModelIds] = useState<string[]>([])
  const [prompt, setPrompt] = useState('')
  const [openingEnabled, setOpeningEnabled] = useState(false)
  const [openingStatement, setOpeningStatement] = useState('')
  const [quickInputs, setQuickInputs] = useState<string[]>([])
  const [wikiIds, setWikiIds] = useState<number[]>([])
  const [graphIds, setGraphIds] = useState<number[]>([])
  const [pluginIds, setPluginIds] = useState<string[]>([])
  const [workflowAppIds, setWorkflowAppIds] = useState<string[]>([])
  const [workflowAppOptions, setWorkflowAppOptions] = useState<AppItem[]>([])
  const [agentAppIds, setAgentAppIds] = useState<string[]>([])
  const [agentAppOptions, setAgentAppOptions] = useState<AppItem[]>([])
  const [skillIds, setSkillIds] = useState<string[]>([])
  const [sandboxEnabled, setSandboxEnabled] = useState(false)
  const [sandboxTimeout, setSandboxTimeout] = useState<number | null>(null)
  const [sandboxRenew, setSandboxRenew] = useState(true)
  const [sandboxCpu, setSandboxCpu] = useState('')
  const [sandboxMemory, setSandboxMemory] = useState('')
  const [sandboxNetworkAction, setSandboxNetworkAction] = useState<string>()
  // 出站规则行列表（保存时写入 sandbox.network.rules；旧格式 egress 只读兼容转换后回显）
  const [sandboxNetworkRules, setSandboxNetworkRules] = useState<SandboxNetworkRuleRow[]>([])
  // 命令过滤（应用级黑名单，运行时与平台级禁用清单取并集；写入 sandbox.commandFilter）
  const [sandboxCommandFilterEnabled, setSandboxCommandFilterEnabled] = useState(false)
  const [sandboxCommandDeny, setSandboxCommandDeny] = useState('')
  const [sandboxLimits, setSandboxLimits] = useState<SandboxLimits | null>(null)
  const [executionSettings, setExecutionSettings] = useState<Record<string, unknown>>({})
  // 审批策略：六类绑定资源的自动放通白名单与沙箱开关（存于 executionSettings.toolApproval）
  const [autoPass, setAutoPass] = useState<AutoPassState>(EMPTY_AUTO_PASS)
  const [sandboxAutoApproved, setSandboxAutoApproved] = useState(false)
  // 上下文压缩（存于 executionSettings 顶层字段）：auto=按模型窗口比例推导阈值（推荐），custom=按绝对值运行
  const [compactionMode, setCompactionMode] = useState<'auto' | 'custom'>('auto')
  const [compactionToolResult, setCompactionToolResult] = useState<number | null>(null)
  const [compactionSummarizeTokens, setCompactionSummarizeTokens] = useState<number | null>(null)
  const [compactionHardLimit, setCompactionHardLimit] = useState<number | null>(null)
  const [compactionSummarization, setCompactionSummarization] = useState(false)
  const [compactionSummarizerModelId, setCompactionSummarizerModelId] = useState<string>()
  const [compactionDefaults, setCompactionDefaults] = useState<AgentCompactionDefaults | null>(null)
  // 子代理协作（存于 executionSettings.subAgent）
  const [subAgentEnabled, setSubAgentEnabled] = useState(false)
  const [subAgentTimeout, setSubAgentTimeout] = useState<number | null>(null)
  const [subAgentResultMaxChars, setSubAgentResultMaxChars] = useState<number | null>(null)
  // 团队智能体绑定（team_agent，子代理协作下方）：多选 ≤20，多选时对话端可切换生效者；绑定须开启子代理协作
  const [teamAgentIds, setTeamAgentIds] = useState<string[]>([])
  const [defaultTeamAgentId, setDefaultTeamAgentId] = useState<string>()
  const [teamAgentOptions, setTeamAgentOptions] = useState<TeamAgentListItem[]>([])
  // 配置回显的智能体选项（含弱依赖声明）：绑定缺失提示「一键添加」的数据源
  const [teamAgentEchoOptions, setTeamAgentEchoOptions] = useState<AppAgentConfig['teamAgents']>([])
  const [pluginOptions, setPluginOptions] = useState<TeamPluginItemType[]>([])
  const [skillOptions, setSkillOptions] = useState<SkillOption[]>([])
  const [wikiOptions, setWikiOptions] = useState<WikiItem[]>([])
  const [graphOptions, setGraphOptions] = useState<KnowledgeGraphItem[]>([])
  const [availableModels, setAvailableModels] = useState<TeamAvailableModelItem[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [publishing, setPublishing] = useState(false)
  // 0=草稿有未发布变更 1=草稿与已发布一致（已发布应用 0 时线上仍按发布快照执行）
  const [localConfigStatus, setLocalConfigStatus] = useState(0)
  // 内容脱敏面板注册的保存回调（展开「内容脱敏」卡片并加载完成后才有值）：随「保存设置」一并提交
  const [securitySave, setSecuritySave] = useState<(() => Promise<void>) | null>(null)
  // 长期记忆面板注册的 memory 节构造回调（展开「长期记忆」卡片并加载完成后才有值）：随「保存设置」并入 executionSettings
  const memorySettingsRef = useRef<(() => Record<string, unknown>) | null>(null)

  const isAgent = detail?.appType !== 'workflow'
  // 外部应用面向外部用户/匿名开放，不支持沙箱与技能（后端保存强校验 + 对话装配兜底强制关闭）
  const isExternal = detail?.isExternal === true
  // 平台已强制默认拒绝（deny/intranet 管控模式）：应用不能配置默认允许，只能追加放行规则
  const sandboxNetworkGoverned =
    sandboxLimits?.networkMode === 'deny' || sandboxLimits?.networkMode === 'intranet'

  // 警告条/发布入口使用的状态：工作台传入的真值优先（头部发布后同步清除），否则用本地加载值
  const effectiveConfigStatus = configStatus ?? localConfigStatus

  useEffect(() => {
    if (loading || !appId || !isAgent) return
    const loadConfig = async () => {
      try {
        const [config, limits, defaults] = await Promise.all([getAppAgentConfig(appId), getSandboxLimits(), getCompactionDefaults()])
        setSandboxLimits(limits)
        setCompactionDefaults(defaults)
        setLocalConfigStatus(config.status ?? 0)
        onConfigStatusChange?.(config.status ?? 0)
        // 后端未选择模型时返回全零 Guid，归一为未选择以显示占位符而非裸串
        setModelId(config.modelId && config.modelId !== EMPTY_GUID ? config.modelId : undefined)
        // 可选模型回显：存量行 model_ids 为空时归一为仅默认模型（与后端运行时口径一致）
        const loadedModelIds = (config.modelIds ?? []).filter((id) => id && id !== EMPTY_GUID)
        setModelIds(
          loadedModelIds.length > 0
            ? loadedModelIds
            : config.modelId && config.modelId !== EMPTY_GUID
              ? [config.modelId]
              : [],
        )
        setPrompt(config.prompt ?? '')
        setOpeningEnabled(Boolean(config.openingStatementEnabled))
        setOpeningStatement(config.openingStatement ?? '')
        setQuickInputs(config.quickInputs ?? [])
        setWikiIds(config.wikiIds ?? [])
        setGraphIds(config.graphIds ?? [])
        setTeamAgentIds(config.teamAgentIds ?? [])
        setDefaultTeamAgentId(config.defaultTeamAgentId || undefined)
        setTeamAgentEchoOptions(config.teamAgents ?? [])
        setPluginIds(config.plugins ?? [])
        setWorkflowAppIds(config.workflowApps ?? [])
        setAgentAppIds(config.agentApps ?? [])
        setSkillIds(config.skills ?? [])
        const settings = config.executionSettings ?? {}
        setExecutionSettings(settings)
        setCompactionMode(settings.compactionMode === 'custom' ? 'custom' : 'auto')
        const sandbox = (settings.sandbox ?? {}) as Record<string, unknown>
        setSandboxEnabled(Boolean(sandbox.enabled))
        setSandboxTimeout(typeof sandbox.timeoutSeconds === 'number' ? (sandbox.timeoutSeconds as number) : null)
        setSandboxRenew(sandbox.renewOnAccess !== false)
        // 输入框预填生效值（custom 模式生效；auto 模式由运行时按模型窗口推导）：应用显式配置优先，未配置的字段写平台默认值
        setCompactionToolResult(typeof settings.toolResultTriggerMessages === 'number' ? (settings.toolResultTriggerMessages as number) : defaults.toolResultTriggerMessages)
        setCompactionSummarizeTokens(typeof settings.summarizeTriggerTokens === 'number' ? (settings.summarizeTriggerTokens as number) : defaults.summarizeTriggerTokens)
        setCompactionHardLimit(typeof settings.hardTokenLimit === 'number' ? (settings.hardTokenLimit as number) : defaults.hardTokenLimit)
        setCompactionSummarization(typeof settings.enableSummarization === 'boolean' ? (settings.enableSummarization as boolean) : defaults.enableSummarization)
        setCompactionSummarizerModelId(typeof settings.summarizerModelId === 'string' && settings.summarizerModelId ? settings.summarizerModelId : undefined)
        const subAgent = (settings.subAgent ?? {}) as Record<string, unknown>
        setSubAgentEnabled(subAgent.enabled === true)
        setSubAgentTimeout(typeof subAgent.timeoutSeconds === 'number' ? (subAgent.timeoutSeconds as number) : null)
        setSubAgentResultMaxChars(typeof subAgent.resultMaxChars === 'number' ? (subAgent.resultMaxChars as number) : null)
        const toolApproval = (settings.toolApproval ?? {}) as Record<string, unknown>
        const asIdList = (value: unknown): string[] =>
          Array.isArray(value) ? value.map((x) => String(x)).filter(Boolean) : []
        const asLongList = (value: unknown): number[] =>
          Array.isArray(value) ? value.map((x) => Number(x)).filter((x) => Number.isFinite(x) && x > 0) : []
        setAutoPass({
          plugins: asIdList(toolApproval.autoApprovePlugins),
          workflowApps: asIdList(toolApproval.autoApproveWorkflowApps),
          agentApps: asIdList(toolApproval.autoApproveAgentApps),
          skills: asIdList(toolApproval.autoApproveSkills),
          wikis: asLongList(toolApproval.autoApproveWikis),
          graphs: asLongList(toolApproval.autoApproveGraphs),
        })
        setSandboxAutoApproved(toolApproval.sandboxAutoApproved === true)
        const resource = (sandbox.resource ?? {}) as Record<string, unknown>
        setSandboxCpu(typeof resource.cpu === 'string' ? (resource.cpu as string) : '')
        setSandboxMemory(typeof resource.memory === 'string' ? (resource.memory as string) : '')
        const network = (sandbox.network ?? {}) as Record<string, unknown>
        setSandboxNetworkAction(typeof network.defaultAction === 'string' ? (network.defaultAction as string) : undefined)
        // 新格式 rules 优先；旧格式 egress（字符串数组）按旧语义转换回显：默认 deny 时为放行名单，否则为拒绝名单
        const toRuleRows = (): SandboxNetworkRuleRow[] => {
          if (Array.isArray(network.rules)) {
            return (network.rules as unknown[])
              .filter((x): x is Record<string, unknown> => typeof x === 'object' && x !== null)
              .map((x) => ({
                action: x.action === 'deny' ? 'deny' : 'allow',
                target: typeof x.target === 'string' ? x.target : '',
                portsText: Array.isArray(x.ports) ? (x.ports as unknown[]).map((p) => String(p)).join(',') : '',
              }))
          }
          if (Array.isArray(network.egress)) {
            const legacyAction = network.defaultAction === 'deny' ? 'allow' : 'deny'
            return (network.egress as unknown[])
              .map((x) => String(x).trim())
              .filter(Boolean)
              .map((target) => ({ action: legacyAction, target, portsText: '' }))
          }
          return []
        }
        setSandboxNetworkRules(toRuleRows())
        const commandFilter = (sandbox.commandFilter ?? {}) as Record<string, unknown>
        setSandboxCommandFilterEnabled(commandFilter.enabled === true)
        setSandboxCommandDeny(Array.isArray(commandFilter.deny) ? (commandFilter.deny as unknown[]).map((x) => String(x)).join('\n') : '')
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    }
    void loadConfig()
  }, [appId, isAgent, loading, onConfigStatusChange])

  /** 团队可访问的模型/插件/知识库/知识图谱选项（资源绑定的取值范围） */
  const loadOptions = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setOptionsLoading(true)
    try {
      const [models, plugins, wikis, skills, apps, graphs, teamAgents] = await Promise.all([
        getTeamAvailableModels(teamId),
        getTeamPlugins(teamId),
        getWikis(teamId),
        // 默认技能不含个人技能；配置后默认启用，用户在对话的应用设置中可取消勾选
        getSkillOptions({ teamId }),
        // 流程应用绑定候选：本团队已发布的内部流程应用（对话中作为工具调用）
        getApps(teamId),
        // 图谱绑定候选：仅托管图（接入图不参与应用侧向量检索，不开放绑定）
        getKnowledgeGraphs(teamId),
        // 团队智能体绑定候选：本团队启用中的智能体（含系统内置规划师/构建师）
        getTeamAgents(teamId),
      ])
      setAvailableModels(models.filter((item) => item.aiModelId))
      setPluginOptions(
        (plugins.items ?? []).filter((item) => item.pluginId && String(item.pluginId) !== EMPTY_GUID),
      )
      setSkillOptions(skills)
      setWikiOptions(wikis.items ?? [])
      setGraphOptions((graphs.items ?? []).filter((item) => item.mode === 'managed' && item.kgId != null))
      setWorkflowAppOptions(
        (apps.items ?? []).filter(
          (item) => item.appType === 'workflow' && item.publishStatus === 1 && item.appId,
        ),
      )
      // Agent 应用绑定候选：本团队已发布的 Agent 应用（对话中作为工具无头单轮调用，自身在选项派生时排除）
      setAgentAppOptions(
        (apps.items ?? []).filter((item) => item.appType === 'agent' && item.publishStatus === 1 && item.appId),
      )
      setTeamAgentOptions((teamAgents.items ?? []).filter((item) => item.id && item.enabled !== false))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setOptionsLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    if (!isAgent) return
    void loadOptions()
  }, [isAgent, loadOptions])

  /** 启用沙箱时校验存活时间 / CPU / 内存不得超出系统上限（后端保存时会再次强校验） */
  const validateSandboxLimits = (): boolean => {
    if (!sandboxEnabled || !sandboxLimits) return true
    if (sandboxTimeout && sandboxTimeout > sandboxLimits.maxTtlSeconds) {
      feedback.error(t('appManage.sandboxLimitTtlExceeded', { max: sandboxLimits.maxTtlSeconds }))
      return false
    }
    const cpuText = sandboxCpu.trim()
    if (cpuText) {
      const cpuValue = parseCpuMillicores(cpuText)
      const cpuLimit = parseCpuMillicores(sandboxLimits.maxCpu)
      if (cpuValue == null || cpuLimit == null) {
        feedback.error(t('appManage.sandboxQuantityInvalid'))
        return false
      }
      if (cpuValue > cpuLimit) {
        feedback.error(t('appManage.sandboxLimitCpuExceeded', { max: sandboxLimits.maxCpu }))
        return false
      }
    }
    const memoryText = sandboxMemory.trim()
    if (memoryText) {
      const memoryValue = parseMemoryBytes(memoryText)
      const memoryLimit = parseMemoryBytes(sandboxLimits.maxMemory)
      if (memoryValue == null || memoryLimit == null) {
        feedback.error(t('appManage.sandboxQuantityInvalid'))
        return false
      }
      if (memoryValue > memoryLimit) {
        feedback.error(t('appManage.sandboxLimitMemoryExceeded', { max: sandboxLimits.maxMemory }))
        return false
      }
    }
    return validateSandboxNetwork()
  }

  /** 解析逗号分隔端口文本为端口数组；空文本返回空数组，非法（非整数 / 越界 / 重复 / 超量）返回 null */
  const parsePortsText = (text: string): number[] | null => {
    const trimmed = text.trim()
    if (!trimmed) return []
    const ports: number[] = []
    for (const part of trimmed.split(',')) {
      const value = part.trim()
      if (!value) continue
      const n = Number(value)
      if (!Number.isInteger(n) || n < 1 || n > 65535) return null
      ports.push(n)
    }
    return isValidRulePorts(ports) ? ports : null
  }

  /** 启用沙箱时校验出站规则（后端保存时再次强校验；与后端 SandboxNetworkTarget 语义一致） */
  const validateSandboxNetwork = (): boolean => {
    if (!sandboxEnabled) return true
    if (sandboxNetworkRules.length > MAX_RULES_PER_POLICY) {
      feedback.error(t('appManage.sandboxNetworkRuleCountExceeded', { max: MAX_RULES_PER_POLICY }))
      return false
    }
    for (const rule of sandboxNetworkRules) {
      const target = rule.target.trim()
      const ports = parsePortsText(rule.portsText)
      if (ports == null) {
        feedback.error(t('appManage.sandboxNetworkRulePortsInvalid'))
        return false
      }
      const { valid, isIpOrCidr } = parseNetworkTarget(target)
      if (!target && ports.length === 0) {
        feedback.error(t('appManage.sandboxNetworkRuleTargetRequired'))
        return false
      }
      if (target && !valid) {
        feedback.error(t('appManage.sandboxNetworkRuleTargetInvalid', { target }))
        return false
      }
      // 与服务端对齐：域名目标在 DNS 层评估，不支持端口限制
      if (ports.length > 0 && !isIpOrCidr) {
        feedback.error(t('appManage.sandboxNetworkRulePortsIpOnly', { target }))
        return false
      }
    }
    return validateSandboxCommandFilter()
  }

  /** 启用沙箱时校验命令过滤黑名单（后端保存时再次强校验；与后端 SandboxCommandPatterns 语义一致） */
  const validateSandboxCommandFilter = (): boolean => {
    if (!sandboxEnabled) return true
    if (!sandboxCommandFilterEnabled) return true
    const lines = sandboxCommandDeny.split('\n').map((x) => x.trim()).filter(Boolean)
    if (lines.length === 0) {
      feedback.error(t('appManage.sandboxCommandDenyRequired'))
      return false
    }
    if (!isValidCommandPatternList(sandboxCommandDeny)) {
      feedback.error(t('appManage.sandboxCommandPatternInvalid'))
      return false
    }
    return true
  }

  const addNetworkRule = () => setSandboxNetworkRules((prev) => [...prev, { action: 'allow', target: '', portsText: '' }])
  const updateNetworkRule = (index: number, patch: Partial<SandboxNetworkRuleRow>) =>
    setSandboxNetworkRules((prev) => prev.map((rule, i) => (i === index ? { ...rule, ...patch } : rule)))
  const removeNetworkRule = (index: number) => setSandboxNetworkRules((prev) => prev.filter((_, i) => i !== index))

  /** 内容脱敏面板保存回调注册（须稳定引用，面板以之为 effect 依赖） */
  const registerSecuritySave = useCallback((save: (() => Promise<void>) | null) => {
    setSecuritySave(() => save)
  }, [])

  /** 长期记忆面板 memory 节构造回调注册（须稳定引用，面板以之为 effect 依赖） */
  const registerMemorySettings = useCallback((get: (() => Record<string, unknown>) | null) => {
    memorySettingsRef.current = get
  }, [])

  const handleSaveConfig = async () => {
    if (!appId) return
    // 对话模型与系统提示词必填（与后端保存强校验同语义；不存在「默认模型」回退）
    if (!modelId) {
      feedback.error(t('appManage.modelRequired'))
      return
    }
    // 默认模型必须落在可选列表内（后端保存强校验同语义；勾选联动已保证，此处兜底）
    const submitModelIds = modelIds.length > 0 ? modelIds : [modelId]
    if (!submitModelIds.includes(modelId)) {
      feedback.error(t('appManage.defaultModelRequired'))
      return
    }
    if (!prompt.trim()) {
      feedback.error(t('appManage.promptRequired'))
      return
    }
    if (!isExternal && !validateSandboxLimits()) return
    // 团队智能体绑定依赖子代理协作派发链路（后端保存强校验同语义），保存前本地提示
    if (teamAgentIds.length > 0 && !subAgentEnabled) {
      feedback.error(t('appManage.teamAgentRequireSubAgent'))
      return
    }
    setSavingConfig(true)
    try {
      // 内容脱敏随「保存设置」一并提交：security 接口即时生效（校验/提交失败抛错，中断整体保存）
      // 卡片未展开或仍在加载时未注册回调，本次仅保存主配置
      if (securitySave) await securitySave()
      // 外部应用不支持沙箱：固定关闭，避免携带历史开启状态触发后端 400
      const sandbox: Record<string, unknown> = isExternal
        ? { enabled: false }
        : { enabled: sandboxEnabled, renewOnAccess: sandboxRenew }
      if (!isExternal) {
        if (sandboxTimeout && sandboxTimeout > 0) sandbox.timeoutSeconds = sandboxTimeout
        if (sandboxCpu.trim() || sandboxMemory.trim()) {
          sandbox.resource = {
            ...(sandboxCpu.trim() ? { cpu: sandboxCpu.trim() } : {}),
            ...(sandboxMemory.trim() ? { memory: sandboxMemory.trim() } : {}),
          }
        }
        const networkRules = sandboxNetworkRules
          .map((rule) => {
            const target = rule.target.trim()
            const ports = parsePortsText(rule.portsText) ?? []
            return {
              action: rule.action === 'deny' ? 'deny' : 'allow',
              ...(target ? { target } : {}),
              ...(ports.length ? { ports } : {}),
            }
          })
          .filter((rule) => rule.target || (rule.ports?.length ?? 0) > 0)
        if (sandboxNetworkAction || networkRules.length) {
          sandbox.network = {
            ...(sandboxNetworkAction ? { defaultAction: sandboxNetworkAction } : {}),
            rules: networkRules,
          }
        }
        const commandDenyLines = parseCommandPatterns(sandboxCommandDeny)
        if (sandboxCommandFilterEnabled || commandDenyLines.length) {
          sandbox.commandFilter = {
            enabled: sandboxCommandFilterEnabled,
            deny: commandDenyLines,
          }
        }
      }

      // 审批策略：六类自动放通白名单收敛为本次绑定资源的子集（后端强校验），避免解绑资源后保存失败
      const toolApproval = {
        autoApprovePlugins: autoPass.plugins.filter((id) => pluginIds.includes(id)),
        autoApproveWorkflowApps: autoPass.workflowApps.filter((id) => workflowAppIds.includes(id)),
        autoApproveAgentApps: autoPass.agentApps.filter((id) => agentAppIds.includes(id)),
        // 外部应用不支持技能：固定空列表（后端强校验拒绝非空）
        autoApproveSkills: isExternal ? [] : autoPass.skills.filter((id) => skillIds.includes(id)),
        autoApproveWikis: autoPass.wikis.filter((id) => wikiIds.includes(id)),
        autoApproveGraphs: autoPass.graphs.filter((id) => graphIds.includes(id)),
        sandboxAutoApproved: isExternal ? false : sandboxAutoApproved,
      }

      // 压缩参数：模式 + 表单预填生效值（custom 模式生效；auto 模式运行时按模型窗口推导、忽略绝对值），随发布快照生效
      const mergedSettings: Record<string, unknown> = { ...executionSettings }
      mergedSettings.compactionMode = compactionMode
      mergedSettings.toolResultTriggerMessages = compactionToolResult ?? compactionDefaults?.toolResultTriggerMessages ?? 16
      mergedSettings.summarizeTriggerTokens = compactionSummarizeTokens ?? compactionDefaults?.summarizeTriggerTokens ?? 16000
      mergedSettings.hardTokenLimit = compactionHardLimit ?? compactionDefaults?.hardTokenLimit ?? 64000
      mergedSettings.enableSummarization = compactionSummarization
      mergedSettings.summarizerModelId = compactionSummarization ? (compactionSummarizerModelId ?? '') : ''

      // 子代理协作：外部应用固定关闭（后端保存强校验 + 装配兜底）
      const subAgent: Record<string, unknown> = isExternal
        ? { enabled: false }
        : { enabled: subAgentEnabled }
      if (!isExternal && subAgentEnabled) {
        if (subAgentTimeout != null && subAgentTimeout > 0) subAgent.timeoutSeconds = subAgentTimeout
        if (subAgentResultMaxChars != null && subAgentResultMaxChars > 0) subAgent.resultMaxChars = subAgentResultMaxChars
      }

      // 长期记忆随「保存设置」一并提交（开启未选向量化模型时回调抛错中断整体保存）；
      // 卡片未展开时未注册回调，memory 节保持加载时原值不动
      if (memorySettingsRef.current) mergedSettings.memory = memorySettingsRef.current()

      await saveAppAgentConfig(appId, {
        modelId: modelId ?? null,
        modelIds: submitModelIds,
        prompt,
        wikiIds,
        graphIds,
        teamAgentIds,
        // 多选时默认智能体随表单（未选落第一个，后端同语义）；单选/未绑定时传空由后端归一
        defaultTeamAgentId: teamAgentIds.length > 1 ? (defaultTeamAgentId ?? null) : null,
        plugins: pluginIds,
        workflowApps: workflowAppIds,
        agentApps: agentAppIds,
        // 外部应用不支持技能：固定空列表（后端强校验拒绝非空）
        skills: isExternal ? [] : skillIds,
        openingStatement,
        openingStatementEnabled: openingEnabled,
        quickInputs: quickInputs.map((x) => x.trim()).filter(Boolean),
        // 与已加载的执行参数合并，避免覆盖其他扩展配置
        executionSettings: { ...mergedSettings, sandbox, toolApproval, subAgent },
      })
      feedback.success(t(detail?.publishStatus === 1 ? 'appManage.configSaveDraftSuccess' : 'appManage.configSaveSuccess'))
      setLocalConfigStatus(0)
      onConfigStatusChange?.(0)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSavingConfig(false)
    }
  }

  /** 发布：把当前草稿配置推入发布快照，线上对话立即生效（发布接口本身即重发语义） */
  const handlePublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      await publishApp(appId)
      feedback.success(t('appManage.publishSuccess'))
      setLocalConfigStatus(1)
      onConfigStatusChange?.(1)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  /** 插件选项统一渲染：头像（存储路径解析，无头像回退标题首字母）+ 标题；title 字符串供搜索过滤 */
  const renderPluginOption = (item: TeamPluginItemType): { value: string; label: ReactNode; title: string } => {
    const name = item.title || item.pluginName || '-'
    return {
      value: String(item.pluginId),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarPath ?? null) || undefined} alt={name}>
            {name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{name}</span>
        </span>
      ),
    }
  }

  // 插件下拉按来源分组：团队自有插件在前、系统插件（公开或已授权）在后（与模型下拉的团队/系统分组同款式），空组不展示
  const pluginSelectOptions = [
    {
      label: t('appManage.pluginGroupTeam'),
      title: t('appManage.pluginGroupTeam'),
      options: pluginOptions.filter((item) => item.isTeamOwned).map(renderPluginOption),
    },
    {
      label: t('appManage.pluginGroupSystem'),
      title: t('appManage.pluginGroupSystem'),
      options: pluginOptions.filter((item) => !item.isTeamOwned).map(renderPluginOption),
    },
  ].filter((group) => group.options.length > 0)

  /** 技能选项统一渲染：头像（存储路径解析，无头像回退首字母）+ 名称；title 字符串供搜索过滤 */
  const renderSkillOption = (item: SkillOption): { value: string; label: ReactNode; title: string } => {
    const name = item.name || item.key || '-'
    return {
      value: String(item.id),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarPath ?? null) || undefined} alt={name}>
            {name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{name}</span>
        </span>
      ),
    }
  }

  const skillSelectOptions = skillOptions.map(renderSkillOption)

  /** 应用类选项统一渲染：头像（存储路径解析，无头像回退首字母）+ 名称；title 字符串供搜索过滤 */
  const renderAppOption = (item: AppItem): { value: string; label: ReactNode; title: string } => {
    const name = item.name || '-'
    return {
      value: String(item.appId),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarPath ?? null) || undefined} alt={name}>
            {name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{name}</span>
        </span>
      ),
    }
  }

  const workflowAppSelectOptions = workflowAppOptions.map(renderAppOption)

  // 排除自身：绑定自己会形成自身调用环（后端保存强校验，前端选项层先隐藏）
  const agentAppSelectOptions = agentAppOptions
    .filter((item) => String(item.appId) !== appId)
    .map(renderAppOption)

  /** 知识库选项统一渲染：头像（存储路径解析，无头像回退首字母）+ 名称；title 字符串供搜索过滤 */
  const renderWikiOption = (item: WikiItem): { value: number; label: ReactNode; title: string } => {
    const name = item.name || '-'
    return {
      value: Number(item.wikiId),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarPath ?? null) || undefined} alt={name}>
            {name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{name}</span>
        </span>
      ),
    }
  }

  const wikiSelectOptions = wikiOptions.filter((item) => item.wikiId != null).map(renderWikiOption)

  /** 知识图谱选项统一渲染：头像（存储路径解析，无头像回退首字母）+ 名称；title 字符串供搜索过滤 */
  const renderGraphOption = (item: KnowledgeGraphItem): { value: number; label: ReactNode; title: string } => {
    const name = item.name || '-'
    return {
      value: Number(item.kgId),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarPath ?? null) || undefined} alt={name}>
            {name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{name}</span>
        </span>
      ),
    }
  }

  const graphSelectOptions = graphOptions.map(renderGraphOption)

  /** 「进入对话」入口（原侧边栏移入调试面板标题右侧）：仅已发布应用展示 */
  const enterChatExtra =
    detail?.publishStatus === 1 ? (
      <Button size="small" type="text" icon={<MessageOutlined />} onClick={() => navigate(`/team/${teamId}/app/${appId}/chat`)}>
        {t('appManage.enterChat')}
      </Button>
    ) : null

  // 对话类模型：对话模型与 LLM 摘要压缩模型下拉只列 conversation 类型（embedding/图像等模型不适用对话场景）
  const conversationModels = availableModels.filter((item) => item.modelKind === 'conversation')

  /** 模型下拉分组选项：按来源分「团队渠道模型/系统渠道模型」两组（与聚合接口团队自有优先排序一致）；
   * 下拉行内附渠道名次要文本（不同渠道可能存在同名模型，仅凭名称无法区分），关闭态/多选标签仍显示纯模型名（optionLabelProp="name"），title 附渠道名供搜索 */
  const conversationModelOptions = [
    {
      label: t('appManage.modelGroupTeam'),
      title: t('appManage.modelGroupTeam'),
      options: conversationModels
        .filter((item) => item.isTeamChannel)
        .map((item) => {
          const name = item.name || item.modelId || '-'
          const channel = item.channelName || '-'
          return {
            value: String(item.aiModelId),
            name,
            label: (
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, minWidth: 0 }}>
                <span style={{ flexShrink: 0 }}>{name}</span>
                <span style={{ fontSize: 12, color: token.colorTextTertiary, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {channel}
                </span>
              </div>
            ),
            title: `${name}（${channel}）`,
          }
        }),
    },
    {
      label: t('appManage.modelGroupSystem'),
      title: t('appManage.modelGroupSystem'),
      options: conversationModels
        .filter((item) => !item.isTeamChannel)
        .map((item) => {
          const name = item.name || item.modelId || '-'
          const channel = item.channelName || '-'
          return {
            value: String(item.aiModelId),
            name,
            label: (
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, minWidth: 0 }}>
                <span style={{ flexShrink: 0 }}>{name}</span>
                <span style={{ fontSize: 12, color: token.colorTextTertiary, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {channel}
                </span>
              </div>
            ),
            title: `${name}（${channel}）`,
          }
        }),
    },
  ].filter((group) => group.options.length > 0)

  // 当前选中的对话模型（用于右侧参数 Tag 与压缩预算换算展示）
  const selectedModel = availableModels.find((item) => String(item.aiModelId) === modelId)

  /** 可选模型变更：去重并保持默认模型落在列表内（不在时自动落到第一个，保存强校验同语义） */
  const handleModelIdsChange = (ids: string[]) => {
    const deduped = Array.from(new Set(ids))
    setModelIds(deduped)
    if (deduped.length > 0 && (!modelId || !deduped.includes(modelId))) {
      setModelId(deduped[0])
    }
    if (deduped.length === 0) {
      setModelId(undefined)
    }
  }

  /** 默认模型下拉：仅在已选模型范围内选择（保持团队/系统分组） */
  const defaultModelOptions = conversationModelOptions
    .map((group) => ({ ...group, options: group.options.filter((option) => modelIds.includes(option.value)) }))
    .filter((group) => group.options.length > 0)

  /** 团队智能体多选变更：去重；默认智能体不在列表内时自动落到第一个（保存强校验同语义） */
  const handleTeamAgentIdsChange = (ids: string[]) => {
    const deduped = Array.from(new Set(ids))
    setTeamAgentIds(deduped)
    setDefaultTeamAgentId((prev) => (prev && deduped.includes(prev) ? prev : deduped[0]))
  }

  const renderTeamAgentOption = (item: TeamAgentListItem): { value: string; label: ReactNode; title: string } => {
    const disabled = item.enabled === false
    const name = `${item.name || '-'}${disabled ? `（${t('appManage.teamAgentDisabledSuffix')}）` : ''}`
    return {
      value: String(item.id),
      title: name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarObjectKey ?? null) || undefined} alt={name}>
            {(item.name || '-').slice(0, 1).toUpperCase()}
          </Avatar>
          <span style={disabled ? { opacity: 0.55 } : undefined}>{name}</span>
          {item.isSystem && !disabled && <Tag style={{ marginInlineEnd: 0 }} color="purple">{t('teamAgents.builtin')}</Tag>}
        </span>
      ),
    }
  }

  // 绑定候选 = 启用中智能体；已绑定后被停用的项也要出现（带「已停用」标记，避免回显成裸 id，提示移除）
  const teamAgentSelectOptions = [
    ...teamAgentOptions,
    ...(teamAgentEchoOptions ?? []).filter(
      (item) => item && item.enabled === false && item.id && teamAgentIds.includes(String(item.id)),
    ).map((item) => ({
      id: item.id,
      key: item.key,
      name: item.name,
      description: item.description,
      avatarObjectKey: item.avatarObjectKey,
      enabled: false,
      isSystem: false,
    } as TeamAgentListItem)),
  ].map(renderTeamAgentOption)
  /** 默认团队智能体下拉：仅在已选智能体范围内选择（多选 ≥2 时展示，单选默认即唯一项无需配置） */
  const defaultTeamAgentSelectOptions = teamAgentSelectOptions.filter((option) => teamAgentIds.includes(option.value))

  // 弱依赖提示（team-agent sdd D3）：已选智能体声明的主依赖未绑定到应用时提示一键补齐（不阻断绑定）
  const missingDeps = useMemo(() => {
    const agents = (teamAgentEchoOptions ?? []).filter((a) => teamAgentIds.includes(String(a?.id ?? '')))
    const missingPlugins = agents.flatMap((a) => (a.mainPluginIds ?? []).map(String).filter((id) => !pluginIds.includes(id)))
    const missingWikis = agents.flatMap((a) => (a.mainWikiIds ?? []).map(Number).filter((id) => !wikiIds.includes(id)))
    const missingGraphs = agents.flatMap((a) => (a.mainGraphIds ?? []).map(Number).filter((id) => !graphIds.includes(id)))
    const agentNames = agents
      .filter((a) => (a.mainPluginIds ?? []).map(String).some((id) => !pluginIds.includes(id))
        || (a.mainWikiIds ?? []).map(Number).some((id) => !wikiIds.includes(id))
        || (a.mainGraphIds ?? []).map(Number).some((id) => !graphIds.includes(id)))
      .map((a) => a.name || a.key || '')
      .filter(Boolean)
    return { missingPlugins, missingWikis, missingGraphs, agentNames: Array.from(new Set(agentNames)) }
  }, [teamAgentIds, teamAgentEchoOptions, pluginIds, wikiIds, graphIds])

  /** token 数格式化：128000 → 128K、1050000 → 1.1M */
  const formatModelTokens = (tokens: number) => {
    if (tokens >= 1_000_000) {
      const m = tokens / 1_000_000
      return `${Number.isInteger(m) ? m : m.toFixed(1)}M`
    }
    return `${Math.round(tokens / 1000)}K`
  }

  /** 选中模型的参数 Tag 文案：上下文/最大输出按 K/M 缩写，能力按布尔展示；元数据缺失的项不展示 */
  const modelParamTags = (item: TeamAvailableModelItem): string[] => {
    const tags: string[] = []
    if ((item.contextWindow ?? 0) > 0) tags.push(t('appManage.modelParamContext', { size: formatModelTokens(item.contextWindow ?? 0) }))
    if ((item.maxOutput ?? 0) > 0) tags.push(t('appManage.modelParamOutput', { size: formatModelTokens(item.maxOutput ?? 0) }))
    if (item.supportsVision) tags.push(t('appManage.modelParamVision'))
    if (item.supportsToolCall) tags.push(t('appManage.modelParamTool'))
    if (item.supportsReasoning) tags.push(t('appManage.modelParamReasoning'))
    return tags
  }

  /** 自动模式压缩预算：ContextWindow − MaxOutput（与后端运行时同口径）；窗口元数据缺失返回 null（走固定值兜底） */
  const compactionBudget = (() => {
    const window = selectedModel?.contextWindow ?? 0
    if (!window || window <= 0) return null
    const budget = window - Math.max(selectedModel?.maxOutput ?? 0, 0)
    return budget > 0 ? budget : window
  })()

  // 自动放通列表行：按当前选中 id 从选项派生（带头像，与下拉选项同一取名列与头像）
  const appAutoPassItems = (ids: string[], options: AppItem[]): AutoPassItem[] =>
    ids
      .map((id) => options.find((item) => String(item.appId) === id))
      .filter((item): item is AppItem => Boolean(item))
      .map((item) => ({ id: String(item.appId), name: item.name || '-', avatarPath: item.avatarPath ?? null }))

  const pluginAutoPassItems: AutoPassItem[] = pluginIds
    .map((id) => pluginOptions.find((item) => String(item.pluginId) === id))
    .filter((item): item is TeamPluginItemType => Boolean(item))
    .map((item) => ({ id: String(item.pluginId), name: item.title || item.pluginName || '-', avatarPath: item.avatarPath ?? null }))
  const workflowAutoPassItems = appAutoPassItems(workflowAppIds, workflowAppOptions)
  const agentAutoPassItems = appAutoPassItems(agentAppIds, agentAppOptions)
  const skillAutoPassItems: AutoPassItem[] = skillIds
    .map((id) => skillOptions.find((item) => String(item.id) === id))
    .filter((item): item is SkillOption => Boolean(item))
    .map((item) => ({ id: String(item.id), name: item.name || item.key || '-', avatarPath: item.avatarPath ?? null }))
  const wikiAutoPassItems: AutoPassItem[] = wikiIds
    .map((id) => wikiOptions.find((item) => Number(item.wikiId) === id))
    .filter((item): item is WikiItem => Boolean(item))
    .map((item) => ({ id: Number(item.wikiId), name: item.name || '-', avatarPath: item.avatarPath ?? null }))
  const graphAutoPassItems: AutoPassItem[] = graphIds
    .map((id) => graphOptions.find((item) => Number(item.kgId) === id))
    .filter((item): item is KnowledgeGraphItem => Boolean(item))
    .map((item) => ({ id: Number(item.kgId), name: item.name || '-', avatarPath: item.avatarPath ?? null }))

  const toggleAutoPassId = (key: 'plugins' | 'workflowApps' | 'agentApps' | 'skills', id: string, checked: boolean) => {
    setAutoPass((prev) => ({
      ...prev,
      [key]: checked ? [...prev[key], id] : prev[key].filter((x) => x !== id),
    }))
  }
  const toggleAutoPassLong = (key: 'wikis' | 'graphs', id: number, checked: boolean) => {
    setAutoPass((prev) => ({
      ...prev,
      [key]: checked ? [...prev[key], id] : prev[key].filter((x) => x !== id),
    }))
  }
  // 六类绑定资源当前 id 列表（全部放通按它取全量/清空）
  const allBoundIds: Record<'plugins' | 'workflowApps' | 'agentApps' | 'skills' | 'wikis' | 'graphs', Array<string | number>> = {
    plugins: [...pluginIds],
    workflowApps: [...workflowAppIds],
    agentApps: [...agentAppIds],
    skills: [...skillIds],
    wikis: [...wikiIds],
    graphs: [...graphIds],
  }
  /** 「全部放通」总开关：勾选=该类全部绑定资源自动放通，取消=全部清空（恢复人工批准） */
  const toggleAutoPassAll = (key: 'plugins' | 'workflowApps' | 'agentApps' | 'skills' | 'wikis' | 'graphs', checked: boolean) => {
    setAutoPass((prev) => ({ ...prev, [key]: checked ? [...allBoundIds[key]] : [] }) as AutoPassState)
  }

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      {/* 配置拆为收缩卡片便于定位；左栏内部滚动（不把调试对话栏顶出可视区），并让出更多宽度给右栏 */}
      <Col
        xs={24}
        lg={11}
        xxl={11}
        style={{ height: 'calc(100vh - 48px)', minHeight: 480, overflowY: 'auto', paddingRight: spacing.xs }}
      >
        {!canManage && (
          <Alert
            type="info"
            showIcon
            message={t('appManage.memberHint')}
            style={{ marginBottom: spacing.md }}
          />
        )}
        {canManage && detail?.publishStatus === 1 && effectiveConfigStatus === 0 && (
          <Alert
            type="warning"
            showIcon
            message={t('appManage.draftPendingHint')}
            action={
              <Button size="small" type="primary" loading={publishing} onClick={() => void handlePublish()}>
                {t('appManage.publish')}
              </Button>
            }
            style={{ marginBottom: spacing.md }}
          />
        )}
        {!isAgent ? (
          <DSCard title={t('appManage.agentConfigTitle')}>
            <Alert
              type="info"
              showIcon
              message={t('appManage.workflowConfigUnavailable')}
              action={
                canManage ? (
                  <Button size="small" onClick={() => navigate(`/team/${teamId}/app/${appId}/design`)}>
                    {t('appManage.workflowGoDesign')}
                  </Button>
                ) : undefined
              }
            />
          </DSCard>
        ) : (
          <>
            {isExternal && (
              <Alert
                type="info"
                showIcon
                message={t('appManage.externalRestrictionHint')}
                style={{ marginBottom: spacing.md }}
              />
            )}
            <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
              {/* 一、基础配置：对话模型 + 上下文压缩 + 子代理协作 */}
              <Collapse
                defaultActiveKey="basic"
                items={[
                  {
                    key: 'basic',
                    label: groupHeader(t('appManage.groupBasic'), t('appManage.groupBasicDesc')),
                    children: (
                      <Form layout="vertical" disabled={!canManage}>
                        <Form.Item label={requiredLabel(t('appManage.selectableModels'))} extra={t('appManage.selectableModelsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%', maxWidth: 640 }}
                            maxTagCount="responsive"
                            placeholder={t('appManage.modelPlaceholder')}
                            loading={optionsLoading}
                            value={modelIds}
                            onChange={handleModelIdsChange}
                            options={conversationModelOptions}
                            optionLabelProp="name"
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.modelEmpty')}
                          />
                        </Form.Item>
                        <Form.Item label={requiredLabel(t('appManage.defaultModel'))}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md, flexWrap: 'wrap' }}>
                            <Select
                              showSearch
                              optionFilterProp="title"
                              style={{ width: 320, flexShrink: 0 }}
                              placeholder={t('appManage.defaultModelPlaceholder')}
                              loading={optionsLoading}
                              value={modelId}
                              onChange={(value) => setModelId(value)}
                              options={defaultModelOptions}
                              optionLabelProp="name"
                              notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.defaultModelPlaceholder')}
                            />
                            {selectedModel && modelParamTags(selectedModel).length > 0 && (
                              <Space size={[4, 4]} wrap>
                                {modelParamTags(selectedModel).map((label) => (
                                  <Tag key={label} style={{ marginInlineEnd: 0 }}>
                                    {label}
                                  </Tag>
                                ))}
                              </Space>
                            )}
                          </div>
                        </Form.Item>
                        <Divider style={{ margin: `${spacing.md}px 0` }} />
                        <Form.Item label={t('appManage.compactionSection')} extra={t('appManage.compactionHint')} style={{ marginBottom: spacing.sm }}>
                          <Form.Item label={t('appManage.compactionMode')} style={{ marginBottom: spacing.sm }}>
                            <Select
                              style={{ width: '100%', maxWidth: 360 }}
                              value={compactionMode}
                              onChange={(value) => setCompactionMode(value === 'custom' ? 'custom' : 'auto')}
                              options={[
                                { value: 'auto', label: t('appManage.compactionModeAuto') },
                                { value: 'custom', label: t('appManage.compactionModeCustom') },
                              ]}
                            />
                          </Form.Item>
                          {compactionMode === 'auto' ? (
                            <Alert
                              type="info"
                              showIcon
                              message={
                                compactionBudget
                                  ? t('appManage.compactionAutoComputed', {
                                      budget: formatModelTokens(compactionBudget),
                                      tool: formatModelTokens(Math.floor(compactionBudget * (compactionDefaults?.toolEvictionRatio ?? 0.5))),
                                      trigger: formatModelTokens(Math.floor(compactionBudget * (compactionDefaults?.summarizeTriggerRatio ?? 0.8))),
                                      target: formatModelTokens(Math.floor(compactionBudget * (compactionDefaults?.summarizeTargetRatio ?? 0.5))),
                                      hard: formatModelTokens(Math.floor(compactionBudget * (compactionDefaults?.truncationRatio ?? 0.95))),
                                    })
                                  : t('appManage.compactionAutoFallback')
                              }
                              description={t('appManage.compactionAutoHint')}
                              style={{ marginBottom: spacing.sm }}
                            />
                          ) : (
                            <Row gutter={spacing.md} style={{ marginBottom: spacing.sm }}>
                              <Col xs={24} md={12}>
                                <Form.Item label={t('appManage.compactionToolResult')} style={{ marginBottom: spacing.sm }}>
                                  <InputNumber
                                    min={1}
                                    max={500}
                                    precision={0}
                                    style={{ width: '100%' }}
                                    value={compactionToolResult ?? undefined}
                                    onChange={(value) => setCompactionToolResult(typeof value === 'number' ? value : null)}
                                  />
                                </Form.Item>
                              </Col>
                              <Col xs={24} md={12}>
                                <Form.Item label={t('appManage.compactionSummarizeTokens')} style={{ marginBottom: spacing.sm }}>
                                  <InputNumber
                                    min={200}
                                    max={2000000}
                                    precision={0}
                                    style={{ width: '100%' }}
                                    value={compactionSummarizeTokens ?? undefined}
                                    onChange={(value) => setCompactionSummarizeTokens(typeof value === 'number' ? value : null)}
                                  />
                                </Form.Item>
                              </Col>
                              <Col xs={24} md={12}>
                                <Form.Item label={t('appManage.compactionHardLimit')} style={{ marginBottom: spacing.sm }}>
                                  <InputNumber
                                    min={200}
                                    max={4000000}
                                    precision={0}
                                    style={{ width: '100%' }}
                                    value={compactionHardLimit ?? undefined}
                                    onChange={(value) => setCompactionHardLimit(typeof value === 'number' ? value : null)}
                                  />
                                </Form.Item>
                              </Col>
                            </Row>
                          )}
                        </Form.Item>
                        <Form.Item
                          label={t('appManage.compactionSummarization')}
                          valuePropName="checked"
                          extra={t('appManage.compactionSummarizationHint')}
                        >
                          <Switch checked={compactionSummarization} onChange={setCompactionSummarization} />
                        </Form.Item>
                        {compactionSummarization && (
                          // 摘要模型默认自动绑定上方对话模型（显示即跟随）；可手动指定，清空后恢复跟随。
                          // 存储语义不变：空串=跟随对话模型，运行时回退到 config.ModelId（AppCompactionStrategyFactory）
                          <Form.Item label={t('appManage.compactionSummarizerModel')}>
                            <Select
                              allowClear
                              showSearch
                              optionFilterProp="title"
                              style={{ width: '100%', maxWidth: 360 }}
                              placeholder={t('appManage.compactionSummarizerModelPlaceholder')}
                              value={compactionSummarizerModelId || modelId}
                              onChange={(value) => setCompactionSummarizerModelId(value)}
                              options={conversationModelOptions}
                              optionLabelProp="name"
                            />
                          </Form.Item>
                        )}
                        <Divider style={{ margin: `${spacing.md}px 0` }} />
                        <Form.Item
                          label={t('appManage.subAgentSection')}
                          valuePropName="checked"
                          extra={t('appManage.subAgentHint')}
                          style={{ marginBottom: spacing.sm }}
                        >
                          <Switch
                            checked={subAgentEnabled}
                            onChange={(checked) => {
                              setSubAgentEnabled(checked)
                              // 关闭协作时同步清空团队智能体绑定（绑定依赖协作派发链路，后端强校验同语义）
                              if (!checked) {
                                setTeamAgentIds([])
                                setDefaultTeamAgentId(undefined)
                              }
                            }}
                            disabled={isExternal}
                          />
                        </Form.Item>
                        {subAgentEnabled && !isExternal && (
                          <>
                            <Row gutter={spacing.md}>
                              <Col xs={24} md={12}>
                                <Form.Item label={t('appManage.subAgentTimeout')} style={{ marginBottom: spacing.sm }}>
                                  <InputNumber
                                    min={30}
                                    max={1800}
                                    precision={0}
                                    style={{ width: '100%' }}
                                    value={subAgentTimeout ?? undefined}
                                    onChange={(value) => setSubAgentTimeout(typeof value === 'number' ? value : null)}
                                    placeholder="180"
                                    addonAfter={t('appManage.sandboxSeconds')}
                                  />
                                </Form.Item>
                              </Col>
                              <Col xs={24} md={12}>
                                <Form.Item label={t('appManage.subAgentResultMaxChars')} style={{ marginBottom: spacing.sm }}>
                                  <InputNumber
                                    min={500}
                                    max={100000}
                                    precision={0}
                                    style={{ width: '100%' }}
                                    value={subAgentResultMaxChars ?? undefined}
                                    onChange={(value) => setSubAgentResultMaxChars(typeof value === 'number' ? value : null)}
                                    placeholder="8000"
                                  />
                                </Form.Item>
                              </Col>
                            </Row>
                            <Form.Item
                              label={t('appManage.teamAgentSection')}
                              extra={t('appManage.teamAgentHint')}
                              style={{ marginBottom: spacing.sm }}
                            >
                              <Select
                                mode="multiple"
                                allowClear
                                showSearch
                                optionFilterProp="title"
                                maxTagCount="responsive"
                                placeholder={t('appManage.teamAgentPlaceholder')}
                                value={teamAgentIds}
                                onChange={handleTeamAgentIdsChange}
                                options={teamAgentSelectOptions}
                                optionLabelProp="title"
                                disabled={optionsLoading}
                              />
                            </Form.Item>
                            {missingDeps.agentNames.length > 0 && (
                              <Alert
                                type="warning"
                                showIcon
                                style={{ marginBottom: spacing.sm }}
                                message={t('appManage.teamAgentMissingDeps', { names: missingDeps.agentNames.join('、') })}
                                action={
                                  <Button size="small" danger type="text" onClick={() => {
                                    setPluginIds((prev) => Array.from(new Set([...prev, ...missingDeps.missingPlugins])))
                                    setWikiIds((prev) => Array.from(new Set([...prev, ...missingDeps.missingWikis])))
                                    setGraphIds((prev) => Array.from(new Set([...prev, ...missingDeps.missingGraphs])))
                                  }}>
                                    {t('appManage.teamAgentAddDeps')}
                                  </Button>
                                }
                              />
                            )}
                            {teamAgentIds.length > 1 && (
                              <Form.Item label={t('appManage.teamAgentDefault')} style={{ marginBottom: spacing.sm }}>
                                <Select
                                  allowClear
                                  showSearch
                                  optionFilterProp="title"
                                  placeholder={t('appManage.teamAgentDefaultPlaceholder')}
                                  value={defaultTeamAgentId ?? undefined}
                                  onChange={(value) => setDefaultTeamAgentId(typeof value === 'string' ? value : undefined)}
                                  options={defaultTeamAgentSelectOptions}
                                  optionLabelProp="title"
                                  disabled={optionsLoading}
                                />
                              </Form.Item>
                            )}
                          </>
                        )}
                      </Form>
                    ),
                  },
                ]}
              />
              {/* 二、提示词：系统提示词 + 对话开场白 + 快捷输入 */}
              <Collapse
                defaultActiveKey="prompts"
                items={[
                  {
                    key: 'prompts',
                    label: groupHeader(t('appManage.groupPrompts'), t('appManage.groupPromptsDesc')),
                    children: (
                      <Form layout="vertical" disabled={!canManage}>
                        <Form.Item label={requiredLabel(t('appManage.sectionPrompt'))} extra={t('appManage.promptHint')}>
                          <Input.TextArea
                            value={prompt}
                            onChange={(e) => setPrompt(e.target.value)}
                            placeholder={t('appManage.promptPlaceholder')}
                            maxLength={4000}
                            showCount
                            rows={8}
                          />
                        </Form.Item>
                        <Form.Item label={t('appManage.openingStatement')} valuePropName="checked" extra={t('appManage.openingStatementHint')}>
                          <Switch checked={openingEnabled} onChange={setOpeningEnabled} />
                        </Form.Item>
                        {openingEnabled && (
                          <Form.Item label={t('appManage.openingStatementContent')}>
                            <Input.TextArea
                              value={openingStatement}
                              onChange={(e) => setOpeningStatement(e.target.value)}
                              placeholder={t('appManage.openingStatementPlaceholder')}
                              maxLength={4000}
                              showCount
                              autoSize={{ minRows: 3, maxRows: 8 }}
                            />
                          </Form.Item>
                        )}
                        <Form.Item label={t('appManage.quickInputs')} extra={t('appManage.quickInputsHint')}>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xs, maxWidth: 480 }}>
                            {quickInputs.map((text, index) => (
                              <div key={index} style={{ display: 'flex', gap: spacing.xs, alignItems: 'center' }}>
                                <Input
                                  value={text}
                                  maxLength={200}
                                  showCount
                                  placeholder={t('appManage.quickInputsPlaceholder')}
                                  onChange={(e) =>
                                    setQuickInputs((prev) => prev.map((x, i) => (i === index ? e.target.value : x)))
                                  }
                                />
                                <Button
                                  type="text"
                                  danger
                                  icon={<DeleteOutlined />}
                                  aria-label={t('appManage.quickInputsRemove')}
                                  onClick={() => setQuickInputs((prev) => prev.filter((_, i) => i !== index))}
                                />
                              </div>
                            ))}
                            {quickInputs.length < MAX_QUICK_INPUTS && (
                              <Button
                                type="dashed"
                                icon={<PlusOutlined />}
                                style={{ maxWidth: 240 }}
                                onClick={() => setQuickInputs((prev) => [...prev, ''])}
                              >
                                {t('appManage.quickInputsAdd')}
                              </Button>
                            )}
                          </div>
                        </Form.Item>
                      </Form>
                    ),
                  },
                ]}
              />
              {/* 三、能力扩展：插件 + 流程应用 + Agent 应用 + 技能 + 知识库 + 知识图谱（选中后逐项配置审批模式自动放通） */}
              <Collapse
                defaultActiveKey="capabilities"
                items={[
                  {
                    key: 'capabilities',
                    label: groupHeader(t('appManage.groupCapabilities'), t('appManage.groupCapabilitiesDesc')),
                    children: (
                      // 顶部留白：避免卡片标题与首个组件粘连；各资源区间距由 AutoPassList 的下边距撑开
                      <div style={{ paddingTop: spacing.xs }}>
                        <div style={{ fontSize: 12, color: token.colorTextTertiary, marginBottom: spacing.md }}>
                          {t('appManage.capabilitiesAutoPassHint')}
                        </div>
                        <Form layout="vertical" disabled={!canManage}>
                        <Form.Item label={t('appManage.sectionPlugins')} extra={t('appManage.pluginsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.pluginsPlaceholder')}
                            loading={optionsLoading}
                            value={pluginIds}
                            onChange={setPluginIds}
                            options={pluginSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.pluginsEmpty')}
                          />
                        </Form.Item>
                        <AutoPassList
                          items={pluginAutoPassItems}
                          checkedIds={autoPass.plugins}
                          onToggle={(id, checked) => toggleAutoPassId('plugins', String(id), checked)}
                          onToggleAll={(checked) => toggleAutoPassAll('plugins', checked)}
                        />
                        <Form.Item label={t('appManage.sectionWorkflowApps')} extra={t('appManage.workflowAppsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.workflowAppsPlaceholder')}
                            loading={optionsLoading}
                            value={workflowAppIds}
                            onChange={setWorkflowAppIds}
                            options={workflowAppSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.workflowAppsEmpty')}
                          />
                        </Form.Item>
                        <AutoPassList
                          items={workflowAutoPassItems}
                          checkedIds={autoPass.workflowApps}
                          onToggle={(id, checked) => toggleAutoPassId('workflowApps', String(id), checked)}
                          onToggleAll={(checked) => toggleAutoPassAll('workflowApps', checked)}
                        />
                        <Form.Item label={t('appManage.sectionAgentApps')} extra={t('appManage.agentAppsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.agentAppsPlaceholder')}
                            loading={optionsLoading}
                            value={agentAppIds}
                            onChange={setAgentAppIds}
                            options={agentAppSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.agentAppsEmpty')}
                          />
                        </Form.Item>
                        <AutoPassList
                          items={agentAutoPassItems}
                          checkedIds={autoPass.agentApps}
                          onToggle={(id, checked) => toggleAutoPassId('agentApps', String(id), checked)}
                          onToggleAll={(checked) => toggleAutoPassAll('agentApps', checked)}
                        />
                        {!isExternal && (
                          <>
                          <Form.Item label={t('appManage.sectionSkills')} extra={t('appManage.skillsHint')}>
                            <Select
                              mode="multiple"
                              allowClear
                              showSearch
                              optionFilterProp="title"
                                style={{ width: '100%' }}
                                placeholder={t('appManage.skillsPlaceholder')}
                                loading={optionsLoading}
                                value={skillIds}
                                onChange={setSkillIds}
                                options={skillSelectOptions}
                                notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.skillsEmpty')}
                              />
                            </Form.Item>
                            <AutoPassList
                              items={skillAutoPassItems}
                              checkedIds={autoPass.skills}
                              onToggle={(id, checked) => toggleAutoPassId('skills', String(id), checked)}
                              onToggleAll={(checked) => toggleAutoPassAll('skills', checked)}
                            />
                          </>
                        )}
                        <Form.Item label={t('appManage.sectionKnowledge')} extra={t('appManage.knowledgeHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.knowledgePlaceholder')}
                            loading={optionsLoading}
                            value={wikiIds}
                            onChange={setWikiIds}
                            options={wikiSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.knowledgeEmpty')}
                          />
                        </Form.Item>
                        <AutoPassList
                          items={wikiAutoPassItems}
                          checkedIds={autoPass.wikis}
                          onToggle={(id, checked) => toggleAutoPassLong('wikis', Number(id), checked)}
                          onToggleAll={(checked) => toggleAutoPassAll('wikis', checked)}
                        />
                        <Form.Item label={t('appManage.sectionGraphs')} extra={t('appManage.graphsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="title"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.graphsPlaceholder')}
                            loading={optionsLoading}
                            value={graphIds}
                            onChange={setGraphIds}
                            options={graphSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.graphsEmpty')}
                          />
                        </Form.Item>
                        <AutoPassList
                          items={graphAutoPassItems}
                          checkedIds={autoPass.graphs}
                          onToggle={(id, checked) => toggleAutoPassLong('graphs', Number(id), checked)}
                          onToggleAll={(checked) => toggleAutoPassAll('graphs', checked)}
                        />
                        </Form>
                      </div>
                    ),
                  },
                ]}
              />
              {/* 四、沙箱：执行参数 + 审批模式沙箱自动放行（外部应用不支持，整体不渲染） */}
              {!isExternal && (
                <Collapse
                  defaultActiveKey="sandbox"
                  items={[
                    {
                      key: 'sandbox',
                      label: groupHeader(t('appManage.groupSandbox'), t('appManage.groupSandboxDesc')),
                      children: (
                        <Form layout="vertical" disabled={!canManage}>
                          <Form.Item label={t('appManage.sandboxEnabled')} valuePropName="checked" extra={t('appManage.sandboxHint')}>
                            <Switch checked={sandboxEnabled} onChange={setSandboxEnabled} />
                          </Form.Item>
                          {sandboxEnabled && (
                            <>
                              <Row gutter={spacing.md}>
                                <Col xs={24} md={12}>
                                  <Form.Item
                                    label={t('appManage.sandboxTimeout')}
                                    extra={`${t('appManage.sandboxTimeoutHint')}${
                                      sandboxLimits ? t('appManage.sandboxLimitSuffix', { max: sandboxLimits.maxTtlSeconds }) : ''
                                    }`}
                                  >
                                    <InputNumber
                                      min={60}
                                      max={sandboxLimits?.maxTtlSeconds ?? 86400}
                                      style={{ width: '100%' }}
                                      value={sandboxTimeout ?? undefined}
                                      onChange={(value) => setSandboxTimeout(typeof value === 'number' ? value : null)}
                                      placeholder="900"
                                      addonAfter={t('appManage.sandboxSeconds')}
                                    />
                                  </Form.Item>
                                </Col>
                                <Col xs={24} md={12}>
                                  <Form.Item label={t('appManage.sandboxRenew')} valuePropName="checked" extra={t('appManage.sandboxRenewHint')}>
                                    <Switch checked={sandboxRenew} onChange={setSandboxRenew} />
                                  </Form.Item>
                                </Col>
                              </Row>
                              <Row gutter={spacing.md}>
                                <Col xs={24} md={12}>
                                  <Form.Item
                                    label={t('appManage.sandboxCpu')}
                                    extra={`${t('appManage.sandboxCpuHint')}${
                                      sandboxLimits ? t('appManage.sandboxLimitSuffix', { max: sandboxLimits.maxCpu }) : ''
                                    }`}
                                  >
                                    <Input
                                      value={sandboxCpu}
                                      onChange={(e) => setSandboxCpu(e.target.value)}
                                      placeholder="1"
                                    />
                                  </Form.Item>
                                </Col>
                                <Col xs={24} md={12}>
                                  <Form.Item
                                    label={t('appManage.sandboxMemory')}
                                    extra={`${t('appManage.sandboxMemoryHint')}${
                                      sandboxLimits ? t('appManage.sandboxLimitSuffix', { max: sandboxLimits.maxMemory }) : ''
                                    }`}
                                  >
                                    <Input
                                      value={sandboxMemory}
                                      onChange={(e) => setSandboxMemory(e.target.value)}
                                      placeholder="2Gi"
                                    />
                                  </Form.Item>
                                </Col>
                              </Row>
                              <Form.Item
                                label={t('appManage.sandboxNetwork')}
                                extra={sandboxNetworkGoverned ? t('appManage.sandboxNetworkGovernedHint') : t('appManage.sandboxNetworkHint')}
                              >
                                <Select
                                  allowClear
                                  style={{ width: '100%', maxWidth: 360 }}
                                  placeholder={t('appManage.sandboxNetworkPlaceholder')}
                                  value={sandboxNetworkAction}
                                  onChange={setSandboxNetworkAction}
                                  options={[
                                    { value: 'allow', label: t('appManage.sandboxNetworkAllow'), disabled: sandboxNetworkGoverned },
                                    { value: 'deny', label: t('appManage.sandboxNetworkDeny') },
                                  ]}
                                />
                              </Form.Item>
                              {(sandboxNetworkAction || sandboxNetworkRules.length > 0) && (
                                <Form.Item label={t('appManage.sandboxNetworkRules')} extra={t('appManage.sandboxNetworkRulesHint')}>
                                  <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, alignItems: 'flex-start' }}>
                                    {sandboxNetworkRules.map((rule, index) => (
                                      <Space key={index} style={{ display: 'flex' }} align="center">
                                        <Select
                                          style={{ width: 96 }}
                                          value={rule.action}
                                          onChange={(value) => updateNetworkRule(index, { action: value })}
                                          options={[
                                            { value: 'allow', label: t('appManage.sandboxNetworkRuleAllow') },
                                            { value: 'deny', label: t('appManage.sandboxNetworkRuleDeny') },
                                          ]}
                                        />
                                        <Input
                                          style={{ width: 240 }}
                                          value={rule.target}
                                          onChange={(e) => updateNetworkRule(index, { target: e.target.value })}
                                          placeholder="pypi.org / 10.0.0.0/8"
                                        />
                                        <Input
                                          style={{ width: 150 }}
                                          value={rule.portsText}
                                          onChange={(e) => updateNetworkRule(index, { portsText: e.target.value })}
                                          placeholder="443,80（可选）"
                                        />
                                        <Button
                                          type="text"
                                          size="small"
                                          icon={<DeleteOutlined />}
                                          onClick={() => removeNetworkRule(index)}
                                        />
                                      </Space>
                                    ))}
                                    <Button type="dashed" icon={<PlusOutlined />} onClick={addNetworkRule}>
                                      {t('appManage.sandboxNetworkRuleAdd')}
                                    </Button>
                                  </div>
                                </Form.Item>
                              )}
                              <Form.Item label={t('appManage.sandboxCommandFilter')} valuePropName="checked" extra={t('appManage.sandboxCommandFilterHint')} style={{ marginTop: spacing.md }}>
                                <Switch checked={sandboxCommandFilterEnabled} onChange={setSandboxCommandFilterEnabled} />
                              </Form.Item>
                              {sandboxCommandFilterEnabled && (
                                <Form.Item label={t('appManage.sandboxCommandDeny')} extra={t('appManage.sandboxCommandDenyHint')}>
                                  <Input.TextArea
                                    aria-label={t('appManage.sandboxCommandDeny')}
                                    value={sandboxCommandDeny}
                                    onChange={(e) => setSandboxCommandDeny(e.target.value)}
                                    placeholder={'shutdown\nmkfs\nregex:\\b(wget|curl)\\b'}
                                    autoSize={{ minRows: 3, maxRows: 8 }}
                                  />
                                </Form.Item>
                              )}
                            </>
                          )}
                          <Divider style={{ margin: `${spacing.md}px 0` }} />
                          <Form.Item
                            label={t('appManage.toolApprovalSandbox')}
                            valuePropName="checked"
                            extra={t('appManage.toolApprovalSandboxHint')}
                            style={{ marginBottom: spacing.sm }}
                          >
                            <Switch checked={sandboxAutoApproved} onChange={setSandboxAutoApproved} />
                          </Form.Item>
                        </Form>
                      ),
                    },
                  ]}
                />
              )}
              {/* 五、内容脱敏（原独立「安全」分区并入，展开才加载设置；随下方「保存设置」一并提交，security 接口即时生效不经发布快照） */}
              {canManage && (
                <Collapse
                  items={[
                    {
                      key: 'security',
                      label: groupHeader(t('appManage.groupSecurity'), t('appManage.groupSecurityDesc')),
                      children: (
                        <AppSecuritySettingsPanel appId={appId} canManage={canManage} registerSave={registerSecuritySave} />
                      ),
                    },
                  ]}
                />
              )}
              {/* 六、长期记忆（原独立「记忆」分区并入，展开才加载设置；memory 节随「保存设置」并入主配置，随发布快照生效；
                  外部应用面向匿名/外部用户不开放记忆） */}
              {canManage && !isExternal && (
                <Collapse
                  items={[
                    {
                      key: 'memory',
                      label: groupHeader(t('appManage.groupMemory'), t('appManage.groupMemoryDesc')),
                      children: (
                        <AppMemorySettingsPanel
                          teamId={teamId}
                          appId={appId}
                          canManage={canManage}
                          registerSettings={registerMemorySettings}
                        />
                      ),
                    },
                  ]}
                />
              )}
            </div>
            {canManage && (
              <Button
                type="primary"
                style={{ marginTop: spacing.md }}
                loading={savingConfig}
                onClick={() => void handleSaveConfig()}
              >
                {t('appManage.saveConfig')}
              </Button>
            )}
          </>
        )}
      </Col>
      {/* 右栏调试对话与左栏同一高度基准（视口 - 全局壳 24px×2 内边距），卡片内部弹性布局：消息区滚动、输入区停靠底部 */}
      <Col xs={24} lg={13} xxl={13} style={{ height: 'calc(100vh - 48px)', minHeight: 480 }}>
        {isAgent && canManage ? (
          <DSCard
            style={{ height: '100%', display: 'flex', flexDirection: 'column' }}
            styles={{ body: { padding: 20, flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' } }}
          >
            <AppDebugChat
              appId={appId}
              appName={detail?.name ?? undefined}
              appAvatar={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}
              openingStatement={openingEnabled ? openingStatement.trim() : ''}
              quickInputs={quickInputs}
              headerExtra={enterChatExtra}
            />
          </DSCard>
        ) : (
          <DSCard title={t('appDebug.title')} extra={enterChatExtra}>
            <Alert type="info" showIcon message={t('appDebug.adminOnly')} />
          </DSCard>
        )}
      </Col>
    </Row>
  )
}
