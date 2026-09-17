/**
 * 流程全局变量抽屉：声明流程级变量（名称/类型/默认值/描述），
 * 所有节点可经 system.变量名 引用，调试运行与启动时可赋值覆盖默认值.
 */

import { Button, Input, Popconfirm, Select } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { GlobalVariableDef } from './types'

const FIELD_TYPE_OPTIONS = ['string', 'number', 'boolean', 'object', 'array', 'dynamic']

export interface VariablesDrawerProps {
  variables: GlobalVariableDef[]
  onChange: (variables: GlobalVariableDef[]) => void
}

export function VariablesDrawerBody({ variables, onChange }: VariablesDrawerProps) {
  const { t } = useTranslation()
  const list = variables ?? []

  const update = (index: number, patch: Partial<GlobalVariableDef>) => {
    onChange(list.map((v, i) => (i === index ? { ...v, ...patch } : v)))
  }

  const add = () => {
    onChange([...list, { name: `var_${Date.now().toString(36)}`, fieldType: 'string', defaultValue: '', description: '' }])
  }

  const remove = (index: number) => {
    onChange(list.filter((_, i) => i !== index))
  }

  return (
    <div className="wf-vars">
      <div className="wf-config-hint" style={{ marginBottom: 10 }}>
        {t('workflowDesigner.variablesHint')}
      </div>
      {list.map((variable, index) => (
        <div key={`${variable.name}-${index}`} className="wf-vars-row">
          <div className="wf-vars-row-top">
            <Input
              size="small"
              value={variable.name}
              onChange={(e) => update(index, { name: e.target.value.replace(/[^a-zA-Z0-9_]/g, '') })}
              placeholder="variable_name"
              className="wf-field-name"
            />
            <Select
              size="small"
              value={FIELD_TYPE_OPTIONS.includes(variable.fieldType ?? '') ? variable.fieldType : 'string'}
              onChange={(v) => update(index, { fieldType: v })}
              className="wf-field-type"
              popupMatchSelectWidth={false}
              options={FIELD_TYPE_OPTIONS.map((ft) => ({ value: ft, label: ft }))}
            />
            <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => remove(index)}>
              <Button size="small" type="text" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          </div>
          <div className="wf-vars-row-bottom">
            <Input
              size="small"
              value={variable.defaultValue ?? ''}
              onChange={(e) => update(index, { defaultValue: e.target.value })}
              placeholder={t('workflowDesigner.variableDefault')}
              className="wf-vars-half"
            />
            <Input
              size="small"
              value={variable.description ?? ''}
              onChange={(e) => update(index, { description: e.target.value })}
              placeholder={t('workflowDesigner.fieldDesc')}
              className="wf-vars-half"
            />
          </div>
        </div>
      ))}
      <Button size="small" type="dashed" block icon={<PlusOutlined />} onClick={add}>
        {t('workflowDesigner.addVariable')}
      </Button>
    </div>
  )
}
