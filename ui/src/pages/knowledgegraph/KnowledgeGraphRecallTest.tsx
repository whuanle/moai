import { useEffect, useState } from 'react'
import { Alert, Button, Form, Grid, Input, InputNumber, Select, Space, Spin, Switch, Tag, Typography, theme } from 'antd'
import { RobotOutlined, SearchOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system'
import { getKnowledgeGraphModelOptions, recallKnowledgeGraphTest, type KnowledgeGraphRecallTestHit } from '@/api/knowledgeGraph'

const { Text, Paragraph, Title } = Typography

interface KnowledgeGraphRecallTestProps {
  kgId: number
  teamId: number
}

interface RecallFormValues {
  query: string
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
  items: KnowledgeGraphRecallTestHit[]
  skippedHints: string[]
  /** 本次搜索是否请求了 AI 优化 / AI 回答（用于空结果时的差异化提示） */
  requestedOptimize: boolean
  requestedAnswer: boolean
  /** 本次回答使用的模型名（用于回答卡片署名） */
  answerModelName: string
}

/** 知识图谱召回测试：向量召回实体 + 一跳邻居，支持 AI 优化问题与 AI 生成回答（语义对齐知识库召回测试） */
export function KnowledgeGraphRecallTest({ kgId, teamId }: KnowledgeGraphRecallTestProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<RecallFormValues>()
  const { token } = theme.useToken()
  const screens = Grid.useBreakpoint()
  const isWide = screens.md ?? false

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
    if (!Number.isFinite(teamId) || teamId <= 0) return
    let cancelled = false
    setModelsLoading(true)
    setModelsFailed(false)
    getKnowledgeGraphModelOptions(teamId)
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
      const res = await recallKnowledgeGraphTest(kgId, {
        query: values.query.trim(),
        top: values.top,
        minScore: values.minScore ?? null,
        aiModelId: values.isOptimizeQuery || values.isAnswer ? values.aiModelId ?? null : null,
        isOptimizeQuery: values.isOptimizeQuery,
        isAnswer: values.isAnswer,
      })
      setResult({
        optimizedQuery: res.optimizedQuery ?? '',
        answer: res.answer ?? '',
        items: res.hits ?? [],
        skippedHints: res.skippedHints ?? [],
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

  const handleAiToggle = () => {
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
        label={t('knowledgegraph.recall.queryLabel')}
        rules={[
          { required: true, message: t('knowledgegraph.recall.queryRequired') },
          { max: 1000, message: t('knowledgegraph.recall.queryMax') },
        ]}
      >
        <Input.TextArea
          placeholder={t('knowledgegraph.recall.queryPlaceholder')}
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
      <div style={{ display: 'flex', gap: spacing.md }}>
        <Form.Item
          name="top"
          label={t('knowledgegraph.recall.topLabel')}
          normalize={(value) => (value == null ? 5 : Number(value))}
          rules={[{ required: true, message: t('knowledgegraph.recall.topRequired') }]}
          style={{ flex: 1 }}
        >
          <InputNumber min={1} max={50} precision={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="minScore"
          label={t('knowledgegraph.recall.minScoreLabel')}
          normalize={(value) => (value === '' || value == null ? null : Number(value))}
          style={{ flex: 1 }}
        >
          <InputNumber min={0} max={1} step={0.05} placeholder={t('knowledgegraph.recall.minScorePlaceholder')} style={{ width: '100%' }} />
        </Form.Item>
      </div>
      <Form.Item label={t('knowledgegraph.recall.aiTitle')} style={{ marginBottom: spacing.xs }}>
        <Space size={spacing.lg} wrap>
          <Space size={spacing.xs} style={{ whiteSpace: 'nowrap' }}>
            <Form.Item name="isOptimizeQuery" valuePropName="checked" noStyle>
              <Switch aria-label={t('knowledgegraph.recall.optimizeLabel')} onChange={handleAiToggle} />
            </Form.Item>
            <Text style={{ whiteSpace: 'nowrap' }}>{t('knowledgegraph.recall.optimizeLabel')}</Text>
          </Space>
          <Space size={spacing.xs} style={{ whiteSpace: 'nowrap' }}>
            <Form.Item name="isAnswer" valuePropName="checked" noStyle>
              <Switch aria-label={t('knowledgegraph.recall.answerLabel')} onChange={handleAiToggle} />
            </Form.Item>
            <Text style={{ whiteSpace: 'nowrap' }}>{t('knowledgegraph.recall.answerLabel')}</Text>
          </Space>
        </Space>
      </Form.Item>
      <Form.Item
        name="aiModelId"
        label={t('knowledgegraph.recall.modelLabel')}
        rules={[{ required: needModel, message: t('knowledgegraph.recall.modelRequired') }]}
      >
        <Select
          allowClear
          disabled={!needModel || modelsLoading || modelsFailed}
          placeholder={t('knowledgegraph.recall.modelPlaceholder')}
          options={conversationModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
          showSearch
          virtual={false}
          notFoundContent={t('knowledgegraph.recall.modelEmpty')}
        />
      </Form.Item>
      {modelsFailed && <Alert type="warning" showIcon message={t('knowledgegraph.recall.modelsFailed')} style={{ marginBottom: spacing.md }} />}
      <Button type="primary" icon={<SearchOutlined />} loading={searching} block onClick={() => void handleSearch()}>
        {t('knowledgegraph.recall.search')}
      </Button>
    </Form>
  )

  const renderResults = () => {
    if (!result) {
      if (searching) {
        return (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: spacing.sm, padding: '72px 0' }}>
            <Spin />
            <Text type="secondary">{t('knowledgegraph.recall.searchingHint')}</Text>
          </div>
        )
      }
      return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: spacing.sm, padding: '72px 0' }}>
          <SearchOutlined style={{ fontSize: 36, color: token.colorTextQuaternary }} />
          <Text type="secondary">{t('knowledgegraph.recall.initialHint')}</Text>
        </div>
      )
    }

    return (
      <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
        {result.skippedHints.length > 0 && (
          <Alert type="warning" showIcon message={t('knowledgegraph.recall.skippedTitle')} description={result.skippedHints.join('；')} />
        )}
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
                <Text strong>{t('knowledgegraph.recall.answerTitle')}</Text>
                {result.answerModelName && (
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {t('knowledgegraph.recall.answerBy', { model: result.answerModelName })}
                  </Text>
                )}
              </Space>
              <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0, fontSize: 14, lineHeight: 1.8 }}>
                {result.answer}
              </Paragraph>
            </div>
          ) : result.items.length > 0 ? (
            <Alert type="warning" showIcon message={t('knowledgegraph.recall.answerEmpty')} />
          ) : (
            <Alert type="info" showIcon message={t('knowledgegraph.recall.answerSkipped')} />
          )
        )}
        {result.requestedOptimize && result.optimizedQuery && (
          <Alert
            type="info"
            showIcon
            message={<Text>{t('knowledgegraph.recall.optimizedPrefix')}<Text strong>{result.optimizedQuery}</Text></Text>}
          />
        )}
        <Title level={5} style={{ margin: 0 }}>
          {t('knowledgegraph.recall.resultTitle')}
          <Text type="secondary" style={{ fontSize: 13, fontWeight: 'normal', marginLeft: spacing.sm }}>
            {t('knowledgegraph.recall.resultCount', { count: result.items.length })}
          </Text>
        </Title>
        {result.items.length === 0 ? (
          <Text type="secondary">{searched ? t('knowledgegraph.recall.empty') : ''}</Text>
        ) : (
          result.items.map((hit, index) => (
            <div
              key={`${hit.nodeId}-${index}`}
              style={{
                border: `1px solid ${token.colorBorderSecondary}`,
                borderRadius: token.borderRadiusLG,
                padding: spacing.md,
              }}
            >
              <Space wrap size={spacing.xs} style={{ marginBottom: spacing.xs }}>
                <Tag color={scoreColor(hit.score ?? 0)}>{index + 1} · {t('knowledgegraph.recall.scoreLabel')} {(hit.score ?? 0).toFixed(4)}</Tag>
                <Tag>{hit.name}</Tag>
                {hit.entityTypeName && <Tag color="blue">{hit.entityTypeName}</Tag>}
              </Space>
              {hit.description && (
                <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }} ellipsis={{ rows: 4, expandable: true, symbol: t('knowledgegraph.recall.expand') }}>
                  {hit.description}
                </Paragraph>
              )}
              {(hit.neighbors?.length ?? 0) > 0 && (
                <div style={{ marginTop: spacing.xs }}>
                  <Text type="secondary" style={{ fontSize: 12 }}>{t('knowledgegraph.recall.neighborTitle')}</Text>
                  {hit.neighbors!.map((neighbor, neighborIndex) => (
                    <div key={`${neighbor.direction}-${neighborIndex}`} style={{ fontSize: 12 }}>
                      <Text type="secondary">
                        {neighbor.direction === 'out' ? '→' : '←'} {neighbor.relationName || '-'} {neighbor.direction === 'out' ? '→' : '←'} <Text strong style={{ fontSize: 12 }}>{neighbor.name}</Text>
                        {neighbor.description ? `（${neighbor.description}）` : ''}
                      </Text>
                    </div>
                  ))}
                </div>
              )}
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
            <Text type="secondary">{t('knowledgegraph.recall.searchingBadge')}</Text>
          </div>
        )}
        {renderResults()}
      </div>
    </div>
  )
}
