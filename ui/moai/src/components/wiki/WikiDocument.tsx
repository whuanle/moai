import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Card,
  Input,
  Button,
  Upload,
  Modal,
  message,
  Space,
  Table,
  Progress,
  Tag,
  Tooltip,
  Popconfirm,
  Empty,
  List,
  Select,
  Radio,
  Checkbox,
} from "antd";
import type { TableProps } from "antd";
import {
  UploadOutlined,
  CheckCircleOutlined,
  DeleteOutlined,
  FileTextOutlined,
  LockOutlined,
  PlusOutlined,
  CheckOutlined,
  CloseOutlined,
  DownloadOutlined,
  ThunderboltOutlined,
  SearchOutlined,
  CloseCircleOutlined,
  EditOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useNavigate } from "react-router";
import {
  QueryWikiDocumentListCommand,
  QueryWikiDocumentListItem,
  ComplateUploadWikiDocumentCommand,
  DeleteWikiDocumentCommand,
  UpdateWikiDocumentFileNameCommand,
  DownloadWikiDocumentCommand,
  WikiBatchProcessDocumentCommand,
  WikiPluginAutoProcessConfig,
  PreUploadWikiDocumentCommandResponse,
  KeyValueBool,
} from "../../apiClient/models";
import { FileSizeHelper } from "../../helper/FileSizeHelper";
import { FileTypeHelper } from "../../helper/FileTypeHelper";
import { formatDateTime, parseJsonDateTime } from "../../helper/DateTimeHelper";
import { MoAIClient } from "../../apiClient/moAIClient";
import { GetFileMd5 } from "../../helper/Md5Helper";
import { proxyRequestError } from "../../helper/RequestError";
import StartTaskConfigModal from "./plugins/common/StartTaskConfigModal";
import "./WikiDocument.css";

// ============ 类型定义 ============
interface DocumentItem {
  documentId: number;
  fileName: string;
  fileSize: string;
  fileSizeRaw: number;
  contentType: string;
  createTime: string;
  createUserName: string;
  updateTime: string;
  updateUserName: string;
  embedding: boolean;
  chunkCount: number;
  metadataCount: number;
}

interface UploadStatus {
  file: File;
  status: "waiting" | "uploading" | "success" | "error";
  progress: number;
  message?: string;
}

interface TableSortState {
  field: string | null;
  order: "ascend" | "descend" | null;
}

// ============ 常量 ============
const FILE_FORMAT_OPTIONS = [
  {
    label: "文本类",
    options: [
      { value: ".txt", label: ".txt" },
      { value: ".md", label: ".md" },
    ],
  },
  {
    label: "标记语言类",
    options: [
      { value: ".htm", label: ".htm" },
      { value: ".html", label: ".html" },
      { value: ".xml", label: ".xml" },
    ],
  },
  {
    label: "文档类",
    options: [
      { value: ".pdf", label: ".pdf" },
      { value: ".doc", label: ".doc" },
      { value: ".docx", label: ".docx" },
      { value: ".ppt", label: ".ppt" },
      { value: ".pptx", label: ".pptx" },
      { value: ".xls", label: ".xls" },
      { value: ".xlsx", label: ".xlsx" },
    ],
  },
  {
    label: "数据类",
    options: [
      { value: ".json", label: ".json" },
      { value: ".csv", label: ".csv" },
    ],
  },
];

// ============ 工具函数 ============
const getErrorMessage = (error: unknown, defaultMessage: string): string => {
  const err = error as Record<string, unknown>;
  if (err?.detail && typeof err.detail === "string") return (err.detail as string).trim() || defaultMessage;
  const errError = err?.error as Record<string, unknown> | undefined;
  if (errError?.detail) return (errError.detail as string).trim() || defaultMessage;
  const errResponse = err?.response as Record<string, unknown> | undefined;
  if (errResponse?.detail) return (errResponse.detail as string).trim() || defaultMessage;
  if (error instanceof Error) return error.message || defaultMessage;
  if (typeof error === "string") return error;
  return defaultMessage;
};

