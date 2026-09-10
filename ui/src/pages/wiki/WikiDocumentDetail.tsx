import { useCallback, useEffect, useMemo, useState } from 'react'
import { FileTextOutlined, ScissorOutlined, ThunderboltOutlined } from '@ant-design/icons'
import Editor, { loader } from '@monaco-editor/react'
import { Alert, Button, Card, Checkbox, Col, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Tabs, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { aichannelApi } from '@/api/aichannel'
import {
  aiPartitionWikiDocument,
  extractWikiDocumentContent,
  generateWikiDocumentChunkMetadata,
  generateWikiDocumentChunksMetadata,
  getWikiDocumentContent,
  getWikiDocumentEmbedding,
  partitionWikiDocument,
  triggerDocumentEmbedding,
  type WikiDocumentPartitionOverlapUnit,
  type WikiDocumentPartitionSizeUnit,
  type WikiDocumentPartitionSplitMode,
  type WikiDocumentChunkMetadataItem,
  type WikiDocumentEmbeddingChunkItem,
  type WikiDocumentEmbeddingResult,
  type WikiMetadataGenerationStrategy,
} from '@/api/wiki'

loader.config({ paths: { vs: '/monaco/vs' } })

const { Text } = Typography

/** 默认切片/重叠，普通切割表单首选项。 */
const DEFAULT_CHUNK_OVERLAP = 1
const DEFAULT_SPLIT_MODE: WikiDocumentPartitionSplitMode = 'markdown'
const DEFAULT_OVERLAP_UNIT: WikiDocumentPartitionOverlapUnit = 'sentence'
const DEFAULT_SIZE_UNIT: WikiDocumentPartitionSizeUnit = 'token'
const DEFAULT_TOKEN_ENCODING = 'cl100k_base'
const DEFAULT_AI_PARTITION_PROMPT = '请按语义完整性切分文档，尽量保留标题、段落、列表、表格和代码块结构。每个切片只引用原文内容，不要改写、总结或补充。请严格返回 JSON 字符串数组，例如 ["第一段原文", "第二段原文"]，不要返回其他解释。'
const TOKEN_ENCODING_OPTIONS = ['r50k_base', 'p50k_base', 'p50k_edit', 'cl100k_base', 'o200k_base', 'o200k_harmony', 'claude'].map((value) => ({ value, label: value }))
const DEFAULT_METADATA_STRATEGY: WikiMetadataGenerationStrategy = 'outlineGeneration'

interface EmbedFormValues {
  isEmbedSourceText: boolean
  isEmbedMetadata: boolean
}

interface PartitionFormValues {
  splitMode: WikiDocumentPartitionSplitMode
  chunkSize: number
  chunkOverlap: number
  overlapUnit: WikiDocumentPartitionOverlapUnit
  sizeUnit: WikiDocumentPartitionSizeUnit
  tokenEncodingOrModel?: string
}

interface AiPartitionFormValues {
  aiModelId?: string
  promptTemplate?: string
}

function getRecommendedChunkRange(dimensions: number | null | undefined) {
  const value = dimensions ?? 0
  if (value <= 384) return { min: 200, max: 450, recommended: 320 }
  if (value <= 768) return { min: 350, max: 700, recommended: 512 }
  if (value <= 1024) return { min: 500, max: 900, recommended: 700 }
  if (value <= 1536) return { min: 700, max: 1200, recommended: 900 }
  return { min: 900, max: 1500, recommended: 1100 }
}

function getRecommendedPartitionValues(dimensions: number | null | undefined): PartitionFormValues {
  return {
    splitMode: DEFAULT_SPLIT_MODE,
    chunkSize: getRecommendedChunkRange(dimensions).recommended,
    chunkOverlap: DEFAULT_CHUNK_OVERLAP,
    overlapUnit: DEFAULT_OVERLAP_UNIT,
    sizeUnit: DEFAULT_SIZE_UNIT,
    tokenEncodingOrModel: DEFAULT_TOKEN_ENCODING,
  }
}

function metadataTypeKey(type: number | null | undefined): string {
  switch (type) {
    case 1:
      return 'wiki.doc.metadataTypeOutline'
    case 2:
      return 'wiki.doc.metadataTypeQuestion'
    case 3:
      return 'wiki.doc.metadataTypeKeyword'
    case 4:
      return 'wiki.doc.metadataTypeSummary'
    case 5:
      return 'wiki.doc.metadataTypeAggregated'
    default:
      return 'wiki.doc.metadataTypeUnknown'
  }
}

/** 文档操作页面：内容提取 → 文档切割（普通/AI）→ 切片预览 → 向量化 */
export function WikiDocumentDetail() {
  const { t } = useTranslation()
  const themeKey = useAppStore((state) => state.themeKey)
  const params = useParams<{ teamId: string; wikiId: string; documentId: string }>()
  const wikiId = Number(params.wikiId)
  const documentId = Number(params.documentId)

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<WikiDocumentEmbeddingResult | null>(null)
  const [models, setModels] = useState<{ id: string; name: string; kind: string }[]>([])
  const [extracting, setExtracting] = useState(false)
  const [partitioning, setPartitioning] = useState(false)
  const [aiPartitioning, setAiPartitioning] = useState(false)
  const [triggering, setTriggering] = useState(false)
  const [metadataGenerateModelId, setMetadataGenerateModelId] = useState<string>()
  const [metadataGenerationStrategy, setMetadataGenerationStrategy] = useState<WikiMetadataGenerationStrategy>(DEFAULT_METADATA_STRATEGY)
  const [batchGeneratingMetadata, setBatchGeneratingMetadata] = useState(false)
  const [generatingMetadataChunkIds, setGeneratingMetadataChunkIds] = useState<Set<string>>(new Set())
  const [previewChunkId, setPreviewChunkId] = useState<string>()
  const [contentModalOpen, setContentModalOpen] = useState(false)
  const [fullContent, setFullContent] = useState<string | null>(null)
  const [loadingFull, setLoadingFull] = useState(false)
  const [expandedChunks, setExpandedChunks] = useState<Set<string>>(new Set())
  const [embedForm] = Form.useForm<EmbedFormValues>()
  const [partitionForm] = Form.useForm<PartitionFormValues>()
  const [aiPartitionForm] = Form.useForm<AiPartitionFormValues>()

  // 非成员访问会被后端 Handler 返回 404，因此能加载到 detail 即为团队成员。
  const isMember = detail != null
  const isContentExtracted = !!detail?.isContentExtracted
  const contentPreview = detail?.content ?? ''
  // detail 只回传截断预览；预览长度小于全文长度即说明内容被截断，需「全部加载」取全文。
  const contentLength = detail?.contentLength ?? 0
  const contentPreviewLength = detail?.contentPreviewLength ?? contentPreview.length
  const contentTruncated = contentLength > contentPreviewLength
  const shownContent = fullContent ?? contentPreview
  const chunkCount = detail?.items?.length ?? 0
  const canEmbed = isMember && !!detail?.embeddingModelId && isContentExtracted && chunkCount > 0
  const selectedSizeUnit = Form.useWatch('sizeUnit', partitionForm) ?? DEFAULT_SIZE_UNIT
  const selectedOverlapUnit = Form.useWatch('overlapUnit', partitionForm) ?? DEFAULT_OVERLAP_UNIT
  const selectedSplitMode = Form.useWatch('splitMode', partitionForm) ?? DEFAULT_SPLIT_MODE
  const isOverlapUnitLocked = selectedSplitMode === 'sentence' || selectedSplitMode === 'paragraph'
  const recommendedChunkRange = useMemo(() => getRecommendedChunkRange(detail?.embeddingDimensions), [detail?.embeddingDimensions])
  const recommendedPartitionValues = useMemo(() => getRecommendedPartitionValues(detail?.embeddingDimensions), [detail?.embeddingDimensions])
  const partitionChunkSizeLabel = selectedSizeUnit === 'token' ? t('wiki.doc.partitionChunkSizeToken') : t('wiki.doc.partitionChunkSizeCharacter')
  const partitionChunkSizePlaceholder = selectedSizeUnit === 'token' ? t('wiki.doc.partitionChunkSizeTokenPlaceholder') : t('wiki.doc.partitionChunkSizeCharacterPlaceholder')
  const partitionChunkOverlapLabel =
    selectedOverlapUnit === 'sentence'
      ? t('wiki.doc.partitionChunkOverlapSentence')
      : selectedOverlapUnit === 'paragraph'
        ? t('wiki.doc.partitionChunkOverlapParagraph')
        : t('wiki.doc.partitionChunkOverlapCharacter')
  const partitionChunkOverlapPlaceholder =
    selectedOverlapUnit === 'sentence'
      ? t('wiki.doc.partitionChunkOverlapSentencePlaceholder')
      : selectedOverlapUnit === 'paragraph'
        ? t('wiki.doc.partitionChunkOverlapParagraphPlaceholder')
        : t('wiki.doc.partitionChunkOverlapCharacterPlaceholder')
  const partitionChunkOverlapInvalid = selectedOverlapUnit === 'character' ? t('wiki.doc.partitionChunkOverlapCharacterInvalid') : t('wiki.doc.partitionChunkOverlapUnitInvalid')

  // 预览文本变化（重提取/切换文档）时，重置已惰性加载的全文。
  useEffect(() => {
    setFullContent(null)
    setContentModalOpen(false)
  }, [contentPreview])

  const load = useCallback(async (showPageLoading = true) => {
    if (!Number.isFinite(wikiId) || wikiId <= 0 || !Number.isFinite(documentId) || documentId <= 0) return
    if (showPageLoading) setLoading(true)
    try {
      const detailRes = await getWikiDocumentEmbedding(wikiId, documentId)
      setDetail(detailRes ?? null)
      if (!detailRes) return
      const previousChunkSize = detailRes.chunkSize ?? 0
      const previousChunkOverlap = detailRes.chunkOverlap ?? 0
      const hasPreviousPartition = previousChunkSize > 0
      const recommendedValues = getRecommendedPartitionValues(detailRes.embeddingDimensions)
      partitionForm.setFieldsValue(
        hasPreviousPartition
          ? {
              splitMode: (detailRes.splitMode as WikiDocumentPartitionSplitMode | null | undefined) ?? DEFAULT_SPLIT_MODE,
              chunkSize: previousChunkSize,
              chunkOverlap: previousChunkOverlap,
              overlapUnit: (detailRes.overlapUnit as WikiDocumentPartitionOverlapUnit | null | undefined) ?? 'character',
              sizeUnit: (detailRes.sizeUnit as WikiDocumentPartitionSizeUnit | null | undefined) ?? 'character',
              tokenEncodingOrModel: detailRes.tokenEncodingOrModel ?? DEFAULT_TOKEN_ENCODING,
            }
          : recommendedValues,
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      if (showPageLoading) setLoading(false)
    }
  }, [wikiId, documentId, partitionForm])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    const teamId = Number(params.teamId)
    if (!Number.isFinite(teamId) || teamId <= 0) return

    aichannelApi
      .getModels(undefined, teamId)
      .then((items) =>
        setModels(
          (items ?? [])
            .filter((m) => m.enabled !== false)
            .map((m) => ({ id: String(m.id ?? ''), name: m.name ?? m.modelId ?? '', kind: m.modelKind ?? '' })),
        ),
      )
      .catch(() => undefined)
  }, [params.teamId])

  const conversationModelOptions = useMemo(
    () => models.filter((m) => m.kind === 'conversation').map((m) => ({ value: m.id, label: m.name })),
    [models],
  )
  const metadataStrategyOptions = useMemo(
    () => [
      { value: 'outlineGeneration' as const, label: t('wiki.doc.metadataStrategyOutline') },
      { value: 'questionGeneration' as const, label: t('wiki.doc.metadataStrategyQuestion') },
      { value: 'keywordSummaryFusion' as const, label: t('wiki.doc.metadataStrategyKeywordSummary') },
      { value: 'semanticAggregation' as const, label: t('wiki.doc.metadataStrategySemantic') },
    ],
    [t],
  )

  const handleExtract = async () => {
    setExtracting(true)
    try {
      await extractWikiDocumentContent(wikiId, documentId)
      feedback.success(t('wiki.doc.extractSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setExtracting(false)
    }
  }

  const handlePartition = async () => {
    const values = await partitionForm.validateFields()
    setPartitioning(true)
    try {
      await partitionWikiDocument(wikiId, documentId, {
        splitMode: values.splitMode,
        chunkSize: values.chunkSize,
        chunkOverlap: values.chunkOverlap,
        overlapUnit: values.overlapUnit,
        sizeUnit: values.sizeUnit,
        tokenEncodingOrModel: values.tokenEncodingOrModel,
      })
      feedback.success(t('wiki.doc.partitionSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPartitioning(false)
    }
  }

  const handleApplyRecommendedPartition = () => {
    partitionForm.setFieldsValue(recommendedPartitionValues)
  }

  const handleSplitModeChange = (value: WikiDocumentPartitionSplitMode) => {
    if (value === 'sentence') {
      partitionForm.setFieldValue('overlapUnit', 'sentence')
    } else if (value === 'paragraph') {
      partitionForm.setFieldValue('overlapUnit', 'paragraph')
    } else if (value === 'fixedSize') {
      partitionForm.setFieldValue('overlapUnit', 'character')
    }
  }

  const handleAiPartition = async () => {
    const values = await aiPartitionForm.validateFields()
    setAiPartitioning(true)
    try {
      await aiPartitionWikiDocument(wikiId, documentId, {
        aiModelId: values.aiModelId ?? '',
        promptTemplate: values.promptTemplate ?? null,
      })
      feedback.success(t('wiki.doc.partitionSuccess'))
      void load()
    } catch (error) {
      feedback.handleError(error)
    } finally {
      setAiPartitioning(false)
    }
  }

  const handleTrigger = async () => {
    if (!canEmbed) return
    const values = await embedForm.validateFields()
    if (!values.isEmbedSourceText && !values.isEmbedMetadata) {
      feedback.warning(t('wiki.embedding.selectAtLeastOne'))
      return
    }
    setTriggering(true)
    try {
      await triggerDocumentEmbedding(wikiId, documentId, {
        isEmbedSourceText: values.isEmbedSourceText,
        isEmbedMetadata: values.isEmbedMetadata,
      })
      feedback.success(t('wiki.embedding.triggerSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTriggering(false)
    }
  }

  const handleGenerateChunkMetadata = async (chunkId: string) => {
    if (!metadataGenerateModelId) {
      feedback.warning(t('wiki.doc.metadataGenerateModelRequired'))
      return
    }
    setGeneratingMetadataChunkIds((prev) => new Set(prev).add(String(chunkId)))
    try {
      const generatedCount = await generateWikiDocumentChunkMetadata(wikiId, documentId, chunkId, metadataGenerateModelId, true, metadataGenerationStrategy)
      if (generatedCount <= 0) {
        feedback.warning(t('wiki.doc.metadataGenerateEmpty'))
        return
      }
      feedback.success(t('wiki.doc.metadataGenerateSuccess'))
      setPreviewChunkId(String(chunkId))
      void load(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setGeneratingMetadataChunkIds((prev) => {
        const next = new Set(prev)
        next.delete(String(chunkId))
        return next
      })
    }
  }

  const handleGenerateAllChunkMetadata = async () => {
    if (!metadataGenerateModelId) {
      feedback.warning(t('wiki.doc.metadataGenerateModelRequired'))
      return
    }
    if (chunkCount === 0) return
    setBatchGeneratingMetadata(true)
    try {
      const generatedCount = await generateWikiDocumentChunksMetadata(wikiId, documentId, metadataGenerateModelId, undefined, metadataGenerationStrategy)
      if (generatedCount <= 0) {
        feedback.warning(t('wiki.doc.metadataGenerateEmpty'))
        return
      }
      feedback.success(t('wiki.doc.metadataGenerateBatchSuccess'))
      void load(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBatchGeneratingMetadata(false)
    }
  }

  const handleLoadFullContent = async () => {
    if (fullContent || loadingFull) return
    setLoadingFull(true)
    try {
      const full = await getWikiDocumentContent(wikiId, documentId)
      setFullContent(full)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoadingFull(false)
    }
  }

  // 打开内容模态窗：预览被截断时自动惰性加载全文。
  const handleOpenContentModal = () => {
    setContentModalOpen(true)
    if (contentTruncated && fullContent == null && !loadingFull) void handleLoadFullContent()
  }

  const toggleExpand = (chunkId: string) => {
    setExpandedChunks((prev) => {
      const next = new Set(prev)
      if (next.has(chunkId)) next.delete(chunkId)
      else next.add(chunkId)
      return next
    })
  }

  const previewChunk = (detail?.items ?? []).find((item) => String(item.chunkId ?? '') === previewChunkId)

  const openMetadataPreview = (chunkId: string) => {
    setPreviewChunkId(chunkId)
  }

  if (loading) {
    return (
      <Page
        breadcrumb={[
          { title: <Link to={`/team/${params.teamId}/wiki/${wikiId}`}>{t('wiki.menuFiles')}</Link> },
          { title: t('wiki.doc.operationTitle') },
        ]}
      >
        <div style={{ padding: '48px 0', textAlign: 'center' }}>
          <Spin />
        </div>
      </Page>
    )
  }

  if (!detail) {
    return (
      <Page
        breadcrumb={[
          { title: <Link to={`/team/${params.teamId}/wiki/${wikiId}`}>{t('wiki.menuFiles')}</Link> },
          { title: t('wiki.doc.operationTitle') },
        ]}
      >
        <Alert type="info" showIcon message={t('wiki.doc.operationPlaceholder')} />
      </Page>
    )
  }

  return (
    <Page
      breadcrumb={[
        { title: <Link to={`/team/${params.teamId}/wiki/${wikiId}`}>{t('wiki.menuFiles')}</Link> },
        { title: t('wiki.doc.operationTitle') },
      ]}
    >
      <Space direction="vertical" size={spacing.lg} style={{ width: '100%' }}>
        <Card
          title={t('wiki.doc.extractTitle')}
          styles={{ body: { padding: spacing.lg } }}
          extra={
            <Space wrap>
              <Text strong>{detail.fileName ?? '-'}</Text>
              <Tag color={isContentExtracted ? 'green' : 'orange'}>
                {isContentExtracted ? t('wiki.doc.extractStatusDone', { length: detail.contentLength ?? 0 }) : t('wiki.doc.extractStatusNone')}
              </Tag>
              {/* 上传后已自动提取内容；此处按钮作为自动提取失败的兜底重试入口 */}
              <Button
                type={isContentExtracted ? 'default' : 'primary'}
                icon={<FileTextOutlined />}
                loading={extracting}
                onClick={() => void handleExtract()}
              >
                {isContentExtracted ? t('wiki.doc.extractReextract') : t('wiki.doc.extractAction')}
              </Button>
              {isContentExtracted && (
                <Button type="primary" icon={<FileTextOutlined />} onClick={handleOpenContentModal}>
                  {t('wiki.doc.contentView')}
                </Button>
              )}
            </Space>
          }
        >
          {!isMember ? (
            <Alert type="info" showIcon message={t('wiki.embedding.noPermissionTip')} />
          ) : !isContentExtracted ? (
            <Alert type="warning" showIcon message={t('wiki.doc.extractHint')} />
          ) : (
            <div
              style={{
                height: 300,
                overflowY: 'auto',
                padding: spacing.md,
                border: '1px solid rgba(5, 5, 5, 0.06)',
                borderRadius: 6,
                background: 'rgba(0, 0, 0, 0.02)',
              }}
            >
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                {shownContent || '-'}
              </Typography.Paragraph>
            </div>
          )}
        </Card>

        {isMember && (
          <Card title={t('wiki.doc.partitionTitle')} styles={{ body: { padding: spacing.lg } }}>
            {!isContentExtracted ? (
              <Alert type="warning" showIcon message={t('wiki.doc.extractRequireFirst')} />
            ) : (
              <Tabs
                items={[
                  {
                    key: 'normal',
                    label: t('wiki.doc.partitionTabNormal'),
                    children: (
                      <Form
                        form={partitionForm}
                        layout="vertical"
                        onFinish={() => void handlePartition()}
                        initialValues={getRecommendedPartitionValues(detail.embeddingDimensions)}
                        style={{ maxWidth: 960 }}
                      >
                        <div style={{ border: '1px solid rgba(5, 5, 5, 0.06)', borderRadius: 6, padding: spacing.md, marginBottom: spacing.md }}>
                          <Space direction="vertical" size={spacing.xs} style={{ width: '100%' }}>
                            <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
                              <Typography.Text strong>{t('wiki.doc.partitionConfigTitle')}</Typography.Text>
                              <Button size="small" onClick={handleApplyRecommendedPartition}>
                                {t('wiki.doc.partitionApplyRecommended')}
                              </Button>
                            </Space>
                            <Typography.Text type="secondary">
                              {t('wiki.doc.partitionRecommendedHint', {
                                dimensions: detail.embeddingDimensions ?? 0,
                                min: recommendedChunkRange.min,
                                max: recommendedChunkRange.max,
                                recommended: recommendedChunkRange.recommended,
                              })}
                            </Typography.Text>
                          </Space>
                          <Row gutter={spacing.md} style={{ marginTop: spacing.md }}>
                            <Col xs={24} md={8}>
                              <Form.Item name="splitMode" label={t('wiki.doc.partitionSplitMode')} rules={[{ required: true, message: t('wiki.doc.partitionSplitModeRequired') }]}>
                                <Select
                                  onChange={handleSplitModeChange}
                                  options={[
                                    { value: 'markdown', label: t('wiki.doc.partitionSplitModeMarkdown') },
                                    { value: 'recursive', label: t('wiki.doc.partitionSplitModeRecursive') },
                                    { value: 'fixedSize', label: t('wiki.doc.partitionSplitModeFixedSize') },
                                    { value: 'sentence', label: t('wiki.doc.partitionSplitModeSentence') },
                                    { value: 'paragraph', label: t('wiki.doc.partitionSplitModeParagraph') },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={8}>
                              <Form.Item name="sizeUnit" label={t('wiki.doc.partitionSizeUnit')} rules={[{ required: true, message: t('wiki.doc.partitionSizeUnitRequired') }]}>
                                <Select
                                  options={[
                                    { value: 'character', label: t('wiki.doc.partitionSizeUnitCharacter') },
                                    { value: 'token', label: t('wiki.doc.partitionSizeUnitToken') },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={8}>
                              <Form.Item name="overlapUnit" label={t('wiki.doc.partitionOverlapUnit')} rules={[{ required: true, message: t('wiki.doc.partitionOverlapUnitRequired') }]}>
                                <Select
                                  disabled={isOverlapUnitLocked}
                                  options={[
                                    { value: 'character', label: t('wiki.doc.partitionOverlapUnitCharacter') },
                                    { value: 'sentence', label: t('wiki.doc.partitionOverlapUnitSentence') },
                                    { value: 'paragraph', label: t('wiki.doc.partitionOverlapUnitParagraph') },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={12}>
                              <Form.Item
                                name="chunkSize"
                                label={partitionChunkSizeLabel}
                                rules={[
                                  { required: true, message: t('wiki.doc.partitionChunkSizeRequired') },
                                  { type: 'number', min: 1, max: 8192, message: t('wiki.doc.partitionChunkSizeInvalid') },
                                  { validator: (_, value) => (Number.isInteger(value) ? Promise.resolve() : Promise.reject(new Error(t('wiki.doc.partitionChunkSizeInvalid')))) },
                                ]}
                              >
                                <InputNumber min={1} max={8192} precision={0} style={{ width: '100%' }} placeholder={partitionChunkSizePlaceholder} />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={12}>
                              <Form.Item
                                name="chunkOverlap"
                                label={partitionChunkOverlapLabel}
                                dependencies={['chunkSize', 'overlapUnit']}
                                rules={[
                                  { required: true, message: t('wiki.doc.partitionChunkOverlapRequired') },
                                  { type: 'number', min: 0, max: 8192, message: partitionChunkOverlapInvalid },
                                  { validator: (_, value) => (Number.isInteger(value) ? Promise.resolve() : Promise.reject(new Error(partitionChunkOverlapInvalid))) },
                                  ({ getFieldValue }) => ({
                                    validator(_, value) {
                                      const cs = getFieldValue('chunkSize')
                                      const ou = getFieldValue('overlapUnit')
                                      if (ou === 'character' && typeof cs === 'number' && typeof value === 'number' && value >= cs) {
                                        return Promise.reject(new Error(t('wiki.doc.partitionChunkOverlapCharacterInvalid')))
                                      }
                                      return Promise.resolve()
                                    },
                                  }),
                                ]}
                              >
                                <InputNumber min={0} max={8192} precision={0} style={{ width: '100%' }} placeholder={partitionChunkOverlapPlaceholder} />
                              </Form.Item>
                            </Col>
                            {selectedSizeUnit === 'token' && (
                              <Col xs={24}>
                                <Form.Item name="tokenEncodingOrModel" label={t('wiki.doc.partitionTokenEncoding')} extra={t('wiki.doc.partitionTokenEncodingExtra')} rules={[{ max: 64, message: t('wiki.doc.partitionTokenEncodingInvalid') }]}>
                                  <Select options={TOKEN_ENCODING_OPTIONS} placeholder={t('wiki.doc.partitionTokenEncodingPlaceholder')} />
                                </Form.Item>
                              </Col>
                            )}
                          </Row>
                        </div>
                        <Form.Item>
                          <Button type="primary" icon={<ScissorOutlined />} loading={partitioning} htmlType="submit">
                            {t('wiki.doc.partitionNormalAction')}
                          </Button>
                        </Form.Item>
                      </Form>
                    ),
                  },
                  {
                    key: 'ai',
                    label: t('wiki.doc.partitionTabAi'),
                    children: (
                      <Form form={aiPartitionForm} layout="vertical" onFinish={() => void handleAiPartition()} initialValues={{ promptTemplate: DEFAULT_AI_PARTITION_PROMPT }} style={{ maxWidth: 720 }}>
                        <Form.Item
                          name="aiModelId"
                          label={t('wiki.doc.partitionAiModel')}
                          rules={[{ required: true, message: t('wiki.doc.partitionAiModelPlaceholder') }]}
                        >
                          <Select options={conversationModelOptions} placeholder={t('wiki.doc.partitionAiModelPlaceholder')} showSearch optionFilterProp="label" />
                        </Form.Item>
                        <Form.Item name="promptTemplate" label={t('wiki.doc.partitionPrompt')} extra={t('wiki.doc.partitionPromptExtra')}>
                          <Input.TextArea rows={6} placeholder={t('wiki.doc.partitionPromptPlaceholder')} />
                        </Form.Item>
                        <Form.Item>
                          <Button type="primary" icon={<ScissorOutlined />} loading={aiPartitioning} htmlType="submit">
                            {t('wiki.doc.partitionAiAction')}
                          </Button>
                        </Form.Item>
                      </Form>
                    ),
                  },
                ]}
              />
            )}
          </Card>
        )}

        <Card
          title={`${t('wiki.doc.previewTitle')}${chunkCount > 0 ? ` (${chunkCount})` : ''}`}
          styles={{ body: { padding: spacing.lg, minHeight: 0 } }}
          extra={
            chunkCount > 0 ? (
              <Space wrap>
                <Select
                  value={metadataGenerateModelId}
                  onChange={setMetadataGenerateModelId}
                  options={conversationModelOptions}
                  placeholder={t('wiki.doc.metadataGenerateModelPlaceholder')}
                  style={{ minWidth: 240 }}
                  showSearch
                  optionFilterProp="label"
                />
                <Select
                  value={metadataGenerationStrategy}
                  onChange={setMetadataGenerationStrategy}
                  options={metadataStrategyOptions}
                  style={{ minWidth: 180 }}
                />
                <Button type="primary" icon={<ThunderboltOutlined />} loading={batchGeneratingMetadata} onClick={() => void handleGenerateAllChunkMetadata()}>
                  {t('wiki.doc.metadataGenerateAll')}
                </Button>
              </Space>
            ) : null
          }
        >
          {chunkCount === 0 ? (
            <Alert type="info" showIcon message={t('wiki.doc.previewEmpty')} />
          ) : (
            <div data-testid="wiki-chunk-card-list" style={{ maxHeight: 'calc(100vh - 360px)', minHeight: 280, overflowY: 'auto', overflowX: 'hidden', paddingRight: spacing.xs }}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))', gap: spacing.md }}>
                {(detail.items ?? []).map((item) => (
                  <ChunkCard
                    key={String(item.chunkId ?? '')}
                    item={item}
                    expanded={expandedChunks.has(String(item.chunkId ?? ''))}
                    onToggle={() => toggleExpand(String(item.chunkId ?? ''))}
                    onGenerateMetadata={() => openMetadataPreview(String(item.chunkId ?? ''))}
                    onPreviewMetadata={() => openMetadataPreview(String(item.chunkId ?? ''))}
                    generatingMetadata={generatingMetadataChunkIds.has(String(item.chunkId ?? ''))}
                    metadataGenerateDisabled={!item.chunkId}
                  />
                ))}
              </div>
            </div>
          )}
        </Card>

        {isMember && (
          <Card title={t('wiki.embedding.actionTitle')} styles={{ body: { padding: spacing.lg } }}>
            {!detail.embeddingModelId ? (
              <Alert type="warning" showIcon message={t('wiki.embedding.optionsFailed')} />
            ) : !isContentExtracted || chunkCount === 0 ? (
              <Alert type="warning" showIcon message={t('wiki.doc.extractRequireFirst')} />
            ) : (
              <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
                <Space wrap size={spacing.sm}>
                  <Tag>{`${t('wiki.embedding.colEmbeddingModel')} ${detail.embeddingModelName || t('wiki.embedding.notConfigured')}`}</Tag>
                  <Tag>{`${t('wiki.embedding.colDimensions')} ${detail.embeddingDimensions ?? '-'}`}</Tag>
                  <Tag color={detail.isEmbedding ? 'green' : 'orange'}>{detail.isEmbedding ? t('wiki.doc.embeddingDone') : t('wiki.doc.embeddingNone')}</Tag>
                  <Tag>{`${t('wiki.embedding.colEmbeddingCount')} ${detail.embeddingCount ?? 0}`}</Tag>
                </Space>
                <Form form={embedForm} initialValues={{ isEmbedSourceText: true, isEmbedMetadata: true }}>
                  <Space size={spacing.lg} wrap>
                    <Form.Item name="isEmbedSourceText" valuePropName="checked" noStyle>
                      <Checkbox>{t('wiki.embedding.embedSourceText')}</Checkbox>
                    </Form.Item>
                    <Form.Item name="isEmbedMetadata" valuePropName="checked" noStyle>
                      <Checkbox>{t('wiki.embedding.embedMetadata')}</Checkbox>
                    </Form.Item>
                  </Space>
                </Form>
                <Space wrap>
                  <Button type="primary" icon={<ThunderboltOutlined />} loading={triggering} disabled={!canEmbed} onClick={() => void handleTrigger()}>
                    {t('wiki.embedding.trigger')}
                  </Button>
                  <Text type="secondary">{t('wiki.embedding.triggerTip')}</Text>
                </Space>
              </Space>
            )}
          </Card>
        )}
      </Space>

      {/* 文档内容查看模态窗：完整内容在此滚动查看，不占用页面高度 */}
      <Modal
        open={contentModalOpen}
        title={`${t('wiki.doc.extractTitle')} - ${detail.fileName ?? ''}`}
        width="60vw"
        maskClosable={false}
        style={{ top: '10vh' }}
        styles={{ content: { height: '80vh', display: 'flex', flexDirection: 'column' }, body: { flex: 1, minHeight: 0, overflow: 'hidden' } }}
        onCancel={() => setContentModalOpen(false)}
        footer={
          <Button type="primary" onClick={() => setContentModalOpen(false)}>
            {t('wiki.doc.contentClose')}
          </Button>
        }
      >
        {loadingFull ? (
          <div style={{ padding: '48px 0', textAlign: 'center' }}>
            <Spin />
          </div>
        ) : (
          <Editor
            height="100%"
            width="100%"
            language="plaintext"
            theme={themeKey === 'dark' ? 'vs-dark' : 'light'}
            value={shownContent || '-'}
            loading={<Spin />}
            options={{
              automaticLayout: true,
              fontSize: 13,
              minimap: { enabled: false },
              readOnly: true,
              scrollBeyondLastLine: false,
              wordWrap: 'on',
            }}
          />
        )}
      </Modal>

      <Modal
        open={previewChunk != null}
        title={previewChunk ? `${t('wiki.doc.metadataGenerate')} - #${previewChunk.sliceOrder ?? '-'}` : t('wiki.doc.metadataGenerate')}
        width={760}
        maskClosable={false}
        style={{ top: '6vh' }}
        styles={{ content: { height: '88vh', display: 'flex', flexDirection: 'column' }, body: { flex: 1, minHeight: 0, overflow: 'hidden', paddingTop: spacing.md }, footer: { display: 'none' } }}
        onCancel={() => setPreviewChunkId(undefined)}
        footer={null}
      >
        {previewChunk && (
          <div style={{ height: '100%', minHeight: 0, display: 'flex', flexDirection: 'column', gap: spacing.md }}>
            <div style={{ flexShrink: 0, maxHeight: 140, overflowY: 'auto', padding: spacing.md, border: '1px solid rgba(5, 5, 5, 0.06)', borderRadius: 6, background: 'rgba(0, 0, 0, 0.02)' }}>
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                {previewChunk.sliceContent || '-'}
              </Typography.Paragraph>
            </div>

            <div style={{ flexShrink: 0, padding: spacing.md, border: '1px solid rgba(22, 119, 255, 0.25)', borderRadius: 6, background: 'rgba(22, 119, 255, 0.06)' }}>
              <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
                <Typography.Text strong>{t('wiki.doc.metadataGenerateStrategy')}</Typography.Text>
                <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(180px, 220px)', gap: spacing.sm }}>
                  <Select
                    value={metadataGenerateModelId}
                    onChange={setMetadataGenerateModelId}
                    options={conversationModelOptions}
                    placeholder={t('wiki.doc.metadataGenerateModelPlaceholder')}
                    style={{ width: '100%' }}
                    showSearch
                    optionFilterProp="label"
                  />
                  <Select
                    value={metadataGenerationStrategy}
                    onChange={setMetadataGenerationStrategy}
                    options={metadataStrategyOptions}
                    style={{ width: '100%' }}
                  />
                </div>
                <Space style={{ width: '100%', justifyContent: 'flex-end' }}>
                  <Button onClick={() => setPreviewChunkId(undefined)}>{t('wiki.doc.contentClose')}</Button>
                  <Button
                    type="primary"
                    icon={<ThunderboltOutlined />}
                    loading={previewChunk ? generatingMetadataChunkIds.has(String(previewChunk.chunkId ?? '')) : false}
                    disabled={!previewChunk?.chunkId}
                    onClick={() => previewChunk && void handleGenerateChunkMetadata(String(previewChunk.chunkId))}
                  >
                    {previewChunk && (previewChunk.metadatas?.length ?? 0) > 0 ? t('wiki.doc.metadataContinueGenerate') : t('wiki.doc.metadataGenerate')}
                  </Button>
                </Space>
              </Space>
            </div>

            <div style={{ flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography.Text strong>{t('wiki.doc.metadataBase')} ({previewChunk.metadatas?.length ?? 0})</Typography.Text>
            </div>
            <div data-testid="wiki-metadata-list" style={{ flex: 1, minHeight: 0, overflowY: 'auto', overflowX: 'hidden', paddingRight: spacing.xs }}>
              {(previewChunk.metadatas ?? []).length > 0 ? (
                <Space direction="vertical" size="small" style={{ width: '100%' }}>
                  {(previewChunk.metadatas ?? []).map((metadata, index) => (
                    <div key={index} style={{ padding: spacing.sm, border: '1px solid rgba(5, 5, 5, 0.06)', borderRadius: 6, background: 'rgba(0, 0, 0, 0.02)' }}>
                      <Tag color="purple" style={{ marginBottom: spacing.xs }}>{t(metadataTypeKey(metadata.metadataType))}</Tag>
                      <Text style={{ display: 'block', wordBreak: 'break-word', whiteSpace: 'pre-wrap' }}>{metadata.metadataContent || '-'}</Text>
                    </div>
                  ))}
                </Space>
              ) : (
                <Text type="secondary">{t('wiki.doc.previewMetadataEmpty')}</Text>
              )}
            </div>
          </div>
        )}
      </Modal>
    </Page>
  )
}

interface ChunkCardProps {
  item: WikiDocumentEmbeddingChunkItem
  expanded: boolean
  onToggle: () => void
  onGenerateMetadata: () => void
  onPreviewMetadata: () => void
  generatingMetadata: boolean
  metadataGenerateDisabled: boolean
}

function ChunkCard({ item, expanded, onToggle, onGenerateMetadata, onPreviewMetadata, generatingMetadata, metadataGenerateDisabled }: ChunkCardProps) {
  const { t } = useTranslation()
  const metadatas = item.metadatas ?? []
  return (
    <Card
      size="small"
      title={
        <Space size="small">
          <Tag color="blue">#{item.sliceOrder ?? '-'}</Tag>
          <Text type="secondary" style={{ fontSize: 12 }}>
            ID: {String(item.chunkId ?? 'N/A')}
          </Text>
        </Space>
      }
      extra={
        <Space size="small">
          <Button type="text" size="small" onClick={onPreviewMetadata}>
            {t('wiki.doc.previewMetadata')}
          </Button>
          <Button type="text" size="small" icon={<ThunderboltOutlined />} loading={generatingMetadata} disabled={metadataGenerateDisabled} onClick={onGenerateMetadata}>
            {metadatas.length > 0 ? t('wiki.doc.metadataRegenerate') : t('wiki.doc.metadataGenerate')}
          </Button>
        </Space>
      }
      styles={{ body: { padding: spacing.md } }}
    >
      <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
        <Typography.Paragraph style={{ marginBottom: 0 }} ellipsis={{ rows: 8, expandable: false }}>
          {item.sliceContent || '-'}
        </Typography.Paragraph>
        {metadatas.length > 0 && (
          <>
            <Button type="text" size="small" onClick={onToggle}>
              {t('wiki.doc.previewMetadata')} ({metadatas.length}) {expanded ? t('wiki.doc.previewCollapse') : t('wiki.doc.previewExpand')}
            </Button>
            {expanded && (
              <Space direction="vertical" size="small" style={{ width: '100%' }}>
                {metadatas.map((m: WikiDocumentChunkMetadataItem, idx: number) => (
                  <div key={idx}>
                    <Tag color="purple">{t(metadataTypeKey(m.metadataType))}</Tag>
                    <Text style={{ wordBreak: 'break-all' }}>{m.metadataContent || '-'}</Text>
                  </div>
                ))}
              </Space>
            )}
          </>
        )}
      </Space>
    </Card>
  )
}

