/**
 * 左侧浮层节点库（FastGPT 风格）：由画布左上「添加节点」唤出、← 收起，
 * 按分组两列网格展示可添加的节点，按住拖入画布（dataTransfer 携带类型 JSON）.
 * start/end 由默认画布提供，不在面板中.
 */

import { Button, Tooltip } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { NODE_TEMPLATES } from './constants'

/** 面板分组：控制流 / AI 能力 / 数据处理 / 集成 */
const GROUPS: { key: string; titleKey: string; types: string[] }[] = [
  { key: 'control', titleKey: 'workflowDesigner.groupControl', types: ['condition', 'switch'] },
  { key: 'ai', titleKey: 'workflowDesigner.groupAi', types: ['aiChat'] },
  { key: 'data', titleKey: 'workflowDesigner.groupData', types: ['javaScript'] },
  { key: 'integration', titleKey: 'workflowDesigner.groupIntegration', types: ['plugin'] },
]

export interface NodeLibraryPanelProps {
  onClose: () => void
}

export function NodeLibraryPanel({ onClose }: NodeLibraryPanelProps) {
  const { t } = useTranslation()

  const handleDragStart = (e: React.DragEvent, type: string) => {
    e.dataTransfer.setData('application/x-moai-node', JSON.stringify({ type }))
    e.dataTransfer.effectAllowed = 'copy'
  }

  return (
    <div className="wf-side-panel">
      <div className="wf-side-panel-head">
        <span className="wf-side-panel-title">{t('workflowDesigner.addNode')}</span>
        <Tooltip title={t('workflowDesigner.closePanel')} placement="right">
          <Button size="small" type="text" icon={<ArrowLeftOutlined />} onClick={onClose} />
        </Tooltip>
      </div>
      <div className="wf-side-panel-body">
        {GROUPS.map((group) => {
          const templates = NODE_TEMPLATES.filter((t2) => group.types.includes(t2.type))
          if (templates.length === 0) return null
          return (
            <div key={group.key} className="wf-node-panel-group">
              <div className="wf-node-panel-group-title">{t(group.titleKey)}</div>
              <div className="wf-node-panel-grid">
                {templates.map((template) => (
                  <Tooltip key={template.type} title={t(template.descKey, { defaultValue: template.desc })} placement="right">
                    <div
                      className="wf-node-panel-item"
                      draggable
                      onDragStart={(e) => handleDragStart(e, template.type)}
                    >
                      <span className="wf-node-panel-icon" style={{ background: template.color }}>
                        {template.icon}
                      </span>
                      <span className="wf-node-panel-name">{t(template.nameKey, { defaultValue: template.name })}</span>
                    </div>
                  </Tooltip>
                ))}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
