/**
 * 工作流 API 服务层
 * 封装所有工作流相关的 API 调用
 */

import { GetApiClient } from '../../../ServiceClient';
import { MoAIClient } from '../../../../apiClient/moAIClient';
import { BackendWorkflowData, BackendNodeData, BackendEdgeData } from './workflowStore';
import { NodeType } from './types';
import type { 
  NodeDesign,
  FieldDesign
} from '../../../../apiClient/models';

// 导出 API 类型供其他模块使用
export type { FieldDesign };

// ==================== API 响应类型 ====================

/**
 * 工作流定义响应
 */
export interface WorkflowDefinitionResponse {
  id: string;
  appId: string;
  name: string;
  description?: string;
  avatar?: string;
  teamId: number;
  uiDesign?: string;
  uiDesignDraft?: string;
  functionDesign?: string;
  functionDesignDraft?: string;
  isPublish: boolean;
  createTime?: string;
  updateTime?: string;
}

/**
 * 节点定义项（使用 API 类型）
 */
export interface NodeDefineItem {
  nodeType?: string;
  pluginId?: number;
  wikiId?: number;
  modelId?: number;
  inputFields?: FieldDesign[];
  outputFields?: FieldDesign[];
}

/**
 * 内部工作流数据
 */
interface WorkflowData {
  id: string;
  appId: string;
  name: string;
  description: string;
  uiDesign: any;
  functionDesign: any;
  isPublish: boolean;
  isDraft: boolean;
}

// ==================== API 服务类 ====================

export class WorkflowApiService {
  private apiClient: MoAIClient;
  private nodeDefineCache: Map<string, NodeDefineItem>;

  constructor() {
    this.apiClient = GetApiClient();
    this.nodeDefineCache = new Map();
  }

  /**
   * 加载工作流定义
   */
  async loadWorkflow(appId: string, teamId: number): Promise<WorkflowData> {
    const response = await this.apiClient.api.team.workflowapp.config.post({
      appId,
      teamId,
    });

    if (!response) {
      throw new Error('加载工作流失败：响应为空');
    }

    return this.parseWorkflowResponse(response);
  }

  /**
   * 保存工作流定义
   */
  async saveWorkflow(
    appId: string,
    teamId: number,
    workflow: BackendWorkflowData,
    uiDesign: string
  ): Promise<void> {
    await this.apiClient.api.team.workflowapp.update.put({
      appId,
      teamId,
      name: workflow.name,
      description: workflow.description,
      avatar: '',
      nodes: this.convertToNodeDesigns(workflow.nodes),
      uiDesignDraft: uiDesign,
    });
  }

  /**
   * 批量查询节点定义
   * 注意：此功能需要后端 API 支持，当前暂时返回空数组
   */
  async queryNodeDefinitions(
    teamId: number,
    nodeQueries: Map<NodeType, string[]>
  ): Promise<NodeDefineItem[]> {
    // 检查缓存
    const uncachedQueries = this.filterUncached(nodeQueries);

    if (uncachedQueries.size === 0) {
      return this.getCachedDefinitions(nodeQueries);
    }

    // TODO: 等待后端 API 实现节点定义查询接口
    // 当前返回空数组，避免编译错误
    console.warn('节点定义查询功能暂未实现，teamId:', teamId, 'queries:', uncachedQueries);
    
    return [];
  }

  /**
   * 查询单个节点定义
   */
  async queryNodeDefinition(
    teamId: number,
    nodeType: NodeType,
    instanceId?: string
  ): Promise<NodeDefineItem | null> {
    const cacheKey = this.getCacheKey({ nodeType, instanceId });

    // 检查缓存
    if (this.nodeDefineCache.has(cacheKey)) {
      return this.nodeDefineCache.get(cacheKey)!;
    }

    // 调用批量查询 API
    const queries = new Map([[nodeType, instanceId ? [instanceId] : []]]);
    const results = await this.queryNodeDefinitions(teamId, queries);

    return results.find(r =>
      r.nodeType === nodeType &&
      this.matchesInstance(r, instanceId)
    ) || null;
  }

  /**
   * 清除缓存
   */
  clearCache(): void {
    this.nodeDefineCache.clear();
  }

  /**
   * 清除特定节点的缓存
   */
  clearNodeCache(nodeType: NodeType, instanceId?: string): void {
    const cacheKey = this.getCacheKey({ nodeType, instanceId });
    this.nodeDefineCache.delete(cacheKey);
  }

  // ==================== 私有方法 ====================

