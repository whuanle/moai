/**
 * 右侧配置抽屉：编辑画布节点的表单数据（标题/描述 + 类型专属配置 + 输入/输出字段）.
 * 读写走 FlowGram 节点表单模型（getFormModel），保存仍以画布 toJSON 为准.
 */

import { useEffect, useMemo, useState } from 'react'
import { Button, Input, Select, Space } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { getFormModel } from '@flowgram.ai/form-core'
import { FormModelV2, isFormModelV2 } from '@flowgram.ai/node'
import { useClientContext } from '@flowgram.ai/free-layout-editor'

import { getTeamGatewayModels } from '@/api/gateway'
import { getTeamPlugins } from '@/api/team-plugin'
import { FieldsEditor } from './FieldsEditor'
import type { FieldBinding, OutputField } from './types'

export interface ConfigPanelProps {
  teamId: number
  nodeId: string | null
  onClose: () => void
}

interface NodeFormData {
  title?: string
  content?: string
  inputs?: Record<string, FieldBinding>
  outputs?: OutputField[]
  settings?: { aiModelId?: string; pluginKey?: string; code?: string }
}

export function ConfigPanel({ teamId, nodeId, onClose }: ConfigPanelProps) {
  const { t } = useTranslation()
  const { document } = useClientContext()

  const [formData, setFormData] = useState<NodeFormData | null>(null)
  const [nodeType, setNodeType] = useState<string>('')
  // 保存原始快照用于「取消」恢复
  const [snapshot, setSnapshot] = useState<NodeFormData | null>(null)

  const [modelOptions, setModelOptions] = useState<{ value: string; label: string }[]>([])
  const [pluginOptions, setPluginOptions] = useState<{ value: string; label: string }[]>([])

  useEffect(() => {
    if (!teamId) return
    void getTeamGatewayModels(teamId)
      .then((items) =>
        setModelOptions(
          items
            .filter((m) => m.aiModelId)
            .map((m) => ({ value: String(m.aiModelId), label: m.name || String(m.aiModelId) })),
        ),
      )
      .catch(() => setModelOptions([]))
    void getTeamPlugins(teamId)
      .then((res) =>
        setPluginOptions(
          (res.items ?? [])
            .filter((p) => p.pluginKey)
            .map((p) => ({ value: String(p.pluginKey), label: p.pluginName || String(p.pluginKey) })),
        ),
      )
      .catch(() => setPluginOptions([]))
  }, [teamId])

  useEffect(() => {
    if (!nodeId) {
      setFormData(null)
      return
    }
    const node = document.getAllNodes().find((n) => n.id === nodeId)
    const model = node ? getFormModel(node) : undefined
    const formModel = model && isFormModelV2(model) ? (model as FormModelV2) : undefined
    if (!formModel) {
      setFormData(null)
      return
    }
    const data = {
      title: formModel.getValueIn('title'),
      content: formModel.getValueIn('content'),
      inputs: formModel.getValueIn('inputs'),
      outputs: formModel.getValueIn('outputs'),
      settings: formModel.getValueIn('settings'),
    } as NodeFormData
    setNodeType(String(node?.flowNodeType ?? ''))
    setFormData(data)
    setSnapshot(JSON.parse(JSON.stringify(data)) as NodeFormData)
  }, [nodeId, document])

  const title = useMemo(() => {
    if (!formData) return ''
    return t(`workflowDesigner.nodeTitle.${nodeType}`, { defaultValue: formData.title ?? nodeType })
  }, [formData, nodeType, t])

  if (!nodeId || !formData) return null

  const patchForm = (patch: Partial<NodeFormData>) => setFormData((prev) => (prev ? { ...prev, ...patch } : prev))

  const applyToNode = (data: NodeFormData) => {
    const node = document.getAllNodes().find((n) => n.id === nodeId)
    const model = node ? getFormModel(node) : undefined
    const formModel = model && isFormModelV2(model) ? (model as FormModelV2) : undefined
    if (!formModel) return
    formModel.setValueIn('title', data.title ?? '')
    formModel.setValueIn('content', data.content ?? '')
    formModel.setValueIn('inputs', data.inputs ?? {})
    formModel.setValueIn('outputs', data.outputs ?? [])
    formModel.setValueIn('settings', data.settings ?? {})
  }

  const handleSave = () => {
    applyToNode(formData)
    onClose()
  }

  const handleCancel = () => {
    if (snapshot) applyToNode(snapshot)
    onClose()
  }

  return (
    <div className="wf-config-panel">
      <div className="wf-config-header">
        <span className="wf-config-title">{title}</span>
        <Button type="text" size="small" icon={<DeleteOutlined rotate={135} />} onClick={onClose} />
      </div>
      <div className="wf-config-body">
        <div className="wf-config-section">
          <label>{t('workflowDesigner.nodeName')}</label>
          <Input value={formData.title ?? ''} onChange={(e) => patchForm({ title: e.target.value })} />
        </div>
        <div className="wf-config-section">
          <label>{t('workflowDesigner.nodeDesc')}</label>
          <Input value={formData.content ?? ''} onChange={(e) => patchForm({ content: e.target.value })} />
        </div>

        {nodeType === 'aiChat' && (
          <div className="wf-config-section">
            <label>{t('workflowDesigner.aiModel')}</label>
            <Select
              showSearch
              optionFilterProp="label"
              value={formData.settings?.aiModelId || undefined}
              placeholder={t('workflowDesigner.aiModelPlaceholder')}
              options={modelOptions}
              onChange={(v) => patchForm({ settings: { ...formData.settings, aiModelId: v } })}
            />
          </div>
        )}

        {nodeType === 'plugin' && (
          <div className="wf-config-section">
            <label>{t('workflowDesigner.pluginKey')}</label>
            <Select
              showSearch
              optionFilterProp="label"
              value={formData.settings?.pluginKey || undefined}
              placeholder={t('workflowDesigner.pluginKeyPlaceholder')}
              options={pluginOptions}
              onChange={(v) => patchForm({ settings: { ...formData.settings, pluginKey: v } })}
            />
          </div>
        )}

        {nodeType === 'javaScript' && (
          <div className="wf-config-section">
            <label>{t('workflowDesigner.jsCode')}</label>
            <Input.TextArea
              rows={10}
              value={formData.settings?.code ?? ''}
              onChange={(e) => patchForm({ settings: { ...formData.settings, code: e.target.value } })}
              className="wf-config-code"
            />
            <div className="wf-config-hint">{t('workflowDesigner.jsCodeHint')}</div>
          </div>
        )}

        {nodeType === 'condition' && (
          <div className="wf-config-hint">{t('workflowDesigner.conditionHint')}</div>
        )}

        <FieldsEditor
          nodeId={nodeId}
          nodeType={nodeType}
          inputs={formData.inputs ?? {}}
          outputs={formData.outputs ?? []}
          onChange={(inputs, outputs) => patchForm({ inputs, outputs })}
        />
      </div>
      <div className="wf-config-footer">
        <Space>
          <Button onClick={handleCancel}>{t('workflowDesigner.cancel')}</Button>
          <Button type="primary" onClick={handleSave}>
            {t('workflowDesigner.confirm')}
          </Button>
        </Space>
      </div>
    </div>
  )
}
