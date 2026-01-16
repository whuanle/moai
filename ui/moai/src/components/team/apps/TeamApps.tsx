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
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  StopOutlined,
  CheckCircleOutlined,
  AppstoreOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { useParams, useOutletContext, useNavigate } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../../helper/RequestError";
import type {
  QueryAppListCommandResponseItem,
  TeamRole,
  AppType,
} from "../../../apiClient/models";
import { AppTypeObject, TeamRoleObject } from "../../../apiClient/models";
import type { Guid } from "@microsoft/kiota-abstractions";

const { TextArea } = Input;

interface TeamContext {
  teamInfo: { id?: number; name?: string } | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

interface CreateFormValues {
  name: string;
  description?: string;
  isForeign: boolean;
  appType: AppType;
}

export default function TeamApps() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  const teamId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [apps, setApps] = useState<QueryAppListCommandResponseItem[]>([]);
  const [activeTab, setActiveTab] = useState<string>("internal");

  // 创建应用弹窗
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [createForm] = Form.useForm<CreateFormValues>();

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  // 加载应用列表
  const fetchApps = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const isForeign = activeTab === "external";
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

  useEffect(() => {
    fetchApps();
  }, [fetchApps]);

  // 打开创建弹窗
  const handleOpenCreateModal = () => {
    createForm.resetFields();
    createForm.setFieldsValue({
      isForeign: activeTab === "external",
      appType: AppTypeObject.Common,
    });
    setCreateModalOpen(true);
  };

  // 创建应用
  const handleCreate = async (values: CreateFormValues) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      await client.api.app.manage.create.post({
        teamId,
        name: values.name,
        description: values.description,
        isForeign: values.isForeign,
        appType: values.appType,
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

  // 跳转到配置页面
  const handleConfig = (record: QueryAppListCommandResponseItem) => {
    if (record.appType === AppTypeObject.Common) {
      navigate(`/app/team/${teamId}/apps/${record.appId}/config`);
    }
    // TODO: 流程编排类型的配置页面
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
          <Avatar src={record.avatar} icon={<AppstoreOutlined />} size="small" />
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
              onClick={() => handleConfig(record)}
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
        <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
        <Table
          rowKey="appId"
          columns={columns}
          dataSource={apps}
          loading={loading}
          pagination={false}
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
          <Form.Item name="isForeign" label="应用类型" rules={[{ required: true }]}>
            <Select
              options={[
                { label: "内部应用", value: false },
                { label: "外部应用", value: true },
              ]}
            />
          </Form.Item>
          <Form.Item name="appType" label="应用模式" rules={[{ required: true }]}>
            <Select
              options={[
                { label: "普通应用", value: AppTypeObject.Common },
                { label: "流程编排", value: AppTypeObject.Workflow, disabled: true },
              ]}
            />
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
    </>
  );
}
