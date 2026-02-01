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
} from "antd";
import { BookOutlined, SearchOutlined, TeamOutlined } from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import {
  QueryWikiInfoResponse,
} from "../../apiClient/models";

const { Text } = Typography;

interface WikiSelectorProps {
  visible: boolean;
  onCancel: () => void;
  onSelect: (wikiId: number, wikiName: string) => void;
  selectedWikiId?: number | null;
}

const WikiSelector: React.FC<WikiSelectorProps> = ({
  visible,
  onCancel,
  onSelect,
  selectedWikiId,
}) => {
  const [wikis, setWikis] = useState<QueryWikiInfoResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 加载知识库列表
  const loadWikiList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.wiki.query_wiki_list.post({});

      if (response) {
        setWikis(response);
      } else {
        setWikis([]);
      }
    } catch (error) {
      console.error("加载知识库列表失败:", error);
      messageApi.error("加载知识库列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 组件显示时加载数据
  useEffect(() => {
    if (visible) {
      loadWikiList();
    }
  }, [visible, loadWikiList]);

  // 处理知识库选择
  const handleWikiSelect = (wiki: QueryWikiInfoResponse) => {
    onSelect(wiki.wikiId!, wiki.name || "未命名知识库");
    onCancel();
  };

  // 渲染知识库列表项
  const renderWikiItem = (wiki: QueryWikiInfoResponse) => {
    const isSelected = selectedWikiId === wiki.wikiId;
    
    return (
      <List.Item
        className={`wiki-item ${isSelected ? "selected" : ""}`}
        onClick={() => handleWikiSelect(wiki)}
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
              icon={<BookOutlined />}
              style={{
                backgroundColor: isSelected ? "#1890ff" : "#52c41a",
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
                {wiki.name || "未命名知识库"}
              </Text>
              {isSelected && (
                <Tag color="blue">已选择</Tag>
              )}
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Text type="secondary" style={{ fontSize: "12px" }}>
                {wiki.description || "暂无描述"}
              </Text>
              <Space size="small">
                <Text type="secondary" style={{ fontSize: "12px" }}>
                  <TeamOutlined style={{ marginRight: "4px" }} />
                  成员
                </Text>
                <Text type="secondary" style={{ fontSize: "12px" }}>
                  <SearchOutlined style={{ marginRight: "4px" }} />
                  {wiki.documentCount || 0} 个文档
                </Text>
              </Space>
            </Space>
          }
        />
      </List.Item>
    );
  };

  return (
    <>
      {contextHolder}
      <Modal
        title="选择知识库"
        open={visible}
        onCancel={onCancel}
        footer={null}
        width={600}
        bodyStyle={{ maxHeight: "60vh", overflow: "auto" }}
      >
        <div>
          <Text type="secondary" style={{ fontSize: "14px", marginBottom: "16px", display: "block" }}>
            选择一个知识库来增强AI助手的回答能力
          </Text>
          
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>
              <Spin size="large" />
            </div>
          ) : wikis.length === 0 ? (
            <Empty
              description="暂无知识库"
              style={{ marginTop: "40px" }}
            />
          ) : (
            <List
              dataSource={wikis}
              renderItem={renderWikiItem}
              style={{ marginTop: "16px" }}
            />
          )}
        </div>
      </Modal>
    </>
  );
};

export default WikiSelector; 