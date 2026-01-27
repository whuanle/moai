import { useState, useEffect, useCallback } from "react";
import {
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
import type { TableProps } from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  StopOutlined,
  AppstoreOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { useParams, useOutletContext, useNavigate } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../../helper/RequestError";
import type {
  TeamRole,
  AppType,
  QueryAppListCommandResponseItem,
  AppClassifyItem,
} from "../../../apiClient/models";
import { AppTypeObject, TeamRoleObject } from "../../../apiClient/models";
import type { Guid } from "@microsoft/kiota-abstractions";
import "./TeamApps.css";
import { formatDateTimeStandard, formatRelativeTime } from "../../../helper/DateTimeHelper";

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
  const [apps, setApps] = useState<QueryAppListCommandResponseItem[]>([]);
  const [classifyList, setClassifyList] = useState<AppClassifyItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [selectedAppType, setSelectedAppType] = useState<AppType | null>(null);
  const [selectedIsForeign, setSelectedIsForeign] = useState<boolean | null>(null);

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
        classifyId: selectedCategory ?? undefined,
        appType: selectedAppType ?? undefined,
        isForeign: selectedIsForeign ?? undefined,
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
  }, [teamId, selectedCategory, selectedAppType, selectedIsForeign, messageApi]);

  useEffect(() => {
    fetchApps();
  }, [fetchApps]);

  // 打开创建弹窗
  const handleOpenCreateModal = () => {
    createForm.resetFields();
    createForm.setFieldsValue({
      isForeign: false,
      appType: AppTypeObject.Chat,
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
  const handleConfig = (record: QueryAppListCommandResponseItem) => {
    if (record.appType === AppTypeObject.Chat || record.appType === AppTypeObject.Agent) {
      navigate(`/app/team/${teamId}/apps/${record.appId}/config`);
    } else if (record.appType === AppTypeObject.Workflow) {
      navigate(`/app/team/${teamId}/apps/${record.appId}/workflow`);
    }
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

  const columns: TableProps<QueryAppListCommandResponseItem>['columns'] = [
    {
      title: "应用",
      dataIndex: "name",
      key: "name",
      sorter: (a, b) => (a.name ?? '').localeCompare(b.name ?? '', 'zh-CN'),
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
      render: (appType: AppType) => {
        if (appType === AppTypeObject.Workflow) {
          return <Tag color="purple">流程编排</Tag>;
        } else if (appType === AppTypeObject.Agent) {
          return <Tag color="cyan">智能体</Tag>;
        } else {
          return <Tag color="blue">对话应用</Tag>;
        }
      },
    },
    {
      title: "状态",
      dataIndex: "isDisable",
      key: "isDisable",
      render: (isDisable: boolean) => (
        <Tag color={isDisable ? "red" : "green"}>
          {isDisable ? "已禁用" : "正常"}
        </Tag>
      ),
    }, {
      title: "isForeign",
      dataIndex: "isForeign",
      key: "isForeign",
      render: (isForeign: boolean) => (
        <Tag color={isForeign ? "red" : "green"}>
          {isForeign ? "外部" : "正常"}
        </Tag>
      ),
    },
    {
      title: "创建人",
      dataIndex: "createUserName",
      key: "createUserName",
      sorter: (a, b) => (a.createUserName ?? '').localeCompare(b.createUserName ?? '', 'zh-CN'),
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
      sorter: (a, b) => (a.createTime ?? '').localeCompare(b.createTime ?? '', 'zh-CN'),
      render: (v) => formatDateTimeStandard(v)
    },
    {
      title: "最后修改人",
      dataIndex: "updateUserName",
      key: "updateUserName",
      sorter: (a, b) => (a.updateUserName ?? '').localeCompare(b.updateUserName ?? '', 'zh-CN'),
    },
    {
      title: "修改时间",
      dataIndex: "updateTime",
      key: "updateTime",
      sorter: (a, b) => (a.updateTime ?? '').localeCompare(b.updateTime ?? '', 'zh-CN'),
      render: (v) => formatRelativeTime(v)
    },
    {
      title: "操作",
      key: "action",
      fixed: 'right',
      width: 260,
      render: (_: unknown, record: QueryAppListCommandResponseItem) => {
        if (!canManage) return null;
        const isDisabled = record.isDisable === true;
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
            {isDisabled ? (
              <Button
                type="link"
                size="small"
                icon={<StopOutlined />}
                onClick={() => handleToggleDisable(record.appId!, false)}
              >
                启用
              </Button>
            ) : (
              <Button
                type="link"
                size="small"
                style={{ color: "red" }}
                danger
                icon={<StopOutlined />}
                onClick={() => handleToggleDisable(record.appId!, true)}
              >
                禁用
              </Button>
            )}
            <Popconfirm
              title="确认删除"
              description="确定要删除此应用吗？此操作不可恢复。"
              onConfirm={() => handleDelete(record.appId!)}
              okText="确定"
              cancelText="取消"
              okButtonProps={{ danger: true }}
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

        {/* 分类标签栏 */}
        <div className="app-category-bar">
          <div className="category-tags">
            <Tag
              className={`category-tag ${selectedCategory === null ? "active" : ""}`}
              onClick={() => setSelectedCategory(null)}
            >
              全部
            </Tag>
            {classifyList.map((category) => (
              <Tag
                key={category.classifyId}
                className={`category-tag ${selectedCategory === category.classifyId ? "active" : ""}`}
                onClick={() => setSelectedCategory(category.classifyId ?? null)}
              >
                {category.name}
              </Tag>
            ))}
          </div>
        </div>

        {/* 筛选工具栏 */}
        <div className="app-toolbar">
          <Select
            placeholder="应用类型"
            value={selectedAppType}
            onChange={setSelectedAppType}
            allowClear
            style={{ width: 150 }}
            options={[
              { label: "对话应用", value: AppTypeObject.Chat },
              { label: "智能体", value: AppTypeObject.Agent },
              { label: "流程编排", value: AppTypeObject.Workflow },
            ]}
          />
          <Select
            placeholder="是否外部应用"
            value={selectedIsForeign}
            onChange={setSelectedIsForeign}
            allowClear
            style={{ width: 150 }}
            options={[
              { label: "内部应用", value: false },
              { label: "外部应用", value: true },
            ]}
          />
          <Button icon={<ReloadOutlined />} onClick={fetchApps} loading={loading}>
            刷新
          </Button>
          {canManage && (
            <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenCreateModal}>
              创建应用
            </Button>
          )}
        </div>

        {/* 应用列表表格 */}
        <div className="team-apps-table-container">
          <Table
            rowKey="appId"
            columns={columns}
            dataSource={apps}
            loading={loading}
            pagination={false}
            locale={{ emptyText: "暂无应用" }}
            scroll={{ x: 1200 }}
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
        closable={false}
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
                { label: "对话应用", value: AppTypeObject.Chat },
                { label: "智能体", value: AppTypeObject.Agent, disabled: true },
                { label: "流程编排", value: AppTypeObject.Workflow },
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
