import { useEffect, useState } from "react";
import {
  useNavigate,
  Link,
  useLocation,
  Outlet,
  Route,
  Routes,
  Navigate,
} from "react-router";
import {
  Layout,
  Menu,
  message,
  Image,
  Modal,
  Card,
  Avatar,
  Space,
  Tag,
  Spin,
  Flex,
  Typography,
  Dropdown,
  Button,
} from "antd";
import {
  TeamOutlined,
  HomeOutlined,
  AppstoreOutlined,
  DashboardOutlined,
  RobotOutlined,
  AppstoreAddOutlined,
  BookOutlined,
  ApiOutlined,
  SettingOutlined,
  FileTextOutlined,
  UserOutlined,
  DownOutlined,
  LogoutOutlined,
} from "@ant-design/icons";
import "./App.css";
import {
  CheckToken,
  RefreshServerInfo,
  GetUserDetailInfo
} from "./InitService";
import { GetApiClient } from "./components/ServiceClient";
import useAppStore from "./stateshare/store";
import Dashboard from "./components/dashboard/Dashboard";
import OAuth from "./components/admin/OAuth";
import UserManager from "./components/admin/UserManager";
import UserSetting from "./components/user/UserSetting";
import AIModel from "./components/admin/AiModel";
import WikiPage from "./components/wiki/WikiPage";
import WikiLayout from "./components/wiki/WikiLayout";
import WikiSettings from "./components/wiki/WikiSettings";
import WikiDocument from "./components/wiki/WikiDocument";
import DocumentEmbedding from "./components/wiki/DocumentEmbedding";
import WikiSearch from "./components/wiki/WikiSearch";
import WikiUser from "./components/wiki/WikiUser";

const { Sider, Content, Footer } = Layout;
const { Title } = Typography;

