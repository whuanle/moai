import { useCallback, useEffect, useState } from 'react'
import { UploadOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { Alert, Avatar, Button, Col, Divider, Form, Input, InputNumber, Popconfirm, Row, Select, Space, Spin, Switch, Tag, Typography, Upload } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Card as DSCard, feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppAgentConfig,
  getAppDetail,
  publishApp,
  saveAppAgentConfig,
  unpublishApp,
  updateApp,
  uploadAppAvatar,
  type AppKind,
} from '@/api/app'
import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getWikis, type WikiItem } from '@/api/wiki'
import { resolveStorageUrl } from '@/utils/storage'

const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

/** 后端 memory 静态插件无 DB 记录，pluginId 为空 Guid，不能作为绑定目标 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

interface AppDetail {
  appId?: string | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  appType?: AppKind | null
  avatarPath?: string | null
  enableForeign?: boolean | null
  publishStatus?: number | null
  publishTime?: string | null
  myRole?: number | null
}

interface InfoFormValues {
  name: string
  description?: string
  enableForeign?: boolean
}

/**
 * 应用管理页：单页左右分栏——左栏应用信息（头像/名称/描述/允许外部使用），
 * 右栏 Agent 应用配置（对话模型、系统提示词、允许使用的插件与知识库）。
 * 入口在团队页应用卡片的「管理」按钮；资源绑定只能选择该团队有权使用的模型/插件/知识库。
 */
export function AppManage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetail | null>(null)
  const [modelId, setModelId] = useState<string>()
  const [prompt, setPrompt] = useState('')
  const [wikiIds, setWikiIds] = useState<number[]>([])
  const [pluginIds, setPluginIds] = useState<string[]>([])
  const [sandboxEnabled, setSandboxEnabled] = useState(false)
  const [sandboxTimeout, setSandboxTimeout] = useState<number | null>(null)
  const [sandboxRenew, setSandboxRenew] = useState(true)
  const [sandboxCpu, setSandboxCpu] = useState('')
  const [sandboxMemory, setSandboxMemory] = useState('')
  const [sandboxNetworkAction, setSandboxNetworkAction] = useState<string>()
  const [sandboxEgress, setSandboxEgress] = useState('')
  const [executionSettings, setExecutionSettings] = useState<Record<string, unknown>>({})
  const [pluginOptions, setPluginOptions] = useState<TeamPluginItemType[]>([])
  const [wikiOptions, setWikiOptions] = useState<WikiItem[]>([])
  const [modelOptions, setModelOptions] = useState<{ value: string; label: string }[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [savingInfo, setSavingInfo] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [publishing, setPublishing] = useState(false)
  const [infoForm] = Form.useForm<InfoFormValues>()

  const isAgent = detail?.appType !== 'workflow'
  const canManage = detail?.myRole != null && detail.myRole !== ROLE_MEMBER
  const isPublished = detail?.publishStatus === 1

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      const app = (await getAppDetail(appId)) as unknown as AppDetail
      setDetail(app)

      if (app?.appType !== 'workflow') {
        const config = await getAppAgentConfig(appId)
        setModelId(config.modelId ?? undefined)
        setPrompt(config.prompt ?? '')
        setWikiIds(config.wikiIds ?? [])
        setPluginIds(config.plugins ?? [])
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
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!detail) return
    infoForm.setFieldsValue({
      name: detail.name ?? '',
      description: detail.description ?? undefined,
      enableForeign: detail.enableForeign ?? false,
    })
  }, [detail, infoForm])

  /** 团队可访问的模型/插件/知识库选项（资源绑定的取值范围） */
  const loadOptions = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setOptionsLoading(true)
    try {
      const [models, plugins, wikis] = await Promise.all([
        getTeamGatewayModels(teamId),
        getTeamPlugins(teamId),
        getWikis(teamId),
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
        enableForeign: values.enableForeign ?? false,
      })
      feedback.success(t('appManage.updateSuccess'))
      await load()
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

  const handlePublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      await publishApp(appId)
      feedback.success(t('appManage.publishSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  const handleUnpublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      await unpublishApp(appId)
      feedback.success(t('appManage.unpublishSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  const avatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {    if (!file.type.startsWith('image/')) {
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
      .then(() => load())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
    return Upload.LIST_IGNORE
  }

  const pluginSelectOptions = pluginOptions.map((item) => ({
    value: String(item.pluginId),
    label: item.title || item.pluginName || '-',
  }))

  const wikiSelectOptions = wikiOptions
    .filter((item) => item.wikiId != null)
    .map((item) => ({ value: Number(item.wikiId), label: item.name || '-' }))

  const appName = detail?.name ?? ''

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        { title: <Link to={`/team/${teamId}/apps`}>{t('team.apps')}</Link> },
        { title: appName },
      ]}
      extra={
        <Space>
          {isAgent &&
            (isPublished ? (
              <Tag color="green">{t('appManage.published')}</Tag>
            ) : (
              <Tag>{t('appManage.unpublished')}</Tag>
            ))}
          {isAgent && canManage && (
            <Popconfirm
              title={isPublished ? t('appManage.unpublishConfirm') : t('appManage.publishConfirm')}
              onConfirm={() => void (isPublished ? handleUnpublish() : handlePublish())}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
            >
              <Button type="primary" loading={publishing}>
                {isPublished ? t('appManage.unpublish') : t('appManage.publish')}
              </Button>
            </Popconfirm>
          )}
          {isAgent && isPublished && (
            <Button onClick={() => navigate(`/team/${teamId}/app/${appId}/chat`)}>{t('appManage.enterChat')}</Button>
          )}
          <Button onClick={() => navigate(`/team/${teamId}/apps`)}>{t('appManage.backToList')}</Button>
        </Space>
      }
    >
      {loading ? (
        <div style={{ padding: spacing.xl, textAlign: 'center' }}>
          <Spin />
        </div>
      ) : (
        <>
          {!canManage && (
            <Alert
              type="info"
              showIcon
              message={t('appManage.memberHint')}
              style={{ marginBottom: spacing.md }}
            />
          )}
          <Row gutter={[spacing.md, spacing.md]} align="top">
            <Col xs={24} lg={10} xxl={9}>
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
                      <Text type="secondary" style={{ fontSize: 12 }}>
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
                  <Form.Item
                    name="enableForeign"
                    label={t('appManage.enableForeign')}
                    valuePropName="checked"
                    extra={t('appManage.enableForeignHint')}
                  >
                    <Switch checkedChildren={t('appManage.externalOn')} unCheckedChildren={t('appManage.externalOff')} />
                  </Form.Item>
                  {canManage && (
                    <Button type="primary" loading={savingInfo} onClick={() => void handleSaveInfo()}>
                      {t('appManage.saveInfo')}
                    </Button>
                  )}
                </Form>
              </DSCard>
            </Col>

            <Col xs={24} lg={14} xxl={15}>
              {!isAgent ? (
                <DSCard title={t('appManage.agentConfigTitle')}>
                  <Alert type="warning" showIcon message={t('appManage.workflowConfigUnavailable')} />
                </DSCard>
              ) : (
                <DSCard title={t('appManage.agentConfigTitle')}>
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
                </DSCard>
              )}
            </Col>
          </Row>
        </>
      )}
    </Page>
  )
}
