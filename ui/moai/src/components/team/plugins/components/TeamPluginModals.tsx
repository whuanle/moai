/**
 * 团队插件相关模态框组件
 */
import { useState, useEffect, useCallback } from "react";
import { Modal, Form, Input, Row, Col, Select, Divider, Button, Spin, Upload, Progress, Alert, Table, Typography, Empty, message } from "antd";
import { PlusOutlined, MinusCircleOutlined, UploadOutlined } from "@ant-design/icons";
import type { PluginClassifyItem, PluginFunctionItem, PluginBaseInfoItem, KeyValueString } from "../../../../apiClient/models";
import { PluginTypeObject } from "../../../../apiClient/models";
import { GetApiClient } from "../../../ServiceClient";
import { proxyFormRequestError } from "../../../../helper/RequestError";
import { GetFileMd5 } from "../../../../helper/Md5Helper";
import { FileTypeHelper } from "../../../../helper/FileTypeHelper";

const { TextArea } = Input;

interface KeyValueItem {
  key: string;
  value: string;
}

function useClassifyList(open: boolean) {
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);

  useEffect(() => {
    if (open) {
      const fetchClassifyList = async () => {
        try {
          const client = GetApiClient();
          const response = await client.api.plugin.classify_list.get();
          setClassifyList(response?.items || []);
        } catch (error) {
          console.error("获取分类列表失败:", error);
        }
      };
      fetchClassifyList();
    }
  }, [open]);

  return { classifyList };
}

const processKeyValueArray = (values: KeyValueItem[]): KeyValueString[] => {
  return values?.filter((item) => item.key && item.value).map((item) => ({ key: item.key, value: item.value })) || [];
};

interface KeyValueConfigProps {
  name: string;
  title: string;
}

function KeyValueConfig({ name, title }: KeyValueConfigProps) {
  return (
    <>
      <Divider orientation="left" style={{ fontSize: 13, color: "var(--color-text-secondary)" }}>
        {title} 配置
      </Divider>
      <Form.List name={name}>
        {(fields, { add, remove }) => (
          <>
            {fields.map(({ key, name: fieldName, ...restField }) => (
              <Row gutter={16} key={key} style={{ marginBottom: 8 }}>
                <Col span={10}>
                  <Form.Item {...restField} name={[fieldName, "key"]} rules={[{ required: true, message: `请输入${title} Key` }]}>
                    <Input placeholder={`${title} Key`} />
                  </Form.Item>
                </Col>
                <Col span={10}>
                  <Form.Item {...restField} name={[fieldName, "value"]} rules={[{ required: true, message: `请输入${title} Value` }]}>
                    <Input placeholder={`${title} Value`} />
                  </Form.Item>
                </Col>
                <Col span={4}>
                  <Button type="text" icon={<MinusCircleOutlined />} onClick={() => remove(fieldName)} danger />
                </Col>
              </Row>
            ))}
            <Form.Item>
              <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>添加 {title}</Button>
            </Form.Item>
          </>
        )}
      </Form.List>
    </>
  );
}

interface BaseFormFieldsProps {
  classifyList: PluginClassifyItem[];
  showServerUrl?: boolean;
  serverUrlLabel?: string;
}

