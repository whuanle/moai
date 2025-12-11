import { useState, useEffect, useCallback } from "react";
import {
  Card,
  Input,
  Button,
  Upload,
  Modal,
  message,
  Space,
  Typography,
  Row,
  Col,
  Table,
  Progress,
  Tag,
  Tooltip,
  Popconfirm,
  Empty,
  List,
  Divider,
  Alert,
  Spin,
  Select,
  Radio,
} from "antd";
import {
  UploadOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  DeleteOutlined,
  FileTextOutlined,
  LockOutlined,
  ReloadOutlined,
  PlusOutlined,
  CheckOutlined,
  CloseOutlined,
  DownloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useNavigate } from "react-router";
import {
  QueryWikiDocumentListCommand,
  QueryWikiDocumentListItem,
  ComplateUploadWikiDocumentCommand,
  DeleteWikiDocumentCommand,
  PreloadWikiDocumentResponse,
  UpdateWikiDocumentFileNameCommand,
  DownloadWikiDocumentCommand,
  BusinessValidationResult,
} from "../../apiClient/models";
import { FileSizeHelper } from "../../helper/FileSizeHelper";
import { FileTypeHelper } from "../../helper/FileTypeHelper";
import {
  formatDateTime,
  parseJsonDateTime,
} from "../../helper/DateTimeHelper";
import { MoAIClient } from "../..//apiClient/moAIClient";
import { GetFileMd5 } from "../../helper/Md5Helper";

const { Text, Title } = Typography;
const { Search } = Input;

// 辅助函数：检查错误是否为 BusinessValidationResult 并提取错误信息
const getErrorMessage = (error: any, defaultMessage: string): string => {
  // 优先检查 BusinessValidationResult 的 detail 属性
  // 使用可选链操作符安全地访问属性
  if (error?.detail && typeof error.detail === "string") {
    const detail = error.detail.trim();
    if (detail) {
      return detail;
    }
  }
  
  // 检查错误是否被包装在某个属性中（某些 API 客户端可能会将错误包装起来）
  if (error?.error?.detail && typeof error.error.detail === "string") {
    const detail = error.error.detail.trim();
    if (detail) {
      return detail;
    }
  }
  
  if (error?.response?.detail && typeof error.response.detail === "string") {
    const detail = error.response.detail.trim();
    if (detail) {
      return detail;
    }
  }
  
  // 如果不是 BusinessValidationResult，尝试其他方式获取错误信息
  if (error instanceof Error) {
    return error.message || defaultMessage;
  }
  
  if (typeof error === "string") {
    return error;
  }
  
  return defaultMessage;
};

// 类型定义
interface DocumentItem {
  documentId: number;
  fileName: string;
  fileSize: string;
  contentType: string;
  createTime: string;
  createUserName: string;
  updateTime: string;
  updateUserName: string;
  embedding: boolean;
}

interface UploadStatus {
  file: File;
  status: "waiting" | "uploading" | "success" | "error";
  progress: number;
  message?: string;
  fileId?: number;
}

interface BatchUploadStatus {
  files: File[];
  currentIndex: number;
  uploadStatuses: UploadStatus[];
  isUploading: boolean;
}

