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
      // 存储后端配置
      meta: {
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
    const meta = edge.meta as Record<string, unknown> | undefined;
    const backendConfig = meta?._backendConfig as Record<string, unknown> | undefined;
    
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
    const meta = edge.meta as Record<string, unknown> | undefined;
    const uiConfig = meta?._uiConfig as CanvasNodeData['ui'] | undefined;
    
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
 * 验证数据一致性
 */
export function validateDataConsistency(
  backend: BackendWorkflowData,
  canvas: CanvasWorkflowData
): { valid: boolean; errors: string[] } {
  const errors: string[] = [];
  
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
  
  // 检查连接引用的节点是否存在
  backend.edges.forEach(edge => {
    if (!backendNodeIds.has(edge.sourceNodeId)) {
      errors.push(`连接 ${edge.id} 的源节点 ${edge.sourceNodeId} 不存在`);
    }
    if (!backendNodeIds.has(edge.targetNodeId)) {
      errors.push(`连接 ${edge.id} 的目标节点 ${edge.targetNodeId} 不存在`);
    }
  });
  
  return {
    valid: errors.length === 0,
    errors,
  };
}
