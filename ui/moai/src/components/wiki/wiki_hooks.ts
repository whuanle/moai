/**
 * Wiki 模块共享 hooks
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import { AiModelType } from "@/apiClient/models";

export interface AiModelItem {
  id: number;
  name: string;
}

/**
 * 获取 AI 模型列表
 * @param teamId 团队ID，如果为 0 或 undefined 则获取个人可用的模型列表
 * @param aiModelType 模型类型
 */
export function useAiModelList(teamId?: number, aiModelType?: AiModelType) {
  const [modelList, setModelList] = useState<AiModelItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchModelList = useCallback(async () => {
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      
      let response;
      if (teamId && teamId > 0) {
        // 团队知识库：获取团队可用的模型列表
        response = await apiClient.api.team.common.team_modellist.post({
          aiModelType: aiModelType,
          teamId: teamId,
        });
      } else {
        // 个人知识库：获取个人可用的模型列表
        response = await apiClient.api.aimodel.modellist.post({
          aiModelType: aiModelType,
        });
      }

      if (response?.aiModels && Array.isArray(response.aiModels)) {
        const models = response.aiModels
          .filter((item: any) => item && typeof item.id === "number" && item.name)
          .map((item: any) => ({
            id: Number(item.id),
            name: String(item.name || ""),
          }));
        setModelList(models);
      } else {
        setModelList([]);
      }
    } catch (error) {
      console.error("Failed to fetch AI model list:", error);
      proxyRequestError(error, messageApi, "获取AI模型列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi, teamId, aiModelType]);

  return {
    modelList,
    loading,
    contextHolder,
    fetchModelList,
  };
}