// 文件格式选项
const FILE_FORMAT_OPTIONS = [
  {
    label: "文本类",
    options: [
      { value: ".txt", label: ".txt (Plain Text)" },
      { value: ".md", label: ".md (Markdown)" },
    ],
  },
  {
    label: "标记语言类",
    options: [
      { value: ".htm", label: ".htm (HTML)" },
      { value: ".html", label: ".html (HTML)" },
      { value: ".xhtml", label: ".xhtml (XHTML)" },
      { value: ".xml", label: ".xml (XML)" },
      { value: ".jsonld", label: ".jsonld (JSON-LD)" },
    ],
  },
  {
    label: "代码类",
    options: [
      { value: ".css", label: ".css (Cascading Style Sheets)" },
      { value: ".js", label: ".js (JavaScript)" },
      { value: ".sh", label: ".sh (Shell Script)" },
    ],
  },
  {
    label: "文档类",
    options: [
      { value: ".pdf", label: ".pdf (Portable Document Format)" },
      { value: ".rtf", label: ".rtf (Rich Text Format)" },
      { value: ".doc", label: ".doc (Microsoft Word 97-2003)" },
      { value: ".docx", label: ".docx (Microsoft Word)" },
      { value: ".ppt", label: ".ppt (Microsoft PowerPoint 97-2003)" },
      { value: ".pptx", label: ".pptx (Microsoft PowerPoint)" },
      { value: ".xls", label: ".xls (Microsoft Excel 97-2003)" },
      { value: ".xlsx", label: ".xlsx (Microsoft Excel)" },
    ],
  },
  {
    label: "开放文档类",
    options: [
      { value: ".odt", label: ".odt (OpenDocument Text)" },
      { value: ".ods", label: ".ods (OpenDocument Spreadsheet)" },
      { value: ".odp", label: ".odp (OpenDocument Presentation)" },
      { value: ".epub", label: ".epub (EPUB - Electronic Publication)" },
    ],
  },
  {
    label: "数据类",
    options: [
      { value: ".url", label: ".url (URL)" },
      { value: ".text_embedding", label: ".text_embedding (Text Embedding)" },
      { value: ".json", label: ".json (JSON)" },
      { value: ".csv", label: ".csv (Comma-Separated Values)" },
    ],
  }
];

// 自定义 Hook - 文档列表管理
const useDocumentList = (wikiId: number) => {
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: 20,
    total: 0,
  });
  const [searchText, setSearchText] = useState("");
  const [filterMode, setFilterMode] = useState<"include" | "exclude">("exclude");
  const [selectedFormats, setSelectedFormats] = useState<string[]>([".html"]);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchDocuments = useCallback(async (page = 1, pageSize = 20, search = "") => {
    if (!wikiId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const client = GetApiClient();
      const requestBody: QueryWikiDocumentListCommand = {
        wikiId,
        pageNo: page,
        pageSize,
        query: search,
      };

      // 根据过滤模式设置文件类型参数
      if (selectedFormats.length > 0) {
        if (filterMode === "include") {
          requestBody.includeFileTypes = selectedFormats;
        } else {
          requestBody.excludeFileTypes = selectedFormats;
        }
      }

      const response = await client.api.wiki.document.list.post(requestBody);

      if (response?.items) {
        const formattedDocuments: DocumentItem[] = response.items.map((item: QueryWikiDocumentListItem) => ({
          documentId: item.documentId!,
          fileName: item.fileName || "",
          fileSize: FileSizeHelper.formatFileSize(item.fileSize || 0),
          contentType: item.contentType || "",
          createTime: parseJsonDateTime(item.createTime || "")?.toISOString() || "",
          createUserName: item.createUserName || "",
          updateTime: parseJsonDateTime(item.updateTime || "")?.toISOString() || "",
          updateUserName: item.updateUserName || "",
          embedding: item.embedding || false,
        }));

        setDocuments(formattedDocuments);
        setPagination({
          current: page,
          pageSize,
          total: response.total || 0,
        });
      }
    } catch (error) {
      const errorMessage = getErrorMessage(error, "获取文档列表失败");
      messageApi.error(errorMessage);
      console.error("Fetch documents error:", error);
    } finally {
      setLoading(false);
    }
  }, [wikiId, messageApi, filterMode, selectedFormats]);

  const deleteDocument = useCallback(async (documentId: number) => {
    try {
      const client = GetApiClient();
      const deleteCommand: DeleteWikiDocumentCommand = {
        wikiId,
        documentId,
      };
      await client.api.wiki.document.delete_document.post(deleteCommand);
      messageApi.success("删除成功");
      fetchDocuments(pagination.current, pagination.pageSize, searchText);
    } catch (error) {
      const errorMessage = getErrorMessage(error, "删除失败");
      messageApi.error(errorMessage);
      console.error("Delete document error:", error);
    }
  }, [wikiId, pagination.current, pagination.pageSize, searchText, fetchDocuments, messageApi]);

  const updateDocumentFileName = useCallback(async (documentId: number, fileName: string) => {
    try {
      const client = GetApiClient();
      const updateCommand: UpdateWikiDocumentFileNameCommand = {
        wikiId,
        documentId,
        fileName,
      };
      await client.api.wiki.document.update_document_file_name.post(updateCommand);
      messageApi.success("文档名称更新成功");
      fetchDocuments(pagination.current, pagination.pageSize, searchText);
    } catch (error) {
      const errorMessage = getErrorMessage(error, "文档名称更新失败");
      messageApi.error(errorMessage);
      console.error("Update document file name error:", error);
      throw error; // 重新抛出错误，以便在编辑组件中处理
    }
  }, [wikiId, pagination.current, pagination.pageSize, searchText, fetchDocuments, messageApi]);

  return {
    documents,
    loading,
    pagination,
    searchText,
    filterMode,
    selectedFormats,
    contextHolder,
    fetchDocuments,
    deleteDocument,
    updateDocumentFileName,
    setSearchText,
    setFilterMode,
    setSelectedFormats,
  };
};

