import React, { useState, useEffect, useCallback } from "react";
import {
  Modal,
  Input,
  List,
  Typography,
  Spin,
  Empty,
  Tabs,
  Button,
  message,
} from "antd";
import { SearchOutlined, FileTextOutlined } from "@ant-design/icons";
import ReactMarkdown from "react-markdown";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import type {
  PromptClassifyItem,
  PromptItem,
} from "../../apiClient/models";
import "./PromptSelector.css";

const { Text } = Typography;
const { Search } = Input;

interface PromptSelectorProps {
  open: boolean;
  onClose: () => void;
  onSelect: (prompt: PromptItem) => void;
}

const PromptSelector: React.FC<PromptSelectorProps> = ({
  open,
  onClose,
  onSelect,
}) => {
  const [messageApi, contextHolder] = message.useMessage();

  // 分类列表
  const [classes, setClasses] = useState<PromptClassifyItem[]>([]);
  const [classesLoading, setClassesLoading] = useState(false);
  const [activeClassId, setActiveClassId] = useState<number | null>(null);

  // 提示词列表
  const [prompts, setPrompts] = useState<PromptItem[]>([]);
  const [promptsLoading, setPromptsLoading] = useState(false);
  const [searchKeyword, setSearchKeyword] = useState("");

  // 预览
  const [selectedPrompt, setSelectedPrompt] = useState<PromptItem | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  // 加载分类列表
  const loadClasses = useCallback(async () => {
    setClassesLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.prompt.class_list.get();
      if (response?.items) {
        setClasses(response.items);
        // 默认选中第一个分类
        if (response.items.length > 0 && !activeClassId) {
          setActiveClassId(response.items[0].classifyId || null);
        }
      }
    } catch (error) {
      console.error("加载分类列表失败:", error);
      proxyRequestError(error, messageApi, "加载分类列表失败");
    } finally {
      setClassesLoading(false);
    }
  }, [messageApi, activeClassId]);

  // 加载提示词列表
  const loadPrompts = useCallback(async (classId?: number | null, search?: string) => {
    setPromptsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.prompt.prompt_list.post({
        classId: classId || undefined,
        search: search || undefined,
      });
      if (response?.items) {
        setPrompts(response.items);
      } else {
        setPrompts([]);
      }
    } catch (error) {
      console.error("加载提示词列表失败:", error);
      proxyRequestError(error, messageApi, "加载提示词列表失败");
    } finally {
      setPromptsLoading(false);
    }
  }, [messageApi]);

  // 加载提示词内容
  const loadPromptContent = useCallback(async (promptId: number) => {
    setPreviewLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId },
      });
      if (response) {
        setSelectedPrompt(response);
      }
    } catch (error) {
      console.error("加载提示词内容失败:", error);
      proxyRequestError(error, messageApi, "加载提示词内容失败");
    } finally {
      setPreviewLoading(false);
    }
  }, [messageApi]);

  // 初始化加载
  useEffect(() => {
    if (open) {
      loadClasses();
    }
  }, [open, loadClasses]);

  // 分类变化时加载提示词
  useEffect(() => {
    if (open && activeClassId !== null) {
      loadPrompts(activeClassId, searchKeyword);
    }
  }, [open, activeClassId, loadPrompts, searchKeyword]);

  // 搜索
  const handleSearch = (value: string) => {
    setSearchKeyword(value);
    loadPrompts(activeClassId, value);
  };

  // 选择提示词
  const handleSelectPrompt = (prompt: PromptItem) => {
    if (prompt.id) {
      loadPromptContent(prompt.id);
    }
  };

  // 确认选择
  const handleConfirm = () => {
    if (selectedPrompt) {
      onSelect(selectedPrompt);
      handleClose();
    }
  };

  // 关闭弹窗
  const handleClose = () => {
    setSelectedPrompt(null);
    setSearchKeyword("");
    onClose();
  };

  // 分类标签页
  const tabItems = [
    { key: "all", label: "全部", classifyId: null },
    ...classes.map((c) => ({
      key: String(c.classifyId),
      label: c.name,
      classifyId: c.classifyId,
    })),
  ];

  return (
    <Modal
      title="选择提示词"
      open={open}
      onCancel={handleClose}
      width={1100}
      maskClosable={false}
      footer={[
        <Button key="cancel" onClick={handleClose}>
          取消
        </Button>,
        <Button
          key="confirm"
          type="primary"
          disabled={!selectedPrompt}
          onClick={handleConfirm}
        >
          选择此提示词
        </Button>,
      ]}
    >
      {contextHolder}
      <div className="prompt-selector-container">
        {/* 左侧：分类和列表 */}
        <div className="prompt-selector-left">
          <Search
            placeholder="搜索提示词"
            allowClear
            onSearch={handleSearch}
            onChange={(e) => !e.target.value && handleSearch("")}
            prefix={<SearchOutlined />}
            className="prompt-search"
          />
          
          <Spin spinning={classesLoading}>
            <Tabs
              activeKey={activeClassId === null ? "all" : String(activeClassId)}
              onChange={(key) => {
                const classId = key === "all" ? null : Number(key);
                setActiveClassId(classId);
                setSelectedPrompt(null);
              }}
              items={tabItems}
              size="small"
              className="prompt-class-tabs"
            />
          </Spin>

          <div className="prompt-list-container">
            <Spin spinning={promptsLoading}>
              {prompts.length === 0 ? (
                <Empty description="暂无提示词" />
              ) : (
                <List
                  dataSource={prompts}
                  renderItem={(item) => (
                    <List.Item
                      className={`prompt-list-item ${
                        selectedPrompt?.id === item.id ? "prompt-list-item-active" : ""
                      }`}
                      onClick={() => handleSelectPrompt(item)}
                    >
                      <List.Item.Meta
                        avatar={<FileTextOutlined className="prompt-item-icon" />}
                        title={
                          <Text ellipsis className="prompt-item-title">
                            {item.name}
                          </Text>
                        }
                        description={
                          <Text type="secondary" ellipsis className="prompt-item-desc">
                            {item.description || "暂无描述"}
                          </Text>
                        }
                      />
                    </List.Item>
                  )}
                />
              )}
            </Spin>
          </div>
        </div>

        {/* 右侧：预览 */}
        <div className="prompt-selector-right">
          <div className="prompt-preview-header">
            <Text strong>提示词预览</Text>
          </div>
          <div className="prompt-preview-content">
            <Spin spinning={previewLoading}>
              {selectedPrompt ? (
                <div className="prompt-preview-detail">
                  <div className="prompt-preview-title">
                    <Text strong>{selectedPrompt.name}</Text>
                  </div>
                  {selectedPrompt.description && (
                    <div className="prompt-preview-desc">
                      <Text type="secondary">{selectedPrompt.description}</Text>
                    </div>
                  )}
                  <div className="prompt-preview-body">
                    <div className="prompt-content-markdown">
                      <ReactMarkdown>
                        {selectedPrompt.content || "暂无内容"}
                      </ReactMarkdown>
                    </div>
                  </div>
                </div>
              ) : (
                <Empty description="请选择一个提示词查看内容" />
              )}
            </Spin>
          </div>
        </div>
      </div>
    </Modal>
  );
};

export default PromptSelector;
