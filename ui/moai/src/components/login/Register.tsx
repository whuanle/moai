import { Col, Row, Card, Form, Input, Button, message } from "antd";
import { useNavigate } from "react-router";
import { RsaHelper } from "../../helper/RsaHalper";
import "./Register.css";
import { GetAllowApiClient } from "../ServiceClient";
import { useEffect } from "react";
import { CheckToken, GetServiceInfo, RefreshServerInfo } from "../../InitPage";
import { proxyFormRequestError } from "../../helper/RequestError";
export default function Register() {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();

  useEffect(() => {
    let client =  GetAllowApiClient();
    const fetchData = (async () => {
      await RefreshServerInfo(client);
      var isVerify = await CheckToken();
      if (isVerify) {
        messageApi.success("您已经登录，正在重定向到首页");
        setTimeout(() => {
          navigate("/app");
        }, 1000);
      }
    });
    fetchData();

    return () => {};
  }, []);

  const onFinish = async (values: any) => {
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
      }
    }
  };
  return (
    <>
      {contextHolder}

      <Row
        justify="center"
        align="middle"
        style={{
          minHeight: "90vh",
          minWidth: "80vh",
          width: "80%",
          margin: "0 auto",
          background: "#f0f2f5",
        }}
      >
        <Col span={20}>
          <Card
            title={
              <div
                style={{
                  textAlign: "center",
                  fontSize: "24px",
                  fontWeight: "bold",
                }}
              >
                注册账号
              </div>
            }
            style={{
              boxShadow: "0 4px 8px rgba(0,0,0,0.1)",
              borderRadius: "8px",
            }}
          >
            <Form
              form={form}
              name="register"
              onFinish={onFinish}
              layout="vertical"
              size="large"
            >
              <Form.Item
                name="userName"
                label="用户名"
                rules={[{ required: true, message: "请输入用户名" }]}
              >
                <Input placeholder="请输入您的用户名" />
              </Form.Item>

              <Form.Item
                name="password"
                label="密码"
                rules={[
                  { required: true, message: "请输入密码" },
                  { min: 6, message: "密码长度至少6位" },
                  {
                    pattern: /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$/,
                    message: "密码必须包含字母、数字和特殊符号",
                  },
                ]}
              >
                <Input.Password placeholder="请输入密码" />
              </Form.Item>

              <Form.Item
                name="confirmPassword"
                label="确认密码"
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
                <Input.Password placeholder="请再次输入密码" />
              </Form.Item>

              <Form.Item
                name="email"
                label="邮箱"
                rules={[
                  { required: true, message: "请输入邮箱" },
                  { type: "email", message: "请输入有效的邮箱地址" },
                ]}
              >
                <Input placeholder="请输入您的邮箱地址" />
              </Form.Item>

              <Form.Item
                name="nickName"
                label="昵称"
                rules={[{ required: true, message: "请输入昵称" }]}
              >
                <Input placeholder="请输入您的昵称" />
              </Form.Item>

              <Form.Item
                name="phone"
                label="手机号"
                rules={[
                  { required: true, message: "请输入手机号" },
                  { pattern: /^1[3-9]\d{9}$/, message: "请输入有效的手机号" },
                ]}
              >
                <Input placeholder="请输入您的手机号" />
              </Form.Item>

              <Form.Item style={{ marginTop: "24px" }}>
                <Button
                  type="primary"
                  htmlType="submit"
                  block
                  size="large"
                  style={{ height: "40px", borderRadius: "4px" }}
                >
                  立即注册
                </Button>
              </Form.Item>

              <Form.Item style={{ marginBottom: 0, textAlign: "center" }}>
                <Button
                  type="link"
                  onClick={() => navigate("/login")}
                  style={{ fontSize: "14px" }}
                >
                  已有账号？点击登录
                </Button>
              </Form.Item>
            </Form>
          </Card>
        </Col>
      </Row>
    </>
  );
}
