import { Outlet, useNavigate, useLocation } from "react-router";
import { Layout, Menu } from "antd";
import { ApiOutlined, CloudServerOutlined, FolderOutlined } from "@ant-design/icons";
import type { MenuProps } from "antd";
import useAppStore from "../../../stateshare/store";

const { Content } = Layout;

export default function PluginLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  const isAdmin = userDetailInfo?.isAdmin === true;

  // 获取当前选中的菜单项
  const getSelectedKey = (): string => {
    const path = location.pathname;
    if (path.includes("/plugin/custom")) {
      return "custom";
    }
    if (path.includes("/plugin/builtin")) {
      return "builtin";
    }
    if (path.includes("/plugin/classify")) {
      return "classify";
    }
    return "builtin";
  };

  // 二级菜单项
  const menuItems: MenuProps["items"] = [
    {
      key: "builtin",
      icon: <CloudServerOutlined />,
      label: "内置插件",
    },
    {
      key: "custom",
      icon: <ApiOutlined />,
      label: "自定义插件",
    },
    // 只有管理员才显示分类管理
    ...(isAdmin ? [{
      key: "classify",
      icon: <FolderOutlined />,
      label: "分类管理",
    }] : []),
  ];

  // 菜单点击处理
  const handleMenuClick: MenuProps["onClick"] = (e) => {
    const key = e.key;
    if (key === "custom") {
      navigate("/app/plugin/custom");
    } else if (key === "builtin") {
      navigate("/app/plugin/builtin");
    } else if (key === "classify") {
      navigate("/app/plugin/classify");
    }
  };

  return (
    <Layout style={{ minHeight: "100vh", background: "#fff" }}>
      <div
        style={{
          background: "#fff",
          borderBottom: "1px solid #f0f0f0",
          padding: "0 24px",
        }}
      >
        <Menu
          mode="horizontal"
          selectedKeys={[getSelectedKey()]}
          items={menuItems}
          onClick={handleMenuClick}
          style={{ borderBottom: "none" }}
        />
      </div>
      <Content>
        <Outlet />
      </Content>
    </Layout>
  );
}
