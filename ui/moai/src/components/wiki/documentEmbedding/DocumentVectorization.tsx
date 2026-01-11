/**
 * 文档向量化模块
 * 负责文档的向量化操作和向量清空
 */

import { useState, useEffect, useCallback } from "react";
import {
  Form,
  Button,
  message,
  InputNumber,
  Space,
  Row,
  Col,
  Statistic,
  Alert,
  Empty,
  Collapse,
  Tooltip,
  Popconfirm,
  Checkbox,
} from "antd";
import { FileTextOutlined, ReloadOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import { useDocumentInfo } from "./hooks";
import { FormField } from "./components";
import "./styles.css";

const { Panel } = Collapse;

interface DocumentVectorizationProps {
  wikiId: string;
  documentId: string;
  onVectorizationSuccess?: () => void;
}

/**
 * 文档向量化组件
 */
export default function DocumentVectorization({
  wikiId,
  documentId,
  onVectorizationSuccess,
}: DocumentVectorizationProps) {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();

  // 使用共享 hook
  const { documentInfo, loading: docLoading, fetchDocumentInfo } = useDocumentInfo(wikiId, documentId);

  // 向量化状态
  const [embedLoading, setEmbedLoading] = useState(false);
  const [clearLoading, setClearLoading] = useState(false);

  useEffect(() => {
    fetchDocumentInfo();
  }, [fetchDocumentInfo]);

  /**
   * 提交向量化任务
   */
  const handleSubmit = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }
      if (values?.threadCount && (values.threadCount < 1 || values.threadCount > 100)) {
        messageApi.warning("并发线程数量必须在1-100之间");
        return;
      }

      try {
        setEmbedLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.embedding_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          isEmbedSourceText: values.isEmbedSourceText ?? false,
          threadCount: values.threadCount ?? null,
        });

        messageApi.success("向量化任务已提交");
        onVectorizationSuccess?.();
      } catch (error) {
        console.error("Failed to submit embedding task:", error);
        proxyRequestError(error, messageApi, "提交向量化任务失败");
      } finally {
        setEmbedLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onVectorizationSuccess]
  );

  /**
   * 清空向量
   */
  const handleClearVectors = useCallback(async () => {
    if (!wikiId || !documentId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setClearLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.clear_embeddingt.post({
        wikiId: parseInt(wikiId),
        documentIds: [parseInt(documentId)],
      });

      messageApi.success("向量已清空");
      fetchDocumentInfo();
      onVectorizationSuccess?.();
    } catch (error) {
      console.error("Failed to clear vectors:", error);
      proxyRequestError(error, messageApi, "清空向量失败");
    } finally {
      setClearLoading(false);
    }
  }, [wikiId, documentId, messageApi, onVectorizationSuccess]);

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
              <FileTextOutlined />
              <span>文档向量化</span>
              <Button
                type="text"
                icon={<ReloadOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  fetchDocumentInfo();
                }}
                loading={docLoading}
                size="small"
              />
            </Space>
          }
          key="embedding"
        >
          {docLoading ? (
            <Empty description="加载中..." />
          ) : documentInfo ? (
            <>
              <Row gutter={[16, 16]} className="doc-embed-stats-row">
                <Col span={6}>
                  <Statistic title="文档名称" value={documentInfo.fileName} valueStyle={{ fontSize: 16 }} />
                </Col>
                <Col span={6}>
                  <Statistic title="切片数量" value={documentInfo.chunkCount} valueStyle={{ fontSize: 16 }} />
                </Col>
                <Col span={6}>
                  <Statistic title="元数据数量" value={documentInfo.metadataCount} valueStyle={{ fontSize: 16 }} />
                </Col>
                <Col span={6}>
                  <Statistic
                    title="向量化状态"
                    value={documentInfo.isEmbedding ? "已向量化" : "未向量化"}
                    valueStyle={{ fontSize: 16, color: documentInfo.isEmbedding ? "#52c41a" : "#faad14" }}
                  />
                </Col>
              </Row>

              <Form
                form={form}
                layout="vertical"
                onFinish={handleSubmit}
                initialValues={{ isEmbedSourceText: false, threadCount: 5 }}
              >
                <Row gutter={[16, 16]}>
                  <Col span={12}>
                    <FormField label="是否将 chunk 源文本也向量化" description="是否将 chunk 源文本也向量化。">
                      <Form.Item name="isEmbedSourceText" valuePropName="checked">
                        <Checkbox>将 chunk 源文本也向量化</Checkbox>
                      </Form.Item>
                    </FormField>
                  </Col>
                  <Col span={12}>
                    <FormField label="并发线程数量" description="并发线程数量，用于控制向量化任务的并发度。">
                      <Form.Item name="threadCount">
                        <InputNumber min={1} max={100} placeholder="请输入并发线程数量（可选）" style={{ width: "100%" }} />
                      </Form.Item>
                    </FormField>
                  </Col>
                </Row>

                <Form.Item>
                  <Space>
                    <Button type="primary" htmlType="submit" loading={embedLoading} icon={<FileTextOutlined />}>
                      开始向量化
                    </Button>
                    <Tooltip title="清空该文档的所有向量">
                      <Popconfirm
                        title="确认清空向量"
                        description="确定要清空该文档的所有向量数据吗？此操作不可恢复。"
                        onConfirm={handleClearVectors}
                        okText="确认"
                        cancelText="取消"
                        okButtonProps={{ danger: true }}
                      >
                        <Button type="default" loading={clearLoading} danger>
                          清空向量
                        </Button>
                      </Popconfirm>
                    </Tooltip>
                  </Space>
                </Form.Item>
              </Form>

              <Alert
                className="doc-embed-alert"
                message="向量化说明"
                description="文档向量化是将文档内容转换为向量表示的过程，用于后续的语义搜索和相似度计算。"
                type="info"
                showIcon
              />
            </>
          ) : (
            <Empty description="无法获取文档信息" />
          )}
        </Panel>
      </Collapse>
    </>
  );
}
   