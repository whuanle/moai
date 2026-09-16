/**
 * 节点模板与约束 —— 一期支持引擎已实现的 6 种节点.
 * fork/forEach/wiki/dataProcess 等待引擎补齐后在此扩展.
 */

import type { FieldBinding, NodeType, OutputField } from './types'

export interface NodeConstraints {
  minCount: number
  /** -1 表示无限制 */
  maxCount: number
  deletable: boolean
  copyable: boolean
  requiresInput: boolean
  requiresOutput: boolean
}

export interface NodeTemplate {
  type: NodeType
  nameKey: string
  /** 兜底中文名（i18n 缺失时展示） */
  name: string
  descKey: string
  desc: string
  color: string
  /** 输入字段绑定模板（key 为字段名） */
  inputs: Record<string, FieldBinding>
  /** 输出字段模板 */
  outputs: OutputField[]
  settings?: {
    aiModelId?: string
    pluginKey?: string
    code?: string
  }
}

export const NODE_CONSTRAINTS: Record<NodeType, NodeConstraints> = {
  start: { minCount: 1, maxCount: 1, deletable: false, copyable: false, requiresInput: false, requiresOutput: true },
  end: { minCount: 1, maxCount: 1, deletable: false, copyable: false, requiresInput: true, requiresOutput: false },
  condition: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  aiChat: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  javaScript: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  plugin: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
}

export const DEFAULT_JS_CODE = `function run(inputs, sys, nodes) {
  // inputs: 当前节点输入；sys: 系统变量；nodes: 上游节点输出
  return { result: inputs }
}`

export const NODE_TEMPLATES: NodeTemplate[] = [
  {
    type: 'start',
    nameKey: 'workflowDesigner.nodeStart',
    name: '开始',
    descKey: 'workflowDesigner.nodeStartDesc',
    desc: '工作流入口，声明启动参数',
    color: '#52c41a',
    inputs: {},
    outputs: [{ name: 'query', fieldType: 'string', isRequired: true, description: '工作流启动参数' }],
  },
  {
    type: 'end',
    nameKey: 'workflowDesigner.nodeEnd',
    name: '结束',
    descKey: 'workflowDesigner.nodeEndDesc',
    desc: '收集最终输出，输入即工作流结果',
    color: '#ff4d4f',
    inputs: {
      output: { expressionType: 'variable', value: '', required: false, description: '工作流最终输出' },
    },
    outputs: [],
  },
  {
    type: 'condition',
    nameKey: 'workflowDesigner.nodeCondition',
    name: '条件判断',
    descKey: 'workflowDesigner.nodeConditionDesc',
    desc: '按布尔结果路由到 true/false 分支',
    color: '#faad14',
    inputs: {
      condition: { expressionType: 'variable', value: '', required: true, description: '判断条件（布尔）' },
    },
    outputs: [],
  },
  {
    type: 'aiChat',
    nameKey: 'workflowDesigner.nodeAiChat',
    name: 'AI 对话',
    descKey: 'workflowDesigner.nodeAiChatDesc',
    desc: '调用模型生成回答',
    color: '#1677ff',
    inputs: {
      system: { expressionType: 'fixed', value: '', required: false, description: '系统提示词' },
      prompt: { expressionType: 'variable', value: '', required: true, description: '用户提示词' },
    },
    outputs: [{ name: 'answer', fieldType: 'string', description: '模型回答' }],
    settings: { aiModelId: '' },
  },
  {
    type: 'javaScript',
    nameKey: 'workflowDesigner.nodeJs',
    name: 'JavaScript',
    descKey: 'workflowDesigner.nodeJsDesc',
    desc: '执行 JS 脚本加工数据（服务端沙箱）',
    color: '#2f54eb',
    inputs: {},
    outputs: [{ name: 'result', fieldType: 'dynamic', description: '脚本返回对象' }],
    settings: { code: DEFAULT_JS_CODE },
  },
  {
    type: 'plugin',
    nameKey: 'workflowDesigner.nodePlugin',
    name: '插件调用',
    descKey: 'workflowDesigner.nodePluginDesc',
    desc: '按 key 调用团队可用插件',
    color: '#eb2f96',
    inputs: {},
    outputs: [{ name: 'result', fieldType: 'dynamic', description: '插件返回结果' }],
    settings: { pluginKey: '' },
  },
]

/** 条件节点固定双出边端口（引擎要求恰好 condition=true/false 两条出边） */
export const CONDITION_PORTS = [
  { portID: 'true', type: 'output' as const },
  { portID: 'false', type: 'output' as const },
]

export function getNodeTemplate(type: string): NodeTemplate | undefined {
  return NODE_TEMPLATES.find((t) => t.type === type)
}

const NODE_TYPE_SET = new Set<string>(Object.keys(NODE_CONSTRAINTS))

export function isSupportedNodeType(type: string): type is NodeType {
  return NODE_TYPE_SET.has(type)
}

/** 生成节点 id（start/end 固定 key，其余带随机后缀） */
export function generateNodeId(type: string): string {
  if (type === 'start' || type === 'end') return type
  const rand = Math.random().toString(36).slice(2, 8)
  return `${type}_${Date.now().toString(36)}${rand}`
}
