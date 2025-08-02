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
  Tabs,
} from "antd";
import { 
  Bot, 
  User, 
  CheckCircle,
  Info 
} from "lucide-react";
import { GetApiClient } from "../ServiceClient";
import {
  QuerySystemAiModelListCommand,
  AiNotKeyEndpoint,
  QueryUserAiModelListRequest,
  AiModelType,
} from "../../apiClient/models";

const { Title, Text } = Typography;

interface ModelSelectorProps {
  visible: boolean;
  onCancel: () => void;
  onSelect: (modelId: number, modelName: string) => void;
  selectedModelId?: number | null;
  modelType?: AiModelType; // 模型类型，如 "chat", "embedding" 等
}

const ModelSelector: React.FC<ModelSelectorProps> = ({
  visible,
  onCancel,
  onSelect,
  selectedModelId,
  modelType = "chat" as AiModelType, // 默认值为 "chat"
}) => {
  const [systemModels, setSystemModels] = useState<AiNotKeyEndpoint[]>([]);
  const [userModels, setUserModels] = useState<AiNotKeyEndpoint[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 加载系统模型列表
  const loadSystemModels = useCallback(async () => {
    try {
      const client = GetApiClient();
      const command: QuerySystemAiModelListCommand = {
        aiModelType: modelType
      };
      const response = await client.api.aimodel.type.system_modellist.post(command);

      if (response?.aiModels) {
        setSystemModels(response.aiModels);
      } else {
        setSystemModels([]);
      }
    } catch (error) {
      console.error("加载系统模型列表失败:", error);
      messageApi.error("加载系统模型列表失败");
    }
  }, [messageApi, modelType]);

  // 加载用户模型列表
  const loadUserModels = useCallback(async () => {
    try {
      const client = GetApiClient();
      const command: QueryUserAiModelListRequest = {
        aiModelType: modelType
      };
      const response = await client.api.aimodel.type.user_modellist.post(command);

      if (response?.aiModels) {
        setUserModels(response.aiModels);
      } else {
        setUserModels([]);
      }
    } catch (error) {
      console.error("加载用户模型列表失败:", error);
      messageApi.error("加载用户模型列表失败");
    }
  }, [messageApi, modelType]);

  // 加载所有模型列表
  const loadAllModels = useCallback(async () => {
    setLoading(true);
    await Promise.all([loadSystemModels(), loadUserModels()]);
    setLoading(false);
  }, [loadSystemModels, loadUserModels]);

  // 组件显示时加载数据
  useEffect(() => {
    if (visible) {
      loadAllModels();
    }
  }, [visible, loadAllModels]);

  // 处理模型选择
  const handleModelSelect = (model: AiNotKeyEndpoint) => {
    onSelect(model.id!, model.name || "未命名模型");
    onCancel();
  };

  // 渲染模型列表项
  const renderModelItem = (model: AiNotKeyEndpoint, isSystem: boolean = false) => {
    const isSelected = selectedModelId === model.id;
    
    return (
      <List.Item
        className={`model-item ${isSelected ? "selected" : ""}`}
        onClick={() => handleModelSelect(model)}
        style={{
          cursor: "pointer",
          padding: "12px 16px",
          borderRadius: "8px",
          marginBottom: "8px",
          border: isSelected ? "2px solid #1890ff" : "1px solid #f0f0f0",
          backgroundColor: isSelected ? "#f6ffed" : "white",
          transition: "all 0.2s",
        }}
        onMouseEnter={(e) => {
          if (!isSelected) {
            e.currentTarget.style.backgroundColor = "#f5f5f5";
          }
        }}
        onMouseLeave={(e) => {
          if (!isSelected) {
            e.currentTarget.style.backgroundColor = "white";
          }
        }}
      >
        <List.Item.Meta
          avatar={
            <Avatar
              size="large"
              icon={isSystem ? <Bot size={16} /> : <User size={16} />}
              style={{
                backgroundColor: isSelected ? "#1890ff" : (isSystem ? "#52c41a" : "#fa8c16"),
              }}
            />
          }
          title={
            <Space>
              <Text
                strong
                style={{
                  color: isSelected ? "#1890ff" : "#262626",
                }}
              >
                {model.name || "未命名模型"}
              </Text>
                              {isSelected && (
                  <CheckCircle style={{ color: "#52c41a", fontSize: "16px" }} />
                )}
              <Tag color={isSystem ? "blue" : "orange"}>
                {isSystem ? "系统" : "私有"}
              </Tag>
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Text type="secondary" style={{ fontSize: "12px" }}>
                {model.title || "暂无描述"}
              </Text>
              <Space size="small">
                                  <Text type="secondary" style={{ fontSize: "12px" }}>
                    提供商: {model.provider || "未知"}
                  </Text>
                                  {model.maxDimension && (
                    <Text type="secondary" style={{ fontSize: "12px" }}>
                      最大维度: {model.maxDimension}
                    </Text>
                  )}
              </Space>
            </Space>
          }
        />
      </List.Item>
    );
  };

  const tabItems = [
    {
      key: "system",
      label: (
        <Space>
          <Bot size={16} />
          系统模型
        </Space>
      ),
      children: (
        <div>
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>
              <Spin size="large" />
            </div>
          ) : systemModels.length === 0 ? (
            <Empty
              description="暂无系统模型"
              style={{ marginTop: "40px" }}
            />
          ) : (
            <List
              dataSource={systemModels}
              renderItem={(model) => renderModelItem(model, true)}
              style={{ marginTop: "16px" }}
            />
          )}
        </div>
      ),
    },
    {
      key: "user",
      label: (
        <Space>
          <User size={16} />
          私有模型
        </Space>
      ),
      children: (
        <div>
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>
              <Spin size="large" />
            </div>
          ) : userModels.length === 0 ? (
            <Empty
              description="暂无私有模型"
              style={{ marginTop: "40px" }}
            />
          ) : (
            <List
              dataSource={userModels}
              renderItem={(model) => renderModelItem(model, false)}
              style={{ marginTop: "16px" }}
            />
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <Modal
        title="选择模型"
        open={visible}
        onCancel={onCancel}
        footer={null}
        width={700}
        bodyStyle={{ maxHeight: "60vh", overflow: "auto" }}
      >
        <div>
          <div style={{ marginBottom: "16px" }}>
            <Space>
              <Info style={{ color: "#1890ff" }} />
              <Text type="secondary" style={{ fontSize: "14px" }}>
                选择一个{modelType === "chat" ? "聊天" : modelType === "embedding" ? "向量化" : ""}AI模型
              </Text>
            </Space>
          </div>
          
          <Tabs
            items={tabItems}
            defaultActiveKey="system"
            style={{ marginTop: "16px" }}
          />
        </div>
      </Modal>
    </>
  );
};

export default ModelSelector; 