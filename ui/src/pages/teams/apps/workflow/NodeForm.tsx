/**
 * 节点内配置表单（FastGPT 风格）：配置直接在画布节点卡片内编辑，无右侧配置面板.
 * 通过 useNodeRender().data / updateData 读写节点数据，变更自动触发画布内容事件.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { AutoComplete, Button, Input, InputNumber, Popconfirm, Select, Tooltip } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useClientContext, useNodeRender, WorkflowDragService, WorkflowNodeLinesData, WorkflowNodePortsData } from '@flowgram.ai/free-layout-editor'

import { getWikis } from '@/api/wiki'
import { getTeamPlugins } from '@/api/team-plugin'
import { getTeamGatewayModels } from '@/api/gateway'
import { useWorkflowDesignerStore } from './store'
import { inputsFromPluginSchema, outputsFromPluginSchema } from './utils'
import {
  EXPRESSION_TYPE_OPTIONS,
  FIELD_TYPE_OPTIONS,
  useVariableOptions,
} from './node-form-shared'
import { RefValueInput, SectionTitle, TypeSelect } from './node-form-widgets'
import { HttpNodeForm } from './HttpNodeForm'
import type { ClassifierClassDef, FieldBinding, NodeSettings, OutputField, SwitchBranchDef } from './types'

/** 条件脚本模式默认脚本 */
const CONDITION_DEFAULT_SCRIPT = `function condition(inputs, sys, nodes, system) {
  // 返回 true 走「真」分支，false 走「假」分支
  return true
}`

interface NodeData {
  key?: string
  title?: string
  content?: string
  branches?: SwitchBranchDef[]
  classes?: ClassifierClassDef[]
  inputs?: Record<string, FieldBinding>
  outputs?: OutputField[]
  settings?: NodeSettings
}

// ==================== 节点 Key（引用前缀，写 data.key；名称/描述在卡片头部点击编辑） ====================

