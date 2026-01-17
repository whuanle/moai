import { useState, useEffect } from "react";
import { Button, message, Tag, Spin, Empty, Input } from "antd";
import {
  ReloadOutlined,
  ArrowLeftOutlined,
  SearchOutlined,
} from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";
import { GetApiClient } from "../ServiceClient";
import { AccessibleAppItem } from "../../apiClient/models";
import { proxyRequestError } from "../../helper/RequestError";
import "./ApplicationListPage.css";

/**
 * 应用列表页面 - 显示特定分类下的应用
 */
export default function ApplicationListPage() {
  const navigate = useNavigate();
  const { classifyId } = useParams<{ classifyId: string }>();
  const [appList, setAppList] = useState<AccessibleAppItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [classifyName, setClassifyName] = useState("");
  const [searchText, setSearchText] = useState("");
  const [messageApi, contextHolder] = message.useMessage();

  // 获取应用列表
  const fetchAppList = async () => {
    if (!classifyId) return;
    
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.store.accessible_list.get();
      if (response?.items) {
        const filteredApps = response.items || [];
        setAppList(filteredApps);
      }
      if (response?.items && response.items.length > 0) {
        setClassifyName(response.items[0].teamName || "应用列表");
      }
    } catch (error) {
      proxyRequestError(error, messageApi, "获取应用列表失败");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAppList();
  }, [classifyId]);

  const handleViewApp = (appId: string | null | undefined) => {
    if (appId) {
      navigate(`/app/appstore/${appId}`);
    }
  };

  const filteredApps = appList.filter(app =>
    !searchText || app.name?.toLowerCase().includes(searchText.toLowerCase())
  );

  const renderAppCard = (app: AccessibleAppItem) => {
    return (
      <div key={app.id || ""} className="app-card">
        {app.avatar && (
          <div className="app-card-icon">
            <img src={app.avatar} alt={app.name || "应用"} />
          </div>
        )}
        <div className="app-card-body" onClick={() => handleViewApp(app.id)}>
          <div className="app-card-title">{app.name}</div>
          <div className="app-card-desc">{app.description || "暂无描述"}</div>
          <div className="app-card-meta">
            {app.teamName && <Tag color="blue">{app.teamName}</Tag>}
            <Tag color={app.isPublic ? "green" : "orange"}>
              {app.isPublic ? "公开" : "私有"}
            </Tag>
          </div>
        </div>
        <div className="app-card-footer">
          <Button type="text" size="small" onClick={() => handleViewApp(app.id)}>
            使用应用
          </Button>
        </div>
      </div>
    );
  };

  return (
    <div className="page-container">
      {contextHolder}

      <div className="moai-page-header">
        <div>
          <div className="app-header-top">
            <Button icon={<ArrowLeftOutlined />} onClick={() => navigate("/app/application")}>
              返回
            </Button>
            <h1 className="moai-page-title moai-page-title-inline">
              {classifyName || "应用列表"}
            </h1>
          </div>
          <p className="moai-page-subtitle">浏览此分类下的所有应用</p>
        </div>
      </div>

      {/* 工具栏 */}
      <div className="app-toolbar">
        <Input
          placeholder="搜索应用"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          prefix={<SearchOutlined style={{ color: "#bfbfbf" }} />}
          allowClear
          className="app-search-input"
        />
        <Button
          icon={<ReloadOutlined />}
          onClick={fetchAppList}
          loading={loading}
        >
          刷新
        </Button>
      </div>

      {/* 应用卡片网格 */}
      <Spin spinning={loading}>
        {filteredApps.length > 0 ? (
          <div className="app-card-grid">
            {filteredApps.map(renderAppCard)}
          </div>
        ) : (
          <Empty description={searchText ? "未找到匹配的应用" : "暂无应用"} />
        )}
      </Spin>
    </div>
  );
}
