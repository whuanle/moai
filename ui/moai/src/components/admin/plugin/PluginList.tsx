import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Button,
  Space,
  Modal,
  Form,
  Input,
  Switch,
  Row,
  Col,
  Typography,
  message,
  Divider,
  Table,
  Tag,
  Empty,
  Spin,
  Popconfirm,
  Tooltip,
  Upload,
  Progress,
  Alert,
} from "antd";
import {
  PlusOutlined,
  ApiOutlined,
  DeleteOutlined,
  MinusCircleOutlined,
  ReloadOutlined,
  EditOutlined,
  UploadOutlined,
  EyeOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import {
  ImportMcpServerPluginCommand,
  KeyValueString,
  QueryCustomPluginBaseListCommand,
  QueryCustomPluginBaseListCommandResponse,
  PluginBaseInfoItem,
  PluginTypeObject,
  UpdateMcpServerPluginCommand,
  UpdateOpenApiPluginCommand,
  DeletePluginCommand,
  QueryCustomPluginDetailCommand,
  QueryCustomPluginDetailCommandResponse,
  PreUploadOpenApiFilePluginCommandResponse,
  QueryCustomPluginFunctionsListCommandResponse,
  PluginFunctionItem,
  QueryCustomPluginFunctionsListCommand
} from "../../../apiClient/models";
import {
  proxyFormRequestError,
  proxyRequestError,
} from "../../../helper/RequestError";
import { formatDateTime } from "../../../helper/DateTimeHelper";
import { GetFileMd5 } from "../../../helper/Md5Helper";
import useAppStore from "../../../stateshare/store";
import { FileTypeHelper } from "../../../helper/FileTypeHelper";
import "./PluginList.css";

const { Title } = Typography;
const { TextArea } = Input;

interface KeyValueItem {
  key: string;
  value: string;
}

interface OpenApiUploadStatus {
  file?: File;
  uploading: boolean;
  progress: number;
  error?: string;
  fileId?: number;
}

// 常量定义
const PLUGIN_TYPE_MAP = {
  [PluginTypeObject.Mcp]: { color: "green", text: "MCP" },
  [PluginTypeObject.OpenApi]: { color: "orange", text: "OpenAPI" },
} as const;

// 自定义Hook：文件上传
const useFileUpload = () => {
  const uploadOpenApiFile = useCallback(
    async (
      client: any,
      file: File
    ): Promise<PreUploadOpenApiFilePluginCommandResponse> => {
      const md5 = await GetFileMd5(file);
      console.log("文件MD5:", md5);
      const preUploadResponse = await client.api.admin.custom_plugin.pre_upload_openapi.post(
        {
          contentType: FileTypeHelper.getFileType(file),
          fileName: file.name,
          fileSize: file.size,
          mD5: md5,
        }
      );

      if (!preUploadResponse) {
        throw new Error("获取预签名URL失败");
      }

      if (preUploadResponse.isExist === true) {
        return preUploadResponse;
      }

      if (!preUploadResponse.uploadUrl) {
        throw new Error("获取预签名URL失败");
      }

      // 使用 fetch API 上传到预签名的 S3 URL
      const uploadResponse = await fetch(preUploadResponse.uploadUrl, {
        method: "PUT",
        body: file,
        headers: {
          "Content-Type": FileTypeHelper.getFileType(file),
          Authorization: `Bearer ${localStorage.getItem(
            "userinfo.accessToken"
          )}`,
        },
      });

      if (uploadResponse.status !== 200) {
        console.error("upload file error:");
        console.error(uploadResponse);
        throw new Error(uploadResponse.statusText);
      }

      await client.api.storage.complate_url.post({
        fileId: preUploadResponse.fileId,
        isSuccess: true,
      });

      return preUploadResponse;
    },
    []
  );

  return { uploadOpenApiFile };
};

// 自定义Hook：表单处理
const useFormHandlers = () => {
  const processKeyValueArray = useCallback(
    (values: KeyValueItem[]): KeyValueString[] => {
      return (
        values
          ?.filter((item: KeyValueItem) => item.key && item.value)
          .map((item: KeyValueItem) => ({
            key: item.key,
            value: item.value,
          })) || []
      );
    },
    []
  );

  return { processKeyValueArray };
};

export default function PluginList() {
  // 状态管理
  const [mcpModalVisible, setMcpModalVisible] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [editOpenApiModalVisible, setEditOpenApiModalVisible] = useState(false);
  const [submitLoading, setSubmitLoading] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editOpenApiLoading, setEditOpenApiLoading] = useState(false);
  const [pluginList, setPluginList] = useState<PluginBaseInfoItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [currentUser, setCurrentUser] = useState<any>(null);
  const [editingPlugin, setEditingPlugin] = useState<PluginBaseInfoItem | null>(
    null
  );

  // 表单实例
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const [editOpenApiForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();

  // OpenAPI相关状态
  const [openApiModalVisible, setOpenApiModalVisible] = useState(false);
  const [openApiForm] = Form.useForm();
  const [openApiUploadStatus, setOpenApiUploadStatus] =
    useState<OpenApiUploadStatus>({ uploading: false, progress: 0 });
  const [openApiSelectedFile, setOpenApiSelectedFile] = useState<File | null>(
    null
  );
  const [openApiSubmitLoading, setOpenApiSubmitLoading] = useState(false);
  const [editOpenApiSelectedFile, setEditOpenApiSelectedFile] =
    useState<File | null>(null);
  const [editOpenApiUploadStatus, setEditOpenApiUploadStatus] =
    useState<OpenApiUploadStatus>({ uploading: false, progress: 0 });
  const [currentOpenApiFileInfo, setCurrentOpenApiFileInfo] = useState<{
    fileName?: string;
    fileId?: number;
  } | null>(null);

  // 函数列表相关状态
  const [functionListModalVisible, setFunctionListModalVisible] =
    useState(false);
  const [functionList, setFunctionList] = useState<
   PluginFunctionItem[]
  >([]);
  const [functionListLoading, setFunctionListLoading] = useState(false);
  const [currentPlugin, setCurrentPlugin] = useState<PluginBaseInfoItem | null>(
    null
  );

  // 自定义Hooks
  const { uploadOpenApiFile } = useFileUpload();
  const { processKeyValueArray } = useFormHandlers();

  // 获取当前用户信息
  const fetchCurrentUser = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.common.userinfo.get();
      if (response) {
        setCurrentUser(response);
      }
    } catch (error) {
      console.log("Fetch current user error:", error);
    }
  }, []);

  // 获取插件列表
  const fetchPluginList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestData: QueryCustomPluginBaseListCommand = {};
      const response = await client.api.admin.custom_plugin.plugin_list.post(requestData);

      if (response?.items) {
        setPluginList(response.items);
      }
    } catch (error) {
      console.log("Fetch plugin list error:", error);
      proxyRequestError(error, messageApi, "获取插件列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 页面加载时获取数据
  useEffect(() => {
    fetchCurrentUser();
    fetchPluginList();
  }, [fetchCurrentUser, fetchPluginList]);

  // 刷新列表
  const handleRefresh = useCallback(() => {
    fetchPluginList();
  }, [fetchPluginList]);

  // 编辑插件
  const handleEdit = useCallback(
    async (record: PluginBaseInfoItem) => {
      setEditingPlugin(record);
      setEditLoading(true);

      try {
        const client = GetApiClient();
        const requestData: QueryCustomPluginDetailCommand = {
          pluginId: record.pluginId,
        };

        const response = await client.api.admin.custom_plugin.plugin_detail.post(
          requestData
        );

        if (response) {
          console.log("Plugin detail response:", response);

          if (record.type === PluginTypeObject.OpenApi) {
            setEditOpenApiModalVisible(true);
            editOpenApiForm.setFieldsValue({
              name: response.pluginName,
              title: response.title,
              serverUrl: response.server,
              description: response.description,
              header:
                response.header?.map((item) => ({
                  key: item.key,
                  value: item.value,
                })) || [],
              query:
                response.query?.map((item) => ({
                  key: item.key,
                  value: item.value,
                })) || [],
            });
            setEditOpenApiSelectedFile(null);
            setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
            setCurrentOpenApiFileInfo({
              fileName: response.openapiFileName || undefined,
              fileId: response.openapiFileId || undefined,
            });
          } else {
            setEditModalVisible(true);
            editForm.setFieldsValue({
              name: response.pluginName,
              title: response.title,
              description: response.description,
              serverUrl: response.server,
              header:
                response.header?.map((item) => ({
                  key: item.key,
                  value: item.value,
                })) || [],
              query:
                response.query?.map((item) => ({
                  key: item.key,
                  value: item.value,
                })) || [],
            });
          }
        }
      } catch (error) {
        console.log("Fetch plugin detail error:", error);
        proxyRequestError(error, messageApi, "获取插件详情失败");

        if (record.type === PluginTypeObject.OpenApi) {
          setEditOpenApiModalVisible(true);
          editOpenApiForm.setFieldsValue({
            name: record.pluginName,
            title: record.title,
            serverUrl: record.server,
            description: record.description,
            header: [],
            query: [],
          });
          setCurrentOpenApiFileInfo(null);
        } else {
          setEditModalVisible(true);
          editForm.setFieldsValue({
            name: record.pluginName,
            title: record.title,
            description: record.description,
            serverUrl: record.server,
            header: [],
            query: [],
          });
        }
      } finally {
        setEditLoading(false);
      }
    },
    [editForm, editOpenApiForm, messageApi]
  );

  // 删除插件
  const handleDelete = useCallback(
    async (pluginId: number) => {
      try {
        const client = GetApiClient();
        const requestData: DeletePluginCommand = { pluginId };
        await client.api.admin.custom_plugin.delete_plugin.delete(requestData);

        messageApi.success("插件删除成功");
        fetchPluginList();
      } catch (error) {
        console.log("Delete plugin error:", error);
        proxyRequestError(error, messageApi, "删除插件失败");
      }
    },
    [fetchPluginList, messageApi]
  );

  // 查看插件函数列表
  const handleViewFunctions = useCallback(
    async (record: PluginBaseInfoItem) => {
      setCurrentPlugin(record);
      setFunctionListLoading(true);
      setFunctionListModalVisible(true);

      try {
        const client = GetApiClient();
        const requestData: QueryCustomPluginFunctionsListCommand = {
          pluginId: record.pluginId,
        };

        const response = await client.api.admin.custom_plugin.function_list.post(
          requestData
        );

        if (response?.items) {
          setFunctionList(response.items);
        } else {
          setFunctionList([]);
        }
      } catch (error) {
        console.log("Fetch function list error:", error);
        proxyRequestError(error, messageApi, "获取函数列表失败");
        setFunctionList([]);
      } finally {
        setFunctionListLoading(false);
      }
    },
    [messageApi]
  );

  // 表格列定义
  const columns = useMemo(
    () => [
      {
        title: "插件名称",
        dataIndex: "pluginName",
        key: "pluginName",
        render: (pluginName: string) => (
          <Typography.Text strong>{pluginName}</Typography.Text>
        ),
      },
      {
        title: "标题",
        dataIndex: "title",
        key: "title",
        render: (title: string) => title || "-",
      },
      {
        title: "类型",
        dataIndex: "type",
        key: "type",
        render: (type: string) => {
          const typeInfo = PLUGIN_TYPE_MAP[
            type as keyof typeof PLUGIN_TYPE_MAP
          ] || { color: "default", text: type };
          return <Tag color={typeInfo.color}>{typeInfo.text}</Tag>;
        },
      },
      {
        title: "服务器地址",
        dataIndex: "server",
        key: "server",
        render: (server: string) => (
          <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
            {server || "-"}
          </Typography.Text>
        ),
      },
      {
        title: "描述",
        dataIndex: "description",
        key: "description",
        render: (description: string) => (
          <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
            {description || "-"}
          </Typography.Text>
        ),
      },
      {
        title: "是否公开",
        dataIndex: "isPublic",
        key: "isPublic",
        render: (isPublic: boolean) => (
          <Tag color={isPublic ? "green" : "orange"}>
            {isPublic ? "公开" : "私有"}
          </Tag>
        ),
      },
      {
        title: "创建用户",
        dataIndex: "createUserName",
        key: "createUserName",
        render: (createUserName: string) => createUserName || "-",
      },
      {
        title: "创建时间",
        dataIndex: "createTime",
        key: "createTime",
        render: (createTime: string) => {
          if (!createTime) return "-";
          try {
            return formatDateTime(createTime);
          } catch {
            return createTime;
          }
        },
      },
      {
        title: "操作",
        key: "action",
        width: 230,
        fixed: "right" as const,
        render: (_: any, record: PluginBaseInfoItem) => {
          const isOwner = currentUser?.userId === record.createUserId;

          return (
            <Space size="small">
              <Tooltip title="查看函数">
                <Button
                  type="link"
                  size="small"
                  icon={<EyeOutlined />}
                  onClick={() => handleViewFunctions(record)}
                >
                  查看
                </Button>
              </Tooltip>
              {isOwner && (
                <>
                  <Tooltip title="编辑插件">
                    <Button
                      type="link"
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => handleEdit(record)}
                    >
                      编辑
                    </Button>
                  </Tooltip>
                  <Popconfirm
                    title="删除插件"
                    description="确定要删除这个插件吗？删除后无法恢复。"
                    okText="确认删除"
                    cancelText="取消"
                    onConfirm={() => handleDelete(record.pluginId!)}
                  >
                    <Tooltip title="删除插件">
                      <Button
                        type="link"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                      >
                        删除
                      </Button>
                    </Tooltip>
                  </Popconfirm>
                </>
              )}
            </Space>
          );
        },
      },
    ],
    [currentUser?.userId, handleViewFunctions, handleEdit, handleDelete]
  );

  // 表单提交处理
  const handleSubmit = useCallback(async () => {
    try {
      const values = await form.validateFields();

      const headerArray = processKeyValueArray(values.header);
      const queryArray = processKeyValueArray(values.query);

      const requestData: ImportMcpServerPluginCommand = {
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: headerArray.length > 0 ? headerArray : undefined,
        query: queryArray.length > 0 ? queryArray : undefined,
      };

      setSubmitLoading(true);
      const client = GetApiClient();
      const response = await client.api.admin.custom_plugin.import_mcp.post(requestData);

      if (response?.value) {
        messageApi.success("MCP插件导入成功");
        setMcpModalVisible(false);
        form.resetFields();
        fetchPluginList();
      }
    } catch (error) {
      console.log("Import MCP error:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitLoading(false);
    }
  }, [form, processKeyValueArray, messageApi, fetchPluginList]);

  const handleEditSubmit = useCallback(async () => {
    if (!editingPlugin?.pluginId) {
      messageApi.error("插件信息不完整，请重新选择");
      return;
    }
    
    try {
      const values = await editForm.validateFields();

      const headerArray = processKeyValueArray(values.header);
      const queryArray = processKeyValueArray(values.query);

      const requestData: UpdateMcpServerPluginCommand = {
        pluginId: editingPlugin.pluginId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: headerArray.length > 0 ? headerArray : undefined,
        query: queryArray.length > 0 ? queryArray : undefined,
      };

      setEditLoading(true);
      const client = GetApiClient();
      await client.api.admin.custom_plugin.update_mcp.post(requestData);

      messageApi.success("插件更新成功");
      setEditModalVisible(false);
      setEditingPlugin(null);
      editForm.resetFields();
      fetchPluginList();
    } catch (error) {
      console.log("Update plugin error:", error);
      proxyFormRequestError(error, messageApi, editForm);
    } finally {
      setEditLoading(false);
    }
  }, [
    editForm,
    editingPlugin,
    processKeyValueArray,
    messageApi,
    fetchPluginList,
  ]);

  const handleEditOpenApiSubmit = useCallback(async () => {
    if (!editingPlugin?.pluginId) {
      messageApi.error("插件信息不完整，请重新选择");
      return;
    }
    
    try {
      const values = await editOpenApiForm.validateFields();
      setEditOpenApiLoading(true);

      const client = GetApiClient();
      let fileId: number | undefined;
      let fileName: string | undefined;

      if (editOpenApiSelectedFile) {
        console.log("开始上传新的 OpenAPI 文件...");
        setEditOpenApiUploadStatus({ uploading: true, progress: 10 });

        const uploadResult = await uploadOpenApiFile(
          client,
          editOpenApiSelectedFile
        );

        if (!uploadResult || !uploadResult.fileId) {
          throw new Error("文件上传失败");
        }

        fileId = uploadResult.fileId;
        fileName = editOpenApiSelectedFile.name;
        setEditOpenApiUploadStatus({ uploading: true, progress: 80 });
      }

      const headerArray = processKeyValueArray(values.header);
      const queryArray = processKeyValueArray(values.query);

      const requestData: UpdateOpenApiPluginCommand = {
        pluginId: editingPlugin.pluginId,
        name: values.name,
        title: values.title,
        serverUrl: values.serverUrl,
        description: values.description,
        header: headerArray.length > 0 ? headerArray : undefined,
        query: queryArray.length > 0 ? queryArray : undefined,
        fileId: fileId,
        fileName: fileName,
      };

      await client.api.admin.custom_plugin.update_openapi.post(requestData);

      setEditOpenApiUploadStatus({ uploading: false, progress: 100 });
      messageApi.success("OpenAPI 插件更新成功");
      setEditOpenApiModalVisible(false);
      setEditingPlugin(null);
      editOpenApiForm.resetFields();
      setEditOpenApiSelectedFile(null);
      setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
      setCurrentOpenApiFileInfo(null);
      fetchPluginList();
    } catch (error: any) {
      console.log("Update OpenAPI plugin error:", error);
      setEditOpenApiUploadStatus({
        uploading: false,
        progress: 0,
        error: error?.message || "更新失败",
      });
      proxyFormRequestError(error, messageApi, editOpenApiForm);
    } finally {
      setEditOpenApiLoading(false);
    }
  }, [
    editOpenApiForm,
    editingPlugin,
    editOpenApiSelectedFile,
    uploadOpenApiFile,
    processKeyValueArray,
    messageApi,
    fetchPluginList,
  ]);

  const handleOpenApiSubmit = useCallback(async () => {
    try {
      const values = await openApiForm.validateFields();
      if (!openApiSelectedFile) {
        messageApi.error("请先选择要上传的 OpenAPI 文件");
        return;
      }

      setOpenApiSubmitLoading(true);
      setOpenApiUploadStatus({ uploading: true, progress: 10 });
      const client = GetApiClient();

      console.log("开始上传 OpenAPI 文件...");
      const uploadResult = await uploadOpenApiFile(client, openApiSelectedFile);

      if (!uploadResult || !uploadResult.fileId) {
        throw new Error("文件上传失败");
      }

      setOpenApiUploadStatus({ uploading: true, progress: 80 });
      console.log("开始导入 OpenAPI 插件...");
      const importBody = {
        name: values.name,
        title: values.title,
        description: values.description,
        fileId: uploadResult.fileId,
        fileName: openApiSelectedFile.name,
      };

      await client.api.admin.custom_plugin.import_openapi.post(importBody);

      setOpenApiUploadStatus({ uploading: false, progress: 100 });
      messageApi.success("OpenAPI 插件导入成功");
      setOpenApiModalVisible(false);
      openApiForm.resetFields();
      setOpenApiSelectedFile(null);
      setOpenApiUploadStatus({ uploading: false, progress: 0 });
      fetchPluginList();
    } catch (error: any) {
      console.error("OpenAPI 导入失败:", error);
      setOpenApiUploadStatus({
        uploading: false,
        progress: 0,
        error: error?.message || "导入失败",
      });
      proxyFormRequestError(error, messageApi, openApiForm);
    } finally {
      setOpenApiSubmitLoading(false);
    }
  }, [
    openApiForm,
    openApiSelectedFile,
    uploadOpenApiFile,
    messageApi,
    fetchPluginList,
  ]);

  // 文件处理函数
  const handleFileSelect = useCallback((file: File) => {
    console.log("File selected:", file);
    setOpenApiSelectedFile(file);
    setOpenApiUploadStatus({ uploading: false, progress: 0 });
  }, []);

  const handleOpenApiFileChange = useCallback((info: any) => {
    if (info.file.status === "removed") {
      setOpenApiSelectedFile(null);
      setOpenApiUploadStatus({ uploading: false, progress: 0 });
      return;
    }

    if (info.file.status === "done" || info.file.originFileObj) {
      const file = info.file.originFileObj || info.file;
      setOpenApiSelectedFile(file);
      setOpenApiUploadStatus({ uploading: false, progress: 0 });
    }
  }, []);

  const handleEditOpenApiFileSelect = useCallback((file: File) => {
    setEditOpenApiSelectedFile(file);
    setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
  }, []);

  const handleEditOpenApiFileChange = useCallback((info: any) => {
    if (info.file.status === "removed") {
      setEditOpenApiSelectedFile(null);
      setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
      return;
    }

    if (info.file.status === "done" || info.file.originFileObj) {
      const file = info.file.originFileObj || info.file;
      setEditOpenApiSelectedFile(file);
      setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
    }
  }, []);

  // 模态框控制函数
  const handleImportMCP = useCallback(() => {
    setMcpModalVisible(true);
    form.resetFields();
  }, [form]);

  const handleCancel = useCallback(() => {
    setMcpModalVisible(false);
    form.resetFields();
    setSubmitLoading(false);
  }, [form]);

  const handleEditCancel = useCallback(() => {
    setEditModalVisible(false);
    setEditingPlugin(null);
    editForm.resetFields();
    setEditLoading(false);
  }, [editForm]);

  const handleEditOpenApiCancel = useCallback(() => {
    setEditOpenApiModalVisible(false);
    setEditingPlugin(null);
    editOpenApiForm.resetFields();
    setEditOpenApiLoading(false);
    setEditOpenApiSelectedFile(null);
    setEditOpenApiUploadStatus({ uploading: false, progress: 0 });
    setCurrentOpenApiFileInfo(null);
  }, [editOpenApiForm]);

  const handleImportOpenApi = useCallback(() => {
    setOpenApiModalVisible(true);
    openApiForm.resetFields();
    setOpenApiSelectedFile(null);
    setOpenApiUploadStatus({ uploading: false, progress: 0 });
  }, [openApiForm]);

  const handleFunctionListModalCancel = useCallback(() => {
    setFunctionListModalVisible(false);
    setCurrentPlugin(null);
    setFunctionList([]);
    setFunctionListLoading(false);
  }, []);

  // 渲染Header和Query配置组件
  const renderKeyValueConfig = useCallback(
    (name: string, title: string) => (
      <>
        <Divider orientation="left">{title} 配置</Divider>
        <Form.List name={name}>
          {(fields, { add, remove }) => (
            <>
              {fields.map(({ key, name: fieldName, ...restField }) => (
                <Row gutter={16} key={key} style={{ marginBottom: 8 }}>
                  <Col span={10}>
                    <Form.Item
                      {...restField}
                      name={[fieldName, "key"]}
                      rules={[
                        { required: true, message: `请输入${title} Key` },
                      ]}
                    >
                      <Input placeholder={`${title} Key`} />
                    </Form.Item>
                  </Col>
                  <Col span={10}>
                    <Form.Item
                      {...restField}
                      name={[fieldName, "value"]}
                      rules={[
                        { required: true, message: `请输入${title} Value` },
                      ]}
                    >
                      <Input placeholder={`${title} Value`} />
                    </Form.Item>
                  </Col>
                  <Col span={4}>
                    <Button
                      type="text"
                      icon={<MinusCircleOutlined />}
                      onClick={() => remove(fieldName)}
                      danger
                    />
                  </Col>
                </Row>
              ))}
              <Form.Item>
                <Button
                  type="dashed"
                  onClick={() => add()}
                  block
                  icon={<PlusOutlined />}
                >
                  添加 {title}
                </Button>
              </Form.Item>
            </>
          )}
        </Form.List>
      </>
    ),
    []
  );

  return (
    <>
      {contextHolder}
      <div className="custom-plugin-content">
        <div className="custom-plugin-header">
          <Title level={4} style={{ margin: 0 }}>
            <ApiOutlined style={{ marginRight: "8px" }} />
            自定义插件管理
          </Title>
          <Space>
            <Button
              icon={<ReloadOutlined />}
              onClick={handleRefresh}
              loading={loading}
            >
              刷新
            </Button>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleImportMCP}
            >
              导入 MCP
            </Button>
            <Button
              type="primary"
              icon={<ApiOutlined />}
              onClick={handleImportOpenApi}
            >
              导入 OpenAPI
            </Button>
          </Space>
        </div>

        <Table
          columns={columns}
            dataSource={pluginList}
            rowKey="pluginId"
            loading={loading}
            pagination={false}
            scroll={{ x: 1200 }}
            locale={{
              emptyText: (
                <Empty
                  description="暂无插件数据"
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                />
              ),
            }}
          />
      </div>

        {/* 导入MCP模态框 */}
        <Modal
          title="导入 MCP 插件"
          open={mcpModalVisible}
          onOk={handleSubmit}
          onCancel={handleCancel}
          width={800}
          okText="确定"
          cancelText="取消"
          destroyOnClose
          confirmLoading={submitLoading}
          maskClosable={false}
        >
          <Form form={form} layout="vertical" style={{ marginTop: "16px" }}>
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="name"
                  label="插件名称"
                  rules={[
                    { required: true, message: "请输入插件名称" },
                    { pattern: /^[a-zA-Z]+$/, message: "插件名称只能包含字母" },
                  ]}
                >
                  <Input placeholder="请输入插件名称（仅限字母）" />
                </Form.Item>
                <div
                  style={{
                    fontSize: "12px",
                    color: "#666",
                    marginTop: "-8px",
                    marginBottom: "8px",
                  }}
                >
                  * 插件名称会被 AI 识别使用，请确保具有明确含义
                </div>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="title"
                  label="插件标题"
                  rules={[{ required: true, message: "请输入插件标题" }]}
                >
                  <Input placeholder="请输入插件标题" />
                </Form.Item>
                <div
                  style={{
                    fontSize: "12px",
                    color: "#666",
                    marginTop: "-8px",
                    marginBottom: "8px",
                  }}
                >
                  * 插件标题只用于系统显示
                </div>
              </Col>
            </Row>
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="serverUrl"
                  label="MCP服务地址"
                  rules={[{ required: true, message: "请输入MCP服务地址" }]}
                >
                  <Input placeholder="请输入MCP服务地址" />
                </Form.Item>
              </Col>
            </Row>

            <Form.Item name="description" label="描述">
              <TextArea placeholder="请输入插件描述" rows={3} />
            </Form.Item>

            {renderKeyValueConfig("header", "Header")}
            {renderKeyValueConfig("query", "Query")}
          </Form>
        </Modal>

        {/* 导入OpenAPI模态框 */}
        <Modal
          title="导入 OpenAPI 插件"
          open={openApiModalVisible}
          onOk={handleOpenApiSubmit}
          onCancel={() => setOpenApiModalVisible(false)}
          width={600}
          okText="导入"
          cancelText="取消"
          destroyOnClose
          confirmLoading={openApiSubmitLoading}
          maskClosable={false}
        >
          <Form form={openApiForm} layout="vertical">
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="name"
                  label="插件名称"
                  rules={[
                    { required: true, message: "请输入插件名称" },
                    { pattern: /^[a-zA-Z]+$/, message: "插件名称只能包含字母" },
                  ]}
                >
                  <Input placeholder="请输入插件名称（仅限字母）" />
                </Form.Item>
                <div
                  style={{
                    fontSize: "12px",
                    color: "#666",
                    marginTop: "-8px",
                    marginBottom: "8px",
                  }}
                >
                  * 插件名称会被 AI 识别使用，请确保具有明确含义
                </div>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="title"
                  label="插件标题"
                  rules={[{ required: true, message: "请输入插件标题" }]}
                >
                  <Input placeholder="请输入插件标题" />
                </Form.Item>
                <div
                  style={{
                    fontSize: "12px",
                    color: "#666",
                    marginTop: "-8px",
                    marginBottom: "8px",
                  }}
                >
                  * 插件标题只用于系统显示
                </div>
              </Col>
            </Row>
            <Form.Item name="description" label="描述">
              <TextArea placeholder="请输入插件描述" rows={3} />
            </Form.Item>
            <Form.Item
              label="OpenAPI 文件"
              required
              validateStatus={
                !openApiSelectedFile && openApiSubmitLoading
                  ? "error"
                  : undefined
              }
              help={
                !openApiSelectedFile && openApiSubmitLoading
                  ? "请上传 OpenAPI 文件"
                  : undefined
              }
            >
              <Upload.Dragger
                accept=".json,.yaml,.yml"
                maxCount={1}
                beforeUpload={(file) => {
                  handleFileSelect(file);
                  return false;
                }}
                fileList={
                  openApiSelectedFile
                    ? [
                        {
                          uid: "1",
                          name: openApiSelectedFile.name,
                          status: "done",
                          size: openApiSelectedFile.size,
                        },
                      ]
                    : []
                }
                onChange={handleOpenApiFileChange}
                onRemove={() => {
                  setOpenApiSelectedFile(null);
                  setOpenApiUploadStatus({ uploading: false, progress: 0 });
                }}
                showUploadList={{
                  showRemoveIcon: true,
                  showPreviewIcon: false,
                }}
                disabled={openApiSubmitLoading}
              >
                <p className="ant-upload-drag-icon">
                  <UploadOutlined />
                </p>
                <p className="ant-upload-text">
                  点击或拖拽文件到此区域上传，仅支持 JSON/YAML 格式
                </p>
                {openApiSelectedFile && (
                  <p className="ant-upload-hint" style={{ color: "#52c41a" }}>
                    已选择文件: {openApiSelectedFile.name} (
                    {openApiSelectedFile.size} bytes)
                  </p>
                )}
                <p
                  className="ant-upload-hint"
                  style={{ fontSize: "12px", color: "#999" }}
                >
                  当前状态: {openApiSelectedFile ? "已选择文件" : "未选择文件"}
                </p>
              </Upload.Dragger>
              {openApiUploadStatus.uploading && (
                <Progress
                  percent={openApiUploadStatus.progress}
                  size="small"
                  style={{ marginTop: 8 }}
                  format={(percent) => {
                    if (percent && percent < 80) return "上传文件中...";
                    if (percent && percent < 100) return "导入插件中...";
                    return `${percent}%`;
                  }}
                />
              )}
              {openApiUploadStatus.error && (
                <Alert
                  type="error"
                  message={openApiUploadStatus.error}
                  showIcon
                  style={{ marginTop: 8 }}
                />
              )}
            </Form.Item>
          </Form>
        </Modal>

        {/* 编辑插件模态框 */}
        <Modal
          title="编辑 MCP 插件"
          open={editModalVisible}
          onOk={handleEditSubmit}
          onCancel={handleEditCancel}
          width={800}
          okText="确定"
          cancelText="取消"
          destroyOnClose
          confirmLoading={editLoading}
          maskClosable={false}
        >
          <Spin spinning={editLoading} tip="正在获取插件详情...">
            <Form
              form={editForm}
              layout="vertical"
              style={{ marginTop: "16px" }}
            >
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    name="name"
                    label="插件名称"
                    rules={[
                      { required: true, message: "请输入插件名称" },
                      {
                        pattern: /^[a-zA-Z]+$/,
                        message: "插件名称只能包含字母",
                      },
                    ]}
                  >
                    <Input placeholder="请输入插件名称（仅限字母）" />
                  </Form.Item>
                  <div
                    style={{
                      fontSize: "12px",
                      color: "#666",
                      marginTop: "-8px",
                      marginBottom: "8px",
                    }}
                  >
                    * 插件名称会被 AI 识别使用，请确保具有明确含义
                  </div>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="title"
                    label="插件标题"
                    rules={[{ required: true, message: "请输入插件标题" }]}
                  >
                    <Input placeholder="请输入插件标题" />
                  </Form.Item>
                  <div
                    style={{
                      fontSize: "12px",
                      color: "#666",
                      marginTop: "-8px",
                      marginBottom: "8px",
                    }}
                  >
                    * 插件标题只用于系统显示
                  </div>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    name="serverUrl"
                    label="MCP服务地址"
                    rules={[{ required: true, message: "请输入MCP服务地址" }]}
                  >
                    <Input placeholder="请输入MCP服务地址" />
                  </Form.Item>
                </Col>
              </Row>

              <Form.Item name="description" label="描述">
                <TextArea placeholder="请输入插件描述" rows={3} />
              </Form.Item>

              {renderKeyValueConfig("header", "Header")}
              {renderKeyValueConfig("query", "Query")}
            </Form>
          </Spin>
        </Modal>

        {/* 编辑OpenAPI插件模态框 */}
        <Modal
          title="编辑 OpenAPI 插件"
          open={editOpenApiModalVisible}
          onOk={handleEditOpenApiSubmit}
          onCancel={handleEditOpenApiCancel}
          width={800}
          okText="确定"
          cancelText="取消"
          destroyOnClose
          confirmLoading={editOpenApiLoading}
          maskClosable={false}
        >
          <Spin spinning={editOpenApiLoading} tip="正在获取插件详情...">
            <Form
              form={editOpenApiForm}
              layout="vertical"
              style={{ marginTop: "16px" }}
            >
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    name="name"
                    label="插件名称"
                    rules={[
                      { required: true, message: "请输入插件名称" },
                      {
                        pattern: /^[a-zA-Z]+$/,
                        message: "插件名称只能包含字母",
                      },
                    ]}
                  >
                    <Input placeholder="请输入插件名称（仅限字母）" />
                  </Form.Item>
                  <div
                    style={{
                      fontSize: "12px",
                      color: "#666",
                      marginTop: "-8px",
                      marginBottom: "8px",
                    }}
                  >
                    * 插件名称会被 AI 识别使用，请确保具有明确含义
                  </div>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="title"
                    label="插件标题"
                    rules={[{ required: true, message: "请输入插件标题" }]}
                  >
                    <Input placeholder="请输入插件标题" />
                  </Form.Item>
                  <div
                    style={{
                      fontSize: "12px",
                      color: "#666",
                      marginTop: "-8px",
                      marginBottom: "8px",
                    }}
                  >
                    * 插件标题只用于系统显示
                  </div>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    name="serverUrl"
                    label="服务器地址"
                    rules={[{ required: true, message: "请输入服务器地址" }]}
                  >
                    <Input placeholder="请输入服务器地址" />
                  </Form.Item>
                </Col>
              </Row>

              <Form.Item name="description" label="描述">
                <TextArea placeholder="请输入插件描述" rows={3} />
              </Form.Item>

              <Form.Item label="OpenAPI 文件（可选，重新上传将覆盖原文件）">
                {currentOpenApiFileInfo?.fileName && (
                  <Alert
                    message={`当前文件: ${currentOpenApiFileInfo.fileName}`}
                    type="info"
                    showIcon
                    style={{ marginBottom: 8 }}
                  />
                )}
                <Upload.Dragger
                  accept=".json,.yaml,.yml"
                  maxCount={1}
                  beforeUpload={(file) => {
                    handleEditOpenApiFileSelect(file);
                    return false;
                  }}
                  fileList={
                    editOpenApiSelectedFile
                      ? [
                          {
                            uid: "1",
                            name: editOpenApiSelectedFile.name,
                            status: "done",
                            size: editOpenApiSelectedFile.size,
                          },
                        ]
                      : []
                  }
                  onChange={handleEditOpenApiFileChange}
                  onRemove={() => {
                    setEditOpenApiSelectedFile(null);
                    setEditOpenApiUploadStatus({
                      uploading: false,
                      progress: 0,
                    });
                  }}
                  showUploadList={{
                    showRemoveIcon: true,
                    showPreviewIcon: false,
                  }}
                  disabled={editOpenApiLoading}
                >
                  <p className="ant-upload-drag-icon">
                    <UploadOutlined />
                  </p>
                  <p className="ant-upload-text">
                    点击或拖拽文件到此区域上传，仅支持 JSON/YAML 格式
                  </p>
                  {editOpenApiSelectedFile && (
                    <p className="ant-upload-hint" style={{ color: "#52c41a" }}>
                      已选择新文件: {editOpenApiSelectedFile.name} (
                      {editOpenApiSelectedFile.size} bytes)
                    </p>
                  )}
                  {!editOpenApiSelectedFile &&
                    currentOpenApiFileInfo?.fileName && (
                      <p
                        className="ant-upload-hint"
                        style={{ color: "#1890ff" }}
                      >
                        将使用原文件: {currentOpenApiFileInfo.fileName}
                      </p>
                    )}
                  <p
                    className="ant-upload-hint"
                    style={{ fontSize: "12px", color: "#999" }}
                  >
                    当前状态: {editOpenApiSelectedFile
                      ? "已选择新文件"
                      : currentOpenApiFileInfo?.fileName
                      ? "使用原文件"
                      : "未选择文件"}
                  </p>
                </Upload.Dragger>
                {editOpenApiUploadStatus.uploading && (
                  <Progress
                    percent={editOpenApiUploadStatus.progress}
                    size="small"
                    style={{ marginTop: 8 }}
                    format={(percent) => {
                      if (percent && percent < 80) return "上传文件中...";
                      if (percent && percent < 100) return "更新插件中...";
                      return `${percent}%`;
                    }}
                  />
                )}
                {editOpenApiUploadStatus.error && (
                  <Alert
                    type="error"
                    message={editOpenApiUploadStatus.error}
                    showIcon
                    style={{ marginTop: 8 }}
                  />
                )}
              </Form.Item>

              {renderKeyValueConfig("header", "Header")}
              {/* OpenAPI 插件不支持编辑 query 参数 */}
            </Form>
          </Spin>
        </Modal>

        {/* 函数列表模态框 */}
        <Modal
          title={`${currentPlugin?.pluginName || "插件"} - 函数列表`}
          open={functionListModalVisible}
          onCancel={handleFunctionListModalCancel}
          width={800}
          footer={null}
          destroyOnClose
          maskClosable={false}
        >
          {currentPlugin?.type === PluginTypeObject.Mcp && (
            <div
              style={{
                fontSize: "12px",
                color: "#666",
                marginBottom: "16px",
                padding: "8px 12px",
                backgroundColor: "#f5f5f5",
                borderRadius: "4px",
                border: "1px solid #d9d9d9",
              }}
            >
              MCP 插件会在使用时动态更新，本列表只供参考
            </div>
          )}
          <Spin spinning={functionListLoading} tip="正在获取函数列表...">
            <Table
              columns={[
                {
                  title: "函数ID",
                  dataIndex: "functionId",
                  key: "functionId",
                  width: 100,
                  render: (functionId: number) => functionId || "-",
                },
                {
                  title: "函数名称",
                  dataIndex: "name",
                  key: "name",
                  render: (name: string) => (
                    <Typography.Text strong>{name || "-"}</Typography.Text>
                  ),
                },
                {
                  title: "API路径",
                  dataIndex: "path",
                  key: "path",
                  render: (path: string) => (
                    <Typography.Text
                      type="secondary"
                      style={{ fontSize: "12px", fontFamily: "monospace" }}
                    >
                      {path || "-"}
                    </Typography.Text>
                  ),
                },
                {
                  title: "描述",
                  dataIndex: "summary",
                  key: "summary",
                  render: (summary: string) => (
                    <Typography.Text
                      type="secondary"
                      style={{ fontSize: "12px" }}
                    >
                      {summary || "-"}
                    </Typography.Text>
                  ),
                },
              ]}
              dataSource={functionList}
              rowKey="functionId"
              loading={functionListLoading}
              pagination={false}
              scroll={{ y: 400 }}
              locale={{
                emptyText: (
                  <Empty
                    description="暂无函数数据"
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                ),
              }}
            />
          </Spin>
        </Modal>
    </>
  );
}
