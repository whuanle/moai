import { useCallback, useEffect, useState } from 'react'
import { UploadOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { Alert, Avatar, Button, Col, Divider, Form, Input, InputNumber, Modal, Popconfirm, Row, Select, Space, Spin, Switch, Tag, Tooltip, Typography, Upload } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card as DSCard, feedback } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import {
  getAppAgentConfig,
  saveAppAgentConfig,
  updateApp,
  uploadAppAvatar,
  type AppKind,
} from '@/api/app'
import {
  applyPublication,
  getTeamPublicationList,
  withdrawPublication,
  type PublicationReviewItem,
} from '@/api/publication'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getSkillOptions, type SkillOption } from '@/api/skills'
import { getWikis, type WikiItem } from '@/api/wiki'
import { resolveStorageUrl } from '@/utils/storage'
import { AppDebugChat } from './chat/AppDebugChat'

const { Text } = Typography

/** 后端 memory 静态插件无 DB 记录，pluginId 为空 Guid，不能作为绑定目标 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

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

interface InfoFormValues {
  name: string
  description?: string
  isAuth?: boolean
}

export interface AppConfigSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  loading: boolean
  canManage: boolean
  onReload: () => Promise<void> | void
}

/**
 * 应用「配置」分区：左栏应用信息（头像/名称/描述/授权与公开），
 * 右栏 Agent 配置（对话模型、系统提示词、插件与知识库、沙箱参数）与调试对话面板。
 * 资源绑定只能选择该团队有权使用的模型/插件/知识库；工作流应用仅展示应用信息与未开放提示。
 */
