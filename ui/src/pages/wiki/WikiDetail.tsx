import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ApiOutlined,
  BookOutlined,
  ExperimentOutlined,
  FileTextOutlined,
  GlobalOutlined,
  SettingOutlined,
} from '@ant-design/icons'
import { Alert, AutoComplete, Button, Form, Input, Layout, Menu, Popconfirm, Progress, Select, Space, Spin, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import { AvatarUpload, Card, feedback, Page, SiderHeader } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getWikiDetail,
  getWikiModelMigrationStatus,
  getWikiModelOptions,
  startWikiModelMigration,
  updateWiki,
  updateWikiEmbeddingConfig,
  updateWikiRerankModel,
  uploadWikiAvatar,
  type WikiModelMigrationStatus,
} from '@/api/wiki'
import { resolveStorageUrl } from '@/utils/storage'
import { NAME_PATTERN } from '@/utils/validation'
import { WikiDocuments } from './WikiDocuments'
import { WikiMcp } from './WikiMcp'
import { WikiRecallTest } from './WikiRecallTest'
import { WikiSources } from './WikiSources'

const { Sider, Content } = Layout
const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const SECTION_KEYS = ['files', 'sources', 'recall', 'mcp', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]

interface WikiDetail {
  wikiId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  avatarPath?: string | null
  myRole?: number | null
  embeddingModelId?: string | null
  embeddingDimensions?: number | null
  isLock?: boolean | null
  rerankModelId?: string | null
  createTime?: string | null
  /** 默认工作流配置（切割 / 元数据 / 向量化三步预设），为空表示未配置 */
}

interface SettingsFormValues {
  name: string
  description?: string
}

interface EmbeddingFormValues {
  embeddingModelId: string
  embeddingDimensions: number
}

interface RerankFormValues {
  rerankModelId?: string | null
}

interface ModelOption {
  id?: string | null
  name?: string | null
}

/** 向量维度选项：知识库维度由用户手动设置，上限 2000（pgvector 建 hnsw 索引硬上限）。 */
const EMBEDDING_DIMENSION_OPTIONS = [256, 512, 1024, 2048]
const EMBEDDING_DIMENSION_MAX = 2000

