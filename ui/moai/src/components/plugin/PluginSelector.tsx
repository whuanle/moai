import React, { useState, useEffect, useCallback } from "react";
import {
  Modal,
  List,
  Avatar,
  Typography,
  Space,
  Spin,
  Empty,
  message,
  Tag,
  Button,
  Checkbox,
} from "antd";
import { 
  ApiOutlined, 
  CheckCircleOutlined,
  InfoCircleOutlined 
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import {
  QueryPluginBaseListCommand,
  PluginBaseInfoItem,
} from "../../apiClient/models";

const { Title, Text } = Typography;

interface PluginSelectorProps {
  visible: boolean;
  onCancel: () => void;
  onSelect: (pluginIds: number[]) => void;
  selectedPluginIds?: number[];
}

const PluginSelector: React.FC<PluginSelectorProps> = ({
  visible,
  onCancel,
  onSelect,
  selectedPluginIds = [],
}) => {
  const [plugins, setPlugins] = useState<PluginBaseInfoItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>(selectedPluginIds);
  const [messageApi, contextHolder] = message.useMessage();

  // 加载插件列表
  const loadPluginList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestData: QueryPluginBaseListCommand = {};
      const response = await client.api.plugin.plugin_list.post(requestData);

      if (response?.items) {
        setPlugins(response.items);
      } else {
        setPlugins([]);
      }
    } catch (error) {
      console.error("加载插件列表失败:", error);
      messageApi.error("加载插件列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 组件显示时加载数据
  useEffect(() => {
    if (visible) {
      loadPluginList();
      setSelectedIds(selectedPluginIds);
    }
  }, [visible, loadPluginList, selectedPluginIds]);

  // 处理插件选择
  const handlePluginToggle = (pluginId: number, checked: boolean) => {
    if (checked) {
      if (selectedIds.length >= 3) {
        messageApi.warning("最多只能选择3个插件");
        return;
      }
      setSelectedIds([...selectedIds, pluginId]);
    } else {
      setSelectedIds(selectedIds.filter(id => id !== pluginId));
    }
  };

  // 确认选择
  const handleConfirm = () => {
    onSelect(selectedIds);
    onCancel();
  };

  // 渲染插件列表项
  const renderPluginItem = (plugin: PluginBaseInfoItem) => {
    const isSelected = selectedIds.includes(plugin.pluginId!);
    
    return (
      <List.Item
        className={`plugin-item ${isSelected ? "selected" : ""}`}
        style={{
          padding: "12px 16px",
          borderRadius: "8px",
          marginBottom: "8px",
          border: isSelected ? "2px solid #1890ff" : "1px solid #f0f0f0",
          backgroundColor: isSelected ? "#f6ffed" : "white",
          transition: "all 0.2s",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", width: "100%" }}>
          <Checkbox
            checked={isSelected}
            onChange={(e) => handlePluginToggle(plugin.pluginId!, e.target.checked)}
            style={{ marginRight: "12px" }}
          />
          
          <Avatar
            size="large"
            icon={<ApiOutlined />}
            style={{
              backgroundColor: isSelected ? "#1890ff" : "#52c41a",
              marginRight: "12px",
            }}
          />
          
          <div style={{ flex: 1 }}>
            <div style={{ display: "flex", alignItems: "center", marginBottom: "4px" }}>
              <Text
                strong
                style={{
                  color: isSelected ? "#1890ff" : "#262626",
                  marginRight: "8px",
                }}
              >
                {plugin.pluginName || "未命名插件"}
              </Text>
              {isSelected && (
                <CheckCircleOutlined style={{ color: "#52c41a", fontSize: "16px" }} />
              )}
            </div>
            
            <Text type="secondary" style={{ fontSize: "12px", display: "block", marginBottom: "4px" }}>
              {plugin.description || "暂无描述"}
            </Text>
            
            <Space size="small">
              <Tag color="blue" icon={<ApiOutlined />}>
                插件
              </Tag>
              {plugin.isPublic ? (
                <Tag color="green">公开</Tag>
              ) : (
                <Tag color="orange">私有</Tag>
              )}
            </Space>
          </div>
        </div>
      </List.Item>
    );
  };

  return (
    <>
      {contextHolder}
      <Modal
        title="选择插件"
        open={visible}
        onCancel={onCancel}
        footer={[
          <Button key="cancel" onClick={onCancel}>
            取消
          </Button>,
          <Button 
            key="confirm" 
            type="primary" 
            onClick={handleConfirm}
            disabled={selectedIds.length === 0}
          >
            确认选择 ({selectedIds.length}/3)
          </Button>,
        ]}
        width={700}
        bodyStyle={{ maxHeight: "60vh", overflow: "auto" }}
      >
        <div>
          <div style={{ marginBottom: "16px" }}>
            <Space>
              <InfoCircleOutlined style={{ color: "#1890ff" }} />
              <Text type="secondary" style={{ fontSize: "14px" }}>
                选择插件来增强AI助手的功能，最多可选择3个插件
              </Text>
            </Space>
          </div>
          
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>
              <Spin size="large" />
            </div>
          ) : plugins.length === 0 ? (
            <Empty
              description="暂无可用插件"
              style={{ marginTop: "40px" }}
            />
          ) : (
            <List
              dataSource={plugins}
              renderItem={renderPluginItem}
              style={{ marginTop: "16px" }}
            />
          )}
        </div>
      </Modal>
    </>
  );
};

export default PluginSelector; 