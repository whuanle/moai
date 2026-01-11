/**
 * 任务列表模块
 * 负责展示和管理文档处理任务
 */

import { useState, useEffect, useCallback, useMemo } from "react";
import { Button, message, Table, Space, Empty, Collapse } from "antd";
import { ClockCircleOutlined, ReloadOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import { formatDateTime } from "../../../helper/DateTimeHelper";
import { TaskStatusTag } from "./components";
import type { TaskInfo } from "./types";
import "./styles.css";

const { Panel } = Collapse;

interface TaskListProps {
  wikiId: string;
  documentId: string;
}

/**
 * 任务列表组件
 */
export default function TaskList({ wikiId, documentId }: TaskListProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [loading, setLoading] = useState(false);

  /**
   * 获取任务列表
   */
  const fetchTasks = useCallback(async () => {
    if (!wikiId || !documentId) return;

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.task_list.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response && Array.isArray(response)) {
        setTasks(
          response
            .filter((task: any) => task && task.id)
            .map((task: any) => ({
              id: String(task.id || ""),
              fileName: String(task.fileName || ""),
              tokenizer: String(task.tokenizer || ""),
              maxTokensPerParagraph: typeof task.maxTokensPerParagraph === "number" ? task.maxTokensPerParagraph : 0,
              overlappingTokens: typeof task.overlappingTokens === "number" ? task.overlappingTokens : 0,
              state: String(task.state || ""),
              message: String(task.message || ""),
              createTime: String(task.createTime || ""),
              documentId: String(task.documentId || ""),
            }))
        );
      } else {
        setTasks([]);
      }
    } catch (error) {
      console.error("Failed to fetch tasks:", error);
      proxyRequestError(error, messageApi, "获取任务列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  /**
   * 取消任务
   */
  const cancelTask = useCallback(
    async (taskId: string) => {
      if (!taskId?.trim()) {
        messageApi.warning("任务ID不能为空");
        return;
      }
      try {
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.cancal_embedding.post({
          taskId,
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
        });

        messageApi.success("任务已取消");
        fetchTasks();
      } catch (error) {
        console.error("Failed to cancel task:", error);
        proxyRequestError(error, messageApi, "取消任务失败");
      }
    },
    [wikiId, documentId, messageApi, fetchTasks]
  );

  /**
   * 判断任务是否可以取消
   */
  const canCancelTask = useCallback((state: string) => {
    if (!state) return false;
    const lowerState = state.toLowerCase();
    return lowerState === "none" || lowerState === "wait" || lowerState === "processing";
  }, []);

  const taskColumns = useMemo(
    () => [
      { title: "任务ID", dataIndex: "id", key: "id", width: 220, ellipsis: true },
      { title: "文件名", dataIndex: "fileName", key: "fileName", width: 200, ellipsis: true },
      {
        title: "状态",
        dataIndex: "state",
        key: "state",
        width: 120,
        render: (state: string) => <TaskStatusTag state={state} />,
      },
      { title: "执行信息", dataIndex: "message", key: "message", width: 200, ellipsis: true },
      {
        title: "创建时间",
        dataIndex: "createTime",
        key: "createTime",
        width: 180,
        render: (text: string) => formatDateTime(text),
      },
      {
        title: "操作",
        key: "action",
        width: 120,
        fixed: "right" as const,
        render: (_: any, record: TaskInfo) => (
          <Space>
            {canCancelTask(record.state) && (
              <Button type="link" danger size="small" onClick={() => cancelTask(record.id)}>
                取消
              </Button>
            )}
          </Space>
        ),
      },
    ],
    [canCancelTask, cancelTask]
  );

  if (!wikiId || !documentId) {
    return <Empty description="缺少必要的参数" />;
  }

  return (
    <>
      {contextHolder}
      <Collapse defaultActiveKey={[]} className="doc-embed-collapse">
        <Panel
          header={
            <Space className="task-list-header">
              <ClockCircleOutlined />
              <span>任务列表</span>
              <Button
                type="text"
                icon={<ReloadOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  fetchTasks();
                }}
                loading={loading}
                size="small"
              />
            </Space>
          }
          key="tasks"
        >
          <Table
            columns={taskColumns}
            dataSource={tasks}
            rowKey="id"
            loading={loading}
            scroll={{ x: 1200 }}
            pagination={false}
            locale={{ emptyText: <Empty description="暂无任务" /> }}
          />
        </Panel>
      </Collapse>
    </>
  );
}
