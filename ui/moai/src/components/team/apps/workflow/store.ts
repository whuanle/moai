/**
 * 工作流状态管理 - 重构版
 * 合并 workflowStore.ts 和 useWorkflowStore.ts
 */

import { create } from 'zustand';
import { 
  WorkflowData, 
  WorkflowNode, 
  WorkflowEdge,
  NodeType,
  ValidationError 
} from './types';
import { 
  getNodeTemplate, 
  generateNodeId, 
  generateEdgeId,
  NODE_CONSTRAINTS 
} from './constants';
import { 
  validateWorkflow,
  validateWorkflowForDebug,
  canAddNode as checkCanAddNode,
  canDeleteNode as checkCanDeleteNode,
  canAddEdge as checkCanAddEdge,
  deepClone,
  createDefaultWorkflow
} from './utils';
import { workflowApi } from './api';

// ==================== 数据 ====================

interface WorkflowStore {
  // 数据
  workflow: WorkflowData | null;
  editorRawData: any | null;  // 编辑器原始数据
  
  // 状态
  loading: boolean;
  saving: boolean;
  dirty: boolean;
  errors: ValidationError[];
  
  // 元数据
  appId: string;
  teamId: number;
  isDraft: boolean;
  
  // 加载和保存
  load: (appId: string, teamId: number) => Promise<void>;
  save: () => Promise<void>;
  saveEditorData: (editorData: any) => void;  // 保存编辑器原始数据
  reset: () => void;
  
  // 节点操作
  addNode: (type: NodeType, position: { x: number; y: number }) => string | null;
  updateNode: (id: string, updates: Partial<WorkflowNode>) => void;
  deleteNode: (id: string) => boolean;
  copyNode: (id: string, offset?: { x: number; y: number }) => string | null;
  
  // 连接操作
  addEdge: (source: string, target: string) => string | null;
  deleteEdge: (id: string) => void;
  
  // 批量操作
  updateNodes: (updates: Array<{ id: string; updates: Partial<WorkflowNode> }>) => void;
  deleteNodes: (ids: string[]) => void;
  
  // 验证
  validate: () => ValidationError[];
  validateForRun: () => ValidationError[]; // 运行前的完整验证
  
  // 工具方法
  getNode: (id: string) => WorkflowNode | undefined;
  getEdge: (id: string) => WorkflowEdge | undefined;
  canAddNode: (type: NodeType) => boolean | string;
  canDeleteNode: (id: string) => boolean | string;
  canAddEdge: (source: string, target: string) => boolean | string;
}

// ==================== Store 实现 ====================

