import { useState, useEffect, useCallback } from "react";
import { useParams } from "react-router";
import {
  Card,
  Select,
  Input,
  Button,
  Table,
  message,
  Space,
  Tag,
  Typography,
  Row,
  Col,
  Divider,
  Modal,
  Empty,
  Alert,
} from "antd";
import {
  SearchOutlined,
  FullscreenOutlined,
  FullscreenExitOutlined,
  FileTextOutlined,
  InfoCircleOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import type {
  QueryWikiDocumentListItem,
  SearchWikiDocumentTextCommandResponse,
  Citation,
  Citation_Partition,
  QueryWikiDocumentListCommand,
  SearchWikiDocumentTextCommand,
} from "../../apiClient/models";

const { Text } = Typography;

// 类型定义
interface DocumentOption {
  value: number;
  label: string;
  documentId: number;
}

interface SearchResultItem {
  key: string;
  documentId: number;
  fileName: string;
  sourceContentType: string;
  partitionNumber: number;
  sectionNumber: number;
  relevance: number;
  text: string;
  lastUpdate: string;
}

// 文档列表 hook
function useDocumentList(wikiId: number) {
  const [documents, setDocuments] = useState<DocumentOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchDocuments = useCallback(async () => {
    if (!wikiId) {
      messageApi.error("缺少必要的参数");
      return;
    }
    try {
      setLoading(true);
      const client = GetApiClient();
      const requestBody: QueryWikiDocumentListCommand = {
        wikiId,
        pageNo: 1,
        pageSize: 1000,
        query: "",
      };
      const response = await client.api.wiki.document.list.post(requestBody);
      if (response?.items) {
        const embeddedDocs = response.items
          .filter((item: QueryWikiDocumentListItem) => item.embedding === true)
          .map((item: QueryWikiDocumentListItem) => ({
            value: item.documentId!,
            label: item.fileName!,
            documentId: item.documentId!,
          }));
        setDocuments(embeddedDocs);
      }
    } catch (error) {
      messageApi.error("获取文档列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, messageApi]);

  return { documents, loading, contextHolder, fetchDocuments };
}

// 搜索 hook
function useSearch(wikiId: number) {
  const [searchResults, setSearchResults] = useState<SearchResultItem[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const performSearch = useCallback(
    async (searchText: string, selectedDocumentId?: number) => {
      if (!searchText.trim()) {
        messageApi.warning("请输入搜索关键词");
        return;
      }
      if (!wikiId) {
        messageApi.error("缺少必要的参数");
        return;
      }
      try {
        setSearchLoading(true);
        const client = GetApiClient();
        const command: SearchWikiDocumentTextCommand = {
          wikiId,
          query: searchText.trim(),
          documentId: selectedDocumentId || undefined,
        };
        const response: SearchWikiDocumentTextCommandResponse | undefined =
          await client.api.wiki.document.search.post(command);
        if (response?.searchResult) {
          const { searchResult } = response;
          if (searchResult.noResult) {
            setSearchResults([]);
            messageApi.info("未找到相关结果");
            return;
          }
          if (searchResult.results && searchResult.results.length > 0) {
            const resultItems: SearchResultItem[] = [];
            searchResult.results.forEach(
              (citation: Citation, citationIndex: number) => {
                if (citation.partitions && citation.partitions.length > 0) {
                  citation.partitions.forEach(
                    (partition: Citation_Partition, partitionIndex: number) => {
                      resultItems.push({
                        key: `${citationIndex}-${partitionIndex}`,
                        documentId: parseInt(citation.documentId || "0"),
                        fileName: citation.sourceName || "",
                        sourceContentType: citation.sourceContentType || "",
                        partitionNumber: partition.partitionNumber || 0,
                        sectionNumber: partition.sectionNumber || 0,
                        relevance: partition.relevance || 0,
                        text: partition.text || "",
                        lastUpdate: partition.lastUpdate || "",
                      });
                    }
                  );
                }
              }
            );
            setSearchResults(resultItems);
            messageApi.success(
              `已返回排名靠前的 ${resultItems.length} 个相关结果`
            );
          } else {
            setSearchResults([]);
            messageApi.info("未找到相关结果");
          }
        } else {
          setSearchResults([]);
          messageApi.info("未找到相关结果");
        }
      } catch (error) {
        messageApi.error("搜索失败");
      } finally {
        setSearchLoading(false);
      }
    },
    [wikiId, messageApi]
  );

  return { searchResults, searchLoading, contextHolder, performSearch };
}

// 文本详情弹窗 hook
function useTextModal() {
  const [visible, setVisible] = useState(false);
  const [text, setText] = useState("");
  const [fileName, setFileName] = useState("");
  const [isFullscreen, setIsFullscreen] = useState(false);

  const show = useCallback((text: string, fileName: string) => {
    setText(text);
    setFileName(fileName);
    setVisible(true);
    setIsFullscreen(false);
  }, []);
  const toggleFullscreen = useCallback(() => setIsFullscreen((f) => !f), []);
  const close = useCallback(() => {
    setVisible(false);
    setIsFullscreen(false);
  }, []);

  return {
    visible,
    text,
    fileName,
    isFullscreen,
    show,
    toggleFullscreen,
    close,
  };
}

export default function WikiSearch() {
  const { id } = useParams();
  const wikiId = parseInt(id || "0");
  const [selectedDocumentId, setSelectedDocumentId] = useState<
    number | undefined
  >();
  const [searchText, setSearchText] = useState("");

  // hooks
  const {
    documents,
    loading: documentsLoading,
    contextHolder: documentsContextHolder,
    fetchDocuments,
  } = useDocumentList(wikiId);
  const {
    searchResults,
    searchLoading,
    contextHolder: searchContextHolder,
    performSearch,
  } = useSearch(wikiId);
  const {
    visible: textModalVisible,
    text: selectedText,
    fileName: selectedFileName,
    isFullscreen,
    show: showTextModal,
    toggleFullscreen,
    close: closeModal,
  } = useTextModal();

  useEffect(() => {
    if (wikiId) fetchDocuments();
  }, [wikiId, fetchDocuments]);

  // 搜索
  const handleSearch = useCallback(() => {
    performSearch(searchText, selectedDocumentId);
  }, [performSearch, searchText, selectedDocumentId]);
  const handlePressEnter = useCallback(() => {
    handleSearch();
  }, [handleSearch]);

  // 表格列
  const columns = [
    {
      title: "所属文档",
      dataIndex: "fileName",
      key: "fileName",
      width: 200,
      ellipsis: true,
      fixed: "left" as const,
      render: (text: string) => (
        <Text strong ellipsis={{ tooltip: text }}>
          {text}
        </Text>
      ),
    },
    {
      title: "文件类型",
      dataIndex: "sourceContentType",
      key: "sourceContentType",
      width: 150,
      ellipsis: true,
      render: (contentType: string) => (
        <Tag color="blue" icon={<FileTextOutlined />}>
          {contentType || "未知"}
        </Tag>
      ),
    },
    {
      title: "分段号",
      dataIndex: "partitionNumber",
      key: "partitionNumber",
      width: 80,
      align: "center" as const,
      render: (value: number) => <Text code>{value}</Text>,
    },
    {
      title: "章节号",
      dataIndex: "sectionNumber",
      key: "sectionNumber",
      width: 80,
      align: "center" as const,
      render: (value: number) => <Text code>{value}</Text>,
    },
    {
      title: "相关度",
      dataIndex: "relevance",
      key: "relevance",
      width: 100,
      align: "center" as const,
      render: (relevance: number) => (
        <Tag
          color={
            relevance > 0.8 ? "green" : relevance > 0.6 ? "orange" : "default"
          }
        >
          {(relevance * 100).toFixed(1)}%
        </Tag>
      ),
    },
    {
      title: "文本内容",
      dataIndex: "text",
      key: "text",
      ellipsis: true,
      render: (text: string, record: SearchResultItem) => (
        <Text
          style={{ fontSize: 12, cursor: "pointer", color: "#1890ff" }}
          ellipsis={{ tooltip: "点击查看完整内容" }}
          onClick={() => showTextModal(text, record.fileName)}
        >
          {text}
        </Text>
      ),
    },
    {
      title: "生成时间",
      dataIndex: "lastUpdate",
      key: "lastUpdate",
      width: 150,
      fixed: "right" as const,
      render: (lastUpdate: string) => {
        if (!lastUpdate) return "-";
        try {
          return new Date(lastUpdate).toLocaleString("zh-CN");
        } catch {
          return lastUpdate;
        }
      },
    },
  ];

  return (
    <>
      {documentsContextHolder}
      {searchContextHolder}
      <Card
        title={
          <Space>
            <SearchOutlined />
            <span>向量化搜索</span>
          </Space>
        }
        loading={documentsLoading}
        bordered
        style={{ margin: 0 }}
      >
        <Space.Compact
          style={{ minWidth: "500px", maxWidth: "800", marginBottom: 16 }}
        >
          <Select
            placeholder="选择文档(留空搜索整个知识库)"
            allowClear
            style={{ width: 300 }}
            value={selectedDocumentId}
            onChange={setSelectedDocumentId}
            options={documents}
            showSearch
            filterOption={(input, option) =>
              (option?.label ?? "").toLowerCase().includes(input.toLowerCase())
            }
            loading={documentsLoading}
          />
          <Input
            placeholder="请输入搜索关键词"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            onPressEnter={handlePressEnter}
            allowClear
            style={{ flex: 1, width: 400, marginLeft: "10px" }}
          />
          <Button
            type="primary"
            icon={<SearchOutlined />}
            onClick={handleSearch}
            loading={searchLoading}
            disabled={!searchText.trim()}
            style={{ marginLeft: "10px" }}
          >
            搜索
          </Button>
        </Space.Compact>
        <Divider style={{ margin: "12px 0" }} />
        <Table
          columns={columns}
          dataSource={searchResults}
          loading={searchLoading}
          scroll={{ x: 1200, y: 400 }}
          size="small"
          locale={{
            emptyText: (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="暂无搜索结果"
              />
            ),
          }}
          pagination={false}
          bordered
        />
        {searchResults.length > 0 && (
          <Alert
            message={
              <Space>
                <InfoCircleOutlined />
                <span>默认固定返回最符合要求的 10 条数据</span>
              </Space>
            }
            type="info"
            showIcon={false}
            style={{ marginTop: 16 }}
          />
        )}
      </Card>
      <Modal
        title={
          <Space>
            <FileTextOutlined />
            <span>文本内容 - {selectedFileName}</span>
          </Space>
        }
        open={textModalVisible}
        onCancel={closeModal}
        footer={[
          <Button
            key="fullscreen"
            icon={
              isFullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />
            }
            onClick={toggleFullscreen}
          >
            {isFullscreen ? "退出全屏" : "全屏"}
          </Button>,
          <Button key="close" onClick={closeModal}>
            关闭
          </Button>,
        ]}
        width={isFullscreen ? "100vw" : 800}
        style={
          isFullscreen
            ? { top: 0, maxWidth: "100vw", margin: 0, paddingBottom: 0 }
            : { top: 20 }
        }
        bodyStyle={
          isFullscreen ? { height: "calc(100vh - 110px)", padding: "24px" } : {}
        }
      >
        <div
          style={{
            maxHeight: isFullscreen ? "calc(100vh - 150px)" : "60vh",
            overflow: "auto",
            padding: "16px",
            backgroundColor: "#fafafa",
            border: "1px solid #d9d9d9",
            borderRadius: "6px",
            lineHeight: "1.6",
            fontSize: "14px",
            whiteSpace: "pre-wrap",
            wordBreak: "break-word",
          }}
        >
          {selectedText}
        </div>
      </Modal>
    </>
  );
}
