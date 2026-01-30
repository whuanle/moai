/**
 * 工作流工具函数 - 重构版
 * 合并 workflowConverter.ts 和验证逻辑
 */

import { 
  WorkflowData, 
  WorkflowNode, 
  WorkflowEdge,
  ApiWorkflowConfig,
  ApiNodeDesign,
  ValidationError,
  ValidationErrorType,
  NodeType,
  FieldType
} from './types';
import { NODE_CONSTRAINTS } from './constants';

// ==================== API 数据转换 ====================

/**
 * 将 API 数据转换为内部格式
 */
export function fromApiFormat(apiData: ApiWorkflowConfig): WorkflowData {
  console.log('🔍 fromApiFormat - API 数据:', apiData);
  
  // 解析节点数据 - 优先使用 functionDesignDraft（草稿），其次使用 functionDesign（已发布版本）
  let nodeDesigns: ApiNodeDesign[] = [];
  const functionSource = apiData.functionDesignDraft || apiData.functionDesign;
  
  if (typeof functionSource === 'string') {
    try {
      const parsed = JSON.parse(functionSource);
      nodeDesigns = Array.isArray(parsed) ? parsed : parsed.nodes || [];
      console.log('🔍 fromApiFormat - 从字符串解析节点设计');
    } catch (error) {
      console.error('解析 functionDesign 失败:', error);
    }
  } else if (Array.isArray(functionSource)) {
    nodeDesigns = functionSource;
    console.log('🔍 fromApiFormat - 使用数组格式的节点设计');
  }
  
  console.log('🔍 fromApiFormat - 节点设计数量:', nodeDesigns.length);
  
  // 解析 UI 数据 - 优先使用 uiDesignDraft（草稿），其次使用 uiDesign（已发布版本）
  let uiData: any = {};
  const uiSource = apiData.uiDesignDraft || apiData.uiDesign;
  
  if (typeof uiSource === 'string') {
    try {
      uiData = JSON.parse(uiSource);
      console.log('🔍 fromApiFormat - 从字符串解析 UI 数据');
    } catch (error) {
      console.error('解析 UI 数据失败:', error);
    }
  } else if (uiSource) {
    uiData = uiSource;
    console.log('🔍 fromApiFormat - 使用对象格式的 UI 数据');
  }
  
  console.log('🔍 fromApiFormat - UI 数据:', {
    hasNodes: !!uiData.nodes,
    nodesCount: uiData.nodes?.length || 0,
    hasEdges: !!uiData.edges,
    edgesCount: uiData.edges?.length || 0,
    hasViewport: !!uiData.viewport
  });
  
  // 转换节点
  const nodes: WorkflowNode[] = nodeDesigns.map((design, index) => {
    const uiNode = uiData.nodes?.find((n: any) => n.id === design.nodeKey);
    
    return {
      id: design.nodeKey,
      type: design.nodeType,
      name: design.name,
      description: design.description,
      position: uiNode?.position || { 
        x: 100 + index * 300, 
        y: 200 
      },
      config: {
        inputFields: design.inputFields || [],
        outputFields: design.outputFields || [],
        settings: design.fieldDesigns || {},
      },
      ui: uiNode?.ui,
    };
  });
  
  // 转换连接
  // 优先使用 uiData.edges（如果存在），否则从 nextNodeKeys 构建
  let edges: WorkflowEdge[] = [];
  
  if (uiData.edges && Array.isArray(uiData.edges)) {
    // 从 uiData 还原 edges（保持完整的画布状态）
    console.log('🔍 fromApiFormat - 从 uiData.edges 还原连接');
    edges = uiData.edges.map((edge: any) => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      data: edge.data,
    }));
  } else {
    // 从 nextNodeKeys 构建 edges（向后兼容）
    console.log('🔍 fromApiFormat - 从 nextNodeKeys 构建连接');
    nodeDesigns.forEach(design => {
      if (design.nextNodeKeys && Array.isArray(design.nextNodeKeys)) {
        design.nextNodeKeys.forEach(targetKey => {
          edges.push({
            id: `edge_${design.nodeKey}_${targetKey}`,
            source: design.nodeKey,
            target: targetKey,
          });
        });
      }
    });
  }
  
  console.log('🔍 fromApiFormat - 转换结果:', {
    nodes: nodes.length,
    edges: edges.length,
    viewport: uiData.viewport || { zoom: 1, x: 0, y: 0 }
  });
  
  return {
    id: apiData.id,
    name: apiData.name,
    description: apiData.description,
    nodes,
    edges,
    viewport: uiData.viewport || { zoom: 1, x: 0, y: 0 },
  };
}

