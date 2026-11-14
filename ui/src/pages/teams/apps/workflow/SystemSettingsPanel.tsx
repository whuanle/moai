/**
 * 左侧浮层系统设置面板：由画布左上「系统设置」唤出、← 收起.
 * 含「系统提示词」（TextArea，经 sys.prompt 供节点引用）、「对话开场白」（开关 + 文案）、「快捷输入」
 * （欢迎态胶囊，点击即发送）、「系统变量」（对话注入的 sys.* 只读列表）
 * 与「全局变量」（随草稿保存，节点经 system.* 引用）分区.
 */

import { ArrowLeftOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Input, Switch, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import { useWorkflowDesignerStore } from './store'
import { VariablesDrawerBody } from './VariablesDrawer'

/** 快捷输入上限（与后端 SaveAppAgentConfigCommand 校验一致），每条最长 200 字符由 Input maxLength 限制 */
const MAX_QUICK_INPUTS = 10

export interface SystemSettingsPanelProps {
  onClose: () => void
}

export function SystemSettingsPanel({ onClose }: SystemSettingsPanelProps) {
  const { t } = useTranslation()
  const openingStatement = useWorkflowDesignerStore((s) => s.openingStatement)
  const openingStatementEnabled = useWorkflowDesignerStore((s) => s.openingStatementEnabled)
  const setOpeningStatement = useWorkflowDesignerStore((s) => s.setOpeningStatement)
  const prompt = useWorkflowDesignerStore((s) => s.prompt)
  const setPrompt = useWorkflowDesignerStore((s) => s.setPrompt)
  const quickInputs = useWorkflowDesignerStore((s) => s.quickInputs)
  const setQuickInputs = useWorkflowDesignerStore((s) => s.setQuickInputs)
  const variables = useWorkflowDesignerStore((s) => s.variables)
  const setVariables = useWorkflowDesignerStore((s) => s.setVariables)

  // 对话系统变量（只读展示）：发布后对话时由服务端注入 sys.*，节点直接引用；
  // sys.prompt 为应用系统提示词（正式对话按发布快照、调试按草稿），AI 等节点的 system 输入可绑定引用
  const sysVariableItems = [
    { name: t('workflowDesigner.sysVarPrompt'), ref: 'sys.prompt', type: 'String' },
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
        <Tooltip title={t('workflowDesigner.closePanel')}>
          <Button size="small" type="text" icon={<ArrowLeftOutlined />} aria-label={t('workflowDesigner.closePanel')} onClick={onClose} />
        </Tooltip>
      </div>
      <div className="wf-side-panel-body">
        <div className="wf-settings-section">
          <div className="wf-settings-sec-title">{t('workflowDesigner.systemPrompt')}</div>
          <div className="wf-config-hint" style={{ margin: '6px 0 4px' }}>
            {t('workflowDesigner.systemPromptHint')}
          </div>
          <Input.TextArea
            rows={4}
            maxLength={4000}
            showCount
            value={prompt}
            placeholder={t('workflowDesigner.systemPromptPlaceholder')}
            onChange={(e) => setPrompt(e.target.value)}
          />
        </div>
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
          <div className="wf-settings-sec-title">{t('workflowDesigner.quickInputs')}</div>
          <div className="wf-config-hint" style={{ margin: '6px 0 4px' }}>
            {t('workflowDesigner.quickInputsHint')}
          </div>
          <div className="wf-quick-inputs-list">
            {quickInputs.map((text, index) => (
              <div key={index} className="wf-quick-inputs-row">
                <Input
                  value={text}
                  maxLength={200}
                  placeholder={t('workflowDesigner.quickInputsPlaceholder')}
                  onChange={(e) => setQuickInputs(quickInputs.map((x, i) => (i === index ? e.target.value : x)))}
                />
                <Tooltip title={t('workflowDesigner.quickInputsRemove')}>
                  <Button
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    aria-label={t('workflowDesigner.quickInputsRemove')}
                    onClick={() => setQuickInputs(quickInputs.filter((_, i) => i !== index))}
                  />
                </Tooltip>
              </div>
            ))}
            {quickInputs.length < MAX_QUICK_INPUTS && (
              <Button
                type="dashed"
                icon={<PlusOutlined />}
                block
                onClick={() => setQuickInputs([...quickInputs, ''])}
              >
                {t('workflowDesigner.quickInputsAdd')}
              </Button>
            )}
          </div>
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
