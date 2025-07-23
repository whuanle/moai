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
import { UserOutlined, LockOutlined, LoginOutlined } from "@ant-design/icons";
import { useNavigate, useSearchParams } from "react-router";
import { useEffect, useState } from "react";
import { RsaHelper } from "../../helper/RsaHalper";
import { GetAllowApiClient } from "../ServiceClient";
import {
  CheckToken,
  GetServiceInfo,
  RefreshServerInfo,
  SetUserInfo,
} from "../../InitService";
import { proxyFormRequestError } from "../../helper/RequestError";
import { QueryAllOAuthPrividerCommandResponseItem } from "../../apiClient/models";
import "./Login.css";

const { Title, Text } = Typography;
const { Content } = Layout;

interface LoginFormValues {
  username: string;
  password: string;
}

export default function Login() {
  const [form] = Form.useForm<LoginFormValues>();
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [oauthProviders, setOauthProviders] = useState<QueryAllOAuthPrividerCommandResponseItem[]>([]);
  const [loading, setLoading] = useState(false);

  // 获取重定向路径参数
  const redirectUri = searchParams.get('redirect_uri');

  // 验证重定向路径的安全性
  const validateRedirectUri = (uri: string | null): string | null => {
    if (!uri || uri.trim() === '') {
      return null;
    }
    
    // 检查是否包含协议前缀（http://, https://, ftp:// 等）
    if (/^[a-zA-Z]+:\/\//.test(uri)) {
      console.warn('重定向路径包含协议前缀，已忽略:', uri);
      return null;
    }
    
    // 检查是否以斜杠开头（相对路径）
    if (!uri.startsWith('/')) {
      console.warn('重定向路径不是以斜杠开头的相对路径，已忽略:', uri);
      return null;
    }
    
    return uri;
  };

  const safeRedirectUri = validateRedirectUri(redirectUri);

  useEffect(() => {
    const initializeLogin = async () => {
      try {
        const client = GetAllowApiClient();
        await RefreshServerInfo(client);
        
        const isVerified = await CheckToken();
        if (isVerified) {
          messageApi.success("您已经登录，正在重定向到首页");
          // 如果有重定向路径，优先跳转到指定路径
          const targetPath = safeRedirectUri || "/app";
          setTimeout(() => navigate(targetPath), 1000);
        }

        // 获取第三方登录列表
        await fetchOAuthProviders();
      } catch (error) {
        console.error("初始化登录页面失败:", error);
      }
    };

    initializeLogin();
  }, [messageApi, navigate]);

  const fetchOAuthProviders = async () => {
    try {
      const client = GetAllowApiClient();
      const response = await client.api.account.oauth_prividers.get();
      if (response && response.items) {
        setOauthProviders(response.items);
      }
    } catch (error) {
      console.error("获取第三方登录列表失败:", error);
    }
  };

  const handleLogin = async (values: LoginFormValues) => {
    setLoading(true);
    try {
      const serviceInfo = await GetServiceInfo();
      const encryptedPassword = RsaHelper.encrypt(
        serviceInfo.rsaPublic,
        values.password
      );

      // 从URL参数中获取oAuthBindId
      const oAuthBindId = searchParams.get('oAuthBindId');

      const client = GetAllowApiClient();
      const loginData: any = {
        userName: values.username,
        password: encryptedPassword,
      };

      // 如果oAuthBindId存在且不为空，则添加到请求参数中
      if (oAuthBindId && oAuthBindId.trim() !== '') {
        loginData.oAuthBindId = oAuthBindId;
      }

      const response = await client.api.account.login.post(loginData);

      if (response) {
        SetUserInfo(response);
        messageApi.success("登录成功，正在重定向到主页");
        // 如果有重定向路径，优先跳转到指定路径
        const targetPath = safeRedirectUri || "/app";
        setTimeout(() => navigate(targetPath), 1000);
      } else {
        messageApi.error("登录失败");
      }
    } catch (error) {
      console.log("Register error:", error);
      const typedError = error as {
        detail?: string;
        errors?: Record<string, string[]>;
      };
      if (typedError.errors && Object.keys(typedError.errors).length > 0) {
        messageApi.error("登录失败");
        proxyFormRequestError(error, messageApi, form);
      }else if(typedError.detail){
        messageApi.error(typedError.detail);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleRegisterClick = () => {
    navigate("/register");
  };

  const handleOAuthLogin = (redirectUrl: string) => {
    if (redirectUrl) {
      window.location.href = redirectUrl;
    }
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
                    欢迎回来
                  </Title>
                  <Text type="secondary" style={{ fontSize: "16px" }}>
                    请登录您的账户
                  </Text>
                </div>

                <Divider style={{ margin: "24px 0" }} />

                {/* 表单区域 */}
                <Form
                  form={form}
                  name="login"
                  onFinish={handleLogin}
                  layout="vertical"
                  size="large"
                  autoComplete="off"
                >
                  <Form.Item
                    name="username"
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
                      { min: 6, message: "密码至少6个字符" }
                    ]}
                  >
                    <Input.Password
                      prefix={<LockOutlined />}
                      placeholder="请输入密码"
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
                      icon={<LoginOutlined />}
                      className="login-button"
                      loading={loading}
                      style={{
                        height: "48px",
                        fontSize: "16px",
                        fontWeight: "600"
                      }}
                    >
                      立即登录
                    </Button>
                  </Form.Item>
                </Form>

                {/* 第三方登录区域 */}
                {oauthProviders.length > 0 && (
                  <>
                    <Divider style={{ margin: "16px 0" }}>
                      <Text type="secondary" style={{ fontSize: "14px" }}>
                        或使用第三方登录
                      </Text>
                    </Divider>
                    
                    <div style={{ 
                      display: "flex", 
                      justifyContent: "center", 
                      gap: "16px",
                      flexWrap: "wrap",
                      marginBottom: "8px"
                    }}>
                      {oauthProviders.map((provider) => (
                        <Button
                          key={provider.oAuthId}
                          type="text"
                          size="large"
                          onClick={() => handleOAuthLogin(provider.redirectUrl || "")}
                          className="oauth-provider-button"
                          title={provider.name || ""}
                        >
                          {provider.iconUrl ? (
                            <img
                              src={provider.iconUrl}
                              alt={provider.name || ""}
                              className="oauth-provider-icon"
                            />
                          ) : (
                            <Text className="oauth-provider-fallback">
                              {provider.name?.charAt(0) || "?"}
                            </Text>
                          )}
                        </Button>
                      ))}
                    </div>
                  </>
                )}

                <Divider style={{ margin: "24px 0" }} />

                {/* 注册链接 */}
                <div style={{ textAlign: "center" }}>
                  <Text type="secondary" style={{ fontSize: "14px" }}>
                    还没有账户？
                  </Text>
                  <Button
                    type="link"
                    onClick={handleRegisterClick}
                    className="register-link"
                    style={{
                      fontSize: "14px",
                      padding: "0 8px"
                    }}
                  >
                    立即注册
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
