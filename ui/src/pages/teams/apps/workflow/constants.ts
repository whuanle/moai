/**
 * 节点模板与约束 —— 与引擎 MoAI.App.Workflow 已实现的节点一一对应.
 * 新节点类型在引擎补齐后在此扩展.
 */

import type { FieldBinding, HttpAuthSetting, HttpExtractFieldDef, HttpKvItem, NodeType, OutputField } from './types'

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
  icon: string
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
    systemPrompt?: string
    temperature?: number
    skillIds?: string[]
    sandboxEnabled?: boolean
    agentAppId?: string
    pluginKey?: string
    code?: string
    wikiId?: number
    wikiIds?: number[]
    topK?: number
    backgroundKnowledge?: string
    historyCount?: number
    /** http：请求配置（保存时经 sanitizeSettings 清洗为引擎 config） */
    method?: string
    url?: string
    timeoutSeconds?: number
    params?: HttpKvItem[]
    headers?: HttpKvItem[]
    bodyType?: 'none' | 'json' | 'form' | 'text'
    body?: string
    formEntries?: HttpKvItem[]
    auth?: HttpAuthSetting
    errorCapture?: boolean
    extract?: HttpExtractFieldDef[]
  }
}

export const NODE_CONSTRAINTS: Record<NodeType, NodeConstraints> = {
  start: { minCount: 1, maxCount: 1, deletable: false, copyable: false, requiresInput: false, requiresOutput: true },
  end: { minCount: 1, maxCount: 1, deletable: false, copyable: false, requiresInput: true, requiresOutput: false },
  condition: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  aiChat: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  javaScript: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  plugin: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  switch: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  knowledgeSearch: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  questionClassifier: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  http: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
  agentApp: { minCount: 0, maxCount: -1, deletable: true, copyable: true, requiresInput: true, requiresOutput: true },
}

export const DEFAULT_JS_CODE = `function run(inputs, sys, nodes) {
  // inputs: 当前节点输入；sys: 系统变量；nodes: 上游节点输出
  return { result: inputs }
}`

