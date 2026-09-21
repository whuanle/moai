import { useEffect, useState } from 'react'
import { Alert, Button, Form, List, Modal, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  batchRunWikiDocumentsWorkflow,
  getWikiDetail,
  getWikiModelOptions,
  type BatchWorkflowDocumentResult,
  type WikiModelOptionsResult,
} from '@/api/wiki'
import {
  WikiWorkflowFormFields,
} from './WikiWorkflowForm'
import {
  DEFAULT_WORKFLOW_VALUES,
  type ModelOption,
  type WikiWorkflowFormValues,
} from './wikiWorkflow'

const { Text } = Typography

interface BatchWorkflowModalProps {
  wikiId: number
  teamId?: number
  open: boolean
  documentIds: number[]
  onClose: () => void
  /** 批量任务提交成功后回调（刷新列表、清空多选） */
  onDone: () => void
}

/**
 * 批量处理文档弹窗：多选文件后按勾选步骤（切割 / 生成元数据 / 向量化）一次性执行，也可只勾选其中一步；
 * 步骤参数每次打开时按通用默认值预填，由用户实时调整后提交。
 */
export function BatchWorkflowModal({ wikiId, teamId, open, documentIds, onClose, onDone }: BatchWorkflowModalProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<WikiWorkflowFormValues>()

  const [submitting, setSubmitting] = useState(false)
  const [results, setResults] = useState<BatchWorkflowDocumentResult[] | null>(null)
  const [embeddingReady, setEmbeddingReady] = useState(true)
  const [conversationModels, setConversationModels] = useState<ModelOption[]>([])
  const [modelsLoading, setModelsLoading] = useState(false)
  const [modelsFailed, setModelsFailed] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setResults(null)
    form.setFieldsValue({ ...DEFAULT_WORKFLOW_VALUES })
    getWikiDetail(wikiId)
      .then((res) => {
        if (cancelled) return
        const detail = res as unknown as { embeddingModelId?: string | null; embeddingDimensions?: number | null }
        setEmbeddingReady(Boolean(detail.embeddingModelId) && Number(detail.embeddingDimensions ?? 0) > 0)
      })
      .catch(() => {
        // 详情读取失败不阻塞弹窗，仅按向量模型未配置处理
        if (!cancelled) setEmbeddingReady(false)
      })
    if (Number.isFinite(teamId) && (teamId ?? 0) > 0) {
      setModelsLoading(true)
      getWikiModelOptions(teamId as number)
        .then((options: WikiModelOptionsResult | undefined) => {
          if (!cancelled) setConversationModels(options?.conversationModels ?? [])
        })
        .catch(() => {
          if (!cancelled) setModelsFailed(true)
        })
        .finally(() => {
          if (!cancelled) setModelsLoading(false)
        })
    }
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, wikiId, teamId])

  const embeddingEnabled = Form.useWatch('embeddingEnabled', form) ?? false
  const embeddingNotReady = embeddingEnabled && !embeddingReady

  const handleSubmit = async () => {
    const values = await form.validateFields()
    if (!values.partitionEnabled && !values.metadataEnabled && !values.embeddingEnabled) {
      feedback.warning(t('wiki.workflow.selectStepRequired'))
      return
    }
    setSubmitting(true)
    try {
      const items = await batchRunWikiDocumentsWorkflow(wikiId, {
        documentIds,
        isPartition: values.partitionEnabled,
        isAiPartition: values.partitionMode === 'ai',
        aiModelId: values.aiModelId,
        promptTemplate: values.promptTemplate || null,
        splitMode: values.splitMode,
        chunkSize: values.chunkSize,
        chunkOverlap: values.chunkOverlap,
        overlapUnit: values.overlapUnit,
        sizeUnit: values.sizeUnit,
        tokenEncodingOrModel: values.sizeUnit === 'token' ? (values.tokenEncodingOrModel || null) : null,
        isGenerateMetadata: values.metadataEnabled,
        metadataModelId: values.metadataModelId,
        strategyTypes: values.strategyTypes?.length ? values.strategyTypes : null,
        isEmbedding: values.embeddingEnabled,
        embedSourceText: values.embedSourceText,
        embedMetadata: values.embedMetadata,
      })
      setResults(items)
      const failed = items.filter((item) => !item.success).length
      if (failed === 0) {
        feedback.success(t('wiki.workflow.batchAllSubmitted', { count: items.length }))
      }
      onDone()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const successCount = results?.filter((item) => item.success).length ?? 0
  const failCount = (results?.length ?? 0) - successCount

  return (
    <Modal
      title={results ? t('wiki.workflow.batchResultTitle') : t('wiki.workflow.batchTitle')}
      open={open}
      onCancel={onClose}
      maskClosable={false}
      width={680}
      footer={
        results
          ? [
            <Button key="done" type="primary" onClick={onClose}>
              {t('wiki.workflow.batchDone')}
            </Button>,
          ]
          : [
            <Button key="cancel" onClick={onClose} disabled={submitting}>
              {t('wiki.cancel')}
            </Button>,
            <Button key="submit" type="primary" loading={submitting} disabled={embeddingNotReady} onClick={() => void handleSubmit()}>
              {t('wiki.workflow.batchSubmit', { count: documentIds.length })}
            </Button>,
          ]
      }
    >
      {results ? (
        <>
          <Alert
            type={failCount === 0 ? 'success' : 'warning'}
            showIcon
            message={t('wiki.workflow.batchResultSummary', { success: successCount, fail: failCount })}
            style={{ marginBottom: spacing.md }}
          />
          <List
            size="small"
            dataSource={results}
            style={{ maxHeight: 360, overflow: 'auto' }}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta
                  title={item.fileName || `#${item.documentId ?? ''}`}
                  description={<Text type="secondary">{item.message}</Text>}
                />
                {item.success ? <Tag color="green">{t('wiki.workflow.batchSuccessTag')}</Tag> : <Tag color="red">{t('wiki.workflow.batchFailTag')}</Tag>}
              </List.Item>
            )}
          />
        </>
      ) : (
        <>
          <Alert
            type="info"
            showIcon
            message={t('wiki.workflow.batchSelectedCount', { count: documentIds.length })}
            style={{ marginBottom: spacing.md }}
          />
          {embeddingNotReady && (
            <Alert type="warning" showIcon message={t('wiki.workflow.embeddingNotReady')} style={{ marginBottom: spacing.md }} />
          )}
          <Form form={form} layout="vertical">
            <WikiWorkflowFormFields form={form} conversationModels={conversationModels} modelsLoading={modelsLoading} modelsFailed={modelsFailed} />
          </Form>
        </>
      )}
    </Modal>
  )
}
