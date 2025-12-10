import React, { useEffect, useState, useCallback, useMemo } from "react";
import {
  Table,
  Button,
  message,
  Popconfirm,
  Space,
  Card,
  Typography,
  Row,
  Col,
  Spin,
  Tag,
  Image,
} from "antd";
import {
  DisconnectOutlined,
  PlusOutlined,
  LinkOutlined,
  ExclamationCircleOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import {
  QueryUserBindAccountCommandResponseItem,
  QueryAllOAuthPrividerCommandResponseItem,
  UnbindUserAccountCommand,
} from "../../apiClient/models";

const { Text } = Typography;

interface BindOAuthProps {
  onBindSuccess?: () => void;
}

export default function BindOAuth({ onBindSuccess }: BindOAuthProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unbindingId, setUnbindingId] = useState<number | null>(null);
  const [bindLoading, setBindLoading] = useState<string | null>(null);
  
  // 已绑定的OAuth账号列表
  const [boundAccounts, setBoundAccounts] = useState<QueryUserBindAccountCommandResponseItem[]>([]);
  
  // 可用的OAuth提供商列表
  const [availableProviders, setAvailableProviders] = useState<QueryAllOAuthPrividerCommandResponseItem[]>([]);

  // 获取已绑定的OAuth账号列表
  const fetchBoundAccounts = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.user.oauth.oauth_list.get();
      
      if (response && response.items) {
        setBoundAccounts(response.items);
      }
    } catch (error) {
      console.error("获取已绑定账号失败:", error);
      messageApi.error("获取已绑定账号失败");
    }
  }, [messageApi]);

  // 获取可用的OAuth提供商列表
  const fetchAvailableProviders = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.account.oauth_prividers.get();
      
      if (response && response.items) {
        setAvailableProviders(response.items);
      }
    } catch (error) {
      console.error("获取OAuth提供商失败:", error);
      messageApi.error("获取OAuth提供商失败");
    }
  }, [messageApi]);

  // 解绑OAuth账号
  const handleUnbind = useCallback(async (bindId: number, providerName: string) => {
    console.log("用户确认解绑，开始发送请求:", { bindId, providerName });
    setUnbindingId(bindId);
    try {
      const client = GetApiClient();
      const requestData = {
        bindId: bindId,
      } as UnbindUserAccountCommand;
      
      console.log("发送解绑请求:", requestData);
      await client.api.user.oauth.unbindOauth.post(requestData);
      
      console.log("解绑请求成功");
      messageApi.success("解绑成功");
      await fetchBoundAccounts(); // 重新获取列表
      onBindSuccess?.(); // 通知父组件
    } catch (error) {
      console.error("解绑失败:", error);
      messageApi.error("解绑失败，请重试");
    } finally {
      setUnbindingId(null);
    }
  }, [fetchBoundAccounts, messageApi, onBindSuccess]);

  // 新增绑定OAuth账号
  const handleAddBind = useCallback((provider: QueryAllOAuthPrividerCommandResponseItem) => {
    if (!provider.redirectUrl) {
      messageApi.error("该OAuth提供商配置不完整");
      return;
    }

    setBindLoading(provider.oAuthId || "");
    
    // 打开新窗口进行OAuth授权
    const popup = window.open(
      provider.redirectUrl,
      "oauth_bind",
      "width=600,height=700,scrollbars=yes,resizable=yes"
    );

    if (!popup) {
      messageApi.error("无法打开授权窗口，请检查浏览器弹窗设置");
      setBindLoading(null);
      return;
    }

    // 监听弹窗关闭事件
    const checkClosed = setInterval(() => {
      if (popup.closed) {
        clearInterval(checkClosed);
        setBindLoading(null);
        
        // 重新获取绑定列表
        fetchBoundAccounts();
        onBindSuccess?.();
      }
    }, 1000);
  }, [fetchBoundAccounts, messageApi, onBindSuccess]);

  // 检查提供商是否已被绑定
  const isProviderBound = useCallback((providerId: string | null | undefined) => {
    if (!providerId) return false;
    return boundAccounts.some(account => account.providerId?.toString() === providerId);
  }, [boundAccounts]);

  // 刷新数据
  const refreshData = useCallback(async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        fetchBoundAccounts(),
        fetchAvailableProviders(),
      ]);
    } catch (error) {
      console.error("刷新数据失败:", error);
      messageApi.error("刷新数据失败");
    } finally {
      setRefreshing(false);
    }
  }, [fetchBoundAccounts, fetchAvailableProviders, messageApi]);

  // 初始化数据
  useEffect(() => {
    const initData = async () => {
      setLoading(true);
      try {
        await Promise.all([
          fetchBoundAccounts(),
          fetchAvailableProviders(),
        ]);
      } catch (error) {
        console.error("初始化数据失败:", error);
        messageApi.error("初始化数据失败");
      } finally {
        setLoading(false);
      }
    };

    initData();
  }, [fetchBoundAccounts, fetchAvailableProviders, messageApi]);

  // 表格列定义
  const columns = useMemo(() => [
    {
      title: "提供商",
      dataIndex: "name",
      key: "name",
      render: (name: string, record: QueryUserBindAccountCommandResponseItem) => (
        <Space>
          {record.iconUrl && (
            <Image
              src={record.iconUrl}
              alt={name}
              width={24}
              height={24}
              preview={false}
              fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
            />
          )}
          <span>{name}</span>
        </Space>
      ),
    },
    {
      title: "绑定时间",
      dataIndex: "bindTime",
      key: "bindTime",
      render: () => <Text type="secondary">已绑定</Text>,
    },
    {
      title: "操作",
      key: "action",
      render: (_: unknown, record: QueryUserBindAccountCommandResponseItem) => (
        <Popconfirm
          title="确认解绑"
          description={`您确定要解绑 ${record.name} 账号吗？解绑后将无法使用该账号登录。`}
          onConfirm={() => handleUnbind(record.bindId!, record.name!)}
          okText="确认解绑"
          cancelText="取消"
          icon={<ExclamationCircleOutlined style={{ color: 'red' }} />}
        >
          <Button
            type="text"
            danger
            icon={<DisconnectOutlined />}
            loading={unbindingId === record.bindId}
          >
            解绑
          </Button>
        </Popconfirm>
      ),
    },
  ], [handleUnbind, unbindingId]);

  if (loading) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>
          <Text type="secondary">加载中...</Text>
        </div>
      </div>
    );
  }

  return (
    <div>
      {contextHolder}
      
      {/* 已绑定的OAuth账号 */}
      <Card 
        title="已绑定的第三方账号" 
        style={{ marginBottom: 24 }}
        extra={
          <Button
            icon={<ReloadOutlined />}
            onClick={refreshData}
            loading={refreshing}
            size="small"
          >
            刷新
          </Button>
        }
      >
        <Table
          columns={columns}
          dataSource={boundAccounts}
          rowKey="bindId"
          pagination={false}
          loading={refreshing}
          locale={{
            emptyText: "暂无绑定的第三方账号",
          }}
        />
      </Card>

      {/* 可绑定的OAuth提供商 */}
      <Card 
        title="可绑定的第三方账号"
        extra={
          <Button
            icon={<ReloadOutlined />}
            onClick={refreshData}
            loading={refreshing}
            size="small"
          >
            刷新
          </Button>
        }
      >
        {availableProviders.length > 0 ? (
          <Row gutter={[16, 16]}>
            {availableProviders.map((provider) => {
              const isBound = isProviderBound(provider.oAuthId);
              
              return (
                <Col xs={24} sm={12} md={8} lg={6} key={provider.oAuthId}>
                  <Card
                    size="small"
                    hoverable={!isBound}
                    style={{
                      textAlign: "center",
                      opacity: isBound ? 0.6 : 1,
                    }}
                  >
                    <Space direction="vertical" size="small">
                      {provider.iconUrl && (
                        <Image
                          src={provider.iconUrl}
                          alt={provider.name || ""}
                          width={48}
                          height={48}
                          preview={false}
                          fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
                        />
                      )}
                      <div>
                        <Text strong>{provider.name}</Text>
                      </div>
                      {isBound ? (
                        <Tag color="green" icon={<LinkOutlined />}>
                          已绑定
                        </Tag>
                      ) : (
                        <Button
                          type="primary"
                          size="small"
                          icon={<PlusOutlined />}
                          loading={bindLoading === provider.oAuthId}
                          onClick={() => handleAddBind(provider)}
                        >
                          绑定账号
                        </Button>
                      )}
                    </Space>
                  </Card>
                </Col>
              );
            })}
          </Row>
        ) : (
          <div style={{ textAlign: "center", padding: "40px" }}>
            <Text type="secondary">暂无可绑定的第三方账号</Text>
            <br />
            <Button 
              type="link" 
              onClick={refreshData}
              loading={refreshing}
              style={{ marginTop: 8 }}
            >
              点击刷新
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}