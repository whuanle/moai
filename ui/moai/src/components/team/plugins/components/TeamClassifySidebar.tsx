/**
 * 团队插件分类侧边栏组件
 */
import { List, Space, Typography, Tag } from "antd";
import { AppstoreOutlined, FolderOutlined } from "@ant-design/icons";
import type { PluginClassifyItem } from "../../../../apiClient/models";

interface ClassifyItem {
  key: number | "all";
  name: string;
  count: number;
}

interface TeamClassifySidebarProps {
  classifyList: PluginClassifyItem[];
  allPluginCount: number;
  pluginCountByClassify: Record<number, number>;
  selectedClassify: number | "all";
  onSelect: (key: number | "all") => void;
}

export default function TeamClassifySidebar({
  classifyList,
  allPluginCount,
  pluginCountByClassify,
  selectedClassify,
  onSelect,
}: TeamClassifySidebarProps) {
  const dataSource: ClassifyItem[] = [
    { key: "all", name: "全部插件", count: allPluginCount },
    ...classifyList
      .filter((item) => item.classifyId != null)
      .map((item) => ({
        key: item.classifyId!,
        name: item.name || "",
        count: pluginCountByClassify[item.classifyId!] || 0,
      })),
  ];

  return (
    <div className="team-plugin-classify-sidebar">
      <div className="classify-sidebar-header">
        <Typography.Text className="classify-sidebar-title">
          插件分类
        </Typography.Text>
      </div>
      <List
        size="small"
        dataSource={dataSource}
        renderItem={(item) => {
          const isSelected = selectedClassify === item.key;
          const isAll = item.key === "all";
          return (
            <List.Item
              className={isSelected ? "selected" : ""}
              onClick={() => onSelect(item.key)}
            >
              <div className="classify-item-content">
                <Space>
                  {isAll ? (
                    <AppstoreOutlined className="classify-item-icon" />
                  ) : (
                    <FolderOutlined className="classify-item-icon" />
                  )}
                  <Typography.Text strong={isSelected}>
                    {item.name}
                  </Typography.Text>
                </Space>
                <Tag 
                  color={isSelected ? "blue" : "default"}
                  style={{ 
                    borderRadius: 10,
                    minWidth: 32,
                    textAlign: 'center'
                  }}
                >
                  {item.count}
                </Tag>
              </div>
            </List.Item>
          );
        }}
      />
    </div>
  );
}
