/**
 * 批量生成策略模态窗口组件
 * 用于批量生成多个文本块的元数据
 */

import React, { useState, useCallback, useEffect } from "react";
import {
  Modal,
  Button,
  Select,
  Space,
  Checkbox,
  Typography,
  Tag,
  message,
} from "antd";
import { DeleteOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { BatchGenerateModalProps } from "../types";
import type { 
  WikiDocumentDerivativeItem,
  PreprocessStrategyType,
  AddWikiDocumentDerivativeItem,
  KeyValueOfInt64AndString,
} from "../../../../apiClient/models";
import {
  PREPROCESS_STRATEGY_OPTIONS,
  DERIVATIVE_TYPE_MAP,
} from "../constants";
import { extractBatchDerivativesFromResponse } from "../utils/aiGenerationHelper";

/**
 * 批量生成策略模态窗口组件
 */
export const BatchGenerateModal: React.FC<BatchGenerateModalProps> = ({
  open,
  previewData,
  modelList,
  onClose,
  onGenerate,
  onSave,
}) => {
  const [selectedChunkIds, setSelectedChunkIds] = useState<Set<string>>(new Set());
  const [aiModelId, setAiModelId] = useState<number | null>(null);
  const [preprocessStrategyType, setPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [generating, setGenerating] = useState(false);
  const [generateResults, setGenerateResults] = useState<Map<string, WikiDocumentDerivativeItem[]>>(new Map());
  const [messageApi, contextHolder] = message.useMessage();

  // 初始化选中所有块
  React.useEffect(() => {
    if (open && previewData && previewData.items.length > 0) {
      setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId)));
    }
  }, [open, previewData]);

  /**
   * 处理批量生成
   */
  const handleGenerate = useCallback(async () => {
    if (!aiModelId) {
      messageApi.warning("请选择AI模型");
      return;
    }
    if (!preprocessStrategyType) {
      messageApi.warning("请选择优化策略");
      return;
    }
    if (selectedChunkIds.size === 0) {
      messageApi.warning("请至少选择一个文本块");
      return;
    }
    if (!previewData) {
      messageApi.warning("没有可用的文本块");
      return;
    }

    try {
      setGenerating(true);
      const results = await onGenerate(selectedChunkIds, aiModelId, preprocessStrategyType);
      setGenerateResults(results);
      messageApi.success(`成功生成 ${results.size} 个文本块的元数据`);
    } catch (error) {
      console.error("Failed to batch generate derivatives:", error);
      proxyRequestError(error, messageApi, "批量生成失败");
    } finally {
      setGenerating(false);
    }
  }, [aiModelId, preprocessStrategyType, selectedChunkIds, previewData, messageApi, onGenerate]);

  /**
   * 处理保存结果
   */
  const handleSave = useCallback(async () => {
    if (generateResults.size === 0) {
      messageApi.warning("没有可保存的结果");
      return;
    }

    try {
      await onSave(generateResults, aiModelId);
      // 清空状态
      setGenerateResults(new Map());
      setSelectedChunkIds(new Set());
      setAiModelId(null);
      setPreprocessStrategyType(null);
      onClose();
    } catch (error) {
      console.error("Failed to save batch results:", error);
      proxyRequestError(error, messageApi, "保存结果失败");
    }
  }, [generateResults, aiModelId, messageApi, onSave, onClose]);

  /**
   * 处理关闭
   */
  const handleClose = useCallback(() => {
    setGenerateResults(new Map());
    setSelectedChunkIds(new Set());
    setAiModelId(null);
    setPreprocessStrategyType(null);
    onClose();
  }, [onClose]);

  return (
    <>
      {contextHolder}
      <Modal
        title="批量生成策略"
        open={open}
        onCancel={handleClose}
        width={800}
        footer={[
          <Button key="cancel" onClick={handleClose}>
            取消
          </Button>,
          generateResults.size > 0 && (
            <Button key="save" type="primary" onClick={handleSave}>
              保存结果
            </Button>
          ),
          <Button
            key="generate"
            type="primary"
            loading={generating}
            onClick={handleGenerate}
            disabled={!aiModelId || !preprocessStrategyType || selectedChunkIds.size === 0}
          >
            生成
          </Button>,
        ].filter(Boolean)}
        destroyOnClose
      >
        <Space direction="vertical" style={{ width: "100%" }} size="large">
          {/* AI 模型选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>AI 模型</div>
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

          {/* 优化策略选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>优化策略</div>
            <Select
              placeholder="请选择优化策略"
              value={preprocessStrategyType}
              onChange={setPreprocessStrategyType}
              style={{ width: "100%" }}
              options={PREPROCESS_STRATEGY_OPTIONS}
            />
          </div>

          {/* 块列表选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>
              选择文本块 ({selectedChunkIds.size} / {previewData?.items.length || 0})
            </div>
            <div style={{ marginBottom: 8 }}>
              <Space>
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    if (previewData) {
                      setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId)));
                    }
                  }}
                >
                  全选
                </Button>
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    setSelectedChunkIds(new Set());
                  }}
                >
                  全不选
                </Button>
              </Space>
            </div>
            <div
              style={{
                maxHeight: "300px",
                overflow: "auto",
                border: "1px solid #d9d9d9",
                borderRadius: "4px",
                padding: "8px",
              }}
            >
              <Space direction="vertical" style={{ width: "100%" }} size="small">
                {previewData?.items.map((item) => (
                  <div key={item.chunkId} style={{ display: "flex", alignItems: "flex-start" }}>
                    <Checkbox
                      checked={selectedChunkIds.has(item.chunkId)}
                      onChange={(e) => {
                        const newSelected = new Set(selectedChunkIds);
                        if (e.target.checked) {
                          newSelected.add(item.chunkId);
                        } else {
                          newSelected.delete(item.chunkId);
                        }
                        setSelectedChunkIds(newSelected);
                      }}
                    >
                      <div style={{ marginLeft: 8, flex: 1 }}>
                        <div style={{ fontWeight: 500, marginBottom: 4 }}>
                          #{item.order + 1} - {item.chunkId.substring(0, 20)}...
                        </div>
                        <Typography.Text
                          type="secondary"
                          style={{ fontSize: 12 }}
                          ellipsis={{ tooltip: item.text || "(空)" }}
                        >
                          {item.text || "(空)"}
                        </Typography.Text>
                      </div>
                    </Checkbox>
                  </div>
                ))}
              </Space>
            </div>
          </div>

          {/* 生成结果预览 */}
          {generateResults.size > 0 && (
            <div>
              <div style={{ marginBottom: 8, fontWeight: 500 }}>
                生成结果 ({generateResults.size} 个文本块)
              </div>
              <div
                style={{
                  maxHeight: "300px",
                  overflow: "auto",
                  border: "1px solid #d9d9d9",
                  borderRadius: "4px",
                  padding: "8px",
                  backgroundColor: "#fafafa",
                }}
              >
                <Space direction="vertical" style={{ width: "100%" }} size="small">
                  {Array.from(generateResults.entries()).map(([chunkId, derivatives]) => {
                    const item = previewData?.items.find(i => i.chunkId === chunkId);
                    return (
                      <div
                        key={chunkId}
                        style={{
                          padding: "8px",
                          backgroundColor: "white",
                          borderRadius: "4px",
                          border: "1px solid #e8e8e8",
                        }}
                      >
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>
                          #{item?.order !== undefined ? item.order + 1 : "?"} - {chunkId.substring(0, 20)}...
                        </div>
                        <Space direction="vertical" size="small" style={{ width: "100%" }}>
                          {derivatives.map((derivative, idx) => (
                            <div
                              key={idx}
                              style={{
                                padding: "4px 8px",
                                backgroundColor: "#f5f5f5",
                                borderRadius: "4px",
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "flex-start",
                              }}
                            >
                              <div style={{ flex: 1, minWidth: 0 }}>
                                <Tag color="purple" style={{ marginRight: 8 }}>
                                  {DERIVATIVE_TYPE_MAP[derivative.derivativeType || ""] || derivative.derivativeType || "未知"}
                                </Tag>
                                <Typography.Text
                                  style={{ fontSize: 12 }}
                                  ellipsis={{ tooltip: derivative.derivativeContent || "(空)" }}
                                >
                                  {derivative.derivativeContent || "(空)"}
                                </Typography.Text>
                              </div>
                              <Button
                                type="text"
                                danger
                                size="small"
                                icon={<DeleteOutlined />}
                                onClick={() => {
                                  const newResults = new Map(generateResults);
                                  const currentDerivatives = newResults.get(chunkId) || [];
                                  const updatedDerivatives = currentDerivatives.filter((_, index) => index !== idx);
                                  
                                  if (updatedDerivatives.length > 0) {
                                    newResults.set(chunkId, updatedDerivatives);
                                  } else {
                                    newResults.delete(chunkId);
                                  }
                                  
                                  setGenerateResults(newResults);
                                }}
                                style={{ marginLeft: 8, flexShrink: 0 }}
                              />
                            </div>
                          ))}
                        </Space>
                      </div>
                    );
                  })}
                </Space>
              </div>
            </div>
          )}
        </Space>
      </Modal>
    </>
  );
};

