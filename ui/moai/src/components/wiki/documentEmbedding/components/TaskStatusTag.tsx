/**
 * 任务状态标签组件
 * 用于显示任务状态并应用相应的颜色
 */

import { Tag } from "antd";
import { TASK_STATUS_COLOR_MAP } from "../constants";

interface TaskStatusTagProps {
  state: string;
}

/**
 * 任务状态标签组件
 * 根据任务状态显示不同颜色的标签
 */
export const TaskStatusTag: React.FC<TaskStatusTagProps> = ({ state }) => {
  const getStatusColor = (state: string): string => {
    const lowerState = state.toLowerCase();
    return TASK_STATUS_COLOR_MAP[lowerState] || "default";
  };

  return <Tag color={getStatusColor(state)}>{state}</Tag>;
};