const buildOrderByFields = (sortState: TableSortState): KeyValueBool[] | undefined => {
  if (!sortState.field || !sortState.order) return undefined;
  return [{ key: sortState.field, value: sortState.order === "ascend" }];
};

// 上传私有文件
const uploadPrivateFile = async (client: MoAIClient, file: File, wikiId: number): Promise<PreUploadWikiDocumentCommandResponse> => {
  const md5 = await GetFileMd5(file);
  const preUploadResponse = await client.api.wiki.document.preupload_document.post({
    wikiId,
    contentType: FileTypeHelper.getFileType(file),
    fileName: file.name,
    mD5: md5,
    fileSize: file.size,
  });

  if (!preUploadResponse) throw new Error("获取预签名URL失败");
  if (preUploadResponse.isExist) return preUploadResponse;
  if (!preUploadResponse.uploadUrl) throw new Error("获取预签名URL失败");

  const uploadResponse = await fetch(preUploadResponse.uploadUrl, {
    method: "PUT",
    body: file,
    headers: {
      "Content-Type": FileTypeHelper.getFileType(file),
      "x-amz-meta-max-file-size": file.size.toString(),
      Authorization: `Bearer ${localStorage.getItem("userinfo.accessToken")}`,
    },
  });

  if (uploadResponse.status !== 200) throw new Error(uploadResponse.statusText);

  await client.api.storage.complate_url.post({
    fileId: preUploadResponse.fileId,
    isSuccess: true,
  });

  return preUploadResponse;
};