function NodeKeySection({
  data,
  nodeId,
  nodeType,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  nodeType: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  // start/end 是固定单例节点，key 被引擎与引用前缀（start.*）依赖，不可改
  const fixedKey = nodeType === 'start' || nodeType === 'end'
  const effectiveKey = String(data.key ?? '').trim() || nodeId
  return (
    <Tooltip title={t('workflowDesigner.nodeKeyTip')} placement="top">
      <Input
        size="small"
        className="wf-node-key-input"
        value={fixedKey ? nodeId : effectiveKey}
        disabled={fixedKey}
        placeholder={t('workflowDesigner.nodeKey')}
        prefix={<span className="wf-node-key-prefix">{t('workflowDesigner.nodeKey')}</span>}
        onChange={(e) => onUpdateData({ key: e.target.value.replace(/[^a-zA-Z0-9_]/g, '') })}
      />
    </Tooltip>
  )
}

// ==================== 条件/多条件：表达式编辑与分支绑定 ====================

/** 节点出边（供分支绑定展示） */
function useNodeOutLines(nodeId: string, version: number) {
  const { document } = useClientContext()
  return useMemo(
    () =>
      (document.getAllNodes().find((n) => n.id === nodeId)?.getData(WorkflowNodeLinesData)?.outputLines ?? []).map((line) => ({
        id: line.id,
        from: line.from?.id,
        to: line.to?.id ?? '',
        fromPort: line.fromPort?.portID,
      })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [document, nodeId, version],
  )
}

/** 按表达式类型渲染值编辑器（variable/jsonpath 用变量提示，interpolation/fixed 用文本输入） */
function ExpressionValueEditor({
  binding,
  options,
  onChange,
}: {
  binding: FieldBinding
  options: { value: string; label: string }[]
  onChange: (patch: Partial<FieldBinding>) => void
}) {
  const { t } = useTranslation()

  if (binding.expressionType === 'variable' || binding.expressionType === 'jsonpath') {
    return (
      <RefValueInput
        value={binding.value}
        options={options}
        placeholder={binding.expressionType === 'jsonpath' ? '$.nodes.search.hasResult' : 'system.xxx / start.xxx'}
        onChange={(v) => onChange({ value: v })}
      />
    )
  }

  return (
    <Input
      size="small"
      value={binding.value}
      placeholder={binding.expressionType === 'interpolation' ? '{start.query} 模板' : t('workflowDesigner.exprFixed')}
      onChange={(e) => onChange({ value: e.target.value })}
    />
  )
}

/** 条件节点绑定模式：单一条件表达式（不带字段名/必填等冗余） */
function ConditionBindingEditor({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const binding = data.inputs?.condition ?? { expressionType: 'variable', value: '', required: true }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.conditionField')} />
      <div className="wf-b-row-value">
        <Select
          size="small"
          className="wf-field-expr-type"
          popupMatchSelectWidth={false}
          value={binding.expressionType ?? 'variable'}
          onChange={(v) => onUpdateData({ inputs: { ...data.inputs, condition: { ...binding, expressionType: v as FieldBinding['expressionType'] } } })}
          options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
        />
        <ExpressionValueEditor
          binding={binding}
          options={options}
          onChange={(patch) => onUpdateData({ inputs: { ...data.inputs, condition: { ...binding, ...patch } } })}
        />
      </div>
    </div>
  )
}

/** 条件节点真/假分支绑定：满足时/不满足时 固定顺序在左，绑定的节点在右（存 config.trueTarget，保存时写出到边标记） */
function ConditionBranchBinding({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const editorJSON = useWorkflowDesignerStore((s) => s.editorJSON ?? s.initialData)
  const outLines = useNodeOutLines(nodeId, 0)
  const trueTarget = String(data.settings?.trueTarget ?? '')

  if (outLines.length !== 2) {
    return (
      <div className="wf-node-sec">
        <SectionTitle text={t('workflowDesigner.branchBinding')} />
        <div className="wf-config-hint">{t('workflowDesigner.branchBindingNeedTwo')}</div>
      </div>
    )
  }

  // 解析当前满足/不满足分别绑定的目标节点：优先 trueTarget 配置，否则按端口位置（true 端口在顶部）
  let trueTo: string
  let falseTo: string
  if (trueTarget && outLines.some((l) => l.to === trueTarget)) {
    trueTo = trueTarget
    falseTo = outLines.find((l) => l.to !== trueTarget)?.to ?? ''
  } else {
    trueTo = outLines.find((l) => String(l.fromPort) !== 'false')?.to ?? ''
    falseTo = outLines.find((l) => String(l.fromPort) === 'false')?.to ?? ''
  }

  const targetTitle = (id: string) => {
    const target = editorJSON?.nodes?.find((n) => n.id === id)
    return String(target?.data?.title ?? id)
  }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.branchBinding')} />
      <div className="wf-branch-row">
        <span className="wf-branch-label">
          <span className="wf-branch-dot wf-branch-dot-true" />
          {t('workflowDesigner.branchTrue2')}
        </span>
        <Select
          size="small"
          className="wf-branch-target-select"
          popupMatchSelectWidth={false}
          value={trueTo}
          onChange={(v) => onUpdateData({ settings: { ...data.settings, trueTarget: v } })}
          options={outLines.map((l) => ({ value: l.to, label: targetTitle(l.to) }))}
        />
      </div>
      <div className="wf-branch-row">
        <span className="wf-branch-label">
          <span className="wf-branch-dot wf-branch-dot-false" />
          {t('workflowDesigner.branchFalse2')}
        </span>
        <span className="wf-branch-target">{targetTitle(falseTo)}</span>
      </div>
    </div>
  )
}

/** 多条件节点分支编辑：每个分支一条出边（data-port-id 动态端口）+ 顺序条件 */
function SwitchBranchesEditor({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const branches = data.branches ?? []
  const client = useClientContext()
  const rootRef = useRef<HTMLDivElement>(null)

  // 分支端口锚点是自定义 DOM（data-port-id），FlowGram 的节点拖拽监听在节点 DOM 上且先于 React 合成事件触发，
  // 因此用捕获阶段原生监听阻断节点拖拽，并直接调用 startDrawingLine 启动画线
  useEffect(() => {
    const el = rootRef.current
    if (!el) return
    const onPortMouseDown = (e: MouseEvent) => {
      const portEl = (e.target as HTMLElement | null)?.closest?.('[data-port-id]') as HTMLElement | null
      if (!portEl) return
      e.stopPropagation()
      e.preventDefault()
      const branchId = portEl.getAttribute('data-port-id') ?? ''
      const entity = client.document.getAllNodes().find((n) => n.id === nodeId)
      const port = entity?.getData(WorkflowNodePortsData)?.getPortEntityByKey('output', branchId)
      if (!port) return
      void client.get(WorkflowDragService).startDrawingLine(port, { clientX: e.clientX, clientY: e.clientY })
    }
    el.addEventListener('mousedown', onPortMouseDown, true)
    return () => el.removeEventListener('mousedown', onPortMouseDown, true)
  }, [client, nodeId])

  const update = (index: number, patch: Partial<SwitchBranchDef>) => {
    onUpdateData({ branches: branches.map((b, i) => (i === index ? { ...b, ...patch } : b)) })
  }

  const remove = (index: number) => {
    onUpdateData({ branches: branches.filter((_, i) => i !== index) })
  }

  const add = () => {
    let id = `b${branches.length + 1}`
    while (branches.some((b) => b.id === id) || id === 'else') id += 'x'
    onUpdateData({ branches: [...branches, { id, label: `条件 ${branches.length + 1}`, binding: { expressionType: 'variable', value: '', required: true } }] })
  }

  return (
    <div className="wf-node-sec" ref={rootRef}>
      <SectionTitle text={t('workflowDesigner.switchBranches')} />
      {branches.map((branch, index) => (
        <div key={branch.id} className="wf-b-row">
          <div className="wf-b-row-top">
            <span
              className="wf-switch-port"
              data-port-id={branch.id}
              data-port-type="output"
              data-port-location="right"
              title={`${t('workflowDesigner.branch')} ${branch.id}`}
            />
            <Input
              size="small"
              value={branch.label}
              onChange={(e) => update(index, { label: e.target.value })}
              placeholder={t('workflowDesigner.branchLabel')}
              className="wf-field-name"
            />
            {branches.length > 1 && (
              <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(index)}>
                <Button size="small" type="text" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            )}
          </div>
          <div className="wf-b-row-value">
            <Select
              size="small"
              className="wf-field-expr-type"
              popupMatchSelectWidth={false}
              value={branch.binding.expressionType ?? 'variable'}
              onChange={(v) => update(index, { binding: { ...branch.binding, expressionType: v as FieldBinding['expressionType'] } })}
              options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
            />
            <ExpressionValueEditor
              binding={branch.binding}
              options={options}
              onChange={(patch) => update(index, { binding: { ...branch.binding, ...patch } })}
            />
          </div>
        </div>
      ))}
      <div className="wf-b-row">
        <div className="wf-b-row-top">
          <span
            className="wf-switch-port wf-switch-port-else"
            data-port-id="else"
            data-port-type="output"
            data-port-location="right"
            title="else"
          />
          <span className="wf-switch-else-label">else</span>
        </div>
      </div>
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t('workflowDesigner.addBranch')}
      </Button>
    </div>
  )
}

// ==================== 问题分类节点：模型选择 + 背景知识/聊天记录 + 分类列表（动态端口） ====================

/** 问题分类节点的 AI 模型选择（团队网关可用模型） */
function ClassifierModelSelect({
  data,
  onUpdateData,
}: {
  data: NodeData
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const teamId = useWorkflowDesignerStore((s) => s.teamId)
  const [models, setModels] = useState<{ value: string; label: string }[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!teamId) return
    let cancelled = false
    setLoading(true)
    getTeamGatewayModels(teamId)
      .then((res) => {
        if (cancelled) return
        setModels(
          res
            .filter((m) => m.aiModelId)
            .map((m) => ({ value: String(m.aiModelId), label: m.name || m.modelId || '-' })),
        )
      })
      .catch(() => {
        if (!cancelled) setModels([])
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [teamId])

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.classifierModel')} />
      <Select
        size="small"
        style={{ width: '100%' }}
        loading={loading}
        showSearch
        optionFilterProp="label"
        value={data.settings?.aiModelId || undefined}
        placeholder={t('workflowDesigner.classifierModelPlaceholder')}
        options={models}
        onChange={(v) => onUpdateData({ settings: { ...data.settings, aiModelId: v } })}
      />
    </div>
  )
}

/** 问题分类节点的背景知识（可选，补充分类判断所需的领域信息） */
function ClassifierBackgroundSection({
  data,
  onUpdateData,
}: {
  data: NodeData
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.classifierBackground')} />
      <Input.TextArea
        size="small"
        rows={3}
        value={data.settings?.backgroundKnowledge ?? ''}
        placeholder={t('workflowDesigner.classifierBackgroundPlaceholder')}
        onChange={(e) => onUpdateData({ settings: { ...data.settings, backgroundKnowledge: e.target.value } })}
      />
    </div>
  )
}

const CLASSIFIER_HISTORY_MAX = 50

/** 问题分类节点的聊天记录条数（从历史消息变量中携带最近 N 条参与分类） */
function ClassifierHistoryCountSection({
  data,
  onUpdateData,
}: {
  data: NodeData
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const value = typeof data.settings?.historyCount === 'number' ? data.settings.historyCount : 6
  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.classifierHistoryCount')} />
      <InputNumber
        size="small"
        min={0}
        max={CLASSIFIER_HISTORY_MAX}
        precision={0}
        style={{ width: 88 }}
        value={value}
        onChange={(v) => {
          if (v == null) return
          onUpdateData({ settings: { ...data.settings, historyCount: Math.min(Math.max(Number(v), 0), CLASSIFIER_HISTORY_MAX) } })
        }}
      />
      <div className="wf-config-hint">{t('workflowDesigner.classifierHistoryCountHint')}</div>
    </div>
  )
}

/** 问题分类节点的用户问题输入绑定（必填） */
function ClassifierQueryBinding({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const binding = data.inputs?.query ?? { expressionType: 'variable', value: '', required: true }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.classifierQuery')} />
      <div className="wf-b-row-value">
        <Select
          size="small"
          className="wf-field-expr-type"
          popupMatchSelectWidth={false}
          value={binding.expressionType ?? 'variable'}
          onChange={(v) => onUpdateData({ inputs: { ...data.inputs, query: { ...binding, expressionType: v as FieldBinding['expressionType'] } } })}
          options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
        />
        <ExpressionValueEditor
          binding={binding}
          options={options}
          onChange={(patch2) => onUpdateData({ inputs: { ...data.inputs, query: { ...binding, ...patch2 } } })}
        />
      </div>
    </div>
  )
}

/** 问题分类节点的历史消息输入绑定（可选，[{role, content}] 数组） */
function ClassifierHistoryBinding({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const binding = data.inputs?.history ?? { expressionType: 'variable', value: '', required: false }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.classifierHistory')} />
      <div className="wf-b-row-value">
        <Select
          size="small"
          className="wf-field-expr-type"
          popupMatchSelectWidth={false}
          value={binding.expressionType ?? 'variable'}
          onChange={(v) => onUpdateData({ inputs: { ...data.inputs, history: { ...binding, expressionType: v as FieldBinding['expressionType'] } } })}
          options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
        />
        <ExpressionValueEditor
          binding={binding}
          options={options}
          onChange={(patch2) => onUpdateData({ inputs: { ...data.inputs, history: { ...binding, ...patch2 } } })}
        />
      </div>
      <div className="wf-config-hint">{t('workflowDesigner.classifierHistoryHint')}</div>
    </div>
  )
}

/** 问题分类节点的分类列表编辑：每个分类一条出边（data-port-id 动态端口）+ 分类名称 */
function ClassifierClassesEditor({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const classes = data.classes ?? []
  const client = useClientContext()
  const rootRef = useRef<HTMLDivElement>(null)

  // 分类端口锚点是自定义 DOM（data-port-id），与多条件分支相同：捕获阶段原生监听阻断节点拖拽，直接启动画线
  useEffect(() => {
    const el = rootRef.current
    if (!el) return
    const onPortMouseDown = (e: MouseEvent) => {
      const portEl = (e.target as HTMLElement | null)?.closest?.('[data-port-id]') as HTMLElement | null
      if (!portEl) return
      e.stopPropagation()
      e.preventDefault()
      const classId = portEl.getAttribute('data-port-id') ?? ''
      const entity = client.document.getAllNodes().find((n) => n.id === nodeId)
      const port = entity?.getData(WorkflowNodePortsData)?.getPortEntityByKey('output', classId)
      if (!port) return
      void client.get(WorkflowDragService).startDrawingLine(port, { clientX: e.clientX, clientY: e.clientY })
    }
    el.addEventListener('mousedown', onPortMouseDown, true)
    return () => el.removeEventListener('mousedown', onPortMouseDown, true)
  }, [client, nodeId])

  const update = (index: number, patch: Partial<ClassifierClassDef>) => {
    onUpdateData({ classes: classes.map((c, i) => (i === index ? { ...c, ...patch } : c)) })
  }

  const remove = (index: number) => {
    onUpdateData({ classes: classes.filter((_, i) => i !== index) })
  }

  const add = () => {
    let id = `c${classes.length + 1}`
    while (classes.some((c) => c.id === id)) id += 'x'
    onUpdateData({ classes: [...classes, { id, label: '' }] })
  }

  return (
    <div className="wf-node-sec" ref={rootRef}>
      <SectionTitle text={t('workflowDesigner.classifierClasses')} />
      {classes.map((cls, index) => (
        <div key={cls.id} className="wf-b-row">
          <div className="wf-b-row-top">
            <span
              className="wf-switch-port"
              data-port-id={cls.id}
              data-port-type="output"
              data-port-location="right"
              title={`${t('workflowDesigner.classifierClass')} ${cls.id}`}
            />
            <Input
              size="small"
              value={cls.label}
              onChange={(e) => update(index, { label: e.target.value })}
              placeholder={t('workflowDesigner.classifierClassPlaceholder')}
              className="wf-field-name"
            />
            {classes.length > 1 && (
              <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(index)}>
                <Button size="small" type="text" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            )}
          </div>
        </div>
      ))}
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t('workflowDesigner.classifierAddClass')}
      </Button>
    </div>
  )
}

// ==================== 输出参数编辑（javaScript / plugin：输出是用户声明的元数据） ====================

function OutputsEditor({ data, onUpdateData }: { data: NodeData; onUpdateData: (patch: Partial<NodeData>) => void }) {
  const { t } = useTranslation()
  const outputs = data.outputs ?? []

  const update = (index: number, p: Partial<OutputField>) => {
    onUpdateData({ outputs: outputs.map((o, i) => (i === index ? { ...o, ...p } : o)) })
  }
  const remove = (index: number) => {
    onUpdateData({ outputs: outputs.filter((_, i) => i !== index) })
  }
  const add = () => {
    onUpdateData({ outputs: [...outputs, { name: `out_${Date.now().toString(36)}`, fieldType: 'string', description: '' }] })
  }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.outputFields')} />
      {outputs.map((o, i) => (
        <div key={`${o.name}-${i}`} className="wf-b-row">
          <div className="wf-b-row-top">
            <Input
              size="small"
              value={o.name}
              onChange={(e) => update(i, { name: e.target.value.replace(/[^a-zA-Z0-9_]/g, '') })}
              placeholder="output_name"
              className="wf-field-name"
            />
            <TypeSelect value={o.fieldType} onChange={(v) => update(i, { fieldType: v })} />
            <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(i)}>
              <Button size="small" type="text" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          </div>
          <Input
            size="small"
            value={o.description ?? ''}
            onChange={(e) => update(i, { description: e.target.value })}
            placeholder={t('workflowDesigner.fieldDesc')}
          />
        </div>
      ))}
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t('workflowDesigner.addOutput')}
      </Button>
    </div>
  )
}

