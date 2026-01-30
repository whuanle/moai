/**
 * 工作流核心架构 - 状态管理
 * 
 * 设计理念：
 * 1. 分离画布 UI 数据和后端逻辑数据
 * 2. 实施节点约束规则（开始/结束节点唯一性等）
 * 3. 提供统一的节点操作接口
 * 4. 支持数据验证和转换
 */

import { NodeType, FieldDefine } from './types';

// ==================== 后端逻辑数据结构 ====================

/**
 * 后端节点数据（业务逻辑）
 */
export interface BackendNodeData {
  id: string;
  type: NodeType;
  name: string;
  description?: string;
  
  // 节点配置
  config: {
    // 输入字段定义
    inputFields: FieldDefine[];
    // 输出字段定义
    outputFields: FieldDefine[];
    // 节点特定配置（如 AI 模型、插件 ID 等）
    settings: Record<string, unknown>;
  };
  
  // 执行逻辑
  execution?: {
    // 超时时间（毫秒）
    timeout?: number;
    // 重试次数
    retryCount?: number;
    // 错误处理策略
    errorHandling?: 'stop' | 'continue' | 'retry';
  };
}

/**
 * 后端连接数据
 */
export interface BackendEdgeData {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  // 源节点输出字段
  sourceField?: string;
  // 目标节点输入字段
  targetField?: string;
  // 条件表达式（用于条件分支）
  condition?: string;
  // 连接标签
  label?: string;
}

/**
 * 后端工作流数据
 */
export interface BackendWorkflowData {
  id: string;
  name: string;
  description?: string;
  version: string;
  nodes: BackendNodeData[];
  edges: BackendEdgeData[];
  // 全局变量
  variables?: Record<string, unknown>;
  // 工作流元数据
  metadata?: {
    createdAt?: string;
    updatedAt?: string;
    author?: string;
    tags?: string[];
  };
}

// ==================== 画布 UI 数据结构 ====================

/**
 * 画布节点位置
 */
export interface CanvasPosition {
  x: number;
  y: number;
}

/**
 * 画布节点数据（UI 展示）
 */
export interface CanvasNodeData {
  id: string;
  type: NodeType;
  position: CanvasPosition;
  // UI 状态
  ui: {
    // 是否展开
    expanded: boolean;
    // 是否选中
    selected: boolean;
    // 是否高亮
    highlighted: boolean;
    // 节点尺寸
    width?: number;
    height?: number;
  };
  // 显示标题
  title: string;
  // 显示内容
  content?: string;
}

/**
 * 画布连接数据
 */
export interface CanvasEdgeData {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  // UI 状态
  ui: {
    // 是否选中
    selected: boolean;
    // 连接样式
    style?: 'solid' | 'dashed' | 'dotted';
    // 连接颜色
    color?: string;
  };
}

/**
 * 画布工作流数据
 */
export interface CanvasWorkflowData {
  nodes: CanvasNodeData[];
  edges: CanvasEdgeData[];
  // 画布视图状态
  viewport: {
    zoom: number;
    offsetX: number;
    offsetY: number;
  };
}

// ==================== 工作流约束规则 ====================

/**
 * 节点约束规则
 */
export interface NodeConstraints {
  // 最小数量
  minCount: number;
  // 最大数量（-1 表示无限制）
  maxCount: number;
  // 是否可删除
  deletable: boolean;
  // 是否可复制
  copyable: boolean;
  // 是否必须连接
  requireConnection: boolean;
}

/**
 * 默认节点约束
 */
export const DEFAULT_NODE_CONSTRAINTS: Record<NodeType, NodeConstraints> = {
  [NodeType.Start]: {
    minCount: 1,
    maxCount: 1,
    deletable: false,
    copyable: false,
    requireConnection: true,
  },
  [NodeType.End]: {
    minCount: 1,
    maxCount: 1,
    deletable: false,
    copyable: false,
    requireConnection: true,
  },
  [NodeType.Condition]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.Fork]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.ForEach]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.AiChat]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.DataProcess]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.JavaScript]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.Plugin]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
  [NodeType.Wiki]: {
    minCount: 0,
    maxCount: -1,
    deletable: true,
    copyable: true,
    requireConnection: true,
  },
};

// ==================== 验证错误类型 ====================

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

// ==================== 工作流状态管理 ====================

/**
 * 工作流核心状态
 */
export interface WorkflowState {
  // 工作流 ID
  workflowId: string;
  
  // 应用 ID
  appId: string;
  
  // 团队 ID
  teamId: number;
  
  // 后端逻辑数据
  backend: BackendWorkflowData;
  
  // 画布 UI 数据
  canvas: CanvasWorkflowData;
  
  // 验证错误
  validationErrors: ValidationError[];
  
