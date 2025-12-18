/**
 * Chunk编辑模态窗口组件
 * 用于编辑文本块内容和元数据
 */

import { useState, useEffect, useCallback } from "react";
import {
  Modal,
  Button,
  Input,
  Select,
  Space,
  Typography,
  List,
  Popconfirm,
  Tag,
  Divider,
  message,
} from "antd";
import {
  EditOutlined,
  DeleteOutlined,
  PlusOutlined,
  CheckOutlined,
} from "@ant-design/icons";
import CodeEditorModal from "../../../common/CodeEditorModal";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { ChunkEditModalProps } from "../types";
import type { 
  WikiDocumentDerivativeItem,
  PreprocessStrategyType,
  ParagrahProcessorMetadataType as DerivativeType,
  KeyValueOfInt64AndString,
} from "../../../../apiClient/models";
import {
  DERIVATIVE_TYPE_MAP,
  DERIVATIVE_TYPE_OPTIONS,
  PREPROCESS_STRATEGY_OPTIONS,
} from "../constants";
import { extractDerivativesFromResponse } from "../utils/aiGenerationHelper";

/**
 * Chunk编辑模态窗口组件
 */
export const ChunkEditModal: React.FC<ChunkEditModalProps> = ({
  open,
  chunkId,
  initialText,
  initialDerivatives,
  wikiId,
  documentId,
  modelList,
  onClose,
  onSave,
}) => {
  const [textEditorVisible, setTextEditorVisible] = useState(false);
  const [textEditorValue, setTextEditorValue] = useState(initialText);
  const [derivatives, setDerivatives] = useState<WikiDocumentDerivativeItem[]>(
    initialDerivatives || []
  );
  const [editingDerivativeIndex, setEditingDerivativeIndex] = useState<number | null>(null);
  const [newDerivativeType, setNewDerivativeType] = useState<DerivativeType | null>(null);
  const [newDerivativeContent, setNewDerivativeContent] = useState<string>("");
  const [aiGenerateVisible, setAiGenerateVisible] = useState(false);
  const [aiModelId, setAiModelId] = useState<number | null>(null);
  const [preprocessStrategyType, setPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [aiGenerating, setAiGenerating] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 当模态窗口打开时，重置状态
  useEffect(() => {
    if (open) {
      setTextEditorValue(initialText);
      setDerivatives(initialDerivatives || []);
    }
  }, [open, initialText, initialDerivatives]);

  const handleTextEditorConfirm = useCallback((value: string) => {
    setTextEditorValue(value);
    setTextEditorVisible(false);
  }, []);

  const handleAddDerivative = useCallback(() => {
    if (!newDerivativeType || !newDerivativeContent.trim()) {
      messageApi.warning("请选择类型并输入内容");
      return;
    }
    setDerivatives([
      ...derivatives,
      {
        derivativeType: newDerivativeType,
        derivativeContent: newDerivativeContent.trim(),
      },
    ]);
    setNewDerivativeType(null);
    setNewDerivativeContent("");
  }, [newDerivativeType, newDerivativeContent, derivatives, messageApi]);

  const handleDeleteDerivative = useCallback((index: number) => {
    setDerivatives(derivatives.filter((_, i) => i !== index));
  }, [derivatives]);

  const handleSave = useCallback(() => {
    onSave(textEditorValue, derivatives.length > 0 ? derivatives : null);
  }, [textEditorValue, derivatives, onSave]);

  /**
   * AI生成元数据
   * 调用API生成元数据并添加到列表中
   */
  const handleAiGenerate = useCallback(async () => {
    if (!chunkId) {
      messageApi.warning("请先保存文本块");
      return;
    }
    if (!aiModelId) {
      messageApi.warning("请选择AI模型");
      return;
    }
    if (!preprocessStrategyType) {
      messageApi.warning("请选择优化策略");
      return;
    }
    if (!textEditorValue.trim()) {
      messageApi.warning("文本内容不能为空");
      return;
    }

    try {
      setAiGenerating(true);
      const apiClient = GetApiClient();
      
      // 构建 chunks 参数
      const chunks: KeyValueOfInt64AndString[] = [{
        key: chunkId,
        value: textEditorValue,
      }];

      const response = await apiClient.api.wiki.document.ai_generation_chunk.post({
        wikiId: parseInt(wikiId),
        aiModelId: aiModelId,
        chunks: chunks,
        preprocessStrategyType: preprocessStrategyType,
      });

      if (response?.items && response.items.length > 0) {
        // 使用工具函数提取元数据
        const newDerivatives = extractDerivativesFromResponse(
          response.items,
          preprocessStrategyType
        );

        if (newDerivatives.length > 0) {
          setDerivatives((prev) => [...prev, ...newDerivatives]);
          messageApi.success(`成功生成 ${newDerivatives.length} 个元数据`);
          setAiGenerateVisible(false);
          setAiModelId(null);
          setPreprocessStrategyType(null);
        } else {
          messageApi.warning("AI生成未返回有效内容");
        }
      } else {
        messageApi.warning("AI生成未返回结果");
      }
    } catch (error) {
      console.error("Failed to generate AI derivatives:", error);
      proxyRequestError(error, messageApi, "AI生成失败，请重试");
    } finally {
      setAiGenerating(false);
    }
  }, [
    chunkId,
    aiModelId,
    preprocessStrategyType,
    textEditorValue,
    wikiId,
    messageApi,
  ]);

  return (
    <>
      {contextHolder}
      <Modal
        title={chunkId ? "编辑文本块" : "新增文本块"}
        open={open}
        onCancel={onClose}
        onOk={handleSave}
        width={1000}
        okText="保存"
        cancelText="取消"
        destroyOnClose
      >
        <div style={{ marginBottom: 24 }}>
          <div style={{ marginBottom: 8, fontWeight: 500 }}>文本内容</div>
          <Button
            type="default"
            icon={<EditOutlined />}
            onClick={() => setTextEditorVisible(true)}
            style={{ width: "100%" }}
          >
            编辑文本
          </Button>
          <Typography.Paragraph
            style={{
              marginTop: 12,
              padding: 12,
              backgroundColor: "#f5f5f5",
              borderRadius: 4,
              maxHeight: 200,
              overflow: "auto",
              whiteSpace: "pre-wrap",
              wordBreak: "break-word",
            }}
          >
            {textEditorValue || "(空)"}
          </Typography.Paragraph>
        </div>

        <Divider />

        <div>
          <div style={{ marginBottom: 16, fontWeight: 500, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span>元数据</span>
            <Button
              type="dashed"
              icon={<PlusOutlined />}
              onClick={() => setAiGenerateVisible(!aiGenerateVisible)}
              disabled={!chunkId}
            >
              AI 生成策略
            </Button>
          </div>

          {/* AI 生成策略面板 */}
          {aiGenerateVisible && (
            <div style={{ marginBottom: 16, padding: 16, backgroundColor: "#f0f7ff", borderRadius: 4, border: "1px solid #91d5ff" }}>
              <Space direction="vertical" style={{ width: "100%" }} size="middle">
                <div>
                  <div style={{ marginBottom: 8, fontSize: 13, fontWeight: 500 }}>AI 模型</div>
                  <Select
                    placeholder="请选择AI模型"
                    value={aiModelId}
                    onChange={setAiModelId}
                    style={{ width: "100%" }}
                    options={modelList.map((model) => ({
                      label: model.name,
                      value: model.id,
                    }))}
                  />
                </div>
                <div>
                  <div style={{ marginBottom: 8, fontSize: 13, fontWeight: 500 }}>优化策略</div>
                  <Select
                    placeholder="请选择优化策略"
                    value={preprocessStrategyType}
                    onChange={setPreprocessStrategyType}
                    style={{ width: "100%" }}
                    options={PREPROCESS_STRATEGY_OPTIONS}
                  />
                </div>
                <Space style={{ width: "100%", justifyContent: "flex-end" }}>
                  <Button onClick={() => {
                    setAiGenerateVisible(false);
                    setAiModelId(null);
                    setPreprocessStrategyType(null);
                  }}>
                    取消
                  </Button>
                  <Button
                    type="primary"
                    loading={aiGenerating}
                    onClick={handleAiGenerate}
                    disabled={!aiModelId || !preprocessStrategyType}
                  >
                    生成
                  </Button>
                </Space>
              </Space>
            </div>
          )}
          
          {/* 新增元数据 */}
          <div style={{ marginBottom: 16, padding: 12, backgroundColor: "#fafafa", borderRadius: 4 }}>
            <Space.Compact style={{ width: "100%" }}>
              <Select
                placeholder="选择类型"
                value={newDerivativeType}
                onChange={setNewDerivativeType}
                style={{ width: 150 }}
                options={DERIVATIVE_TYPE_OPTIONS}
              />
              <Input
                placeholder="输入内容"
                value={newDerivativeContent}
                onChange={(e) => setNewDerivativeContent(e.target.value)}
                onPressEnter={handleAddDerivative}
                style={{ flex: 1 }}
              />
              <Button type="primary" icon={<PlusOutlined />} onClick={handleAddDerivative}>
                添加
              </Button>
            </Space.Compact>
          </div>

          {/* 元数据列表 */}
          <List
            dataSource={derivatives}
            locale={{ emptyText: "暂无元数据" }}
            renderItem={(item, index) => (
              <List.Item
                actions={[
                  <Button
                    key="edit"
                    type="link"
                    size="small"
                    icon={<EditOutlined />}
                    onClick={() => setEditingDerivativeIndex(index)}
                  >
                    编辑
                  </Button>,
                  <Popconfirm
                    key="delete"
                    title="确认删除"
                    description="确定要删除这个元数据吗？"
                    onConfirm={() => handleDeleteDerivative(index)}
                    okText="确认"
                    cancelText="取消"
                  >
                    <Button
                      type="link"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                    >
                      删除
                    </Button>
                  </Popconfirm>,
                ]}
              >
                {editingDerivativeIndex === index ? (
                  <Space.Compact style={{ width: "100%" }}>
                    <Select
                      value={item.derivativeType || null}
                      onChange={(value) => {
                        const updated = [...derivatives];
                        updated[index] = {
                          ...updated[index],
                          derivativeType: value,
                        };
                        setDerivatives(updated);
                      }}
                      style={{ width: 150 }}
                      options={DERIVATIVE_TYPE_OPTIONS}
                    />
                    <Input
                      value={item.derivativeContent || ""}
                      onChange={(e) => {
                        const updated = [...derivatives];
                        updated[index] = {
                          ...updated[index],
                          derivativeContent: e.target.value,
                        };
                        setDerivatives(updated);
                      }}
                      onPressEnter={() => setEditingDerivativeIndex(null)}
                      style={{ flex: 1 }}
                    />
                    <Button
                      type="primary"
                      icon={<CheckOutlined />}
                      onClick={() => setEditingDerivativeIndex(null)}
                    >
                      完成
                    </Button>
                  </Space.Compact>
                ) : (
                  <List.Item.Meta
                    title={
                      <Tag color="purple">
                        {DERIVATIVE_TYPE_MAP[item.derivativeType || ""] || item.derivativeType || "未知"}
                      </Tag>
                    }
                    description={
                      <Typography.Text
                        style={{
                          fontSize: 13,
                          color: "#666",
                          whiteSpace: "pre-wrap",
                          wordBreak: "break-word",
                        }}
                      >
                        {item.derivativeContent || "(空)"}
                      </Typography.Text>
                    }
                  />
                )}
              </List.Item>
            )}
          />
        </div>
      </Modal>

      {/* 文本编辑器 */}
      <CodeEditorModal
        open={textEditorVisible}
        initialValue={textEditorValue}
        language="plaintext"
        title="编辑文本内容"
        onClose={() => setTextEditorVisible(false)}
        onConfirm={handleTextEditorConfirm}
        width={1200}
        height="70vh"
      />
    </>
  );
};

