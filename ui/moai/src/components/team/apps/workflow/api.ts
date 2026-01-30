/**
 * 工作流 API 服务 - 重构版
 * 简化的 API 调用层
 */

import { WorkflowData, ApiWorkflowConfig } from './types';
import { fromApiFormat, toApiFormat } from './utils';
import { GetApiClient } from '../../../ServiceClient';

/**
 * 工作流 API 服务
 */
class WorkflowApiService {
  /**
   * 加载工作流
   */
  async load(appId: string, teamId: number): Promise<{ workflow: WorkflowData; editorRawData?: any }> {
    const client = GetApiClient();
    
    const response = await client.api.team.workflowapp.config.post({
      appId,
      teamId,
    });
    
    if (!response) {
      throw new Error('加载工作流失败：响应为空');
    }
    
    // 转换 API 格式到内部格式
    const workflow = fromApiFormat(response as unknown as ApiWorkflowConfig);
    
    // 提取编辑器原始数据
    let editorRawData: any = undefined;
    if (response.uiDesignDraft) {
      try {
        editorRawData = typeof response.uiDesignDraft === 'string' 
          ? JSON.parse(response.uiDesignDraft)
          : response.uiDesignDraft;
        
        console.log('🔍 API load - 加载编辑器原始数据成功');
      } catch (error) {
        console.error('解析 uiDesignDraft 失败:', error);
      }
    }
    
    return { workflow, editorRawData };
  }
  
  /**
   * 保存工作流
   */
  async save(appId: string, teamId: number, workflow: WorkflowData, editorRawData?: any): Promise<void> {
    const client = GetApiClient();
    
    // 转换内部格式到 API 格式
    const { functionDesign } = toApiFormat(workflow);
    
    // 使用编辑器原始数据作为 uiDesignDraft
    const uiDesignDraft = editorRawData ? JSON.stringify(editorRawData) : undefined;
    
    console.log('🔍 API save - uiDesignDraft:', uiDesignDraft);
    
    // 转换 ApiNodeDesign[] 到 API 需要的格式
    const nodes = functionDesign.map(node => ({
      nodeKey: node.nodeKey,
      nodeType: node.nodeType,
      name: node.name,
      description: node.description,
      nextNodeKeys: node.nextNodeKeys,
      fieldDesigns: node.fieldDesigns 
        ? Object.entries(node.fieldDesigns).map(([key, value]) => ({
            key,
            value,
          }))
        : undefined,
    }));
    
    await client.api.team.workflowapp.update.put({
      appId,
      teamId,
      name: workflow.name,
      description: workflow.description,
      nodes,
      uiDesignDraft,
    });
  }
}

// 导出单例
export const workflowApi = new WorkflowApiService();