function BaseFormFields({ classifyList, showServerUrl = true, serverUrlLabel = "服务器地址" }: BaseFormFieldsProps) {
  return (
    <>
      <Row gutter={16}>
        <Col span={12}>
          <Form.Item
            name="name"
            label="插件名称"
            rules={[
              { required: true, message: "请输入插件名称" },
              { pattern: /^[a-zA-Z_]+$/, message: "仅支持字母和下划线" },
            ]}
            extra="插件名称会被 AI 识别使用"
          >
            <Input placeholder="请输入插件名称" />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="title" label="插件标题" rules={[{ required: true, message: "请输入插件标题" }]} extra="插件标题只用于系统显示">
            <Input placeholder="请输入插件标题" />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        {showServerUrl && (
          <Col span={12}>
            <Form.Item name="serverUrl" label={serverUrlLabel} rules={[{ required: true, message: `请输入${serverUrlLabel}` }]}>
              <Input placeholder={`请输入${serverUrlLabel}`} />
            </Form.Item>
          </Col>
        )}
        <Col span={showServerUrl ? 12 : 24}>
          <Form.Item name="classifyId" label="分类">
            <Select placeholder="请选择分类（可选）" allowClear>
              {classifyList.map((item) => (
                <Select.Option key={item.classifyId} value={item.classifyId}>{item.name}</Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Col>
      </Row>
      <Form.Item name="description" label="描述">
        <TextArea placeholder="请输入插件描述" rows={3} />
      </Form.Item>
    </>
  );
}

const HTTP_TRANSPORT_MODE_OPTIONS = [
  { value: "AutoDetect", label: "自动检测 (AutoDetect)" },
  { value: "StreamableHttp", label: "仅 Streamable HTTP (StreamableHttp)" },
  { value: "Sse", label: "仅 HTTP with SSE (Sse)" },
];

interface McpModalProps {
  open: boolean;
  loading: boolean;
  form: React.ComponentProps<typeof Form>["form"];
  onOk: () => void;
  onCancel: () => void;
  isEdit?: boolean;
}

export function TeamMcpPluginModal({ open, loading, form, onOk, onCancel, isEdit = false }: McpModalProps) {
  const { classifyList } = useClassifyList(open);

  return (
    <Modal
      title={isEdit ? "编辑 MCP 插件" : "导入 MCP 插件"}
      open={open}
      onOk={onOk}
      onCancel={onCancel}
      width={720}
      okText="确定"
      cancelText="取消"
      destroyOnClose
      confirmLoading={loading}
      maskClosable={false}
    >
      <Spin spinning={isEdit && loading} tip="正在获取插件详情...">
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <BaseFormFields classifyList={classifyList} showServerUrl serverUrlLabel="MCP服务地址" />
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="httpTransportMode" label="通讯方式">
                <Select placeholder="请选择通讯方式（可选）" allowClear options={HTTP_TRANSPORT_MODE_OPTIONS} />
              </Form.Item>
            </Col>
          </Row>
          <KeyValueConfig name="header" title="Header" />
          <KeyValueConfig name="query" title="Query" />
        </Form>
      </Spin>
    </Modal>
  );
}

async function uploadTeamOpenApiFile(file: File, teamId: number) {
  const client = GetApiClient();
  const md5 = await GetFileMd5(file);
  const preUploadResponse = await client.api.team.plugin.pre_upload_openapi.post({
    teamId,
    contentType: FileTypeHelper.getFileType(file),
    fileName: file.name,
    fileSize: file.size,
    mD5: md5,
  });

  if (!preUploadResponse) throw new Error("获取预签名URL失败");
  if (preUploadResponse.isExist === true) return preUploadResponse;
  if (!preUploadResponse.uploadUrl) throw new Error("获取预签名URL失败");

  const uploadResponse = await fetch(preUploadResponse.uploadUrl, {
    method: "PUT",
    body: file,
    headers: {
      "Content-Type": FileTypeHelper.getFileType(file),
      Authorization: `Bearer ${localStorage.getItem("userinfo.accessToken")}`,
    },
  });

  if (uploadResponse.status !== 200) throw new Error(uploadResponse.statusText);

  await client.api.storage.complate_url.post({ fileId: preUploadResponse.fileId, isSuccess: true });
  return preUploadResponse;
}

interface OpenApiImportModalProps {
  open: boolean;
  teamId: number | undefined;
  onSuccess: () => void;
  onCancel: () => void;
}

export function TeamOpenApiImportModal({ open, teamId, onSuccess, onCancel }: OpenApiImportModalProps) {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const { classifyList } = useClassifyList(open);
  const [loading, setLoading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadStatus, setUploadStatus] = useState({ uploading: false, progress: 0, error: "" });

  const handleCancel = useCallback(() => {
    form.resetFields();
    setSelectedFile(null);
    setUploadStatus({ uploading: false, progress: 0, error: "" });
    onCancel();
  }, [form, onCancel]);

  const handleSubmit = useCallback(async () => {
    if (!teamId) return;
    try {
      const values = await form.validateFields();
      if (!selectedFile) {
        messageApi.error("请先选择要上传的 OpenAPI 文件");
        return;
      }

      setLoading(true);
      setUploadStatus({ uploading: true, progress: 10, error: "" });

      const uploadResult = await uploadTeamOpenApiFile(selectedFile, teamId);
      if (!uploadResult?.fileId) throw new Error("文件上传失败");

      setUploadStatus({ uploading: true, progress: 80, error: "" });

      const client = GetApiClient();
      await client.api.team.plugin.import_openapi.post({
        teamId,
        name: values.name,
        title: values.title,
        description: values.description,
        fileId: uploadResult.fileId,
        fileName: selectedFile.name,
        classifyId: values.classifyId,
      });

      setUploadStatus({ uploading: false, progress: 100, error: "" });
      messageApi.success("OpenAPI 插件导入成功");
      handleCancel();
      onSuccess();
    } catch (error) {
      console.error("导入OpenAPI插件失败:", error);
      setUploadStatus({ uploading: false, progress: 0, error: "导入失败" });
      proxyFormRequestError(error, messageApi, form, "导入OpenAPI插件失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, form, selectedFile, messageApi, handleCancel, onSuccess]);

  return (
    <>
      {contextHolder}
      <Modal
        title="导入 OpenAPI 插件"
        open={open}
        onOk={handleSubmit}
        onCancel={handleCancel}
        width={600}
        okText="导入"
        cancelText="取消"
        destroyOnClose
        confirmLoading={loading}
        maskClosable={false}
      >
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <BaseFormFields classifyList={classifyList} showServerUrl={false} />
          <Form.Item label="OpenAPI 文件" required>
            <Upload.Dragger
              accept=".json,.yaml,.yml"
              maxCount={1}
              beforeUpload={(file) => { setSelectedFile(file); return false; }}
              fileList={selectedFile ? [{ uid: "1", name: selectedFile.name, status: "done" as const, size: selectedFile.size }] : []}
              onRemove={() => { setSelectedFile(null); setUploadStatus({ uploading: false, progress: 0, error: "" }); }}
              showUploadList={{ showRemoveIcon: true, showPreviewIcon: false }}
              disabled={loading}
            >
              <p className="ant-upload-drag-icon"><UploadOutlined style={{ fontSize: 36, color: "var(--color-primary)" }} /></p>
              <p className="ant-upload-text">点击或拖拽文件上传</p>
              <p className="ant-upload-hint">支持 JSON/YAML 格式</p>
            </Upload.Dragger>
            {uploadStatus.uploading && <Progress percent={uploadStatus.progress} size="small" style={{ marginTop: 8 }} />}
            {uploadStatus.error && <Alert type="error" message={uploadStatus.error} showIcon style={{ marginTop: 8 }} />}
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

interface OpenApiEditModalProps {
  open: boolean;
  teamId: number | undefined;
  plugin: PluginBaseInfoItem | null;
  onSuccess: () => void;
  onCancel: () => void;
}

export function TeamOpenApiEditModal({ open, teamId, plugin, onSuccess, onCancel }: OpenApiEditModalProps) {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const { classifyList } = useClassifyList(open);
  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [currentFileName, setCurrentFileName] = useState<string | undefined>();
  const [uploadStatus, setUploadStatus] = useState({ uploading: false, progress: 0, error: "" });

  useEffect(() => {
    if (open && plugin?.pluginId && teamId) {
      const fetchDetail = async () => {
        setDetailLoading(true);
        try {
          const client = GetApiClient();
          const response = await client.api.team.plugin.detail.post({ teamId, pluginId: plugin.pluginId });
          if (response) {
            form.setFieldsValue({
              name: response.pluginName,
              title: response.title,
              serverUrl: response.server,
              description: response.description,
              classifyId: response.classifyId,
              header: response.header?.map((item) => ({ key: item.key, value: item.value })) || [],
            });
            setCurrentFileName(response.openapiFileName || undefined);
          }
        } catch (error) {
          console.error("获取插件详情失败:", error);
        } finally {
          setDetailLoading(false);
        }
      };
      fetchDetail();
    }
  }, [open, plugin?.pluginId, teamId, form]);

  const handleCancel = useCallback(() => {
    form.resetFields();
    setSelectedFile(null);
    setCurrentFileName(undefined);
    setUploadStatus({ uploading: false, progress: 0, error: "" });
    onCancel();
  }, [form, onCancel]);

  const handleSubmit = useCallback(async () => {
    if (!teamId) return;
    try {
      const values = await form.validateFields();
      setLoading(true);

      let fileId: number | undefined;
      let fileName: string | undefined;

      if (selectedFile) {
        setUploadStatus({ uploading: true, progress: 10, error: "" });
        const uploadResult = await uploadTeamOpenApiFile(selectedFile, teamId);
        if (!uploadResult?.fileId) throw new Error("文件上传失败");
        fileId = uploadResult.fileId;
        fileName = selectedFile.name;
        setUploadStatus({ uploading: true, progress: 80, error: "" });
      }

      const client = GetApiClient();
      await client.api.team.plugin.update_openapi.post({
        teamId,
        pluginId: plugin?.pluginId,
        name: values.name,
        title: values.title,
        serverUrl: values.serverUrl,
        description: values.description,
        header: processKeyValueArray(values.header),
        fileId,
        fileName,
        classifyId: values.classifyId,
      });

      setUploadStatus({ uploading: false, progress: 100, error: "" });
      messageApi.success("OpenAPI 插件更新成功");
      handleCancel();
      onSuccess();
    } catch (error) {
      console.error("更新OpenAPI插件失败:", error);
      setUploadStatus({ uploading: false, progress: 0, error: "更新失败" });
      proxyFormRequestError(error, messageApi, form, "更新OpenAPI插件失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, form, selectedFile, plugin, messageApi, handleCancel, onSuccess]);

  return (
    <>
      {contextHolder}
      <Modal
        title="编辑 OpenAPI 插件"
        open={open}
        onOk={handleSubmit}
        onCancel={handleCancel}
        width={720}
        okText="确定"
        cancelText="取消"
        destroyOnClose
        confirmLoading={loading}
        maskClosable={false}
      >
        <Spin spinning={detailLoading} tip="正在获取插件详情...">
          <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
            <BaseFormFields classifyList={classifyList} showServerUrl serverUrlLabel="服务器地址" />
            <Form.Item label="OpenAPI 文件（可选，重新上传将覆盖原文件）">
              {currentFileName && (
                <Alert message={`当前文件: ${currentFileName}`} type="info" showIcon style={{ marginBottom: 8 }} />
              )}
              <Upload.Dragger
                accept=".json,.yaml,.yml"
                maxCount={1}
                beforeUpload={(file) => { setSelectedFile(file); return false; }}
                fileList={selectedFile ? [{ uid: "1", name: selectedFile.name, status: "done" as const, size: selectedFile.size }] : []}
                onRemove={() => { setSelectedFile(null); setUploadStatus({ uploading: false, progress: 0, error: "" }); }}
                showUploadList={{ showRemoveIcon: true, showPreviewIcon: false }}
                disabled={loading}
              >
                <p className="ant-upload-drag-icon"><UploadOutlined style={{ fontSize: 36, color: "var(--color-primary)" }} /></p>
                <p className="ant-upload-text">点击或拖拽文件上传</p>
                <p className="ant-upload-hint">支持 JSON/YAML 格式</p>
              </Upload.Dragger>
              {uploadStatus.uploading && <Progress percent={uploadStatus.progress} size="small" style={{ marginTop: 8 }} />}
              {uploadStatus.error && <Alert type="error" message={uploadStatus.error} showIcon style={{ marginTop: 8 }} />}
            </Form.Item>
            <KeyValueConfig name="header" title="Header" />
          </Form>
        </Spin>
      </Modal>
    </>
  );
}

interface FunctionListModalProps {
  open: boolean;
  teamId: number | undefined;
  plugin: PluginBaseInfoItem | null;
  onCancel: () => void;
}

export function TeamFunctionListModal({ open, teamId, plugin, onCancel }: FunctionListModalProps) {
  const [functionList, setFunctionList] = useState<PluginFunctionItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open && plugin?.pluginId && teamId) {
      const fetchFunctionList = async () => {
        setLoading(true);
        try {
          const client = GetApiClient();
          const response = await client.api.team.plugin.function_list.post({ teamId, pluginId: plugin.pluginId });
          setFunctionList(response?.items || []);
        } catch (error) {
          console.error("获取函数列表失败:", error);
          setFunctionList([]);
        } finally {
          setLoading(false);
        }
      };
      fetchFunctionList();
    }
  }, [open, plugin?.pluginId, teamId]);

  const handleCancel = () => {
    setFunctionList([]);
    onCancel();
  };

  const columns = [
    {
      title: "函数名称",
      dataIndex: "name",
      key: "name",
      render: (name: string) => <Typography.Text strong>{name || "-"}</Typography.Text>,
    },
    {
      title: "API路径",
      dataIndex: "path",
      key: "path",
      render: (path: string) => (
        <Typography.Text type="secondary" style={{ fontFamily: "monospace", fontSize: 12 }}>{path || "-"}</Typography.Text>
      ),
    },
    {
      title: "描述",
      dataIndex: "summary",
      key: "summary",
      render: (summary: string) => <Typography.Text type="secondary">{summary || "-"}</Typography.Text>,
    },
  ];

  return (
    <Modal
      title={`${plugin?.pluginName || "插件"} - 函数列表`}
      open={open}
      onCancel={handleCancel}
      width={800}
      footer={null}
      destroyOnClose
      maskClosable={false}
    >
      {plugin?.type === PluginTypeObject.Mcp && (
        <Alert message="MCP 插件会在使用时动态更新，本列表仅供参考" type="info" showIcon style={{ marginBottom: 16 }} />
      )}
      <Spin spinning={loading} tip="正在获取函数列表...">
        <Table
          columns={columns}
          dataSource={functionList}
          rowKey="functionId"
          loading={loading}
          pagination={false}
          scroll={{ y: 400 }}
          locale={{ emptyText: <Empty description="暂无函数数据" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
        />
      </Spin>
    </Modal>
  );
}