// ==================== 输入绑定列表（aiChat / plugin 参数、condition 条件、end 汇总） ====================

function BindingsSection({
  data,
  nodeId,
  onUpdateData,
  showType = true,
  titleKey,
  addLabelKey,
  namePlaceholder = 'field_name',
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
  allowRename?: boolean
  showType?: boolean
  titleKey: string
  addLabelKey: string
  namePlaceholder?: string
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const inputs = data.inputs ?? {}
  const names = Object.keys(inputs)

  const update = (name: string, patch: Partial<FieldBinding>) => {
    onUpdateData({ inputs: { ...inputs, [name]: { ...inputs[name], ...patch } } })
  }
  const rename = (oldName: string, newName: string) => {
    const next: Record<string, FieldBinding> = {}
    for (const [n, b] of Object.entries(inputs)) next[n === oldName ? newName : n] = b
    onUpdateData({ inputs: next })
  }
  const remove = (name: string) => {
    const next = { ...inputs }
    delete next[name]
    onUpdateData({ inputs: next })
  }
  const add = () => {
    let name = `field_${Date.now().toString(36)}`
    while (name in inputs) name += 'x'
    onUpdateData({ inputs: { ...inputs, [name]: { expressionType: 'variable', value: '', required: false, fieldType: 'string' } } })
  }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t(titleKey)} />
      {names.map((name) => {
        const binding = inputs[name]
        return (
          <div key={name} className="wf-b-row">
            <div className="wf-b-row-top">
              <Input
                size="small"
                value={name}
                onChange={(e) => rename(name, e.target.value)}
                placeholder={namePlaceholder}
                className="wf-field-name"
              />
              {showType && (
                <TypeSelect value={binding.fieldType} onChange={(v) => update(name, { fieldType: v })} />
              )}
              <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(name)}>
                <Button size="small" type="text" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </div>
            <div className="wf-b-row-value">
              <Select
                size="small"
                className="wf-field-expr-type"
                popupMatchSelectWidth={false}
                value={binding.expressionType ?? 'fixed'}
                onChange={(v) => update(name, { expressionType: v as FieldBinding['expressionType'] })}
                options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
              />
              {binding.expressionType === 'variable' || binding.expressionType === 'jsonpath' ? (
                <RefValueInput
                  value={binding.value}
                  options={options}
                  placeholder={
                    binding.expressionType === 'jsonpath' ? '$.nodes.search.hasResult' : 'system.xxx / start.xxx'
                  }
                  onChange={(v) => update(name, { value: v, expressionType: binding.expressionType })}
                />
              ) : binding.expressionType === 'interpolation' ? (
                <Input
                  size="small"
                  value={binding.value}
                  placeholder="{start.query} 模板"
                  onChange={(e) => update(name, { value: e.target.value })}
                />
              ) : (
                <Input
                  size="small"
                  value={binding.value}
                  placeholder={t('workflowDesigner.exprFixed')}
                  onChange={(e) => update(name, { value: e.target.value })}
                />
              )}
              <label className="wf-field-required" title={t('workflowDesigner.required')}>
                <input
                  type="checkbox"
                  checked={binding.required !== false}
                  onChange={(e) => update(name, { required: e.target.checked })}
                />
              </label>
            </div>
          </div>
        )
      })}
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t(addLabelKey)}
      </Button>
    </div>
  )
}

