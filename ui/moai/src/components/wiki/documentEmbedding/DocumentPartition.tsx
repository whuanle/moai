/**
 * 文档切割模块
 * 负责普通切割和智能切割功能
 */

import { useState, useEffect, useCallback } from "react";
import {
  Form,
  Button,
  message,
  InputNumber,
  Select,
  Space,
  Row,
  Col,
  Statistic,
  Alert,
  Empty,
  Tabs,
  Input,
  Collapse,
} from "antd";
import { ScissorOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import { useDocumentInfo } from "./hooks";
import { useAiModelList } from "../wiki_hooks";
import { FormField } from "./components";
import "./styles.css";

const { Panel } = Collapse;

interface DocumentPartitionProps {
  wikiId: string;
  documentId: string;
  onPartitionSuccess?: () => void;
}

/**
 * 文档切割组件
 */
export default function DocumentPartition({
  wikiId,
  documentId,
  onPartitionSuccess,
}: DocumentPartitionProps) {
  const [partitionForm] = Form.useForm();
  const [aiPartitionForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const [activeTab, setActiveTab] = useState<string>("normal");
  const [teamId, setTeamId] = useState<number | undefined>(undefined);
  const [wikiInfoLoaded, setWikiInfoLoaded] = useState(false);

  // 使用共享 hooks
  const { documentInfo, loading: docLoading, fetchDocumentInfo } = useDocumentInfo(wikiId, documentId);
  const { modelList, loading: modelListLoading, fetchModelList } = useAiModelList(teamId, "chat");

  // 切割状态
  const [partitionLoading, setPartitionLoading] = useState(false);
  const [aiPartitionLoading, setAiPartitionLoading] = useState(false);

  // 获取 wiki 信息以获取 teamId
  useEffect(() => {
    const fetchWikiInfo = async () => {
      if (!wikiId) return;
      try {
        const apiClient = GetApiClient();
        const response = await apiClient.api.wiki.query_wiki_info.post({
          wikiId: parseInt(wikiId),
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
    fetchDocumentInfo();
  }, [fetchDocumentInfo]);

  // 当 wikiInfo 加载完成后获取模型列表
  useEffect(() => {
    if (wikiInfoLoaded) {
      fetchModelList();
    }
  }, [wikiInfoLoaded, fetchModelList]);

  // 当获取到文档信息后，更新表单默认值
  useEffect(() => {
    if (documentInfo?.partionConfig) {
      const config = documentInfo.partionConfig;
      partitionForm.setFieldsValue({
        maxTokensPerChunk: config.maxTokensPerChunk ?? 1000,
        overlap: config.overlap ?? 100,
        chunkHeader: config.chunkHeader ?? "",
      });
    }
  }, [documentInfo, partitionForm]);

  /**
   * 提交普通切割
   */
  const handlePartitionSubmit = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }
      if (!values?.maxTokensPerChunk || values.maxTokensPerChunk <= 0) {
        messageApi.warning("每段最大Token数必须大于0");
        return;
      }

      try {
        setPartitionLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.text_partition_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          maxTokensPerChunk: values.maxTokensPerChunk,
          overlap: values.overlap,
          chunkHeader: values.chunkHeader || null,
        });

        messageApi.success("文档切割成功");
        onPartitionSuccess?.();
      } catch (error) {
        console.error("Failed to submit partition task:", error);
        proxyRequestError(error, messageApi, "文档切割失败");
      } finally {
        setPartitionLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onPartitionSuccess]
  );

  /**
   * 提交智能切割
   */
  const handleAiPartitionSubmit = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }
      if (!values?.aiModelId) {
        messageApi.error("请选择AI模型");
        return;
      }

      try {
        setAiPartitionLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.ai_text_partition_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          aiModelId: values.aiModelId,
          promptTemplate: values.promptTemplate || null,
        });

        messageApi.success("智能切割任务已提交");
        onPartitionSuccess?.();
      } catch (error) {
        console.error("Failed to submit AI partition task:", error);
        proxyRequestError(error, messageApi, "智能切割失败");
      } finally {
        setAiPartitionLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onPartitionSuccess]
  );

  if (!wikiId || !documentId) {
    return <Empty description="缺少必要的参数" />;
  }

  // 文档统计信息
  const renderDocStats = () => (
    <Row gutter={[16, 16]} className="doc-embed-stats-row">
      <Col span={8}>
        <Statistic title="文档名称" value={documentInfo?.fileName || "-"} valueStyle={{ fontSize: 16 }} />
      </Col>
      <Col span={8}>
        <Statistic title="文件大小" value={documentInfo?.fileSize || 0} suffix="bytes" valueStyle={{ fontSize: 16 }} />
      </Col>
    </Row>
  );

  // 普通切割表单
  const renderNormalPartition = () => (
    <Form
      form={partitionForm}
      layout="vertical"
      onFinish={handlePartitionSubmit}
      initialValues={{ maxTokensPerChunk: 1000, overlap: 100, chunkHeader: "" }}
    >
      {docLoading ? (
        <Empty description="加载中..." />
      ) : documentInfo ? (
        <>
          {renderDocStats()}
          <Row gutter={[16, 16]}>
            <Col span={12}>
              <FormField label="每段最大Token数" description="当对文档进行分段时，每个分段通常包含一个段落。此参数控制每个段落的最大token数量。">
                <Form.Item name="maxTokensPerChunk" rules={[{ required: true, message: "请输入每段最大Token数" }]}>
                  <InputNumber min={1} max={100000} style={{ width: "100%" }} />
                </Form.Item>
              </FormField>
            </Col>
            <Col span={12}>
              <FormField label="重叠Token数" description="分段之间的重叠token数量，用于保持上下文的连贯性。">
                <Form.Item name="overlap" rules={[{ required: true, message: "请输入重叠Token数" }]}>
                  <InputNumber min={0} max={1000} style={{ width: "100%" }} />
                </Form.Item>
              </FormField>
            </Col>
          </Row>
          <Row gutter={[16, 16]}>
            <Col span={24}>
              <FormField label="分块标题（可选）" description="可选，在每个分块前添加的标题。">
                <Form.Item name="chunkHeader">
                  <Input placeholder="请输入分块标题（可选）" />
                </Form.Item>
              </FormField>
            </Col>
          </Row>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={partitionLoading} icon={<ScissorOutlined />}>
              开始切割
            </Button>
          </Form.Item>
          <Alert
            className="doc-embed-alert"
            message="普通切割说明"
            description="普通切割适合对称检索，按照固定的token数量和重叠数量对文档进行分段。"
            type="info"
            showIcon
          />
        </>
      ) : (
        <Empty description="无法获取文档信息" />
      )}
    </Form>
  );

  // 智能切割表单
  const renderSmartPartition = () => (
    <Form
      form={aiPartitionForm}
      layout="vertical"
      onFinish={handleAiPartitionSubmit}
      initialValues={{
        promptTemplate: `你是一个专业的中文知识库文档拆分助手。

请根据用户提供的完整文档内容按照以下要求拆分文本：

1. 每个文本块长度尽量不超过 1000 个字符，尽可能不要切开多个文本块，可根据语义适当调整，如果长度够用，则请勿拆分多个块。
2. 在有多个文本块的情况下，则相邻文本块需要保留约 50 个字符的重叠内容以保证上下文衔接，只有一个块则不需要生成重叠内容。
3.尽可能不要拆开代码或段落，尽可能让语义相近的内容在一个段落内。

3. 只允许引用原文内容，不要编造或总结。

4. 输出统一使用 JSON 字符串数组，格式如下：

[
"第一块原文内容",
"第二块原文内容"
]

只输出 JSON，不要附加其他解释。`,
      }}
    >
      {docLoading ? (
        <Empty description="加载中..." />
      ) : documentInfo ? (
        <>
          {renderDocStats()}
          <Row gutter={[16, 16]}>
            <Col span={8}>
              <FormField label="AI模型" description="选择用于智能切割的AI模型。">
                <Form.Item name="aiModelId" rules={[{ required: true, message: "请选择AI模型" }]}>
                  <Select
                    placeholder="请选择AI模型"
                    loading={modelListLoading}
                    options={modelList.map((model) => ({ label: model.name, value: model.id }))}
                  />
                </Form.Item>
              </FormField>
            </Col>
          </Row>
          <Row gutter={[16, 16]}>
            <Col span={24}>
              <FormField label="提示词模板" description="用于指导AI进行智能切割的提示词模板，请务必提示模型输出 JSON 格式的数据。">
                <Form.Item name="promptTemplate">
                  <Input.TextArea rows={4} placeholder="请输入提示词模板" />
                </Form.Item>
              </FormField>
            </Col>
          </Row>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={aiPartitionLoading} icon={<ScissorOutlined />}>
              智能切割
            </Button>
          </Form.Item>
          <Alert
            className="doc-embed-alert"
            message="智能切割说明"
            description="智能切割使用AI模型来理解文档内容，按照语义和上下文进行更智能的分段，适合复杂文档的切割需求。"
            type="info"
            showIcon
          />
        </>
      ) : (
        <Empty description="无法获取文档信息" />
      )}
    </Form>
  );

  return (
    <>
      {contextHolder}
      <Collapse defaultActiveKey={[]} className="doc-embed-collapse">
        <Panel
          header={
            <Space>
              <ScissorOutlined />
              <span>文档切割</span>
            </Space>
          }
          key="partition"
        >
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={[
              { key: "normal", label: "普通切割", children: renderNormalPartition() },
              { key: "smart", label: "智能切割", children: renderSmartPartition() },
            ]}
          />
        </Panel>
      </Collapse>
    </>
  );
}
