/**
 * 左侧浮层系统设置面板：由画布左上「系统设置」唤出、← 收起.
 * 含「对话开场白」（开关 + 文案）、「系统变量」（对话注入的 sys.* 只读列表）
 * 与「全局变量」（随草稿保存，节点经 system.* 引用）三个分区.
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

  // 对话系统变量（只读展示）：发布后对话时由服务端注入 sys.*，节点直接引用
  const sysVariableItems = [
    { name: t('workflowDesigner.sysVarUserId'), ref: 'sys.userId', type: 'String' },
    { name: t('workflowDesigner.sysVarAppId'), ref: 'sys.appId', type: 'String' },
    { name: t('workflowDesigner.sysVarConversationId'), ref: 'sys.conversationId', type: 'String' },
    { name: t('workflowDesigner.sysVarMessageId'), ref: 'sys.messageId', type: 'String' },
    { name: t('workflowDesigner.sysVarHistory'), ref: 'sys.history', type: 'Array<Object>' },
    { name: t('workflowDesigner.sysVarCurrentTime'), ref: 'sys.currentTime', type: 'String' },
  ]

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
          <div className="wf-settings-sec-title">{t('workflowDesigner.sysVariables')}</div>
          <div className="wf-config-hint" style={{ margin: '6px 0 4px' }}>
            {t('workflowDesigner.sysVariablesHint')}
          </div>
          {sysVariableItems.map((item) => (
            <div key={item.ref} className="wf-sys-var-row">
              <span className="wf-sys-var-name">{item.name}</span>
              <span className="wf-sys-var-ref">{item.ref}</span>
              <span className="wf-sys-var-tag">{item.type}</span>
            </div>
          ))}
        </div>
        <div className="wf-settings-section">
          <div className="wf-settings-sec-title">{t('workflowDesigner.variables')}</div>
          <VariablesDrawerBody variables={variables} onChange={setVariables} />
        </div>
      </div>
    </div>
  )
}
