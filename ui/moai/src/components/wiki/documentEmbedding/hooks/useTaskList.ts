/**
 * 任务列表管理Hook
 * 负责获取、管理和取消文档处理任务
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { TaskInfo } from "../types";

/**
 * 任务列表管理Hook
 * @param wikiId Wiki ID
 * @param documentId 文档ID
 * @returns 任务列表、加载状态、上下文持有者、刷新函数和取消任务函数
 */
export function useTaskList(wikiId: string, documentId: string) {
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchTasks = useCallback(async () => {
    if (!wikiId || !documentId) {
      return;
    }

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.task_list.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response) {
        const formattedTasks: TaskInfo[] = response.map((task: any) => ({
          id: task.id || "",
          fileName: task.fileName || "",
          tokenizer: task.tokenizer || "",
          maxTokensPerParagraph: task.maxTokensPerParagraph || 0,
          overlappingTokens: task.overlappingTokens || 0,
          state: task.state || "",
          message: task.message || "",
          createTime: task.createTime || "",
          documentId: task.documentId || "",
        }));
        setTasks(formattedTasks);
      }
    } catch (error) {
      console.error("Failed to fetch tasks:", error);
      proxyRequestError(error, messageApi, "获取任务列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  const cancelTask = useCallback(
    async (taskId: string) => {
      if (!wikiId || !documentId) {
        return;
      }

      try {
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.cancal_embedding.post({
          taskId: taskId,
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
        });

        messageApi.success("任务已取消");
        await fetchTasks();
      } catch (error) {
        console.error("Failed to cancel task:", error);
        proxyRequestError(error, messageApi, "取消任务失败");
      }
    },
    [wikiId, documentId, messageApi, fetchTasks]
  );

  return { tasks, loading, contextHolder, fetchTasks, cancelTask };
}