  /**
   * 解析工作流响应
   */
  private parseWorkflowResponse(response: any): WorkflowData {
    console.log('API 响应原始数据:', response);
    
    // 优先使用草稿版本，如果草稿为空则使用正式版本
    let uiDesignStr = response.uiDesignDraft || response.uiDesign || '{}';
    let functionDesignData = response.functionDesignDraft || response.functionDesign;
    
    // 处理 uiDesign - 如果是字符串，直接使用；如果是对象，转为字符串
    if (typeof uiDesignStr === 'object') {
      uiDesignStr = JSON.stringify(uiDesignStr);
    }
    
    // 处理 functionDesign - 可能是数组或字符串
    let functionDesign: any;
    if (Array.isArray(functionDesignData)) {
      // 如果是数组，直接使用
      functionDesign = functionDesignData;
      console.log('Function Design 是数组格式');
    } else if (typeof functionDesignData === 'string') {
      // 如果是字符串，解析为对象
      try {
        functionDesign = JSON.parse(functionDesignData);
      } catch (error) {
        console.error('解析 Function Design 字符串失败:', error);
        functionDesign = [];
      }
    } else if (typeof functionDesignData === 'object' && functionDesignData !== null) {
      // 如果是对象，直接使用
      functionDesign = functionDesignData;
    } else {
      // 其他情况，使用空数组
      functionDesign = [];
    }
    
    console.log('使用的 UI Design:', uiDesignStr);
    console.log('使用的 Function Design:', functionDesign);
    
    let uiDesign: any;
    
    try {
      uiDesign = JSON.parse(uiDesignStr);
    } catch (error) {
      console.error('解析 UI Design 失败:', error);
      uiDesign = {};
    }

    return {
      id: response.id || '',
      appId: response.appId || '',
      name: response.name || '未命名工作流',
      description: response.description || '',
      uiDesign,
      functionDesign,
      isPublish: response.isPublish || false,
      isDraft: !!(response.uiDesignDraft || response.functionDesignDraft),
    };
  }

  /**
   * 转换为后端 NodeDesign 格式
   */
  private convertToNodeDesigns(nodes: BackendNodeData[]): NodeDesign[] {
    return nodes.map(node => ({
      nodeKey: node.id,
      nodeType: node.type,
      name: node.name,
      description: node.description,
      inputFields: node.config.inputFields as FieldDesign[],
      outputFields: node.config.outputFields as FieldDesign[],
      fieldDesigns: Object.entries(node.config.settings || {}).map(([key, value]) => ({
        key,
        value: value as FieldDesign,
      })),
      nextNodeKeys: [],
    }));
  }

  /**
   * 获取缓存键
   */
  private getCacheKey(item: { nodeType?: NodeType; instanceId?: string }): string {
    const { nodeType, instanceId } = item;
    return instanceId ? `${nodeType}:${instanceId}` : nodeType || '';
  }

  /**
   * 从节点定义项获取实例 ID
   */
  private getInstanceId(item: NodeDefineItem): string | undefined {
    if (item.pluginId) return item.pluginId.toString();
    if (item.wikiId) return item.wikiId.toString();
    if (item.modelId) return item.modelId.toString();
    return undefined;
  }

  /**
   * 检查节点定义是否匹配实例
   */
  private matchesInstance(item: NodeDefineItem, instanceId?: string): boolean {
    if (!instanceId) return true;
    const itemInstanceId = this.getInstanceId(item);
    return itemInstanceId === instanceId;
  }

  /**
   * 过滤未缓存的查询
   */
  private filterUncached(nodeQueries: Map<NodeType, string[]>): Map<NodeType, string[]> {
    const uncached = new Map<NodeType, string[]>();

    nodeQueries.forEach((ids, nodeType) => {
      const uncachedIds = ids.filter(id => {
        const cacheKey = this.getCacheKey({ nodeType, instanceId: id });
        return !this.nodeDefineCache.has(cacheKey);
      });

      if (uncachedIds.length > 0) {
        uncached.set(nodeType, uncachedIds);
      }
    });

    return uncached;
  }

  /**
   * 从缓存获取定义
   */
  private getCachedDefinitions(nodeQueries: Map<NodeType, string[]>): NodeDefineItem[] {
    const results: NodeDefineItem[] = [];

    nodeQueries.forEach((ids, nodeType) => {
      ids.forEach(id => {
        const cacheKey = this.getCacheKey({ nodeType, instanceId: id });
        const cached = this.nodeDefineCache.get(cacheKey);
        if (cached) {
          results.push(cached);
        }
      });
    });

    return results;
  }
}