// ==================== 知识库检索：知识库选择/变量绑定（单个） + 召回条数 + 检索问题 ====================

const KNOWLEDGE_TOPK_MAX = 50

/** 知识库来源（单个）：下拉选择本团队知识库，或输入/选择上游变量运行时动态绑定 */
function KnowledgeWikiSelect({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const teamId = useWorkflowDesignerStore((s) => s.teamId)
  const variableOptions = useVariableOptions(nodeId)
  const [wikiOptions, setWikiOptions] = useState<{ value: string; label: string }[]>([])

  useEffect(() => {
    if (!teamId) return
    let cancelled = false
    getWikis(teamId)
      .then((res) => {
        if (cancelled) return
        setWikiOptions(
          (res.items ?? [])
            .filter((w) => w.wikiId != null)
            .map((w) => ({ value: String(w.wikiId), label: String(w.name ?? w.wikiId) })),
        )
      })
      .catch(() => {
        // 团队知识库列表加载失败时仅空选项，不阻塞节点编辑
      })
    return () => {
      cancelled = true
    }
  }, [teamId])

  // 当前值：变量绑定优先展示，否则展示静态选择的知识库 id
  const bound = data.inputs?.wikiId
  const value = bound && String(bound.value ?? '').trim() !== '' ? bound.value : data.settings?.wikiId ? String(data.settings.wikiId) : ''

  const handleChange = (raw: string) => {
    const v = raw.trim()
    if (v === '') {
      // 清空：同时移除静态选择与变量绑定
      const inputs = { ...(data.inputs ?? {}) }
      delete inputs.wikiId
      const { wikiId: _drop, ...restSettings } = data.settings ?? {}
      void _drop
      onUpdateData({ settings: restSettings, inputs })
      return
    }

    if (/^\d+$/.test(v)) {
      // 纯数字 → 静态选择知识库，移除变量绑定
      const inputs = { ...(data.inputs ?? {}) }
      delete inputs.wikiId
      onUpdateData({ settings: { ...data.settings, wikiId: Number(v) }, inputs })
      return
    }

    // 其余视为变量引用 → 写入输入绑定，移除静态选择
    const { wikiId: _drop, ...restSettings } = data.settings ?? {}
    void _drop
    onUpdateData({
      settings: restSettings,
      inputs: { ...(data.inputs ?? {}), wikiId: { expressionType: 'variable', value: v, required: false } },
    })
  }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.knowledgeWiki')} />
      <AutoComplete
        size="small"
        style={{ width: '100%' }}
        value={value}
        popupMatchSelectWidth={false}
        placeholder={t('workflowDesigner.knowledgeWikiPlaceholder')}
        options={[
          { label: t('workflowDesigner.knowledgeWikiGroup'), options: wikiOptions },
          { label: t('workflowDesigner.knowledgeVariableGroup'), options: variableOptions },
        ]}
        filterOption={(input, option) => {
          const o = option as { value?: string; label?: string }
          return (
            String(o?.value ?? '').toLowerCase().includes(input.toLowerCase()) ||
            String(o?.label ?? '').toLowerCase().includes(input.toLowerCase())
          )
        }}
        onChange={handleChange}
      />
      <div className="wf-b-row" style={{ marginTop: 8 }}>
        <span className="wf-out-name" style={{ whiteSpace: 'nowrap' }}>{t('workflowDesigner.knowledgeTopK')}</span>
        <InputNumber
          size="small"
          min={1}
          max={KNOWLEDGE_TOPK_MAX}
          precision={0}
          style={{ width: 88 }}
          value={typeof data.settings?.topK === 'number' && data.settings.topK >= 1 ? data.settings.topK : 5}
          onChange={(v) => {
            if (v == null) return
            onUpdateData({ settings: { ...data.settings, topK: Math.min(Math.max(Number(v), 1), KNOWLEDGE_TOPK_MAX) } })
          }}
        />
      </div>
      <div className="wf-config-hint">{t('workflowDesigner.knowledgeWikiHint')}</div>
    </div>
  )
}

