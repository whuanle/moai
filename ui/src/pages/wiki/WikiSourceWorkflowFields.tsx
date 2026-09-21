import { Alert, Form } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

/**
 * 外部源工作流说明：外部源默认继承知识库默认工作流（在知识库设置页配置）。
 * 此处仅做提示，不提供逐源覆盖表单，避免与知识库默认工作流两处口径不一致。
 */
export function WikiSourceWorkflowFields() {
  const { t } = useTranslation()
  return (
    <Form.Item style={{ marginTop: spacing.lg, marginBottom: 0 }}>
      <Alert
        type="info"
        showIcon
        message={t('wiki.source.workflowInheritTitle')}
        description={t('wiki.source.workflowInheritHint')}
      />
    </Form.Item>
  )
}
