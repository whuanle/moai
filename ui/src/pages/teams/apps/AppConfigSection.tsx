import { useCallback, useEffect, useRef, useState } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Col, Collapse, Divider, Form, Input, InputNumber, Row, Select, Spin, Switch, theme } from 'antd'
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
  type AppKind,
  type AppItem,
  type SandboxLimits,
} from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getSkillOptions, type SkillOption } from '@/api/skills'
import { getWikis, type WikiItem } from '@/api/wiki'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'
import { resolveStorageUrl } from '@/utils/storage'
import { parseCpuMillicores, parseMemoryBytes } from '@/utils/sandboxQuantity'
import { AppDebugChat } from './chat/AppDebugChat'
import { AppSecuritySettingsPanel } from './AppSecuritySection'
import { AppMemorySettingsPanel } from './AppMemoryPanel'

/** 后端 memory 静态插件无 DB 记录，pluginId 为空 Guid，不能作为绑定目标 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

/** 快捷输入上限（与后端 SaveAppAgentConfigCommand 校验一致），每条最长 200 字符由 Input maxLength 限制 */
const MAX_QUICK_INPUTS = 10

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
  myRole?: number | null
}

export interface AppConfigSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  loading: boolean
  canManage: boolean
  /** 配置状态（工作台持有的真值，头部「重新发布」与本地警告条共用）：0=草稿有未发布变更 1=一致；null/缺省回退本地加载值 */
  configStatus?: number | null
  /** 配置状态变化上报（加载/保存/重新发布时） */
  onConfigStatusChange?: (status: number) => void
}