function KnowledgeQueryBinding({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const binding = data.inputs?.query ?? { expressionType: 'variable', value: '', required: true }

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.knowledgeQuery')} />
      <div className="wf-b-row-value">
        <Select
          size="small"
          className="wf-field-expr-type"
          popupMatchSelectWidth={false}
          value={binding.expressionType ?? 'variable'}
          onChange={(v) => onUpdateData({ inputs: { ...data.inputs, query: { ...binding, expressionType: v as FieldBinding['expressionType'] } } })}
          options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
        />
        <ExpressionValueEditor
          binding={binding}
          options={options}
          onChange={(patch2) => onUpdateData({ inputs: { ...data.inputs, query: { ...binding, ...patch2 } } })}
        />
      </div>
    </div>
  )
}

// ==================== 插件节点：团队工具选择 + 响应 schema 自动填充输出 ====================

interface TeamToolOption {
  value: string
  label: string
  description?: string
  schema: { name?: string | null; fieldType?: string | null; description?: string | null }[]
}

/** 团队可用插件工具列表（插件节点与团队工具 Tab 共用数据源） */
function useTeamTools(): { tools: TeamToolOption[]; loading: boolean } {
  const teamId = useWorkflowDesignerStore((s) => s.teamId)
  const [tools, setTools] = useState<TeamToolOption[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!teamId) return
    let cancelled = false
    setLoading(true)
    getTeamPlugins(teamId)
      .then((res) => {
        if (cancelled) return
        setTools(
          (res.items ?? []).map((item) => ({
            value: String(item.pluginName ?? ''),
            label: String(item.title || item.pluginName || ''),
            description: item.description ?? '',
            schema: item.responseSchema ?? [],
          })),
        )
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
  }, [teamId])

  return { tools, loading }
}

