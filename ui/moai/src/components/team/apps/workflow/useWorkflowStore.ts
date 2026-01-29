/**
 * 工作流状态管理实现（Zustand）
 */

import { create } from 'zustand';
import { NodeType } from './types';
import {
  WorkflowState,
  WorkflowActions,
  BackendNodeData,
  BackendEdgeData,
  BackendWorkflowData,
  CanvasNodeData,
  CanvasEdgeData,
  CanvasPosition,
  ValidationError,
  ValidationErrorType,
  DEFAULT_NODE_CONSTRAINTS,
} from './workflowStore';
import { getNodeTemplate } from './nodeTemplates';

/**
 * 生成唯一 ID
 */
function generateId(prefix: string): string {
  return `${prefix}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * 创建工作流 Store
 */
export const useWorkflowStore = create<WorkflowState & WorkflowActions>((set, get) => ({
  // ==================== 初始状态 ====================
  workflowId: '',
  backend: {
    id: '',
    name: '未命名工作流',
    version: '1.0.0',
    nodes: [],
    edges: [],
  },
  canvas: {
    nodes: [],
    edges: [],
    viewport: {
      zoom: 1,
      offsetX: 0,
      offsetY: 0,
    },
  },
  validationErrors: [],
  isDirty: false,
  isSaving: false,

  // ==================== 节点操作 ====================

  addNode: (type: NodeType, position: CanvasPosition) => {
    const state = get();
    
    // 检查是否可以添加
    const canAdd = state.canAddNode(type);
    if (typeof canAdd === 'object') {
      return canAdd;
    }
    
    // 获取节点模板
    const template = getNodeTemplate(type);
    if (!template) {
      return { error: `未找到节点类型: ${type}` };
    }
    
    // 生成节点 ID
    const nodeId = generateId(type);
    
    // 创建后端节点数据
    const backendNode: BackendNodeData = {
      id: nodeId,
      type,
      name: template.name,
      description: template.description,
      config: {
        inputFields: template.defaultData.inputFields || [],
        outputFields: template.defaultData.outputFields || [],
        settings: {},
      },
      execution: {
        timeout: 30000,
        retryCount: 0,
        errorHandling: 'stop',
      },
    };
    
    // 创建画布节点数据
    const canvasNode: CanvasNodeData = {
      id: nodeId,
      type,
      position,
      ui: {
        expanded: true,
        selected: false,
        highlighted: false,
      },
      title: template.name,
      content: template.description,
    };
    
    // 更新状态
    set({
      backend: {
        ...state.backend,
        nodes: [...state.backend.nodes, backendNode],
      },
      canvas: {
        ...state.canvas,
        nodes: [...state.canvas.nodes, canvasNode],
      },
      isDirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return nodeId;
  },

  deleteNode: (nodeId: string) => {
    const state = get();
    
    // 检查是否可以删除
    const canDelete = state.canDeleteNode(nodeId);
    if (typeof canDelete === 'object') {
      return canDelete;
    }
    
    // 删除节点
    const newBackendNodes = state.backend.nodes.filter(n => n.id !== nodeId);
    const newCanvasNodes = state.canvas.nodes.filter(n => n.id !== nodeId);
    
    // 删除相关连接
    const newBackendEdges = state.backend.edges.filter(
      e => e.sourceNodeId !== nodeId && e.targetNodeId !== nodeId
    );
    const newCanvasEdges = state.canvas.edges.filter(
      e => e.sourceNodeId !== nodeId && e.targetNodeId !== nodeId
    );
    
    // 更新状态
    set({
      backend: {
        ...state.backend,
        nodes: newBackendNodes,
        edges: newBackendEdges,
      },
      canvas: {
        ...state.canvas,
        nodes: newCanvasNodes,
        edges: newCanvasEdges,
      },
      isDirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return true;
  },

  copyNode: (nodeId: string, offset: CanvasPosition) => {
    const state = get();
    
    // 查找原节点
    const backendNode = state.backend.nodes.find(n => n.id === nodeId);
    const canvasNode = state.canvas.nodes.find(n => n.id === nodeId);
    
    if (!backendNode || !canvasNode) {
      return { error: '节点不存在' };
    }
    
    // 检查是否可以复制
    const constraints = DEFAULT_NODE_CONSTRAINTS[backendNode.type];
    if (!constraints.copyable) {
      return { error: '该节点不允许复制' };
    }
    
    // 检查是否可以添加
    const canAdd = state.canAddNode(backendNode.type);
    if (typeof canAdd === 'object') {
      return canAdd;
    }
    
    // 生成新节点 ID
    const newNodeId = generateId(backendNode.type);
    
    // 复制后端节点
    const newBackendNode: BackendNodeData = {
      ...backendNode,
      id: newNodeId,
      name: `${backendNode.name} (副本)`,
    };
    
    // 复制画布节点
    const newCanvasNode: CanvasNodeData = {
      ...canvasNode,
      id: newNodeId,
      position: {
        x: canvasNode.position.x + offset.x,
        y: canvasNode.position.y + offset.y,
      },
      title: `${canvasNode.title} (副本)`,
    };
    
    // 更新状态
    set({
      backend: {
        ...state.backend,
        nodes: [...state.backend.nodes, newBackendNode],
      },
      canvas: {
        ...state.canvas,
        nodes: [...state.canvas.nodes, newCanvasNode],
      },
      isDirty: true,
    });
    
    return newNodeId;
  },

  updateNodePosition: (nodeId: string, position: CanvasPosition) => {
    const state = get();
    
    const newCanvasNodes = state.canvas.nodes.map(node =>
      node.id === nodeId ? { ...node, position } : node
    );
    
    set({
      canvas: {
        ...state.canvas,
        nodes: newCanvasNodes,
      },
      isDirty: true,
    });
  },

  updateNodeConfig: (nodeId: string, config: Partial<BackendNodeData['config']>) => {
    const state = get();
    
    const newBackendNodes = state.backend.nodes.map(node =>
      node.id === nodeId
        ? { ...node, config: { ...node.config, ...config } }
        : node
    );
    
    set({
      backend: {
        ...state.backend,
        nodes: newBackendNodes,
      },
      isDirty: true,
    });
    
    // 重新验证
    get().validate();
  },

  updateNodeUI: (nodeId: string, ui: Partial<CanvasNodeData['ui']>) => {
    const state = get();
    
    const newCanvasNodes = state.canvas.nodes.map(node =>
      node.id === nodeId
        ? { ...node, ui: { ...node.ui, ...ui } }
        : node
    );
    
    set({
      canvas: {
        ...state.canvas,
        nodes: newCanvasNodes,
      },
    });
  },

  // ==================== 连接操作 ====================

  addEdge: (sourceNodeId: string, targetNodeId: string) => {
    const state = get();
    
    // 检查是否可以添加
    const canAdd = state.canAddEdge(sourceNodeId, targetNodeId);
    if (typeof canAdd === 'object') {
      return canAdd;
    }
    
    // 生成连接 ID
    const edgeId = generateId('edge');
    
    // 创建后端连接
    const backendEdge: BackendEdgeData = {
      id: edgeId,
      sourceNodeId,
      targetNodeId,
    };
    
    // 创建画布连接
    const canvasEdge: CanvasEdgeData = {
      id: edgeId,
      sourceNodeId,
      targetNodeId,
      ui: {
        selected: false,
        style: 'solid',
      },
    };
    
    // 更新状态
    set({
      backend: {
        ...state.backend,
        edges: [...state.backend.edges, backendEdge],
      },
      canvas: {
        ...state.canvas,
        edges: [...state.canvas.edges, canvasEdge],
      },
      isDirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return edgeId;
  },

  deleteEdge: (edgeId: string) => {
    const state = get();
    
    const newBackendEdges = state.backend.edges.filter(e => e.id !== edgeId);
    const newCanvasEdges = state.canvas.edges.filter(e => e.id !== edgeId);
    
    set({
      backend: {
        ...state.backend,
        edges: newBackendEdges,
      },
      canvas: {
        ...state.canvas,
        edges: newCanvasEdges,
      },
      isDirty: true,
    });
    
    // 重新验证
    get().validate();
  },

  updateEdgeConfig: (edgeId: string, config: Partial<BackendEdgeData>) => {
    const state = get();
    
    const newBackendEdges = state.backend.edges.map(edge =>
      edge.id === edgeId ? { ...edge, ...config } : edge
    );
    
    set({
      backend: {
        ...state.backend,
        edges: newBackendEdges,
      },
      isDirty: true,
    });
  },

  // ==================== 验证操作 ====================

  validate: () => {
    const state = get();
    const errors: ValidationError[] = [];
    
    // 检查开始节点
    const startNodes = state.backend.nodes.filter(n => n.type === NodeType.Start);
    if (startNodes.length === 0) {
      errors.push({
        type: ValidationErrorType.MissingStartNode,
        message: '工作流必须包含一个开始节点',
      });
    } else if (startNodes.length > 1) {
      errors.push({
        type: ValidationErrorType.MultipleStartNodes,
        message: '工作流只能包含一个开始节点',
      });
    }
    
    // 检查结束节点
    const endNodes = state.backend.nodes.filter(n => n.type === NodeType.End);
    if (endNodes.length === 0) {
      errors.push({
        type: ValidationErrorType.MissingEndNode,
        message: '工作流必须包含一个结束节点',
      });
    } else if (endNodes.length > 1) {
      errors.push({
        type: ValidationErrorType.MultipleEndNodes,
        message: '工作流只能包含一个结束节点',
      });
    }
    
    // 检查断开的节点
    state.backend.nodes.forEach(node => {
      const constraints = DEFAULT_NODE_CONSTRAINTS[node.type];
      if (constraints.requireConnection) {
        const hasIncoming = state.backend.edges.some(e => e.targetNodeId === node.id);
        const hasOutgoing = state.backend.edges.some(e => e.sourceNodeId === node.id);
        
        if (node.type === NodeType.Start && !hasOutgoing) {
          errors.push({
            type: ValidationErrorType.DisconnectedNode,
            message: `开始节点必须有输出连接`,
            nodeId: node.id,
          });
        } else if (node.type === NodeType.End && !hasIncoming) {
          errors.push({
            type: ValidationErrorType.DisconnectedNode,
            message: `结束节点必须有输入连接`,
            nodeId: node.id,
          });
        } else if (node.type !== NodeType.Start && node.type !== NodeType.End) {
          if (!hasIncoming || !hasOutgoing) {
            errors.push({
              type: ValidationErrorType.DisconnectedNode,
              message: `节点 "${node.name}" 未正确连接`,
              nodeId: node.id,
            });
          }
        }
      }
    });
    
    // 检查必填字段
    state.backend.nodes.forEach(node => {
      node.config.inputFields.forEach(field => {
        if (field.isRequired) {
          const value = node.config.settings[field.fieldName];
          if (value === undefined || value === null || value === '') {
            errors.push({
              type: ValidationErrorType.MissingRequiredField,
              message: `节点 "${node.name}" 缺少必填字段: ${field.fieldName}`,
              nodeId: node.id,
              field: field.fieldName,
            });
          }
        }
      });
    });
    
    // 更新验证错误
    set({ validationErrors: errors });
    
    return errors;
  },

  canAddNode: (type: NodeType) => {
    const state = get();
    const constraints = DEFAULT_NODE_CONSTRAINTS[type];
    
    // 检查最大数量限制
    if (constraints.maxCount !== -1) {
      const currentCount = state.backend.nodes.filter(n => n.type === type).length;
      if (currentCount >= constraints.maxCount) {
        return { error: `${type} 节点数量已达上限 (${constraints.maxCount})` };
      }
    }
    
    return true;
  },

  canDeleteNode: (nodeId: string) => {
    const state = get();
    const node = state.backend.nodes.find(n => n.id === nodeId);
    
    if (!node) {
      return { error: '节点不存在' };
    }
    
    const constraints = DEFAULT_NODE_CONSTRAINTS[node.type];
    
    // 检查是否可删除
    if (!constraints.deletable) {
      return { error: '该节点不允许删除' };
    }
    
    // 检查最小数量限制
    const currentCount = state.backend.nodes.filter(n => n.type === node.type).length;
    if (currentCount <= constraints.minCount) {
      return { error: `${node.type} 节点数量不能少于 ${constraints.minCount}` };
    }
    
    return true;
  },

  canAddEdge: (sourceNodeId: string, targetNodeId: string) => {
    const state = get();
    
    // 检查节点是否存在
    const sourceNode = state.backend.nodes.find(n => n.id === sourceNodeId);
    const targetNode = state.backend.nodes.find(n => n.id === targetNodeId);
    
    if (!sourceNode || !targetNode) {
      return { error: '源节点或目标节点不存在' };
    }
    
    // 检查是否已存在连接
    const existingEdge = state.backend.edges.find(
      e => e.sourceNodeId === sourceNodeId && e.targetNodeId === targetNodeId
    );
    if (existingEdge) {
      return { error: '连接已存在' };
    }
    
    // 检查是否形成环路（简单检查）
    if (sourceNodeId === targetNodeId) {
      return { error: '不能连接到自身' };
    }
    
    return true;
  },

  // ==================== 数据转换 ====================

  toWorkflowData: () => {
    const state = get();
    return state.backend;
  },

  loadFromBackend: (data: BackendWorkflowData) => {
    // 从后端数据重建画布数据
    const canvasNodes: CanvasNodeData[] = data.nodes.map((node, index) => ({
      id: node.id,
      type: node.type,
      position: { x: 100 + index * 300, y: 200 }, // 默认位置
      ui: {
        expanded: true,
        selected: false,
        highlighted: false,
      },
      title: node.name,
      content: node.description,
    }));
    
    const canvasEdges: CanvasEdgeData[] = data.edges.map(edge => ({
      id: edge.id,
      sourceNodeId: edge.sourceNodeId,
      targetNodeId: edge.targetNodeId,
      ui: {
        selected: false,
        style: 'solid',
      },
    }));
    
    set({
      workflowId: data.id,
      backend: data,
      canvas: {
        nodes: canvasNodes,
        edges: canvasEdges,
        viewport: {
          zoom: 1,
          offsetX: 0,
          offsetY: 0,
        },
      },
      isDirty: false,
    });
    
    // 验证
    get().validate();
  },

  exportToJSON: () => {
    const state = get();
    return JSON.stringify(state.backend, null, 2);
  },

  importFromJSON: (json: string) => {
    try {
      const data: BackendWorkflowData = JSON.parse(json);
      get().loadFromBackend(data);
    } catch (error) {
      console.error('导入失败:', error);
      throw new Error('无效的 JSON 格式');
    }
  },

  // ==================== 保存操作 ====================

  save: async () => {
    const state = get();
    
    // 验证工作流
    const errors = state.validate();
    if (errors.length > 0) {
      console.error('工作流验证失败:', errors);
      return false;
    }
    
    set({ isSaving: true });
    
    try {
      // TODO: 调用 API 保存到后端
      const workflowData = state.toWorkflowData();
      console.log('保存工作流:', workflowData);
      
      // 模拟 API 调用
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      set({ isDirty: false, isSaving: false });
      return true;
    } catch (error) {
      console.error('保存失败:', error);
      set({ isSaving: false });
      return false;
    }
  },

  resetDirty: () => {
    set({ isDirty: false });
  },
}));
