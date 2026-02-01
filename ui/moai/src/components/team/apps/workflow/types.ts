/**
 * 工作流类型定义 - 重构版
 * 统一的数据模型，不再分离 Backend 和 Canvas
 */

// ==================== 基础类型 ====================

export enum NodeType {
  Start = 'start',
  End = 'end',
  Condition = 'condition',
  Fork = 'fork',
  ForEach = 'forEach',
  AiChat = 'aiChat',
  DataProcess = 'dataProcess',
  JavaScript = 'javaScript',
  Plugin = 'plugin',
  Wiki = 'wiki'
}

export enum NodeCategory {
  Control = 'control',
  AI = 'ai',
  Data = 'data',
  Integration = 'integration'
}

export enum FieldType {
  Empty = 'empty',
  String = 'string',
  Number = 'number',
  Boolean = 'boolean',
  Object = 'object',
  Array = 'array',
  Map = 'map',
  Dynamic = 'dynamic'
}

export enum FieldExpressionType {
  Constant = 'constant',
  Variable = 'variable',
  Expression = 'expression',
  NodeOutput = 'nodeOutput',
  Context = 'context'
}

// ==================== 字段定义 ====================

export interface FieldDefine {
  fieldName: string;
  fieldType: FieldType;
  expressionType?: string;  // 表达式类型：Run, Fixed, Variable, Jsonpath, Interpolation
  isRequired?: boolean;
  description?: string;
  defaultValue?: any;
  value?: any;  // 字段值
  children?: FieldDefine[];
  isArrayIndex?: boolean;  // 是否是数组索引（数组子字段）
}

export interface FieldValue {
  fieldName: string;
  expressionType: FieldExpressionType;
  value: any;
}

// ==================== 节点数据（统一模型）====================

export interface WorkflowNode {
  // 基础信息
  id: string;
  type: NodeType;
  name: string;
  description?: string;
  
  // 位置信息（直接包含，不分离）
  position: {
    x: number;
    y: number;
  };
  
  // 配置信息
  config: {
    inputFields: FieldDefine[];
    outputFields: FieldDefine[];
    settings: Record<string, any>;
  };
  
  // UI 状态（最小化，可选）
  ui?: {
    selected?: boolean;
    expanded?: boolean;
    width?: number;
    height?: number;
  };
}

// ==================== 连接数据 ====================

export interface WorkflowEdge {
  id: string;
  source: string;  // 源节点 ID
  target: string;  // 目标节点 ID
  sourceHandle?: string;  // 源端口
  targetHandle?: string;  // 目标端口
  data?: {
    label?: string;
    condition?: string;
  };
}

// ==================== 工作流数据 ====================

export interface WorkflowData {
  id: string;
  name: string;
  description?: string;
  version?: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  viewport?: {
    zoom: number;
    x: number;
    y: number;
  };
  metadata?: {
    createdAt?: string;
    updatedAt?: string;
    author?: string;
  };
}

// ==================== 节点模板 ====================

export interface NodeTemplate {
  type: NodeType;
  name: string;
  description: string;
  icon: string;
  color: string;
  category: NodeCategory;
  defaultData: {
    inputFields: FieldDefine[];
    outputFields: FieldDefine[];
    settings?: Record<string, any>;
  };
}

// ==================== 验证 ====================

export enum ValidationErrorType {
  MissingStartNode = 'missing_start_node',
  MissingEndNode = 'missing_end_node',
  MultipleStartNodes = 'multiple_start_nodes',
  MultipleEndNodes = 'multiple_end_nodes',
  DisconnectedNode = 'disconnected_node',
  CyclicDependency = 'cyclic_dependency',
  InvalidConnection = 'invalid_connection',
  MissingRequiredField = 'missing_required_field',
  InvalidFieldType = 'invalid_field_type',
}

export interface ValidationError {
  type: ValidationErrorType;
  message: string;
  nodeId?: string;
  edgeId?: string;
  field?: string;
}

// ==================== 节点约束 ====================

export interface NodeConstraints {
  minCount: number;
  maxCount: number;  // -1 表示无限制
  deletable: boolean;
  copyable: boolean;
  requiresInput: boolean;
  requiresOutput: boolean;
}

// ==================== API 数据格式 ====================

// 后端 API 返回的节点设计格式
export interface ApiNodeDesign {
  nodeKey: string;
  nodeType: NodeType;
  name: string;
  description?: string;
  inputFields?: FieldDefine[];
  outputFields?: FieldDefine[];
  fieldDesigns?: Record<string, any>;
  nextNodeKeys?: string[];
}

// 后端 API 返回的工作流配置
export interface ApiWorkflowConfig {
  id: string;
  appId: string;
  name: string;
  description?: string;
  functionDesign?: ApiNodeDesign[] | string;  // 已发布版本（可能是数组或 JSON 字符串）
  functionDesignDraft?: ApiNodeDesign[];  // 草稿版本
  uiDesign?: string | object;  // JSON 字符串或对象（已发布版本）
  uiDesignDraft?: string | object;  // JSON 字符串或对象（草稿版本）
  isDraft?: boolean;
  isPublish?: boolean;
}
