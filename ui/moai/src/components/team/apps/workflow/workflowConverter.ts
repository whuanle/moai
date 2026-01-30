/**
 * 工作流数据转换工具
 * 在画布编辑器格式和内部存储格式之间转换
 */

import { WorkflowJSON } from '@flowgram.ai/free-layout-editor';
import { BackendWorkflowData, CanvasWorkflowData, BackendNodeData, CanvasNodeData } from './workflowStore';
import { NodeType } from './types';

/**
 * 将内部存储格式转换为画布编辑器格式
 */
export function toEditorFormat(
  backend: BackendWorkflowData,
  canvas: CanvasWorkflowData
): WorkflowJSON {
  // 合并后端数据和画布数据
  const nodes = backend.nodes.map(backendNode => {
    const canvasNode = canvas.nodes.find(n => n.id === backendNode.id);
    
    return {
      id: backendNode.id,
      type: backendNode.type,
      meta: {
        position: canvasNode?.position || { x: 0, y: 0 },
        defaultExpanded: canvasNode?.ui.expanded ?? true,
      },
      data: {
        title: canvasNode?.title || backendNode.name,
        content: canvasNode?.content || backendNode.description,
        inputFields: backendNode.config.inputFields,
        outputFields: backendNode.config.outputFields,
        // 存储后端配置
        _backendConfig: backendNode.config,
        _execution: backendNode.execution,
      },
      blocks: [],
      edges: [],
    };
  });
  
  const edges = backend.edges.map(backendEdge => {
    const canvasEdge = canvas.edges.find(e => e.id === backendEdge.id);
    
    return {
      sourceNodeID: backendEdge.sourceNodeId,
      targetNodeID: backendEdge.targetNodeId,
      data: {
        _backendConfig: {
          sourceField: backendEdge.sourceField,
          targetField: backendEdge.targetField,
          condition: backendEdge.condition,
          label: backendEdge.label,
        },
        _uiConfig: canvasEdge?.ui,
      },
    };
  });
  
  return { nodes, edges };
}

/**
 * 从画布编辑器格式转换为内部存储格式
 */
export function fromEditorFormat(editorData: WorkflowJSON): {
  backend: Partial<BackendWorkflowData>;
  canvas: CanvasWorkflowData;
} {
  // 提取后端数据
  const backendNodes: BackendNodeData[] = editorData.nodes.map(node => {
    const data = node.data as Record<string, unknown>;
    const backendConfig = data._backendConfig as BackendNodeData['config'] | undefined;
    const execution = data._execution as BackendNodeData['execution'] | undefined;
    
    return {
      id: node.id,
      type: node.type as NodeType,
      name: (data.title as string) || '未命名节点',
      description: (data.content as string) || '',
      config: backendConfig || {
        inputFields: [],
        outputFields: [],
        settings: {},
      },
      execution: execution || {
        timeout: 30000,
        retryCount: 0,
        errorHandling: 'stop',
      },
    };
  });
  
  const backendEdges = editorData.edges.map((edge, index) => {
    const data = edge.data as Record<string, unknown> | undefined;
    const backendConfig = data?._backendConfig as Record<string, unknown> | undefined;
    
    return {
      id: `edge_${index}`,
      sourceNodeId: edge.sourceNodeID,
      targetNodeId: edge.targetNodeID,
      sourceField: backendConfig?.sourceField as string | undefined,
      targetField: backendConfig?.targetField as string | undefined,
      condition: backendConfig?.condition as string | undefined,
      label: backendConfig?.label as string | undefined,
    };
  });
  
  // 提取画布数据
  const canvasNodes: CanvasNodeData[] = editorData.nodes.map(node => {
    const data = node.data as Record<string, unknown>;
    const position = node.meta?.position as { x: number; y: number } | undefined;
    
    return {
      id: node.id,
      type: node.type as NodeType,
      position: position || { x: 0, y: 0 },
      ui: {
        expanded: node.meta?.defaultExpanded ?? true,
        selected: false,
        highlighted: false,
      },
      title: (data.title as string) || '未命名节点',
      content: (data.content as string) || '',
    };
  });
  
  const canvasEdges = editorData.edges.map((edge, index) => {
    const data = edge.data as Record<string, unknown> | undefined;
    const uiConfig = data?._uiConfig as CanvasNodeData['ui'] | undefined;
    
    return {
      id: `edge_${index}`,
      sourceNodeId: edge.sourceNodeID,
      targetNodeId: edge.targetNodeID,
      ui: uiConfig || {
        selected: false,
        style: 'solid' as const,
      },
    };
  });
  
  return {
    backend: {
      nodes: backendNodes,
      edges: backendEdges,
    },
    canvas: {
      nodes: canvasNodes,
      edges: canvasEdges,
      viewport: {
        zoom: 1,
        offsetX: 0,
        offsetY: 0,
      },
    },
  };
}

