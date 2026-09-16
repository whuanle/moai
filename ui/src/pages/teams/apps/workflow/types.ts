/**
 * 流程编排类型定义 —— 与后端引擎 MoAI.App.Workflow 的 WorkflowDefinition 契约一一对应.
 * 编辑器（FlowGram 画布）JSON 与引擎定义 JSON 之间的转换见 utils.ts.
 */

// ==================== 后端引擎契约 ====================

/** 表达式类型：运行时/固定值/变量引用/JSONPath/字符串插值（对齐引擎 ExpressionType 枚举） */
export type ExpressionType = 'run' | 'fixed' | 'variable' | 'jsonpath' | 'interpolation'

/** 输入字段绑定：描述节点输入如何取值（引擎 FieldBinding） */
export interface FieldBinding {
  expressionType: ExpressionType
  value: string
  /** 非必需字段解析失败时置为 null（分支汇合场景） */
  required?: boolean
  description?: string
}

/** 输出端口定义：描述节点会产生哪些输出（引擎 PortDefinition） */
export interface OutputField {
  name: string
  /** string/number/boolean/object/map/array/dynamic */
  fieldType: string
  isRequired?: boolean
  description?: string
}

/** 节点类型（一期支持引擎已实现的 6 种） */
export type NodeType = 'start' | 'end' | 'condition' | 'aiChat' | 'javaScript' | 'plugin'

/** 节点私有配置 */
export interface NodeSettings {
  /** aiChat：模型 id（ai_model.id，团队网关可选模型） */
  aiModelId?: string
  /** plugin：插件 key */
  pluginKey?: string
  /** javaScript：沙箱脚本，约定 function run(inputs, sys, nodes) 返回对象 */
  code?: string
}

/** 引擎节点定义（WorkflowNodeDefinition） */
export interface WorkflowNodeDef {
  key: string
  name: string
  type: NodeType | string
  description?: string
  /** 节点私有配置（对象，随定义 JSON 原样存储） */
  config?: NodeSettings
  /** 输入字段绑定，键为字段名 */
  inputs: Record<string, FieldBinding>
  /** 输出字段定义 */
  outputs: OutputField[]
}

/** 引擎连接定义（ConnectionDefinition） */
export interface WorkflowConnectionDef {
  id: string
  source: string
  target: string
  /** 仅条件节点出边使用："true" / "false" */
  condition?: string
  label?: string
}

/** 引擎工作流定义（WorkflowDefinition） */
export interface WorkflowDefinition {
  id: string
  name: string
  description?: string
  version: number
  /** 'draft' | 'published' */
  status: string
  nodes: WorkflowNodeDef[]
  connections: WorkflowConnectionDef[]
  ui?: {
    nodePositions?: Record<string, { x: number; y: number }>
    zoom?: number
  }
}

// ==================== FlowGram 编辑器契约 ====================

/** 编辑器节点 JSON（WorkflowNodeJSON 的投影） */
export interface EditorNodeJSON {
  id: string
  type: NodeType | string
  meta?: {
    position?: { x: number; y: number }
    defaultExpanded?: boolean
    [key: string]: unknown
  }
  /** 节点表单数据：title/content/inputs/outputs/settings */
  data?: {
    title?: string
    content?: string
    inputs?: Record<string, FieldBinding>
    outputs?: OutputField[]
    settings?: NodeSettings
    [key: string]: unknown
  }
  blocks?: unknown[]
  /** 节点出边（FlowGram 的边挂在节点下） */
  edges?: EditorEdgeJSON[]
}

/** 编辑器边 JSON */
export interface EditorEdgeJSON {
  sourceNodeID: string
  targetNodeID: string
  /** 条件节点出边的端口 id："true" / "false" */
  sourcePortID?: string | number
  targetPortID?: string | number
}

/** 编辑器画布 JSON（toJSON 产物） */
export interface EditorWorkflowJSON {
  nodes: EditorNodeJSON[]
  edges: EditorEdgeJSON[]
}

// ==================== 调试运行 ====================

/** 节点运行状态（来自调试执行响应） */
export interface NodeRunState {
  state: string
  errorMessage?: string | null
  startedAt?: string | null
  endedAt?: string | null
}