function PluginKeySelect({ data, onUpdateData }: { data: NodeData; onUpdateData: (patch: Partial<NodeData>) => void }) {
  const { t } = useTranslation()
  const { tools, loading } = useTeamTools()
  const pluginKey = String(data.settings?.pluginKey ?? '')

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.pluginKey')} />
      <Select
        size="small"
        style={{ width: '100%' }}
        loading={loading}
        value={pluginKey || undefined}
        placeholder={t('workflowDesigner.pluginKeyPlaceholder')}
        options={tools.map((tool) => ({ value: tool.value, label: tool.label, title: tool.description }))}
        onChange={(v) => {
          const tool = tools.find((tool2) => tool2.value === v)
          if (!tool) return
          // 自动按请求/响应 schema 覆盖输入与输出参数（schema 为空则保留现有值）
          const inputs = inputsFromPluginSchema(tool.schema)
          const outputs = outputsFromPluginSchema(tool.schema)
          onUpdateData({
            settings: { ...data.settings, pluginKey: tool.value },
            ...(Object.keys(inputs).length > 0 ? { inputs } : {}),
            ...(outputs.length > 0 ? { outputs } : {}),
          })
        }}
      />
      <div className="wf-config-hint">{t('workflowDesigner.pluginOutputsAuto')}</div>
    </div>
  )
}

// ==================== 插件节点：输入/输出由插件 schema 自动生成，仅可设置取值方式 ====================

/**
 * 插件节点输入参数（由插件请求 schema 自动生成，不可增删字段）：
 * 每个字段仅可设置取值方式（变量/JSONPath/插值/固定值）与是否必填.
 */
