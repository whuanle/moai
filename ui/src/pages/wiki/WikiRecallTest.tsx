import { useEffect, useMemo, useState } from 'react'
import { Alert, Button, Form, Input, InputNumber, Select, Space, Switch, Tag, Typography } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
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

export function WikiRecallTest({ wikiId, teamId }: WikiRecallTestProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<RecallFormValues>()

  const [documents, setDocuments] = useState<{ id: number; name: string }[]>([])
  const [documentsLoading, setDocumentsLoading] = useState(false)
  const [conversationModels, setConversationModels] = useState<ModelOption[]>([])
  const [modelsLoading, setModelsLoading] = useState(false)
  const [modelsFailed, setModelsFailed] = useState(false)

  const [searching, setSearching] = useState(false)
  const [searched, setSearched] = useState(false)
  const [result, setResult] = useState<{ optimizedQuery: string; answer: string; items: WikiRecallTestHit[] } | null>(null)

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
    // 开启任一 AI 开关时若未选模型，提示先选择
    const modelId = form.getFieldValue('aiModelId')
    if (!modelId && conversationModels.length > 0) {
      form.setFieldValue('aiModelId', conversationModels[0]?.id ?? undefined)
    }
  }

  return (
    <Space direction="vertical" size={spacing.lg} style={{ width: '100%' }}>
      <Form
        form={form}
        layout="vertical"
        initialValues={{ top: 5, isOptimizeQuery: false, isAnswer: false }}
        style={{ maxWidth: 860 }}
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
            rows={3}
            onPressEnter={(event) => {
              if (!event.shiftKey && !searching) {
                event.preventDefault()
                void handleSearch()
              }
            }}
          />
        </Form.Item>
        <Space wrap size={spacing.md} align="start">
          <Form.Item name="documentIds" label={t('wiki.recall.scopeLabel')} style={{ minWidth: 320 }}>
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
          <Form.Item
            name="top"
            label={t('wiki.recall.topLabel')}
            normalize={(value) => (value == null ? 5 : Number(value))}
            rules={[{ required: true, message: t('wiki.recall.topRequired') }]}
          >
            <InputNumber min={1} max={50} precision={0} style={{ width: 120 }} />
          </Form.Item>
          <Form.Item
            name="minScore"
            label={t('wiki.recall.minScoreLabel')}
            normalize={(value) => (value === '' || value == null ? null : Number(value))}
          >
            <InputNumber min={0} max={1} step={0.05} placeholder={t('wiki.recall.minScorePlaceholder')} style={{ width: 160 }} />
          </Form.Item>
        </Space>
        <Space wrap size={spacing.md} align="start">
          <Form.Item name="isOptimizeQuery" label={t('wiki.recall.optimizeLabel')} valuePropName="checked">
            <Switch onChange={handleOptimizeToggle} />
          </Form.Item>
          <Form.Item name="isAnswer" label={t('wiki.recall.answerLabel')} valuePropName="checked">
            <Switch onChange={handleOptimizeToggle} />
          </Form.Item>
          <Form.Item
            name="aiModelId"
            label={t('wiki.recall.modelLabel')}
            rules={[{ required: needModel, message: t('wiki.recall.modelRequired') }]}
            style={{ minWidth: 260 }}
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
        </Space>
        {modelsFailed && <Alert type="warning" showIcon message={t('wiki.recall.modelsFailed')} style={{ marginBottom: spacing.md, maxWidth: 860 }} />}
        <Button type="primary" icon={<SearchOutlined />} loading={searching} onClick={() => void handleSearch()}>
          {t('wiki.recall.search')}
        </Button>
      </Form>

      {result && (
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          {result.optimizedQuery && (
            <Alert
              type="info"
              showIcon
              message={`${t('wiki.recall.optimizedPrefix')}${result.optimizedQuery}`}
            />
          )}
          {result.answer && (
            <div>
              <Title level={5} style={{ marginTop: 0 }}>{t('wiki.recall.answerTitle')}</Title>
              <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>{result.answer}</Paragraph>
            </div>
          )}
          <Title level={5} style={{ margin: 0 }}>{t('wiki.recall.resultTitle')}</Title>
          {result.items.length === 0 ? (
            <Text type="secondary">{searched ? t('wiki.recall.empty') : ''}</Text>
          ) : (
            result.items.map((hit, index) => (
              <div
                key={`${hit.documentId}-${hit.chunkId}-${hit.metadataType}-${index}`}
                style={{
                  border: '1px solid rgba(0, 0, 0, 0.06)',
                  borderRadius: spacing.sm,
                  padding: spacing.md,
                }}
              >
                <Space wrap size={spacing.xs} style={{ marginBottom: spacing.xs }}>
                  <Tag color={scoreColor(hit.score ?? 0)}>{t('wiki.recall.scoreLabel')}: {(hit.score ?? 0).toFixed(4)}</Tag>
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
      )}
    </Space>
  )
}
