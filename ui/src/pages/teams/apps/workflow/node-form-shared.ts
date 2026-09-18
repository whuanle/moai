/**
 * 节点内配置表单共享常量与 hooks（纯 TS，无组件，供 NodeForm/HttpNodeForm 共用）.
 */

import { useMemo } from 'react'

import { useWorkflowDesignerStore } from './store'
import { collectUpstreamVariables } from './utils'
import type { GlobalVariableDef } from './types'

export const FIELD_TYPE_OPTIONS = ['string', 'number', 'boolean', 'object', 'map', 'array', 'dynamic']

/** 绑定表达式类型选项（引擎求值器支持的输入表达式） */
export const EXPRESSION_TYPE_OPTIONS = [
  { value: 'variable', labelKey: 'workflowDesigner.exprVariable' },
  { value: 'jsonpath', labelKey: 'workflowDesigner.exprJsonPath' },
  { value: 'interpolation', labelKey: 'workflowDesigner.exprInterp' },
  { value: 'fixed', labelKey: 'workflowDesigner.exprFixed' },
]

/** 上游变量提示选项（system.* + sys.* + 祖先节点输出） */
export function useVariableOptions(nodeId: string): { value: string; label: string }[] {
  const canvasJSON = useWorkflowDesignerStore((s) => s.editorJSON ?? s.initialData)
  const variables = useWorkflowDesignerStore((s) => s.variables)
  return useMemo(
    () => collectUpstreamVariables(canvasJSON, nodeId, variables as GlobalVariableDef[]),
    [canvasJSON, nodeId, variables],
  )
}

/** 变量插入选项：把上游变量包装成 {引用} 模板项（供 http 节点的 URL/参数/请求体等插值输入使用） */
export function useTemplateVariableOptions(nodeId: string): { value: string; label: string }[] {
  const options = useVariableOptions(nodeId)
  return useMemo(
    () => options.map((opt) => ({ value: `{${opt.value}}`, label: opt.label })),
    [options],
  )
}
