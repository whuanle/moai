/**
 * 工作流编辑器自定义 Hooks
 */

import { message } from 'antd';
import { useClientContext } from '@flowgram.ai/free-layout-editor';
import { useWorkflowStore } from './store';
import { fromEditorFormat } from './utils';
import { WorkflowEdge } from './types';
import { proxyRequestError } from '../../../../helper/RequestError';

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
      
      // 构建连接关系（从节点的输出线条中提取）
      const edges: WorkflowEdge[] = [];
      const edgeSet = new Set<string>(); // 用于去重
      
      allNodes.forEach((node: any) => {
        // 从输出线条中提取连接
        if (node.lines && node.lines.outputLines) {
          node.lines.outputLines.forEach((line: any) => {
            if (line && !line.isDrawing && !line.isHidden) {
              const edgeId = `edge_${line.sourceNodeID}_${line.targetNodeID}`;
              
              // 避免重复添加
              if (!edgeSet.has(edgeId)) {
                edgeSet.add(edgeId);
                edges.push({
                  id: edgeId,
                  source: line.sourceNodeID,
                  target: line.targetNodeID,
                  data: line.data,
                });
              }
            }
          });
        }
      });
      
      console.log('💾 保存 - 提取的 edges:', edges);
      
      // 同步编辑器数据到 store
      const editorData = document.toJSON();
      const currentWorkflow = store.workflow;
      
      if (currentWorkflow) {
        // 从编辑器数据构建 workflow
        const updatedWorkflow = fromEditorFormat(editorData, currentWorkflow);
        
        // 使用从节点提取的 edges（这是实际的连接状态）
        updatedWorkflow.edges = edges;
        
        console.log('💾 保存 - 最终 workflow.nodes:', updatedWorkflow.nodes.length);
        console.log('💾 保存 - 最终 workflow.edges:', updatedWorkflow.edges);
        
        useWorkflowStore.setState({ 
          workflow: updatedWorkflow,
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
