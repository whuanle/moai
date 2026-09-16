/**
 * 输入/输出字段编辑器（引擎契约的扁平结构）：
 * 输入 = 字段名 + 表达式类型（run/fixed/variable/jsonpath/interpolation）+ 值 + 必填；
 * 输出 = 字段名 + 类型 + 必填 + 描述.
 */

import { Button, Input, Popconfirm, Select, Tooltip } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { FieldBinding, OutputField } from './types'

const EXPRESSION_OPTIONS: { value: FieldBinding['expressionType']; labelKey: string; hint: string }[] = [
  { value: 'run', labelKey: 'workflowDesigner.exprRun', hint: '由运行时传入' },
  { value: 'fixed', labelKey: 'workflowDesigner.exprFixed', hint: '固定常量' },
  { value: 'variable', labelKey: 'workflowDesigner.exprVariable', hint: '变量引用，如 start.query' },
  { value: 'jsonpath', labelKey: 'workflowDesigner.exprJsonPath', hint: '$.nodes.a.b[*].c' },
  { value: 'interpolation', labelKey: 'workflowDesigner.exprInterp', hint: '模板 {start.query} 插值' },
]

const FIELD_TYPE_OPTIONS = ['string', 'number', 'boolean', 'object', 'map', 'array', 'dynamic']

export interface FieldsEditorProps {
  nodeId: string
  /** 哪个节点类型正在编辑（用于允许 start 的 run 表达式） */
  nodeType: string
  inputs: Record<string, FieldBinding>
  outputs: OutputField[]
  onChange: (inputs: Record<string, FieldBinding>, outputs: OutputField[]) => void
}

export function FieldsEditor({ nodeType, inputs, outputs, onChange }: FieldsEditorProps) {
  const { t } = useTranslation()
  const inputNames = Object.keys(inputs ?? {})
  const outputList = outputs ?? []

  const renameInput = (oldName: string, newName: string) => {
    const next: Record<string, FieldBinding> = {}
    for (const [name, binding] of Object.entries(inputs)) {
      next[name === oldName ? newName : name] = binding
    }
    onChange(next, outputList)
  }

  const updateInput = (name: string, patch: Partial<FieldBinding>) => {
    onChange({ ...inputs, [name]: { ...inputs[name], ...patch } }, outputList)
  }

  const addInput = () => {
    let name = `field_${Date.now().toString(36)}`
    while (name in inputs) name += 'x'
    onChange({ ...inputs, [name]: { expressionType: nodeType === 'start' ? 'run' : 'variable', value: '', required: false } }, outputList)
  }

  const removeInput = (name: string) => {
    const next = { ...inputs }
    delete next[name]
    onChange(next, outputList)
  }

  const updateOutput = (index: number, patch: Partial<OutputField>) => {
    const next = outputList.map((o, i) => (i === index ? { ...o, ...patch } : o))
    onChange(inputs, next)
  }

  const addOutput = () => {
    onChange(inputs, [...outputList, { name: `field_${Date.now().toString(36)}`, fieldType: 'string', isRequired: false }])
  }

  const removeOutput = (index: number) => {
    onChange(inputs, outputList.filter((_, i) => i !== index))
  }

  return (
    <div className="wf-fields">
      <div className="wf-fields-section">
        <div className="wf-fields-title">{t('workflowDesigner.inputFields')}</div>
        {inputNames.map((name) => {
          const binding = inputs[name]
          const isRun = binding.expressionType === 'run'
          return (
            <div key={name} className="wf-field-row">
              <div className="wf-field-row-top">
                <Input
                  size="small"
                  value={name}
                  disabled={isRun}
                  onChange={(e) => renameInput(name, e.target.value)}
                  placeholder="field_name"
                  className="wf-field-name"
                />
                <Select
                  size="small"
                  value={binding.expressionType}
                  onChange={(v) => updateInput(name, { expressionType: v })}
                  className="wf-field-expr"
                  options={EXPRESSION_OPTIONS.filter((o) => o.value !== 'run' || nodeType === 'start').map((o) => ({
                    value: o.value,
                    label: t(o.labelKey),
                  }))}
                />
                <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => removeInput(name)}>
                  <Button size="small" type="text" danger icon={<DeleteOutlined />} />
                </Popconfirm>
              </div>
              {!isRun && (
                <Input
                  size="small"
                  value={binding.value}
                  onChange={(e) => updateInput(name, { value: e.target.value })}
                  placeholder={EXPRESSION_OPTIONS.find((o) => o.value === binding.expressionType)?.hint}
                />
              )}
              <label className="wf-field-required">
                <input
                  type="checkbox"
                  checked={binding.required !== false}
                  onChange={(e) => updateInput(name, { required: e.target.checked })}
                />
                {t('workflowDesigner.required')}
              </label>
            </div>
          )
        })}
        <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={addInput}>
          {t('workflowDesigner.addInput')}
        </Button>
      </div>

      <div className="wf-fields-section">
        <div className="wf-fields-title">{t('workflowDesigner.outputFields')}</div>
        {outputList.map((output, index) => (
          <div key={`${output.name}-${index}`} className="wf-field-row">
            <div className="wf-field-row-top">
              <Input
                size="small"
                value={output.name}
                onChange={(e) => updateOutput(index, { name: e.target.value })}
                placeholder="field_name"
                className="wf-field-name"
              />
              <Tooltip title={output.description}>
                <Select
                  size="small"
                  value={FIELD_TYPE_OPTIONS.includes(output.fieldType) ? output.fieldType : 'dynamic'}
                  onChange={(v) => updateOutput(index, { fieldType: v })}
                  className="wf-field-expr"
                  options={FIELD_TYPE_OPTIONS.map((ft) => ({ value: ft, label: ft }))}
                />
              </Tooltip>
              <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => removeOutput(index)}>
                <Button size="small" type="text" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </div>
            <Input
              size="small"
              value={output.description ?? ''}
              onChange={(e) => updateOutput(index, { description: e.target.value })}
              placeholder={t('workflowDesigner.fieldDesc')}
            />
          </div>
        ))}
        <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={addOutput}>
          {t('workflowDesigner.addOutput')}
        </Button>
      </div>
    </div>
  )
}
