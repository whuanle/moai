import { useEffect, useMemo, useState } from 'react'
import { Alert, Button, Form, Grid, Input, InputNumber, Select, Space, Spin, Switch, Tag, Typography, theme } from 'antd'
import { RobotOutlined, SearchOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system'
import { getWikiDocuments, getWikiModelOptions, recallWikiTest, type WikiRecallTestHit } from '@/api/wiki'

const { Text, Paragraph, Title } = Typography

interface WikiRecallTestProps {
  wikiId: number
  teamId: number
}

interface RecallFormValues {
  query: string
  documentIds?: number[]
  top: number
  minScore?: number | null
  isOptimizeQuery: boolean
  isAnswer: boolean
  aiModelId?: string | null
}

interface ModelOption {
  id?: string | null
  name?: string | null
}

/** 元数据类型标签：与后端 MetadataType 约定一致 */
const METADATA_TYPE_KEYS: Record<number, string> = {
  0: 'wiki.recall.metaTypeSource',
  1: 'wiki.recall.metaTypeOutline',
  2: 'wiki.recall.metaTypeQuestion',
  3: 'wiki.recall.metaTypeKeyword',
  4: 'wiki.recall.metaTypeSummary',
  5: 'wiki.recall.metaTypeAggregated',
}

/** 相似度得分颜色：越高越绿 */
function scoreColor(score: number): string {
  if (score >= 0.75) return 'green'
  if (score >= 0.5) return 'orange'
  return 'default'
}

/** 左栏表单宽度（md 及以上为双栏布局） */
const FORM_PANE_WIDTH = 400

interface RecallResult {
  optimizedQuery: string
  answer: string
  items: WikiRecallTestHit[]
  /** 本次搜索是否请求了 AI 优化 / AI 回答（用于空结果时的差异化提示） */
  requestedOptimize: boolean
  requestedAnswer: boolean
  /** 本次回答使用的模型名（用于回答卡片署名） */
  answerModelName: string
}

export function WikiRecallTest({ wikiId, teamId }: WikiRecallTestProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<RecallFormValues>()
  const { token } = theme.useToken()
  const screens = Grid.useBreakpoint()
  const isWide = screens.md ?? false

  const [documents, setDocuments] = useState<{ id: number; name: string }[]>([])
  const [documentsLoading, setDocumentsLoading] = useState(false)
  const [conversationModels, setConversationModels] = useState<ModelOption[]>([])
  const [modelsLoading, setModelsLoading] = useState(false)
  const [modelsFailed, setModelsFailed] = useState(false)

  const [searching, setSearching] = useState(false)
  const [searched, setSearched] = useState(false)
  const [result, setResult] = useState<RecallResult | null>(null)

  const optimizeEnabled = Form.useWatch('isOptimizeQuery', form)
  const answerEnabled = Form.useWatch('isAnswer', form)
  const needModel = optimizeEnabled === true || answerEnabled === true

  useEffect(() => {
    let cancelled = false
    setDocumentsLoading(true)
    getWikiDocuments(wikiId, { pageNo: 1, pageSize: 100 })
      .then((res) => {
        if (cancelled) return
        setDocuments((res.items ?? []).map((item) => ({ id: Number(item.documentId), name: item.fileName ?? String(item.documentId) })))
      })
      .catch(() => undefined)
      .finally(() => {
        if (!cancelled) setDocumentsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [wikiId])

  useEffect(() => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    let cancelled = false
    setModelsLoading(true)
    setModelsFailed(false)
    getWikiModelOptions(teamId)
      .then((options) => {
        if (cancelled) return
        setConversationModels(options?.conversationModels ?? [])
      })
      .catch(() => {
        if (cancelled) return
        setConversationModels([])
        setModelsFailed(true)
      })
      .finally(() => {
        if (!cancelled) setModelsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [teamId])

  const documentOptions = useMemo(
    () => documents.map((doc) => ({ value: doc.id, label: doc.name })),
    [documents],
  )

  const handleSearch = async () => {
    let values: RecallFormValues
    try {
      values = await form.validateFields()
    } catch {
      // 校验失败，错误信息已由表单展示
      return
    }
    setSearching(true)
    try {
      const res = await recallWikiTest(wikiId, {
        query: values.query.trim(),
        documentIds: values.documentIds ?? [],
        top: values.top,
        minScore: values.minScore ?? null,
        aiModelId: values.isOptimizeQuery || values.isAnswer ? values.aiModelId ?? null : null,
        isOptimizeQuery: values.isOptimizeQuery,
        isAnswer: values.isAnswer,
      })
      setResult({
        optimizedQuery: res.optimizedQuery ?? '',
        answer: res.answer ?? '',
        items: res.items ?? [],
        requestedOptimize: values.isOptimizeQuery,
        requestedAnswer: values.isAnswer,
        answerModelName: conversationModels.find((model) => model.id === values.aiModelId)?.name ?? '',
      })
      setSearched(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSearching(false)
    }
  }

  const handleOptimizeToggle = (checked: boolean) => {
    if (!checked) return
    // 开启任一 AI 开关时若未选模型，自动带入第一个可用模型
    const modelId = form.getFieldValue('aiModelId')
    if (!modelId && conversationModels.length > 0) {
      form.setFieldValue('aiModelId', conversationModels[0]?.id ?? undefined)
    }
  }

  const formPane = (
    <Form
      form={form}
      layout="vertical"
      initialValues={{ top: 5, isOptimizeQuery: false, isAnswer: false }}
    >
      <Form.Item
        name="query"
        label={t('wiki.recall.queryLabel')}
        rules={[
          { required: true, message: t('wiki.recall.queryRequired') },
          { max: 1000, message: t('wiki.recall.queryMax') },
        ]}
      >
        <Input.TextArea
          placeholder={t('wiki.recall.queryPlaceholder')}
          maxLength={1000}
          rows={4}
          onPressEnter={(event) => {
            if (!event.shiftKey && !searching) {
              event.preventDefault()
              void handleSearch()
            }
          }}
        />
      </Form.Item>
      <Form.Item name="documentIds" label={t('wiki.recall.scopeLabel')}>
        <Select
          mode="multiple"
          allowClear
          maxTagCount="responsive"
          placeholder={t('wiki.recall.scopePlaceholder')}
          options={documentOptions}
          loading={documentsLoading}
          showSearch
          optionFilterProp="label"
          virtual={false}
        />
      </Form.Item>
      <div style={{ display: 'flex', gap: spacing.md }}>
        <Form.Item
          name="top"
          label={t('wiki.recall.topLabel')}
          normalize={(value) => (value == null ? 5 : Number(value))}
          rules={[{ required: true, message: t('wiki.recall.topRequired') }]}
          style={{ flex: 1 }}
        >
          <InputNumber min={1} max={50} precision={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="minScore"
          label={t('wiki.recall.minScoreLabel')}
          normalize={(value) => (value === '' || value == null ? null : Number(value))}
          style={{ flex: 1 }}
        >
          <InputNumber min={0} max={1} step={0.05} placeholder={t('wiki.recall.minScorePlaceholder')} style={{ width: '100%' }} />
        </Form.Item>
      </div>
      <Form.Item label={t('wiki.recall.aiTitle')} style={{ marginBottom: spacing.xs }}>
        <Space size={spacing.lg} wrap>
          <Space size={spacing.xs} style={{ whiteSpace: 'nowrap' }}>
            <Form.Item name="isOptimizeQuery" valuePropName="checked" noStyle>
              <Switch aria-label={t('wiki.recall.optimizeLabel')} onChange={handleOptimizeToggle} />
            </Form.Item>
            <Text style={{ whiteSpace: 'nowrap' }}>{t('wiki.recall.optimizeLabel')}</Text>
          </Space>
          <Space size={spacing.xs} style={{ whiteSpace: 'nowrap' }}>
            <Form.Item name="isAnswer" valuePropName="checked" noStyle>
              <Switch aria-label={t('wiki.recall.answerLabel')} onChange={handleOptimizeToggle} />
            </Form.Item>
            <Text style={{ whiteSpace: 'nowrap' }}>{t('wiki.recall.answerLabel')}</Text>
          </Space>
        </Space>
      </Form.Item>
      <Form.Item
        name="aiModelId"
        label={t('wiki.recall.modelLabel')}
        rules={[{ required: needModel, message: t('wiki.recall.modelRequired') }]}
      >
        <Select
          allowClear
          disabled={!needModel || modelsLoading || modelsFailed}
          placeholder={t('wiki.recall.modelPlaceholder')}
          options={conversationModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
          showSearch
          virtual={false}
          notFoundContent={t('wiki.recall.modelEmpty')}
        />
      </Form.Item>
      {modelsFailed && <Alert type="warning" showIcon message={t('wiki.recall.modelsFailed')} style={{ marginBottom: spacing.md }} />}
      <Button type="primary" icon={<SearchOutlined />} loading={searching} block onClick={() => void handleSearch()}>
        {t('wiki.recall.search')}
      </Button>
    </Form>
  )

  const renderResults = () => {
    if (!result) {
      if (searching) {
        return (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: spacing.sm, padding: '72px 0' }}>
            <Spin />
            <Text type="secondary">{t('wiki.recall.searchingHint')}</Text>
          </div>
        )
      }
      return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: spacing.sm, padding: '72px 0' }}>
          <SearchOutlined style={{ fontSize: 36, color: token.colorTextQuaternary }} />
          <Text type="secondary">{t('wiki.recall.initialHint')}</Text>
        </div>
      )
    }

    return (
      <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
        {result.requestedAnswer && (
          result.answer ? (
            <div
              style={{
                background: token.colorInfoBg,
                border: `1px solid ${token.colorInfoBorder}`,
                borderRadius: token.borderRadiusLG,
                padding: spacing.md,
              }}
            >
              <Space size={spacing.xs} wrap style={{ marginBottom: spacing.xs }}>
                <RobotOutlined style={{ color: token.colorPrimary }} />
                <Text strong>{t('wiki.recall.answerTitle')}</Text>
                {result.answerModelName && (
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {t('wiki.recall.answerBy', { model: result.answerModelName })}
                  </Text>
                )}
              </Space>
              <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0, fontSize: 14, lineHeight: 1.8 }}>
                {result.answer}
              </Paragraph>
            </div>
          ) : result.items.length > 0 ? (
            <Alert type="warning" showIcon message={t('wiki.recall.answerEmpty')} />
          ) : (
            <Alert type="info" showIcon message={t('wiki.recall.answerSkipped')} />
          )
        )}
        {result.requestedOptimize && result.optimizedQuery && (
          <Alert
            type="info"
            showIcon
            message={<Text>{t('wiki.recall.optimizedPrefix')}<Text strong>{result.optimizedQuery}</Text></Text>}
          />
        )}
        <Title level={5} style={{ margin: 0 }}>
          {t('wiki.recall.resultTitle')}
          <Text type="secondary" style={{ fontSize: 13, fontWeight: 'normal', marginLeft: spacing.sm }}>
            {t('wiki.recall.resultCount', { count: result.items.length })}
          </Text>
        </Title>
        {result.items.length === 0 ? (
          <Text type="secondary">{searched ? t('wiki.recall.empty') : ''}</Text>
        ) : (
          result.items.map((hit, index) => (
            <div
              key={`${hit.documentId}-${hit.chunkId}-${hit.metadataType}-${index}`}
              style={{
                border: `1px solid ${token.colorBorderSecondary}`,
                borderRadius: token.borderRadiusLG,
                padding: spacing.md,
              }}
            >
              <Space wrap size={spacing.xs} style={{ marginBottom: spacing.xs }}>
                <Tag color={scoreColor(hit.score ?? 0)}>{index + 1} · {t('wiki.recall.scoreLabel')} {(hit.score ?? 0).toFixed(4)}</Tag>
                <Tag>{hit.documentName || t('wiki.recall.unknownDocument')}</Tag>
                <Tag>{t(METADATA_TYPE_KEYS[hit.metadataType ?? 0] ?? METADATA_TYPE_KEYS[0])}</Tag>
              </Space>
              <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }} ellipsis={{ rows: 4, expandable: true, symbol: t('wiki.recall.expand') }}>
                {hit.content}
              </Paragraph>
            </div>
          ))
        )}
      </Space>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: isWide ? 'row' : 'column', gap: spacing.lg, width: '100%' }}>
      <div style={isWide ? { width: FORM_PANE_WIDTH, flexShrink: 0 } : { width: '100%' }}>
        {formPane}
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        {searching && result && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: spacing.sm,
              padding: `${spacing.xs}px ${spacing.md}px`,
              background: token.colorFillQuaternary,
              borderRadius: token.borderRadiusLG,
              marginBottom: spacing.md,
            }}
          >
            <Spin size="small" />
            <Text type="secondary">{t('wiki.recall.searchingBadge')}</Text>
          </div>
        )}
        {renderResults()}
      </div>
    </div>
  )
}
