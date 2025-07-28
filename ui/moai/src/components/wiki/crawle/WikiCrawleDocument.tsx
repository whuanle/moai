import { useState, useEffect, useCallback } from "react";
import {
  Card,
  Input,
  Button,
  message,
  Space,
  Typography,
  Row,
  Col,
  Table,
  Tag,
  Tooltip,
  Popconfirm,
  Empty,
  Spin,
} from "antd";
import {
  CheckCircleOutlined,
  DeleteOutlined,
  FileTextOutlined,
  LockOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { useParams, useNavigate } from "react-router";
import {
  QueryWikiWebDocumentListCommand,
  QueryWikiDocumentListItem,
} from "../../../apiClient/models";
import { FileSizeHelper } from "../../../helper/FileSizeHelper";
import {
  formatDateTime,
  parseJsonDateTime,
} from "../../../helper/DateTimeHelper";

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



// 自定义 Hook - 文档列表管理
const useDocumentList = (wikiId: number, wikiWebConfigId: number) => {
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
    if (!wikiId || !wikiWebConfigId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const client = GetApiClient();
      const requestBody: QueryWikiWebDocumentListCommand = {
        wikiId,
        wikiWebConfigId,
        pageNo: page,
        pageSize,
        query: search,
      };

      const response = await client.api.wiki.web.query_web_document_list.post(requestBody);

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
  }, [wikiId, wikiWebConfigId, messageApi]);

  return {
    documents,
    loading,
    pagination,
    searchText,
    contextHolder,
    fetchDocuments,
    setSearchText,
  };
};

// 文档表格列配置
const getTableColumns = (wikiId: number, wikiWebConfigId: number, navigate: any) => [
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
    title: "操作",
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
      </Space>
    ),
  },
];

// 主组件
export default function WikiCrawleDocument() {
  const { id: wikiId, crawleId: wikiWebConfigId } = useParams();
  const wikiIdNum = parseInt(wikiId || "0");
  const wikiWebConfigIdNum = parseInt(wikiWebConfigId || "0");
  const navigate = useNavigate();

  const {
    documents,
    loading,
    pagination,
    searchText,
    contextHolder,
    fetchDocuments,
    setSearchText,
  } = useDocumentList(wikiIdNum, wikiWebConfigIdNum);

  useEffect(() => {
    if (wikiIdNum && wikiWebConfigIdNum) {
      fetchDocuments();
    }
  }, [wikiIdNum, wikiWebConfigIdNum, fetchDocuments]);

  const handleSearch = useCallback(() => {
    fetchDocuments(1, pagination.pageSize, searchText);
  }, [fetchDocuments, pagination.pageSize, searchText]);

  const handleTableChange = useCallback((newPagination: any) => {
    fetchDocuments(newPagination.current, newPagination.pageSize, searchText);
  }, [fetchDocuments, searchText]);

  const handleRefresh = useCallback(() => {
    fetchDocuments(pagination.current, pagination.pageSize, searchText);
  }, [fetchDocuments, pagination.current, pagination.pageSize, searchText]);



  const columns = getTableColumns(wikiIdNum, wikiWebConfigIdNum, navigate);

  return (
    <>
      {contextHolder}
      
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
    </>
  );
}