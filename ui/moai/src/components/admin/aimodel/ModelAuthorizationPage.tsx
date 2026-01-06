import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router";
import {
  Card,
  Table,
  Button,
  message,
  Space,
  Modal,
  Transfer,
  Spin,
  Tag,
  Typography,
  Tabs,
} from "antd";
import {
  ReloadOutlined,
  SafetyOutlined,
  TeamOutlined,
  ArrowLeftOutlined,
} from "@ant-design/icons";
import "./ModelAuthorizationPage.css";
import { GetApiClient } from "../../ServiceClient";
import {
  ModelAuthorizationItem,
  TeamAuthorizationItem,
  AuthorizedTeamItem,
  AuthorizedModelItem,
  QueryModelAuthorizationsCommand,
  QueryTeamAuthorizationsCommand,
  UpdateModelAuthorizationsCommand,
  BatchAuthorizeModelsToTeamCommand,
  BatchRevokeModelsFromTeamCommand,
  QueryAllTeamSimpleListCommandResponseItem,
} from "../../../apiClient/models";
import type { TransferProps } from "antd";
import { proxyRequestError } from "../../../helper/RequestError";

const { Title, Text } = Typography;

interface TransferItem {
  key: string;
  title: string;
  description?: string;
}

export default function ModelAuthorizationPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<string>("model");
  const [modelAuthorizations, setModelAuthorizations] = useState<ModelAuthorizationItem[]>([]);
  const [teamAuthorizations, setTeamAuthorizations] = useState<TeamAuthorizationItem[]>([]);
  const [allTeams, setAllTeams] = useState<QueryAllTeamSimpleListCommandResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalVisible, setModalVisible] = useState(false);
  const [modalType, setModalType] = useState<"model" | "team">("model");
  const [currentItem, setCurrentItem] = useState<ModelAuthorizationItem | TeamAuthorizationItem | null>(null);
  const [transferData, setTransferData] = useState<TransferItem[]>([]);
  const [targetKeys, setTargetKeys] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取模型授权列表
  const fetchModelAuthorizations = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryModelAuthorizationsCommand = {};
      const response = await client.api.aimodel.authorization.models.post(requestBody);

      if (response?.models) {
        setModelAuthorizations(response.models);
      }
    } catch (error) {
      console.error("获取模型授权列表失败:", error);
      proxyRequestError(error, messageApi, "获取模型授权列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 获取团队授权列表（聚合团队信息）
  const fetchTeamAuthorizations = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 并行获取团队授权和所有团队信息
      const [authResponse, teamsResponse] = await Promise.all([
        client.api.aimodel.authorization.teams.post({} as QueryTeamAuthorizationsCommand),
        client.api.team.all.simple.post(),
      ]);

      const teams = teamsResponse?.items || [];
      setAllTeams(teams);

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

      setTeamAuthorizations(enrichedAuthorizations as TeamAuthorizationItem[]);
    } catch (error) {
      console.error("获取团队授权列表失败:", error);
      proxyRequestError(error, messageApi, "获取团队授权列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 刷新数据
  const refreshData = () => {
    if (activeTab === "model") {
      fetchModelAuthorizations();
    } else {
      fetchTeamAuthorizations();
    }
  };

  // 处理模型授权配置
  const handleConfigureModel = async (model: ModelAuthorizationItem) => {
    setCurrentItem(model);
    setModalType("model");

    // 获取所有团队列表
    try {
      const client = GetApiClient();
      const requestBody: QueryTeamAuthorizationsCommand = {};
      const response = await client.api.aimodel.authorization.teams.post(requestBody);

      if (response?.teams) {
        // 构建穿梭框数据
        const allTeams: TransferItem[] = response.teams.map((team) => ({
          key: team.teamId?.toString() || "",
          title: team.teamName || "",
        }));

        // 已授权的团队ID
        const authorizedTeamIds = model.authorizedTeams?.map((t) => t.teamId?.toString() || "") || [];

        setTransferData(allTeams);
        setTargetKeys(authorizedTeamIds);
        setModalVisible(true);
      }
    } catch (error) {
      console.error("获取团队列表失败:", error);
      proxyRequestError(error, messageApi, "获取团队列表失败");
    }
  };

  // 处理团队授权配置
  const handleConfigureTeam = async (team: TeamAuthorizationItem) => {
    setCurrentItem(team);
    setModalType("team");

    // 获取所有模型列表
    try {
      const client = GetApiClient();
      const requestBody: QueryModelAuthorizationsCommand = {};
      const response = await client.api.aimodel.authorization.models.post(requestBody);

      if (response?.models) {
        // 构建穿梭框数据 - 显示所有模型
        const allModels: TransferItem[] = response.models.map((model) => ({
          key: model.modelId?.toString() || "",
          title: model.title || model.name || "",
          description: model.name || "",
        }));

        // 已授权的模型ID
        const authorizedModelIds = team.authorizedModels?.map((m) => m.modelId?.toString() || "") || [];

        setTransferData(allModels);
        setTargetKeys(authorizedModelIds);
        setModalVisible(true);
      }
    } catch (error) {
      console.error("获取模型列表失败:", error);
      proxyRequestError(error, messageApi, "获取模型列表失败");
    }
  };

  // 处理穿梭框变化
  const handleTransferChange: TransferProps["onChange"] = (newTargetKeys) => {
    setTargetKeys(newTargetKeys as string[]);
  };

  // 提交授权配置
  const handleSubmit = async () => {
    if (!currentItem) return;

    setSubmitting(true);
    try {
      const client = GetApiClient();

      if (modalType === "model") {
        // 更新模型授权
        const model = currentItem as ModelAuthorizationItem;
        const requestBody: UpdateModelAuthorizationsCommand = {
          modelId: model.modelId,
          teamIds: targetKeys.map((id) => parseInt(id)),
        };

        await client.api.aimodel.authorization.model.update.post(requestBody);
        messageApi.success("模型授权配置成功");
      } else {
        // 更新团队授权 - 需要计算增量变化
        const team = currentItem as TeamAuthorizationItem;
        const oldModelIds = team.authorizedModels?.map((m) => m.modelId?.toString() || "") || [];
        const newModelIds = targetKeys;

        // 找出需要授权的模型（新增的）
        const toAuthorize = newModelIds.filter((id) => !oldModelIds.includes(id));
        // 找出需要撤销的模型（删除的）
        const toRevoke = oldModelIds.filter((id) => !newModelIds.includes(id));

        // 执行授权操作
        if (toAuthorize.length > 0) {
          const authorizeBody: BatchAuthorizeModelsToTeamCommand = {
            teamId: team.teamId,
            modelIds: toAuthorize.map((id) => parseInt(id)),
          };
          await client.api.aimodel.authorization.team.authorize.post(authorizeBody);
        }

        // 执行撤销操作
        if (toRevoke.length > 0) {
          const revokeBody: BatchRevokeModelsFromTeamCommand = {
            teamId: team.teamId,
            modelIds: toRevoke.map((id) => parseInt(id)),
          };
          await client.api.aimodel.authorization.team.revoke.post(revokeBody);
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
  };

  // 获取模型类型的中文名称
  const getModelTypeName = (type: string | null | undefined): string => {
    const names: Record<string, string> = {
      chat: "聊天",
      embedding: "嵌入",
      image: "图像",
      tts: "语音合成",
      stts: "语音转文字",
      text2video: "文本转视频",
      text2music: "文本转音乐",
    };
    return names[type || ""] || type || "-";
  };

  // 获取服务商显示名称
  const getProviderDisplayName = (provider: string | null | undefined): string => {
    const names: Record<string, string> = {
      openai: "OpenAI",
      anthropic: "Anthropic",
      azure: "Azure",
      google: "Google",
      huggingface: "HuggingFace",
      ollama: "Ollama",
      custom: "自定义",
    };
    return names[provider || ""] || provider || "-";
  };

  // 模型授权表格列
  const modelColumns = [
    {
      title: "模型名称",
      dataIndex: "name",
      key: "name",
      width: 180,
    },
    {
      title: "显示名称",
      dataIndex: "title",
      key: "title",
      width: 150,
    },
    {
      title: "模型类型",
      dataIndex: "aiModelType",
      key: "aiModelType",
      width: 120,
      render: (type: string | null | undefined) => (
        <Tag color="blue">{getModelTypeName(type)}</Tag>
      ),
    },
    {
      title: "服务商",
      dataIndex: "aiProvider",
      key: "aiProvider",
      width: 120,
      render: (provider: string | null | undefined) => getProviderDisplayName(provider),
    },
    {
      title: "已授权团队",
      dataIndex: "authorizedTeams",
      key: "authorizedTeams",
      render: (teams: AuthorizedTeamItem[]) => (
        <Space wrap size="small">
          {teams && teams.length > 0 ? (
            teams.map((team) => (
              <Tag key={team.teamId} color="blue">
                {team.teamName}
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
      render: (_: any, record: ModelAuthorizationItem) => (
        <Button
          type="link"
          size="small"
          onClick={() => handleConfigureModel(record)}
        >
          配置授权
        </Button>
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
    },
    {
      title: "团队所有人",
      dataIndex: "ownerName",
      key: "ownerName",
      width: 150,
      render: (name: string) => <Text type="secondary">{name || "-"}</Text>,
    },
    {
      title: "已授权模型",
      dataIndex: "authorizedModels",
      key: "authorizedModels",
      render: (models: AuthorizedModelItem[]) => (
        <Space wrap size="small">
          {models && models.length > 0 ? (
            models.map((model) => (
              <Tag key={model.modelId} color="blue">
                {model.name}
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
      render: (_: any, record: TeamAuthorizationItem) => (
        <Button
          type="link"
          size="small"
          onClick={() => handleConfigureTeam(record)}
        >
          配置授权
        </Button>
      ),
    },
  ];

  useEffect(() => {
    refreshData();
  }, [activeTab]);

  const tabItems = [
    {
      key: "model",
      label: (
        <span>
          <SafetyOutlined /> 按模型授权
        </span>
      ),
      children: (
        <div className="authorization-table">
          <Table
            dataSource={modelAuthorizations}
            columns={modelColumns}
            rowKey={(record) => record.modelId?.toString() || ""}
            pagination={false}
            loading={loading}
          />
        </div>
      ),
    },
    {
      key: "team",
      label: (
        <span>
          <TeamOutlined /> 按团队授权
        </span>
      ),
      children: (
        <div className="authorization-table">
          <Table
            dataSource={teamAuthorizations}
            columns={teamColumns}
            rowKey={(record) => record.teamId?.toString() || ""}
            pagination={false}
            loading={loading}
          />
        </div>
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <div className="model-authorization-page">
        <div className="authorization-tabs-container">
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={tabItems}
            className="authorization-tabs"
            tabBarExtraContent={
              <Space size="middle">
                <Button
                  icon={<ReloadOutlined />}
                  onClick={refreshData}
                  loading={loading}
                >
                  刷新
                </Button>
                <Button
                  icon={<ArrowLeftOutlined />}
                  onClick={() => navigate("/app/admin/aimodel")}
                >
                  返回模型列表
                </Button>
              </Space>
            }
          />
        </div>

        {/* 授权配置模态窗口 */}
        <Modal
          title={modalType === "model" ? "配置模型授权" : "配置团队授权"}
          open={modalVisible}
          onCancel={() => setModalVisible(false)}
          onOk={handleSubmit}
          width={700}
          maskClosable={false}
          keyboard={false}
          confirmLoading={submitting}
        >
          <div className="modal-description">
            <Text strong>
              {modalType === "model"
                ? `模型: ${(currentItem as ModelAuthorizationItem)?.title || (currentItem as ModelAuthorizationItem)?.name}`
                : `团队: ${(currentItem as TeamAuthorizationItem)?.teamName}`}
            </Text>
          </div>
          <Transfer
            dataSource={transferData}
            titles={[
              modalType === "model" ? "未授权团队" : "未授权模型",
              modalType === "model" ? "已授权团队" : "已授权模型",
            ]}
            targetKeys={targetKeys}
            onChange={handleTransferChange}
            render={(item) => item.title}
            listStyle={{
              width: 300,
              height: 400,
            }}
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
