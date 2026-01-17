import { useState, useEffect } from "react";
import { Button, message, Tag, Spin, Empty, Input, Tooltip } from "antd";
import {
  EditOutlined,
  SearchOutlined,
  TeamOutlined,
  LockOutlined,
  AppstoreOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import { GetApiClient } from "../ServiceClient";
import { AppClassifyItem, AccessibleAppItem, QueryAccessibleAppListCommand } from "../../apiClient/models";
import { proxyRequestError } from "../../helper/RequestError";
import useAppStore from "../../stateshare/store";
import "./ApplicationPage.css";

/**
 * 应用中心页面 - 按分类展示应用
 */
export default function ApplicationPage() {
  const navigate = useNavigate();
  const [classifyList, setClassifyList] = useState<AppClassifyItem[]>([]);
  const [appList, setAppList] = useState<AccessibleAppItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [searchText, setSearchText] = useState("");
  const [messageApi, contextHolder] = message.useMessage();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  const isAdmin = userDetailInfo?.isAdmin === true;

  // 获取应用分类列表
  const fetchClassifyList = async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.admin.appclassify.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items || []);
      }
    } catch (error) {
      console.error("获取分类列表失败:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    }
  };

  // 获取应用列表
  const fetchAppList = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const command: QueryAccessibleAppListCommand = {
        classifyId: selectedCategory ?? undefined,
        name: searchText || undefined,
      };
      const response = await client.api.app.store.accessible_list.post(command);
      if (response?.items) {
        setAppList(response.items || []);
      }
    } catch (error) {
      console.error("获取应用列表失败:", error);
      proxyRequestError(error, messageApi, "获取应用列表失败");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchClassifyList();
    fetchAppList();
  }, []);

  // 当分类改变时重新获取
  useEffect(() => {
    fetchAppList();
  }, [selectedCategory]);

  const handleManageClassify = () => {
    navigate("/app/application/classify");
  };

  const handleViewApp = (appId: string | null | undefined) => {
    if (appId) {
      navigate(`/app/application/chat/${appId}`);
    }
  };

  const handleSearch = () => {
    fetchAppList();
  };

  const renderAppCard = (app: AccessibleAppItem) => {
    return (
      <div key={app.id || ""} className="app-card" onClick={() => handleViewApp(app.id)}>
        <div className="app-card-header">
          <div className="app-card-avatar">
            {app.avatar ? (
              <img src={app.avatar} alt={app.name || "应用"} />
            ) : (
              <AppstoreOutlined />
            )}
          </div>
          <div className="app-card-title-section">
            <Tooltip title={app.name}>
              <h3 className="app-card-title">{app.name}</h3>
            </Tooltip>
            <div className="app-card-tags">
              {app.teamId ? (
                <Tag color="orange" icon={<TeamOutlined />}>
                  {app.teamName || "团队"}
                </Tag>
              ) : (
                <Tag color="blue" icon={<LockOutlined />}>
                  私有
                </Tag>
              )}
              {app.isPublic && (
                <Tag color="green">公开</Tag>
              )}
            </div>
          </div>
        </div>

        <div className="app-card-content">
          <p className="app-card-description">
            {app.description || "暂无描述"}
          </p>
        </div>

        <div className="app-card-footer">
          <div className="app-card-meta">
            {app.createTime && (
              <span className="app-card-meta-item">
                {new Date(app.createTime).toLocaleDateString()}
              </span>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="page-container">
      {contextHolder}

      {/* 分类标签栏 */}
      <div className="app-category-bar">
        <div className="category-tags">
          <Tag
            className={`category-tag ${selectedCategory === null ? "active" : ""}`}
            onClick={() => setSelectedCategory(null)}
          >
            全部
          </Tag>
          {classifyList.map(category => (
            <Tag
              key={category.classifyId}
              className={`category-tag ${selectedCategory === category.classifyId ? "active" : ""}`}
              onClick={() => setSelectedCategory(category.classifyId ?? null)}
            >
              {category.name}
            </Tag>
          ))}
          {isAdmin && (
            <Tag
              className="category-tag category-tag-edit"
              onClick={handleManageClassify}
            >
              <EditOutlined /> 编辑分类
            </Tag>
          )}
        </div>
      </div>

      {/* 筛选工具栏 */}
      <div className="app-toolbar">
        <Input
          placeholder="搜索应用"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onPressEnter={handleSearch}
          prefix={<SearchOutlined style={{ color: "#bfbfbf" }} />}
          allowClear
          className="app-search-input"
        />
        <Button icon={<SearchOutlined />} onClick={handleSearch} loading={loading}>
          搜索
        </Button>
      </div>

      {/* 应用卡片网格 */}
      <Spin spinning={loading}>
        {appList.length > 0 ? (
          <div className="app-grid">
            {appList.map(renderAppCard)}
          </div>
        ) : (
          <Empty description={searchText ? "未找到匹配的应用" : "暂无应用"} />
        )}
      </Spin>
    </div>
  );
}
