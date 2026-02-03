import React, { useEffect, useState } from "react";
import {
  Button,
  Card,
  Row,
  Col,
  Typography,
  Space,
  Avatar,
  Divider,
  Tag,
  Statistic,
} from "antd";
import {
  RobotOutlined,
  BookOutlined,
  AppstoreOutlined,
  FileTextOutlined,
  UserOutlined,
  LoginOutlined,
  AppstoreAddOutlined,
  ThunderboltOutlined,
  SafetyOutlined,
  GlobalOutlined,
  RocketOutlined,
  StarOutlined,
  CheckCircleOutlined,
  ArrowRightOutlined,
  ApiOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import useAppStore from "./stateshare/store";
import AuthModal from "./components/common/AuthModal";
import "./Home.css";

const { Title, Paragraph, Text } = Typography;

const Home: React.FC = () => {
  const navigate = useNavigate();
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [loading, setLoading] = useState(true);
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const [authModalTab, setAuthModalTab] = useState<"login" | "register">("login");
  const userInfo = useAppStore((state) => state.getUserInfo());

  // 强制首页使用暗色主题
  useEffect(() => {
    // 保存当前主题
    const currentTheme = document.documentElement.getAttribute('data-theme');
    
    // 强制设置为暗色主题
    document.documentElement.setAttribute('data-theme', 'dark');
    
    // 组件卸载时恢复之前的主题
    return () => {
      if (currentTheme) {
        document.documentElement.setAttribute('data-theme', currentTheme);
      }
    };
  }, []);

  useEffect(() => {
    // 检查登录状态
    const checkLoginStatus = () => {
      const user = useAppStore.getState().getUserInfo();
      setIsLoggedIn(!!user?.accessToken);
      setLoading(false);
    };

    checkLoginStatus();
  }, []);

  const handleLogin = () => {
    setAuthModalTab("login");
    setAuthModalOpen(true);
  };

  const handleRegister = () => {
    setAuthModalTab("register");
    setAuthModalOpen(true);
  };

  const handleEnterDashboard = () => {
    navigate("/app");
  };

  const handleAuthSuccess = () => {
    setIsLoggedIn(true);
    navigate("/app");
  };

  // 功能特性数据
  const features = [
    {
      icon: <RobotOutlined className="feature-icon" />,
      title: "多模型AI助手",
      description: "支持GPT、Claude、Gemini等多种AI模型，智能对话，创意无限",
      color: "#1890ff",
    },
    {
      icon: <BookOutlined className="feature-icon" />,
      title: "智能知识库",
      description: "构建专属知识库，文档智能检索，知识管理更高效",
      color: "#52c41a",
    },
    {
      icon: <AppstoreOutlined className="feature-icon" />,
      title: "丰富插件生态",
      description: "海量插件扩展功能，定制化AI应用体验",
      color: "#722ed1",
    },
    {
      icon: <FileTextOutlined className="feature-icon" />,
      title: "提示词管理",
      description: "专业提示词库，提升AI对话效果，工作效率倍增",
      color: "#fa8c16",
    },
  ];

  // 统计数据
  const stats = [
    { title: "AI模型", value: "50+", suffix: "种" },
    { title: "插件数量", value: "100+", suffix: "个" },
    { title: "用户数量", value: "10K+", suffix: "人" },
    { title: "对话次数", value: "1M+", suffix: "次" },
  ];

  // 优势特点
  const advantages = [
    {
      icon: <ThunderboltOutlined />,
      title: "极速响应",
      description: "毫秒级AI响应，实时对话体验",
    },
    {
      icon: <SafetyOutlined />,
      title: "安全可靠",
      description: "企业级安全保障，数据隐私保护",
    },
    {
      icon: <GlobalOutlined />,
      title: "全球部署",
      description: "多地域服务器，稳定可靠服务",
    },
    {
      icon: <RocketOutlined />,
      title: "持续更新",
      description: "定期功能更新，技术前沿跟进",
    },
  ];

  if (loading) {
    return (
      <div className="loading-container">
        <div className="loading-spinner"></div>
        <Text>正在加载...</Text>
      </div>
    );
  }

  return (
    <div className="home-container">
      {/* 认证弹窗 */}
      <AuthModal
        open={authModalOpen}
        onClose={() => setAuthModalOpen(false)}
        defaultTab={authModalTab}
        onSuccess={handleAuthSuccess}
      />
      {/* 导航栏 */}
      <header className="header">
        <div className="header-content">
          <div className="logo-section">
            <Avatar size={48} src="/logo.png" />
            <Title
              level={3}
              style={{ margin: 0, marginLeft: 12, color: "#fff" }}
            >
              MoAI
            </Title>
          </div>
          <div className="nav-actions">
            {isLoggedIn ? (
              <Button
                type="primary"
                size="large"
                icon={<AppstoreAddOutlined />}
                onClick={handleEnterDashboard}
                className="enter-dashboard-btn"
              >
                进入后台
              </Button>
            ) : (
              <Space size="middle">
                <Button
                  size="large"
                  icon={<LoginOutlined />}
                  onClick={handleLogin}
                  className="login-btn"
                >
                  登录
                </Button>
                <Button
                  type="primary"
                  size="large"
                  onClick={handleRegister}
                  className="register-btn"
                >
                  注册
                </Button>
              </Space>
            )}
          </div>
        </div>
      </header>

      {/* 主横幅 */}
      <section className="hero-section">
        <div className="hero-content">
          <div className="hero-text">
            <Title level={1} className="hero-title">
              下一代
              <span className="gradient-text"> AI 应用平台</span>
            </Title>
            <Paragraph className="hero-description">
              集成多种AI模型，构建智能知识库，扩展丰富插件，打造专属AI助手。
              让AI成为您工作和生活的得力助手。
            </Paragraph>
            <div className="hero-actions">
              {!isLoggedIn && (
                <Button
                  type="primary"
                  size="large"
                  className="cta-button"
                  onClick={handleRegister}
                >
                  立即开始
                  <ArrowRightOutlined />
                </Button>
              )}
              {isLoggedIn && (
                <Button
                  type="primary"
                  size="large"
                  className="cta-button"
                  onClick={handleEnterDashboard}
                >
                  进入工作台
                  <ArrowRightOutlined />
                </Button>
              )}
            </div>
          </div>
          <div className="hero-visual">
            <div className="floating-cards">
              <div className="floating-card card-1">
                <RobotOutlined />
                <Text>模型管理</Text>
              </div>
              <div className="floating-card card-2">
                <AppstoreOutlined />
                <Text>MCP 插件</Text>
              </div>
              <div className="floating-card card-3">
                <ApiOutlined />
                <Text>OpenApi插件</Text>
              </div>
              <div className="floating-card card-4">
                <BookOutlined />
                <Text>知识库</Text>
              </div>
              <div className="floating-card card-5">
                <GlobalOutlined />
                <Text>知识库爬虫</Text>
              </div>
              <div className="floating-card card-6">
                <RobotOutlined />
                <Text>AI助手</Text>
              </div>
              <div className="floating-card card-7">
                <UserOutlined />
                <Text>单点登录</Text>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 统计数据 */}
      <section className="stats-section">
        <div className="stats-content">
          <Row gutter={[32, 32]} justify="center">
            {stats.map((stat, index) => (
              <Col xs={12} sm={6} key={index}>
                <Card className="stat-card">
                  <Statistic
                    title={stat.title}
                    value={stat.value}
                    suffix={stat.suffix}
                    valueStyle={{ color: "#1890ff", fontSize: "2rem" }}
                  />
                </Card>
              </Col>
            ))}
          </Row>
        </div>
      </section>

      {/* 功能特性 */}
      <section className="features-section">
        <div className="features-content">
          <div className="section-header">
            <Title level={2} className="section-title">
              强大功能，<span className="gradient-text">无限可能</span>
            </Title>
            <Paragraph className="section-description">
              一站式AI应用平台，满足您的各种AI需求
            </Paragraph>
          </div>
          <Row gutter={[32, 32]} className="features-grid">
            {features.map((feature, index) => (
              <Col xs={24} sm={12} lg={6} key={index}>
                <Card className="feature-card" hoverable>
                  <div
                    className="feature-icon-wrapper"
                    style={{ color: feature.color }}
                  >
                    {feature.icon}
                  </div>
                  <Title level={4} className="feature-title">
                    {feature.title}
                  </Title>
                  <Paragraph className="feature-description">
                    {feature.description}
                  </Paragraph>
                </Card>
              </Col>
            ))}
          </Row>
        </div>
      </section>

      {/* 页脚 */}
      <footer className="footer">
        <Divider style={{ borderColor: "#333" }} />
        <div className="footer-bottom">
          <Text style={{ color: "#999" }}>
            © 2025 MoAI. All rights reserved.
          </Text>
        </div>
      </footer>
    </div>
  );
};

export default Home;
