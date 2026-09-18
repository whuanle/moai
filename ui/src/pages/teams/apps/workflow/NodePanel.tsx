/**
 * 左侧浮层面板（FastGPT 风格）：由画布左上「添加节点」唤出、← 收起。
 * 两个 Tab：
 * - 节点类型：按分组的内置节点，拖入画布后自行配置；
 * - 团队工具：团队可用插件工具，拖入画布直接生成已绑定该插件的插件节点（含按响应 schema 预填的输出参数）。
 */

import { useEffect, useState } from 'react'
import { Button, Empty, Spin, Tooltip } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { NODE_TEMPLATES } from './constants'
import { inputsFromPluginSchema, outputsFromPluginSchema } from './utils'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { useWorkflowDesignerStore } from './store'

/** 面板分组：控制流 / AI 能力 / 数据处理 / 集成 */
const GROUPS: { key: string; titleKey: string; types: string[] }[] = [
  { key: 'control', titleKey: 'workflowDesigner.groupControl', types: ['condition', 'switch'] },
  { key: 'ai', titleKey: 'workflowDesigner.groupAi', types: ['aiChat', 'knowledgeSearch', 'questionClassifier'] },
  { key: 'data', titleKey: 'workflowDesigner.groupData', types: ['javaScript'] },
  { key: 'integration', titleKey: 'workflowDesigner.groupIntegration', types: ['plugin', 'http'] },
]

type PanelTab = 'nodes' | 'tools'

export interface NodeLibraryPanelProps {
  onClose: () => void
}

export function NodeLibraryPanel({ onClose }: NodeLibraryPanelProps) {
  const { t } = useTranslation()
  const teamId = useWorkflowDesignerStore((s) => s.teamId)
  const [tab, setTab] = useState<PanelTab>('nodes')
  const [tools, setTools] = useState<TeamPluginItemType[] | null>(null)
  const [loading, setLoading] = useState(false)

  // 团队工具懒加载：首次切到工具 Tab 时拉取一次
  useEffect(() => {
    if (tab !== 'tools' || !teamId || tools) return
    let cancelled = false
    setLoading(true)
    getTeamPlugins(teamId)
      .then((res) => {
        if (!cancelled) setTools(res.items ?? [])
      })
      .catch(() => {
        if (!cancelled) setTools([])
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [tab, teamId, tools])

  const handleDragStart = (e: React.DragEvent, payload: object) => {
    e.dataTransfer.setData('application/x-moai-node', JSON.stringify(payload))
    e.dataTransfer.effectAllowed = 'copy'
  }

  return (
    <div className="wf-side-panel">
      <div className="wf-side-panel-head">
        <div className="wf-side-panel-tabs">
          <button
            type="button"
            className={`wf-side-panel-tab${tab === 'nodes' ? ' wf-side-panel-tab-active' : ''}`}
            onClick={() => setTab('nodes')}
          >
            {t('workflowDesigner.tabNodes')}
          </button>
          <button
            type="button"
            className={`wf-side-panel-tab${tab === 'tools' ? ' wf-side-panel-tab-active' : ''}`}
            onClick={() => setTab('tools')}
          >
            {t('workflowDesigner.tabTools')}
          </button>
        </div>
        <Tooltip title={t('workflowDesigner.closePanel')} placement="right">
          <Button size="small" type="text" icon={<ArrowLeftOutlined />} onClick={onClose} />
        </Tooltip>
      </div>
      <div className="wf-side-panel-body">
        {tab === 'nodes' ? (
          GROUPS.map((group) => {
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
                        onDragStart={(e) => handleDragStart(e, { type: template.type })}
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
          })
        ) : loading ? (
          <div className="wf-node-panel-loading"><Spin size="small" /></div>
        ) : !tools || tools.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('workflowDesigner.toolsEmpty')} className="wf-node-panel-empty" />
        ) : (
          <div className="wf-node-panel-group">
            <div className="wf-node-panel-group-title">{t('workflowDesigner.teamTools')}</div>
            <div className="wf-node-panel-grid wf-node-panel-grid-tools">
              {tools.map((tool) => {
                const outputs = outputsFromPluginSchema(tool.responseSchema)
                const inputs = inputsFromPluginSchema(tool.paramsSchema)
                return (
                  <Tooltip key={`${tool.pluginId ?? tool.pluginName}`} title={tool.description ?? tool.title} placement="right">
                    <div
                      className="wf-node-panel-item"
                      draggable
                      onDragStart={(e) =>
                        handleDragStart(e, {
                          type: 'plugin',
                          pluginKey: tool.pluginName,
                          title: tool.title,
                          description: tool.description ?? '',
                          inputs,
                          outputs,
                        })
                      }
                    >
                      <span className="wf-node-panel-icon">🔌</span>
                      <span className="wf-node-panel-name">{tool.title || tool.pluginName}</span>
                    </div>
                  </Tooltip>
                )
              })}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
