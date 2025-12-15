/**
 * 切割预览卡片组件
 * 用于显示单个文本块及其衍生内容
 */

import { Card, Button, Space, Tag, Typography, Popconfirm } from "antd";
import {
  DragOutlined,
  EditOutlined,
  DeleteOutlined,
  UpOutlined,
  DownOutlined,
} from "@ant-design/icons";
import type { PartitionPreviewItem } from "../types";
import type { WikiDocumentDerivativeItem } from "../../../../apiClient/models";
import { DERIVATIVE_TYPE_MAP } from "../constants";

interface PartitionPreviewCardProps {
  item: PartitionPreviewItem;
  index: number;
  isDragging: boolean;
  isDragOver: boolean;
  isExpanded: boolean;
  onDragStart: (e: React.DragEvent, index: number) => void;
  onDragOver: (e: React.DragEvent, index: number) => void;
  onDragLeave: (e: React.DragEvent) => void;
  onDragEnd: () => void;
  onDrop: (e: React.DragEvent, index: number) => void;
  onEdit: (chunkId: string, text: string, derivatives?: WikiDocumentDerivativeItem[] | null) => void;
  onDelete: (chunkId: string) => void;
  onToggleExpand: (chunkId: string) => void;
}

/**
 * 切割预览卡片组件
 * 显示文本块内容、衍生内容，支持拖拽排序和编辑操作
 */
export const PartitionPreviewCard: React.FC<PartitionPreviewCardProps> = ({
  item,
  index,
  isDragging,
  isDragOver,
  isExpanded,
  onDragStart,
  onDragOver,
  onDragLeave,
  onDragEnd,
  onDrop,
  onEdit,
  onDelete,
  onToggleExpand,
}) => {
  const displayText = item.text || "(空)";
  const hasDerivatives = item.derivatives && Array.isArray(item.derivatives) && item.derivatives.length > 0;

  // 获取衍生类型显示文本
  const getDerivativeTypeLabel = (derivative: any): string => {
    const type = derivative?.derivativeType 
      ?? derivative?.DerivativeType 
      ?? derivative?.type 
      ?? null;
    
    if (!type) return "未知";
    
    // 尝试直接匹配
    if (DERIVATIVE_TYPE_MAP[type]) {
      return DERIVATIVE_TYPE_MAP[type];
    }
    
    // 尝试转换为字符串后匹配
    const typeStr = String(type);
    if (DERIVATIVE_TYPE_MAP[typeStr]) {
      return DERIVATIVE_TYPE_MAP[typeStr];
    }
    
    // 尝试小写匹配
    const typeLower = typeStr.toLowerCase();
    if (DERIVATIVE_TYPE_MAP[typeLower]) {
      return DERIVATIVE_TYPE_MAP[typeLower];
    }
    
    return typeStr;
  };

  // 获取衍生内容
  const getDerivativeContent = (derivative: any): string => {
    return derivative?.derivativeContent 
      ?? derivative?.DerivativeContent 
      ?? derivative?.content 
      ?? "";
  };

  return (
    <Card
      key={item.chunkId}
      size="small"
      draggable
      onDragStart={(e) => onDragStart(e, index)}
      onDragOver={(e) => onDragOver(e, index)}
      onDragLeave={onDragLeave}
      onDragEnd={onDragEnd}
      onDrop={(e) => onDrop(e, index)}
      title={
        <Space style={{ width: "100%", justifyContent: "space-between" }}>
          <Space>
            <DragOutlined
              style={{
                cursor: "move",
                color: "#8c8c8c",
              }}
            />
            <Tag color="blue">#{item.order + 1}</Tag>
            <Tag color="default" title={item.chunkId || "未设置"}>
              ID: {item.chunkId 
                ? (item.chunkId.length > 20 ? `${item.chunkId.substring(0, 20)}...` : item.chunkId)
                : "N/A"}
            </Tag>
          </Space>
          <Space>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                onEdit(item.chunkId, item.text, item.derivatives);
              }}
            >
              编辑
            </Button>
            <Popconfirm
              title="确认删除"
              description="确定要删除这个文本块吗？"
              onConfirm={(e) => {
                e?.stopPropagation();
                onDelete(item.chunkId);
              }}
              onCancel={(e) => {
                e?.stopPropagation();
              }}
              okText="确认"
              cancelText="取消"
            >
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                }}
              >
                删除
              </Button>
            </Popconfirm>
          </Space>
        </Space>
      }
      style={{
        marginBottom: 0,
        cursor: isDragging ? "grabbing" : "grab",
        opacity: isDragging ? 0.5 : 1,
        border: isDragOver ? "2px dashed #1890ff" : "1px solid #d9d9d9",
        backgroundColor: isDragOver ? "#e6f7ff" : "white",
        transition: "all 0.2s",
      }}
      bodyStyle={{ padding: "16px" }}
    >
      {/* 文本内容 */}
      <div style={{ marginBottom: hasDerivatives ? 16 : 0 }}>
        <Typography.Text
          type="secondary"
          style={{ fontSize: 12, marginBottom: 8, display: "block" }}
        >
          文本内容
        </Typography.Text>
        <Typography.Paragraph
          style={{
            fontSize: "14px",
            lineHeight: "1.6",
            whiteSpace: "pre-wrap",
            wordBreak: "break-word",
            marginBottom: 0,
            maxHeight: "200px",
            overflow: "auto",
            padding: "8px",
            backgroundColor: "#fafafa",
            borderRadius: "4px",
          }}
          ellipsis={{ rows: 8, expandable: false }}
        >
          {displayText}
        </Typography.Paragraph>
      </div>

      {/* 衍生内容 */}
      {hasDerivatives && item.derivatives && (
        <div style={{ marginTop: 16, paddingTop: 16, borderTop: "1px solid #f0f0f0" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <Typography.Text
              type="secondary"
              style={{ fontSize: 12 }}
            >
              衍生内容 ({item.derivatives.length})
            </Typography.Text>
            <Button
              type="text"
              size="small"
              icon={isExpanded ? <UpOutlined /> : <DownOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                onToggleExpand(item.chunkId);
              }}
              style={{ padding: 0, height: "auto" }}
            >
              {isExpanded ? "收起" : "展开"}
            </Button>
          </div>
          {isExpanded && (
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              {item.derivatives.map((derivative: any, idx: number) => (
                <div
                  key={idx}
                  style={{
                    padding: "8px 12px",
                    backgroundColor: "#f5f5f5",
                    borderRadius: "4px",
                    border: "1px solid #e8e8e8",
                  }}
                >
                  <Tag
                    color="purple"
                    style={{ marginBottom: 6, marginRight: 0 }}
                  >
                    {getDerivativeTypeLabel(derivative)}
                  </Tag>
                  <Typography.Text
                    style={{
                      fontSize: "13px",
                      color: "#666",
                      display: "block",
                      whiteSpace: "pre-wrap",
                      wordBreak: "break-word",
                    }}
                  >
                    {getDerivativeContent(derivative) || "(空)"}
                  </Typography.Text>
                </div>
              ))}
            </Space>
          )}
        </div>
      )}
    </Card>
  );
};

