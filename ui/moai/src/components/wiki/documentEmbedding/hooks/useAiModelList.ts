/**
 * AI模型列表管理Hook
 * 负责获取可用的AI模型列表
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";

/**
 * AI模型列表管理Hook
 * @returns 模型列表、加载状态、上下文持有者和刷新函数
 */
export function useAiModelList() {
  const [modelList, setModelList] = useState<Array<{ id: number; name: string }>>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchModelList = useCallback(async () => {
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.aimodel.modellist.post({
        aiModelType: "chat",
      });

      if (response?.aiModels) {
        const models = response.aiModels
          .filter((item: any) => item.id != null && item.name != null)
          .map((item: any) => ({
            id: item.id!,
            name: item.name || "",
          }));
        setModelList(models);
      }
    } catch (error) {
      console.error("Failed to fetch AI model list:", error);
      proxyRequestError(error, messageApi, "获取AI模型列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  return {
    modelList,
    loading,
    contextHolder,
    fetchModelList,
  };
}