export function WikiDetail() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; wikiId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const wikiId = Number(params.wikiId)
  const rawSection = params.section ?? 'files'
  const section: SectionKey = SECTION_KEYS.includes(rawSection as SectionKey) ? (rawSection as SectionKey) : 'files'

  const [loading, setLoading] = useState(true)
  const [wiki, setWiki] = useState<WikiDetail | null>(null)
  const [saving, setSaving] = useState(false)
  const [embeddingSaving, setEmbeddingSaving] = useState(false)
  const [rerankSaving, setRerankSaving] = useState(false)
  const [modelOptions, setModelOptions] = useState<{ embeddingModels: ModelOption[]; conversationModels: ModelOption[]; rerankModels: ModelOption[] }>({
    embeddingModels: [],
    conversationModels: [],
    rerankModels: [],
  })
  const [modelOptionsLoading, setModelOptionsLoading] = useState(false)
  const [modelOptionsFailed, setModelOptionsFailed] = useState(false)
  const [modelOptionsLoaded, setModelOptionsLoaded] = useState(false)
  const [dimensionsOpen, setDimensionsOpen] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [migrationStatus, setMigrationStatus] = useState<WikiModelMigrationStatus | null>(null)
  const [startingMigration, setStartingMigration] = useState(false)
  const [settingsForm] = Form.useForm<SettingsFormValues>()
  const [embeddingForm] = Form.useForm<EmbeddingFormValues>()
  const [rerankForm] = Form.useForm<RerankFormValues>()

  const isAdminPlus = wiki?.myRole != null && wiki.myRole !== ROLE_MEMBER
  const embeddingLocked = wiki?.isLock === true
  const embeddingDisabled = embeddingLocked || modelOptionsLoading || modelOptionsFailed
  // 重排序模型与向量化配置解耦：知识库锁定后仍可修改
  const rerankDisabled = modelOptionsLoading || modelOptionsFailed

  const migrationTask = migrationStatus?.activeMigration ?? null
  // 1 等待 / 2 处理中视为迁移进行中
  const wikiMigrating = migrationTask?.state === 1 || migrationTask?.state === 2
  const migrationPercent =
    migrationTask == null
      ? 0
      : migrationTask.state === 4
        ? 100
        : (migrationTask.totalCount ?? 0) > 0
          ? Math.min(100, Math.round(((migrationTask.migratedCount ?? 0) / (migrationTask.totalCount ?? 1)) * 100))
          : 0
  /** 迁移要求表单中的目标模型/维度与当前生效配置不同 */
  const migrationTargetChanged =
    embeddingForm.getFieldValue('embeddingModelId') !== (wiki?.embeddingModelId ?? '') ||
    embeddingForm.getFieldValue('embeddingDimensions') !== (wiki?.embeddingDimensions ?? undefined)

  const load = useCallback(async () => {
    if (!Number.isFinite(wikiId) || wikiId <= 0) return
    setLoading(true)
    try {
      const res = (await getWikiDetail(wikiId)) as unknown as WikiDetail
      setWiki(res)
      settingsForm.setFieldsValue({
        name: res.name ?? '',
        description: res.description ?? undefined,
      })
      embeddingForm.setFieldsValue({
        embeddingModelId: res.embeddingModelId ?? undefined,
        embeddingDimensions: res.embeddingDimensions ?? undefined,
      })
      rerankForm.setFieldsValue({ rerankModelId: res.rerankModelId ?? null })
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [wikiId, settingsForm, embeddingForm, rerankForm])

  /**
   * 静默刷新：保留当前页面结构与已挂载的表单，仅更新 wiki 状态与表单字段值。
   * 用于保存配置/上传头像后的局部刷新，避免整张 Card 卸载再挂载。
   */
  const reload = useCallback(async () => {
    if (!Number.isFinite(wikiId) || wikiId <= 0) return
    try {
      const res = (await getWikiDetail(wikiId)) as unknown as WikiDetail
      setWiki(res)
      settingsForm.setFieldsValue({
        name: res.name ?? '',
        description: res.description ?? undefined,
      })
      embeddingForm.setFieldsValue({
        embeddingModelId: res.embeddingModelId ?? undefined,
        embeddingDimensions: res.embeddingDimensions ?? undefined,
      })
      rerankForm.setFieldsValue({ rerankModelId: res.rerankModelId ?? null })
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [wikiId, settingsForm, embeddingForm, rerankForm])

  useEffect(() => {
    void load()
  }, [load])

  // 锁定状态下加载迁移状态（最近任务进度），供设置页展示与轮询
  useEffect(() => {
    if (!isAdminPlus || !embeddingLocked) return
    let cancelled = false
    getWikiModelMigrationStatus(wikiId)
      .then((status) => {
        if (!cancelled) setMigrationStatus(status)
      })
      .catch(() => {
        // 状态加载失败不影响其余设置展示
        if (!cancelled) setMigrationStatus(null)
      })
    return () => {
      cancelled = true
    }
  }, [isAdminPlus, embeddingLocked, wikiId])

  // 迁移进行中每 3s 轮询一次进度，终态提示并刷新知识库配置（模型/维度已由服务端切换）
  useEffect(() => {
    if (!wikiMigrating) return
    const timer = window.setInterval(async () => {
      try {
        const status = await getWikiModelMigrationStatus(wikiId)
        setMigrationStatus(status)
        const state = status.activeMigration?.state
        if (state === 4) {
          feedback.success(t('wiki.migration.success'))
          void reload()
        } else if (state === 5) {
          feedback.error(t('wiki.migration.failed'))
        }
      } catch {
        // 轮询失败忽略，下一轮重试
      }
    }, 3000)
    return () => window.clearInterval(timer)
  }, [wikiMigrating, wikiId, t, reload])

  useEffect(() => {
    if (!isAdminPlus) return
    let cancelled = false
    setModelOptionsLoading(true)
    setModelOptionsLoaded(false)
    setModelOptionsFailed(false)
    getWikiModelOptions(teamId)
      .then((options) => {
        if (cancelled) return
        setModelOptions({
          embeddingModels: options?.embeddingModels ?? [],
          conversationModels: options?.conversationModels ?? [],
          rerankModels: options?.rerankModels ?? [],
        })
      })
      .catch(() => {
        if (cancelled) return
        setModelOptions({ embeddingModels: [], conversationModels: [], rerankModels: [] })
        setModelOptionsFailed(true)
      })
      .finally(() => {
        if (cancelled) return
        setModelOptionsLoading(false)
        setModelOptionsLoaded(true)
      })
    return () => {
      cancelled = true
    }
  }, [isAdminPlus, teamId])

  const menuItems: Required<MenuProps>['items'] = useMemo(
    () => [
      { key: 'files', icon: <FileTextOutlined />, label: t('wiki.menuFiles') },
      { key: 'sources', icon: <GlobalOutlined />, label: t('wiki.menuSources') },
      { key: 'recall', icon: <ExperimentOutlined />, label: t('wiki.menuRecall') },
      { key: 'mcp', icon: <ApiOutlined />, label: t('wiki.menuMcp') },
      { key: 'settings', icon: <SettingOutlined />, label: t('wiki.menuSettings') },
    ],
    [t],
  )

  const handleSaveSettings = async () => {
    const values = await settingsForm.validateFields()
    setSaving(true)
    try {
      await updateWiki(wikiId, {
        name: values.name,
        description: values.description,
      })
      feedback.success(t('wiki.saveSuccess'))
      void reload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleSaveEmbeddingConfig = async () => {
    if (embeddingLocked || modelOptionsFailed) return
    setEmbeddingSaving(true)
    try {
      const values = await embeddingForm.validateFields()
      await updateWikiEmbeddingConfig(wikiId, values)
      feedback.success(t('wiki.embedding.saveSuccess'))
      void reload()
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      if (error instanceof Error && (error.message.includes('409') || error.message.includes('锁定'))) {
        void reload()
      }
    } finally {
      setEmbeddingSaving(false)
    }
  }

  const handleSaveRerankModel = async () => {
    if (modelOptionsFailed) return
    setRerankSaving(true)
    try {
      const values = await rerankForm.validateFields()
      await updateWikiRerankModel(wikiId, values.rerankModelId ?? null)
      feedback.success(t('wiki.rerank.saveSuccess'))
      void reload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRerankSaving(false)
    }
  }

  /** 发起向量化模型迁移：以表单当前选择的模型/维度重建该库全部向量 */
  const handleStartMigration = async () => {
    const values = embeddingForm.getFieldsValue()
    if (!values.embeddingModelId) {
      feedback.error(t('wiki.embedding.modelRequired'))
      return
    }
    setStartingMigration(true)
    try {
      const taskId = await startWikiModelMigration(wikiId, values.embeddingModelId, Number(values.embeddingDimensions))
      if (!taskId) {
        feedback.error(t('wiki.migration.failed'))
        return
      }
      feedback.success(t('wiki.migration.started'))
      setMigrationStatus(await getWikiModelMigrationStatus(wikiId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setStartingMigration(false)
    }
  }

  const avatarSrc = wiki?.avatarPath?.trim() ? resolveStorageUrl(wiki.avatarPath) : undefined

  /** 校验并上传知识库头像；非法文件直接忽略 */
  const handleAvatarFile = (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('wiki.avatarTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('wiki.avatarSizeError'))
      return
    }
    setUploadingAvatar(true)
    uploadWikiAvatar(wikiId, file)
      .then(() => feedback.success(t('wiki.avatarSuccess')))
      .then(() => reload())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
  }

  if (loading) {
    return (
      <Page>
        <div style={{ padding: '48px 0', textAlign: 'center' }}>
          <Spin />
        </div>
      </Page>
    )
  }

  if (!wiki) {
    return (
      <Page>
        <div style={{ padding: '48px 0', textAlign: 'center' }}>
          <Text type="secondary">{t('wiki.notFound')}</Text>
        </div>
      </Page>
    )
  }

  return (
    <Page>
      <Layout style={{ background: 'transparent', gap: spacing.md }}>
        <Sider width={200} style={{ background: 'transparent' }}>
          {/* 侧栏顶部身份区：上段「库头像 + 库名」、下段「← 返回知识库」整宽按钮（与团队/应用工作台同构，仿首页侧边栏头部） */}
          <SiderHeader
            name={wiki?.name}
            avatarSrc={avatarSrc}
            avatarFallback={<BookOutlined />}
            backTo={`/team/${teamId}/knowledge`}
            backLabel={t('wiki.backToKnowledge')}
          />
          <Menu
            mode="inline"
            items={menuItems}
            selectedKeys={[section]}
            onClick={({ key }) => navigate(`/team/${teamId}/wiki/${wikiId}/${key}`)}
            style={{ borderRadius: spacing.sm }}
          />
        </Sider>
        <Content>
          {/* 文件列表/外部源：工具行与表格分体（PageToolbar 裸排 + DataTable 自带卡片），不再包一层 Card */}
          {section === 'files' ? (
            <WikiDocuments wikiId={wikiId} teamId={teamId} />
          ) : section === 'sources' ? (
            <WikiSources wikiId={wikiId} myRole={wiki.myRole} />
          ) : section === 'recall' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <WikiRecallTest wikiId={wikiId} teamId={teamId} />
            </Card>
          ) : section === 'mcp' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <WikiMcp wikiId={wikiId} teamId={teamId} canManage={isAdminPlus} />
            </Card>
          ) : (
            // 设置表单按设计系统 FormPage 同款 720 阅读宽度收口，输入框不铺满整屏
            <Card styles={{ body: { padding: spacing.lg, maxWidth: 720 } }}>
              {isAdminPlus ? (
                <>
                  <Form form={settingsForm} layout="vertical">
                  <Form.Item label={t('wiki.avatar')}>
                    <Space direction="vertical" size={spacing.xs}>
                      <AvatarUpload
                        src={avatarSrc}
                        fallback={<BookOutlined />}
                        shape="square"
                        size={96}
                        uploading={uploadingAvatar}
                        onSelect={handleAvatarFile}
                      />
                      <Text type="secondary" style={{ fontSize: 12 }}>{t('wiki.avatarHint')}</Text>
                    </Space>
                  </Form.Item>
                  <Form.Item
                    name="name"
                    label={t('wiki.name')}
                    rules={[
                      { required: true, message: t('wiki.namePlaceholder') },
                      { max: 50, message: `${t('wiki.name')} ≤ 50` },
                      { pattern: NAME_PATTERN, message: t('validation.nameChars') },
                    ]}
                  >
                    <Input placeholder={t('wiki.namePlaceholder')} maxLength={50} />
                  </Form.Item>
                  <Form.Item name="description" label={t('wiki.desc')} rules={[{ max: 255 }]}>
                    <Input.TextArea placeholder={t('wiki.descPlaceholder')} maxLength={255} rows={3} />
                  </Form.Item>
                    <Button type="primary" loading={saving} onClick={() => void handleSaveSettings()}>
                      {t('wiki.save')}
                    </Button>
                  </Form>
                  {modelOptionsLoaded && <Form
                    form={embeddingForm}
                    initialValues={{
                      embeddingModelId: wiki.embeddingModelId ?? '',
                      embeddingDimensions: wiki.embeddingDimensions ?? undefined,
                    }}
                    layout="vertical"
                    style={{ marginTop: spacing.lg }}
                  >
                  {modelOptionsFailed && (
                    <Alert type="error" showIcon message={t('wiki.embedding.optionsFailed')} style={{ marginBottom: spacing.md }} />
                  )}
                  {embeddingLocked && (
                    <Alert type="warning" showIcon message={t('wiki.embedding.locked')} style={{ marginBottom: spacing.md }} />
                  )}
                  {/* 向量化模型迁移：锁定后更换模型/维度的唯一通道，以表单当前选择为目标重建全部向量 */}
                  {embeddingLocked && (
                    <div style={{ marginBottom: spacing.md }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
                        <Popconfirm
                          title={t('wiki.migration.confirm')}
                          okText={t('common.ok')}
                          cancelText={t('common.cancel')}
                          onConfirm={() => void handleStartMigration()}
                          disabled={startingMigration || wikiMigrating || !migrationTargetChanged}
                        >
                          <Button danger loading={startingMigration} disabled={wikiMigrating || !migrationTargetChanged}>
                            {t('wiki.migration.start')}
                          </Button>
                        </Popconfirm>
                        {migrationTask && (wikiMigrating || migrationTask.state === 4 || migrationTask.state === 5) && (
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <Progress percent={migrationPercent} size="small" status={wikiMigrating ? 'active' : migrationTask.state === 5 ? 'exception' : 'success'} />
                            <Text type="secondary">{migrationTask.message}</Text>
                          </div>
                        )}
                      </div>
                      <Text type="secondary" style={{ fontSize: 12 }}>{t('wiki.migration.desc')}</Text>
                    </div>
                  )}
                  <Form.Item name="embeddingModelId" label={t('wiki.embedding.model')} rules={[{ required: true, message: t('wiki.embedding.modelRequired') }]}>
                    <Select
                      options={modelOptions.embeddingModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
                      loading={modelOptionsLoading}
                      disabled={embeddingDisabled}
                      showSearch
                      virtual={false}
                    />
                  </Form.Item>
                  <Form.Item
                    name="embeddingDimensions"
                    label={t('wiki.embedding.dimension')}
                    extra={t('wiki.embedding.dimensionExtra')}
                    normalize={(value) => (value === '' || value == null ? undefined : Number(value))}
                    rules={[
                      { required: true, message: t('wiki.embedding.dimensionRequired') },
                      { type: 'number', min: 1, max: EMBEDDING_DIMENSION_MAX, message: t('wiki.embedding.dimensionInvalid') },
                      { validator: (_, value) => Number.isInteger(value) ? Promise.resolve() : Promise.reject(new Error(t('wiki.embedding.dimensionInvalid'))) },
                    ]}
                  >
                    <AutoComplete
                      options={EMBEDDING_DIMENSION_OPTIONS.map((value) => ({ value: String(value), label: String(value) }))}
                      disabled={embeddingDisabled}
                      open={dimensionsOpen}
                      filterOption={(inputValue, option) => option?.value.includes(inputValue) ?? false}
                      onFocus={() => setDimensionsOpen(true)}
                      onChange={() => setDimensionsOpen(true)}
                      onSelect={(value) => {
                        embeddingForm.setFieldValue('embeddingDimensions', Number(value))
                        setDimensionsOpen(false)
                      }}
                    />
                  </Form.Item>
                    <Button
                      type="primary"
                      loading={embeddingSaving}
                      disabled={embeddingDisabled}
                      onClick={() => void handleSaveEmbeddingConfig()}
                    >
                      {t('wiki.embedding.save')}
                    </Button>
                  </Form>}
                  {modelOptionsLoaded && (
                    <Form
                      form={rerankForm}
                      initialValues={{ rerankModelId: wiki.rerankModelId ?? null }}
                      layout="vertical"
                      style={{ marginTop: spacing.lg }}
                    >
                      <Form.Item
                        name="rerankModelId"
                        label={t('wiki.rerank.model')}
                        extra={embeddingLocked ? t('wiki.rerank.lockedEditable') : t('wiki.rerank.extra')}
                      >
                        <Select
                          allowClear
                          placeholder={t('wiki.rerank.modelPlaceholder')}
                          options={modelOptions.rerankModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
                          loading={modelOptionsLoading}
                          disabled={rerankDisabled}
                          showSearch
                          virtual={false}
                          notFoundContent={t('wiki.rerank.empty')}
                        />
                      </Form.Item>
                      <Button
                        type="primary"
                        loading={rerankSaving}
                        disabled={rerankDisabled}
                        onClick={() => void handleSaveRerankModel()}
                      >
                        {t('wiki.rerank.save')}
                      </Button>
                    </Form>
                  )}
                </>
              ) : (
                <Space direction="vertical" size={spacing.md}>
                  <div>
                    <Text type="secondary">{t('wiki.name')}: </Text>
                    <Text strong>{wiki.name}</Text>
                  </div>
                  <div>
                    <Text type="secondary">{t('wiki.desc')}: </Text>
                    <Text strong>{wiki.description || '-'}</Text>
                  </div>
                  <Alert type="warning" showIcon message={t('wiki.noPermission')} />
                </Space>
              )}
            </Card>
          )}
        </Content>
      </Layout>
    </Page>
  )
}