/**
 * 将内部格式转换为 API 数据
 */
export function toApiFormat(workflow: WorkflowData): {
  functionDesign: ApiNodeDesign[];
  uiDesign: string;
  uiDesignRaw?: any;  // 原始编辑器数据
} {
  console.log('🔍 toApiFormat - 输入的 workflow:', workflow);
  console.log('🔍 toApiFormat - workflow.edges:', workflow.edges);
  
  // 构建节点的下游连接映射（从 edges 中提取）
  const nextNodeKeysMap = new Map<string, string[]>();
  
  workflow.edges.forEach(edge => {
    // 使用 source 作为 key（这是节点 ID）
    const sourceId = edge.source;
    const targetId = edge.target;
    
    if (!nextNodeKeysMap.has(sourceId)) {
      nextNodeKeysMap.set(sourceId, []);
    }
    nextNodeKeysMap.get(sourceId)!.push(targetId);
  });
  
  console.log('🔍 toApiFormat - nextNodeKeysMap:', Object.fromEntries(nextNodeKeysMap));
  
  // 转换节点
  const functionDesign: ApiNodeDesign[] = workflow.nodes.map(node => {
    const nextKeys = nextNodeKeysMap.get(node.id) || [];
    console.log(`🔍 节点 ${node.id} 的 nextNodeKeys:`, nextKeys);
    
    return {
      nodeKey: node.id,
      nodeType: node.type,
      name: node.name,
      description: node.description,
      inputFields: node.config.inputFields,
      outputFields: node.config.outputFields,
      fieldDesigns: node.config.settings,
      nextNodeKeys: nextKeys,
    };
  });
  
  console.log('🔍 toApiFormat - 生成的 functionDesign:', functionDesign.map(n => ({
    nodeKey: n.nodeKey,
    nextNodeKeys: n.nextNodeKeys
  })));
  
  // 转换 UI 数据 - 保存完整的画布状态（简化版，仅用于向后兼容）
  const uiDesign = JSON.stringify({
    nodes: workflow.nodes.map(node => ({
      id: node.id,
      position: node.position,
      ui: node.ui,
    })),
    edges: workflow.edges,
    viewport: workflow.viewport,
  });
  
  return { functionDesign, uiDesign };
}

// ==================== FlowGram 编辑器格式转换 ====================

/**
 * 转换为 FlowGram 编辑器格式
 */
export function toEditorFormat(workflow: WorkflowData): any {
  // 为每个节点构建其出边列表
  const nodeEdgesMap = new Map<string, any[]>();
  
  workflow.edges.forEach(edge => {
    if (!nodeEdgesMap.has(edge.source)) {
      nodeEdgesMap.set(edge.source, []);
    }
    nodeEdgesMap.get(edge.source)!.push({
      targetNodeID: edge.target,
      data: edge.data,
    });
  });
  
  const nodes = workflow.nodes.map(node => ({
    id: node.id,
    type: node.type,
    meta: {
      position: node.position,
      defaultExpanded: node.ui?.expanded ?? true,
    },
    data: {
      title: node.name,
      content: node.description,
      inputFields: node.config.inputFields,
      outputFields: node.config.outputFields,
      settings: node.config.settings,
    },
    blocks: [],
    edges: nodeEdgesMap.get(node.id) || [],  // 每个节点包含其出边
  }));
  
  console.log('🔍 toEditorFormat - 输出:', { 
    nodes: nodes.length, 
    totalEdges: workflow.edges.length,
    nodesWithEdges: nodes.filter(n => n.edges.length > 0).length
  });
  
  return { nodes, edges: [] };  // 顶层 edges 为空数组
}

