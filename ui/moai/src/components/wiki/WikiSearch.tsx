/**
 * 知识库向量化搜索模块
 * 支持全库搜索或指定文档搜索
 */

import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams } from "react-router";
import {
  Form,
  Button,
  message,
  InputNumber,
  Select,
  Table,
  Space,
  Typography,
  Row,
  Col,
  Alert,
  Empty,
  Tag,
  Input,
  Checkbox,
  Card,
} from "antd";
import { SearchOutlined, FileTextOutlined } from "@ant-design/icons";
import ReactMarkdown from "react-markdown";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import { useAiModelList } from "./wiki_hooks";
import type {
  QueryWikiDocumentListItem,
  SearchWikiDocumentTextCommand,
  SearchWikiDocumentTextItem,
} from "../../apiClient/models";
import "./documentEmbedding/styles.css";

interface DocumentOption {
  value: number;
  label: string;
}

/**
 * 获取已嵌入的文档列表
 */
function useDocumentList(wikiId: number) {
  const [documents, setDocuments] = useState<DocumentOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchDocuments = useCallback(async () => {
    if (!wikiId) return;

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.list.post({
        wikiId,
        pageNo: 1,
        pageSize: 1000,
        query: "",
      });

      if (response?.items) {
        const embeddedDocs = response.items
          .filter((item: QueryWikiDocumentListItem) => item.embedding === true)
          .map((item: QueryWikiDocumentListItem) => ({
            value: item.documentId!,
            label: item.fileName!,
          }));
        setDocuments(embeddedDocs);
      }
    } catch (error) {
      console.error("Failed to fetch documents:", error);
      proxyRequestError(error, messageApi, "获取文档列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, messageApi]);

  return { documents, loading, contextHolder, fetchDocuments };
}

/**
 * 知识库搜索组件
 */
export default function WikiSearch() {
  const { id } = useParams();
  const wikiId = parseInt(id || "0");

  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const [teamId, setTeamId] = useState<number | undefined>(undefined);
  const [wikiInfoLoaded, setWikiInfoLoaded] = useState(false);

  // 使用共享 hooks
  const { documents, loading: documentsLoading, contextHolder: docContextHolder, fetchDocuments } = useDocumentList(wikiId);
  const { modelList, loading: modelListLoading, fetchModelList } = useAiModelList(teamId, "chat");

  // 搜索状态
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchResults, setSearchResults] = useState<SearchWikiDocumentTextItem[]>([]);
  const [searchAnswer, setSearchAnswer] = useState<string | null>(null);
  const [answerVisible, setAnswerVisible] = useState(false);

  // 获取 wiki 信息以获取 teamId
  useEffect(() => {
    const fetchWikiInfo = async () => {
      if (!wikiId) return;
      try {
        const apiClient = GetApiClient();
        const response = await apiClient.api.wiki.query_wiki_info.post({
          wikiId,
        });
        setTeamId(response?.teamId ?? undefined);
        setWikiInfoLoaded(true);
      } catch (error) {
        console.error("获取知识库信息失败:", error);
        setWikiInfoLoaded(true);
      }
    };
    fetchWikiInfo();
  }, [wikiId]);

  useEffect(() => {
    if (wikiId) {
      fetchDocuments();
    }
  }, [wikiId, fetchDocuments]);

  // 当 wikiInfo 加载完成后获取模型列表
  useEffect(() => {
    if (wikiInfoLoaded) {
      fetchModelList();
    }
  }, [wikiInfoLoaded, fetchModelList]);

  /**
   * 执行搜索
   */
  const handleSearch = useCallback(
    async (values: any) => {
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

      if (values?.minRelevance !== undefined && values.minRelevance !== null && (values.minRelevance < 0 || values.minRelevance > 1)) {
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
        setAnswerVisible(!!values?.isAnswer);

        const apiClient = GetApiClient();
        const command: SearchWikiDocumentTextCommand = {
          wikiId,
          documentId: values?.documentId ?? null,
          query: queryText,
          limit: values?.limit ?? null,
          minRelevance: values?.minRelevance ?? null,
          isOptimizeQuery: values?.isOptimizeQuery ?? false,
          isAnswer: values?.isAnswer ?? false,
          aiModelId: values?.aiModelId ?? 0,
        };

        const response = await apiClient.api.wiki.document.search.post(command);
        const results = response?.searchResult ?? [];
        setSearchResults(results);
        setSearchAnswer(values?.isAnswer ? (response?.answer ?? null) : null);

        if (!results || results.length === 0) {
          messageApi.info("未找到搜索结果");
        } else {
          messageApi.success(`共返回 ${results.length} 条结果`);
        }
      } catch (error) {
        console.error("Failed to search:", error);
        proxyRequestError(error, messageApi, "搜索失败");
      } finally {
        setSearchLoading(false);
      }
    },
    [wikiId, messageApi]
  );

  const handleReset = useCallback(() => {
    form.resetFields();
    setSearchResults([]);
    setSearchAnswer(null);
    setAnswerVisible(false);
  }, [form]);

  const dataSource = useMemo(
    () => (searchResults || []).map((item, index) => ({ ...item, key: item.chunkId || `${index}` })),
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
          <Typography.Text ellipsis={{ tooltip: value || "N/A" }}>{value || "N/A"}</Typography.Text>
        ),
      },
      {
        title: "文件类型",
        dataIndex: "fileType",
        key: "fileType",
        width: 120,
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
          const color = percent >= 80 ? "green" : percent >= 60 ? "orange" : "default";
          return <Tag color={color}>{`${percent.toFixed(2)}%`}</Tag>;
        },
      },
      {
        title: "索引文本",
        dataIndex: "text",
        key: "text",
        ellipsis: true,
        render: (text: string) => (
          <Typography.Paragraph className="chunk-text-content" ellipsis={{ rows: 2, tooltip: text || "暂无数据" }}>
            {text || "暂无数据"}
          </Typography.Paragraph>
        ),
      },
      {
        title: "召回文本块",
        dataIndex: "chunkText",
        key: "chunkText",
        ellipsis: true,
        render: (text: string) => (
          <Typography.Paragraph className="chunk-text-content" ellipsis={{ rows: 2, tooltip: text || "暂无数据" }}>
            {text || "暂无数据"}
          </Typography.Paragraph>
        ),
      },
    ],
    []
  );

  if (!wikiId) {
    return <Empty description="缺少必要的参数" />;
  }

  return (
    <>
      {contextHolder}
      {docContextHolder}
      <Card
        title={
          <Space>
            <SearchOutlined />
            <span>向量化搜索</span>
          </Space>
        }
        loading={documentsLoading}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSearch}
          initialValues={{ limit: 20, minRelevance: 0.0, isOptimizeQuery: false, isAnswer: false }}
        >
          <Row gutter={[16, 16]}>
            <Col span={12}>
              <Form.Item label="选择文档（可选）" name="documentId">
                <Select
                  placeholder="留空搜索整个知识库"
                  allowClear
                  showSearch
                  filterOption={(input, option) =>
                    (option?.label ?? "").toLowerCase().includes(input.toLowerCase())
                  }
                  loading={documentsLoading}
                  options={documents}
                />
              </Form.Item>
              <Form.Item label="搜索内容" name="query" rules={[{ required: true, message: "请输入搜索内容" }]}>
                <Input.TextArea rows={4} placeholder="输入待搜索的问题或文本" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Row gutter={[12, 12]}>
                <Col span={12}>
                  <Form.Item label="最大召回数量" name="limit">
                    <InputNumber min={1} max={200} placeholder="默认 20" className="moai-input-full" />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item label="最小相关度" name="minRelevance">
                    <InputNumber min={0} max={1} step={0.01} placeholder="默认 0" className="moai-input-full" />
                  </Form.Item>
                </Col>
                <Col span={24}>
                  <Form.Item noStyle shouldUpdate={(prev, cur) => prev.isOptimizeQuery !== cur.isOptimizeQuery || prev.isAnswer !== cur.isAnswer}>
                    {({ getFieldValue }) => {
                      const needModel = getFieldValue("isOptimizeQuery") || getFieldValue("isAnswer");
                      return (
                        <Form.Item
                          label="AI 模型"
                          name="aiModelId"
                          rules={needModel ? [{ required: true, message: "请选择 AI 模型" }] : []}
                        >
                          <Select
                            allowClear
                            placeholder={needModel ? "请选择 AI 模型" : "不选择则仅召回"}
                            loading={modelListLoading}
                            options={modelList.map((model) => ({ label: model.name, value: model.id }))}
                          />
                        </Form.Item>
                      );
                    }}
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item name="isOptimizeQuery" valuePropName="checked">
                    <Checkbox>启用提问优化</Checkbox>
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item name="isAnswer" valuePropName="checked">
                    <Checkbox>需要 AI 回答</Checkbox>
                  </Form.Item>
                </Col>
              </Row>
            </Col>
          </Row>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={searchLoading} icon={<SearchOutlined />}>
                开始搜索
              </Button>
              <Button onClick={handleReset}>重置</Button>
            </Space>
          </Form.Item>
        </Form>

        {answerVisible && (
          <Alert
            type={searchAnswer ? "success" : "info"}
            showIcon
            message="AI 回答"
            description={searchAnswer ? <ReactMarkdown>{searchAnswer}</ReactMarkdown> : "未返回 AI 回答"}
            className="doc-embed-alert"
          />
        )}

        <Table
          columns={columns}
          dataSource={dataSource}
          loading={searchLoading}
          size="small"
          pagination={false}
          rowKey="key"
          scroll={{ x: 1200 }}
          locale={{ emptyText: <Empty description="暂无搜索结果" /> }}
        />
      </Card>
    </>
  );
}
