import { useState, useEffect, useCallback } from "react";
import {
  Card,
  Table,
  Button,
  message,
  Modal,
  Form,
  Input,
  Space,
  Tag,
  Popconfirm,
  Tabs,
  Select,
  Avatar,
  Slider,
  Collapse,
  Spin,
  Empty,
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  StopOutlined,
  CheckCircleOutlined,
  AppstoreOutlined,
  RobotOutlined,
  SettingOutlined,
  BookOutlined,
  ApiOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useOutletContext } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../helper/RequestError";
import type {
  QueryAppListCommandResponseItem,
  QueryAppDetailInfoCommandResponse,
  PublicModelInfo,
  QueryWikiInfoResponse,
  PluginSimpleInfo,
  KeyValueString,
  TeamRole,
} from "../../apiClient/models";
import { AppTypeObject, AiModelTypeObject, TeamRoleObject } from "../../apiClient/models";
import type { Guid } from "@microsoft/kiota-abstractions";

const { TextArea } = Input;

interface TeamContext {
  teamInfo: { id?: number; name?: string } | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

export default function TeamApps() {
  const { id } = useParams();
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  const teamId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [apps, setApps] = useState<QueryAppListCommandResponseItem[]>([]);
  const [activeTab, setActiveTab] = useState<string>("internal");

  // 创建应用弹窗
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [createForm] = Form.useForm();

  // 编辑应用弹窗
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editForm] = Form.useForm();
  const [currentApp, setCurrentApp] = useState<QueryAppDetailInfoCommandResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // 模型、知识库、插件列表
  const [models, setModels] = useState<PublicModelInfo[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [wikis, setWikis] = useState<QueryWikiInfoResponse[]>([]);
  const [wikisLoading, setWikisLoading] = useState(false);
  const [plugins, setPlugins] = useState<PluginSimpleInfo[]>([]);
  const [pluginsLoading, setPluginsLoading] = useState(false);

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  // 加载应用列表
  const fetchApps = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const isForeign = activeTab === "external" ? true : false;
      const response = await client.api.app.manage.list.get({
        queryParameters: { teamId, isForeign },
      });
      setApps(response?.items || []);
    } catch (error) {
      console.error("获取应用列表失败:", error);
      proxyRequestError(error, messageApi, "获取应用列表失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, activeTab, messageApi]);

  // 加载模型列表
  const loadModels = useCallback(async () => {
    setModelsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.aimodel.modellist.post({
        aiModelType: AiModelTypeObject.Chat,
      });
      setModels(response?.aiModels || []);
    } catch (error) {
      console.error("加载模型列表失败:", error);
    } finally {
      setModelsLoading(false);
    }
  }, []);

  // 加载知识库列表
  const loadWikis = useCallback(async () => {
    setWikisLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.wiki.query_wiki_list.post({ teamId });
      setWikis(response || []);
    } catch (error) {
      console.error("加载知识库列表失败:", error);
    } finally {
      setWikisLoading(false);
    }
  }, [teamId]);

  // 加载插件列表
  const loadPlugins = useCallback(async () => {
    setPluginsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.plugin_list.get();
      setPlugins(response?.items || []);
    } catch (error) {
      console.error("加载插件列表失败:", error);
    } finally {
      setPluginsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchApps();
  }, [fetchApps]);

  // 打开创建弹窗时加载模型列表
  const handleOpenCreateModal = () => {
    createForm.resetFields();
    createForm.setFieldsValue({
      isForeign: activeTab === "external",
      appType: AppTypeObject.Common,
    });
    setCreateModalOpen(true);
    loadModels();
  };