/**
 * 同步画布编辑器的变更到内部存储
 */
export function syncEditorChanges(
  editorData: WorkflowJSON,
  currentBackend: BackendWorkflowData,
  currentCanvas: CanvasWorkflowData
): {
  backend: BackendWorkflowData;
  canvas: CanvasWorkflowData;
} {
  const { backend: newBackend, canvas: newCanvas } = fromEditorFormat(editorData);
  
  // 合并后端数据（保留现有配置）
  const mergedBackendNodes = newBackend.nodes!.map(newNode => {
    const existingNode = currentBackend.nodes.find(n => n.id === newNode.id);
    if (existingNode) {
      // 保留现有配置，只更新位置等 UI 相关数据
      return {
        ...existingNode,
        name: newNode.name,
        description: newNode.description,
      };
    }
    return newNode;
  });
  
  const mergedBackendEdges = newBackend.edges!.map(newEdge => {
    const existingEdge = currentBackend.edges.find(
      e => e.sourceNodeId === newEdge.sourceNodeId && e.targetNodeId === newEdge.targetNodeId
    );
    if (existingEdge) {
      return existingEdge;
    }
    return newEdge;
  });
  
  return {
    backend: {
      ...currentBackend,
      nodes: mergedBackendNodes,
      edges: mergedBackendEdges,
    },
    canvas: newCanvas,
  };
}

/**
 * 将后端 FunctionDesign 转换为 BackendWorkflowData
 * 支持两种格式：
 * 1. 字符串格式（旧格式）：包含 nodes 和 connections 的对象
 * 2. 数组格式（新格式）：直接是节点数组
 */
export function parseFunctionDesign(
  functionDesign: string | any[]
): Partial<BackendWorkflowData> {
  try {
    let data: any;
    
    // 如果是字符串，解析为对象
    if (typeof functionDesign === 'string') {
      data = JSON.parse(functionDesign);
    } else {
      data = functionDesign;
    }
    
    // 如果是数组格式（新格式）
    if (Array.isArray(data)) {
      const nodes = data.map((node: any) => ({
        id: node.nodeKey,
        type: node.nodeType,
        name: node.name,
        description: node.description,
        config: {
          inputFields: (node.inputFields || []) as any[],
          outputFields: (node.outputFields || []) as any[],
          settings: node.fieldDesigns || {}
        },
        execution: {
          timeout: 30000,
          retryCount: 0,
          errorHandling: 'stop' as const,
        }
      }));
      
      // 从 nextNodeKeys 构建连接
      const edges: any[] = [];
      data.forEach((node: any) => {
        if (node.nextNodeKeys && Array.isArray(node.nextNodeKeys)) {
          node.nextNodeKeys.forEach((targetKey: string) => {
            edges.push({
              id: `edge_${node.nodeKey}_${targetKey}`,
              sourceNodeId: node.nodeKey,
              targetNodeId: targetKey,
            });
          });
        }
      });
      
      return { nodes, edges };
    }
    
    // 如果是对象格式（旧格式）
    return {
      nodes: data.nodes?.map((node: any) => ({
        id: node.nodeKey,
        type: node.nodeType,
        name: node.name,
        description: node.description,
        config: {
          inputFields: (node.inputFields || []) as any[],
          outputFields: (node.outputFields || []) as any[],
          settings: node.fieldDesigns || {}
        },
        execution: {
          timeout: 30000,
          retryCount: 0,
          errorHandling: 'stop' as const,
        }
      })) || [],
      edges: data.connections?.map((conn: any, index: number) => ({
        id: `edge_${index}`,
        sourceNodeId: conn.sourceNodeKey,
        targetNodeId: conn.targetNodeKey,
        sourceField: conn.sourceField,
        targetField: conn.targetField,
        condition: conn.condition,
        label: conn.label
      })) || []
    };
  } catch (error) {
    console.error('解析 FunctionDesign 失败:', error);
    return { nodes: [], edges: [] };
  }
}

/**
 * 将 BackendWorkflowData 转换为后端 FunctionDesign
 */
