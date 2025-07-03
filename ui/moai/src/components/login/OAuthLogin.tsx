import React, { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import {
  Card,
  Button,
  message,
  Typography,
  Space,
  Layout,
  Row,
  Col,
  Spin,
  Modal,
} from "antd";
import {
  UserOutlined,
  LinkOutlined,
  UserAddOutlined,
  LogoutOutlined,
} from "@ant-design/icons";
import { GetAllowApiClient, GetApiClient } from "../ServiceClient";
import {
  CheckToken,
  GetServiceInfo,
  GetUserInfo,
  RefreshServerInfo,
  SetUserInfo,
} from "../../InitPage";
import {
  OAuthLoginCommand,
  OAuthLoginCommandResponse,
  OAuthRegisterCommand,
} from "../../apiClient/models";
import useAppStore, { ServerInfoModel, UserInfoModel } from "../../stateshare/store";
import "./Login.css";

const { Content } = Layout;

interface OAuthParams {
  code: string;
  state: string;
}

interface BindAccountState {
  showBindOptions: boolean;
  currentUsername?: string;
  oAuthBindId?: string;
  isBinding: boolean;
  name?: string;
}

interface RegisterAccountState {
  showRegisterOptions: boolean;
  oAuthBindId?: string;
  isRegistering: boolean;
  name?: string;
}

export default function OAuthLogin() {
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const [searchParams] = useSearchParams();
  const [oauthParams, setOAuthParams] = useState<OAuthParams | null>(null);
  const [loading, setLoading] = useState(true);
  const [bindAccountState, setBindAccountState] = useState<BindAccountState>({
    showBindOptions: false,
    isBinding: false,
  });
  const [registerAccountState, setRegisterAccountState] =
    useState<RegisterAccountState>({
      showRegisterOptions: false,
      isRegistering: false,
      name: "",
    });

  const [showBindConfirm, setShowBindConfirm] = useState(false);

  const handleBindAccount = async () => {
    if (!bindAccountState.oAuthBindId) return;

    setBindAccountState((prev) => ({ ...prev, isBinding: true }));

    try {
      const client = GetApiClient();
      await client.api.account.oauth_bind_account.post({
        oAuthBindId: bindAccountState.oAuthBindId,
      });

      messageApi.success("账号绑定成功！");

      navigate("/app");
    } catch (error) {
      console.error("绑定账号时出错:", error);
      messageApi.error("绑定账号失败，请重试");
    } finally {
      setBindAccountState((prev) => ({ ...prev, isBinding: false }));
    }
  };

  const handleBindConfirm = () => {
    setShowBindConfirm(true);
  };

  const handleBindConfirmOk = () => {
    setShowBindConfirm(false);
    handleBindAccount();
  };

  const handleBindConfirmCancel = () => {
    setShowBindConfirm(false);
  };

  const handleLogoutAndRelogin = () => {
    if (!bindAccountState.oAuthBindId) return;

    // 清除当前登录状态
    localStorage.removeItem("token");
    sessionStorage.removeItem("token");

    useAppStore.getState().clearUserInfo();

    // 跳转到登录页面并传递oAuthBindId
    navigate(`/login?oAuthBindId=${bindAccountState.oAuthBindId}`);
  };

  const handleRegisterAccount = async () => {
    if (!registerAccountState.oAuthBindId) return;

    setRegisterAccountState((prev) => ({ ...prev, isRegistering: true }));

    try {
      const client = GetAllowApiClient();
      const response = await client.api.account.oauth_register.post({
        oAuthBindId: registerAccountState.oAuthBindId,
      });

      if (response) {
        messageApi.success("注册成功！");

        SetUserInfo(response);

        navigate("/app");
      }
    } catch (error) {
      console.error("注册账号时出错:", error);
      messageApi.error("注册失败，请重试");
    } finally {
      setRegisterAccountState((prev) => ({ ...prev, isRegistering: false }));
    }
  };

  const handleLogout = () => {
    // 清除当前登录状态
    localStorage.removeItem("token");
    sessionStorage.removeItem("token");

    // 跳转到登录页面
    navigate("/login");
  };

  useEffect(() => {
    const handleOAuthLogin = async () => {
      // 从URL参数中解析OAuth参数
      const code = searchParams.get("code");
      const state = searchParams.get("state");

      // 检查是否所有必需的参数都存在
      if (!code || !state) {
        console.error("缺少必需的OAuth参数:", { code, state });
        messageApi.error("OAuth参数不完整，请重新登录");
        setLoading(false);
        return;
      }

      const params: OAuthParams = {
        code,
        state,
      };
      setOAuthParams(params);
      console.log("解析到的OAuth参数:", params);

      let isVerified = false;
      let currentUsername = "";

      try {
        // 检查用户是否已经登录过
        const client = GetAllowApiClient();
        await RefreshServerInfo(client);

        isVerified = await CheckToken();
        if (isVerified) {
          // 获取当前用户信息
          const userInfoResponse = GetUserInfo();
          currentUsername = userInfoResponse?.userName || "当前用户";
        }
      } catch (error) {
        console.error("检查登录状态时出错:", error);
        messageApi.error("检查登录状态失败，请重试");
      }

      try {
        // 请求OAuth登录
        const client = GetAllowApiClient();
        const oauthLoginResponse = await client.api.account.oauth_login.post({
          code: params.code,
          oAuthId: params.state,
        });

        if (!oauthLoginResponse) {
          throw new Error("OAuth登录响应为空");
        }

        if (oauthLoginResponse.isBindUser) {
          messageApi.success("OAuth登录成功！");
          SetUserInfo(oauthLoginResponse.loginCommandResponse!);
          navigate("/app");
          return;
        }

        // 检查是否需要绑定账号
        if (!oauthLoginResponse.isBindUser) {
          if (isVerified) {
            // 用户已登录，显示绑定选项
            setBindAccountState({
              showBindOptions: true,
              currentUsername,
              oAuthBindId: oauthLoginResponse.oAuthBindId || undefined,
              name: oauthLoginResponse.name || undefined,
              isBinding: false,
            });
            setLoading(false);
            return;
          } else {
            // 用户未登录，显示注册选项
            setRegisterAccountState({
              showRegisterOptions: true,
              oAuthBindId: oauthLoginResponse.oAuthBindId || undefined,
              isRegistering: false,
              name: oauthLoginResponse.name || undefined,
            });
            setLoading(false);
            return;
          }
        }

        // 如果已绑定用户，直接登录成功
        messageApi.success("OAuth登录成功！");
        navigate("/app");
      } catch (error) {
        console.error("OAuth登录时出错:", error);
        messageApi.error("OAuth登录失败，请重试");
        navigate("/login");
      }

      setLoading(false);
    };

    handleOAuthLogin();
  }, [searchParams, navigate]);

  if (loading) {
    return (
      <Layout className="login-container">
        <Content>
          <Row justify="center" align="middle" style={{ height: "100vh" }}>
            <Col>
              <Spin size="large" tip="正在处理OAuth登录..." />
            </Col>
          </Row>
        </Content>

        {contextHolder}
      </Layout>
    );
  }

  if (!oauthParams) {
    return (
      <Layout className="login-container">
        <Content>
          <Row justify="center" align="middle" style={{ height: "100vh" }}>
            <Col>
              <Card>
                <Typography.Title level={4}>OAuth登录失败</Typography.Title>
                <Typography.Text>缺少必需的参数，请重新登录</Typography.Text>
                <br />
                <Button
                  type="primary"
                  onClick={() => navigate("/login")}
                  style={{ marginTop: 16 }}
                >
                  返回登录页面
                </Button>
              </Card>
            </Col>
          </Row>
        </Content>

        {contextHolder}
      </Layout>
    );
  }

  // 显示绑定账号选项
  if (bindAccountState.showBindOptions) {
    return (
      <Layout className="login-container">
        <Content>
          <Row justify="center" align="middle" style={{ height: "100vh" }}>
            <Col>
              <Card>
                <Typography.Title level={4}>账号绑定</Typography.Title>
                <Typography.Text>
                  检测到您已登录账号{" "}
                  <strong>{bindAccountState.currentUsername}</strong>，
                  请选择如何处理OAuth账号：
                </Typography.Text>
                <br />
                <Space
                  direction="vertical"
                  style={{ width: "100%", marginTop: 16 }}
                >
                  <Button
                    type="primary"
                    icon={<LinkOutlined />}
                    onClick={handleBindConfirm}
                    loading={bindAccountState.isBinding}
                    block
                  >
                    绑定当前账号 {bindAccountState.currentUsername}
                  </Button>
                  <Button
                    icon={<LogoutOutlined />}
                    onClick={handleLogoutAndRelogin}
                    block
                  >
                    注销并重新登录
                  </Button>
                </Space>
              </Card>
            </Col>
          </Row>
        </Content>

        {/* 绑定账号确认对话框 */}
        <Modal
          title="确认绑定账号"
          open={showBindConfirm}
          onOk={handleBindConfirmOk}
          onCancel={handleBindConfirmCancel}
          confirmLoading={bindAccountState.isBinding}
          okText="确认绑定"
          cancelText="取消"
        >
          <Typography.Text>
            您确定要将OAuth账号绑定到当前账号{" "}
            <strong>{bindAccountState.currentUsername}</strong> 吗？
          </Typography.Text>
          <br />
          <Typography.Text type="secondary">
            绑定后，您可以使用OAuth方式登录此账号。
          </Typography.Text>
        </Modal>

        {contextHolder}
      </Layout>
    );
  }

  // 显示注册账号选项
  if (registerAccountState.showRegisterOptions) {
    return (
      <Layout className="login-container">
        <Content>
          <Row justify="center" align="middle" style={{ height: "100vh" }}>
            <Col>
              <Card>
                <Typography.Title level={4}>账号注册</Typography.Title>
                <br />
                <Typography.Text>
                  当前第三方账号名称：{registerAccountState.name}
                  <br />
                </Typography.Text>
                <Typography.Text>
                  检测到您当前没有登录任何账号，请选择如何处理OAuth账号：
                </Typography.Text>
                <Space
                  direction="vertical"
                  style={{ width: "100%", marginTop: 16 }}
                >
                  <Button
                    type="primary"
                    icon={<UserAddOutlined />}
                    onClick={handleRegisterAccount}
                    loading={registerAccountState.isRegistering}
                    block
                  >
                    一键注册账号
                  </Button>
                  <Button
                    icon={<LogoutOutlined />}
                    onClick={handleLogout}
                    block
                  >
                    注销
                  </Button>
                </Space>
              </Card>
            </Col>
          </Row>
        </Content>

        {contextHolder}
      </Layout>
    );
  }

  // 默认显示处理中状态
  return (
    <Layout className="login-container">
      <Content>
        <Row justify="center" align="middle" style={{ height: "100vh" }}>
          <Col>
            <Card>
              <Typography.Title level={4}>OAuth登录处理中</Typography.Title>
              <Typography.Text>正在处理您的OAuth登录请求...</Typography.Text>
              <br />
              <Typography.Text type="secondary">
                如长时间无反应，请重新登录
              </Typography.Text>
            </Card>
          </Col>
        </Row>
      </Content>

      {/* 绑定账号确认对话框 */}
      <Modal
        title="确认绑定账号"
        open={showBindConfirm}
        onOk={handleBindConfirmOk}
        onCancel={handleBindConfirmCancel}
        confirmLoading={bindAccountState.isBinding}
        okText="确认绑定"
        cancelText="取消"
      >
        <Typography.Text>
          您确定要将OAuth账号绑定到当前账号{" "}
          <strong>{bindAccountState.currentUsername}</strong> 吗？
        </Typography.Text>
        <br />
        <Typography.Text type="secondary">
          绑定后，您可以使用OAuth方式登录此账号。
        </Typography.Text>
      </Modal>

      {contextHolder}
    </Layout>
  );
}