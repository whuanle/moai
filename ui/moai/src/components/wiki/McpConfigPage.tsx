import { useState, useEffect, useCallback } from "react";
import {
  Card,
  Button,
  message,
  Switch,
  Typography,
  Space,
  Descriptions,
  Popconfirm,
  Spin,
  Alert,
} from "antd";
import {
  KeyOutlined,
  CopyOutlined,
  ExclamationCircleOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams } from "react-router";
import { proxyRequestError } from "../../helper/RequestError";
import type { QueryWikiMcpConfigCommandResponse } from "../../apiClient/models";
import { formatRelativeTime } from "../../helper/DateTimeHelper";

const { Text, Paragraph, Title } = Typography;

export default function McpConfigPage() {
  const { id } = useParams();
  const wikiId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [config, setConfig] = useState<QueryWikiMcpConfigCommandResponse | null>(null);
  const [toggleLoading, setToggleLoading] = useState(false);
  const [resetLoading, setResetLoading] = useState(false);

  // 加载 MCP 配置
  const fetchConfig = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const response = await client.api.wiki.plugin.mcp.config.get({
        queryParameters: { wikiId },
      });
      setConfig(response || null);
    } catch (error) {
      console.log("获取 MCP 配置:", error);
      setConfig(null);
    } finally {
      setLoading(false);
    }
  }, [wikiId]);

  useEffect(() => {
    fetchConfig();
  }, [fetchConfig]);

  // 启用/禁用 MCP
  const handleToggle = async (enabled: boolean) => {
    try {
      setToggleLoading(true);
      const client = GetApiClient();

      if (enabled) {
        await client.api.wiki.plugin.mcp.enable.post({ wikiId });
        messageApi.success("已启用 MCP 功能");
      } else {
        await client.api.wiki.plugin.mcp.disable.post({ wikiId });
        messageApi.success("已禁用 MCP 功能");
      }

      fetchConfig();
    } catch (error) {
      console.error("操作失败:", error);
      proxyRequestError(error, messageApi, "操作失败");
    } finally {
      setToggleLoading(false);
    }
  };

  // 重置密钥
  const handleResetKey = async () => {
    try {
      setResetLoading(true);
      const client = GetApiClient();
      await client.api.wiki.plugin.mcp.reset_key.post({ wikiId });
      messageApi.success("密钥已重置");
      fetchConfig();
    } catch (error) {
      console.error("重置密钥失败:", error);
      proxyRequestError(error, messageApi, "重置密钥失败");
    } finally {
      setResetLoading(false);
    }
  };

  // 复制到剪贴板
  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text).then(() => {
      messageApi.success(`${label}已复制到剪贴板`);
    }).catch(() => {
      messageApi.error("复制失败");
    });
  };

  if (loading) {
    return (
      <Card>
        <div style={{ textAlign: "center", padding: "50px" }}>
          <Spin size="large" />
        </div>
      </Card>
    );
  }

  return (
    <>
      {contextHolder}
      <Card title="MCP 配置">
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <Alert
            message="MCP (Model Context Protocol) 配置"
            description="启用 MCP 后，可以通过 MCP 协议访问此知识库，支持与各种 AI 工具集成。"
            type="info"
            showIcon
          />

          <Descriptions column={1} bordered>
            <Descriptions.Item label="状态">
              <Space>
                <Switch
                  checked={config?.enabled || false}
                  onChange={handleToggle}
                  loading={toggleLoading}
                  checkedChildren={<CheckCircleOutlined />}
                  unCheckedChildren={<CloseCircleOutlined />}
                />
                <Text type={config?.enabled ? "success" : "secondary"}>
                  {config?.enabled ? "已启用" : "未启用"}
                </Text>
              </Space>
            </Descriptions.Item>

            {config?.enabled && (
              <>
                <Descriptions.Item label="MCP 服务地址">
                  <Space>
                    <Text code copyable={{ text: config?.mcpUrl || "" }}>
                      {config?.mcpUrl || "-"}
                    </Text>
                  </Space>
                </Descriptions.Item>

                <Descriptions.Item label="访问密钥">
                  <Space direction="vertical" style={{ width: "100%" }}>
                    <Space>
                      <Text code>
                        {config?.key
                          ? `${config.key.substring(0, 8)}...${config.key.substring(config.key.length - 4)}`
                          : "-"}
                      </Text>
                      <Button
                        type="link"
                        size="small"
                        icon={<CopyOutlined />}
                        onClick={() => handleCopy(config?.key || "", "密钥")}
                      >
                        复制完整密钥
                      </Button>
                    </Space>
                    <Popconfirm
                      title="确认重置密钥"
                      description="重置后原密钥将失效，确定要重置吗？"
                      icon={<ExclamationCircleOutlined style={{ color: "red" }} />}
                      onConfirm={handleResetKey}
                      okText="确定"
                      cancelText="取消"
                    >
                      <Button
                        type="default"
                        danger
                        size="small"
                        icon={<KeyOutlined />}
                        loading={resetLoading}
                      >
                        重置密钥
                      </Button>
                    </Popconfirm>
                  </Space>
                </Descriptions.Item>

                <Descriptions.Item label="创建时间">
                  <Text>{formatRelativeTime(config?.createTime)}</Text>
                </Descriptions.Item>
              </>
            )}
          </Descriptions>

          {config?.enabled && (
            <Card type="inner" title="使用说明" size="small">
              <Space direction="vertical" size="small">
                <Text>1. 复制上方的 MCP 服务地址和访问密钥</Text>
                <Text>2. 在支持 MCP 协议的 AI 工具中配置此服务</Text>
                <Text>3. 配置示例（以 Claude Desktop 为例）：</Text>
                <Paragraph>
                  <pre style={{ background: "#f5f5f5", padding: "12px", borderRadius: "4px", overflow: "auto" }}>
                    {`{
  "mcpServers": {
    "wiki-${wikiId}": {
      "url": "${config?.mcpUrl || ""}"
    }
  }
}`}
                  </pre>
                </Paragraph>
              </Space>
            </Card>
          )}
        </Space>
      </Card>
    </>
  );
}