// ============ 主组件 ============
export default function WikiDocument() {
  const { id } = useParams();
  const wikiId = parseInt(id || "0");
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();

  // 列表状态
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 20, total: 0 });
  const [searchText, setSearchText] = useState("");
  const [filterMode, setFilterMode] = useState<"include" | "exclude">("exclude");
  const [selectedFormats, setSelectedFormats] = useState<string[]>([".html"]);
  const [sortState, setSortState] = useState<TableSortState>({ field: null, order: null });
  // 向量化筛选：null=不筛选, true=已向量化, false=未向量化
  const [embeddingFilter, setEmbeddingFilter] = useState<boolean | null>(null);

  // 编辑状态
  const [editingDocumentId, setEditingDocumentId] = useState<number | null>(null);
  const [editingFileName, setEditingFileName] = useState("");

  // 多选状态
  const [selectedRowKeys, setSelectedRowKeys] = useState<number[]>([]);

  // 上传状态
  const [uploadModalVisible, setUploadModalVisible] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [uploadStatuses, setUploadStatuses] = useState<UploadStatus[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  // 批量处理状态
  const [batchProcessModalVisible, setBatchProcessModalVisible] = useState(false);

  // ============ 数据获取 ============
  const fetchDocuments = useCallback(
    async (page = 1, pageSize = 20, search = "", sort = sortState, embedding = embeddingFilter) => {
      if (!wikiId) return;
      setLoading(true);
      try {
        const client = GetApiClient();
        const requestBody: QueryWikiDocumentListCommand = {
          wikiId,
          pageNo: page,
          pageSize,
          query: search || undefined,
          orderByFields: buildOrderByFields(sort),
          isEmbedding: embedding,
        };

        if (selectedFormats.length > 0) {
          if (filterMode === "include") requestBody.includeFileTypes = selectedFormats;
          else requestBody.excludeFileTypes = selectedFormats;
        }

        const response = await client.api.wiki.document.list.post(requestBody);
        if (response?.items) {
          setDocuments(
            response.items.map((item: QueryWikiDocumentListItem) => ({
              documentId: item.documentId!,
              fileName: item.fileName || "",
              fileSize: FileSizeHelper.formatFileSize(item.fileSize || 0),
              fileSizeRaw: item.fileSize || 0,
              contentType: item.contentType || "",
              createTime: parseJsonDateTime(item.createTime || "")?.toISOString() || "",
              createUserName: item.createUserName || "",
              updateTime: parseJsonDateTime(item.updateTime || "")?.toISOString() || "",
              updateUserName: item.updateUserName || "",
              embedding: item.embedding || false,
              chunkCount: item.chunkCount || 0,
              metadataCount: item.metedataCount || 0,
            }))
          );
          setPagination({ current: page, pageSize, total: response.total || 0 });
        }
      } catch (error) {
        proxyRequestError(error, messageApi, "获取文档列表失败");
      } finally {
        setLoading(false);
      }
    },
    [wikiId, filterMode, selectedFormats, sortState, embeddingFilter, messageApi]
  );

  useEffect(() => {
    if (wikiId) fetchDocuments();
  }, [wikiId]);

  // ============ 事件处理 ============
  const handleSearch = useCallback(() => {
    fetchDocuments(1, pagination.pageSize, searchText);
  }, [fetchDocuments, pagination.pageSize, searchText]);

  const handleRefresh = useCallback(() => {
    fetchDocuments(pagination.current, pagination.pageSize, searchText, sortState, embeddingFilter);
  }, [fetchDocuments, pagination.current, pagination.pageSize, searchText, sortState, embeddingFilter]);

  const handleTableChange: TableProps<DocumentItem>["onChange"] = useCallback(
    (newPagination: any, _filters: any, sorter: any) => {
      let newSortState = sortState;
      if (!Array.isArray(sorter) && sorter.field && sorter.order) {
        newSortState = { field: sorter.field as string, order: sorter.order };
        setSortState(newSortState);
      } else if (!Array.isArray(sorter) && !sorter.order) {
        newSortState = { field: null, order: null };
        setSortState(newSortState);
      }
      fetchDocuments(newPagination.current || 1, newPagination.pageSize || 20, searchText, newSortState, embeddingFilter);
    },
    [fetchDocuments, searchText, sortState, embeddingFilter]
  );

  const handleDelete = useCallback(
    async (documentIds: number | number[]) => {
      try {
        const client = GetApiClient();
        const ids = Array.isArray(documentIds) ? documentIds : [documentIds];
        await client.api.wiki.document.delete_document.post({ wikiId, documentIds: ids } as DeleteWikiDocumentCommand);
        messageApi.success(ids.length > 1 ? `成功删除 ${ids.length} 个文档` : "删除成功");
        fetchDocuments(pagination.current, pagination.pageSize, searchText);
      } catch (error) {
        proxyRequestError(error, messageApi, "删除失败");
      }
    },
    [wikiId, pagination, searchText, fetchDocuments, messageApi]
  );

  const handleDownload = useCallback(
    async (documentId: number, fileName: string) => {
      try {
        const client = GetApiClient();
        const response = await client.api.wiki.document.download_document.post({ wikiId, documentId } as DownloadWikiDocumentCommand);
        if (response?.value) {
          const link = document.createElement("a");
          link.href = response.value;
          link.download = fileName;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          messageApi.success("下载已开始");
        } else {
          messageApi.error("获取下载地址失败");
        }
      } catch (error) {
        proxyRequestError(error, messageApi, "下载失败");
      }
    },
    [wikiId, messageApi]
  );

  const handleUpdateFileName = useCallback(
    async (documentId: number) => {
      if (!editingFileName.trim()) {
        setEditingDocumentId(null);
        return;
      }
      const original = documents.find((d) => d.documentId === documentId);
      if (original?.fileName === editingFileName.trim()) {
        setEditingDocumentId(null);
        return;
      }
      try {
        const client = GetApiClient();
        await client.api.wiki.document.update_document_file_name.post({
          wikiId,
          documentId,
          fileName: editingFileName.trim(),
        } as UpdateWikiDocumentFileNameCommand);
        messageApi.success("文档名称更新成功");
        setEditingDocumentId(null);
        setEditingFileName("");
        fetchDocuments(pagination.current, pagination.pageSize, searchText);
      } catch (error) {
        proxyRequestError(error, messageApi, "文档名称更新失败");
      }
    },
    [wikiId, editingFileName, documents, pagination, searchText, fetchDocuments, messageApi]
  );

  // ============ 上传处理 ============
  const handleFileSelect = useCallback(
    (file: File) => {
      if (!FileTypeHelper.isWikiDocumentSupported(file)) {
        messageApi.error(`${file.name} 不是支持的文档类型`);
        return false;
      }
      setSelectedFiles((prev) => [...prev, file]);
      return false;
    },
    [messageApi]
  );

  const handleUpload = useCallback(async () => {
    if (selectedFiles.length === 0) return;
    setIsUploading(true);
    const statuses: UploadStatus[] = selectedFiles.map((file) => ({ file, status: "waiting", progress: 0 }));
    setUploadStatuses(statuses);

    let successCount = 0;
    for (let i = 0; i < selectedFiles.length; i++) {
      const file = selectedFiles[i];
      setUploadStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: "uploading", progress: 50 } : s)));
      try {
        const client = GetApiClient();
        const result = await uploadPrivateFile(client, file, wikiId);
        if (!result?.fileId) throw new Error("文件上传失败");
        await client.api.wiki.document.complete_upload_document.post({
          wikiId,
          fileId: result.fileId,
          fileName: file.name,
          isSuccess: true,
        } as ComplateUploadWikiDocumentCommand);
        setUploadStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: "success", progress: 100 } : s)));
        successCount++;
      } catch (error) {
        setUploadStatuses((prev) =>
          prev.map((s, idx) => (idx === i ? { ...s, status: "error", progress: 0, message: getErrorMessage(error, "上传失败") } : s))
        );
      }
    }

    setIsUploading(false);
    if (successCount > 0) {
      messageApi.success(`${successCount} 个文件上传成功`);
      fetchDocuments(pagination.current, pagination.pageSize, searchText);
    }
    // 清理成功的文件
    setSelectedFiles((prev) => prev.filter((_, i) => uploadStatuses[i]?.status !== "success"));
  }, [selectedFiles, wikiId, pagination, searchText, fetchDocuments, messageApi, uploadStatuses]);

  const handleCloseUploadModal = useCallback(() => {
    setUploadModalVisible(false);
    setSelectedFiles([]);
    setUploadStatuses([]);
  }, []);

  // ============ 批量处理 ============
  const handleBatchProcess = useCallback(
    async (_: boolean, config: WikiPluginAutoProcessConfig | null) => {
      if (selectedRowKeys.length === 0) return;
      try {
        const client = GetApiClient();
        await client.api.wiki.batch.create.post({
          wikiId,
          documentIds: selectedRowKeys,
          partion: config?.partion || null,
          aiPartion: config?.aiPartion || null,
          preprocessStrategyType: config?.preprocessStrategyType || null,
          preprocessStrategyAiModel: config?.preprocessStrategyAiModel || null,
          isEmbedding: config?.isEmbedding || false,
          isEmbedSourceText: config?.isEmbedSourceText || false,
          threadCount: config?.threadCount || null,
        } as WikiBatchProcessDocumentCommand);
        messageApi.success(`已提交 ${selectedRowKeys.length} 个文档的处理任务`);
        setBatchProcessModalVisible(false);
        setSelectedRowKeys([]);
      } catch (error) {
        proxyRequestError(error, messageApi, "批量处理文档失败");
      }
    },
    [wikiId, selectedRowKeys, messageApi]
  );

  // ============ 表格列配置 ============
  const columns = useMemo(
    () => [
      {
        title: "文件名称",
        dataIndex: "fileName",
        key: "fileName",
        width: 240,
        sorter: true,
        sortOrder: sortState.field === "fileName" ? sortState.order : null,
        render: (text: string, record: DocumentItem) => {
          if (editingDocumentId === record.documentId) {
            return (
              <Input
                value={editingFileName}
                onChange={(e) => setEditingFileName(e.target.value)}
                onPressEnter={() => handleUpdateFileName(record.documentId)}
                onKeyDown={(e) => e.key === "Escape" && setEditingDocumentId(null)}
                autoFocus
                suffix={
                  <Space size={0}>
                    <Button type="text" size="small" icon={<CheckOutlined />} onClick={() => handleUpdateFileName(record.documentId)} />
                    <Button type="text" size="small" icon={<CloseOutlined />} onClick={() => setEditingDocumentId(null)} />
                  </Space>
                }
              />
            );
          }
          return (
            <Space>
              <FileTextOutlined style={{ color: "var(--color-primary)" }} />
              <Tooltip title="双击编辑">
                <span
                  className="wiki-doc-filename"
                  onDoubleClick={() => {
                    setEditingDocumentId(record.documentId);
                    setEditingFileName(text);
                  }}
                >
                  {text}
                </span>
              </Tooltip>
            </Space>
          );
        },
      },
      {
        title: "文件大小",
        dataIndex: "fileSize",
        key: "fileSize",
        width: 100,
        sorter: true,
        sortOrder: sortState.field === "fileSize" ? sortState.order : null,
      },
      {
        title: "文件类型",
        dataIndex: "contentType",
        key: "contentType",
        width: 120,
        render: (text: string) => <Tag>{text || "-"}</Tag>,
      },
      {
        title: "上传时间",
        dataIndex: "createTime",
        key: "createTime",
        width: 160,
        sorter: true,
        sortOrder: sortState.field === "createTime" ? sortState.order : null,
        render: (text: string) => formatDateTime(text),
      },
      {
        title: "上传人",
        dataIndex: "createUserName",
        key: "createUserName",
        width: 160
      },
      {
        title: "向量化",
        dataIndex: "embedding",
        key: "embedding",
        width: 100,
        render: (embedding: boolean) => (
          <Tag color={embedding ? "green" : "orange"} icon={embedding ? <CheckCircleOutlined /> : <LockOutlined />}>
            {embedding ? "已完成" : "未处理"}
          </Tag>
        ),
      },
      {
        title: "切片数",
        dataIndex: "chunkCount",
        key: "chunkCount",
        width: 80,
        align: "center" as const,
      },
      {
        title: "元数据",
        dataIndex: "metadataCount",
        key: "metadataCount",
        width: 80,
        align: "center" as const,
      },
      {
        title: "操作",
        key: "action",
        width: 250,
        fixed: "right" as const,
        render: (_: unknown, record: DocumentItem) => (
          <Space size={4}>
            <Button type="link" size="small" onClick={() => navigate(`/app/wiki/${wikiId}/document/${record.documentId}/embedding`)} icon={<EditOutlined />} >
              向量化
            </Button>
            <Button type="text" size="small" icon={<DownloadOutlined />} onClick={() => handleDownload(record.documentId, record.fileName)}>
              下载
            </Button>
            <Popconfirm
              title="确定删除此文档？"
              okText="确认"
              cancelText="取消"
              onConfirm={() => handleDelete(record.documentId)}
            >
              <Button type="text" size="small" danger icon={<DeleteOutlined />}>
                删除
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    [sortState, editingDocumentId, editingFileName, wikiId, navigate, handleDownload, handleDelete, handleUpdateFileName]
  );

  return (
    <>
      {contextHolder}
      <div className="wiki-document-page">
        {/* 工具栏 */}
        <div className="moai-toolbar">
          <div className="moai-toolbar-left">
            <Input
              placeholder="搜索文档名称"
              allowClear
              prefix={<SearchOutlined style={{ color: "#bfbfbf" }} />}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onPressEnter={handleSearch}
              style={{ width: 240 }}
            />
            <Radio.Group value={filterMode} onChange={(e) => { setFilterMode(e.target.value); setSelectedFormats([]); }} buttonStyle="solid" size="small">
              <Radio.Button value="include">包含</Radio.Button>
              <Radio.Button value="exclude">排除</Radio.Button>
            </Radio.Group>
            <Select
              mode="multiple"
              placeholder={`${filterMode === "include" ? "包含" : "排除"}格式`}
              value={selectedFormats}
              onChange={setSelectedFormats}
              options={FILE_FORMAT_OPTIONS}
              style={{ minWidth: 200 }}
              maxTagCount={2}
              allowClear
            />
            <Button icon={<SearchOutlined />} onClick={handleRefresh} loading={loading}>
              查找
            </Button>
            <Checkbox
              indeterminate={embeddingFilter === false}
              checked={embeddingFilter === true}
              onChange={(e) => {
                // 三态切换：null -> true -> false -> null
                if (embeddingFilter === null) setEmbeddingFilter(true);
                else if (embeddingFilter === true) setEmbeddingFilter(false);
                else setEmbeddingFilter(null);
              }}
            >
              {embeddingFilter === null ? "向量化" : embeddingFilter ? "已向量化" : "未向量化"}
            </Checkbox>
          </div>
          <div className="moai-toolbar-right">
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setUploadModalVisible(true)}>
              上传文档
            </Button>
            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              onClick={() => setBatchProcessModalVisible(true)}
              disabled={selectedRowKeys.length === 0}
            >
              一键处理 {selectedRowKeys.length > 0 && `(${selectedRowKeys.length})`}
            </Button>
            <Popconfirm
              title={`确定删除选中的 ${selectedRowKeys.length} 个文档？`}
              okText="确认"
              cancelText="取消"
              onConfirm={() => { handleDelete(selectedRowKeys); setSelectedRowKeys([]); }}
              disabled={selectedRowKeys.length === 0}
            >
              <Button danger icon={<DeleteOutlined />} disabled={selectedRowKeys.length === 0}>
                批量删除 {selectedRowKeys.length > 0 && `(${selectedRowKeys.length})`}
              </Button>
            </Popconfirm>
          </div>
        </div>

        {/* 表格 */}
        <Card className="wiki-document-table-card">
          <Table
            columns={columns}
            dataSource={documents}
            rowKey="documentId"
            rowSelection={{ selectedRowKeys, onChange: (keys) => setSelectedRowKeys(keys as number[]) }}
            pagination={{
              ...pagination,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
              pageSizeOptions: ["20", "50", "100"],
            }}
            loading={loading}
            onChange={handleTableChange}
            scroll={{ x: 1100 }}
            locale={{ emptyText: <Empty description="暂无文档" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
          />
        </Card>
      </div>

      {/* 上传模态窗口 */}
      <Modal
        title="上传文档"
        open={uploadModalVisible}
        onCancel={handleCloseUploadModal}
        maskClosable={false}
        width={560}
        footer={[
          <Button key="cancel" onClick={handleCloseUploadModal}>取消</Button>,
          <Button key="upload" type="primary" onClick={handleUpload} loading={isUploading} disabled={selectedFiles.length === 0} icon={<UploadOutlined />}>
            开始上传 ({selectedFiles.length})
          </Button>,
        ]}
      >
        <Upload.Dragger multiple showUploadList={false} beforeUpload={handleFileSelect} accept=".pdf,.doc,.docx,.txt,.md">
          <p className="ant-upload-drag-icon"><UploadOutlined /></p>
          <p className="ant-upload-text">点击或拖拽文件到此区域</p>
          <p className="ant-upload-hint">支持 PDF、Word、TXT、Markdown 格式</p>
        </Upload.Dragger>

        {selectedFiles.length > 0 && (
          <List
            size="small"
            style={{ marginTop: 16, maxHeight: 200, overflow: "auto" }}
            dataSource={selectedFiles}
            renderItem={(file, index) => {
              const status = uploadStatuses[index];
              return (
                <List.Item
                  actions={[
                    <Button key="del" type="text" size="small" danger icon={<DeleteOutlined />} onClick={() => setSelectedFiles((prev) => prev.filter((_, i) => i !== index))} />,
                  ]}
                >
                  <List.Item.Meta
                    avatar={status?.status === "error" ? <CloseCircleOutlined style={{ color: "#ff4d4f" }} /> : <FileTextOutlined />}
                    title={file.name}
                    description={
                      status?.status === "uploading" ? (
                        <Progress percent={status.progress} size="small" />
                      ) : status?.status === "error" ? (
                        <span style={{ color: "#ff4d4f" }}>{status.message}</span>
                      ) : status?.status === "success" ? (
                        <span style={{ color: "#52c41a" }}>上传成功</span>
                      ) : (
                        FileSizeHelper.formatFileSize(file.size)
                      )
                    }
                  />
                </List.Item>
              );
            }}
          />
        )}
      </Modal>

      {/* 一键处理配置模态窗口 */}
      <StartTaskConfigModal
        open={batchProcessModalVisible}
        onCancel={() => setBatchProcessModalVisible(false)}
        onConfirm={handleBatchProcess}
        wikiId={wikiId}
      />
    </>
  );
}
