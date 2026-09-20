import { useEffect, useState } from 'react'
import { Alert, Button, Form } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { updateWikiWorkflowConfig, type WikiWorkflowConfig } from '@/api/wiki'
import {
  WikiWorkflowFormFields,
} from './WikiWorkflowForm'
import {
  workflowConfigFromValues,
  workflowValuesFromConfig,
  type ModelOption,
  type WikiWorkflowFormValues,
} from './wikiWorkflow'

interface WikiWorkflowSettingsProps {
  wikiId: number
  config?: WikiWorkflowConfig | null
  conversationModels: ModelOption[]
  modelsLoading?: boolean
  modelsFailed?: boolean
  /** 保存成功后回调（父级刷新详情） */
  onSaved?: () => void
}

/** 知识库设置页·默认工作流：编辑批量处理文档时预填的三个步骤（切割/生成元数据/向量化）预设 */
export function WikiWorkflowSettings({ wikiId, config, conversationModels, modelsLoading, modelsFailed, onSaved }: WikiWorkflowSettingsProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<WikiWorkflowFormValues>()
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    form.setFieldsValue(workflowValuesFromConfig(config))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [config])

  const handleSave = async () => {
    const values = await form.validateFields()
    if (!values.partitionEnabled && !values.metadataEnabled && !values.embeddingEnabled) {
      feedback.warning(t('wiki.workflow.selectStepRequired'))
      return
    }
    setSaving(true)
    try {
      await updateWikiWorkflowConfig(wikiId, workflowConfigFromValues(values))
      feedback.success(t('wiki.workflow.saveSuccess'))
      onSaved?.()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  return (
    <Form form={form} layout="vertical" style={{ marginTop: spacing.lg }}>
      <Alert type="info" showIcon message={t('wiki.workflow.settingsHint')} style={{ marginBottom: spacing.md }} />
      <WikiWorkflowFormFields form={form} conversationModels={conversationModels} modelsLoading={modelsLoading} modelsFailed={modelsFailed} />
      <Button type="primary" loading={saving} onClick={() => void handleSave()} disabled={modelsFailed}>
        {t('wiki.workflow.save')}
      </Button>
    </Form>
  )
}
