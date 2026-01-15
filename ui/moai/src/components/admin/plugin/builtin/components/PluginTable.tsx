/**
 * 插件表格组件
 * 显示插件列表，支持操作按钮和表头排序
 */
import { useMemo } from "react";
import {
  Table,
  Button,
  Space,
  Typography,
  Tag,
  Tooltip,
  Empty,
  Popconfirm,
} from "antd";
import type { TableProps } from "antd";
import {
  EditOutlined,
  PlayCircleOutlined,
  DeleteOutlined,
} from "@ant-design/icons";
import type {
  NativePluginInfo,
  NativePluginTemplateInfo,
} from "../../../../../apiClient/models";
import { PluginTypeObject } from "../../../../../apiClient/models";
import { formatDateTime } from "../../../../../helper/DateTimeHelper";
import type { TableSortState } from "../hooks/usePluginData";

interface PluginTableProps {
  dataSource: NativePluginInfo[];
  loading: boolean;
  sortState: TableSortState;
  onSortChange: (sortState: TableSortState) => void;
  onEdit: (record: NativePluginInfo) => void;
  onRun: (record: NativePluginInfo) => void;
  onDelete: (pluginId: number) => void;
  onRunTemplate?: (template: NativePluginTemplateInfo) => void;
}

export default function PluginTable({
  dataSource,
  loading,
  sortState,
  onSortChange,
  onEdit,
  onRun,
  onDelete,
  onRunTemplate,
}: PluginTableProps) {
  // 处理表格排序变化
  const handleTableChange: TableProps<NativePluginInfo>['onChange'] = (_, __, sorter) => {
    if (Array.isArray(sorter)) return; // 不处理多列排序
    
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
        width: 160,
        sorter: true,
        sortOrder: sortState.field === 'pluginName' ? sortState.order : null,
        render: (
          pluginName: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return (
              <Typography.Text strong className="plugin-name-text">
                {(record as NativePluginTemplateInfo).name || "-"}
              </Typography.Text>
            );
          }
          return (
            <Typography.Text strong className="plugin-name-text">
              {pluginName}
            </Typography.Text>
          );
        },
      },
      {
        title: "标题",
        dataIndex: "title",
        key: "title",
        width: 160,
        sorter: true,
        sortOrder: sortState.field === 'title' ? sortState.order : null,
        render: (
          title: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return (record as NativePluginTemplateInfo).name || "-";
          }
          return title || "-";
        },
      },
      {
        title: "模板Key",
        dataIndex: "templatePluginKey",
        key: "templatePluginKey",
        width: 180,
        sorter: true,
        sortOrder: sortState.field === 'templatePluginKey' ? sortState.order : null,
        render: (
          templatePluginKey: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return (
              <Typography.Text type="secondary" className="plugin-key-text">
                {(record as NativePluginTemplateInfo).key || "-"}
              </Typography.Text>
            );
          }
          return (
            <Typography.Text type="secondary" className="plugin-key-text">
              {templatePluginKey || "-"}
            </Typography.Text>
          );
        },
      },
      {
        title: "描述",
        dataIndex: "description",
        key: "description",
        width: 280,
        ellipsis: { showTitle: false },
        render: (description: string) => (
          <Tooltip placement="topLeft" title={description || "-"}>
            <Typography.Text
              type="secondary"
              className="plugin-desc-text"
              ellipsis
            >
              {description || "-"}
            </Typography.Text>
          </Tooltip>
        ),
      },
      {
        title: "是否公开",
        dataIndex: "isPublic",
        key: "isPublic",
        width: 100,
        render: (
          isPublic: boolean,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return "-";
          }
          return (
            <Tag color={isPublic ? "green" : "orange"}>
              {isPublic ? "公开" : "私有"}
            </Tag>
          );
        },
      },
      {
        title: "使用量",
        dataIndex: "counter",
        key: "counter",
        width: 100,
        sorter: true,
        sortOrder: sortState.field === 'counter' ? sortState.order : null,
        render: (
          counter: number,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return "-";
          }
          return counter ?? 0;
        },
      },
      {
        title: "创建时间",
        dataIndex: "createTime",
        key: "createTime",
        width: 160,
        render: (
          createTime: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return "-";
          }
          if (!createTime) return "-";
          try {
            return formatDateTime(createTime);
          } catch {
            return createTime;
          }
        },
      },
      {
        title: "创建人",
        dataIndex: "createUserName",
        key: "createUserName",
        width: 100,
        render: (
          createUserName: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          if (!("pluginId" in record)) {
            return "-";
          }
          return createUserName || "-";
        },
      },
      {
        title: "操作",
        key: "action",
        width: 230,
        fixed: "right" as const,
        render: (
          _: any,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          const isTemplate = !("pluginId" in record);
          const template = record as NativePluginTemplateInfo;
          const plugin = record as NativePluginInfo;
          const isToolPlugin = plugin.pluginType === PluginTypeObject.ToolPlugin;

          return (
            <Space size={4}>
              {isTemplate &&
                template.pluginType === PluginTypeObject.ToolPlugin && (
                  <Tooltip title="运行测试">
                    <Button
                      type="text"
                      size="small"
                      icon={<PlayCircleOutlined />}
                      onClick={() => onRunTemplate?.(template)}
                      style={{ color: '#52c41a' }}
                    >
                      运行
                    </Button>
                  </Tooltip>
                )}
              {!isTemplate && (
                <>
                  <Tooltip title="运行测试">
                    <Button
                      type="text"
                      size="small"
                      icon={<PlayCircleOutlined />}
                      onClick={() => onRun(plugin)}
                      style={{ color: '#52c41a' }}
                    >
                      运行
                    </Button>
                  </Tooltip>
                  <Tooltip title={isToolPlugin ? "编辑分类" : "编辑插件"}>
                    <Button
                      type="text"
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => onEdit(plugin)}
                      style={{ color: '#1677ff' }}
                    >
                      编辑
                    </Button>
                  </Tooltip>
                  {!isToolPlugin && (
                    <Popconfirm
                      title="删除插件"
                      description="确定要删除这个插件吗？删除后无法恢复。"
                      okText="确认删除"
                      cancelText="取消"
                      onConfirm={() => onDelete(plugin.pluginId!)}
                      okButtonProps={{ danger: true }}
                    >
                      <Tooltip title="删除插件">
                        <Button
                          type="text"
                          size="small"
                          danger
                          icon={<DeleteOutlined />}
                        >
                          删除
                        </Button>
                      </Tooltip>
                    </Popconfirm>
                  )}
                </>
              )}
            </Space>
          );
        },
      },
    ],
    [onEdit, onRun, onDelete, onRunTemplate, sortState]
  );

  return (
    <div className="plugin-table">
      <Table
        columns={columns}
        dataSource={dataSource}
        rowKey={(record) =>
          (record as NativePluginInfo).pluginId?.toString() || ""
        }
        loading={loading}
        pagination={false}
        scroll={{ x: 1300, y: 'calc(100vh - 340px)' }}
        onChange={handleTableChange}
        locale={{
          emptyText: (
            <Empty
              description="暂无内置插件数据"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              className="plugin-empty"
            />
          ),
        }}
      />
    </div>
  );
}
