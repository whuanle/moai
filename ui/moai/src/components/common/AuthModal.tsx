import React, { useState, useEffect } from "react";
import {
  Modal,
  Form,
  Input,
  Button,
  message,
  Typography,
  Space,
  Divider,
  Tabs,
} from "antd";
import {
  UserOutlined,
  LockOutlined,
  MailOutlined,
  PhoneOutlined,
  LoginOutlined,
  UserAddOutlined,
  IdcardOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import { RsaHelper } from "../../helper/RsaHalper";
import { GetAllowApiClient } from "../ServiceClient";
import {
  GetServiceInfo,
  RefreshServerInfo,
  SetUserInfo,
} from "../../InitService";
import { proxyFormRequestError } from "../../helper/RequestError";
import { QueryAllOAuthPrividerCommandResponseItem } from "../../apiClient/models";
import "./AuthModal.css";

const { Text } = Typography;

interface AuthModalProps {
  open: boolean;
  onClose: () => void;
  defaultTab?: "login" | "register";
  redirectPath?: string;
  onSuccess?: () => void;
}

interface LoginFormValues {
  username: string;
  password: string;
}

interface RegisterFormValues {
  userName: string;
  password: string;
  confirmPassword: string;
  email: string;
  nickName: string;
  phone: string;
}

export default function AuthModal({
  open,
  onClose,
  defaultTab = "login",
  redirectPath,
  onSuccess,
}: AuthModalProps) {
  const [loginForm] = Form.useForm<LoginFormValues>();
  const [registerForm] = Form.useForm<RegisterFormValues>();
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<string>(defaultTab);
  const [loginLoading, setLoginLoading] = useState(false);
  const [registerLoading, setRegisterLoading] = useState(false);
  const [oauthProviders, setOauthProviders] = useState<
    QueryAllOAuthPrividerCommandResponseItem[]
  >([]);

  // 初始化
  useEffect(() => {
    if (open) {
      fetchOAuthProviders();
      // 重置表单
      loginForm.resetFields();
      registerForm.resetFields();
      // 设置默认 tab
      setActiveTab(defaultTab);
    }
  }, [open, defaultTab, loginForm, registerForm]);

  // 获取第三方登录列表
  const fetchOAuthProviders = async () => {
    try {
      const client = GetAllowApiClient();
      await RefreshServerInfo(client);
      const response = await client.api.account.oauth_prividers.get();
      if (response && response.items) {
        setOauthProviders(response.items);
      }
    } catch (error) {
      console.error("获取第三方登录列表失败:", error);
    }
  };

  // 处理登录
  const handleLogin = async (values: LoginFormValues) => {
    setLoginLoading(true);
    try {
      const serviceInfo = await GetServiceInfo();
      const encryptedPassword = RsaHelper.encrypt(
        serviceInfo.rsaPublic,
        values.password
      );

      const client = GetAllowApiClient();
      const response = await client.api.account.login.post({
        userName: values.username,
        password: encryptedPassword,
      });

      if (response) {
        SetUserInfo(response);
        messageApi.success("登录成功");
        
        // 延迟关闭弹窗，让用户看到成功提示
        setTimeout(() => {
          onClose();
          
          // 执行成功回调或导航
          if (onSuccess) {
            onSuccess();
          } else if (redirectPath) {
            navigate(redirectPath);
          } else {
            navigate("/app");
          }
        }, 500);
      }
    } catch (error) {
      console.error("登录失败:", error);
      const typedError = error as {
        detail?: string;
        errors?: Record<string, string[]>;
      };
      if (typedError.errors && Object.keys(typedError.errors).length > 0) {
        messageApi.error("登录失败");
        proxyFormRequestError(error, messageApi, loginForm);
      } else if (typedError.detail) {
        messageApi.error(typedError.detail);
      }
    } finally {
      setLoginLoading(false);
    }
  };

  // 处理注册
  const handleRegister = async (values: RegisterFormValues) => {
    setRegisterLoading(true);
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

      messageApi.success("注册成功，请登录");
      
      // 切换到登录标签
      setTimeout(() => {
        setActiveTab("login");
        registerForm.resetFields();
      }, 500);
    } catch (error) {
      console.error("注册失败:", error);
      const typedError = error as {
        detail?: string;
        errors?: Record<string, string[]>;
      };
      if (typedError.errors && Object.keys(typedError.errors).length > 0) {
        messageApi.error("注册失败");
        proxyFormRequestError(error, messageApi, registerForm);
      } else if (typedError.detail) {
        messageApi.error(typedError.detail);
      }
    } finally {
      setRegisterLoading(false);
    }
  };

  // 处理第三方登录
  const handleOAuthLogin = (redirectUrl: string) => {
    if (redirectUrl) {
      window.location.href = redirectUrl;
    }
  };

  // 登录表单
  const LoginForm = (
    <Form
      form={loginForm}
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
          { min: 2, message: "用户名至少2个字符" },
        ]}
      >
        <Input
          prefix={<UserOutlined />}
          placeholder="请输入您的用户名"
          className="auth-input"
        />
      </Form.Item>

      <Form.Item
        name="password"
        label={<Text strong>密码</Text>}
        rules={[
          { required: true, message: "请输入密码" },
          { min: 6, message: "密码至少6个字符" },
        ]}
      >
        <Input.Password
          prefix={<LockOutlined />}
          placeholder="请输入密码"
          className="auth-input"
        />
      </Form.Item>

      <Form.Item style={{ marginTop: 24, marginBottom: 0 }}>
        <Button
          type="primary"
          htmlType="submit"
          block
          size="large"
          icon={<LoginOutlined />}
          loading={loginLoading}
          className="auth-button"
        >
          立即登录
        </Button>
      </Form.Item>

      {/* 第三方登录 */}
      {oauthProviders.length > 0 && (
        <>
          <Divider style={{ margin: "20px 0" }}>
            <Text type="secondary" style={{ fontSize: 12 }}>
              或使用第三方登录
            </Text>
          </Divider>

          <div className="oauth-providers">
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
    </Form>
  );

  // 注册表单
  const RegisterForm = (
    <Form
      form={registerForm}
      name="register"
      onFinish={handleRegister}
      layout="vertical"
      size="large"
      autoComplete="off"
    >
      <Form.Item
        name="userName"
        label={<Text strong>用户名</Text>}
        rules={[
          { required: true, message: "请输入用户名" },
          { min: 2, message: "用户名至少2个字符" },
        ]}
      >
        <Input
          prefix={<UserOutlined />}
          placeholder="请输入您的用户名"
          className="auth-input"
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
          placeholder="请输入密码（8-20位，含字母和数字）"
          className="auth-input"
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
          className="auth-input"
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
          className="auth-input"
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
          className="auth-input"
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
          className="auth-input"
        />
      </Form.Item>

      <Form.Item style={{ marginTop: 24, marginBottom: 0 }}>
        <Button
          type="primary"
          htmlType="submit"
          block
          size="large"
          icon={<UserAddOutlined />}
          loading={registerLoading}
          className="auth-button"
        >
          立即注册
        </Button>
      </Form.Item>
    </Form>
  );

  // Tab 配置
  const tabItems = [
    {
      key: "login",
      label: "登录",
      children: LoginForm,
    },
    {
      key: "register",
      label: "注册",
      children: RegisterForm,
    },
  ];

  return (
    <>
      {contextHolder}
      <Modal
        open={open}
        onCancel={onClose}
        footer={null}
        width={480}
        centered
        className="auth-modal"
        destroyOnClose
        getContainer={false}
        maskClosable={true}
      >
        <div className="auth-modal-content">
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={tabItems}
            centered
            size="large"
            className="auth-tabs"
          />
        </div>
      </Modal>
    </>
  );
}