export function toFunctionDesign(
  backend: BackendWorkflowData
): string {
  const data = {
    nodes: backend.nodes.map(node => ({
      nodeKey: node.id,
      nodeType: node.type,
      name: node.name,
      description: node.description,
      inputFields: node.config.inputFields,
      outputFields: node.config.outputFields,
      fieldDesigns: node.config.settings
    })),
    connections: backend.edges.map(edge => ({
      sourceNodeKey: edge.sourceNodeId,
      targetNodeKey: edge.targetNodeId,
      sourceField: edge.sourceField,
      targetField: edge.targetField,
      condition: edge.condition,
      label: edge.label
    }))
  };
  
  return JSON.stringify(data);
}

/**
 * 解析后端 UiDesign 为 CanvasWorkflowData
 */
export function parseUiDesign(uiDesign: string): Partial<CanvasWorkflowData> {
  try {
    const data = JSON.parse(uiDesign);
    
    return {
      nodes: data.nodes?.map((node: any) => ({
        id: node.id,
        type: node.type || NodeType.Start,
        position: node.position || { x: 0, y: 0 },
        ui: node.ui || {
          expanded: true,
          selected: false,
          highlighted: false
        },
        title: node.title || '',
        content: node.content || ''
      })) || [],
      edges: data.edges?.map((edge: any) => ({
        id: edge.id,
        sourceNodeId: edge.sourceNodeId,
        targetNodeId: edge.targetNodeId,
        ui: edge.ui || {
          selected: false,
          style: 'solid' as const
        }
      })) || [],
      viewport: data.viewport || {
        zoom: 1,
        offsetX: 0,
        offsetY: 0
      }
    };
  } catch (error) {
    console.error('解析 UiDesign 失败:', error);
    return {
      nodes: [],
      edges: [],
      viewport: { zoom: 1, offsetX: 0, offsetY: 0 }
    };
  }
}

/**
 * 将 CanvasWorkflowData 转换为后端 UiDesign
 */
export function toUiDesign(canvas: CanvasWorkflowData): string {
  return JSON.stringify({
    nodes: canvas.nodes.map(node => ({
      id: node.id,
      type: node.type,
      position: node.position,
      ui: node.ui,
      title: node.title,
      content: node.content
    })),
    edges: canvas.edges.map(edge => ({
      id: edge.id,
      sourceNodeId: edge.sourceNodeId,
      targetNodeId: edge.targetNodeId,
      ui: edge.ui
    })),
    viewport: canvas.viewport
  });
}

/**
 * 验证数据一致性（增强版本，支持自动修复）
 */
export function validateDataConsistency(
  backend: BackendWorkflowData,
  canvas: CanvasWorkflowData
): { valid: boolean; errors: string[]; fixed: boolean } {
  const errors: string[] = [];
  let fixed = false;
  
  // 检查节点数量是否一致
  if (backend.nodes.length !== canvas.nodes.length) {
    errors.push(`节点数量不一致: 后端 ${backend.nodes.length}, 画布 ${canvas.nodes.length}`);
  }
  
  // 检查连接数量是否一致
  if (backend.edges.length !== canvas.edges.length) {
    errors.push(`连接数量不一致: 后端 ${backend.edges.length}, 画布 ${canvas.edges.length}`);
  }
  
  // 检查节点 ID 是否匹配
  const backendNodeIds = new Set(backend.nodes.map(n => n.id));
  const canvasNodeIds = new Set(canvas.nodes.map(n => n.id));
  
  backend.nodes.forEach(node => {
    if (!canvasNodeIds.has(node.id)) {
      errors.push(`后端节点 ${node.id} 在画布中不存在`);
    }
  });
  
  canvas.nodes.forEach(node => {
    if (!backendNodeIds.has(node.id)) {
      errors.push(`画布节点 ${node.id} 在后端中不存在`);
    }
  });
  
  // 检查并移除无效连接
  const validBackendEdges = backend.edges.filter(edge => {
    const isValid = backendNodeIds.has(edge.sourceNodeId) && backendNodeIds.has(edge.targetNodeId);
    if (!isValid) {
      errors.push(`移除无效连接 ${edge.id}: 源节点 ${edge.sourceNodeId} 或目标节点 ${edge.targetNodeId} 不存在`);
      fixed = true;
    }
    return isValid;
  });
  
  if (validBackendEdges.length !== backend.edges.length) {
    backend.edges = validBackendEdges;
  }
  
  const validCanvasEdges = canvas.edges.filter(edge => {
    const isValid = canvasNodeIds.has(edge.sourceNodeId) && canvasNodeIds.has(edge.targetNodeId);
    return isValid;
  });
  
  if (validCanvasEdges.length !== canvas.edges.length) {
    canvas.edges = validCanvasEdges;
  }
  
  return {
    valid: errors.length === 0,
    errors,
    fixed,
  };
}