/**
 * 应用「配置」分区：左栏把 Agent 配置拆为收缩卡片（基础配置/用户提示/能力扩展/沙箱/安全审批，
 * 默认全部展开；「内容脱敏」由原独立「安全」分区并入、默认收起；「长期记忆」由原独立「记忆」分区并入、默认收起，
 * 两者均仅管理员可见，长期记忆仅内部应用），右栏调试对话面板。「保存设置」一次提交全部卡片：
 * 主配置走 agent-config（草稿/发布快照，长期记忆 memory 节随主配置一并提交），内容脱敏走 security 接口即时生效。
 * 资源绑定只能选择该团队有权使用的模型/插件/知识库；基础信息（头像/名称/公开状态）在「信息」分区维护（AppInfoSection）。
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

  const [modelId, setModelId] = useState<string>()
  const [prompt, setPrompt] = useState('')
  const [openingEnabled, setOpeningEnabled] = useState(false)
  const [openingStatement, setOpeningStatement] = useState('')
  const [quickInputs, setQuickInputs] = useState<string[]>([])
  const [wikiIds, setWikiIds] = useState<number[]>([])
  const [graphIds, setGraphIds] = useState<number[]>([])
  const [pluginIds, setPluginIds] = useState<string[]>([])
  const [workflowAppIds, setWorkflowAppIds] = useState<string[]>([])
  const [workflowAppOptions, setWorkflowAppOptions] = useState<AppItem[]>([])
  const [skillIds, setSkillIds] = useState<string[]>([])
  const [sandboxEnabled, setSandboxEnabled] = useState(false)
  const [sandboxTimeout, setSandboxTimeout] = useState<number | null>(null)
  const [sandboxRenew, setSandboxRenew] = useState(true)
  const [sandboxCpu, setSandboxCpu] = useState('')
  const [sandboxMemory, setSandboxMemory] = useState('')
  const [sandboxNetworkAction, setSandboxNetworkAction] = useState<string>()
  const [sandboxEgress, setSandboxEgress] = useState('')
  const [sandboxLimits, setSandboxLimits] = useState<SandboxLimits | null>(null)
  const [executionSettings, setExecutionSettings] = useState<Record<string, unknown>>({})
  // 审批策略：审批模式下自动放行的插件白名单与沙箱开关（存于 executionSettings.toolApproval）
  const [autoApprovePluginIds, setAutoApprovePluginIds] = useState<string[]>([])
  const [sandboxAutoApproved, setSandboxAutoApproved] = useState(false)
  // 上下文压缩参数（存于 executionSettings 顶层字段；null=未配置，按平台系统默认执行）
  const [compactionToolResult, setCompactionToolResult] = useState<number | null>(null)
  const [compactionSummarizeTokens, setCompactionSummarizeTokens] = useState<number | null>(null)
  const [compactionPreserveTurns, setCompactionPreserveTurns] = useState<number | null>(null)
  const [compactionHardLimit, setCompactionHardLimit] = useState<number | null>(null)
  const [compactionSummarization, setCompactionSummarization] = useState(false)
  const [compactionSummarizerModelId, setCompactionSummarizerModelId] = useState<string>()
  const [compactionDefaults, setCompactionDefaults] = useState<AgentCompactionDefaults | null>(null)
  // 子代理协作（存于 executionSettings.subAgent）
  const [subAgentEnabled, setSubAgentEnabled] = useState(false)
  const [subAgentTimeout, setSubAgentTimeout] = useState<number | null>(null)
  const [subAgentResultMaxChars, setSubAgentResultMaxChars] = useState<number | null>(null)
  const [pluginOptions, setPluginOptions] = useState<TeamPluginItemType[]>([])
  const [skillOptions, setSkillOptions] = useState<SkillOption[]>([])
  const [wikiOptions, setWikiOptions] = useState<WikiItem[]>([])
  const [graphOptions, setGraphOptions] = useState<KnowledgeGraphItem[]>([])
  const [modelOptions, setModelOptions] = useState<{ value: string; label: string }[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [republishing, setRepublishing] = useState(false)
  // 0=草稿有未发布变更 1=草稿与已发布一致（已发布应用 0 时线上仍按发布快照执行）
  const [localConfigStatus, setLocalConfigStatus] = useState(0)
  // 内容脱敏面板注册的保存回调（展开「内容脱敏」卡片并加载完成后才有值）：随「保存设置」一并提交
  const [securitySave, setSecuritySave] = useState<(() => Promise<void>) | null>(null)
  // 长期记忆面板注册的 memory 节构造回调（展开「长期记忆」卡片并加载完成后才有值）：随「保存设置」并入 executionSettings
  const memorySettingsRef = useRef<(() => Record<string, unknown>) | null>(null)

  const isAgent = detail?.appType !== 'workflow'
  // 外部应用面向外部用户/匿名开放，不支持沙箱与技能（后端保存强校验 + 对话装配兜底强制关闭）
  const isExternal = detail?.isExternal === true

  // 警告条/发布入口使用的状态：工作台传入的真值优先（头部重新发布后同步清除），否则用本地加载值
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
        setPrompt(config.prompt ?? '')
        setOpeningEnabled(Boolean(config.openingStatementEnabled))
        setOpeningStatement(config.openingStatement ?? '')
        setQuickInputs(config.quickInputs ?? [])
        setWikiIds(config.wikiIds ?? [])
        setGraphIds(config.graphIds ?? [])
        setPluginIds(config.plugins ?? [])
        setWorkflowAppIds(config.workflowApps ?? [])
        setSkillIds(config.skills ?? [])
        const settings = config.executionSettings ?? {}
        setExecutionSettings(settings)
        const sandbox = (settings.sandbox ?? {}) as Record<string, unknown>
        setSandboxEnabled(Boolean(sandbox.enabled))
        setSandboxTimeout(typeof sandbox.timeoutSeconds === 'number' ? (sandbox.timeoutSeconds as number) : null)
        setSandboxRenew(sandbox.renewOnAccess !== false)
        // 输入框预填生效值：应用显式配置优先，未配置的字段写平台默认值（可继续调整）
        setCompactionToolResult(typeof settings.toolResultTriggerMessages === 'number' ? (settings.toolResultTriggerMessages as number) : defaults.toolResultTriggerMessages)
        setCompactionSummarizeTokens(typeof settings.summarizeTriggerTokens === 'number' ? (settings.summarizeTriggerTokens as number) : defaults.summarizeTriggerTokens)
        setCompactionPreserveTurns(typeof settings.preserveTurns === 'number' ? (settings.preserveTurns as number) : defaults.preserveTurns)
        setCompactionHardLimit(typeof settings.hardTokenLimit === 'number' ? (settings.hardTokenLimit as number) : defaults.hardTokenLimit)
        setCompactionSummarization(typeof settings.enableSummarization === 'boolean' ? (settings.enableSummarization as boolean) : defaults.enableSummarization)
        setCompactionSummarizerModelId(typeof settings.summarizerModelId === 'string' && settings.summarizerModelId ? settings.summarizerModelId : undefined)
        const subAgent = (settings.subAgent ?? {}) as Record<string, unknown>
        setSubAgentEnabled(subAgent.enabled === true)
        setSubAgentTimeout(typeof subAgent.timeoutSeconds === 'number' ? (subAgent.timeoutSeconds as number) : null)
        setSubAgentResultMaxChars(typeof subAgent.resultMaxChars === 'number' ? (subAgent.resultMaxChars as number) : null)
        const toolApproval = (settings.toolApproval ?? {}) as Record<string, unknown>
        const autoPlugins = toolApproval.autoApprovePlugins
        setAutoApprovePluginIds(Array.isArray(autoPlugins) ? autoPlugins.map((x) => String(x)) : [])
        setSandboxAutoApproved(toolApproval.sandboxAutoApproved === true)
        const resource = (sandbox.resource ?? {}) as Record<string, unknown>
        setSandboxCpu(typeof resource.cpu === 'string' ? (resource.cpu as string) : '')
        setSandboxMemory(typeof resource.memory === 'string' ? (resource.memory as string) : '')
        const network = (sandbox.network ?? {}) as Record<string, unknown>
        setSandboxNetworkAction(typeof network.defaultAction === 'string' ? (network.defaultAction as string) : undefined)
        setSandboxEgress(
          Array.isArray(network.egress) ? (network.egress as unknown[]).map((x) => String(x)).join('\n') : '',
        )
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
      const [models, plugins, wikis, skills, apps, graphs] = await Promise.all([
        getTeamGatewayModels(teamId),
        getTeamPlugins(teamId),
        getWikis(teamId),
        // 默认技能不含个人技能；配置后默认启用，用户在对话的应用设置中可取消勾选
        getSkillOptions({ teamId }),
        // 流程应用绑定候选：本团队已发布的内部流程应用（对话中作为工具调用）
        getApps(teamId),
        // 图谱绑定候选：仅托管图（接入图不参与应用侧向量检索，不开放绑定）
        getKnowledgeGraphs(teamId),
      ])
      setModelOptions(
        models
          .filter((item) => item.aiModelId)
          .map((item) => ({
            value: String(item.aiModelId),
            label: item.name || item.modelId || '-',
          })),
      )
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
    return true
  }

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
    if (!isExternal && !validateSandboxLimits()) return
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
        const egress = sandboxEgress
          .split('\n')
          .map((x) => x.trim())
          .filter(Boolean)
        if (sandboxNetworkAction || egress.length) {
          sandbox.network = {
            ...(sandboxNetworkAction ? { defaultAction: sandboxNetworkAction } : {}),
            egress,
          }
        }
      }

      // 审批策略：自动放行插件收敛为本次绑定插件的子集（后端强校验），避免解绑插件后保存失败
      const toolApproval = {
        autoApprovePlugins: autoApprovePluginIds.filter((id) => pluginIds.includes(id)),
        sandboxAutoApproved: isExternal ? false : sandboxAutoApproved,
      }

      // 压缩参数：表单预填生效值，保存即写入显式配置（随发布快照生效）
      const mergedSettings: Record<string, unknown> = { ...executionSettings }
      mergedSettings.toolResultTriggerMessages = compactionToolResult ?? compactionDefaults?.toolResultTriggerMessages ?? 16
      mergedSettings.summarizeTriggerTokens = compactionSummarizeTokens ?? compactionDefaults?.summarizeTriggerTokens ?? 16000
      mergedSettings.preserveTurns = compactionPreserveTurns ?? compactionDefaults?.preserveTurns ?? 8
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
        prompt,
        wikiIds,
        graphIds,
        plugins: pluginIds,
        workflowApps: workflowAppIds,
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

  /** 重新发布：把当前草稿配置推入发布快照，线上对话立即生效（发布接口本身即重发语义） */
  const handleRepublish = async () => {
    if (!appId) return
    setRepublishing(true)
    try {
      await publishApp(appId)
      feedback.success(t('appManage.republishSuccess'))
      setLocalConfigStatus(1)
      onConfigStatusChange?.(1)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRepublishing(false)
    }
  }

  const pluginSelectOptions = pluginOptions.map((item) => ({
    value: String(item.pluginId),
    label: item.title || item.pluginName || '-',
  }))

  const skillSelectOptions = skillOptions.map((item) => ({
    value: String(item.id),
    label: item.name || item.key || '-',
  }))

  const workflowAppSelectOptions = workflowAppOptions.map((item) => ({
    value: String(item.appId),
    label: item.name || '-',
  }))

  const wikiSelectOptions = wikiOptions
    .filter((item) => item.wikiId != null)
    .map((item) => ({ value: Number(item.wikiId), label: item.name || '-' }))

  const graphSelectOptions = graphOptions.map((item) => ({
    value: Number(item.kgId),
    label: item.name || '-',
  }))

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      {/* 配置拆为收缩卡片便于定位；左栏内部滚动（不把调试对话栏顶出可视区），并让出更多宽度给右栏 */}
      <Col
        xs={24}
        lg={11}
        xxl={11}
        style={{ maxHeight: 'calc(100vh - 160px)', minHeight: 480, overflowY: 'auto', paddingRight: spacing.xs }}
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
              <Button size="small" type="primary" loading={republishing} onClick={() => void handleRepublish()}>
                {t('appManage.republish')}
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
                        <Form.Item label={t('appManage.model')} extra={t('appManage.modelHint')}>
                          <Select
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ width: '100%', maxWidth: 360 }}
                            placeholder={t('appManage.modelPlaceholder')}
                            loading={optionsLoading}
                            value={modelId}
                            onChange={setModelId}
                            options={modelOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.modelEmpty')}
                          />
                        </Form.Item>
                        <Divider style={{ margin: `${spacing.md}px 0` }} />
                        <Form.Item label={t('appManage.compactionSection')} extra={t('appManage.compactionHint')} style={{ marginBottom: spacing.sm }}>
                          <Row gutter={spacing.md}>
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
                              <Form.Item label={t('appManage.compactionPreserveTurns')} style={{ marginBottom: spacing.sm }}>
                                <InputNumber
                                  min={1}
                                  max={10000}
                                  precision={0}
                                  style={{ width: '100%' }}
                                  value={compactionPreserveTurns ?? undefined}
                                  onChange={(value) => setCompactionPreserveTurns(typeof value === 'number' ? value : null)}
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
                                  min={500}
                                  max={4000000}
                                  precision={0}
                                  style={{ width: '100%' }}
                                  value={compactionHardLimit ?? undefined}
                                  onChange={(value) => setCompactionHardLimit(typeof value === 'number' ? value : null)}
                                />
                              </Form.Item>
                            </Col>
                          </Row>
                        </Form.Item>
                        <Form.Item
                          label={t('appManage.compactionSummarization')}
                          valuePropName="checked"
                          extra={t('appManage.compactionSummarizationHint')}
                        >
                          <Switch checked={compactionSummarization} onChange={setCompactionSummarization} />
                        </Form.Item>
                        {compactionSummarization && (
                          <Form.Item label={t('appManage.compactionSummarizerModel')} extra={t('appManage.compactionSummarizerModelHint')}>
                            <Select
                              allowClear
                              showSearch
                              optionFilterProp="label"
                              style={{ width: '100%', maxWidth: 360 }}
                              placeholder={t('appManage.compactionSummarizerModelPlaceholder')}
                              value={compactionSummarizerModelId}
                              onChange={(value) => setCompactionSummarizerModelId(value)}
                              options={modelOptions}
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
                          <Switch checked={subAgentEnabled} onChange={setSubAgentEnabled} disabled={isExternal} />
                        </Form.Item>
                        {subAgentEnabled && !isExternal && (
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
                        )}
                      </Form>
                    ),
                  },
                ]}
              />
              {/* 二、用户提示：提示词 + 对话开场白 + 快捷输入 */}
              <Collapse
                defaultActiveKey="prompts"
                items={[
                  {
                    key: 'prompts',
                    label: groupHeader(t('appManage.groupPrompts'), t('appManage.groupPromptsDesc')),
                    children: (
                      <Form layout="vertical" disabled={!canManage}>
                        <Form.Item label={t('appManage.sectionPrompt')} extra={t('appManage.promptHint')}>
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
              {/* 三、能力扩展：插件 + 流程应用 + 技能 + 知识库 + 知识图谱 */}
              <Collapse
                defaultActiveKey="capabilities"
                items={[
                  {
                    key: 'capabilities',
                    label: groupHeader(t('appManage.groupCapabilities'), t('appManage.groupCapabilitiesDesc')),
                    children: (
                      <Form layout="vertical" disabled={!canManage}>
                        <Form.Item label={t('appManage.sectionPlugins')} extra={t('appManage.pluginsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.pluginsPlaceholder')}
                            loading={optionsLoading}
                            value={pluginIds}
                            onChange={setPluginIds}
                            options={pluginSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.pluginsEmpty')}
                          />
                        </Form.Item>
                        <Form.Item label={t('appManage.sectionWorkflowApps')} extra={t('appManage.workflowAppsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.workflowAppsPlaceholder')}
                            loading={optionsLoading}
                            value={workflowAppIds}
                            onChange={setWorkflowAppIds}
                            options={workflowAppSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.workflowAppsEmpty')}
                          />
                        </Form.Item>
                        {!isExternal && (
                          <Form.Item label={t('appManage.sectionSkills')} extra={t('appManage.skillsHint')}>
                            <Select
                              mode="multiple"
                              allowClear
                              showSearch
                              optionFilterProp="label"
                              style={{ width: '100%' }}
                              placeholder={t('appManage.skillsPlaceholder')}
                              loading={optionsLoading}
                              value={skillIds}
                              onChange={setSkillIds}
                              options={skillSelectOptions}
                              notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.skillsEmpty')}
                            />
                          </Form.Item>
                        )}
                        <Form.Item label={t('appManage.sectionKnowledge')} extra={t('appManage.knowledgeHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.knowledgePlaceholder')}
                            loading={optionsLoading}
                            value={wikiIds}
                            onChange={setWikiIds}
                            options={wikiSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.knowledgeEmpty')}
                          />
                        </Form.Item>
                        <Form.Item label={t('appManage.sectionGraphs')} extra={t('appManage.graphsHint')}>
                          <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ width: '100%' }}
                            placeholder={t('appManage.graphsPlaceholder')}
                            loading={optionsLoading}
                            value={graphIds}
                            onChange={setGraphIds}
                            options={graphSelectOptions}
                            notFoundContent={optionsLoading ? <Spin size="small" /> : t('appManage.graphsEmpty')}
                          />
                        </Form.Item>
                      </Form>
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
                              <Form.Item label={t('appManage.sandboxNetwork')} extra={t('appManage.sandboxNetworkHint')}>
                                <Select
                                  allowClear
                                  style={{ width: '100%', maxWidth: 360 }}
                                  placeholder={t('appManage.sandboxNetworkPlaceholder')}
                                  value={sandboxNetworkAction}
                                  onChange={setSandboxNetworkAction}
                                  options={[
                                    { value: 'allow', label: t('appManage.sandboxNetworkAllow') },
                                    { value: 'deny', label: t('appManage.sandboxNetworkDeny') },
                                  ]}
                                />
                              </Form.Item>
                              {sandboxNetworkAction && (
                                <Form.Item label={t('appManage.sandboxEgress')} extra={t('appManage.sandboxEgressHint')}>
                                  <Input.TextArea
                                    value={sandboxEgress}
                                    onChange={(e) => setSandboxEgress(e.target.value)}
                                    placeholder={'pypi.org\n*.github.com'}
                                    autoSize={{ minRows: 2, maxRows: 6 }}
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
              {/* 五、安全审批：审批模式下自动放行的插件白名单 */}
              <Collapse
                defaultActiveKey="approval"
                items={[
                  {
                    key: 'approval',
                    label: groupHeader(t('appManage.groupApproval'), t('appManage.groupApprovalDesc')),
                    children: (
                      <>
                        <div style={{ fontSize: 12, color: token.colorTextTertiary, marginBottom: spacing.sm }}>
                          {t('appManage.toolApprovalHint')}
                        </div>
                        <Form layout="vertical" disabled={!canManage}>
                          <Form.Item label={t('appManage.toolApprovalPlugins')} extra={t('appManage.toolApprovalPluginsHint')}>
                            <Select
                              mode="multiple"
                              allowClear
                              showSearch
                              optionFilterProp="label"
                              style={{ width: '100%' }}
                              placeholder={t('appManage.toolApprovalPluginsPlaceholder')}
                              value={autoApprovePluginIds}
                              onChange={setAutoApprovePluginIds}
                              options={pluginSelectOptions.filter((opt) => pluginIds.includes(opt.value))}
                              notFoundContent={t('appManage.toolApprovalPluginsEmpty')}
                            />
                          </Form.Item>
                        </Form>
                      </>
                    ),
                  },
                ]}
              />
              {/* 六、内容脱敏（原独立「安全」分区并入，展开才加载设置；随下方「保存设置」一并提交，security 接口即时生效不经发布快照） */}
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
              {/* 七、长期记忆（原独立「记忆」分区并入，展开才加载设置；memory 节随「保存设置」并入主配置，随发布快照生效；
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
      {/* 右栏调试对话与左栏同一高度基准（视口 - 页头），卡片内部弹性布局：消息区滚动、输入区停靠底部 */}
      <Col xs={24} lg={13} xxl={13} style={{ height: 'calc(100vh - 160px)', minHeight: 480 }}>
        {isAgent && canManage ? (
          <DSCard
            style={{ height: '100%', display: 'flex', flexDirection: 'column' }}
            styles={{ body: { padding: 20, flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' } }}
          >
            <AppDebugChat
              appId={appId}
              appAvatar={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}
              openingStatement={openingEnabled ? openingStatement.trim() : ''}
            />
          </DSCard>
        ) : (
          <DSCard title={t('appDebug.title')}>
            <Alert type="info" showIcon message={t('appDebug.adminOnly')} />
          </DSCard>
        )}
      </Col>
    </Row>
  )
}