export const NODE_TEMPLATES: NodeTemplate[] = [
  {
    type: 'start',
    icon: '▶',
    nameKey: 'workflowDesigner.nodeStart',
    name: '开始',
    descKey: 'workflowDesigner.nodeStartDesc',
    desc: '工作流的入口点',
    color: '#52c41a',
    inputs: {
      question: { expressionType: 'run', value: '', required: true, fieldType: 'string' },
    },
    outputs: [],
  },
  {
    type: 'end',
    icon: '⏹',
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
    icon: '◆',
    nameKey: 'workflowDesigner.nodeCondition',
    name: '条件判断',
    descKey: 'workflowDesigner.nodeConditionDesc',
    desc: '根据条件判断选择分支',
    color: '#faad14',
    inputs: {
      condition: { expressionType: 'variable', value: '', required: true, description: '条件判断（布尔）' },
    },
    outputs: [],
  },
  {
    type: 'aiChat',
    icon: '🤖',
    nameKey: 'workflowDesigner.nodeAiChat',
    name: 'AI 对话',
    descKey: 'workflowDesigner.nodeAiChatDesc',
    desc: '调用模型生成回答',
    color: '#1677ff',
    inputs: {
      prompt: { expressionType: 'variable', value: '', required: true, description: '用户提示词' },
    },
    outputs: [{ name: 'answer', fieldType: 'string', description: '模型回答' }],
    settings: { aiModelId: '', systemPrompt: '' },
  },
  {
    type: 'agentApp',
    icon: '🤝',
    nameKey: 'workflowDesigner.nodeAgentApp',
    name: 'Agent 应用',
    descKey: 'workflowDesigner.nodeAgentAppDesc',
    desc: '调用已发布的 Agent 应用完成一轮对话',
    color: '#722ed1',
    inputs: {
      prompt: { expressionType: 'variable', value: '', required: true, description: '发给 Agent 的用户消息' },
    },
    outputs: [{ name: 'answer', fieldType: 'string', description: 'Agent 回答' }],
    settings: { agentAppId: '' },
  },
  {
    type: 'knowledgeSearch',
    icon: '📚',
    nameKey: 'workflowDesigner.nodeKnowledgeSearch',
    name: '知识库检索',
    descKey: 'workflowDesigner.nodeKnowledgeSearchDesc',
    desc: '在团队知识库中检索相关内容',
    color: '#13c2c2',
    inputs: {
      wikiId: { expressionType: 'variable', value: '', required: false, description: '知识库 id（变量绑定，运行时优先于静态选择）' },
      query: { expressionType: 'variable', value: '', required: true, description: '检索问题' },
    },
    outputs: [
      { name: 'query', fieldType: 'string', description: '检索问题' },
      { name: 'count', fieldType: 'number', description: '命中数量' },
      { name: 'hits', fieldType: 'array', description: '命中列表（documentName/content/score 等）' },
      { name: 'contents', fieldType: 'array', description: '切片内容列表' },
      { name: 'text', fieldType: 'string', description: '拼接后的检索文本' },
    ],
    settings: { topK: 5 },
  },
  {
    type: 'javaScript',
    icon: '📜',
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
    icon: '🔌',
    nameKey: 'workflowDesigner.nodePlugin',
    name: '插件调用',
    descKey: 'workflowDesigner.nodePluginDesc',
    desc: '按 key 调用团队可用插件',
    color: '#eb2f96',
    inputs: {},
    outputs: [{ name: 'result', fieldType: 'dynamic', description: '插件返回结果' }],
    settings: { pluginKey: '' },
  },
  {
    type: 'switch',
    icon: '⑂',
    nameKey: 'workflowDesigner.nodeSwitch',
    name: '多条件',
    descKey: 'workflowDesigner.nodeSwitchDesc',
    desc: '按顺序评估多个条件，走第一个命中的分支',
    color: '#722ed1',
    inputs: {},
    outputs: [],
  },
  {
    type: 'questionClassifier',
    icon: '🏷',
    nameKey: 'workflowDesigner.nodeQuestionClassifier',
    name: '问题分类',
    descKey: 'workflowDesigner.nodeQuestionClassifierDesc',
    desc: '用 AI 模型判断用户问题命中的分类，按分类路由分支',
    color: '#fa8c16',
    inputs: {
      query: { expressionType: 'variable', value: '', required: true, description: '用户问题' },
      history: { expressionType: 'variable', value: '', required: false, description: '聊天记录（[{role,content}] 数组，可选）' },
    },
    outputs: [
      { name: 'result', fieldType: 'string', description: '命中的分类 id' },
      { name: 'className', fieldType: 'string', description: '命中的分类名称' },
    ],
    settings: { aiModelId: '', backgroundKnowledge: '', historyCount: 6 },
  },
  {
    type: 'http',
    icon: '🌐',
    nameKey: 'workflowDesigner.nodeHttp',
    name: 'HTTP 请求',
    descKey: 'workflowDesigner.nodeHttpDesc',
    desc: '发出 HTTP 请求调用外部接口（联网搜索、数据库查询等）',
    color: '#597ef7',
    inputs: {},
    outputs: [
      { name: 'statusCode', fieldType: 'number', description: 'HTTP 状态码' },
      { name: 'rawResponse', fieldType: 'dynamic', description: '原始响应（JSON 可解析为对象，否则为文本）' },
      { name: 'hasError', fieldType: 'boolean', description: '是否报错（开启报错捕获时）' },
      { name: 'errorMessage', fieldType: 'string', description: '错误信息（开启报错捕获时）' },
    ],
    settings: {
      method: 'GET',
      url: '',
      timeoutSeconds: 30,
      params: [],
      headers: [],
      bodyType: 'none',
      body: '',
      formEntries: [],
      auth: { type: 'none' },
      errorCapture: false,
      extract: [],
    },
  },
]

/** 条件节点固定双出边端口：真分支在顶部、假分支在右侧（位置分开便于区分画线） */
export const CONDITION_PORTS = [
  { portID: 'true', type: 'output' as const, location: 'top' as const },
  { portID: 'false', type: 'output' as const, location: 'right' as const },
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
