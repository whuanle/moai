import { useCallback, useEffect, useState } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Col, Divider, Form, Input, InputNumber, Row, Select, Spin, Switch } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppAgentConfig,
  getApps,
  getSandboxLimits,
  publishApp,
  saveAppAgentConfig,
  type AppKind,
  type AppItem,
  type SandboxLimits,
} from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getSkillOptions, type SkillOption } from '@/api/skills'
import { getWikis, type WikiItem } from '@/api/wiki'
import { resolveStorageUrl } from '@/utils/storage'
import { parseCpuMillicores, parseMemoryBytes } from '@/utils/sandboxQuantity'
import { AppDebugChat } from './chat/AppDebugChat'

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
  publishStatus?: number | null
  publishTime?: string | null
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
 * 应用「配置」分区：左栏 Agent 配置（对话模型、系统提示词、插件与知识库、沙箱参数），
 * 右栏调试对话面板。资源绑定只能选择该团队有权使用的模型/插件/知识库；
 * 基础信息（头像/名称/公开状态）在「信息」分区维护（AppInfoSection）。
 */
export function AppConfigSection({ teamId, appId, detail, loading, canManage, configStatus, onConfigStatusChange }: AppConfigSectionProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()

  const [modelId, setModelId] = useState<string>()
  const [prompt, setPrompt] = useState('')
  const [openingEnabled, setOpeningEnabled] = useState(false)
  const [openingStatement, setOpeningStatement] = useState('')
  const [quickInputs, setQuickInputs] = useState<string[]>([])
  const [wikiIds, setWikiIds] = useState<number[]>([])
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
  const [pluginOptions, setPluginOptions] = useState<TeamPluginItemType[]>([])
  const [skillOptions, setSkillOptions] = useState<SkillOption[]>([])
  const [wikiOptions, setWikiOptions] = useState<WikiItem[]>([])
  const [modelOptions, setModelOptions] = useState<{ value: string; label: string }[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [republishing, setRepublishing] = useState(false)
  // 0=草稿有未发布变更 1=草稿与已发布一致（已发布应用 0 时线上仍按发布快照执行）
  const [localConfigStatus, setLocalConfigStatus] = useState(0)

  const isAgent = detail?.appType !== 'workflow'
  // 外部应用面向外部用户/匿名开放，不支持沙箱与技能（后端保存强校验 + 对话装配兜底强制关闭）
  const isExternal = detail?.isExternal === true

  // 警告条/发布入口使用的状态：工作台传入的真值优先（头部重新发布后同步清除），否则用本地加载值
  const effectiveConfigStatus = configStatus ?? localConfigStatus

  useEffect(() => {
    if (loading || !appId || !isAgent) return
    const loadConfig = async () => {
      try {
        const [config, limits] = await Promise.all([getAppAgentConfig(appId), getSandboxLimits()])
        setSandboxLimits(limits)
        setLocalConfigStatus(config.status ?? 0)
        onConfigStatusChange?.(config.status ?? 0)
        setModelId(config.modelId ?? undefined)
        setPrompt(config.prompt ?? '')
        setOpeningEnabled(Boolean(config.openingStatementEnabled))
        setOpeningStatement(config.openingStatement ?? '')
        setQuickInputs(config.quickInputs ?? [])
        setWikiIds(config.wikiIds ?? [])
        setPluginIds(config.plugins ?? [])
        setWorkflowAppIds(config.workflowApps ?? [])
        setSkillIds(config.skills ?? [])
        const settings = config.executionSettings ?? {}
        setExecutionSettings(settings)
        const sandbox = (settings.sandbox ?? {}) as Record<string, unknown>
        setSandboxEnabled(Boolean(sandbox.enabled))
        setSandboxTimeout(typeof sandbox.timeoutSeconds === 'number' ? (sandbox.timeoutSeconds as number) : null)
        setSandboxRenew(sandbox.renewOnAccess !== false)
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

  /** 团队可访问的模型/插件/知识库选项（资源绑定的取值范围） */
  const loadOptions = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setOptionsLoading(true)
    try {
      const [models, plugins, wikis, skills, apps] = await Promise.all([
        getTeamGatewayModels(teamId),
        getTeamPlugins(teamId),
        getWikis(teamId),
        // 默认技能不含个人技能；配置后默认启用，用户在对话的应用设置中可取消勾选
        getSkillOptions({ teamId }),
        // 流程应用绑定候选：本团队已发布的内部流程应用（对话中作为工具调用）
        getApps(teamId),
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

  const handleSaveConfig = async () => {
    if (!appId) return
    if (!isExternal && !validateSandboxLimits()) return
    setSavingConfig(true)
    try {
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

      await saveAppAgentConfig(appId, {
        modelId: modelId ?? null,
        prompt,
        wikiIds,
        plugins: pluginIds,
        workflowApps: workflowAppIds,
        // 外部应用不支持技能：固定空列表（后端强校验拒绝非空）
        skills: isExternal ? [] : skillIds,
        openingStatement,
        openingStatementEnabled: openingEnabled,
        quickInputs: quickInputs.map((x) => x.trim()).filter(Boolean),
        // 与已加载的执行参数合并，避免覆盖压缩等其他扩展配置
        executionSettings: { ...executionSettings, sandbox, toolApproval },
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

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      {/* 配置项较多：左栏内部滚动（不把调试对话栏顶出可视区），并让出更多宽度给右栏 */}
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
        <DSCard title={t('appManage.agentConfigTitle')}>
          {!isAgent ? (
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
          ) : (
            <>
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
              </Form>
              {isExternal && (
                <Alert
                  type="info"
                  showIcon
                  message={t('appManage.externalRestrictionHint')}
                  style={{ marginBottom: spacing.md }}
                />
              )}
              {!isExternal && (
                <>
                  <Divider style={{ margin: `${spacing.md}px 0` }} />
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
                  </Form>
                </>
              )}
              <Divider style={{ margin: `${spacing.md}px 0` }} />
              <Form layout="vertical" disabled={!canManage}>
                <Form.Item label={t('appManage.toolApprovalSection')} extra={t('appManage.toolApprovalHint')}>
                  {!isExternal && (
                    <Form.Item
                      label={t('appManage.toolApprovalSandbox')}
                      valuePropName="checked"
                      extra={t('appManage.toolApprovalSandboxHint')}
                      style={{ marginBottom: spacing.sm }}
                    >
                      <Switch checked={sandboxAutoApproved} onChange={setSandboxAutoApproved} />
                    </Form.Item>
                  )}
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
                </Form.Item>
              </Form>
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
        </DSCard>
      </Col>
      <Col xs={24} lg={13} xxl={13}>
        <DSCard title={t('appDebug.title')}>
          {isAgent && canManage ? (
            <AppDebugChat
              appId={appId}
              appAvatar={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}
              openingStatement={openingEnabled ? openingStatement.trim() : ''}
            />
          ) : (
            <Alert type="info" showIcon message={t('appDebug.adminOnly')} />
          )}
        </DSCard>
      </Col>
    </Row>
  )
}
