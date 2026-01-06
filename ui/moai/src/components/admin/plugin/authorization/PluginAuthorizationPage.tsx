/**
 * 插件授权管理页面
 */
import { useState, useEffect, useCallback } from "react";
import { Table, Button, message, Space, Modal, Transfer, Tag, Typography, Tabs } from "antd";
import { ReloadOutlined, SafetyOutlined, TeamOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type {
  PluginAuthorizationItem,
  TeamAuthorizationItem2,
  AuthorizedTeamItem2,
  AuthorizedPluginItem,
  QueryPluginAuthorizationsCommand,
  QueryTeamAuthorizationsCommand2,
  UpdatePluginAuthorizationsCommand,
  BatchAuthorizePluginsToTeamCommand,
  BatchRevokePluginsFromTeamCommand,
} from "../../../../apiClient/models";
import type { TransferProps } from "antd";
import "./PluginAuthorizationPage.css";

const { Text } = Typography;

interface TransferItem {
  key: string;
  title: string;
  description?: string;
}

export default function PluginAuthorizationPage() {
  const [activeTab, setActiveTab] = useState<string>("plugin");
  const [pluginAuthorizations, setPluginAuthorizations] = useState<PluginAuthorizationItem[]>([]);
  const [teamAuthorizations, setTeamAuthorizations] = useState<TeamAuthorizationItem2[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalVisible, setModalVisible] = useState(false);
  const [modalType, setModalType] = useState<"plugin" | "team">("plugin");
  const [currentItem, setCurrentItem] = useState<PluginAuthorizationItem | TeamAuthorizationItem2 | null>(null);
  const [transferData, setTransferData] = useState<TransferItem[]>([]);
  const [targetKeys, setTargetKeys] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取插件授权列表
  const fetchPluginAuthorizations = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryPluginAuthorizationsCommand = {};
      const response = await client.api.plugin.authorization.plugins.post(requestBody);
      setPluginAuthorizations(response?.plugins || []);
    } catch (error) {
      console.error("获取插件授权列表失败:", error);
      proxyRequestError(error, messageApi, "获取插件授权列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 获取团队授权列表（聚合团队信息）
  const fetchTeamAuthorizations = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 并行获取团队授权和所有团队信息
      const [authResponse, teamsResponse] = await Promise.all([
        client.api.plugin.authorization.teams.post({} as QueryTeamAuthorizationsCommand2),
        client.api.team.all.simple.post(),
      ]);
      
      const teams = teamsResponse?.items || [];
      
      // 创建团队 ID 到团队信息的映射
      const teamMap = new Map(teams.map(t => [t.id, t]));
      
      // 聚合数据：将团队所有人信息添加到授权数据中
      const enrichedAuthorizations = (authResponse?.teams || []).map(auth => {
        const teamInfo = teamMap.get(auth.teamId);
        return {
          ...auth,
          ownerName: teamInfo?.ownerName || "-",
        };
      });
      
      setTeamAuthorizations(enrichedAuthorizations as TeamAuthorizationItem2[]);
    } catch (error) {
      console.error("获取团队授权列表失败:", error);
      proxyRequestError(error, messageApi, "获取团队授权列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 刷新数据
  const refreshData = useCallback(() => {
    if (activeTab === "plugin") {
      fetchPluginAuthorizations();
    } else {
      fetchTeamAuthorizations();
    }
  }, [activeTab, fetchPluginAuthorizations, fetchTeamAuthorizations]);

  // 处理插件授权配置
  const handleConfigurePlugin = useCallback(async (plugin: PluginAuthorizationItem) => {
    setCurrentItem(plugin);
    setModalType("plugin");

    try {
      const client = GetApiClient();
      const requestBody: QueryTeamAuthorizationsCommand2 = {};
      const response = await client.api.plugin.authorization.teams.post(requestBody);

      if (response?.teams) {
        const allTeams: TransferItem[] = response.teams.map((team) => ({
          key: team.teamId?.toString() || "",
          title: team.teamName || "",
        }));
        const authorizedTeamIds = plugin.authorizedTeams?.map((t) => t.teamId?.toString() || "") || [];

        setTransferData(allTeams);
        setTargetKeys(authorizedTeamIds);
        setModalVisible(true);
      }
    } catch (error) {
      console.error("获取团队列表失败:", error);
      proxyRequestError(error, messageApi, "获取团队列表失败");
    }
  }, [messageApi]);

  // 处理团队授权配置
  const handleConfigureTeam = useCallback(async (team: TeamAuthorizationItem2) => {
    setCurrentItem(team);
    setModalType("team");

    try {
      const client = GetApiClient();
      const requestBody: QueryPluginAuthorizationsCommand = {};
      const response = await client.api.plugin.authorization.plugins.post(requestBody);

      if (response?.plugins) {
        const allPlugins: TransferItem[] = response.plugins.map((plugin) => ({
          key: plugin.pluginId?.toString() || "",
          title: plugin.title || plugin.pluginName || "",
          description: plugin.pluginName || "",
        }));
        const authorizedPluginIds = team.authorizedPlugins?.map((p) => p.pluginId?.toString() || "") || [];

        setTransferData(allPlugins);
        setTargetKeys(authorizedPluginIds);
        setModalVisible(true);
      }
    } catch (error) {
      console.error("获取插件列表失败:", error);
      proxyRequestError(error, messageApi, "获取插件列表失败");
    }
  }, [messageApi]);

  // 处理穿梭框变化
  const handleTransferChange: TransferProps["onChange"] = (newTargetKeys) => {
    setTargetKeys(newTargetKeys as string[]);
  };

  // 提交授权配置
  const handleSubmit = useCallback(async () => {
    if (!currentItem) return;

    setSubmitting(true);
    try {
      const client = GetApiClient();

      if (modalType === "plugin") {
        const plugin = currentItem as PluginAuthorizationItem;
        const requestBody: UpdatePluginAuthorizationsCommand = {
          pluginId: plugin.pluginId,
          teamIds: targetKeys.map((id) => parseInt(id)),
        };
        await client.api.plugin.authorization.plugin.update.post(requestBody);
        messageApi.success("插件授权配置成功");
      } else {
        const team = currentItem as TeamAuthorizationItem2;
        const oldPluginIds = team.authorizedPlugins?.map((p) => p.pluginId?.toString() || "") || [];
        const newPluginIds = targetKeys;

        const toAuthorize = newPluginIds.filter((id) => !oldPluginIds.includes(id));
        const toRevoke = oldPluginIds.filter((id) => !newPluginIds.includes(id));

        if (toAuthorize.length > 0) {
          const authorizeBody: BatchAuthorizePluginsToTeamCommand = {
            teamId: team.teamId,
            pluginIds: toAuthorize.map((id) => parseInt(id)),
          };
          await client.api.plugin.authorization.team.authorize.post(authorizeBody);
        }

        if (toRevoke.length > 0) {
          const revokeBody: BatchRevokePluginsFromTeamCommand = {
            teamId: team.teamId,
            pluginIds: toRevoke.map((id) => parseInt(id)),
          };
          await client.api.plugin.authorization.team.revoke.post(revokeBody);
        }

        messageApi.success("团队授权配置成功");
      }

      setModalVisible(false);
      refreshData();
    } catch (error) {
      console.error("授权配置失败:", error);
      proxyRequestError(error, messageApi, "授权配置失败");
    } finally {
      setSubmitting(false);
    }
  }, [currentItem, modalType, targetKeys, messageApi, refreshData]);

  useEffect(() => {
    refreshData();
  }, [activeTab]);

  // 插件授权表格列
  const pluginColumns = [
    {
      title: "插件名称",
      dataIndex: "pluginName",
      key: "pluginName",
      width: 180,
      render: (name: string) => <Text strong style={{ color: "var(--color-primary)" }}>{name}</Text>,
    },
    {
      title: "显示名称",
      dataIndex: "title",
      key: "title",
      width: 150,
      render: (title: string) => title || "-",
    },
    {
      title: "描述",
      dataIndex: "description",
      key: "description",
      ellipsis: true,
      render: (desc: string) => <Text type="secondary">{desc || "-"}</Text>,
    },
    {
      title: "公开",
      dataIndex: "isPublic",
      key: "isPublic",
      width: 80,
      render: (isPublic: boolean) => (
        <Tag color={isPublic ? "success" : "warning"}>{isPublic ? "公开" : "私有"}</Tag>
      ),
    },
    {
      title: "已授权团队",
      dataIndex: "authorizedTeams",
      key: "authorizedTeams",
      render: (teams: AuthorizedTeamItem2[]) => (
        <Space wrap size="small">
          {teams && teams.length > 0 ? (
            teams.map((team) => <Tag key={team.teamId} color="blue">{team.teamName}</Tag>)
          ) : (
            <Text type="secondary">未授权</Text>
          )}
        </Space>
      ),
    },
    {
      title: "操作",
      key: "action",
      width: 120,
      fixed: "right" as const,
      render: (_: unknown, record: PluginAuthorizationItem) => (
        <Button type="link" size="small" onClick={() => handleConfigurePlugin(record)}>配置授权</Button>
      ),
    },
  ];

  // 团队授权表格列
  const teamColumns = [
    {
      title: "团队名称",
      dataIndex: "teamName",
      key: "teamName",
      width: 200,
      render: (name: string) => <Text strong>{name}</Text>,
    },
    {
      title: "团队所有人",
      dataIndex: "ownerName",
      key: "ownerName",
      width: 150,
      render: (name: string) => <Text type="secondary">{name || "-"}</Text>,
    },
    {
      title: "已授权插件",
      dataIndex: "authorizedPlugins",
      key: "authorizedPlugins",
      render: (plugins: AuthorizedPluginItem[]) => (
        <Space wrap size="small">
          {plugins && plugins.length > 0 ? (
            plugins.map((plugin) => (
              <Tag key={plugin.pluginId} color="blue">
                {plugin.title || plugin.pluginName}
                {plugin.title && plugin.pluginName && plugin.title !== plugin.pluginName && (
                  <Text type="secondary" style={{ marginLeft: 4, fontSize: 12 }}>({plugin.pluginName})</Text>
                )}
              </Tag>
            ))
          ) : (
            <Text type="secondary">未授权</Text>
          )}
        </Space>
      ),
    },
    {
      title: "操作",
      key: "action",
      width: 120,
      fixed: "right" as const,
      render: (_: unknown, record: TeamAuthorizationItem2) => (
        <Button type="link" size="small" onClick={() => handleConfigureTeam(record)}>配置授权</Button>
      ),
    },
  ];

  const tabItems = [
    {
      key: "plugin",
      label: <span><SafetyOutlined /> 按插件授权</span>,
      children: (
        <Table
          dataSource={pluginAuthorizations}
          columns={pluginColumns}
          rowKey={(record) => record.pluginId?.toString() || ""}
          pagination={false}
          loading={loading}
          scroll={{ x: 900 }}
        />
      ),
    },
    {
      key: "team",
      label: <span><TeamOutlined /> 按团队授权</span>,
      children: (
        <Table
          dataSource={teamAuthorizations}
          columns={teamColumns}
          rowKey={(record) => record.teamId?.toString() || ""}
          pagination={false}
          loading={loading}
        />
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <div className="plugin-authorization-page">
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          className="authorization-tabs"
          tabBarExtraContent={
            <Button icon={<ReloadOutlined />} onClick={refreshData} loading={loading}>刷新</Button>
          }
        />

        <Modal
          title={modalType === "plugin" ? "配置插件授权" : "配置团队授权"}
          open={modalVisible}
          onCancel={() => setModalVisible(false)}
          onOk={handleSubmit}
          width={700}
          maskClosable={false}
          confirmLoading={submitting}
        >
          <div className="modal-description">
            <Text strong>
              {modalType === "plugin"
                ? `插件: ${(currentItem as PluginAuthorizationItem)?.title || (currentItem as PluginAuthorizationItem)?.pluginName}`
                : `团队: ${(currentItem as TeamAuthorizationItem2)?.teamName}`}
            </Text>
          </div>
          <Transfer
            dataSource={transferData}
            titles={[
              modalType === "plugin" ? "未授权团队" : "未授权插件",
              modalType === "plugin" ? "已授权团队" : "已授权插件",
            ]}
            targetKeys={targetKeys}
            onChange={handleTransferChange}
            render={(item) => item.title}
            listStyle={{ width: 300, height: 400 }}
            showSearch
            filterOption={(inputValue, item) =>
              item.title.toLowerCase().includes(inputValue.toLowerCase()) ||
              (item.description?.toLowerCase().includes(inputValue.toLowerCase()) ?? false)
            }
          />
        </Modal>
      </div>
    </>
  );
}