// 自定义 Hook - 文件上传管理
const useFileUpload = (wikiId: number, onUploadSuccess: () => void) => {
  const [isUploadModalVisible, setIsUploadModalVisible] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [batchUploadStatus, setBatchUploadStatus] = useState<BatchUploadStatus | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  const handleFileSelect = useCallback((file: File) => {
    // 检查是否为支持的文档类型
    if (!FileTypeHelper.isWikiDocumentSupported(file)) {
      messageApi.error(`${file.name} 不是支持的文档类型，仅支持 PDF、Word、TXT、Markdown 格式`);
      return false;
    }
    
    setSelectedFiles(prev => [...prev, file]);
    return false;
  }, [messageApi]);

  const uploadSingleFile = useCallback(async (file: File, index: number) => {
    try {
      setBatchUploadStatus(prev => {
        if (!prev) return prev;
        const newStatuses = [...prev.uploadStatuses];
        newStatuses[index] = {
          file,
          status: "uploading",
          progress: 50,
        };
        return {
          ...prev,
          uploadStatuses: newStatuses,
        };
      });

      const client = GetApiClient();
      
      // 使用 UploadPrivateFile 方法上传文件
      const uploadResult = await UploadPrivateFile(client, file, wikiId);
      
      if (!uploadResult || !uploadResult.fileId) {
        throw new Error("文件上传失败");
      }

      // 完成知识库文档上传
      const completeCommand: ComplateUploadWikiDocumentCommand = {
        wikiId,
        fileId: uploadResult.fileId,
        fileName: file.name,
        isSuccess: true,
      };

      await client.api.wiki.document.complete_upload_document.post(completeCommand);

      setBatchUploadStatus(prev => {
        if (!prev) return prev;
        const newStatuses = [...prev.uploadStatuses];
        newStatuses[index] = {
          file,
          status: "success",
          progress: 100,
          fileId: uploadResult.fileId || undefined,
        };
        return {
          ...prev,
          uploadStatuses: newStatuses,
        };
      });

      return true;
    } catch (error) {
      console.error("Upload failed:", error);
      
      // 使用 getErrorMessage 处理所有类型的错误，包括 BusinessValidationResult
      const errorMessage = getErrorMessage(error, "上传失败");
      setBatchUploadStatus(prev => {
        if (!prev) return prev;
        const newStatuses = [...prev.uploadStatuses];
        newStatuses[index] = {
          file,
          status: "error",
          progress: 0,
          message: errorMessage,
        };
        return {
          ...prev,
          uploadStatuses: newStatuses,
        };
      });
      return false;
    }
  }, [wikiId]);

  const uploadAllFiles = useCallback(async () => {
    if (selectedFiles.length === 0) {
      messageApi.warning("请选择要上传的文件");
      return;
    }

    // 保存文件列表副本，避免在循环过程中因状态更新导致索引错位
    const filesToUpload = [...selectedFiles];

    // 初始化批量上传状态
    const initialStatuses: UploadStatus[] = filesToUpload.map(file => ({
      file,
      status: "waiting",
      progress: 0,
    }));

    setBatchUploadStatus({
      files: filesToUpload,
      currentIndex: 0,
      uploadStatuses: initialStatuses,
      isUploading: true,
    });

    // 逐个上传文件
    for (let i = 0; i < filesToUpload.length; i++) {
      const file = filesToUpload[i];
      const success = await uploadSingleFile(file, i);
      
      if (!success) {
        // 错误信息已经在 uploadSingleFile 中通过 batchUploadStatus 显示
        // 这里不再重复显示，避免重复提示
      }
    }

    // 上传完成，只移除成功上传的文件，保留失败的文件
    setBatchUploadStatus(prev => {
      if (!prev) return null;
      
      // 找出所有成功上传的文件
      const successfulFiles = prev.uploadStatuses
        .filter(status => status.status === "success")
        .map(status => status.file);
      
      // 从 selectedFiles 中移除成功上传的文件
      if (successfulFiles.length > 0) {
        setSelectedFiles(currentFiles => 
          currentFiles.filter(file => 
            !successfulFiles.some(successFile => 
              successFile.name === file.name && 
              successFile.size === file.size &&
              successFile.lastModified === file.lastModified
            )
          )
        );
      }
      
      // 统计上传结果
      const successCount = prev.uploadStatuses.filter(s => s.status === "success").length;
      const errorCount = prev.uploadStatuses.filter(s => s.status === "error").length;
      
      // 显示上传结果消息
      if (successCount > 0 && errorCount > 0) {
        messageApi.info(`${successCount} 个文件上传成功，${errorCount} 个文件上传失败`);
        onUploadSuccess();
      } else if (successCount > 0) {
        messageApi.success(`${successCount} 个文件上传成功`);
        onUploadSuccess();
      } else if (errorCount > 0) {
        messageApi.warning(`${errorCount} 个文件上传失败，请检查后重试`);
      }
      
      // 如果还有未完成的文件（失败或等待），保留上传状态
      const hasIncompleteFiles = prev.uploadStatuses.some(
        status => status.status !== "success"
      );
      
      if (hasIncompleteFiles) {
        // 保留上传状态，但标记上传过程已结束
        return {
          ...prev,
          isUploading: false,
        };
      }
      
      // 所有文件都已完成（成功或失败），清空上传状态
      return null;
    });
  }, [selectedFiles, uploadSingleFile, messageApi, onUploadSuccess]);

  const handleUploadModalCancel = useCallback(() => {
    setIsUploadModalVisible(false);
    setSelectedFiles([]);
    setBatchUploadStatus(null);
  }, []);

  const removeFile = useCallback((index: number) => {
    setSelectedFiles(prev => prev.filter((_, i) => i !== index));
  }, []);

  return {
    isUploadModalVisible,
    selectedFiles,
    batchUploadStatus,
    contextHolder,
    setIsUploadModalVisible,
    handleFileSelect,
    uploadAllFiles,
    handleUploadModalCancel,
    removeFile,
  };
};

