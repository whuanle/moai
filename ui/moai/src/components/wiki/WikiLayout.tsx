import { useState, useEffect } from "react";
import { useParams, useNavigate, useLocation, Outlet } from "react-router";
import { Layout, Menu, Button, message, Spin, Card, Typography, Tag, Tooltip } from "antd";
import {
  ArrowLeftOutlined,
  SettingOutlined,
  FileTextOutlined,
  SearchOutlined,
  ThunderboltOutlined,
  CloudDownloadOutlined,
  ApiOutlined,
  CloudServerOutlined,
  GlobalOutlined,
  BookOutlined,
  ScanOutlined,
  TeamOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import "./Wiki.css";

const { Sider, Content } = Layout;
const { Text } = Typography;

interface WikiInfo {
  wikiId: number;
  name: string;
  description: string;
  createUserName?: string;
  teamId?: number;
  teamName?: string;
}

export default function WikiLayout() {
  const [wikiInfo, setWikiInfo] = useState<WikiInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const apiClient = GetApiClient();

  const fetchWikiInfo = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response = await apiClient.api.wiki.query_wiki_info.post({
        wikiId: parseInt(id),
      });

      if (response) {
        setWikiInfo({
          wikiId: response.wikiId!,
          name: response.name!,
          description: response.description || "",
          createUserName: response.createUserName || undefined,
          teamId: response.teamId || undefined,
          teamName: response.teamName || undefined,
        });
      }
    } catch (error) {
      console.error("Failed to fetch wiki info:", error);
      messageApi.error("获取知识库信息失败");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (id) {
      fetchWikiInfo();
    }
  }, [id]);

  const handleBack = () => {
    if (wikiInfo?.teamId && wikiInfo.teamId > 0) {
      navigate(`/app/team/${wikiInfo.teamId}/wiki`);
    } else {
      navigate("/app/wiki/list");
    }
  };

  const handleMenuClick = ({ key }: { key: string }) => {
    if (key === "document") {
      navigate(`/app/wiki/${id}/document`);
    } else if (key === "crawle") {
      navigate(`/app/wiki/${id}/plugin/crawler`);
    } else if (key === "feishu") {
      navigate(`/app/wiki/${id}/plugin/feishu`);
    } else if (key === "openapi") {
      navigate(`/app/wiki/${id}/plugin/openapi`);
    } else if (key === "paddleocr") {
      navigate(`/app/wiki/${id}/plugin/paddleocr`);
    } else if (key === "mcp") {
      navigate(`/app/wiki/${id}/plugin/mcp`);
    } else if (key === "batch") {
      navigate(`/app/wiki/${id}/batch`);
    } else {
      navigate(`/app/wiki/${id}/${key}`);
    }
  };

  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/settings")) {
      return "settings";
    } else if (path.includes("/plugin/crawler")) {
      return "crawle";
    } else if (path.includes("/plugin/feishu")) {
      return "feishu";
    } else if (path.includes("/plugin/openapi")) {
      return "openapi";
    } else if (path.includes("/plugin/paddleocr")) {
      return "paddleocr";
    } else if (path.includes("/plugin/mcp")) {
      return "mcp";
    } else if (path.includes("/search")) {
      return "search";
    } else if (path.includes("/user")) {
      return "user";
    } else if (path.includes("/document")) {
      return "document";
    } else if (path.includes("/batch")) {
      return "batch";
    }
    return "detail";
  };

  const getDefaultOpenKeys = () => {
    // 默认展开外部导入子菜单
    return ["external-import"];
  };

  const menuItems = [
    {
      key: "document",
      icon: <FileTextOutlined />,
      label: "文档列表",
    },
    {
      key: "external-import",
      icon: <CloudDownloadOutlined />,
      label: "外部导入",
      children: [
        {
          key: "crawle",
          icon: <GlobalOutlined />,
          label: "网页爬虫",
        },
        {
          key: "feishu",
          icon: <BookOutlined />,
          label: "飞书知识库",
        },
        {
          key: "openapi",
          icon: <ApiOutlined />,
          label: "OpenAPI",
        },
        {
          key: "paddleocr",
          icon: <ScanOutlined />,
          label: "飞桨 OCR",
        },
        {
          key: "mcp",
          icon: <CloudServerOutlined />,
          label: "MCP 配置",
        },
      ],
    },
    {
      key: "batch",
      icon: <ThunderboltOutlined />,
      label: "批处理任务",
    },
    {
      key: "search",
      icon: <SearchOutlined />,
      label: "召回测试",
    },
    {
      key: "settings",
      icon: <SettingOutlined />,
      label: "设置",
    },
  ];

  if (loading) {
    return (
      <div className="wiki-loading">
        <Spin size="large" />
      </div>
    );
  }

  if (!wikiInfo) {
    return (
      <div className="wiki-empty">
        <Card className="wiki-empty-card">
          <Text type="secondary">未找到知识库信息</Text>
        </Card>
      </div>
    );
  }

  return (
    <>
      {contextHolder}
      <Layout className="wiki-layout">
        <Sider width={260} className="wiki-sider">
          <div className="wiki-sider-header">
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleBack}
              type="text"
              className="wiki-back-btn"
            >
              {wikiInfo.teamId && wikiInfo.teamId > 0 ? "返回团队" : "返回列表"}
            </Button>
            <div className="wiki-info">
              <div className="wiki-title-row">
                <h2 className="wiki-title">{wikiInfo.name}</h2>
                {wikiInfo.teamId && wikiInfo.teamId > 0 && (
                  <Tooltip title={`所属团队: ${wikiInfo.teamName || "团队"}`}>
                    <Tag color="blue" icon={<TeamOutlined />} className="wiki-team-tag">
                      团队
                    </Tag>
                  </Tooltip>
                )}
              </div>
              {wikiInfo.description && (
                <p className="wiki-description">{wikiInfo.description}</p>
              )}
            </div>
          </div>
          <Menu
            mode="inline"
            selectedKeys={[getSelectedKey()]}
            defaultOpenKeys={getDefaultOpenKeys()}
            items={menuItems}
            onClick={handleMenuClick}
            className="wiki-menu"
          />
        </Sider>
        <Content className="wiki-content">
          <Outlet />
        </Content>
      </Layout>
    </>
  );
} 