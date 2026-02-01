/**
 * 工作流编辑器自定义 Hooks
 */

import { useState, useCallback } from 'react';
import { message } from 'antd';
import { useClientContext } from '@flowgram.ai/free-layout-editor';
import { useWorkflowStore } from './store';
import { WorkflowEdge } from './types';
import { proxyRequestError } from '../../../../helper/RequestError';
import { GetApiClient } from '../../../ServiceClient';
import { toApiFormat } from './utils';
import type { NodeDesign, KeyValueOfStringAndFieldDesign, FieldDesign, FieldExpressionType, FieldType } from '../../../../apiClient/models';
import { FieldDefine } from './types';
import { EnvOptions } from '../../../../Env';
import useAppStore from '../../../../stateshare/store';

/**
 * 保存工作流的 Hook
 */
export function useSaveWorkflow() {
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const { document } = useClientContext();

  const handleSave = async () => {
    try {
      // 从编辑器文档中获取所有节点
      const allNodes = document.getAllNodes();
      console.log('💾 保存 - 文档节点数:', allNodes.length);
      
      // 获取编辑器的 JSON 数据
      const editorData = document.toJSON();
      console.log('💾 保存 - 编辑器 JSON 数据:', editorData);
      
      // 构建连接关系
      const edges: WorkflowEdge[] = [];
      const edgeSet = new Set<string>(); // 用于去重
      
      // 构建编辑器节点 ID 到 store 节点 ID 的映射
      const editorIdToStoreId = new Map<string, string>();
      const currentWorkflow = store.workflow;
      
      if (currentWorkflow) {
        allNodes.forEach((editorNode: any) => {
          const storeNode = currentWorkflow.nodes.find(n => n.id === editorNode.id);
          if (storeNode) {
            editorIdToStoreId.set(editorNode.id, storeNode.id);
          } else {
            editorIdToStoreId.set(editorNode.id, editorNode.id);
          }
        });
      }
      
      // 方式1：从节点的 outputLines 中提取连接
      allNodes.forEach((node: any) => {
        if (node.lines && node.lines.outputLines) {
          node.lines.outputLines.forEach((line: any) => {
            if (line && !line.isDrawing && !line.isHidden) {
              const sourceId = editorIdToStoreId.get(line.sourceNodeID) || line.sourceNodeID;
              const targetId = editorIdToStoreId.get(line.targetNodeID) || line.targetNodeID;
              const edgeId = `edge_${sourceId}_${targetId}`;
              
              if (!edgeSet.has(edgeId)) {
                edgeSet.add(edgeId);
                edges.push({
                  id: edgeId,
                  source: sourceId,
                  target: targetId,
                  data: line.data,
                });
              }
            }
          });
        }
      });
      
      // 方式2：从编辑器 JSON 数据的节点 edges 属性中提取
      if (editorData.nodes && Array.isArray(editorData.nodes)) {
        editorData.nodes.forEach((node: any) => {
          if (node.edges && Array.isArray(node.edges)) {
            node.edges.forEach((edge: any) => {
              const sourceId = editorIdToStoreId.get(node.id) || node.id;
              const targetId = editorIdToStoreId.get(edge.targetNodeID) || edge.targetNodeID;
              const edgeId = `edge_${sourceId}_${targetId}`;
              
              if (!edgeSet.has(edgeId)) {
                edgeSet.add(edgeId);
                edges.push({
                  id: edgeId,
                  source: sourceId,
                  target: targetId,
                  data: edge.data,
                });
                console.log(`💾 从节点 edges 提取连接: ${sourceId} -> ${targetId}`);
              }
            });
          }
        });
      }
      
      // 方式3：从编辑器 JSON 数据的顶层 edges 中提取
      if (editorData.edges && Array.isArray(editorData.edges)) {
        editorData.edges.forEach((edge: any) => {
          const sourceId = editorIdToStoreId.get(edge.sourceNodeID) || edge.sourceNodeID;
          const targetId = editorIdToStoreId.get(edge.targetNodeID) || edge.targetNodeID;
          const edgeId = `edge_${sourceId}_${targetId}`;
          
          if (!edgeSet.has(edgeId)) {
            edgeSet.add(edgeId);
            edges.push({
              id: edgeId,
              source: sourceId,
              target: targetId,
              data: edge.data,
            });
            console.log(`💾 从顶层 edges 提取连接: ${sourceId} -> ${targetId}`);
          }
        });
      }
      
      console.log('💾 保存 - 提取的 edges:', edges);
      
      if (currentWorkflow) {
        // 更新节点位置信息
        const updatedNodes = currentWorkflow.nodes.map(storeNode => {
          let editorNode: any = allNodes.find((n: any) => n.id === storeNode.id);
          
          if (!editorNode) {
            editorNode = allNodes.find((n: any) => {
              if (!n.meta?.position || !storeNode.position) return false;
              const dx = Math.abs(n.meta.position.x - storeNode.position.x);
              const dy = Math.abs(n.meta.position.y - storeNode.position.y);
              return dx < 10 && dy < 10;
            });
          }
          
          if (editorNode && editorNode.meta?.position) {
            return {
              ...storeNode,
              position: editorNode.meta.position,
            };
          }
          return storeNode;
        });
        
        // 使用提取的 edges，如果为空则保留原有的
        const finalEdges = edges.length > 0 ? edges : currentWorkflow.edges;
        
        console.log('💾 保存 - 最终 workflow.nodes:', updatedNodes.length);
        console.log('💾 保存 - 最终 workflow.edges:', finalEdges);
        
        useWorkflowStore.setState({ 
          workflow: {
            ...currentWorkflow,
            nodes: updatedNodes,
            edges: finalEdges,
          },
          editorRawData: editorData,
          dirty: true 
        });
      }
      
      // 保存
      await store.save();
      messageApi.success('工作流已保存');
    } catch (error) {
      console.error('保存失败:', error);
      proxyRequestError(error, messageApi, '保存工作流失败');
    }
  };

  return {
    handleSave,
    saving: store.saving,
    contextHolder,
  };
}