export const useWorkflowStore = create<WorkflowStore>((set, get) => ({
  // 初始状态
  workflow: null,
  editorRawData: null,
  loading: false,
  saving: false,
  dirty: false,
  errors: [],
  appId: '',
  teamId: 0,
  isDraft: false,
  
  // ==================== 加载和保存 ====================
  
  load: async (appId: string, teamId: number) => {
    set({ loading: true, appId, teamId });
    
    try {
      const { workflow, editorRawData } = await workflowApi.load(appId, teamId);
      
      set({ 
        workflow,
        editorRawData,  // 保存编辑器原始数据
        loading: false,
        dirty: false,
        errors: [],
      });
      
      // 加载后立即验证
      get().validate();
    } catch (error) {
      console.error('加载工作流失败:', error);
      set({ 
        loading: false,
        workflow: createDefaultWorkflow(),
        editorRawData: null,
      });
      throw error;
    }
  },
  
  save: async () => {
    const { workflow, editorRawData, appId, teamId } = get();
    
    if (!workflow) {
      throw new Error('没有可保存的工作流');
    }
    
    set({ saving: true });
    
    try {
      await workflowApi.save(appId, teamId, workflow, editorRawData);
      
      set({ 
        saving: false,
        dirty: false,
      });
    } catch (error) {
      console.error('保存工作流失败:', error);
      set({ saving: false });
      throw error;
    }
  },
  
  saveEditorData: (editorData: any) => {
    console.log('🔍 saveEditorData - 保存编辑器原始数据');
    set({ 
      editorRawData: editorData,
      dirty: true 
    });
  },
  
  reset: () => {
    set({
      workflow: null,
      editorRawData: null,
      loading: false,
      saving: false,
      dirty: false,
      errors: [],
      appId: '',
      teamId: 0,
      isDraft: false,
    });
  },
  
  // ==================== 节点操作 ====================
  
  addNode: (type: NodeType, position: { x: number; y: number }) => {
    const { workflow } = get();
    if (!workflow) return null;
    
    // 检查是否可以添加
    const canAdd = get().canAddNode(type);
    if (typeof canAdd === 'string') {
      console.warn(canAdd);
      return null;
    }
    
    // 获取节点模板
    const template = getNodeTemplate(type);
    if (!template) {
      console.error(`未找到节点类型: ${type}`);
      return null;
    }
    
    // 创建新节点
    const nodeId = generateNodeId(type);
    const newNode: WorkflowNode = {
      id: nodeId,
      type,
      name: template.name,
      description: template.description,
      position,
      config: {
        inputFields: deepClone(template.defaultData.inputFields),
        outputFields: deepClone(template.defaultData.outputFields),
        settings: deepClone(template.defaultData.settings || {}),
      },
      ui: {
        expanded: true,
        selected: false,
      },
    };
    
    set({
      workflow: {
        ...workflow,
        nodes: [...workflow.nodes, newNode],
      },
      dirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return nodeId;
  },
  
  updateNode: (id: string, updates: Partial<WorkflowNode>) => {
    const { workflow } = get();
    if (!workflow) return;
    
    const nodeIndex = workflow.nodes.findIndex(n => n.id === id);
    if (nodeIndex === -1) return;
    
    const updatedNodes = [...workflow.nodes];
    const oldNode = updatedNodes[nodeIndex];
    
    // 如果 key 改变了，需要更新所有引用
    const newNodeId = updates.id || oldNode.id;
    const keyChanged = newNodeId !== oldNode.id;
    
    updatedNodes[nodeIndex] = {
      ...oldNode,
      ...updates,
      // 深度合并 config
      config: updates.config ? {
        ...oldNode.config,
        ...updates.config,
        inputFields: updates.config.inputFields || oldNode.config.inputFields,
        outputFields: updates.config.outputFields || oldNode.config.outputFields,
        settings: updates.config.settings ? {
          ...oldNode.config.settings,
          ...updates.config.settings,
        } : oldNode.config.settings,
      } : oldNode.config,
      // 深度合并 ui
      ui: updates.ui ? {
        ...oldNode.ui,
        ...updates.ui,
      } : oldNode.ui,
    };
    
    // 如果 key 改变了，更新所有 edges 中的引用
    let updatedEdges = workflow.edges;
    if (keyChanged) {
      updatedEdges = workflow.edges.map(edge => {
        const newSource = edge.source === oldNode.id ? newNodeId : edge.source;
        const newTarget = edge.target === oldNode.id ? newNodeId : edge.target;
        const newId = edge.id 
          ? edge.id.replace(oldNode.id, newNodeId)
          : `edge_${newSource}_${newTarget}`;
        
        return {
          ...edge,
          id: newId,
          source: newSource,
          target: newTarget,
        };
      });
    }
    
    set({
      workflow: {
        ...workflow,
        nodes: updatedNodes,
        edges: updatedEdges,
      },
      dirty: true,
    });
    
    // 如果更新了配置，重新验证
    if (updates.config) {
      get().validate();
    }
  },
  
  deleteNode: (id: string) => {
    const { workflow } = get();
    if (!workflow) return false;
    
    // 检查是否可以删除
    const canDelete = get().canDeleteNode(id);
    if (typeof canDelete === 'string') {
      console.warn(canDelete);
      return false;
    }
    
    // 删除节点和相关连接
    set({
      workflow: {
        ...workflow,
        nodes: workflow.nodes.filter(n => n.id !== id),
        edges: workflow.edges.filter(e => e.source !== id && e.target !== id),
      },
      dirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return true;
  },
  
  copyNode: (id: string, offset = { x: 50, y: 50 }) => {
    const { workflow } = get();
    if (!workflow) return null;
    
    const node = workflow.nodes.find(n => n.id === id);
    if (!node) return null;
    
    // 检查约束
    const constraints = NODE_CONSTRAINTS[node.type];
    if (!constraints.copyable) {
      console.warn('该节点不允许复制');
      return null;
    }
    
    // 检查是否可以添加
    const canAdd = get().canAddNode(node.type);
    if (typeof canAdd === 'string') {
      console.warn(canAdd);
      return null;
    }
    
    // 创建副本
    const newNodeId = generateNodeId(node.type);
    const newNode: WorkflowNode = {
      ...deepClone(node),
      id: newNodeId,
      name: `${node.name} (副本)`,
      position: {
        x: node.position.x + offset.x,
        y: node.position.y + offset.y,
      },
    };
    
    set({
      workflow: {
        ...workflow,
        nodes: [...workflow.nodes, newNode],
      },
      dirty: true,
    });
    
    return newNodeId;
  },
  
  // ==================== 连接操作 ====================
  
  addEdge: (source: string, target: string) => {
    const { workflow } = get();
    if (!workflow) return null;
    
    // 检查是否可以添加
    const canAdd = get().canAddEdge(source, target);
    if (typeof canAdd === 'string') {
      console.warn(canAdd);
      return null;
    }
    
    // 创建连接
    const edgeId = generateEdgeId(source, target);
    const newEdge: WorkflowEdge = {
      id: edgeId,
      source,
      target,
    };
    
    set({
      workflow: {
        ...workflow,
        edges: [...workflow.edges, newEdge],
      },
      dirty: true,
    });
    
    // 重新验证
    get().validate();
    
    return edgeId;
  },
  
  deleteEdge: (id: string) => {
    const { workflow } = get();
    if (!workflow) return;
    
    set({
      workflow: {
        ...workflow,
        edges: workflow.edges.filter(e => e.id !== id),
      },
      dirty: true,
    });
    
    // 重新验证
    get().validate();
  },
  
  // ==================== 批量操作 ====================
  
  updateNodes: (updates: Array<{ id: string; updates: Partial<WorkflowNode> }>) => {
    const { workflow } = get();
    if (!workflow) return;
    
    const updatedNodes = [...workflow.nodes];
    updates.forEach(({ id, updates: nodeUpdates }) => {
      const index = updatedNodes.findIndex(n => n.id === id);
      if (index !== -1) {
        updatedNodes[index] = {
          ...updatedNodes[index],
          ...nodeUpdates,
        };
      }
    });
    
    set({
      workflow: {
        ...workflow,
        nodes: updatedNodes,
      },
      dirty: true,
    });
  },
  
  deleteNodes: (ids: string[]) => {
    const { workflow } = get();
    if (!workflow) return;
    
    const idsSet = new Set(ids);
    
    set({
      workflow: {
        ...workflow,
        nodes: workflow.nodes.filter(n => !idsSet.has(n.id)),
        edges: workflow.edges.filter(e => !idsSet.has(e.source) && !idsSet.has(e.target)),
      },
      dirty: true,
    });
    
    // 重新验证
    get().validate();
  },
  
  // ==================== 验证 ====================
  
  validate: () => {
    const { workflow } = get();
    if (!workflow) return [];
    
    const errors = validateWorkflow(workflow);
    set({ errors });
    return errors;
  },
  
  validateForRun: () => {
    const { workflow } = get();
    if (!workflow) return [];
    
    // 运行前进行宽松验证（不检查连接，允许调试单独节点）
    const errors = validateWorkflowForDebug(workflow);
    return errors;
  },
  
  // ==================== 工具方法 ====================
  
  getNode: (id: string) => {
    const { workflow } = get();
    return workflow?.nodes.find(n => n.id === id);
  },
  
  getEdge: (id: string) => {
    const { workflow } = get();
    return workflow?.edges.find(e => e.id === id);
  },
  
  canAddNode: (type: NodeType) => {
    const { workflow } = get();
    if (!workflow) return '工作流未加载';
    return checkCanAddNode(workflow, type);
  },
  
  canDeleteNode: (id: string) => {
    const { workflow } = get();
    if (!workflow) return '工作流未加载';
    return checkCanDeleteNode(workflow, id);
  },
  
  canAddEdge: (source: string, target: string) => {
    const { workflow } = get();
    if (!workflow) return '工作流未加载';
    return checkCanAddEdge(workflow, source, target);
  },
}));
