import { useState, useEffect } from "react";
import { Button, message, Spin, Empty, Card, Tag, Input } from "antd";
import {
  ReloadOutlined,
  ArrowRightOutlined,
  AppstoreOutlined,
  ArrowLeftOutlined,
  SearchOutlined,
} from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";
import { GetApiClient } from "../ServiceClient";
import { AppClassifyItem, AccessibleAppItem, QueryAccessibleAppListCommand, AppTypeObject } from "../../apiClient/models";
import { proxyRequestError } from "../../helper/RequestError";
import "./ApplicationListPage.css";

interface ApplicationListPageProps {
  teamId?: number;
}

/**
 * 应用列表页面 - 显示分类列表或特定分类下的应用
 */
export default function ApplicationListPage({ teamId }: ApplicationListPageProps) {
  const navigate = useNavigate();
  const { classifyId } = useParams<{ classifyId: string }>();
  const [classifyList, setClassifyList] = useState<AppClassifyItem[]>([]);
  const [appList, setAppList] = useState<AccessibleAppItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [classifyName, setClassifyName] = useState("");
  const [searchText, setSearchText] = useState("");
  const [messageApi, contextHolder] = message.useMessage();

  // 获取分类列表
  const fetchClassifyList = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.store.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items || []);
        
        // 如果有 classifyId，找到对应的分类名称
        if (classifyId) {
          const classify = response.items.find(
            (item: AppClassifyItem) => item.classifyId?.toString() === classifyId
          );
          if (classify) {
            setClassifyName(classify.name || "应用列表");
          }
        }
      }
    } catch (error) {
      console.error("获取分类列表失败:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 获取应用列表
  const fetchAppList = async () => {
    if (!classifyId) return;
    
    setLoading(true);
    try {
      const client = GetApiClient();
      const command: QueryAccessibleAppListCommand = {
        classifyId: parseInt(classifyId),
        name: searchText || undefined,
        teamId: teamId, // 使用传入的 teamId
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
    console.log("ApplicationListPage mounted, classifyId:", classifyId, "teamId:", teamId);
    fetchClassifyList();
    if (classifyId) {
      fetchAppList();
    }
  }, [classifyId, teamId]);

  const handleViewClassify = (classifyId: number | null | undefined) => {
    if (classifyId) {
      // 如果有 teamId，导航到团队应用列表，否则导航到普通应用列表
      if (teamId) {
        navigate(`/app/team/${teamId}/applist/${classifyId}`);
      } else {
        navigate(`/app/application/list/${classifyId}`);
      }
    }
  };

  const handleViewApp = (app: AccessibleAppItem) => {
    if (app.id) {
      // 流程应用暂不支持进入
      if (app.appType === AppTypeObject.Workflow) {
        messageApi.info("流程应用功能正在开发中，敬请期待");
        return;
      }
      navigate(`/app/appstore/${app.id}`);
    }
  };

  // 渲染分类卡片
  const renderClassifyCard = (classify: AppClassifyItem) => {
    return (
      <Card
        key={classify.classifyId || ""}
        className="moai-card moai-card-interactive"
        hoverable
        onClick={() => handleViewClassify(classify.classifyId)}
      >
        <div className="classify-card-content">
          <div className="classify-card-icon">
            <AppstoreOutlined style={{ fontSize: 32, color: "#1677ff" }} />
          </div>
          <div className="classify-card-info">
            <h3 className="classify-card-title">{classify.name}</h3>
            <p className="classify-card-desc">
              {classify.description || "暂无描述"}
            </p>
            <div className="classify-card-meta">
              <span className="classify-card-count">
                {classify.appCount || 0} 个应用
              </span>
            </div>
          </div>
          <div className="classify-card-action">
            <ArrowRightOutlined style={{ fontSize: 20, color: "#8c8c8c" }} />
          </div>
        </div>
      </Card>
    );
  };

  // 渲染应用卡片
  const renderAppCard = (app: AccessibleAppItem) => {
    const isWorkflow = app.appType === AppTypeObject.Workflow;
    
    return (
      <div key={app.id || ""} className={`app-card ${isWorkflow ? 'app-card-disabled' : ''}`}>
        {app.avatar && (
          <div className="app-card-icon">
            <img src={app.avatar} alt={app.name || "应用"} />
          </div>
        )}
        <div className="app-card-body" onClick={() => handleViewApp(app)}>
          <div className="app-card-title">{app.name}</div>
          <div className="app-card-desc">{app.description || "暂无描述"}</div>
          <div className="app-card-meta">
            {app.teamName && <Tag color="blue">{app.teamName}</Tag>}
            <Tag color={app.isPublic ? "green" : "orange"}>
              {app.isPublic ? "公开" : "私有"}
            </Tag>
            {isWorkflow && <Tag color="purple">开发中</Tag>}
          </div>
        </div>
        <div className="app-card-footer">
          <Button type="text" size="small" onClick={() => handleViewApp(app)} disabled={isWorkflow}>
            {isWorkflow ? "敬请期待" : "使用应用"}
          </Button>
        </div>
      </div>
    );
  };

  // 如果没有 classifyId，显示分类列表
  if (!classifyId) {
    return (
      <div className="page-container">
        {contextHolder}

        <div className="moai-page-header">
          <div>
            <h1 className="moai-page-title">{teamId ? "团队应用分类" : "应用分类"}</h1>
            <p className="moai-page-subtitle">{teamId ? "浏览不同分类的团队应用" : "浏览不同分类的应用"}</p>
          </div>
        </div>

        {/* 工具栏 */}
        <div className="moai-toolbar">
          <div className="moai-toolbar-left"></div>
          <div className="moai-toolbar-right">
            <Button
              icon={<ReloadOutlined />}
              onClick={fetchClassifyList}
              loading={loading}
            >
              刷新
            </Button>
          </div>
        </div>

        {/* 分类卡片列表 */}
        <Spin spinning={loading}>
          {classifyList.length > 0 ? (
            <div className="classify-card-list">
              {classifyList.map(renderClassifyCard)}
            </div>
          ) : (
            <Empty description="暂无分类" />
          )}
        </Spin>
      </div>
    );
  }

  // 如果有 classifyId，显示该分类下的应用列表
  return (
    <div className="page-container">
      {contextHolder}

      <div className="moai-page-header">
        <div>
          <div className="app-header-top">
            <Button 
              icon={<ArrowLeftOutlined />} 
              onClick={() => navigate(teamId ? `/app/team/${teamId}/applist` : "/app/application/list")}
            >
              返回
            </Button>
            <h1 className="moai-page-title moai-page-title-inline">
              {classifyName || "应用列表"}
            </h1>
          </div>
          <p className="moai-page-subtitle">{teamId ? "浏览此分类下的所有团队应用" : "浏览此分类下的所有应用"}</p>
        </div>
      </div>

      {/* 工具栏 */}
      <div className="app-toolbar">
        <Input
          placeholder="搜索应用"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onPressEnter={fetchAppList}
          prefix={<SearchOutlined style={{ color: "#bfbfbf" }} />}
          allowClear
          className="app-search-input"
        />
        <Button
          icon={<SearchOutlined />}
          onClick={fetchAppList}
          loading={loading}
        >
          搜索
        </Button>
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
        {appList.length > 0 ? (
          <div className="app-card-grid">
            {appList.map(renderAppCard)}
          </div>
        ) : (
          <Empty description={searchText ? "未找到匹配的应用" : "暂无应用"} />
        )}
      </Spin>
    </div>
  );
}
