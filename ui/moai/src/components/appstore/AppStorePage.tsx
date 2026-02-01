import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { Spin, Empty, Avatar, message } from "antd";
import { AppstoreOutlined, RightOutlined } from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import type { AccessibleAppItem, AppType } from "../../apiClient/models";
import "./AppStorePage.css";

export default function AppStorePage() {
  const { teamId } = useParams();
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(true);
  const [apps, setApps] = useState<AccessibleAppItem[]>([]);

  const id = parseInt(teamId || "0");

  useEffect(() => {
    if (id) {
      fetchAppList();
    }
  }, [id]);

  const fetchAppList = async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const response = await client.api.app.store.accessible_list.post({
        teamId: id,
      });
      setApps(response?.items || []);
    } catch (error) {
      console.error("获取应用列表失败:", error);
      proxyRequestError(error, messageApi, "获取应用列表失败");
    } finally {
      setLoading(false);
    }
  };

  const getAppTypeName = (appType: AppType | null | undefined) => {
    switch (appType) {
      case "chat":
        return "普通应用";
      case "workflow":
        return "流程编排";
      case "agent":
        return "智能体";
      default:
        return "应用";
    }
  };

  const handleAppClick = (app: AccessibleAppItem) => {
    // 目前只支持普通应用 (appType === "chat")
    if (app.appType === "chat") {
      navigate(`/app/appstore/${app.id}`, { state: { teamId: id } });
    } else {
      messageApi.info("该应用类型暂不支持");
    }
  };

  if (loading) {
    return (
      <div className="app-store-page">
        <div className="app-store-loading">
          <Spin size="large" />
        </div>
      </div>
    );
  }

  return (
    <div className="app-store-page">
      {contextHolder}
      <div className="app-store-header">
        <h1 className="app-store-title">应用中心</h1>
        <p className="app-store-subtitle">选择应用开始对话</p>
      </div>

      {apps.length === 0 ? (
        <div className="app-store-empty">
          <Empty description="暂无可用应用" />
        </div>
      ) : (
        <div className="app-store-grid">
          {apps.map((app) => (
            <div
              key={app.id}
              className="app-store-card"
              onClick={() => handleAppClick(app)}
            >
              <div className="app-store-card-header">
                <Avatar
                  size={48}
                  src={app.avatar}
                  icon={!app.avatar && <AppstoreOutlined />}
                  className="app-store-card-avatar"
                />
                <div className="app-store-card-info">
                  <h3 className="app-store-card-name">{app.name}</h3>
                  <div className="app-store-card-type">{getAppTypeName(app.appType)}</div>
                </div>
              </div>
              <div className="app-store-card-desc">
                {app.description || "暂无描述"}
              </div>
              <div className="app-store-card-footer">
                <RightOutlined style={{ color: "var(--color-text-tertiary)" }} />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
