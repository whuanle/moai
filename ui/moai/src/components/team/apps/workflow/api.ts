/**
 * 工作流 API 服务 - 重构版
 * 简化的 API 调用层
 */

import { WorkflowData, ApiWorkflowConfig, FieldDefine } from './types';
import { fromApiFormat, toApiFormat } from './utils';
import { GetApiClient } from '../../../ServiceClient';
import type { NodeDesign, KeyValueOfStringAndFieldDesign, FieldDesign, FieldExpressionType, FieldType } from '../../../../apiClient/models';

/**
 * 将内部字段定义转换为 API 的 KeyValueOfStringAndFieldDesign 格式
 * 包含 fieldName, fieldType, expressionType, value
 */
function convertFieldsToApiFormat(
  fields: FieldDefine[],
  settings: Record<string, any>
): KeyValueOfStringAndFieldDesign[] {
  return fields.map(field => {
    // 从 settings 中获取字段的配置值（如果有）
    const fieldSetting = settings[field.fieldName];
    
    // 优先使用字段自身的 expressionType，其次使用 settings 中的，最后默认 Fixed
    const expressionType = field.expressionType 
      || fieldSetting?.expressionType 
      || 'Fixed';
    
    // 优先使用字段自身的 value，其次使用 settings 中的，最后使用 defaultValue
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
    
    console.log('🔍 API save - functionDesign:', functionDesign);
    console.log('🔍 API save - uiDesignDraft:', uiDesignDraft);
    
    // 转换 ApiNodeDesign[] 到 API 需要的 NodeDesign[] 格式
    const nodes: NodeDesign[] = functionDesign.map(node => {
      // 转换输入字段为 KeyValueOfStringAndFieldDesign[] 格式
      const inputFieldDesigns = convertFieldsToApiFormat(
        node.inputFields || [],
        node.fieldDesigns || {}
      );
      
      // 转换输出字段为 KeyValueOfStringAndFieldDesign[] 格式
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
    
    console.log('🔍 API save - nodes:', JSON.stringify(nodes, null, 2));
    
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
