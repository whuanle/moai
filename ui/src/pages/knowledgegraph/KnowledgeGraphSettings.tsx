import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Alert, AutoComplete, Button, Descriptions, Form, Input, Popconfirm, Select, Space } from 'antd'
import { ClusterOutlined } from '@ant-design/icons'
import { AvatarUpload, Card, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { NAME_PATTERN } from '@/utils/validation'
import {
  deleteKnowledgeGraph,
  getKnowledgeGraphModelOptions,
  updateKnowledgeGraph,
  updateKnowledgeGraphEmbeddingConfig,
  updateKnowledgeGraphExtractModel,
  uploadKnowledgeGraphAvatar,
  getKnowledgeGraphSchema,
  updateKnowledgeGraphCanvasStyle,
  type KnowledgeGraphDetail as GraphDetail,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphModelOption,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface SettingsFormValues {
  name: string
  description?: string
}

interface EmbeddingFormValues {
  embeddingModelId: string
  embeddingDimensions: number
}

/** 画布样式规则的类型行（B8） */
interface StyleRow {
  typeId: string
  typeName: string
  properties: string[]
}

/** 向量维度预设档位：维度由用户手动设置，上限 2000（pgvector 建 hnsw 索引硬上限），与知识库设置页一致 */
const EMBEDDING_DIMENSION_OPTIONS = [256, 512, 1024, 2048]
const EMBEDDING_DIMENSION_MAX = 2000

interface KnowledgeGraphSettingsProps {
  graph: GraphDetail | null
  onChanged: () => void
}

export function KnowledgeGraphSettings({ graph, onChanged }: KnowledgeGraphSettingsProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [saving, setSaving] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [form] = Form.useForm<SettingsFormValues>()
  const [embeddingForm] = Form.useForm<EmbeddingFormValues>()
  const [embeddingSaving, setEmbeddingSaving] = useState(false)
  const [embeddingModels, setEmbeddingModels] = useState<KnowledgeGraphModelOption[]>([])
  const [conversationModels, setConversationModels] = useState<KnowledgeGraphModelOption[]>([])
  const [extractModelId, setExtractModelId] = useState<string | null>(null)
  const [extractSaving, setExtractSaving] = useState(false)
  /** 画布样式规则（B8）：类型行清单 + 当前 captions */
  const [styleRows, setStyleRows] = useState<StyleRow[]>([])
  const [captions, setCaptions] = useState<Record<string, string>>({})
  const [styleSaving, setStyleSaving] = useState(false)
  const [modelOptionsLoading, setModelOptionsLoading] = useState(false)
  const [modelOptionsLoaded, setModelOptionsLoaded] = useState(false)
  const [modelOptionsFailed, setModelOptionsFailed] = useState(false)
  const [dimensionsOpen, setDimensionsOpen] = useState(false)

  const isAdminPlus = graph?.myRole != null && graph.myRole !== ROLE_MEMBER
  const isConnected = graph?.mode === 'connected'
  const avatarSrc = graph?.avatarPath?.trim() ? resolveStorageUrl(graph.avatarPath) : undefined

  /** 校验并上传知识图谱头像；非法文件直接忽略 */
  const handleAvatarFile = (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('knowledgegraph.avatarTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('knowledgegraph.avatarSizeError'))
      return
    }
    if (!graph?.kgId) return
    setUploadingAvatar(true)
    uploadKnowledgeGraphAvatar(Number(graph.kgId), file)
      .then(() => feedback.success(t('knowledgegraph.avatarSuccess')))
      .then(() => onChanged())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
  }

  useEffect(() => {
    form.setFieldsValue({
      name: graph?.name ?? '',
      description: graph?.description ?? undefined,
    })
    embeddingForm.setFieldsValue({
      embeddingModelId: graph?.embeddingModelId ?? '',
      embeddingDimensions: graph?.embeddingDimensions ?? undefined,
    })
    setExtractModelId(graph?.extractModelId ?? null)
    setCaptions(graph?.canvasStyle ?? {})
  }, [graph, form, embeddingForm])

  // 样式规则行（B8）：托管图拉 schema 组装类型与属性清单
  useEffect(() => {
    if (isConnected || !isAdminPlus || !graph?.kgId) {
      setStyleRows([])
      return
    }
    let cancelled = false
    getKnowledgeGraphSchema(Number(graph.kgId))
      .then((schema) => {
        if (cancelled) return
        setStyleRows((schema.entityTypes ?? []).filter((x) => x.entityTypeId != null).map((x) => ({
          typeId: String(x.entityTypeId),
          typeName: x.name ?? '',
          properties: (x.properties ?? []).filter((p) => p.name).map((p) => p.name as string),
        })))
      })
      .catch(() => {
        if (!cancelled) setStyleRows([])
      })
    return () => {
      cancelled = true
    }
  }, [isConnected, isAdminPlus, graph?.kgId])

  // 向量化模型选项（仅托管图 + Admin+ 需要；模型渠道配置变化后重新进入页面可重取）
  useEffect(() => {
    const kgId = Number(graph?.kgId ?? 0)
    const teamId = Number(graph?.teamId ?? 0)
    if (!isAdminPlus || isConnected || !kgId || !teamId) return
    let cancelled = false
    setModelOptionsLoading(true)
    setModelOptionsLoaded(false)
    setModelOptionsFailed(false)
    getKnowledgeGraphModelOptions(teamId)
      .then((options) => {
        if (cancelled) return
        setEmbeddingModels(options.embeddingModels ?? [])
        setConversationModels(options.conversationModels ?? [])
      })
      .catch(() => {
        if (!cancelled) {
          setEmbeddingModels([])
          setModelOptionsFailed(true)
        }
      })
      .finally(() => {
        if (cancelled) return
        setModelOptionsLoading(false)
        setModelOptionsLoaded(true)
      })
    return () => {
      cancelled = true
    }
  }, [isAdminPlus, isConnected, graph])

  const handleSave = async () => {
    if (!graph?.kgId) return
    const values = await form.validateFields()
    setSaving(true)
    try {
      await updateKnowledgeGraph(Number(graph.kgId), { name: values.name, description: values.description })
      feedback.success(t('knowledgegraph.saveSuccess'))
      onChanged()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!graph?.kgId) return
    try {
      await deleteKnowledgeGraph(Number(graph.kgId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      navigate('/kg')
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSaveEmbeddingConfig = async () => {
    if (!graph?.kgId || modelOptionsFailed) return
    setEmbeddingSaving(true)
    try {
      const values = await embeddingForm.validateFields()
      await updateKnowledgeGraphEmbeddingConfig(Number(graph.kgId), values)
      feedback.success(t('knowledgegraph.embedding.saveSuccess'))
      onChanged()
    } catch (error) {
      // 错误已由全局请求中间件统一提示；409（配置被并发修改等）时回读最新值
      if (error instanceof Error && error.message.includes('409')) {
        onChanged()
      }
    } finally {
      setEmbeddingSaving(false)
    }
  }

  const handleSaveExtractModel = async () => {
    if (!graph?.kgId || modelOptionsFailed) return
    setExtractSaving(true)
    try {
      await updateKnowledgeGraphExtractModel(Number(graph.kgId), extractModelId || null)
      feedback.success(t('knowledgegraph.extractModel.saveSuccess'))
      onChanged()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setExtractSaving(false)
    }
  }

  const handleSaveCanvasStyle = async () => {
    if (!graph?.kgId) return
    setStyleSaving(true)
    try {
      const cleaned = Object.fromEntries(Object.entries(captions).filter(([, v]) => v !== ''))
      await updateKnowledgeGraphCanvasStyle(Number(graph.kgId), Object.keys(cleaned).length > 0 ? cleaned : null)
      feedback.success(t('knowledgegraph.style.saveSuccess'))
      onChanged()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setStyleSaving(false)
    }
  }

  if (graph === null) {
    return <Card loading styles={{ body: { padding: spacing.lg, maxWidth: 720, minHeight: 160 } }} />
  }

  return (
    <Card styles={{ body: { padding: spacing.lg, maxWidth: 720 } }}>
      <div style={{ fontWeight: 600, marginBottom: spacing.md }}>{t('knowledgegraph.settings.basicTitle')}</div>
      {isAdminPlus ? (
        <>
          <Form form={form} layout="vertical">
            <Form.Item label={t('knowledgegraph.avatar')}>
              <Space direction="vertical" size={spacing.xs}>
                <AvatarUpload
                  src={avatarSrc}
                  fallback={<ClusterOutlined />}
                  shape="square"
                  size={96}
                  uploading={uploadingAvatar}
                  onSelect={handleAvatarFile}
                />
                <span style={{ opacity: 0.65 }}>{t('knowledgegraph.avatarHint')}</span>
              </Space>
            </Form.Item>
            <Form.Item
              name="name"
              label={t('knowledgegraph.name')}
              rules={[
                { required: true, message: t('knowledgegraph.namePlaceholder') },
                { pattern: NAME_PATTERN, message: t('validation.nameChars') },
              ]}
            >
              <Input maxLength={50} placeholder={t('knowledgegraph.namePlaceholder')} />
            </Form.Item>
            <Form.Item name="description" label={t('knowledgegraph.desc')}>
              <Input.TextArea maxLength={255} rows={3} />
            </Form.Item>
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              {t('knowledgegraph.save')}
            </Button>
          </Form>
          {isConnected ? (
            <Alert
              type="info"
              showIcon
              message={t('knowledgegraph.embedding.connectedHint')}
              style={{ marginTop: spacing.lg }}
            />
          ) : (
            modelOptionsLoaded && (
              <Form
                form={embeddingForm}
                initialValues={{
                  embeddingModelId: graph.embeddingModelId ?? '',
                  embeddingDimensions: graph.embeddingDimensions ?? undefined,
                }}
                layout="vertical"
                style={{ marginTop: spacing.lg }}
              >
                <div style={{ fontWeight: 600, marginBottom: spacing.md }}>
                  {t('knowledgegraph.embedding.title')}
                </div>
                {modelOptionsFailed && (
                  <Alert
                    type="error"
                    showIcon
                    message={t('knowledgegraph.embedding.optionsFailed')}
                    style={{ marginBottom: spacing.md }}
                  />
                )}
                <Form.Item
                  name="embeddingModelId"
                  label={t('knowledgegraph.embedding.model')}
                  extra={t('knowledgegraph.embedding.extra')}
                  rules={[{ required: true, message: t('knowledgegraph.embedding.modelRequired') }]}
                >
                  <Select
                    options={embeddingModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
                    loading={modelOptionsLoading}
                    disabled={modelOptionsFailed}
                    showSearch
                    virtual={false}
                    notFoundContent={t('knowledgegraph.embedding.modelEmpty')}
                  />
                </Form.Item>
                <Form.Item
                  name="embeddingDimensions"
                  label={t('knowledgegraph.embedding.dimension')}
                  extra={t('knowledgegraph.embedding.dimensionExtra')}
                  normalize={(value) => (value === '' || value == null ? undefined : Number(value))}
                  rules={[
                    { required: true, message: t('knowledgegraph.embedding.dimensionRequired') },
                    { type: 'number', min: 1, max: EMBEDDING_DIMENSION_MAX, message: t('knowledgegraph.embedding.dimensionInvalid') },
                    { validator: (_, value) => (Number.isInteger(value) ? Promise.resolve() : Promise.reject(new Error(t('knowledgegraph.embedding.dimensionInvalid')))) },
                  ]}
                >
                  <AutoComplete
                    options={EMBEDDING_DIMENSION_OPTIONS.map((value) => ({ value: String(value), label: String(value) }))}
                    disabled={modelOptionsFailed}
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
                  disabled={modelOptionsFailed}
                  onClick={() => void handleSaveEmbeddingConfig()}
                >
                  {t('knowledgegraph.embedding.save')}
                </Button>
              </Form>
            )
          )}
          {!isConnected && modelOptionsLoaded && (
            <div style={{ marginTop: spacing.lg }}>
              <div style={{ fontWeight: 600, marginBottom: spacing.md }}>
                {t('knowledgegraph.extractModel.title')}
              </div>
              <Select
                style={{ width: '100%' }}
                value={extractModelId}
                onChange={setExtractModelId}
                allowClear
                placeholder={t('knowledgegraph.extractModel.placeholder')}
                loading={modelOptionsLoading}
                disabled={modelOptionsFailed}
                showSearch
                virtual={false}
                options={conversationModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
              />
              <div style={{ marginTop: spacing.sm, fontSize: 12, opacity: 0.65 }}>
                {t('knowledgegraph.extractModel.extra')}
              </div>
              <Button
                type="primary"
                style={{ marginTop: spacing.md }}
                loading={extractSaving}
                disabled={modelOptionsFailed}
                onClick={() => void handleSaveExtractModel()}
              >
                {t('knowledgegraph.extractModel.save')}
              </Button>
            </div>
          )}
          {!isConnected && isAdminPlus && styleRows.length > 0 && (
            <div style={{ marginTop: spacing.lg }}>
              <div style={{ fontWeight: 600, marginBottom: spacing.sm }}>
                {t('knowledgegraph.style.title')}
              </div>
              <div style={{ fontSize: 12, opacity: 0.65, marginBottom: spacing.sm }}>
                {t('knowledgegraph.style.extra')}
              </div>
              {styleRows.map((row) => (
                <div key={row.typeId} style={{ display: 'flex', alignItems: 'center', gap: spacing.md, marginBottom: spacing.sm }}>
                  <span style={{ minWidth: 120, fontSize: 13 }}>{row.typeName}</span>
                  <Select
                    style={{ flex: 1 }}
                    value={captions[row.typeId] ?? ''}
                    onChange={(value) => setCaptions((prev) => ({ ...prev, [row.typeId]: value }))}
                    options={[
                      { value: '', label: t('knowledgegraph.style.none') },
                      { value: '__type__', label: t('knowledgegraph.style.typeName') },
                      ...row.properties.map((p) => ({ value: p, label: p })),
                    ]}
                  />
                </div>
              ))}
              <Button
                type="primary"
                style={{ marginTop: spacing.sm }}
                loading={styleSaving}
                onClick={() => void handleSaveCanvasStyle()}
              >
                {t('knowledgegraph.style.save')}
              </Button>
            </div>
          )}
          <Descriptions column={1} style={{ marginTop: spacing.lg }}>
            {isConnected && (
              <Descriptions.Item label={t('knowledgegraph.database')}>{graph?.database || '-'}</Descriptions.Item>
            )}
            <Descriptions.Item label={t('knowledgegraph.settings.createTime')}>
              {formatDateTime(graph?.createTime)}
            </Descriptions.Item>
          </Descriptions>
          <Space style={{ marginTop: spacing.lg }}>
            <Popconfirm
              title={isConnected ? t('knowledgegraph.deleteConnectedConfirm') : t('knowledgegraph.deleteConfirm')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void handleDelete()}
            >
              <Button danger>{t('knowledgegraph.delete')}</Button>
            </Popconfirm>
          </Space>
        </>
      ) : (
        <Space direction="vertical" size={spacing.md}>
          <div>
            <span style={{ opacity: 0.65 }}>{t('knowledgegraph.name')}: </span>
            <span style={{ fontWeight: 600 }}>{graph?.name || '-'}</span>
          </div>
          <div>
            <span style={{ opacity: 0.65 }}>{t('knowledgegraph.desc')}: </span>
            <span style={{ fontWeight: 600 }}>{graph?.description || '-'}</span>
          </div>
          <Descriptions column={1}>
            {isConnected && (
              <Descriptions.Item label={t('knowledgegraph.database')}>{graph?.database || '-'}</Descriptions.Item>
            )}
            <Descriptions.Item label={t('knowledgegraph.settings.createTime')}>
              {formatDateTime(graph?.createTime)}
            </Descriptions.Item>
          </Descriptions>
          <Alert type="warning" showIcon message={t('knowledgegraph.noPermission')} />
        </Space>
      )}
    </Card>
  )
}
