/**
 * 文档嵌入模块共享组件
 */

import { Select, Tag } from "antd";
import type { AiModelItem } from "./types";

/**
 * 表单字段包装器
 */
interface FormFieldProps {
  label: string;
  description?: string;
  children: React.ReactNode;
}

export const FormField: React.FC<FormFieldProps> = ({ label, description, children }) => (
  <div className="doc-embed-field">
    <div className="doc-embed-field-label">{label}</div>
    {description && <div className="doc-embed-field-desc">{description}</div>}
    {children}
  </div>
);

/**
 * AI 模型选择器
 */
interface AiModelSelectProps {
  value?: number | null;
  onChange?: (value: number) => void;
  modelList: AiModelItem[];
  loading?: boolean;
  placeholder?: string;
  allowClear?: boolean;
  style?: React.CSSProperties;
}

export const AiModelSelect: React.FC<AiModelSelectProps> = ({
  value,
  onChange,
  modelList,
  loading,
  placeholder = "请选择AI模型",
  allowClear = false,
  style,
}) => (
  <Select
    value={value}
    onChange={onChange}
    placeholder={placeholder}
    loading={loading}
    allowClear={allowClear}
    style={style}
    options={modelList.map((model) => ({
      label: model.name,
      value: model.id,
    }))}
  />
);

/**
 * 任务状态标签
 */
interface TaskStatusTagProps {
  state: string;
}

export const TaskStatusTag: React.FC<TaskStatusTagProps> = ({ state }) => {
  const getStatusColor = (state: string) => {
    if (!state) return "default";
    const lowerState = state.toLowerCase();
    switch (lowerState) {
      case "completed":
        return "success";
      case "processing":
        return "processing";
      case "failed":
        return "error";
      case "wait":
        return "warning";
      default:
        return "default";
    }
  };

  return <Tag color={getStatusColor(state)}>{state || "未知"}</Tag>;
};
