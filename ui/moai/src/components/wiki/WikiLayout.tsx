import { useState, useEffect } from "react";
import { useParams, useNavigate, useLocation, Outlet } from "react-router";
import {
  Layout,
  Menu,
  Typography,
  Space,
  Button,
  message,
  Spin,
  Card,
} from "antd";
import {
  ArrowLeftOutlined,
  SettingOutlined,
  FileTextOutlined,
  UserOutlined,
  SearchOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import "./Wiki.css";

const { Sider, Content } = Layout;
const { Title, Text } = Typography;

interface WikiInfo {
  wikiId: number;
  name: string;
  description: string;
  createUserName?: string;
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
    navigate("/app/wiki/list");
  };

  const handleMenuClick = ({ key }: { key: string }) => {
    if (key === "document") {
      navigate(`/app/wiki/${id}/document`);
    } else {
      navigate(`/app/wiki/${id}/${key}`);
    }
  };

  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/settings")) {
      return "settings";
    }
    return "detail";
  };

  const menuItems = [
    {
      key: "document",
      icon: <FileTextOutlined />,
      label: "文档列表",
    }, {
      key: "search",
      icon: <SearchOutlined />,
      label: "内容检索",
    },
    {
      key: "user",
      icon: <UserOutlined />,
      label: "成员",
    },
    {
      key: "settings",
      icon: <SettingOutlined />,
      label: "设置",
    },
  ];

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '50px' }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!wikiInfo) {
    return (
      <Card>
        <div style={{ textAlign: 'center', padding: '50px' }}>
          <Text type="secondary">未找到知识库信息</Text>
        </div>
      </Card>
    );
  }

  return (
    <>
      {contextHolder}
      <Layout style={{ height: '100vh' }}>
        <Sider width={250} theme="light" style={{ borderRight: '1px solid #f0f0f0' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #f0f0f0' }}>
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Button 
                icon={<ArrowLeftOutlined />} 
                onClick={handleBack}
                type="text"
                style={{ padding: 0, height: 'auto' }}
              >
                返回列表
              </Button>
              <Title level={4} style={{ marginTop: 5, marginBottom: 0 }}>
                {wikiInfo.name}
              </Title>
              <Text>{wikiInfo.description}</Text>
            </Space>
          </div>
          <Menu
            mode="inline"
            selectedKeys={[getSelectedKey()]}
            items={menuItems}
            onClick={handleMenuClick}
            style={{ borderRight: 'none' }}
          />
        </Sider>
        <Content style={{ padding: '24px', overflow: 'auto' }}>
          <Outlet />
        </Content>
      </Layout>
    </>
  );
} 