function PluginBindingsSection({
  data,
  nodeId,
  onUpdateData,
}: {
  data: NodeData
  nodeId: string
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const options = useVariableOptions(nodeId)
  const inputs = data.inputs ?? {}
  const names = Object.keys(inputs)

  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.inputFields')} />
      {names.length === 0 && <div className="wf-config-hint">{t('workflowDesigner.pluginInputsEmpty')}</div>}
      {names.map((name) => {
        const binding = inputs[name]
        const update = (patch: Partial<FieldBinding>) =>
          onUpdateData({ inputs: { ...inputs, [name]: { ...binding, ...patch } } })
        return (
          <div key={name} className="wf-b-row">
            <div className="wf-b-row-top">
              <span className="wf-field-name wf-field-name-readonly" title={name}>
                {name}
              </span>
              <TypeSelect value={binding.fieldType} onChange={(v) => update({ fieldType: v })} />
            </div>
            {binding.description && <div className="wf-field-desc-readonly">{binding.description}</div>}
            <div className="wf-b-row-value">
              <Select
                size="small"
                className="wf-field-expr-type"
                popupMatchSelectWidth={false}
                value={binding.expressionType ?? 'fixed'}
                onChange={(v) => update({ expressionType: v as FieldBinding['expressionType'] })}
                options={EXPRESSION_TYPE_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) }))}
              />
              {binding.expressionType === 'variable' || binding.expressionType === 'jsonpath' ? (
                <RefValueInput
                  value={binding.value}
                  options={options}
                  placeholder="system.xxx / start.xxx"
                  onChange={(v) => update({ value: v, expressionType: binding.expressionType })}
                />
              ) : binding.expressionType === 'interpolation' ? (
                <Input
                  size="small"
                  value={binding.value}
                  placeholder="{start.query} 模板"
                  onChange={(e) => update({ value: e.target.value })}
                />
              ) : (
                <Input
                  size="small"
                  value={binding.value}
                  placeholder={t('workflowDesigner.exprFixed')}
                  onChange={(e) => update({ value: e.target.value })}
                />
              )}
              <label className="wf-field-required" title={t('workflowDesigner.required')}>
                <input
                  type="checkbox"
                  checked={binding.required !== false}
                  onChange={(e2) => update({ required: e2.target.checked })}
                />
              </label>
            </div>
          </div>
        )
      })}
      <div className="wf-config-hint">{t('workflowDesigner.pluginInputsHint')}</div>
    </div>
  )
}

// ==================== 只读输出展示 ====================

function OutputsSection({ outputs }: { outputs: OutputField[] }) {
  const { t } = useTranslation()
  if (!outputs || outputs.length === 0) return null
  return (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.outputFields')} />
      <div className="wf-outs">
        {outputs.map((o, i) => (
          <div key={`${o.name}-${i}`} className="wf-out-row">
            {o.isRequired !== false && <span className="wf-out-req">*</span>}
            <span className="wf-out-name">{o.name}</span>
            <span className="wf-out-type">{o.fieldType}</span>
            {o.description && <span className="wf-out-desc" title={o.description}>{o.description}</span>}
          </div>
        ))}
      </div>
    </div>
  )
}

// ==================== 节点内表单（按类型分发） ====================