  // 是否已修改
  isDirty: boolean;
  
  // 是否正在保存
  isSaving: boolean;
  
  // 是否正在加载
  isLoading: boolean;
  
  // 加载错误
  loadError: string | null;
  
  // 是否为草稿版本
  isDraft: boolean;
}

/**
 * 工作流操作接口
 */
export interface WorkflowActions {
  // ==================== 节点操作 ====================
  
  /**
   * 添加节点
   * @param type 节点类型
   * @param position 画布位置
   * @returns 新节点 ID 或错误信息
   */
  addNode: (type: NodeType, position: CanvasPosition) => string | { error: string };
  
  /**
   * 删除节点
   * @param nodeId 节点 ID
   * @returns 是否成功或错误信息
   */
  deleteNode: (nodeId: string) => boolean | { error: string };
  
  /**
   * 复制节点
   * @param nodeId 节点 ID
   * @param offset 位置偏移
   * @returns 新节点 ID 或错误信息
   */
  copyNode: (nodeId: string, offset: CanvasPosition) => string | { error: string };
  
  /**
   * 更新节点位置
   * @param nodeId 节点 ID
   * @param position 新位置
   */
  updateNodePosition: (nodeId: string, position: CanvasPosition) => void;
  
  /**
   * 更新节点配置
   * @param nodeId 节点 ID
   * @param config 新配置
   */
  updateNodeConfig: (nodeId: string, config: Partial<BackendNodeData['config']>) => void;
  
  /**
   * 更新节点 UI 状态
   * @param nodeId 节点 ID
   * @param ui 新 UI 状态
   */
  updateNodeUI: (nodeId: string, ui: Partial<CanvasNodeData['ui']>) => void;
  
  // ==================== 连接操作 ====================
  
  /**
   * 添加连接
   * @param sourceNodeId 源节点 ID
   * @param targetNodeId 目标节点 ID
   * @returns 新连接 ID 或错误信息
   */
  addEdge: (sourceNodeId: string, targetNodeId: string) => string | { error: string };
  
  /**
   * 删除连接
   * @param edgeId 连接 ID
   */
  deleteEdge: (edgeId: string) => void;
  
  /**
   * 更新连接配置
   * @param edgeId 连接 ID
   * @param config 新配置
   */
  updateEdgeConfig: (edgeId: string, config: Partial<BackendEdgeData>) => void;
  
  // ==================== 验证操作 ====================
  
  /**
   * 验证工作流
   * @returns 验证错误列表
   */
  validate: () => ValidationError[];
  
  /**
   * 检查是否可以添加节点
   * @param type 节点类型
   * @returns 是否可以添加或错误信息
   */
  canAddNode: (type: NodeType) => boolean | { error: string };
  
  /**
   * 检查是否可以删除节点
   * @param nodeId 节点 ID
   * @returns 是否可以删除或错误信息
   */
  canDeleteNode: (nodeId: string) => boolean | { error: string };
  
  /**
   * 检查是否可以添加连接
   * @param sourceNodeId 源节点 ID
   * @param targetNodeId 目标节点 ID
   * @returns 是否可以添加或错误信息
   */
  canAddEdge: (sourceNodeId: string, targetNodeId: string) => boolean | { error: string };
  
  // ==================== 数据转换 ====================
  
  /**
   * 将画布数据和后端数据合并为完整工作流
   * @returns 完整工作流数据
   */
  toWorkflowData: () => BackendWorkflowData;
  
  /**
   * 从后端数据加载工作流
   * @param data 后端工作流数据
   */
  loadFromBackend: (data: BackendWorkflowData) => void;
  
  /**
   * 导出为 JSON
   * @returns JSON 字符串
   */
  exportToJSON: () => string;
  
  /**
   * 从 JSON 导入
   * @param json JSON 字符串
   */
  importFromJSON: (json: string) => void;
  
  // ==================== 保存操作 ====================
  
  /**
   * 保存工作流
   * @returns 是否成功
   */
  save: () => Promise<boolean>;
  
  /**
   * 重置修改状态
   */
  resetDirty: () => void;
  
  // ==================== API 集成操作 ====================
  
  /**
   * 从后端加载工作流
   */
  loadFromApi: (appId: string, teamId: number) => Promise<void>;
  
  /**
   * 保存工作流到后端
   */
  saveToApi: () => Promise<boolean>;
  
  /**
   * 查询节点定义并更新节点
   */
  queryAndUpdateNodeDefinition: (
    nodeId: string,
    nodeType: NodeType,
    instanceId?: string
  ) => Promise<void>;
  
  /**
   * 批量查询节点定义
   */
  batchQueryNodeDefinitions: (
    nodeQueries: Array<{ nodeId: string; nodeType: NodeType; instanceId?: string }>
  ) => Promise<void>;
}
