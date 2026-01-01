import { useNavigate, useLocation, Outlet } from "react-router";
import { Menu } from "antd";
import type { MenuProps } from "antd";
import { UserOutlined, LinkOutlined } from "@ant-design/icons";
import "./UserLayout.css";

export default function UserLayout() {
  const navigate = useNavigate();
  const location = useLocation();

  // 获取当前选中的菜单项
  const getSelectedKey = (): string => {
    const path = location.pathname;
    if (path.includes("/user/oauth")) {
      return "oauth";
    }
    return "usersetting";
  };

  // 菜单点击处理
  const handleMenuClick: MenuProps["onClick"] = (e) => {
    switch (e.key) {
      case "usersetting":
        navigate("/app/user/usersetting");
        break;
      case "oauth":
        navigate("/app/user/oauth");
        break;
    }
  };

  // 二级菜单项
  const menuItems: MenuProps["items"] = [
    {
      key: "usersetting",
      icon: <UserOutlined />,
      label: "个人信息",
    },
    {
      key: "oauth",
      icon: <LinkOutlined />,
      label: "第三方账号",
    },
  ];

  return (
    <div className="user-layout">
      <div className="user-layout-header">
        <Menu
          mode="horizontal"
          selectedKeys={[getSelectedKey()]}
          items={menuItems}
          onClick={handleMenuClick}
          className="user-menu"
        />
      </div>
      <div className="user-layout-content">
        <Outlet />
      </div>
    </div>
  );
}
