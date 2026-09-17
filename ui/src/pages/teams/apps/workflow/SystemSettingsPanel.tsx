/**
 * 左侧浮层系统设置面板：由画布左上「系统设置」唤出、← 收起.
 * 含「对话开场白」（开关 + 文案）与「全局变量」（随草稿保存，节点经 system.* 引用）两个分区.
 */

import { Button, Input, Switch } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useWorkflowDesignerStore } from './store'
import { VariablesDrawerBody } from './VariablesDrawer'

export interface SystemSettingsPanelProps {
  onClose: () => void
}

export function SystemSettingsPanel({ onClose }: SystemSettingsPanelProps) {
  const { t } = useTranslation()
  const openingStatement = useWorkflowDesignerStore((s) => s.openingStatement)
  const openingStatementEnabled = useWorkflowDesignerStore((s) => s.openingStatementEnabled)
  const setOpeningStatement = useWorkflowDesignerStore((s) => s.setOpeningStatement)
  const variables = useWorkflowDesignerStore((s) => s.variables)
  const setVariables = useWorkflowDesignerStore((s) => s.setVariables)

  return (
    <div className="wf-side-panel">
      <div className="wf-side-panel-head">
        <span className="wf-side-panel-title">{t('workflowDesigner.systemSettings')}</span>
        <Button size="small" type="text" icon={<ArrowLeftOutlined />} onClick={onClose} />
      </div>
      <div className="wf-side-panel-body">
        <div className="wf-settings-section">
          <div className="wf-settings-sec-head">
            <span className="wf-settings-sec-title">{t('workflowDesigner.openingStatement')}</span>
            <Switch
              size="small"
              checked={openingStatementEnabled}
              onChange={(checked) => setOpeningStatement(openingStatement, checked)}
            />
          </div>
          <Input.TextArea
            rows={4}
            maxLength={4000}
            value={openingStatement}
            disabled={!openingStatementEnabled}
            placeholder={t('workflowDesigner.openingStatementPlaceholder')}
            onChange={(e) => setOpeningStatement(e.target.value, openingStatementEnabled)}
          />
        </div>
        <div className="wf-settings-section">
          <div className="wf-settings-sec-title">{t('workflowDesigner.variables')}</div>
          <VariablesDrawerBody variables={variables} onChange={setVariables} />
        </div>
      </div>
    </div>
  )
}
