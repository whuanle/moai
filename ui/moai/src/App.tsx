import { useEffect, useState } from "react";
import {
  Route,
  Routes,
  useNavigate,
  Link,
  useLocation,
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
  GetUserDetailInfo,
  SetUserDetailInfo,
  GetUserDetailInfoFromCache,
} from "./InitService";
import { GetApiClient } from "./components/ServiceClient";
import useAppStore from "./stateshare/store";
import Dashboard from "./components/dashboard/Dashboard";
import OAuth from "./components/admin/OAuth";
import UserManager from "./components/admin/UserManager";
import UserSetting from "./components/user/UserSetting";

const { Sider, Content, Footer } = Layout;
const { Title } = Typography;

function App() {
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const location = useLocation();
  const [userDetailInfo, setUserDetailInfo] = useState(
    GetUserDetailInfoFromCache()
  );
  const [isAdmin, setIsAdmin] = useState(false);
  const serverInfo = useAppStore.getState().getServerInfo();

  // 获取子菜单的 key
  const getSubMenuKey = (path: string) => {
    switch (path) {
      case "dashboard":
        return "0";
      case "user":
        return "2";
      case "admin":
        return "10";
      default:
        return "0";
    }
  };

  // 获取当前选中的菜单项
  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/admin/oauth")) {
      return "10.1";
    }
    if (path.includes("/admin/usermanager")) {
      return "10.2";
    }
    if (path.includes("/admin")) {
      return "10";
    }
    if (path.includes("/user/usersetting")) {
      return "2.1";
    }
    if (path.includes("/user")) {
      return "2";
    }
    return "1";
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
          SetUserDetailInfo(detailInfo);
          setUserDetailInfo(detailInfo);
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
        key: "1",
        icon: <HomeOutlined />,
        label: <Link to="/app/index">首页</Link>,
      },
      {
        key: "2",
        icon: <UserOutlined />,
        label: "个人中心",
        children: [
          {
            key: "2.1",
            icon: <UserOutlined />,
            label: <Link to="/app/user/usersetting">个人信息</Link>,
          },
        ],
      },
    ];

    // 只有管理员才能看到管理面板
    if (isAdmin) {
      baseItems.push({
        key: "10",
        icon: <SettingOutlined />,
        label: "管理面板",
        children: [
          {
            key: "10.1",
            icon: <ApiOutlined />,
            label: <Link to="/app/admin/oauth">OAuth</Link>,
          },
          {
            key: "10.2",
            icon: <TeamOutlined />,
            label: <Link to="/app/admin/usermanager">用户管理</Link>,
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
                src={getAvatarUrl(userDetailInfo?.avatar)}
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
                </Route>
                <Route path="user">
                  <Route index element={<Navigate to="usersetting" replace />} />
                  <Route path="usersetting" element={<UserSetting />} />
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
