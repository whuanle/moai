import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams } from "react-router";
import {
  Card,
  Select,
  Input,
  InputNumber,
  Button,
  Table,
  message,
  Space,
  Tag,
  Typography,
  Form,
  Checkbox,
  Row,
  Col,
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
  SearchWikiDocumentTextItem,
  QueryWikiDocumentListCommand,
  SearchWikiDocumentTextCommand,
} from "../../apiClient/models";
import { useAiModelList } from "./documentEmbedding/hooks/useAiModelList";

const { Text } = Typography;

// 类型定义
interface DocumentOption {
  value: number;
  label: string;
  documentId: number;
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
  const [searchResults, setSearchResults] = useState<SearchWikiDocumentTextItem[]>([]);
  const [searchAnswer, setSearchAnswer] = useState<string | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const performSearch = useCallback(
    async (values: {
      query: string;
      documentId?: number;
      limit?: number | null;
      minRelevance?: number | null;
      isOptimizeQuery?: boolean;
      isAnswer?: boolean;
      aiModelId?: number | null;
    }) => {
      const queryText = values?.query?.trim();
      if (!queryText) {
        messageApi.warning("请输入搜索关键词");
        return;
      }
      if (!wikiId) {
        messageApi.error("缺少必要的参数");
        return;
      }
      if (values?.limit && (values.limit < 1 || values.limit > 200)) {
        messageApi.warning("召回数量需在 1-200 之间");
        return;
      }
      if (
        values?.minRelevance !== undefined &&
        values.minRelevance !== null &&
        (values.minRelevance < 0 || values.minRelevance > 1)
      ) {
        messageApi.warning("最小相关度范围为 0 - 1");
        return;
      }
      if ((values?.isOptimizeQuery || values?.isAnswer) && !values?.aiModelId) {
        messageApi.warning("开启优化/回答时需要选择 AI 模型");
        return;
      }
      try {
        setSearchLoading(true);
        setSearchAnswer(null);
        const client = GetApiClient();
        const command: SearchWikiDocumentTextCommand = {
          wikiId,
          query: queryText,
          documentId: values.documentId ?? null,
          limit: values?.limit ?? null,
          minRelevance: values?.minRelevance ?? null,
          isOptimizeQuery: values?.isOptimizeQuery ?? false,
          isAnswer: values?.isAnswer ?? false,
          aiModelId: values?.aiModelId ?? 0,
        };
        const response: SearchWikiDocumentTextCommandResponse | undefined =
          await client.api.wiki.document.search.post(command);
        const results = response?.searchResult ?? [];
        setSearchResults(results);
        setSearchAnswer(values?.isAnswer ? response?.answer ?? null : null);
        if (!results || results.length === 0) {
          messageApi.info("未找到相关结果");
        } else {
          messageApi.success(`共返回 ${results.length} 条结果`);
        }
      } catch (error) {
        messageApi.error("搜索失败");
      } finally {
        setSearchLoading(false);
      }
    },
    [wikiId, messageApi]
  );

  const resetResults = useCallback(() => {
    setSearchResults([]);
    setSearchAnswer(null);
  }, []);

  return { searchResults, searchLoading, searchAnswer, contextHolder, performSearch, resetResults };
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
  const [form] = Form.useForm();
  const [answerVisible, setAnswerVisible] = useState(false);

