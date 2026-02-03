import React, { useEffect, useState, useMemo } from "react";
import {
  Button,
  Card,
  Row,
  Col,
  Typography,
  Space,
  Avatar,
  Divider,
  Statistic,
} from "antd";
import {
  RobotOutlined,
  BookOutlined,
  AppstoreOutlined,
  FileTextOutlined,
  LoginOutlined,
  AppstoreAddOutlined,
  ThunderboltOutlined,
  SafetyOutlined,
  GlobalOutlined,
  RocketOutlined,
  ArrowRightOutlined,
  ApiOutlined,
  TeamOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import useAppStore from "./stateshare/store";
import "./Home-optimized.css";

const { Title, Paragraph, Text } = Typography;

// 功能特性数据 - 移到组件外部避免重复创建
const FEATURES = [
  {
    icon: RobotOutlined,
    title: "多模型AI助手",
    description: "支持GPT、Claude、Gemini等多种AI模型，智能对话，创意无限",
    color: "#1677ff",
  },
  {
    icon: BookOutlined,
    title: "智能知识库",
    description: "构建专属知识库，文档智能检索，知识管理更高效",
    color: "#52c41a",
  },
  {
    icon: AppstoreOutlined,
    title: "丰富插件生态",
    description: "海量插件扩展功能，定制化AI应用体验",
    color: "#722ed1",
  },
  {
    icon: FileTextOutlined,
    title: "提示词管理",
    description: "专业提示词库，提升AI对话效果，工作效率倍增",
    color: "#fa8c16",
  },
];

// 统计数据
const STATS = [
  { title: "AI模型", value: "50+", suffix: "种" },
  { title: "插件数量", value: "100+", suffix: "个" },
  { title: "用户数量", value: "10K+", suffix: "人" },
  { title: "对话次数", value: "1M+", suffix: "次" },
];

// 优势特点
const ADVANTAGES = [
  {
    icon: ThunderboltOutlined,
    title: "极速响应",
    description: "毫秒级AI响应，实时对话体验",
  },
  {
    icon: SafetyOutlined,
    title: "安全可靠",
    description: "企业级安全保障，数据隐私保护",
  },
  {
    icon: GlobalOutlined,
    title: "全球部署",
    description: "多地域服务器，稳定可靠服务",
  },
  {
    icon: RocketOutlined,
    title: "持续更新",
    description: "定期功能更新，技术前沿跟进",
  },
];

// 简化的浮动卡片数据
const FLOATING_CARDS = [
  { icon: RobotOutlined, text: "AI助手", delay: 0 },
  { icon: AppstoreOutlined, text: "应用中心", delay: 0.2 },
  { icon: BookOutlined, text: "知识库", delay: 0.4 },
  { icon: TeamOutlined, text: "团队协作", delay: 0.6 },
];

// 优化的特性卡片组件
const FeatureCard = React.memo(({ feature }: { feature: typeof FEATURES[0] }) => {
  const Icon = feature.icon;
  return (
    <Card className="feature-card" hoverable>
      <div className="feature-icon-wrapper" style={{ color: feature.color }}>
        <Icon className="feature-icon" />
      </div>
      <Title level={4} className="feature-title">
        {feature.title}
      </Title>
      <Paragraph className="feature-description">
        {feature.description}
      </Paragraph>
    </Card>
  );
});

FeatureCard.displayName = 'FeatureCard';

// 优化的统计卡片组件
const StatCard = React.memo(({ stat }: { stat: typeof STATS[0] }) => (
  <Card className="stat-card">
    <Statistic
      title={stat.title}
      value={stat.value}
      suffix={stat.suffix}
      valueStyle={{ color: "#1677ff", fontSize: "2rem", fontWeight: 600 }}
    />
  </Card>
));

StatCard.displayName = 'StatCard';

// 优化的浮动卡片组件
const FloatingCard = React.memo(({ card, index }: { card: typeof FLOATING_CARDS[0], index: number }) => {
  const Icon = card.icon;
  return (
    <div 
      className={`floating-card card-${index + 1}`}
      style={{ animationDelay: `${card.delay}s` }}
    >
      <Icon />
      <Text>{card.text}</Text>
    </div>
  );
});

FloatingCard.displayName = 'FloatingCard';

const Home: React.FC = () => {
  const navigate = useNavigate();
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // 检查登录状态
    const checkLoginStatus = () => {
      const user = useAppStore.getState().getUserInfo();
      setIsLoggedIn(!!user?.accessToken);
      setLoading(false);
    };

    checkLoginStatus();
  }, []);

  // 使用 useMemo 缓存导航函数
  const handleLogin = useMemo(() => () => navigate("/login"), [navigate]);
  const handleRegister = useMemo(() => () => navigate("/register"), [navigate]);
  const handleEnterDashboard = useMemo(() => () => navigate("/app"), [navigate]);

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
      {/* 导航栏 */}
      <header className="header">
        <div className="header-content">
          <div className="logo-section">
            <Avatar size={48} src="/logo.png" />
            <Title level={3} className="logo-title">
              MoAI
            </Title>
          </div>
          <nav className="nav-actions">
            {isLoggedIn ? (
              <Button
                type="primary"
                size="large"
                icon={<AppstoreAddOutlined />}
                onClick={handleEnterDashboard}
              >
                进入后台
              </Button>
            ) : (
              <Space size="middle">
                <Button
                  size="large"
                  icon={<LoginOutlined />}
                  onClick={handleLogin}
                >
                  登录
                </Button>
                <Button
                  type="primary"
                  size="large"
                  onClick={handleRegister}
                >
                  注册
                </Button>
              </Space>
            )}
          </nav>
        </div>
      </header>

      {/* 主横幅 - 简化版 */}
      <section className="hero-section">
        <div className="hero-content">
          <div className="hero-text">
            <Title level={1} className="hero-title">
              下一代 <span className="gradient-text">AI 应用平台</span>
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
                  icon={<ArrowRightOutlined />}
                >
                  立即开始
                </Button>
              )}
              {isLoggedIn && (
                <Button
                  type="primary"
                  size="large"
                  className="cta-button"
                  onClick={handleEnterDashboard}
                  icon={<ArrowRightOutlined />}
                >
                  进入工作台
                </Button>
              )}
            </div>
          </div>
          <div className="hero-visual">
            <div className="floating-cards">
              {FLOATING_CARDS.map((card, index) => (
                <FloatingCard key={index} card={card} index={index} />
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* 统计数据 */}
      <section className="stats-section">
        <div className="stats-content">
          <Row gutter={[24, 24]} justify="center">
            {STATS.map((stat, index) => (
              <Col xs={12} sm={6} key={index}>
                <StatCard stat={stat} />
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
          <Row gutter={[24, 24]} className="features-grid">
            {FEATURES.map((feature, index) => (
              <Col xs={24} sm={12} lg={6} key={index}>
                <FeatureCard feature={feature} />
              </Col>
            ))}
          </Row>
        </div>
      </section>

      {/* 优势特点 */}
      <section className="advantages-section">
        <div className="advantages-content">
          <Row gutter={[24, 24]}>
            {ADVANTAGES.map((advantage, index) => {
              const Icon = advantage.icon;
              return (
                <Col xs={12} sm={6} key={index}>
                  <div className="advantage-item">
                    <Icon className="advantage-icon" />
                    <Title level={4} className="advantage-title">
                      {advantage.title}
                    </Title>
                    <Paragraph className="advantage-description">
                      {advantage.description}
                    </Paragraph>
                  </div>
                </Col>
              );
            })}
          </Row>
        </div>
      </section>

      {/* 页脚 */}
      <footer className="footer">
        <Divider />
        <div className="footer-bottom">
          <Text type="secondary">
            © 2025 MoAI. All rights reserved.
          </Text>
        </div>
      </footer>
    </div>
  );
};

export default Home;