/**
 * 从 FlowGram 编辑器格式转换
 */
export function fromEditorFormat(editorData: any, currentWorkflow: WorkflowData): WorkflowData {
  console.log('🔍 fromEditorFormat - 原始编辑器数据:', JSON.stringify(editorData, null, 2));
  
  const nodes: WorkflowNode[] = editorData.nodes.map((node: any) => {
    // 查找现有节点以保留配置
    const existingNode = currentWorkflow.nodes.find(n => n.id === node.id);
    
    return {
      id: node.id,
      type: node.type as NodeType,
      name: node.data?.title || existingNode?.name || '未命名节点',
      description: node.data?.content || existingNode?.description,
      position: node.meta?.position || { x: 0, y: 0 },
      config: existingNode?.config || {
        inputFields: node.data?.inputFields || [],
        outputFields: node.data?.outputFields || [],
        settings: node.data?.settings || {},
      },
      ui: {
        expanded: node.meta?.defaultExpanded ?? true,
        selected: false,
      },
    };
  });
  
  // 从节点的 edges 属性中提取所有连接
  const edges: WorkflowEdge[] = [];
  
  // 方式1：从节点的 edges 属性提取
  editorData.nodes.forEach((node: any) => {
    console.log(`🔍 节点 ${node.id} 的 edges:`, node.edges);
    if (node.edges && Array.isArray(node.edges)) {
      node.edges.forEach((edge: any) => {
        console.log(`🔍 添加连接: ${node.id} -> ${edge.targetNodeID}`);
        edges.push({
          id: `edge_${node.id}_${edge.targetNodeID}`,
          source: node.id,
          target: edge.targetNodeID,
          data: edge.data,
        });
      });
    }
  });
  
  // 方式2：如果顶层有 edges，也尝试提取
  if (editorData.edges && Array.isArray(editorData.edges)) {
    console.log('🔍 从顶层 edges 提取:', editorData.edges);
    editorData.edges.forEach((edge: any) => {
      // 检查是否已存在
      const edgeId = `edge_${edge.sourceNodeID}_${edge.targetNodeID}`;
      if (!edges.find(e => e.id === edgeId)) {
        edges.push({
          id: edgeId,
          source: edge.sourceNodeID,
          target: edge.targetNodeID,
          data: edge.data,
        });
      }
    });
  }
  
  console.log('🔍 fromEditorFormat - 转换结果:', {
    nodes: nodes.length,
    edges: edges.length,
    edgesList: edges
  });
  
  return {
    ...currentWorkflow,
    nodes,
    edges,
  };
}

// ==================== 验证逻辑 ====================

/**
 * 验证工作流
 */