export function NodeForm({ nodeId, nodeType, readonly }: { nodeId: string; nodeType: string; readonly: boolean }) {
  const { t } = useTranslation()
  const { data, updateData } = useNodeRender()
  const { document } = useClientContext()
  const nodeData = (data ?? {}) as NodeData

  const patch = (p: Partial<NodeData>) => {
    if (readonly) return
    updateData({ ...nodeData, ...p })
  }

  // 多条件分支/问题分类节点数量变化后刷新动态端口（useDynamicPort 按 DOM [data-port-id] 计算）；
  // 首次挂载不刷新（FlowGram 初始化已计算，且刷新会触发内容变更导致加载即标脏）
  const dynamicPortCount =
    nodeType === 'switch'
      ? (nodeData.branches ?? []).length
      : nodeType === 'questionClassifier'
        ? (nodeData.classes ?? []).length
        : null
  const prevPortCount = useRef<number | null>(null)
  useEffect(() => {
    if (dynamicPortCount == null) return
    if (prevPortCount.current === null) {
      prevPortCount.current = dynamicPortCount
      return
    }
    if (prevPortCount.current === dynamicPortCount) return
    prevPortCount.current = dynamicPortCount
    const entity = document.getAllNodes().find((n) => n.id === nodeId)
    entity?.getData(WorkflowNodePortsData)?.updateAllPorts?.()
  }, [document, nodeId, dynamicPortCount])

  const body = (() => {
    switch (nodeType) {
      case 'start':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <div className="wf-node-sec">
              <SectionTitle text={t('workflowDesigner.inputFields')} />
              <StartInputsEditor data={nodeData} onUpdateData={patch} />
              <div className="wf-config-hint">{t('workflowDesigner.startInputsHint')}</div>
            </div>
          </>
        )
      case 'condition': {
        const scriptMode = String(nodeData.settings?.conditionScript ?? '') !== ''
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <div className="wf-node-sec">
              <SectionTitle text={t('workflowDesigner.conditionMode')} />
              <Select
                size="small"
                style={{ width: '100%' }}
                value={scriptMode ? 'script' : 'binding'}
                onChange={(v) =>
                  patch({
                    settings: {
                      ...nodeData.settings,
                      conditionScript: v === 'script' ? nodeData.settings?.conditionScript || CONDITION_DEFAULT_SCRIPT : undefined,
                    },
                  })
                }
                options={[
                  { value: 'binding', label: t('workflowDesigner.conditionModeBinding') },
                  { value: 'script', label: t('workflowDesigner.conditionModeScript') },
                ]}
              />
            </div>
            {scriptMode ? (
              <div className="wf-node-sec">
                <SectionTitle text={t('workflowDesigner.conditionScriptTitle')} />
                <Input.TextArea
                  size="small"
                  rows={6}
                  value={nodeData.settings?.conditionScript ?? ''}
                  onChange={(e) => patch({ settings: { ...nodeData.settings, conditionScript: e.target.value } })}
                  className="wf-config-code"
                />
                <div className="wf-config-hint">{t('workflowDesigner.conditionScriptHint')}</div>
              </div>
            ) : (
              <>
                <ConditionBindingEditor data={nodeData} nodeId={nodeId} onUpdateData={patch} />
                <ConditionBranchBinding data={nodeData} nodeId={nodeId} onUpdateData={patch} />
                <div className="wf-config-hint">{t('workflowDesigner.conditionHint')}</div>
              </>
            )}
          </>
        )
      }
      case 'switch':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <SwitchBranchesEditor data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <div className="wf-config-hint">{t('workflowDesigner.switchHint')}</div>
          </>
        )
      case 'aiChat':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <BindingsSection
              data={nodeData}
              nodeId={nodeId}
              onUpdateData={patch}
              titleKey="workflowDesigner.inputFields"
              addLabelKey="workflowDesigner.addInput"
            />
            <OutputsSection outputs={nodeData.outputs ?? []} />
          </>
        )
      case 'javaScript':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <div className="wf-node-sec">
              <SectionTitle text={t('workflowDesigner.jsCode')} />
              <Input.TextArea
                size="small"
                rows={7}
                value={nodeData.settings?.code ?? ''}
                onChange={(e) => patch({ settings: { ...nodeData.settings, code: e.target.value } })}
                className="wf-config-code"
              />
              <div className="wf-config-hint">{t('workflowDesigner.jsCodeHint')}</div>
            </div>
            <OutputsEditor data={nodeData} onUpdateData={patch} />
          </>
        )
      case 'plugin':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <PluginKeySelect data={nodeData} onUpdateData={patch} />
            <PluginBindingsSection data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <OutputsSection outputs={nodeData.outputs ?? []} />
          </>
        )
      case 'knowledgeSearch':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <KnowledgeWikiSelect data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <KnowledgeQueryBinding data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <OutputsSection outputs={nodeData.outputs ?? []} />
          </>
        )
      case 'http':
        return <HttpNodeForm data={nodeData} nodeId={nodeId} onUpdateData={patch} />
      case 'questionClassifier':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <ClassifierModelSelect data={nodeData} onUpdateData={patch} />
            <ClassifierBackgroundSection data={nodeData} onUpdateData={patch} />
            <ClassifierHistoryCountSection data={nodeData} onUpdateData={patch} />
            <ClassifierQueryBinding data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <ClassifierHistoryBinding data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <ClassifierClassesEditor data={nodeData} nodeId={nodeId} onUpdateData={patch} />
            <div className="wf-config-hint">{t('workflowDesigner.classifierHint')}</div>
          </>
        )
      default:
        return null
    }
  })()

  return <div className="wf-node-form">{body}</div>
}

// ==================== 开始节点的输入参数声明编辑 ====================

function StartInputsEditor({
  data,
  onUpdateData,
}: {
  data: NodeData
  onUpdateData: (patch: Partial<NodeData>) => void
}) {
  const { t } = useTranslation()
  const inputs = data.inputs ?? {}
  const names = Object.keys(inputs)

  const update = (name: string, p: Partial<FieldBinding>) => {
    onUpdateData({ inputs: { ...inputs, [name]: { ...inputs[name], ...p } } })
  }
  const rename = (oldName: string, newName: string) => {
    const next: Record<string, FieldBinding> = {}
    for (const [n, b] of Object.entries(inputs)) next[n === oldName ? newName : n] = b
    onUpdateData({ inputs: next })
  }
  const add = () => {
    let name = `param_${Date.now().toString(36)}`
    while (name in inputs) name += 'x'
    onUpdateData({ inputs: { ...inputs, [name]: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } })
  }
  const remove = (name: string) => {
    const next = { ...inputs }
    delete next[name]
    onUpdateData({ inputs: next })
  }

  return (
    <>
      {names.map((name) => (
        <div key={name} className="wf-b-row">
          <div className="wf-b-row-top">
            <Input
              size="small"
              value={name}
              onChange={(e) => rename(name, e.target.value)}
              placeholder="param_name"
              className="wf-field-name"
            />
            <Select
              size="small"
              value={inputs[name].fieldType ?? 'string'}
              onChange={(v) => update(name, { fieldType: v })}
              className="wf-field-type"
              popupMatchSelectWidth={false}
              options={FIELD_TYPE_OPTIONS.map((ft) => ({ value: ft, label: ft }))}
            />
            <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(name)}>
              <Button size="small" type="text" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          </div>
          <Input
            size="small"
            value={inputs[name].description ?? ''}
            onChange={(e) => update(name, { description: e.target.value })}
            placeholder={t('workflowDesigner.fieldDesc')}
          />
          <label className="wf-field-required">
            <input
              type="checkbox"
              checked={inputs[name].required !== false}
              onChange={(e) => update(name, { required: e.target.checked })}
            />
            {t('workflowDesigner.required')}
          </label>
        </div>
      ))}
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t('workflowDesigner.addInput')}
      </Button>
    </>
  )
}