  // hooks
  const {
    documents,
    loading: documentsLoading,
    contextHolder: documentsContextHolder,
    fetchDocuments,
  } = useDocumentList(wikiId);
  const {
    modelList,
    loading: modelListLoading,
    contextHolder: modelContextHolder,
    fetchModelList,
  } = useAiModelList();
  const {
    searchResults,
    searchLoading,
    searchAnswer,
    contextHolder: searchContextHolder,
    performSearch,
    resetResults,
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

  const dataSource = useMemo(
    () =>
      (searchResults || []).map((item, index) => ({
        ...item,
        key: item.chunkId || `${index}`,
      })),
    [searchResults]
  );

  const columns = useMemo(
    () => [
      {
        title: "序号",
        key: "index",
        width: 70,
        align: "center" as const,
        render: (_: any, __: any, index: number) => index + 1,
      },
      {
        title: "文件名称",
        dataIndex: "fileName",
        key: "fileName",
        width: 200,
        ellipsis: true,
        render: (value: string | null) => (
          <Text ellipsis={{ tooltip: value || "N/A" }}>{value || "N/A"}</Text>
        ),
      },
      {
        title: "文件类型",
        dataIndex: "fileType",
        key: "fileType",
        width: 150,
        ellipsis: true,
        render: (value: string | null) => (
          <Tag color="blue" icon={<FileTextOutlined />}>
            {value || "N/A"}
          </Tag>
        ),
      },
      {
        title: "相关度",
        dataIndex: "recordRelevance",
        key: "recordRelevance",
        width: 120,
        align: "center" as const,
        render: (relevance: number | null) => {
          if (relevance === null || relevance === undefined) return "-";
          const percent = relevance * 100;
          const color =
            percent >= 80 ? "green" : percent >= 60 ? "orange" : "default";
          return <Tag color={color}>{`${percent.toFixed(2)}%`}</Tag>;
        },
      },
      {
        title: "索引文本",
        dataIndex: "text",
        key: "text",
        ellipsis: true,
        render: (text: string | null, record: SearchWikiDocumentTextItem) => (
          <Text
            style={{ fontSize: 12, cursor: "pointer", color: "#1890ff" }}
            ellipsis={{ tooltip: "点击查看完整内容" }}
            onClick={() => showTextModal(text || "", record.fileName || "")}
          >
            {text || "暂无数据"}
          </Text>
        ),
      },
      {
        title: "召回文本块",
        dataIndex: "chunkText",
        key: "chunkText",
        ellipsis: true,
        render: (text: string | null, record: SearchWikiDocumentTextItem) => (
          <Text
            style={{ fontSize: 12, cursor: "pointer", color: "#1890ff" }}
            ellipsis={{ tooltip: "点击查看完整内容" }}
            onClick={() => showTextModal(text || "", record.fileName || "")}
          >
            {text || "暂无数据"}
          </Text>
        ),
      },
    ],
    [showTextModal]
  );

  useEffect(() => {
    if (wikiId) {
      fetchDocuments();
      fetchModelList();
    }
  }, [wikiId, fetchDocuments, fetchModelList]);

  const handleSearch = useCallback(async () => {
    const values = await form.validateFields();
    setAnswerVisible(!!values?.isAnswer);
    performSearch({
      ...values,
      documentId: selectedDocumentId,
    });
  }, [form, performSearch, selectedDocumentId]);

  return (
    <>
      {documentsContextHolder}
      {modelContextHolder}
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
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            query: "",
            limit: 20,
            minRelevance: 0.0,
            isOptimizeQuery: false,
            isAnswer: false,
            aiModelId: null,
          }}
          onFinish={handleSearch}
        >
          <Form.Item label="选择文档（可选）">
            <Select
              placeholder="留空搜索整个知识库"
              allowClear
              style={{ width: 360 }}
              value={selectedDocumentId}
              onChange={setSelectedDocumentId}
              options={documents}
              showSearch
              filterOption={(input, option) =>
                (option?.label ?? "")
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
              loading={documentsLoading}
            />
          </Form.Item>

          <Form.Item
            label="搜索内容"
            name="query"
            rules={[{ required: true, message: "请输入搜索关键词" }]}
          >
            <Input.TextArea
              rows={3}
              placeholder="输入待搜索的问题或文本"
              allowClear
              onPressEnter={(e) => {
                if (e.ctrlKey || e.metaKey) {
                  form.submit();
                }
              }}
            />
          </Form.Item>

          <Row gutter={[16, 12]} style={{ marginBottom: 8 }}>
            <Col xs={24} sm={12} md={8} lg={8}>
              <Form.Item label="最大召回数量" name="limit">
                <InputNumber
                  min={1}
                  max={200}
                  placeholder="默认 20"
                  style={{ width: "100%" }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12} md={8} lg={8}>
              <Form.Item label="最小相关度" name="minRelevance">
                <InputNumber
                  min={0}
                  max={1}
                  step={0.01}
                  placeholder="默认 0"
                  style={{ width: "100%" }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} sm={24} md={8} lg={8}>
              <Form.Item label="AI 模型（可选）" name="aiModelId">
                <Select
                  allowClear
                  placeholder="不选择则仅召回"
                  loading={modelListLoading}
                  options={modelList.map((model) => ({
                    label: model.name,
                    value: model.id,
                  }))}
                />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={[16, 12]} style={{ marginBottom: 8 }}>
            <Col xs={24} sm={12} md={8} lg={6}>
              <Form.Item
                name="isOptimizeQuery"
                valuePropName="checked"
                style={{ marginBottom: 0 }}
              >
                <Checkbox>启用提问优化</Checkbox>
              </Form.Item>
            </Col>
            <Col xs={24} sm={12} md={8} lg={6}>
              <Form.Item
                name="isAnswer"
                valuePropName="checked"
                style={{ marginBottom: 0 }}
              >
                <Checkbox>需要 AI 回答</Checkbox>
              </Form.Item>
            </Col>
          </Row>

          <Space  style={{ marginTop: 10 }}>
            <Button
              type="primary"
              htmlType="submit"
              icon={<SearchOutlined />}
              loading={searchLoading}
            >
              开始搜索
            </Button>
            <Button
              onClick={() => {
                form.resetFields();
                resetResults();
                setSelectedDocumentId(undefined);
                setAnswerVisible(false);
              }}
            >
              重置
            </Button>
          </Space>
        </Form>

        {answerVisible && (
          <Alert
            type={searchAnswer ? "success" : "info"}
            showIcon
            message="AI 回答"
            description={searchAnswer || "未返回 AI 回答"}
            style={{ margin: "16px 0" }}
          />
        )}

        <Table style={{ marginTop: 20 }}
          columns={columns}
          dataSource={dataSource}
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
                <span>按配置返回召回结果（默认 20 条）</span>
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
