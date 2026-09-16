/**
 * 左侧节点面板：按住模板拖入画布（dataTransfer 携带类型 JSON）.
 */

import { useTranslation } from 'react-i18next'
import { NODE_TEMPLATES } from './constants'

export function NodePanel() {
  const { t } = useTranslation()

  const handleDragStart = (e: React.DragEvent, type: string) => {
    e.dataTransfer.setData('application/x-moai-node', JSON.stringify({ type }))
    e.dataTransfer.effectAllowed = 'copy'
  }

  return (
    <div className="wf-node-panel">
      <div className="wf-node-panel-title">{t('workflowDesigner.nodePanelTitle')}</div>
      {NODE_TEMPLATES.map((template) => (
        <div
          key={template.type}
          className="wf-node-panel-item"
          draggable
          onDragStart={(e) => handleDragStart(e, template.type)}
        >
          <span className="wf-node-panel-dot" style={{ background: template.color }} />
          <div className="wf-node-panel-text">
            <div className="wf-node-panel-name">{t(template.nameKey, { defaultValue: template.name })}</div>
            <div className="wf-node-panel-desc">{t(template.descKey, { defaultValue: template.desc })}</div>
          </div>
        </div>
      ))}
    </div>
  )
}
