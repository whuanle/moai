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
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useNavigate } from "react-router";
import {
  QueryWikiDocumentListCommand,
  QueryWikiDocumentListItem,
  ComplateUploadWikiDocumentCommand,
  DeleteWikiDocumentCommand,
  PreUploadFileCommandResponse,
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
      messageApi.error("获取文档列表失败");
      console.error("Fetch documents error:", error);
    } finally {
      setLoading(false);
    }
  }, [wikiId, messageApi]);

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
      messageApi.error("删除失败");
      console.error("Delete document error:", error);
    }
  }, [wikiId, pagination.current, pagination.pageSize, searchText, fetchDocuments, messageApi]);



  return {
    documents,
    loading,
    pagination,
    searchText,
    contextHolder,
    fetchDocuments,
    deleteDocument,
    setSearchText,
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

      // 上传成功后从文件列表中移除
      setSelectedFiles(prev => prev.filter((_, i) => i !== index));

      return true;
    } catch (error) {
      console.error("Upload failed:", error);
      
      setBatchUploadStatus(prev => {
        if (!prev) return prev;
        const newStatuses = [...prev.uploadStatuses];
        newStatuses[index] = {
          file,
          status: "error",
          progress: 0,
          message: error instanceof Error ? error.message : "上传失败",
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

    // 初始化批量上传状态
    const initialStatuses: UploadStatus[] = selectedFiles.map(file => ({
      file,
      status: "waiting",
      progress: 0,
    }));

    setBatchUploadStatus({
      files: selectedFiles,
      currentIndex: 0,
      uploadStatuses: initialStatuses,
      isUploading: true,
    });

    // 逐个上传文件
    for (let i = 0; i < selectedFiles.length; i++) {
      const file = selectedFiles[i];
      const success = await uploadSingleFile(file, i);
      
      if (!success) {
        messageApi.error(`${file.name} 上传失败`);
      }
    }

    // 上传完成，清空上传状态
    setBatchUploadStatus(null);

    messageApi.info("已处理当前文件上传列表");
    onUploadSuccess();
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
): Promise<PreUploadFileCommandResponse> => {
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
const getTableColumns = (wikiId: number, navigate: any, deleteDocument: (id: number) => void) => [
  {
    title: "文件名称",
    dataIndex: "fileName",
    key: "fileName",
    width: 200,
    render: (text: string) => (
      <Space>
        <FileTextOutlined />
        <Text ellipsis={{ tooltip: text }} style={{ maxWidth: 150 }}>
          {text}
        </Text>
      </Space>
    ),
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
    title: "向量化",
    key: "action",
    width: 120,
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

  const {
    documents,
    loading,
    pagination,
    searchText,
    contextHolder: listContextHolder,
    fetchDocuments,
    deleteDocument,
    setSearchText,
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

  const columns = getTableColumns(wikiId, navigate, deleteDocument);

  return (
    <>
      {listContextHolder}
      {uploadContextHolder}
      
      <Card>
        <Row justify="space-between" align="middle" style={{ marginBottom: 16 }}>
          <Col>
            <Search
              placeholder="搜索文档"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              style={{ width: 300 }}
              onSearch={handleSearch}
              allowClear
              enterButton
            />
          </Col>
          <Col>
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
