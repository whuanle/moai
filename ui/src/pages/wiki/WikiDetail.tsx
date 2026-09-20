import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  BookOutlined,
  ExperimentOutlined,
  FileTextOutlined,
  SettingOutlined,
} from '@ant-design/icons'
import { Alert, AutoComplete, Button, Form, Input, Layout, Menu, Select, Space, Spin, Switch, Tag, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { AvatarUpload, Card, feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getWikiDetail, getWikiModelOptions, updateWiki, updateWikiEmbeddingConfig, updateWikiRerankModel, uploadWikiAvatar } from '@/api/wiki'
import { getTeamDetail } from '@/api/team'
import { resolveStorageUrl } from '@/utils/storage'
import { useAppStore } from '@/store/app'
import type { WikiWorkflowConfig } from '@/api/wiki'
import { WikiDocuments } from './WikiDocuments'
import { WikiWorkflowSettings } from './WikiWorkflowSettings'

const { Sider, Content } = Layout
const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const SECTION_KEYS = ['files', 'recall', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]

interface WikiDetail {
  wikiId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  isPublic?: boolean | null
  avatarPath?: string | null
  myRole?: number | null
  embeddingModelId?: string | null
  embeddingDimensions?: number | null
  isLock?: boolean | null
  rerankModelId?: string | null
  createTime?: string | null
  workflowConfig?: WikiWorkflowConfig | null
}

interface SettingsFormValues {
  name: string
  description?: string
  isPublic?: boolean
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
  const [settingsForm] = Form.useForm<SettingsFormValues>()
  const [embeddingForm] = Form.useForm<EmbeddingFormValues>()
  const [rerankForm] = Form.useForm<RerankFormValues>()

  const isAdminPlus = wiki?.myRole != null && wiki.myRole !== ROLE_MEMBER
  const embeddingLocked = wiki?.isLock === true
  const embeddingDisabled = embeddingLocked || modelOptionsLoading || modelOptionsFailed
  // 重排序模型与向量化配置解耦：知识库锁定后仍可修改
  const rerankDisabled = modelOptionsLoading || modelOptionsFailed

  const myTeams = useAppStore((state) => state.myTeams)
  const [teamName, setTeamName] = useState('')

  useEffect(() => {
    const fromStore = myTeams.find((team) => String(team.teamId) === String(teamId))?.name
    if (fromStore) {
      setTeamName(fromStore)
      return
    }
    // 直接进入（未经过侧边栏团队上下文）时，回退查询团队信息
    if (!Number.isFinite(teamId) || teamId <= 0) return
    getTeamDetail(teamId)
      .then((res) => setTeamName(res?.name ?? ''))
      .catch(() => undefined)
  }, [myTeams, teamId])

  const load = useCallback(async () => {
    if (!Number.isFinite(wikiId) || wikiId <= 0) return
    setLoading(true)
    try {
      const res = (await getWikiDetail(wikiId)) as unknown as WikiDetail
      setWiki(res)
      settingsForm.setFieldsValue({
        name: res.name ?? '',
        description: res.description ?? undefined,
        isPublic: res.isPublic ?? false,
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
        isPublic: res.isPublic ?? false,
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
      { key: 'recall', icon: <ExperimentOutlined />, label: t('wiki.menuRecall') },
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
        isPublic: values.isPublic,
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
    <Page
      breadcrumb={[
        { title: <Link to={`/team/${teamId}`}>{teamName || t('wiki.title')}</Link> },
        { title: <Link to={`/team/${teamId}/knowledge`}>{t('wiki.title')}</Link> },
        { title: wiki.name ?? '' },
      ]}
    >
      <Layout style={{ background: 'transparent', gap: spacing.md }}>
        <Sider width={200} style={{ background: 'transparent' }}>
          <Menu
            mode="inline"
            items={menuItems}
            selectedKeys={[section]}
            onClick={({ key }) => navigate(`/team/${teamId}/wiki/${wikiId}/${key}`)}
            style={{ borderRadius: spacing.sm }}
          />
        </Sider>
        <Content>
          {section === 'files' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <WikiDocuments wikiId={wikiId} teamId={teamId} />
            </Card>
          ) : section === 'recall' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <Alert type="info" showIcon message={t('wiki.recallPlaceholder')} />
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
                    ]}
                  >
                    <Input placeholder={t('wiki.namePlaceholder')} maxLength={50} />
                  </Form.Item>
                  <Form.Item name="description" label={t('wiki.desc')} rules={[{ max: 255 }]}>
                    <Input.TextArea placeholder={t('wiki.descPlaceholder')} maxLength={255} rows={3} />
                  </Form.Item>
                  <Form.Item name="isPublic" label={t('wiki.public')} valuePropName="checked">
                    <Switch checkedChildren={t('wiki.publicOn')} unCheckedChildren={t('wiki.publicOff')} />
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
                  {modelOptionsLoaded && (
                    <WikiWorkflowSettings
                      wikiId={wikiId}
                      config={wiki.workflowConfig}
                      conversationModels={modelOptions.conversationModels}
                      modelsLoading={modelOptionsLoading}
                      modelsFailed={modelOptionsFailed}
                      onSaved={() => void reload()}
                    />
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
                  <div>
                    <Text type="secondary">{t('wiki.public')}: </Text>
                    <Tag color={wiki.isPublic ? 'blue' : 'default'}>
                      {wiki.isPublic ? t('wiki.publicOn') : t('wiki.publicOff')}
                    </Tag>
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
