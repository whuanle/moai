/**
 * 召回测试模块
 * 负责文档的召回测试功能
 */

import { useState, useEffect, useCallback, useMemo } from "react";
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
  Collapse,
  Input,
  Checkbox,
} from "antd";
import { ThunderboltOutlined, SearchOutlined, FileTextOutlined } from "@ant-design/icons";
import ReactMarkdown from "react-markdown";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import { useAiModelList } from "./hooks";
import type { SearchWikiDocumentTextCommand, SearchWikiDocumentTextItem } from "../../../apiClient/models";
import "./styles.css";

const { Panel } = Collapse;

interface RecallTestProps {
  wikiId: string;
  documentId: string;
}

/**
 * 召回测试组件
 */
export default function RecallTest({ wikiId, documentId }: RecallTestProps) {
  const [recallForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();

  // 使用共享 hook
  const { modelList, loading: modelListLoading, fetchModelList }  = useAiModelList(parseInt(wikiId), "chat");

  // 召回状态
  const [recallLoading, setRecallLoading] = useState(false);
  const [recallResults, setRecallResults] = useState<SearchWikiDocumentTextItem[]>([]);
  const [recallAnswer, setRecallAnswer] = useState<string | null>(null);
  const [recallAnswerVisible, setRecallAnswerVisible] = useState(false);

  // 监听表单值变化，判断 AI 模型是否必选
  const isOptimizeQuery = Form.useWatch("isOptimizeQuery", recallForm);
  const isAnswer = Form.useWatch("isAnswer", recallForm);
  const isAiModelRequired = isOptimizeQuery || isAnswer;

  useEffect(() => {
    fetchModelList();
  }, [fetchModelList]);

  /**
   * 召回测试搜索
   */
  const handleRecallSearch = useCallback(
    async (values: any) => {
      const queryText = values?.query?.trim();
      if (!queryText) {
        messageApi.warning("请输入搜索关键词");
        return;
      }

      if (!wikiId || !documentId) {
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
        setRecallLoading(true);
        setRecallAnswer(null);
        setRecallAnswerVisible(!!values?.isAnswer);
        const apiClient = GetApiClient();
        const command: SearchWikiDocumentTextCommand = {
          wikiId: parseInt(wikiId, 10),
          documentId: parseInt(documentId, 10),
          query: queryText,
          limit: values?.limit ?? null,
          minRelevance: values?.minRelevance ?? null,
          isOptimizeQuery: values?.isOptimizeQuery ?? false,
          isAnswer: values?.isAnswer ?? false,
          aiModelId: values?.aiModelId ?? 0,
        };

        const response = await apiClient.api.wiki.document.search.post(command);
        const results = response?.searchResult ?? [];
        setRecallResults(results);
        setRecallAnswer(values?.isAnswer ? (response?.answer ?? null) : null);

        if (!results || results.length === 0) {
          messageApi.info("未找到搜索结果");
        } else {
          messageApi.success(`共返回 ${results.length} 条结果`);
        }
      } catch (error) {
        console.error("Failed to search recall:", error);
        proxyRequestError(error, messageApi, "召回搜索失败");
      } finally {
        setRecallLoading(false);
      }
    },
    [wikiId, documentId, messageApi]
  );

  const handleReset = useCallback(() => {
    recallForm.resetFields();
    setRecallResults([]);
    setRecallAnswer(null);
    setRecallAnswerVisible(false);
  }, [recallForm]);

  const recallDataSource = useMemo(
    () => (recallResults || []).map((item, index) => ({ ...item, key: item.chunkId || `${index}` })),
    [recallResults]
  );

  const recallColumns = useMemo(
    () => [
      {
        title: "序号",
        key: "index",
        width: 70,
        align: "center" as const,
        render: (_: any, __: any, index: number) => index + 1,
      },
      {
        title: "Chunk ID",
        dataIndex: "chunkId",
        key: "chunkId",
        width: 200,
        ellipsis: true,
        render: (value: string) => (
          <Typography.Text code ellipsis={{ tooltip: value || "N/A" }}>
            {value || "N/A"}
          </Typography.Text>
        ),
      },
      {
        title: "文档ID",
        dataIndex: "documentId",
        key: "documentId",
        width: 120,
        align: "center" as const,
        render: (value: number | null) => <Typography.Text>{value ?? "-"}</Typography.Text>,
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
          <Typography.Paragraph style={{ marginBottom: 0 }} ellipsis={{ rows: 2, tooltip: text || "暂无数据" }}>
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
          <Typography.Paragraph style={{ marginBottom: 0 }} ellipsis={{ rows: 2, tooltip: text || "暂无数据" }}>
            {text || "暂无数据"}
          </Typography.Paragraph>
        ),
      },
    ],
    []
  );

  if (!wikiId || !documentId) {
    return <Empty description="缺少必要的参数" />;
  }

  return (
    <>
      {contextHolder}
      <Collapse defaultActiveKey={[]} className="doc-embed-collapse">
        <Panel
          header={
            <Space>
              <ThunderboltOutlined />
              <span>召回测试</span>
            </Space>
          }
          key="recall-test"
        >
          <Form
            form={recallForm}
            layout="vertical"
            onFinish={handleRecallSearch}
            initialValues={{ limit: 20, minRelevance: 0.0, isOptimizeQuery: false, isAnswer: false }}
          >
            <Row gutter={[16, 16]}>
              <Col span={12}>
                <Form.Item label="搜索内容" name="query" rules={[{ required: true, message: "请输入搜索内容" }]}>
                  <Input.TextArea rows={4} placeholder="输入待测试的问题或文本" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Row gutter={[12, 12]}>
                  <Col span={12}>
                    <Form.Item label="最大召回数量" name="limit">
                      <InputNumber min={1} max={200} style={{ width: "100%" }} placeholder="默认 20" />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item label="最小相关度" name="minRelevance">
                      <InputNumber min={0} max={1} step={0.01} style={{ width: "100%" }} placeholder="默认 0" />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item name="isOptimizeQuery" valuePropName="checked" style={{ marginBottom: 0 }}>
                      <Checkbox>启用提问优化</Checkbox>
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item name="isAnswer" valuePropName="checked" style={{ marginBottom: 0 }}>
                      <Checkbox>需要 AI 回答</Checkbox>
                    </Form.Item>
                  </Col>
                  <Col span={24}>
                    <Form.Item
                      label={isAiModelRequired ? "AI 模型" : "AI 模型（可选）"}
                      name="aiModelId"
                      rules={[{ required: isAiModelRequired, message: "请选择 AI 模型" }]}
                    >
                      <Select
                        allowClear={!isAiModelRequired}
                        placeholder={isAiModelRequired ? "请选择 AI 模型" : "不选择则仅召回"}
                        loading={modelListLoading}
                        options={modelList.map((model) => ({ label: model.name, value: model.id }))}
                      />
                    </Form.Item>
                  </Col>
                </Row>
              </Col>
            </Row>
            <Form.Item>
              <Space>
                <Button type="primary" htmlType="submit" loading={recallLoading} icon={<SearchOutlined />}>
                  开始召回
                </Button>
                <Button onClick={handleReset}>重置</Button>
              </Space>
            </Form.Item>
          </Form>

          {recallAnswerVisible && (
            <Alert
              type={recallAnswer ? "success" : "info"}
              showIcon
              message="AI 回答"
              description={recallAnswer ? <ReactMarkdown>{recallAnswer}</ReactMarkdown> : "未返回 AI 回答"}
              style={{ marginBottom: 16 }}
            />
          )}

          <Table
            columns={recallColumns}
            dataSource={recallDataSource}
            loading={recallLoading}
            size="small"
            pagination={false}
            rowKey="key"
            scroll={{ x: 1400 }}
            locale={{ emptyText: <Empty description="暂无召回结果" /> }}
          />
        </Panel>
      </Collapse>
    </>
  );
}
