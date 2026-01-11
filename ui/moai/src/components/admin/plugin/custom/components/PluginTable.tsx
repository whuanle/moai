/**
 * 自定义插件表格组件
 */
import { useMemo } from "react";
import { Table, Typography, Tag, Space, Button, Tooltip, Popconfirm, Empty } from "antd";
import type { TableProps } from "antd";
import { EyeOutlined, EditOutlined, DeleteOutlined } from "@ant-design/icons";
import type { PluginBaseInfoItem, PluginClassifyItem } from "../../../../../apiClient/models";
import { PluginTypeObject } from "../../../../../apiClient/models";
import { formatDateTime } from "../../../../../helper/DateTimeHelper";
import type { TableSortState } from "../hooks/useCustomPluginData";

const PLUGIN_TYPE_MAP = {
  [PluginTypeObject.Mcp]: { color: "green", text: "MCP" },
  [PluginTypeObject.OpenApi]: { color: "orange", text: "OpenAPI" },
} as const;

interface PluginTableProps {
  dataSource: PluginBaseInfoItem[];
  loading: boolean;
  classifyList: PluginClassifyItem[];
  currentUserId?: number;
  sortState: TableSortState;
  onSortChange: (sortState: TableSortState) => void;
  onView: (record: PluginBaseInfoItem) => void;
  onEdit: (record: PluginBaseInfoItem) => void;
  onDelete: (pluginId: number) => void;
}

export default function PluginTable({
  dataSource,
  loading,
  classifyList,
  currentUserId,
  sortState,
  onSortChange,
  onView,
  onEdit,
  onDelete,
}: PluginTableProps) {
  // 处理表格排序变化
  const handleTableChange: TableProps<PluginBaseInfoItem>['onChange'] = (_, __, sorter) => {
    if (Array.isArray(sorter)) return;
    
    if (sorter.field && sorter.order) {
      onSortChange({
        field: sorter.field as string,
        order: sorter.order,
      });
    } else {
      onSortChange({ field: null, order: null });
    }
  };

  const columns = useMemo(
    () => [
      {
        title: "插件名称",
        dataIndex: "pluginName",
        key: "pluginName",
        width: 140,
        sorter: true,
        sortOrder: sortState.field === 'pluginName' ? sortState.order : null,
        render: (name: string) => (
          <Typography.Text strong style={{ color: "var(--color-primary)" }}>{name}</Typography.Text>
        ),
      },
      {
        title: "标题",
        dataIndex: "title",
        key: "title",
        width: 120,
        ellipsis: true,
        sorter: true,
        sortOrder: sortState.field === 'title' ? sortState.order : null,
        render: (title: string) => title || "-",
      },
      {
        title: "类型",
        dataIndex: "type",
        key: "type",
        width: 90,
        sorter: true,
        sortOrder: sortState.field === 'type' ? sortState.order : null,
        render: (type: string) => {
          const typeInfo = PLUGIN_TYPE_MAP[type as keyof typeof PLUGIN_TYPE_MAP] || { color: "default", text: type };
          return <Tag color={typeInfo.color}>{typeInfo.text}</Tag>;
        },
      },
      {
        title: "分类",
        dataIndex: "classifyId",
        key: "classifyId",
        width: 100,
        render: (classifyId: number | null | undefined) => {
          if (!classifyId) return <Typography.Text type="secondary">-</Typography.Text>;
          const classify = classifyList.find((item) => item.classifyId === classifyId);
          return classify ? <Tag color="blue">{classify.name}</Tag> : "-";
        },
      },
      {
        title: "服务器地址",
        dataIndex: "server",
        key: "server",
        width: 180,
        ellipsis: true,
        render: (server: string) => (
          <Typography.Text type="secondary" style={{ fontFamily: "monospace", fontSize: 12 }}>
            {server || "-"}
          </Typography.Text>
        ),
      },
      {
        title: "描述",
        dataIndex: "description",
        key: "description",
        ellipsis: true,
        render: (desc: string) => <Typography.Text type="secondary">{desc || "-"}</Typography.Text>,
      },
      {
        title: "公开",
        dataIndex: "isPublic",
        key: "isPublic",
        width: 80,
        render: (isPublic: boolean) => (
          <Tag color={isPublic ? "success" : "warning"}>{isPublic ? "公开" : "私有"}</Tag>
        ),
      },
      {
        title: "创建者",
        dataIndex: "createUserName",
        key: "createUserName",
        width: 100,
        ellipsis: true,
        render: (name: string) => name || "-",
      },
      {
        title: "创建时间",
        dataIndex: "createTime",
        key: "createTime",
        width: 160,
        render: (time: string) => {
          if (!time) return "-";
          try { return formatDateTime(time); } catch { return time; }
        },
      },
      {
        title: "操作",
        key: "action",
        width: 250,
        fixed: "right" as const,
        render: (_: unknown, record: PluginBaseInfoItem) => {
          const isOwner = currentUserId === record.createUserId;
          return (
            <Space size={3}>
              <Tooltip title="查看函数">
                <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => onView(record)}>查看</Button>
              </Tooltip>
              {isOwner && (
                <>
                  <Tooltip title="编辑">
                    <Button type="link" size="small" icon={<EditOutlined />} onClick={() => onEdit(record)}>编辑</Button>
                  </Tooltip>
                  <Popconfirm
                    title="删除插件"
                    description="确定要删除这个插件吗？"
                    okText="确认"
                    cancelText="取消"
                    onConfirm={() => onDelete(record.pluginId!)}
                  >
                    <Tooltip title="删除">
                      <Button type="link" size="small" danger icon={<DeleteOutlined />}>删除</Button>
                    </Tooltip>
                  </Popconfirm>
                </>
              )}
            </Space>
          );
        },
      },
    ],
    [classifyList, currentUserId, sortState, onView, onEdit, onDelete]
  );

  return (
    <Table
      columns={columns}
      dataSource={dataSource}
      rowKey="pluginId"
      loading={loading}
      pagination={false}
      scroll={{ x: 1200 }}
      onChange={handleTableChange}
      locale={{ emptyText: <Empty description="暂无插件数据" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
    />
  );
}
