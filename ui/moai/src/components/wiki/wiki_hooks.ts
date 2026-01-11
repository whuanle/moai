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
 * @param wikiId 知识库ID，用于获取该知识库可用的模型列表
 */
export function useAiModelList(wikiId: number, aiModelType: AiModelType) {
  const [modelList, setModelList] = useState<AiModelItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchModelList = useCallback(async () => {
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.team_modellist.post({
        aiModelType: aiModelType,
        wikiId: wikiId,
      });

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
  }, [messageApi, wikiId]);

  return {
    modelList,
    loading,
    contextHolder,
    fetchModelList,
  };
}