/**
 * 将内部字段定义转换为 API 的 KeyValueOfStringAndFieldDesign 格式
 */
function convertFieldsToApiFormat(
  fields: FieldDefine[],
  settings: Record<string, any>
): KeyValueOfStringAndFieldDesign[] {
  return fields.map(field => {
    const fieldSetting = settings[field.fieldName];
    const expressionType = field.expressionType 
      || fieldSetting?.expressionType 
      || 'Fixed';
    const value = field.value !== undefined 
      ? (typeof field.value === 'string' ? field.value : JSON.stringify(field.value))
      : (fieldSetting?.value !== undefined 
        ? (typeof fieldSetting.value === 'string' ? fieldSetting.value : JSON.stringify(fieldSetting.value))
        : (field.defaultValue !== undefined ? String(field.defaultValue) : undefined));
    
    const fieldDesign: FieldDesign = {
      fieldName: field.fieldName,
      fieldType: field.fieldType as FieldType,
      expressionType: expressionType as FieldExpressionType,
      value: value,
      description: field.description || '',
    };
    
    return {
      key: field.fieldName,
      value: fieldDesign,
    };
  });
}

/**
 * 调试执行结果项
 */
export interface DebugResultItem {
  nodeKey: string;
  nodeName?: string;
  status: 'running' | 'success' | 'error';
  output?: any;
  error?: string;
  timestamp: number;
}

/**
 * 调试执行工作流的 Hook
 */
export function useDebugWorkflow() {
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const [debugging, setDebugging] = useState(false);
  const [debugResults, setDebugResults] = useState<DebugResultItem[]>([]);
  const { document } = useClientContext();

  const handleDebug = useCallback(async (startupParameters?: Record<string, any>) => {
    const { workflow, teamId, appId } = store;
    
    if (!workflow) {
      messageApi.error('工作流未加载');
      return;
    }

    // 运行前验证
    const errors = store.validateForRun();
    if (errors.length > 0) {
      messageApi.error(`工作流验证失败: ${errors[0].message}`);
      return;
    }

    setDebugging(true);
    setDebugResults([]);

    try {
      // 先同步编辑器数据到 store
      if (document) {
        const allNodes = document.getAllNodes();
        const editorData = document.toJSON();
        const currentWorkflow = store.workflow;
        
        if (currentWorkflow) {
          const updatedNodes = currentWorkflow.nodes.map(storeNode => {
            const editorNode: any = allNodes.find((n: any) => n.id === storeNode.id);
            if (editorNode && editorNode.meta?.position) {
              return { ...storeNode, position: editorNode.meta.position };
            }
            return storeNode;
          });
          
          useWorkflowStore.setState({ 
            workflow: { ...currentWorkflow, nodes: updatedNodes },
            editorRawData: editorData,
          });
        }
      }

      // 获取最新的 workflow
      const latestWorkflow = useWorkflowStore.getState().workflow;
      if (!latestWorkflow) {
        throw new Error('工作流数据丢失');
      }

      // 转换为 API 格式
      const { functionDesign } = toApiFormat(latestWorkflow);
      const nodes: NodeDesign[] = functionDesign.map(node => {
        const inputFieldDesigns = convertFieldsToApiFormat(
          node.inputFields || [],
          node.fieldDesigns || {}
        );
        const outputFieldDesigns = convertFieldsToApiFormat(
          node.outputFields || [],
          node.fieldDesigns || {}
        );
        
        return {
          nodeKey: node.nodeKey,
          nodeType: node.nodeType,
          name: node.name,
          description: node.description,
          nextNodeKeys: node.nextNodeKeys || [],
          inputFieldDesigns,
          outputFieldDesigns,
        };
      });

      // 构建请求 URL
      const serverUrl = EnvOptions.ServerUrl;
      const userInfo = useAppStore.getState().getUserInfo();
      const token = userInfo?.accessToken;
      const url = `${serverUrl}/api/team/workflowapp/debug`;

      // 使用 fetch 发起 SSE 请求
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
          'Accept': 'text/event-stream',
        },
        body: JSON.stringify({
          teamId,
          workflowDefinitionId: appId,
          startupParameters: startupParameters || {},
          nodes,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`调试请求失败: ${response.status} ${errorText}`);
      }

      // 处理 SSE 流
      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('无法读取响应流');
      }

      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data:')) {
            const data = line.slice(5).trim();
            if (data) {
              try {
                const item = JSON.parse(data);
                setDebugResults(prev => [...prev, {
                  nodeKey: item.nodeKey || item.NodeKey,
                  nodeName: item.nodeName || item.NodeName,
                  status: item.isSuccess || item.IsSuccess ? 'success' : 'error',
                  output: item.output || item.Output,
                  error: item.errorMessage || item.ErrorMessage,
                  timestamp: Date.now(),
                }]);
              } catch (e) {
                console.warn('解析 SSE 数据失败:', data, e);
              }
            }
          }
        }
      }

      messageApi.success('工作流执行完成');
    } catch (error) {
      console.error('调试执行失败:', error);
      proxyRequestError(error, messageApi, '调试执行失败');
    } finally {
      setDebugging(false);
    }
  }, [store, document, messageApi]);

  return {
    handleDebug,
    debugging,
    debugResults,
    contextHolder,
  };
}