export function AppConfigSection({ teamId, appId, detail, loading, canManage, onReload }: AppConfigSectionProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()

  const [modelId, setModelId] = useState<string>()
  const [prompt, setPrompt] = useState('')
  const [wikiIds, setWikiIds] = useState<number[]>([])
  const [pluginIds, setPluginIds] = useState<string[]>([])
  const [skillIds, setSkillIds] = useState<string[]>([])
  const [sandboxEnabled, setSandboxEnabled] = useState(false)
  const [sandboxTimeout, setSandboxTimeout] = useState<number | null>(null)
  const [sandboxRenew, setSandboxRenew] = useState(true)
  const [sandboxCpu, setSandboxCpu] = useState('')
  const [sandboxMemory, setSandboxMemory] = useState('')
  const [sandboxNetworkAction, setSandboxNetworkAction] = useState<string>()
  const [sandboxEgress, setSandboxEgress] = useState('')
  const [executionSettings, setExecutionSettings] = useState<Record<string, unknown>>({})
  const [pluginOptions, setPluginOptions] = useState<TeamPluginItemType[]>([])
  const [skillOptions, setSkillOptions] = useState<SkillOption[]>([])
  const [wikiOptions, setWikiOptions] = useState<WikiItem[]>([])
  const [modelOptions, setModelOptions] = useState<{ value: string; label: string }[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [savingInfo, setSavingInfo] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [infoForm] = Form.useForm<InfoFormValues>()

  const isAgent = detail?.appType !== 'workflow'

  useEffect(() => {
    if (loading || !appId || !isAgent) return
    const loadConfig = async () => {
      try {
        const config = await getAppAgentConfig(appId)
        setModelId(config.modelId ?? undefined)
        setPrompt(config.prompt ?? '')
        setWikiIds(config.wikiIds ?? [])
        setPluginIds(config.plugins ?? [])
        setSkillIds(config.skills ?? [])
        const settings = config.executionSettings ?? {}
        setExecutionSettings(settings)
        const sandbox = (settings.sandbox ?? {}) as Record<string, unknown>
        setSandboxEnabled(Boolean(sandbox.enabled))
        setSandboxTimeout(typeof sandbox.timeoutSeconds === 'number' ? (sandbox.timeoutSeconds as number) : null)
        setSandboxRenew(sandbox.renewOnAccess !== false)
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
  }, [appId, isAgent, loading])

  useEffect(() => {
    if (!detail) return
    infoForm.setFieldsValue({
      name: detail.name ?? '',
      description: detail.description ?? undefined,
      isAuth: detail.isAuth ?? false,
    })
  }, [detail, infoForm])

  // 上架审核：应用公开（is_public）需系统管理员审批，团队侧可申请/撤回并查看审批状态
  const [publicationItems, setPublicationItems] = useState<PublicationReviewItem[]>([])
  const [publicationLoading, setPublicationLoading] = useState(false)
  const [applyOpen, setApplyOpen] = useState(false)
  const [applyReason, setApplyReason] = useState('')
  const [applying, setApplying] = useState(false)

  const pendingPublication = publicationItems.find((x) => x.state === 'pending')
  const lastRejected = publicationItems.find((x) => x.state === 'rejected')

  const loadPublications = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setPublicationLoading(true)
    try {
      const list = await getTeamPublicationList(teamId, { resourceType: 'app' })
      setPublicationItems(list.filter((x) => x.resourceId === appId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublicationLoading(false)
    }
  }, [teamId, appId])

  useEffect(() => {
    void loadPublications()
  }, [loadPublications])

  const handleApplyPublication = async () => {
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'app',
        resourceId: appId,
        applyReason: applyReason.trim() || undefined,
      })
      feedback.success(t('appManage.applySuccess'))
      setApplyOpen(false)
      setApplyReason('')
      await loadPublications()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdrawPublication = async () => {
    if (!pendingPublication?.publicationId) return
    try {
      await withdrawPublication(pendingPublication.publicationId)
      feedback.success(t('appManage.withdrawSuccess'))
      await loadPublications()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 团队可访问的模型/插件/知识库选项（资源绑定的取值范围） */
  const loadOptions = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setOptionsLoading(true)
    try {
      const [models, plugins, wikis, skills] = await Promise.all([
        getTeamGatewayModels(teamId),
        getTeamPlugins(teamId),
        getWikis(teamId),
        // 应用绑定场景不含个人技能；此处绑定即锁定，用户在对话中不可移除
        getSkillOptions({ teamId }),
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

  const handleSaveInfo = async () => {
    if (!appId) return
    const values = await infoForm.validateFields()
    setSavingInfo(true)
    try {
      await updateApp(appId, {
        name: values.name,
        description: values.description,
        isExternal: detail?.isExternal ?? false,
        isAuth: values.isAuth ?? false,
      })
      feedback.success(t('appManage.updateSuccess'))
      await onReload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSavingInfo(false)
    }
  }

  const handleSaveConfig = async () => {
    if (!appId) return
    setSavingConfig(true)
    try {
      const sandbox: Record<string, unknown> = { enabled: sandboxEnabled, renewOnAccess: sandboxRenew }
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

      await saveAppAgentConfig(appId, {
        modelId: modelId ?? null,
        prompt,
        wikiIds,
        plugins: pluginIds,
        skills: skillIds,
        // 与已加载的执行参数合并，避免覆盖压缩等其他扩展配置
        executionSettings: { ...executionSettings, sandbox },
      })
      feedback.success(t('appManage.configSaveSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSavingConfig(false)
    }
  }

  const avatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('appManage.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('appManage.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    if (!appId) return Upload.LIST_IGNORE
    setUploadingAvatar(true)
    uploadAppAvatar(appId, file)
      .then(() => feedback.success(t('appManage.avatarSuccess')))
      .then(() => onReload())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
    return Upload.LIST_IGNORE
  }

  const pluginSelectOptions = pluginOptions.map((item) => ({
    value: String(item.pluginId),
    label: item.title || item.pluginName || '-',
  }))

  const skillSelectOptions = skillOptions.map((item) => ({
    value: String(item.id),
    label: item.name || item.key || '-',
  }))

  const wikiSelectOptions = wikiOptions
    .filter((item) => item.wikiId != null)
    .map((item) => ({ value: Number(item.wikiId), label: item.name || '-' }))

  const appName = detail?.name ?? ''

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      <Col xs={24} lg={15} xxl={16}>
        {!canManage && (
          <Alert
            type="info"
            showIcon
            message={t('appManage.memberHint')}
            style={{ marginBottom: spacing.md }}
          />
        )}
        <DSCard title={t('appManage.sectionInfo')}>
          <Form form={infoForm} layout="vertical" disabled={!canManage}>
            <Form.Item label={t('appManage.avatar')}>
              <Space align="center">
                <Avatar shape="square" size={64} src={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}>
                  {appName.slice(0, 1).toUpperCase()}
                </Avatar>
                <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*">
                  <Button icon={<UploadOutlined />} loading={uploadingAvatar} disabled={!canManage}>
                    {t('appManage.avatarUpload')}
                  </Button>
                </Upload>
              </Space>
              <div style={{ marginTop: spacing.xs }}>
                <Text type="secondary" style={{ fontSize: fontSize.xs }}>
                  {t('appManage.avatarHint')}
                </Text>
              </div>
            </Form.Item>
            <Form.Item label={t('appManage.type')}>
              {isAgent ? (
                <Tag color="blue">{t('appManage.typeAgent')}</Tag>
              ) : (
                <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
              )}
            </Form.Item>
            <Form.Item
              name="name"
              label={t('appManage.name')}
              rules={[
                { required: true, message: t('appManage.namePlaceholder') },
                { max: 20, message: `${t('appManage.name')} ≤ 20` },
              ]}
            >
              <Input placeholder={t('appManage.namePlaceholder')} maxLength={20} />
            </Form.Item>
            <Form.Item name="description" label={t('appManage.description')} rules={[{ max: 255 }]}>
              <Input.TextArea placeholder={t('appManage.descriptionPlaceholder')} maxLength={255} rows={4} />
            </Form.Item>
            {detail?.isExternal ? (
              <Form.Item
                name="isAuth"
                label={t('appManage.isAuth')}
                valuePropName="checked"
                extra={t('appManage.isAuthHint')}
              >
                <Switch checkedChildren={t('appManage.authOn')} unCheckedChildren={t('appManage.authOff')} />
              </Form.Item>
            ) : (
              <Form.Item label={t('appManage.publicationStatus')} extra={t('appManage.publicationHint')}>
                {detail?.isPublic ? (
                  <Tag color="green">{t('appManage.publicOn')}</Tag>
                ) : pendingPublication ? (
                  <Space size={spacing.sm}>
                    <Tag color="orange">{t('appManage.publicationPending')}</Tag>
                    {canManage && (
                      <Popconfirm title={t('appManage.withdrawConfirm')} onConfirm={() => void handleWithdrawPublication()}>
                        <Button size="small" loading={publicationLoading}>
                          {t('appManage.withdrawApplication')}
                        </Button>
                      </Popconfirm>
                    )}
                  </Space>
                ) : (
                  <Space size={spacing.sm}>
                    {lastRejected && (
                      <Tooltip
                        title={
                          lastRejected.reviewComment
                            ? `${t('appManage.reviewCommentLabel')}: ${lastRejected.reviewComment}`
                            : undefined
                        }
                      >
                        <Tag color="error">{t('appManage.publicationRejected')}</Tag>
                      </Tooltip>
                    )}
                    {canManage ? (
                      <Button size="small" type="primary" onClick={() => setApplyOpen(true)}>
                        {lastRejected ? t('appManage.reapplyPublication') : t('appManage.applyPublication')}
                      </Button>
                    ) : (
                      <Tag>{t('appManage.publicOff')}</Tag>
                    )}
                  </Space>
                )}
              </Form.Item>
            )}
            {canManage && (
              <Button type="primary" loading={savingInfo} onClick={() => void handleSaveInfo()}>
                {t('appManage.saveInfo')}
              </Button>
            )}
          </Form>
        </DSCard>
        <DSCard title={t('appManage.agentConfigTitle')} style={{ marginTop: spacing.md }}>
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
              <Divider style={{ margin: `${spacing.md}px 0` }} />
              <Form layout="vertical" disabled={!canManage}>
                <Form.Item label={t('appManage.sandboxEnabled')} valuePropName="checked" extra={t('appManage.sandboxHint')}>
                  <Switch checked={sandboxEnabled} onChange={setSandboxEnabled} />
                </Form.Item>
                {sandboxEnabled && (
                  <>
                    <Row gutter={spacing.md}>
                      <Col xs={24} md={12}>
                        <Form.Item label={t('appManage.sandboxTimeout')} extra={t('appManage.sandboxTimeoutHint')}>
                          <InputNumber
                            min={60}
                            max={86400}
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
                        <Form.Item label={t('appManage.sandboxCpu')} extra={t('appManage.sandboxCpuHint')}>
                          <Input
                            value={sandboxCpu}
                            onChange={(e) => setSandboxCpu(e.target.value)}
                            placeholder="1"
                          />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item label={t('appManage.sandboxMemory')} extra={t('appManage.sandboxMemoryHint')}>
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
      <Col xs={24} lg={9} xxl={8}>
        <DSCard title={t('appDebug.title')}>
          {isAgent && canManage ? (
            <AppDebugChat
              appId={appId}
              appAvatar={resolveStorageUrl(detail?.avatarPath ?? null) || undefined}
            />
          ) : (
            <Alert type="info" showIcon message={t('appDebug.adminOnly')} />
          )}
        </DSCard>
      </Col>
      <Modal
        open={applyOpen}
        title={t('appManage.applyPublication')}
        onCancel={() => setApplyOpen(false)}
        confirmLoading={applying}
        onOk={() => void handleApplyPublication()}
        okText={t('appManage.applySubmit')}
        cancelText={t('appManage.cancel')}
        maskClosable={false}
        destroyOnHidden
      >
        <Text type="secondary" style={{ display: 'block', marginBottom: spacing.xs, fontSize: 12 }}>
          {t('appManage.applyHint')}
        </Text>
        <Input.TextArea
          value={applyReason}
          onChange={(e) => setApplyReason(e.target.value)}
          placeholder={t('appManage.applyReasonPlaceholder')}
          maxLength={255}
          rows={3}
        />
      </Modal>
    </Row>
  )
}