  // 创建应用
  const handleCreate = async (values: {
    name: string;
    description?: string;
    isForeign: boolean;
    appType: string;
    modelId: number;
  }) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      await client.api.app.manage.create.post({
        teamId,
        name: values.name,
        description: values.description,
        isForeign: values.isForeign,
        appType: values.appType as typeof AppTypeObject.Common,
        modelId: values.modelId,
      });
      messageApi.success("创建应用成功");
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchApps();
    } catch (error) {
      console.error("创建应用失败:", error);
      proxyFormRequestError(error, messageApi, createForm, "创建应用失败");
    } finally {
      setCreateLoading(false);
    }
  };

  // 获取应用详情并打开编辑弹窗
  const handleOpenEditModal = async (appId: Guid) => {
    try {
      setDetailLoading(true);
      setEditModalOpen(true);
      loadModels();
      loadWikis();
      loadPlugins();

      const client = GetApiClient();
      const response = await client.api.app.manage.detail_info.get({
        queryParameters: { teamId, appId },
      });

      if (response) {
        setCurrentApp(response);
        const executionSettings = response.executionSettings || [];
        const getSettingValue = (key: string, defaultValue: number): number => {
          const setting = executionSettings.find((s) => s.key === key);
          return setting?.value ? parseFloat(setting.value) : defaultValue;
        };

        editForm.setFieldsValue({
          name: response.name,
          description: response.description,
          modelId: response.modelId,
          prompt: response.prompt,
          temperature: getSettingValue("temperature", 1),
          topP: getSettingValue("top_p", 1),
          presencePenalty: getSettingValue("presence_penalty", 0),
          frequencyPenalty: getSettingValue("frequency_penalty", 0),
          wikiIds: response.wikiIds || [],
          plugins: response.plugins || [],
          isPublic: response.isPublic,
          isAuth: response.isAuth,
        });
      }
    } catch (error) {
      console.error("获取应用详情失败:", error);
      proxyRequestError(error, messageApi, "获取应用详情失败");
      setEditModalOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  // 更新应用
  const handleUpdate = async (values: {
    name: string;
    description?: string;
    modelId: number;
    prompt?: string;
    temperature: number;
    topP: number;
    presencePenalty: number;
    frequencyPenalty: number;
    wikiIds: number[];
    plugins: string[];
    isPublic?: boolean;
    isAuth?: boolean;
  }) => {
    if (!currentApp?.appId) return;

    try {
      setEditLoading(true);
      const client = GetApiClient();

      const executionSettings: KeyValueString[] = [
        { key: "temperature", value: String(values.temperature) },
        { key: "top_p", value: String(values.topP) },
        { key: "presence_penalty", value: String(values.presencePenalty) },
        { key: "frequency_penalty", value: String(values.frequencyPenalty) },
      ];

      await client.api.app.manage.update.post({
        teamId,
        appId: currentApp.appId,
        name: values.name,
        description: values.description,
        modelId: values.modelId,
        prompt: values.prompt,
        executionSettings,
        wikiIds: values.wikiIds,
        plugins: values.plugins,
        isPublic: values.isPublic,
        isAuth: values.isAuth,
      });

      messageApi.success("更新应用成功");
      setEditModalOpen(false);
      setCurrentApp(null);
      fetchApps();
    } catch (error) {
      console.error("更新应用失败:", error);
      proxyFormRequestError(error, messageApi, editForm, "更新应用失败");
    } finally {
      setEditLoading(false);
    }
  };

  // 启用/禁用应用
  const handleToggleDisable = async (appId: Guid, isDisable: boolean) => {
    try {
      const client = GetApiClient();
      await client.api.app.manage.set_disable.post({
        teamId,
        appId,
        isDisable,
      });
      messageApi.success(isDisable ? "已禁用应用" : "已启用应用");
      fetchApps();
    } catch (error) {
      console.error("操作失败:", error);
      proxyRequestError(error, messageApi, "操作失败");
    }
  };

  // 删除应用
  const handleDelete = async (appId: Guid) => {
    try {
      const client = GetApiClient();
      await client.api.app.manage.deletePath.delete({
        teamId,
        appId,
      });
      messageApi.success("删除应用成功");
      fetchApps();
    } catch (error) {
      console.error("删除应用失败:", error);
      proxyRequestError(error, messageApi, "删除应用失败");
    }
  };

  const columns = [
    {
      title: "应用",
      key: "app",
      render: (_: unknown, record: QueryAppListCommandResponseItem) => (
        <Space>
          <Avatar
            src={record.avatar}
            icon={<AppstoreOutlined />}
            size="small"
          />
          <span>{record.name}</span>
        </Space>
      ),
    },
    {
      title: "类型",
      dataIndex: "appType",
      key: "appType",
      render: (appType: string) => (
        <Tag color={appType === AppTypeObject.Workflow ? "purple" : "blue"}>
          {appType === AppTypeObject.Workflow ? "流程编排" : "普通应用"}
        </Tag>
      ),
    },
    {
      title: "状态",
      key: "status",
      render: (_: unknown, record: QueryAppListCommandResponseItem) => (
        <Tag color={record.isDisable ? "red" : "green"}>
          {record.isDisable ? "已禁用" : "已启用"}
        </Tag>
      ),
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
    },
    {
      title: "操作",
      key: "action",
      render: (_: unknown, record: QueryAppListCommandResponseItem) => {
        if (!canManage) return null;
        return (
          <Space>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              onClick={() => handleOpenEditModal(record.appId!)}
            >
              配置
            </Button>
            <Button
              type="link"
              size="small"
              icon={record.isDisable ? <CheckCircleOutlined /> : <StopOutlined />}
              onClick={() => handleToggleDisable(record.appId!, !record.isDisable)}
            >
              {record.isDisable ? "启用" : "禁用"}
            </Button>
            <Popconfirm
              title="确认删除"
              description="确定要删除此应用吗？此操作不可恢复。"
              onConfirm={() => handleDelete(record.appId!)}
              okText="确定"
              cancelText="取消"
            >
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                删除
              </Button>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  const tabItems = [
    { key: "internal", label: "内部应用" },
    { key: "external", label: "外部应用" },
  ];

  const editCollapseItems = [
    {
      key: "basic",
      label: (
        <Space>
          <RobotOutlined />
          <span>基本信息</span>
        </Space>
      ),
      children: (
        <>
          <Form.Item
            name="name"
            label="应用名称"
            rules={[{ required: true, message: "请输入应用名称" }]}
          >
            <Input placeholder="请输入应用名称" maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label="应用描述">
            <TextArea placeholder="请输入应用描述" rows={2} maxLength={200} />
          </Form.Item>
          <Form.Item name="prompt" label="系统提示词">
            <TextArea placeholder="设定AI助手的角色和行为" rows={4} />
          </Form.Item>
        </>
      ),
    },
    {
      key: "model",
      label: (
        <Space>
          <SettingOutlined />
          <span>模型配置</span>
        </Space>
      ),
      children: (
        <Spin spinning={modelsLoading}>
          <Form.Item
            name="modelId"
            label="AI模型"
            rules={[{ required: true, message: "请选择AI模型" }]}
          >
            <Select
              placeholder="选择模型"
              options={models.map((m) => ({
                label: m.title || m.name,
                value: m.id,
              }))}
            />
          </Form.Item>
          <Form.Item name="temperature" label="temperature">
            <Slider min={0} max={2} step={0.1} />
          </Form.Item>
          <Form.Item name="topP" label="top_p">
            <Slider min={0} max={1} step={0.1} />
          </Form.Item>
          <Form.Item name="presencePenalty" label="presence_penalty">
            <Slider min={-2} max={2} step={0.1} />
          </Form.Item>
          <Form.Item name="frequencyPenalty" label="frequency_penalty">
            <Slider min={-2} max={2} step={0.1} />
          </Form.Item>
        </Spin>
      ),
    },
    {
      key: "wiki",
      label: (
        <Space>
          <BookOutlined />
          <span>知识库</span>
        </Space>
      ),
      children: (
        <Spin spinning={wikisLoading}>
          {wikis.length === 0 ? (
            <Empty description="暂无知识库" />
          ) : (
            <Form.Item name="wikiIds" label="选择知识库">
              <Select
                mode="multiple"
                placeholder="选择知识库"
                options={wikis.map((w) => ({
                  label: w.name,
                  value: w.wikiId,
                }))}
              />
            </Form.Item>
          )}
        </Spin>
      ),
    },
    {
      key: "plugin",
      label: (
        <Space>
          <ApiOutlined />
          <span>插件</span>
        </Space>
      ),
      children: (
        <Spin spinning={pluginsLoading}>
          {plugins.length === 0 ? (
            <Empty description="暂无插件" />
          ) : (
            <Form.Item name="plugins" label="选择插件">
              <Select
                mode="multiple"
                placeholder="选择插件（最多3个）"
                maxCount={3}
                options={plugins.map((p) => ({
                  label: p.title || p.pluginName,
                  value: p.pluginName,
                }))}
              />
            </Form.Item>
          )}
        </Spin>
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <Card
        title={`${teamInfo?.name || ""} - 应用管理`}
        extra={
          canManage && (
            <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenCreateModal}>
              创建应用
            </Button>
          )
        }
      >
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
        />
        <Table
          rowKey="appId"
          columns={columns}
          dataSource={apps}
          loading={loading}
          pagination={{
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) => `第 ${range[0]}-${range[1]} 条，共 ${total} 条`,
          }}
          locale={{ emptyText: "暂无应用" }}
        />
      </Card>

      {/* 创建应用弹窗 */}
      <Modal
        title="创建应用"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={createForm} layout="vertical" onFinish={handleCreate}>
          <Form.Item
            name="name"
            label="应用名称"
            rules={[{ required: true, message: "请输入应用名称" }]}
          >
            <Input placeholder="请输入应用名称" maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label="应用描述">
            <TextArea placeholder="请输入应用描述" rows={2} maxLength={200} />
          </Form.Item>
          <Form.Item
            name="isForeign"
            label="应用类型"
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { label: "内部应用", value: false },
                { label: "外部应用", value: true },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="appType"
            label="应用模式"
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { label: "普通应用", value: AppTypeObject.Common },
                { label: "流程编排", value: AppTypeObject.Workflow, disabled: true },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="modelId"
            label="AI模型"
            rules={[{ required: true, message: "请选择AI模型" }]}
          >
            <Spin spinning={modelsLoading}>
              <Select
                placeholder="选择模型"
                options={models.map((m) => ({
                  label: m.title || m.name,
                  value: m.id,
                }))}
              />
            </Spin>
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={createLoading}>
                创建
              </Button>
              <Button onClick={() => setCreateModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 编辑应用弹窗 */}
      <Modal
        title="应用配置"
        open={editModalOpen}
        onCancel={() => {
          setEditModalOpen(false);
          setCurrentApp(null);
        }}
        footer={null}
        width={600}
        destroyOnClose
        maskClosable={false}
      >
        <Spin spinning={detailLoading}>
          <Form
            form={editForm}
            layout="vertical"
            onFinish={handleUpdate}
            initialValues={{
              temperature: 1,
              topP: 1,
              presencePenalty: 0,
              frequencyPenalty: 0,
              wikiIds: [],
              plugins: [],
            }}
          >
            <Collapse defaultActiveKey={["basic", "model"]} items={editCollapseItems} />
            <Form.Item style={{ marginTop: 16 }}>
              <Space>
                <Button type="primary" htmlType="submit" loading={editLoading}>
                  保存
                </Button>
                <Button onClick={() => setEditModalOpen(false)}>取消</Button>
              </Space>
            </Form.Item>
          </Form>
        </Spin>
      </Modal>
    </>
  );
}
