import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Select, Space, Spin, Typography, Upload } from 'antd'
import { InboxOutlined } from '@ant-design/icons'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { uploadChatFile } from '@/utils/storage'
import {
  getKnowledgeGraphModelOptions,
  importKnowledgeGraphFile,
  type KnowledgeGraphImportResult,
  type KnowledgeGraphModelOption,
} from '@/api/knowledgeGraph'

const MAX_FILE_SIZE = 20 * 1024 * 1024

interface KnowledgeGraphAiImportFormProps {
  teamId: number
  graphId: number
  /** 导入成功后回调（刷新画布/详情） */
  onImported: () => void
}

/** AI 智能导入表单（导入页内嵌版）：文档直传 → 后端 Maomi.ToMarkdown 提取 → 对话模型按图谱现有模型抽取实体与关系 */
export function KnowledgeGraphAiImportForm({ teamId, graphId, onImported }: KnowledgeGraphAiImportFormProps) {
  const { t } = useTranslation()
  const [models, setModels] = useState<KnowledgeGraphModelOption[]>([])
  const [modelsLoading, setModelsLoading] = useState(true)
  const [aiModelId, setAiModelId] = useState<string | undefined>(undefined)
  const [file, setFile] = useState<File | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<KnowledgeGraphImportResult | null>(null)

  useEffect(() => {
    setModelsLoading(true)
    getKnowledgeGraphModelOptions(teamId)
      .then((options) => {
        const items = options.conversationModels ?? []
        setModels(items)
        if (items.length > 0) setAiModelId(items[0].id ?? undefined)
      })
      .catch(() => setModels([]))
      .finally(() => setModelsLoading(false))
  }, [teamId])

  const handleSelectFile = (selected: File) => {
    if (selected.size > MAX_FILE_SIZE) {
      feedback.error(t('knowledgegraph.import.fileTooLarge'))
      return false
    }

    setFile(selected)
    setResult(null)
    return false
  }

  const handleSubmit = async () => {
    if (!file || !aiModelId) return
    setSubmitting(true)
    try {
      const uploaded = await uploadChatFile(file)
      const res = await importKnowledgeGraphFile(graphId, {
        objectKey: uploaded.objectKey,
        fileName: file.name,
        aiModelId,
      })
      setResult(res)
      if ((res.nodesCreated ?? 0) > 0 || (res.edgesCreated ?? 0) > 0) {
        feedback.success(t('knowledgegraph.import.success'))
        onImported()
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Spin spinning={submitting} tip={t('knowledgegraph.import.submitting')}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: spacing.md, fontSize: 13 }}>
        {t('knowledgegraph.import.hint')}
      </Typography.Paragraph>

      <Upload.Dragger
        accept=".doc,.docx,.pdf,.txt,.md,.markdown,.html,.htm,.xls,.xlsx,.ppt,.pptx,.csv,.json"
        showUploadList={false}
        multiple={false}
        disabled={submitting}
        beforeUpload={handleSelectFile}
        style={{ marginBottom: spacing.md }}
      >
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">{file ? file.name : t('knowledgegraph.import.filePlaceholder')}</p>
        <p className="ant-upload-hint">{t('knowledgegraph.import.fileHint')}</p>
      </Upload.Dragger>

      <div style={{ marginBottom: spacing.sm }}>{t('knowledgegraph.import.modelLabel')}</div>
      {modelsLoading ? (
        <Spin size="small" />
      ) : models.length === 0 ? (
        <Alert type="warning" showIcon message={t('knowledgegraph.import.noModels')} />
      ) : (
        <Select
          style={{ width: '100%' }}
          value={aiModelId}
          onChange={setAiModelId}
          placeholder={t('knowledgegraph.import.modelPlaceholder')}
          options={models.map((x) => ({ value: x.id ?? '', label: x.name ?? '' }))}
        />
      )}

      <Space style={{ marginTop: spacing.md }}>
        <Button type="primary" loading={submitting} disabled={!file || !aiModelId} onClick={() => void handleSubmit()}>
          {t('knowledgegraph.import.submit')}
        </Button>
      </Space>

      {result && (
        <Alert
          type={(result.nodesCreated ?? 0) > 0 || (result.edgesCreated ?? 0) > 0 ? 'success' : 'warning'}
          showIcon
          style={{ marginTop: spacing.md }}
          message={result.message || t('knowledgegraph.import.resultTitle')}
          description={t('knowledgegraph.import.resultSummary', {
            nodes: result.nodesCreated ?? 0,
            edges: result.edgesCreated ?? 0,
            skippedNodes: result.skippedNodes ?? 0,
            skippedEdges: result.skippedEdges ?? 0,
          })}
        />
      )}
    </Spin>
  )
}
