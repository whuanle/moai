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
  Select,
  Avatar,
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  StopOutlined,
  AppstoreOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { useParams, useOutletContext, useNavigate } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../../helper/RequestError";
import type {
  TeamRole,
  AppType,
  TeamAppItem,
  AppClassifyItem,
} from "../../../apiClient/models";
import { AppTypeObject, TeamRoleObject } from "../../../apiClient/models";
import type { Guid } from "@microsoft/kiota-abstractions";
import "./TeamApps.css";

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
  classifyId?: number;
}

export default function TeamApps() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  const teamId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [apps, setApps] = useState<TeamAppItem[]>([]);
  const [classifyList, setClassifyList] = useState<AppClassifyItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [isForeign, setIsForeign] = useState(false); // false=内部应用, true=外部应用

  // 创建应用弹窗
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [createForm] = Form.useForm<CreateFormValues>();

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  // 加载分类列表
  const fetchClassifyList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.app.store.classify_list.get();
      setClassifyList(response?.items || []);
    } catch (error) {
      console.error("获取分类列表失败:", error);
      // 分类列表加载失败不影响主流程，只记录错误
    }
  }, []);

  useEffect(() => {
    fetchClassifyList();
  }, [fetchClassifyList]);

  // 加载应用列表
  const fetchApps = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      
      const queryParams = {
        teamId,
        isForeign,
        classifyId: selectedCategory ?? undefined,
      };
      
      console.log("查询应用列表参数:", queryParams);
      
      const response = await client.api.app.team.list.post(queryParams);
      setApps(response?.items || []);
    } catch (error) {
      console.error("获取应用列表失败:", error);
      proxyRequestError(error, messageApi, "获取应用列表失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, isForeign, selectedCategory, messageApi]);

  useEffect(() => {
    fetchApps();
  }, [fetchApps]);

  // 打开创建弹窗
  const handleOpenCreateModal = () => {
    createForm.resetFields();
    createForm.setFieldsValue({
      isForeign: false,
      appType: AppTypeObject.Common,
    });
    setCreateModalOpen(true);
  };

  // 创建应用
  const handleCreate = async (values: CreateFormValues) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      await client.api.app.team.create.post({
        teamId,
        name: values.name,
        description: values.description,
        isForeign: values.isForeign,
        appType: values.appType,
        classifyId: values.classifyId,
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
  const handleConfig = (record: TeamAppItem) => {
    if (record.appType === 0) { // 0 = Common
      navigate(`/app/team/${teamId}/apps/${record.id}/config`);
    }
    // TODO: 流程编排类型的配置页面
  };

  // 启用/禁用应用
  const handleToggleDisable = async (appId: Guid, isDisable: boolean) => {
    try {
      const client = GetApiClient();
      await client.api.app.team.set_disable.post({
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
      await client.api.app.team.deletePath.delete({
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
      render: (_: unknown, record: TeamAppItem) => (
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
      render: (appType: number) => (
        <Tag color={appType === 1 ? "purple" : "blue"}>
          {appType === 1 ? "流程编排" : "普通应用"}
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
      render: (_: unknown, record: TeamAppItem) => {
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
              icon={<StopOutlined />}
              onClick={() => handleToggleDisable(record.id!, true)}
            >
              禁用
            </Button>
            <Popconfirm
              title="确认删除"
              description="确定要删除此应用吗？此操作不可恢复。"
              onConfirm={() => handleDelete(record.id!)}
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

  return (
    <>
      {contextHolder}
      <div className="page-container">
        {/* 页面标题 */}
        <div className="moai-page-header">
          <h1 className="moai-page-title">{teamInfo?.name || ""} - 应用管理</h1>
          <p className="moai-page-subtitle">管理团队应用，配置AI模型和功能</p>
        </div>

        {/* 应用类型切换 */}
        <div className="app-type-bar">
          <div className="app-type-tabs">
            <div
              className={`app-type-tab ${!isForeign ? "active" : ""}`}
              onClick={() => setIsForeign(false)}
            >
              内部应用
            </div>
            <div
              className={`app-type-tab ${isForeign ? "active" : ""}`}
              onClick={() => setIsForeign(true)}
            >
              外部应用
            </div>
          </div>
        </div>

        {/* 分类标签栏 */}
        <div className="app-category-bar">
          <div className="category-tags">
            <div
              className={`category-tag ${selectedCategory === null ? "active" : ""}`}
              onClick={() => setSelectedCategory(null)}
            >
              全部
            </div>
            {classifyList.map((category) => (
              <div
                key={category.classifyId}
                className={`category-tag ${selectedCategory === category.classifyId ? "active" : ""}`}
                onClick={() => setSelectedCategory(category.classifyId ?? null)}
              >
                {category.name}
              </div>
            ))}
          </div>
        </div>

        {/* 操作按钮 */}
        {canManage && (
          <div className="team-apps-actions">
            <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenCreateModal} size="large">
              创建应用
            </Button>
          </div>
        )}

        {/* 应用列表表格 */}
        <div className="team-apps-table-container">
          <Table
            rowKey="id"
            columns={columns}
            dataSource={apps}
            loading={loading}
            pagination={false}
            locale={{ emptyText: "暂无应用" }}
          />
        </div>
      </div>

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
          <Form.Item 
            name="classifyId" 
            label="应用分类"
            rules={[{ required: true, message: "请选择应用分类" }]}
          >
            <Select
              placeholder="请选择分类"
              options={classifyList.map((item) => ({
                label: item.name,
                value: item.classifyId,
              }))}
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
