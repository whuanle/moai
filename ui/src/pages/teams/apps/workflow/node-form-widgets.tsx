/**
 * 节点内配置表单共享小部件：区块标题/字段类型选择/变量值输入/插值模板输入.
 * 仅导出组件；常量与 hooks 在 node-form-shared.ts.
 */

import { useRef } from 'react'
import { AutoComplete, Select } from 'antd'

import { FIELD_TYPE_OPTIONS } from './node-form-shared'

/** 变量引用值输入（variable/jsonpath 用 AutoComplete 提示；可从下拉选变量，也可自由输入任意引用） */
export function RefValueInput({
  value,
  options,
  placeholder,
  onChange,
}: {
  value: string
  options: { value: string; label: string }[]
  placeholder: string
  onChange: (v: string) => void
}) {
  return (
    <AutoComplete
      size="small"
      value={value}
      options={options}
      popupMatchSelectWidth={false}
      filterOption={(input, option) =>
        String(option?.value ?? '').toLowerCase().includes(input.toLowerCase()) ||
        String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())
      }
      placeholder={placeholder}
      onChange={(v) => onChange(String(v))}
    />
  )
}

export function TypeSelect({ value, onChange }: { value: string | undefined; onChange: (v: string) => void }) {
  return (
    <Select
      size="small"
      value={value ?? 'dynamic'}
      onChange={onChange}
      className="wf-field-type"
      popupMatchSelectWidth={false}
      options={FIELD_TYPE_OPTIONS.map((ft) => ({ value: ft, label: ft }))}
    />
  )
}

export function SectionTitle({ text, extra }: { text: string; extra?: React.ReactNode }) {
  return (
    <div className="wf-sec-title">
      <span className="wf-sec-title-text">{text}</span>
      {extra}
    </div>
  )
}

/** 插值模板输入（http 节点）：自由输入 + 下拉选中变量后追加 {引用} 模板（保留已输入内容，便于拼接 URL/参数值） */
export function TemplateValueInput({
  value,
  options,
  placeholder,
  onChange,
}: {
  value: string
  options: { value: string; label: string }[]
  placeholder: string
  onChange: (v: string) => void
}) {
  // AutoComplete 选中选项时会先 onSelect 再 onChange（onChange 带选项原始值），用标记吞掉后一次 onChange
  const selectingRef = useRef(false)
  return (
    <AutoComplete
      size="small"
      value={value}
      options={options}
      popupMatchSelectWidth={false}
      filterOption={(input, option) =>
        String(option?.value ?? '').toLowerCase().includes(input.toLowerCase()) ||
        String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())
      }
      placeholder={placeholder}
      onSelect={(v) => {
        selectingRef.current = true
        onChange(`${value ?? ''}${String(v)}`)
      }}
      onChange={(v) => {
        if (selectingRef.current) {
          selectingRef.current = false
          return
        }

        onChange(String(v))
      }}
    />
  )
}
