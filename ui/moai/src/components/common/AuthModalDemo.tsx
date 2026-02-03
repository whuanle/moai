/**
 * AuthModal 演示页面
 * 用于测试和展示登录/注册弹窗的各种用法
 * 
 * 访问路径: /auth-demo (需要在路由中配置)
 */

import { useState } from "react";
import { Button, Card, Space, Typography, Divider, Tag } from "antd";
import {
  LoginOutlined,
  UserAddOutlined,
  CheckCircleOutlined,
} from "@ant-design/icons";
import AuthModal from "./AuthModal";

const { Title, Paragraph, Text } = Typography;

export default function AuthModalDemo() {
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const [authModalTab, setAuthModalTab] = useState<"login" | "register">("login");
  const [lastAction, setLastAction] = useState<string>("");

  const handleOpenLogin = () => {
    setAuthModalTab("login");
    setAuthModalOpen(true);
    setLastAction("打开登录弹窗");
  };

  const handleOpenRegister = () => {
    setAuthModalTab("register");
    setAuthModalOpen(true);
    setLastAction("打开注册弹窗");
  };

  const handleSuccess = () => {
    setLastAction("认证成功！");
    console.log("认证成功回调被触发");
  };

  const handleClose = () => {
    setAuthModalOpen(false);
    setLastAction("关闭弹窗");
  };

  return (
    <div style={{ padding: "40px", maxWidth: "1200px", margin: "0 auto" }}>
      {/* 认证弹窗 */}
      <AuthModal
        open={authModalOpen}
        onClose={handleClose}
        defaultTab={authModalTab}
        onSuccess={handleSuccess}
      />

      {/* 页面标题 */}
      <div style={{ textAlign: "center", marginBottom: "40px" }}>
        <Title level={2}>
          <CheckCircleOutlined style={{ color: "#52c41a", marginRight: "12px" }} />
          AuthModal 组件演示
        </Title>
        <Paragraph type="secondary" style={{ fontSize: "16px" }}>
          测试登录/注册弹窗的各种功能和交互
        </Paragraph>
      </div>

      {/* 功能演示区域 */}
      <Space direction="vertical" size="large" style={{ width: "100%" }}>
        {/* 基础用法 */}
        <Card title="基础用法" bordered={false}>
          <Space size="middle" wrap>
            <Button
              type="primary"
              size="large"
              icon={<LoginOutlined />}
              onClick={handleOpenLogin}
            >
              打开登录弹窗
            </Button>
            <Button
              size="large"
              icon={<UserAddOutlined />}
              onClick={handleOpenRegister}
            >
              打开注册弹窗
            </Button>
          </Space>
          <Divider />
          <Paragraph>
            <Text strong>说明：</Text> 点击按钮打开对应的弹窗，弹窗会显示登录或注册表单。
          </Paragraph>
        </Card>

        {/* 状态显示 */}
        <Card title="操作状态" bordered={false}>
          <Space direction="vertical" size="small">
            <div>
              <Text strong>弹窗状态：</Text>
              <Tag color={authModalOpen ? "green" : "default"}>
                {authModalOpen ? "已打开" : "已关闭"}
              </Tag>
            </div>
            <div>
              <Text strong>当前标签：</Text>
              <Tag color="blue">{authModalTab === "login" ? "登录" : "注册"}</Tag>
            </div>
            <div>
              <Text strong>最后操作：</Text>
              <Text type="secondary">{lastAction || "无"}</Text>
            </div>
          </Space>
        </Card>

        {/* 功能特性 */}
        <Card title="功能特性" bordered={false}>
          <Space direction="vertical" size="middle" style={{ width: "100%" }}>
            <div>
              <Text strong>✅ 登录功能</Text>
              <Paragraph type="secondary" style={{ marginLeft: "24px", marginBottom: "8px" }}>
                • 用户名/密码登录<br />
                • 表单验证（用户名至少2字符，密码至少6字符）<br />
                • 第三方 OAuth 登录（如果配置）<br />
                • 错误提示和加载状态
              </Paragraph>
            </div>

            <div>
              <Text strong>✅ 注册功能</Text>
              <Paragraph type="secondary" style={{ marginLeft: "24px", marginBottom: "8px" }}>
                • 完整的注册表单（用户名、密码、邮箱、昵称、手机号）<br />
                • 密码强度验证（8-20位，含字母和数字）<br />
                • 确认密码验证<br />
                • 注册成功后自动切换到登录
              </Paragraph>
            </div>

            <div>
              <Text strong>✅ 交互体验</Text>
              <Paragraph type="secondary" style={{ marginLeft: "24px", marginBottom: "8px" }}>
                • Tab 切换登录/注册<br />
                • 平滑的动画效果<br />
                • 响应式设计（移动端友好）<br />
                • 自动重置表单状态
              </Paragraph>
            </div>
          </Space>
        </Card>

        {/* 使用示例代码 */}
        <Card title="代码示例" bordered={false}>
          <pre style={{
            background: "#f5f5f5",
            padding: "16px",
            borderRadius: "8px",
            overflow: "auto"
          }}>
            <code>{`import { useState } from "react";
import AuthModal from "./components/common/AuthModal";

function YourComponent() {
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const [authModalTab, setAuthModalTab] = useState<"login" | "register">("login");

  return (
    <>
      <Button onClick={() => {
        setAuthModalTab("login");
        setAuthModalOpen(true);
      }}>
        登录
      </Button>

      <AuthModal
        open={authModalOpen}
        onClose={() => setAuthModalOpen(false)}
        defaultTab={authModalTab}
        onSuccess={() => {
          console.log("认证成功");
          // 执行成功后的操作
        }}
      />
    </>
  );
}`}</code>
          </pre>
        </Card>

        {/* 测试提示 */}
        <Card title="测试提示" bordered={false}>
          <Space direction="vertical" size="small">
            <Text>
              <Text strong>1. 测试登录：</Text> 使用已有账号测试登录功能
            </Text>
            <Text>
              <Text strong>2. 测试注册：</Text> 填写完整信息测试注册流程
            </Text>
            <Text>
              <Text strong>3. 测试切换：</Text> 在登录和注册之间切换，观察表单重置
            </Text>
            <Text>
              <Text strong>4. 测试验证：</Text> 故意输入错误信息，查看验证提示
            </Text>
            <Text>
              <Text strong>5. 测试响应式：</Text> 调整浏览器窗口大小，查看移动端效果
            </Text>
          </Space>
        </Card>
      </Space>
    </div>
  );
}
