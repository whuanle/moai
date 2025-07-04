import { 
  Col, 
  Row, 
  Card, 
  Form, 
  Input, 
  Button, 
  message, 
  Typography, 
  Space,
  Divider,
  Layout
} from "antd";
import { 
  UserOutlined, 
  LockOutlined, 
  MailOutlined, 
  PhoneOutlined, 
  UserAddOutlined,
  IdcardOutlined
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import { useEffect, useState } from "react";
import { RsaHelper } from "../../helper/RsaHalper";
import { GetAllowApiClient } from "../ServiceClient";
import { CheckToken, GetServiceInfo, RefreshServerInfo } from "../../InitService";
import { proxyFormRequestError } from "../../helper/RequestError";
import "./Register.css";

const { Title, Text } = Typography;
const { Content } = Layout;

interface RegisterFormValues {
  userName: string;
  password: string;
  confirmPassword: string;
  email: string;
  nickName: string;
  phone: string;
}

export default function Register() {
  const [form] = Form.useForm<RegisterFormValues>();
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const initializeRegister = async () => {
      try {
        const client = GetAllowApiClient();
        await RefreshServerInfo(client);
        
        const isVerified = await CheckToken();
        if (isVerified) {
          messageApi.success("您已经登录，正在重定向到首页");
          setTimeout(() => navigate("/app"), 1000);
        }
      } catch (error) {
        console.error("初始化注册页面失败:", error);
      }
    };

    initializeRegister();
  }, [messageApi, navigate]);

  const onFinish = async (values: RegisterFormValues) => {
    setLoading(true);
    try {
      const serviceInfo = await GetServiceInfo();

      const encryptedPassword = RsaHelper.encrypt(
        serviceInfo.rsaPublic,
        values.password
      );

      const client = GetAllowApiClient();
      await client.api.account.register.post({
        userName: values.userName,
        password: encryptedPassword,
        email: values.email,
        nickName: values.nickName,
        phone: values.phone,
      });

      messageApi.success("注册成功，正在重定向到登录界面");
      setTimeout(() => {
        navigate("/login");
      }, 1000);
    } catch (error) {
      console.log("Register error:", error);
      const typedError = error as {
        detail?: string;
        errors?: Record<string, string[]>;
      };
      if (typedError.errors && Object.keys(typedError.errors).length > 0) {
        messageApi.error("注册失败");
        proxyFormRequestError(error, messageApi, form);
      } else if (typedError.detail) {
        messageApi.error(typedError.detail);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleLoginClick = () => {
    navigate("/login");
  };

  return (
    <Layout className="login-container">
      {contextHolder}
      
      <Content>
        <Row 
          justify="center" 
          align="middle" 
          style={{ minHeight: "100vh", padding: "24px" }}
        >
          <Col 
            xs={24} 
            sm={20} 
            md={16} 
            lg={12} 
            xl={8}
            xxl={6}
          >
            <Card
              bordered={false}
              className="login-card"
              bodyStyle={{ padding: "56px 48px" }}
            >
              <Space direction="vertical" size="large" style={{ width: "100%" }}>
                {/* 标题区域 */}
                <div style={{ textAlign: "center" }}>
                  <Title level={2} className="login-title" style={{ marginBottom: "8px" }}>
                    创建账户
                  </Title>
                  <Text type="secondary" style={{ fontSize: "16px" }}>
                    请填写以下信息完成注册
                  </Text>
                </div>

                <Divider style={{ margin: "24px 0" }} />

                {/* 表单区域 */}
                <Form
                  form={form}
                  name="register"
                  onFinish={onFinish}
                  layout="vertical"
                  size="large"
                  autoComplete="off"
                >
                  <Form.Item
                    name="userName"
                    label={<Text strong>用户名</Text>}
                    rules={[
                      { required: true, message: "请输入用户名" },
                      { min: 2, message: "用户名至少2个字符" }
                    ]}
                  >
                    <Input
                      prefix={<UserOutlined />}
                      placeholder="请输入您的用户名"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item
                    name="password"
                    label={<Text strong>密码</Text>}
                    rules={[
                      { required: true, message: "请输入密码" },
                      { min: 6, message: "密码至少6个字符" },
                      {
                    pattern: /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$/,
                    message: "密码必须包含字母、数字和特殊符号",
                  },
                    ]}
                  >
                    <Input.Password
                      prefix={<LockOutlined />}
                      placeholder="请输入密码"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item
                    name="confirmPassword"
                    label={<Text strong>确认密码</Text>}
                    dependencies={["password"]}
                    rules={[
                      { required: true, message: "请确认密码" },
                      ({ getFieldValue }) => ({
                        validator(_, value) {
                          if (!value || getFieldValue("password") === value) {
                            return Promise.resolve();
                          }
                          return Promise.reject(new Error("两次输入的密码不一致"));
                        },
                      }),
                    ]}
                  >
                    <Input.Password
                      prefix={<LockOutlined />}
                      placeholder="请再次输入密码"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item
                    name="email"
                    label={<Text strong>邮箱</Text>}
                    rules={[
                      { required: true, message: "请输入邮箱" },
                      { type: "email", message: "请输入有效的邮箱地址" },
                    ]}
                  >
                    <Input
                      prefix={<MailOutlined />}
                      placeholder="请输入您的邮箱地址"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item
                    name="nickName"
                    label={<Text strong>昵称</Text>}
                    rules={[{ required: true, message: "请输入昵称" }]}
                  >
                    <Input
                      prefix={<IdcardOutlined />}
                      placeholder="请输入您的昵称"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item
                    name="phone"
                    label={<Text strong>手机号</Text>}
                    rules={[
                      { required: true, message: "请输入手机号" },
                      { pattern: /^1[3-9]\d{9}$/, message: "请输入有效的手机号" },
                    ]}
                  >
                    <Input
                      prefix={<PhoneOutlined />}
                      placeholder="请输入您的手机号"
                      className="login-input"
                      style={{ height: "48px" }}
                    />
                  </Form.Item>

                  <Form.Item style={{ marginTop: "32px", marginBottom: "24px" }}>
                    <Button
                      type="primary"
                      htmlType="submit"
                      block
                      size="large"
                      icon={<UserAddOutlined />}
                      className="login-button"
                      loading={loading}
                      style={{
                        height: "48px",
                        fontSize: "16px",
                        fontWeight: "600"
                      }}
                    >
                      立即注册
                    </Button>
                  </Form.Item>
                </Form>

                <Divider style={{ margin: "24px 0" }} />

                {/* 登录链接 */}
                <div style={{ textAlign: "center" }}>
                  <Text type="secondary" style={{ fontSize: "14px" }}>
                    已有账户？
                  </Text>
                  <Button
                    type="link"
                    onClick={handleLoginClick}
                    className="register-link"
                    style={{
                      fontSize: "14px",
                      padding: "0 8px"
                    }}
                  >
                    立即登录
                  </Button>
                </div>
              </Space>
            </Card>
          </Col>
        </Row>
      </Content>
    </Layout>
  );
}
