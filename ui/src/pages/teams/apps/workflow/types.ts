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
  /** 期望字段类型（string/number/boolean/object/map/array/dynamic），设计器元数据 */
  fieldType?: string
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

/** 节点类型（引擎已实现的节点） */
export type NodeType = 'start' | 'end' | 'condition' | 'aiChat' | 'javaScript' | 'plugin' | 'switch' | 'knowledgeSearch' | 'questionClassifier' | 'http' | 'agentApp'

/** http 节点键值对（查询参数/请求头/表单字段），值支持 {引用} 插值 */
export interface HttpKvItem {
  name: string
  value: string
}

/** http 节点鉴权配置 */
export interface HttpAuthSetting {
  type: 'none' | 'bearer' | 'basic' | 'apiKey'
  token?: string
  username?: string
  password?: string
  headerName?: string
  headerValue?: string
}

/** http 节点输出字段提取配置（对响应 JSON 做 JsonPath 提取） */
export interface HttpExtractFieldDef {
  name: string
  path: string
  fieldType?: string
}

/** 节点私有配置 */
export interface NodeSettings {
  /** aiChat/questionClassifier：模型 id（ai_model.id，团队网关可选模型） */
  aiModelId?: string
  /** aiChat：静态系统提示词（角色/风格设定；输入绑定 system 可覆盖） */
  systemPrompt?: string
  /** aiChat：采样温度 0-2（空=渠道/模型默认） */
  temperature?: number
  /** aiChat：引入的技能 id（挂载为模型可调用工具，需配合沙箱执行技能脚本） */
  skillIds?: string[]
  /** aiChat：开启沙箱（暴露代码执行等沙箱工具） */
  sandboxEnabled?: boolean
  /** agentApp：要调用的 Agent 应用 id（本团队已发布） */
  agentAppId?: string
  /** plugin：插件 key */
  pluginKey?: string
  /** javaScript：沙箱脚本，约定 function run(inputs, sys, nodes) 返回对象 */
  code?: string
  /** condition：脚本模式脚本，约定 function condition(inputs, sys, nodes, system) 返回布尔；存在时优先于 condition 绑定 */
  conditionScript?: string
  /** condition：绑定模式下「满足时」走哪条出边（目标节点 key）；缺省按端口 true/false */
  trueTarget?: string
  /** knowledgeSearch：知识库 id（单个，静态选择；与变量绑定 wikiId 输入二选一，输入优先） */
  wikiId?: number
  /** knowledgeSearch：旧版复数形式（多知识库数组），仅为旧草稿兼容保留 */
  wikiIds?: number[]
  /** knowledgeSearch：每个知识库召回条数（1-50，默认 5） */
  topK?: number
  /** questionClassifier：背景知识（补充分类判断的领域信息，可选） */
  backgroundKnowledge?: string
  /** questionClassifier：聊天记录携带条数（0-50，默认 6） */
  historyCount?: number
  /** http：请求方法（GET/POST/PUT/DELETE/PATCH/HEAD，默认 GET） */
  method?: string
  /** http：请求地址（支持 {引用} 插值） */
  url?: string
  /** http：超时时长（秒，1-300，默认 30） */
  timeoutSeconds?: number
  /** http：查询参数 */
  params?: HttpKvItem[]
  /** http：请求头 */
  headers?: HttpKvItem[]
  /** http：请求体类型（none/json/form/text，缺省时有 body 视为 json） */
  bodyType?: 'none' | 'json' | 'form' | 'text'
  /** http：请求体（json/text，支持 {引用} 插值） */
  body?: string
  /** http：表单字段（bodyType=form） */
  formEntries?: HttpKvItem[]
  /** http：鉴权配置 */
  auth?: HttpAuthSetting
  /** http：报错捕获（开启后请求失败/非 2xx 不中断流程，输出 hasError/errorMessage） */
  errorCapture?: boolean
  /** http：输出字段提取 */
  extract?: HttpExtractFieldDef[]
}

/** 多条件节点分支定义（data.branches，保存时映射为 config.branches） */
export interface SwitchBranchDef {
  /** 分支 id（出边 condition 标记，如 b1/b2） */
  id: string
  /** 分支显示名 */
  label: string
  /** 命中条件 */
  binding: FieldBinding
}

/** 问题分类节点分类定义（data.classes，保存时映射为 config.classes） */
export interface ClassifierClassDef {
  /** 分类 id（出边 condition 标记，如 c1/c2） */
  id: string
  /** 分类名称（问题类型描述） */
  label: string
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

/** 流程全局变量定义（启动时可赋值，节点经 system.变量名 引用） */
export interface GlobalVariableDef {
  name: string
  /** string/number/boolean/object/map/array/dynamic */
  fieldType?: string
  /** 默认值（JSON 字面量字符串：数字/布尔按字面量，其余按字符串） */
  defaultValue?: string
  description?: string
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
  /** 流程全局变量 */
  variables?: GlobalVariableDef[]
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
  /** 节点表单数据：key/title/content/branches(switch)/classes(questionClassifier)/inputs/outputs/settings */
  data?: {
    key?: string
    title?: string
    content?: string
    branches?: SwitchBranchDef[]
    classes?: ClassifierClassDef[]
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