function App() {
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const location = useLocation();

  const [isAdmin, setIsAdmin] = useState(false);
  const serverInfo = useAppStore.getState().getServerInfo();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);

  // 获取子菜单的 key
  const getSubMenuKey = (path: string) => {
    switch (path) {
      case "dashboard":
        return "dashboard";
      case "user":
        return "user";
      case "admin":
        return "admin";
      default:
        return "dashboard";
    }
  };

  // 获取当前选中的菜单项
  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/admin/oauth")) {
      return "admin.oauth";
    }
    if (path.includes("/admin/usermanager")) {
      return "admin.usermanager";
    }
    if (path.includes("/admin")) {
      return "admin";
    }
    if (path.includes("/user/usersetting")) {
      return "user.setting";
    }
    if (path.includes("/user")) {
      return "user";
    }
    if (path.includes("/wiki")) {
      return "wiki";
    }
    return "home";
  };

  useEffect(() => {
    let client = GetApiClient();
    const fetchData = async () => {
      await RefreshServerInfo(client);
      var isVerify = await CheckToken();
      if (!isVerify) {
        messageApi.success("身份已失效，正在重定向到登录页面");
        setTimeout(() => {
          navigate("/login");
        }, 1000);
      } else {
        // 获取用户详细信息
        const detailInfo = await GetUserDetailInfo();
        if (detailInfo) {
          useAppStore.getState().setUserDetailInfo(detailInfo);
          // 设置管理员状态
          setIsAdmin(detailInfo.isAdmin === true);
        }
      }
    };
    fetchData();

    // 每分钟刷新一次 token
    const refreshToken = setInterval(async () => {
      await CheckToken();
    }, 1000 * 60);

    return () => {
      clearInterval(refreshToken);
    };
  }, []);

  // 获取完整的头像URL
  const getAvatarUrl = (avatarPath: string | null | undefined) => {
    if (!avatarPath) return null;
    if (avatarPath.startsWith("http://") || avatarPath.startsWith("https://")) {
      return avatarPath;
    }
    return serverInfo?.publicStoreUrl
      ? `${serverInfo.publicStoreUrl}/${avatarPath}`
      : avatarPath;
  };

  // 注销登录
  const handleLogout = () => {
    useAppStore.getState().clearUserInfo();
    useAppStore.getState().clearUserDetailInfo();
    messageApi.success("已注销登录");
    navigate("/login");
  };

  // 用户头像下拉菜单项
  const userMenuItems = [
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "注销登录",
      onClick: handleLogout,
    },
  ];

  // 动态生成菜单项，根据管理员权限显示或隐藏管理面板
  const getMenuItems = () => {
    const baseItems = [
      {
        key: "home",
        icon: <HomeOutlined />,
        label: <Link to="/app/index">首页</Link>,
      },
      {
        key: "wiki",
        icon: <BookOutlined />,
        label: <Link to="/app/wiki/list">知识库</Link>,
      },
      {
        key: "user",
        icon: <UserOutlined />,
        label: "个人中心",
        children: [
          {
            key: "user.setting",
            icon: <UserOutlined />,
            label: <Link to="/app/user/usersetting">个人信息</Link>,
          },
        ],
      },
    ];

    // 只有管理员才能看到管理面板
    if (isAdmin) {
      baseItems.push({
        key: "admin",
        icon: <SettingOutlined />,
        label: "管理面板",
        children: [
          {
            key: "admin.oauth",
            icon: <ApiOutlined />,
            label: <Link to="/app/admin/oauth">OAuth</Link>,
          },
          {
            key: "admin.usermanager",
            icon: <TeamOutlined />,
            label: <Link to="/app/admin/usermanager">用户管理</Link>,
          },
          {
            key: "admin.aimodel",
            icon: <RobotOutlined />,
            label: <Link to="/app/admin/aimodel">AI模型</Link>,
          },
        ],
      });
    }

    return baseItems;
  };

  return (
    <>
      {contextHolder}
      <Layout className="layout">
        <Layout.Header
          className="header"
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            backgroundColor: "white",
          }}
        >
          <div style={{ display: "flex", alignItems: "center" }}>
            <Image src="/logo.png" width={60} height={60} />
            <p style={{ margin: 0, marginLeft: 10 }}>MaomiAI</p>
          </div>
          <Dropdown
            menu={{ items: userMenuItems }}
            placement="bottomRight"
            arrow
          >
            <div
              style={{
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                padding: "4px 8px",
                borderRadius: "6px",
                transition: "background-color 0.2s",
              }}
              onMouseEnter={(e) =>
                (e.currentTarget.style.backgroundColor = "#f5f5f5")
              }
              onMouseLeave={(e) =>
                (e.currentTarget.style.backgroundColor = "transparent")
              }
            >
              <Avatar
                size={40}
                src={getAvatarUrl(userDetailInfo?.avatarPath)}
                icon={<UserOutlined />}
                style={{ marginRight: 8 }}
              />
              <span style={{ marginRight: 4, color: "#333" }}>
                {userDetailInfo?.nickName || userDetailInfo?.userName || "用户"}
              </span>
              <DownOutlined style={{ fontSize: "12px", color: "#666" }} />
            </div>
          </Dropdown>
        </Layout.Header>
        <Layout>
          <Sider width={200} className="sider" collapsible>
            <Menu
              mode="inline"
              className="menu-inline"
              selectedKeys={[getSelectedKey()]}
              items={getMenuItems()}
            />
          </Sider>
          <Layout style={{ padding: "0 0px 0px" }}>
          <Content className="content">
              <Routes>
                <Route index element={<Dashboard />} />
                <Route path="index" element={<Dashboard />} />
                <Route path="admin">
                  <Route index element={<Navigate to="oauth" replace />} />
                  <Route path="oauth" element={<OAuth />} />
                  <Route path="usermanager" element={<UserManager />} />
                  <Route path="aimodel" element={<AIModel />} />
                </Route>
                <Route path="user">
                  <Route index element={<Navigate to="usersetting" replace />} />
                  <Route path="usersetting" element={<UserSetting />} />
                </Route>
                <Route path="wiki">
                  <Route index element={<Navigate to="list" replace />} />
                  <Route path="list" element={<WikiPage />} />
                  <Route path=":id" element={<WikiLayout />}>
                    <Route path="settings" element={<WikiSettings />} />
                    <Route index element={<WikiDocument />} />
                    <Route path="document" element={<WikiDocument />} />
                    <Route path="document/:documentId/embedding" element={<DocumentEmbedding />} />
                    <Route path="search" element={<WikiSearch />} />
                    <Route path="user" element={<WikiUser />} />
                  </Route>
                </Route>
              </Routes>
            </Content>
            <Footer className="footer">MaomiAI ©2025</Footer>
          </Layout>
        </Layout>
      </Layout>
    </>
  );
}

export default App;
