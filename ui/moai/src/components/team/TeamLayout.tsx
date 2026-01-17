import { useEffect, useState } from "react";
import { Outlet, useParams, useNavigate, useLocation } from "react-router";
import { Layout, Menu, Spin, message, Typography, Space, Avatar } from "antd";
import {
  TeamOutlined,
  SettingOutlined,
  UserOutlined,
  ArrowLeftOutlined,
  BookOutlined,
  AppstoreOutlined,
  ApiOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import type { QueryTeamInfoCommandResponse, TeamRole } from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";

const { Sider, Content } = Layout;
const { Title } = Typography;

export default function TeamLayout() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const [teamInfo, setTeamInfo] = useState<QueryTeamInfoCommandResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [myRole, setMyRole] = useState<TeamRole | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  useEffect(() => {
    if (id) {
      fetchTeamInfo();
    }
  }, [id]);

  const fetchTeamInfo = async () => {
    try {
      setLoading(true);
      const client = GetApiClient();

      // 使用新的 info API 获取团队信息
      const response = await client.api.team.common.info.post({
        teamId: parseInt(id!),
      });

      if (response) {
        setTeamInfo(response);
        setMyRole(response.myRole || null);
      } else {
        messageApi.error("团队不存在或无权访问");
        navigate("/app/team/list");
      }
    } catch (error) {
      console.error("获取团队信息失败:", error);
      messageApi.error("获取团队信息失败");
      navigate("/app/team/list");
    } finally {
      setLoading(false);
    }
  };

  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.includes("/settings")) return "settings";
    if (path.includes("/members")) return "members";
    if (path.includes("/wiki")) return "wiki";
    if (path.includes("/appstore")) return "appstore";
    if (path.includes("/apps")) return "apps";
    if (path.includes("/integration")) return "integration";
    return "wiki";
  };

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  const menuItems = [
    {
      key: "wiki",
      icon: <BookOutlined />,
      label: "知识库",
    },
    {
      key: "appstore",
      icon: <AppstoreOutlined />,
      label: "应用中心",
    },
    {
      key: "members",
      icon: <UserOutlined />,
      label: "团队成员",
    },
    ...(canManage
      ? [
        {
          key: "apps",
          icon: <SettingOutlined />,
          label: "应用管理",
        },
        {
          key: "integration",
          icon: <ApiOutlined />,
          label: "系统接入",
        },
        {
          key: "settings",
          icon: <SettingOutlined />,
          label: "团队设置",
        },
      ]
      : []),
  ];

  const handleMenuClick = (key: string) => {
    if (key === "appstore") {
      navigate(`/app/appstore/${id}`);
    } else {
      navigate(`/app/team/${id}/${key}`);
    }
  };

  if (loading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <>
      {contextHolder}
      <Layout style={{ height: "100%", background: "#fff" }}>
        <Sider width={220} style={{ background: "#fff", borderRight: "1px solid #f0f0f0" }}>
          <div style={{ padding: "16px" }}>
            <Space
              style={{ cursor: "pointer", marginBottom: 16 }}
              onClick={() => navigate("/app/team/list")}
            >
              <ArrowLeftOutlined />
              <span>返回列表</span>
            </Space>
            <Space direction="vertical" align="center" style={{ width: "100%" }}>
              {teamInfo?.avatar ? (
                <Avatar size={64} src={teamInfo.avatar} />
              ) : (
                <Avatar size={64} icon={<TeamOutlined />} />
              )}
              <Title level={5} style={{ margin: "8px 0 0" }}>
                {teamInfo?.name}
              </Title>
            </Space>
          </div>
          <Menu
            mode="inline"
            selectedKeys={[getSelectedKey()]}
            items={menuItems}
            onClick={({ key }) => handleMenuClick(key)}
            style={{ borderRight: 0 }}
          />
        </Sider>
        <Content style={{ padding: "24px", overflow: "auto" }}>
          <Outlet context={{ teamInfo, myRole, refreshTeamInfo: fetchTeamInfo }} />
        </Content>
      </Layout>
    </>
  );
}