// 状态文本获取函数
const getStatusText = (status: UploadStatus["status"]) => {
  switch (status) {
    case "waiting":
      return "等待上传";
    case "uploading":
      return "上传中";
    case "success":
      return "上传成功";
    case "error":
      return "上传失败";
    default:
      return "";
  }
};

// 上传私有类型的文件（知识库文档）
const UploadPrivateFile = async (
  client: MoAIClient,
  file: File,
  wikiId: number
): Promise<PreloadWikiDocumentResponse> => {
  const md5 = await GetFileMd5(file);
  const preUploadResponse = await client.api.wiki.document.preupload_document.post({
    wikiId,
    contentType: FileTypeHelper.getFileType(file),
    fileName: file.name,
    mD5: md5,
    fileSize: file.size,
  });

  if (!preUploadResponse) {
    throw new Error("获取预签名URL失败");
  }

  if (preUploadResponse.isExist === true) {
    return preUploadResponse;
  }

  if (!preUploadResponse.uploadUrl) {
    throw new Error("获取预签名URL失败");
  }

  const uploadUrl = preUploadResponse.uploadUrl;
  if (!uploadUrl) {
    throw new Error("获取预签名URL失败");
  }

  // 使用 fetch API 上传到预签名的 S3 URL
  const uploadResponse = await fetch(uploadUrl, {
    method: "PUT",
    body: file,
    headers: {
      "Content-Type": FileTypeHelper.getFileType(file),
      "x-amz-meta-max-file-size": file.size.toString(),
      "Authorization": `Bearer ${localStorage.getItem("userinfo.accessToken")}`,
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
};

// 文档表格列配置
const getTableColumns = (
  wikiId: number,
  navigate: any,
  deleteDocument: (id: number) => void,
  updateDocumentFileName: (documentId: number, fileName: string) => Promise<void>,
  editingDocumentId: number | null,
  editingFileName: string,
  handleDoubleClickEdit: (documentId: number, fileName: string) => void,
  handleConfirmEdit: (documentId: number) => void,
  handleCancelEdit: () => void,
  setEditingFileName: (value: string) => void,
  handleDownload: (documentId: number, fileName: string) => void
) => [
  {
    title: "文件名称",
    dataIndex: "fileName",
    key: "fileName",
    width: 200,
    render: (text: string, record: DocumentItem) => {
      const isEditing = editingDocumentId === record.documentId;

      if (isEditing) {
        return (
          <Space>
            <FileTextOutlined />
            <Input
              value={editingFileName}
              onChange={(e) => setEditingFileName(e.target.value)}
              onPressEnter={() => handleConfirmEdit(record.documentId)}
              onKeyDown={(e) => {
                if (e.key === "Escape") {
                  handleCancelEdit();
                }
              }}
              autoFocus
              style={{ width: 400 }}
              suffix={
                <Space size={0}>
                  <Button
                    type="text"
                    size="small"
                    icon={<CheckOutlined style={{ fontSize: 14 }} />}
                    onClick={() => handleConfirmEdit(record.documentId)}
                    style={{ padding: "0 4px", height: "auto", minWidth: "auto" }}
                  />
                  <Button
                    type="text"
                    size="small"
                    icon={<CloseOutlined style={{ fontSize: 14 }} />}
                    onClick={handleCancelEdit}
                    style={{ padding: "0 4px", height: "auto", minWidth: "auto" }}
                  />
                </Space>
              }
            />
          </Space>
        );
      }

      return (
        <Space>
          <FileTextOutlined />
          <Text
            onDoubleClick={() => handleDoubleClickEdit(record.documentId, text)}
            ellipsis={{ tooltip: text }}
            style={{ maxWidth: 150, cursor: "pointer" }}
          >
            {text}
          </Text>
        </Space>
      );
    },
  },
  {
    title: "文件大小",
    dataIndex: "fileSize",
    key: "fileSize",
    width: 120,
  },
  {
    title: "文件类型",
    dataIndex: "contentType",
    key: "contentType",
    width: 120,
  },
  {
    title: "创建时间",
    dataIndex: "createTime",
    key: "createTime",
    width: 180,
    render: (text: string) => formatDateTime(text),
  },
  {
    title: "创建人",
    dataIndex: "createUserName",
    key: "createUserName",
    width: 120,
  },
  {
    title: "更新时间",
    dataIndex: "updateTime",
    key: "updateTime",
    width: 180,
    render: (text: string) => formatDateTime(text),
  },
  {
    title: "更新人",
    dataIndex: "updateUserName",
    key: "updateUserName",
    width: 120,
  },
  {
    title: "向量化状态",
    dataIndex: "embedding",
    key: "embedding",
    width: 120,
    render: (embedding: boolean) => (
      <Tag color={embedding ? "green" : "orange"} icon={embedding ? <CheckCircleOutlined /> : <LockOutlined />}>
        {embedding ? "已向量化" : "未向量化"}
      </Tag>
    ),
  },
  {
    title: "操作",
    key: "action",
    width: 180,
    fixed: "right" as const,
    render: (_: unknown, record: DocumentItem) => (
      <Space>
        <Button
          type="link"
          size="small"
          onClick={() => navigate(`/app/wiki/${wikiId}/document/${record.documentId}/embedding`)}
        >
          向量化
        </Button>
        <Tooltip title="下载文档">
          <Button
            type="link"
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => handleDownload(record.documentId, record.fileName)}
          >
            下载
          </Button>
        </Tooltip>
        <Popconfirm
          title="删除文档"
          description="确定要删除这个文档吗？删除后无法恢复。"
          okText="确认删除"
          cancelText="取消"
          onConfirm={() => deleteDocument(record.documentId)}
        >
          <Tooltip title="删除文档">
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
      </Space>
    ),
  },
];

// 上传进度组件
const UploadProgress = ({ batchUploadStatus }: { batchUploadStatus: BatchUploadStatus | null }) => {
  if (!batchUploadStatus) return null;

  // 过滤掉已成功的文件，只显示正在上传和失败的文件
  const activeStatuses = batchUploadStatus.uploadStatuses.filter(
    status => status.status !== "success"
  );

  if (activeStatuses.length === 0) return null;

  return (
    <Card size="small" title="上传进度" style={{ marginTop: 16 }}>
      <List
        dataSource={activeStatuses}
        renderItem={(status, index) => (
          <List.Item>
            <Space direction="vertical" style={{ width: '100%' }}>
              <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                <Text ellipsis={{ tooltip: status.file.name }} style={{ maxWidth: 200 }}>
                  {status.file.name}
                </Text>
                {status.status === "error" && (
                  <CloseCircleOutlined style={{ color: "#ff4d4f" }} />
                )}
              </Space>
              <Progress
                percent={status.progress}
                status={status.status === "error" ? "exception" : undefined}
                size="small"
                showInfo={false}
              />
              <Text
                type={status.status === "error" ? "danger" : "secondary"}
                style={{ fontSize: "12px" }}
              >
                {status.message || getStatusText(status.status)}
              </Text>
            </Space>
          </List.Item>
        )}
      />
    </Card>
  );
};

// 已选择文件列表组件
const SelectedFilesList = ({ 
  selectedFiles, 
  removeFile 
}: { 
  selectedFiles: File[]; 
  removeFile: (index: number) => void;
}) => {
  if (selectedFiles.length === 0) return null;

  return (
    <Card size="small" title={`已选择 ${selectedFiles.length} 个文件`} style={{ marginTop: 16 }}>
      <List
        dataSource={selectedFiles}
        renderItem={(file, index) => (
          <List.Item
            actions={[
              <Button
                key="delete"
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                onClick={() => removeFile(index)}
              >
                删除
              </Button>
            ]}
          >
            <List.Item.Meta
              avatar={<FileTextOutlined />}
              title={file.name}
              description={`${FileSizeHelper.formatFileSize(file.size)} | ${FileTypeHelper.formatFileType(file)}`}
            />
          </List.Item>
        )}
      />
    </Card>
  );
};

// 主组件
export default function WikiDocument() {
  const { id } = useParams();
  const wikiId = parseInt(id || "0");
  const navigate = useNavigate();

  // 编辑文档名称的状态
  const [editingDocumentId, setEditingDocumentId] = useState<number | null>(null);
  const [editingFileName, setEditingFileName] = useState<string>("");

  const {
    documents,
    loading,
    pagination,
    searchText,
    filterMode,
    selectedFormats,
    contextHolder: listContextHolder,
    fetchDocuments,
    deleteDocument,
    updateDocumentFileName,
    setSearchText,
    setFilterMode,
    setSelectedFormats,
  } = useDocumentList(wikiId);

  const {
    isUploadModalVisible,
    selectedFiles,
    batchUploadStatus,
    contextHolder: uploadContextHolder,
    setIsUploadModalVisible,
    handleFileSelect,
    uploadAllFiles,
    handleUploadModalCancel,
    removeFile,
  } = useFileUpload(wikiId, () => fetchDocuments(pagination.current, pagination.pageSize, searchText));

  useEffect(() => {
    if (wikiId) {
      fetchDocuments();
    }
  }, [wikiId, fetchDocuments]);

  const handleSearch = useCallback(() => {
    fetchDocuments(1, pagination.pageSize, searchText);
  }, [fetchDocuments, pagination.pageSize, searchText]);

  const handleTableChange = useCallback((newPagination: any) => {
    fetchDocuments(newPagination.current, newPagination.pageSize, searchText);
  }, [fetchDocuments, searchText]);

  const handleUploadClick = useCallback(() => {
    setIsUploadModalVisible(true);
  }, [setIsUploadModalVisible]);

  const handleRefresh = useCallback(() => {
    fetchDocuments(pagination.current, pagination.pageSize, searchText);
  }, [fetchDocuments, pagination.current, pagination.pageSize, searchText]);

  const handleFilterModeChange = useCallback((value: "include" | "exclude") => {
    setFilterMode(value);
    setSelectedFormats([]); // 清空已选择的格式
  }, [setFilterMode, setSelectedFormats]);

  const handleFormatChange = useCallback((values: string[]) => {
    setSelectedFormats(values);
  }, [setSelectedFormats]);

  // 处理双击开始编辑
  const handleDoubleClickEdit = useCallback((documentId: number, fileName: string) => {
    setEditingDocumentId(documentId);
    setEditingFileName(fileName);
  }, []);

  // 处理确认编辑
  const handleConfirmEdit = useCallback(async (documentId: number) => {
    if (!editingFileName.trim() || editingFileName.trim() === "") {
      setEditingDocumentId(null);
      setEditingFileName("");
      return;
    }

    const originalDocument = documents.find(doc => doc.documentId === documentId);
    if (originalDocument && editingFileName.trim() === originalDocument.fileName) {
      // 名称没有变化，直接取消编辑
      setEditingDocumentId(null);
      setEditingFileName("");
      return;
    }

    try {
      await updateDocumentFileName(documentId, editingFileName.trim());
      setEditingDocumentId(null);
      setEditingFileName("");
    } catch (error) {
      // 错误已在 updateDocumentFileName 中处理
      // 如果更新失败，保持编辑状态，让用户可以重新编辑
    }
  }, [editingFileName, documents, updateDocumentFileName]);

  // 处理取消编辑
  const handleCancelEdit = useCallback(() => {
    setEditingDocumentId(null);
    setEditingFileName("");
  }, []);

  // 处理下载文档
  const handleDownload = useCallback(async (documentId: number, fileName: string) => {
    try {
      const client = GetApiClient();
      const downloadCommand: DownloadWikiDocumentCommand = {
        wikiId,
        documentId,
      };
      
      const response = await client.api.wiki.document.download_document.post(downloadCommand);
      
      if (response?.value) {
        // 创建一个临时的 a 标签来触发下载
        const link = document.createElement("a");
        link.href = response.value;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        message.success("下载已开始");
      } else {
        message.error("获取下载地址失败");
      }
    } catch (error) {
      const errorMessage = getErrorMessage(error, "下载失败");
      console.error("Download document error:", error);
      message.error(errorMessage);
    }
  }, [wikiId]);

  const columns = getTableColumns(
    wikiId,
    navigate,
    deleteDocument,
    updateDocumentFileName,
    editingDocumentId,
    editingFileName,
    handleDoubleClickEdit,
    handleConfirmEdit,
    handleCancelEdit,
    setEditingFileName,
    handleDownload
  );

  return (
    <>
      {listContextHolder}
      {uploadContextHolder}
      
      <Card>
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col span={8}>
            <Search
              placeholder="搜索文档"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onSearch={handleSearch}
              allowClear
              enterButton
            />
          </Col>
          <Col span={16}>
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
                onClick={handleUploadClick}
              >
                上传文档
              </Button>
            </Space>
          </Col>
        </Row>

        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col span={6}>
            <div style={{ marginBottom: 8 }}>
              <Text strong>格式过滤模式：</Text>
            </div>
            <Radio.Group 
              value={filterMode} 
              onChange={(e) => handleFilterModeChange(e.target.value)}
              buttonStyle="solid"
            >
              <Radio.Button value="include">包含格式</Radio.Button>
              <Radio.Button value="exclude">排除格式</Radio.Button>
            </Radio.Group>
          </Col>
          <Col span={18}>
            <div style={{ marginBottom: 8 }}>
              <Text strong>
                {filterMode === "include" ? "包含格式" : "排除格式"}：
              </Text>
            </div>
            <Select
              mode="multiple"
              placeholder={`请选择要${filterMode === "include" ? "包含" : "排除"}的文件格式`}
              value={selectedFormats}
              onChange={handleFormatChange}
              options={FILE_FORMAT_OPTIONS}
              style={{ width: "50%" }}
              showSearch
              filterOption={(input, option) =>
                (option?.label ?? "").includes(input)
              }
              allowClear
            />
          </Col>
        </Row>

        <Table
          columns={columns}
          dataSource={documents}
          rowKey="documentId"
          pagination={{
            ...pagination,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total) => `共 ${total} 条`,
            pageSizeOptions: ["20", "50", "100"],
          }}
          loading={loading}
          onChange={handleTableChange}
          scroll={{ x: 1200 }}
          locale={{
            emptyText: (
              <Empty
                description="暂无文档数据"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            ),
          }}
        />
      </Card>

      <Modal
        title="上传文档"
        open={isUploadModalVisible}
        onCancel={handleUploadModalCancel}
        width={600}
        footer={[
          <Button key="cancel" onClick={handleUploadModalCancel}>
            取消
          </Button>,
          <Button
            key="upload"
            type="primary"
            onClick={uploadAllFiles}
            loading={batchUploadStatus?.isUploading}
            disabled={selectedFiles.length === 0}
            icon={<UploadOutlined />}
          >
            开始上传 ({selectedFiles.length} 个文件)
          </Button>,
        ]}
      >
        <Upload.Dragger
          multiple={true}
          showUploadList={false}
          beforeUpload={handleFileSelect}
          accept=".pdf,.doc,.docx,.txt,.md"
        >
          <p className="ant-upload-drag-icon">
            <UploadOutlined />
          </p>
          <p className="ant-upload-text">点击或拖拽文件到此区域上传</p>
          <p className="ant-upload-hint">支持批量选择文件，支持 PDF、Word、TXT、Markdown 格式</p>
        </Upload.Dragger>

        <SelectedFilesList selectedFiles={selectedFiles} removeFile={removeFile} />
        <UploadProgress batchUploadStatus={batchUploadStatus} />
      </Modal>
    </>
  );
}
