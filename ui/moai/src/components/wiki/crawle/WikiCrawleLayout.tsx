import { useNavigate, useLocation, useParams, Outlet } from "react-router";
import { Layout, Menu, Typography, Space, Button } from "antd";
import {
  ArrowLeftOutlined,
  SettingOutlined,
  FileTextOutlined,
  PlayCircleOutlined,
} from "@ant-design/icons";

const { Sider, Content } = Layout;
const { Title } = Typography;

export default function WikiCrawleLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { id: wikiId, crawleId } = useParams();

  const handleBack = () => {
    navigate(-1);
  };

  const handleMenuClick = ({ key }: { key: string }) => {
    navigate(`/app/wiki/${wikiId}/crawle/${crawleId}/${key}`);
  };

  // 根据当前路径确定选中的菜单项
  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/config")) {
      return "config";
    } else if (path.includes("/task")) {
      return "task";
    } else if (path.includes("/document")) {
      return "document";
    }
    return "config"; // 默认选中配置
  };

  const menuItems = [
    {
      key: "config",
      icon: <SettingOutlined />,
      label: "爬虫配置",
    },
    {
      key: "task",
      icon: <PlayCircleOutlined />,
      label: "任务列表",
    },
    {
      key: "document",
      icon: <FileTextOutlined />,
      label: "页面管理",
    },
  ];

  return (
    <Layout style={{ height: "100vh" }}>
      <Sider
        width={250}
        theme="light"
        style={{ borderRight: "1px solid #f0f0f0" }}
      >
        <div style={{ padding: "16px", borderBottom: "1px solid #f0f0f0" }}>
          <Space direction="vertical" size="small" style={{ width: "100%" }}>
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleBack}
              type="text"
              style={{ padding: 0, height: "auto" }}
            >
              返回
            </Button>
            <Title level={4} style={{ marginTop: 5, marginBottom: 0 }}>
              爬虫管理
            </Title>
          </Space>
        </div>
        <Menu
          mode="inline"
          selectedKeys={[getSelectedKey()]}
          items={menuItems}
          onClick={handleMenuClick}
          style={{ borderRight: "none" }}
        />
      </Sider>
      <Content
        style={{
          paddingLeft: "24px",
          paddingRight: "24px",
          paddingTop: "0px",
          overflow: "auto",
        }}
      >
        <Outlet />
      </Content>
    </Layout>
  );
}