export function validateWorkflow(workflow: WorkflowData): ValidationError[] {
  const errors: ValidationError[] = [];
  
  // 检查开始节点
  const startNodes = workflow.nodes.filter(n => n.type === NodeType.Start);
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
  const endNodes = workflow.nodes.filter(n => n.type === NodeType.End);
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
  
  // 检查节点连接
  workflow.nodes.forEach(node => {
    const constraints = NODE_CONSTRAINTS[node.type];
    const hasIncoming = workflow.edges.some(e => e.target === node.id);
    const hasOutgoing = workflow.edges.some(e => e.source === node.id);
    
    if (constraints.requiresInput && !hasIncoming) {
      errors.push({
        type: ValidationErrorType.DisconnectedNode,
        message: `节点 "${node.name}" 缺少输入连接`,
        nodeId: node.id,
      });
    }
    
    if (constraints.requiresOutput && !hasOutgoing) {
      errors.push({
        type: ValidationErrorType.DisconnectedNode,
        message: `节点 "${node.name}" 缺少输出连接`,
        nodeId: node.id,
      });
    }
  });
  
  // 检查必填字段
  workflow.nodes.forEach(node => {
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
  
  // 检查环路
  const cycles = detectCycles(workflow);
  if (cycles.length > 0) {
    errors.push({
      type: ValidationErrorType.CyclicDependency,
      message: `工作流存在环路: ${cycles.join(' -> ')}`,
    });
  }
  
  return errors;
}

/**
 * 检测环路
 */
function detectCycles(workflow: WorkflowData): string[] {
  const visited = new Set<string>();
  const recStack = new Set<string>();
  const path: string[] = [];
  
  function dfs(nodeId: string): boolean {
    visited.add(nodeId);
    recStack.add(nodeId);
    path.push(nodeId);
    
    const outgoingEdges = workflow.edges.filter(e => e.source === nodeId);
    for (const edge of outgoingEdges) {
      if (!visited.has(edge.target)) {
        if (dfs(edge.target)) {
          return true;
        }
      } else if (recStack.has(edge.target)) {
        // 找到环路
        const cycleStart = path.indexOf(edge.target);
        return true;
      }
    }
    
    recStack.delete(nodeId);
    path.pop();
    return false;
  }
  
  for (const node of workflow.nodes) {
    if (!visited.has(node.id)) {
      if (dfs(node.id)) {
        return path;
      }
    }
  }
  
  return [];
}

/**
 * 检查是否可以添加节点
 */
export function canAddNode(workflow: WorkflowData, type: NodeType): boolean | string {
  const constraints = NODE_CONSTRAINTS[type];
  
  if (constraints.maxCount !== -1) {
    const currentCount = workflow.nodes.filter(n => n.type === type).length;
    if (currentCount >= constraints.maxCount) {
      return `${type} 节点数量已达上限 (${constraints.maxCount})`;
    }
  }
  
  return true;
}

/**
 * 检查是否可以删除节点
 */
export function canDeleteNode(workflow: WorkflowData, nodeId: string): boolean | string {
  const node = workflow.nodes.find(n => n.id === nodeId);
  if (!node) {
    return '节点不存在';
  }
  
  const constraints = NODE_CONSTRAINTS[node.type];
  
  if (!constraints.deletable) {
    return '该节点不允许删除';
  }
  
  const currentCount = workflow.nodes.filter(n => n.type === node.type).length;
  if (currentCount <= constraints.minCount) {
    return `${node.type} 节点数量不能少于 ${constraints.minCount}`;
  }
  
  return true;
}

/**
 * 检查是否可以添加连接
 */
export function canAddEdge(
  workflow: WorkflowData, 
  source: string, 
  target: string
): boolean | string {
  // 检查节点是否存在
  const sourceNode = workflow.nodes.find(n => n.id === source);
  const targetNode = workflow.nodes.find(n => n.id === target);
  
  if (!sourceNode || !targetNode) {
    return '源节点或目标节点不存在';
  }
  
  // 检查是否已存在连接
  const existingEdge = workflow.edges.find(
    e => e.source === source && e.target === target
  );
  if (existingEdge) {
    return '连接已存在';
  }
  
  // 检查是否连接到自身
  if (source === target) {
    return '不能连接到自身';
  }
  
  // 检查是否会形成环路
  const tempWorkflow = {
    ...workflow,
    edges: [...workflow.edges, { id: 'temp', source, target }],
  };
  const cycles = detectCycles(tempWorkflow);
  if (cycles.length > 0) {
    return '该连接会形成环路';
  }
  
  return true;
}

// ==================== 工具函数 ====================

/**
 * 深度克隆对象
 */
export function deepClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

/**
 * 生成默认工作流
 */
export function createDefaultWorkflow(name: string = '新建工作流'): WorkflowData {
  return {
    id: '',
    name,
    description: '',
    nodes: [],
    edges: [],
    viewport: { zoom: 1, x: 0, y: 0 },
  };
}
