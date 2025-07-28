import { useState, useEffect, useCallback } from "react";
import { useParams } from "react-router";
import {
  Card,
  Tabs,
  Table,
  Tag,
  Space,
  Typography,
  Switch,
  Button,
  message,
  Spin,
  Empty,
  Tooltip,
  Progress,
  Popconfirm,
} from "antd";
import {
  ReloadOutlined,
  PlayCircleOutlined,
  PauseCircleOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
  StopOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import {
  QueryWikiWebConfigTaskStateListCommandResponse,
  WikiWebConfigCrawleStateItem,
  WikiConfigCrawleTaskItem,
  CrawleState,
  CancalCrawleTaskCommand,
} from "../../../apiClient/models";
import { formatDateTime } from "../../../helper/DateTimeHelper";
import { proxyRequestError } from "../../../helper/RequestError";

const { Title, Text } = Typography;

// 爬虫状态映射
const getCrawleStateConfig = (state: CrawleState | null | undefined) => {
  switch (state) {
    case "none":
      return {
        color: "default",
        text: "未开始",
        icon: <ClockCircleOutlined />,
      };
    case "wait":
      return {
        color: "processing",
        text: "等待中",
        icon: <ClockCircleOutlined />,
      };
    case "processing":
      return {
        color: "processing",
        text: "处理中",
        icon: <PlayCircleOutlined />,
      };
    case "cancal":
      return {
        color: "warning",
        text: "已取消",
        icon: <PauseCircleOutlined />,
      };
    case "successful":
      return { color: "success", text: "成功", icon: <CheckCircleOutlined /> };
    case "failed":
      return { color: "error", text: "失败", icon: <CloseCircleOutlined /> };
    default:
      return {
        color: "default",
        text: "未知",
        icon: <ExclamationCircleOutlined />,
      };
  }
};

export default function WikiCrawleTask() {
  const { id: wikiId, crawleId: wikiWebConfigId } = useParams();
  const [activeTab, setActiveTab] = useState("tasks");
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [loading, setLoading] = useState(false);
  const [data, setData] =
    useState<QueryWikiWebConfigTaskStateListCommandResponse | null>(null);
  const [messageApi, contextHolder] = message.useMessage();
  const apiClient = GetApiClient();

  // 初始加载数据
  useEffect(() => {
    fetchCrawleStateData();
  }, []);

  // 获取爬虫状态数据
  const fetchCrawleStateData = useCallback(async () => {
    if (!wikiId || !wikiWebConfigId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response =
        await apiClient.api.wiki.web.query_web_document_state_list.get({
          queryParameters: {
            wikiId: parseInt(wikiId),
            wikiWebConfigId: parseInt(wikiWebConfigId),
          },
        });

      if (response) {
        setData(response);
      }
    } catch (error) {
      console.error("Failed to fetch crawle state data:", error);
      messageApi.error("获取爬虫状态数据失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, wikiWebConfigId, apiClient, messageApi]);

  // 自动刷新
  useEffect(() => {
    if (autoRefresh) {
      fetchCrawleStateData();
      const interval = setInterval(fetchCrawleStateData, 1000);
      return () => clearInterval(interval);
    }
  }, [autoRefresh]);

  // 手动刷新
  const handleManualRefresh = () => {
    fetchCrawleStateData();
  };

  // 取消任务
  const handleCancelTask = async (taskId: string) => {
    try {
      const cancelCommand: CancalCrawleTaskCommand = {
        taskId: taskId,
        wikiId: parseInt(wikiId || "0"),
        wikiWebConfigId: parseInt(wikiWebConfigId || "0"),
      };

      await apiClient.api.wiki.web.cancel_crawle_task.post(cancelCommand);
      messageApi.success("任务取消成功");
      // 重新获取数据
      await fetchCrawleStateData();
    } catch (error) {
      console.error("Failed to cancel task:", error);
      proxyRequestError(error, messageApi, "取消任务失败");
    }
  };

  // 页面状态表格列定义
  const pageStatesColumns = [
    {
      title: "爬取地址",
      dataIndex: "url",
      key: "url",
      width: "auto",
      render: (url: string) => (
        <div style={{ wordBreak: "break-all", whiteSpace: "pre-wrap" }}>
          {url}
        </div>
      ),
    },
    {
      title: "状态",
      dataIndex: "state",
      key: "state",
      width: 100,
      render: (state: CrawleState) => {
        const config = getCrawleStateConfig(state);
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      },
    },
    {
      title: "信息",
      dataIndex: "message",
      key: "message",
      width: 200,
      render: (message: string) => (
        <Tooltip title={message}>
          <Text ellipsis style={{ maxWidth: 200 }}>
            {message}
          </Text>
        </Tooltip>
      ),
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
      width: 150,
      render: (time: string) => formatDateTime(time),
    },
  ];

  // 任务表格列定义
  const tasksColumns = [
    {
      title: "任务ID",
      dataIndex: "id",
      key: "id",
      width: 300,
      render: (id: string) => (
        <Text code style={{ fontSize: "12px" }}>
          {id}
        </Text>
      ),
    },
    {
      title: "状态",
      dataIndex: "crawleState",
      key: "crawleState",
      render: (state: CrawleState) => {
        const config = getCrawleStateConfig(state);
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      },
    },
    {
      title: "页面统计",
      key: "pageStats",
      render: (record: WikiConfigCrawleTaskItem) => (
        <Space direction="vertical" size="small">
          <Text type="secondary" style={{ fontSize: "12px" }}>
            成功: {record.pageCount || 0} | 失败: {record.faildPageCount || 0}
          </Text>
          {(record.pageCount || 0) + (record.faildPageCount || 0) > 0 && (
            <Progress
              percent={Math.round(
                ((record.pageCount || 0) /
                  ((record.pageCount || 0) + (record.faildPageCount || 0))) *
                  100
              )}
              size="small"
              showInfo={false}
              strokeColor="#52c41a"
            />
          )}
        </Space>
      ),
    },
    {
      title: "配置信息",
      key: "config",
      render: (record: WikiConfigCrawleTaskItem) => (
        <Space direction="vertical" size="small">
          <Text type="secondary" style={{ fontSize: "12px" }}>
            最大Token: {record.maxTokensPerParagraph || 0}
          </Text>
          <Text type="secondary" style={{ fontSize: "12px" }}>
            重叠Token: {record.overlappingTokens || 0}
          </Text>
          <Text type="secondary" style={{ fontSize: "12px" }}>
            分词器: {record.tokenizer || "默认"}
          </Text>
        </Space>
      ),
    },
    {
      title: "消息",
      dataIndex: "message",
      key: "message",
      render: (message: string) =>
        message ? (
          <Tooltip title={message}>
            <Text ellipsis style={{ maxWidth: 200 }}>
              {message}
            </Text>
          </Tooltip>
        ) : (
          <Text type="secondary">-</Text>
        ),
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
      render: (time: string) => formatDateTime(time),
    },
    {
      title: "操作",
      key: "actions",
      render: (record: WikiConfigCrawleTaskItem) => {
        // 只有处理中的任务才显示取消按钮
        if (record.crawleState === "processing") {
          return (
            <Popconfirm
              title="确定要取消这个任务吗？"
              description="取消后任务将停止执行，已处理的数据将保留。"
              onConfirm={() => handleCancelTask(record.id || "")}
              okText="确定"
              cancelText="取消"
            >
              <Button
                type="primary"
                danger
                size="small"
                icon={<StopOutlined />}
              >
                取消任务
              </Button>
            </Popconfirm>
          );
        }
        return null;
      },
    },
  ];

  return (
    <div style={{ padding: "24px 0" }}>
      {contextHolder}

      <Card>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "16px",
          }}
        >
          <Title level={4} style={{ margin: 0 }}>
            爬虫任务状态
          </Title>
          <Space>
            <Space>
              <Text>自动刷新:</Text>
              <Switch
                checked={autoRefresh}
                onChange={setAutoRefresh}
                checkedChildren="开启"
                unCheckedChildren="关闭"
              />
            </Space>
            <Button
              icon={<ReloadOutlined />}
              onClick={handleManualRefresh}
              loading={loading}
            >
              刷新
            </Button>
          </Space>
        </div>

        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: "tasks",
              label: (
                <Space>
                  <span>任务列表</span>
                  {data?.tasks && data.tasks.length > 0 && (
                    <Tag color="green">{data.tasks.length}</Tag>
                  )}
                </Space>
              ),
              children: (
                <div style={{ marginTop: "16px" }}>
                  {loading ? (
                    <div style={{ textAlign: "center", padding: "40px" }}>
                      <Spin size="large" />
                    </div>
                  ) : data?.tasks && data.tasks.length > 0 ? (
                    <Table
                      columns={tasksColumns}
                      dataSource={data.tasks}
                      rowKey="id"
                      pagination={false}
                      size="middle"
                    />
                  ) : (
                    <Empty description="暂无任务数据" />
                  )}
                </div>
              ),
            },
            {
              key: "pageStates",
              label: (
                <Space>
                  <span>页面状态</span>
                  {data?.pageStates && data.pageStates.length > 0 && (
                    <Tag color="blue">{data.pageStates.length}</Tag>
                  )}
                </Space>
              ),
              children: (
                <div style={{ marginTop: "16px" }}>
                  {loading ? (
                    <div style={{ textAlign: "center", padding: "40px" }}>
                      <Spin size="large" />
                    </div>
                  ) : data?.pageStates && data.pageStates.length > 0 ? (
                    <Table
                      columns={pageStatesColumns}
                      dataSource={data.pageStates}
                      rowKey="id"
                      pagination={false}
                      size="middle"
                    />
                  ) : (
                    <Empty description="暂无页面状态数据" />
                  )}
                </div>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}
