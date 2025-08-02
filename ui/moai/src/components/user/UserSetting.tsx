import React, { useEffect, useState } from 'react';
import {
  Card,
  Form,
  Input,
  Button,
  Avatar,
  Upload,
  message,
  Space,
  Typography,
  Row,
  Col,
  Divider,
  Spin,
  Descriptions,
  Statistic,
  Tag,
  Tooltip,
  Alert,
  Image,
} from 'antd';
import {
  UserOutlined,
  UploadOutlined,
  SaveOutlined,
  EditOutlined,
  LockOutlined,
  MailOutlined,
  PhoneOutlined,
  CrownOutlined,
  SafetyOutlined,
  InfoCircleOutlined,
  LinkOutlined,
} from '@ant-design/icons';
import { GetApiClient, UploadPublicFile } from '../ServiceClient';
import { GetUserDetailInfo, GetServiceInfo } from '../../InitService';
import { proxyRequestError } from '../../helper/RequestError';
import { FileTypeHelper } from '../../helper/FileTypeHelper';
import { RsaHelper } from '../../helper/RsaHalper';
import useAppStore from '../../stateshare/store';
import { useNavigate } from 'react-router';

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;

export default function UserSetting() {
  const navigate = useNavigate();
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [passwordEditing, setPasswordEditing] = useState(false);
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [userInfoLoading, setUserInfoLoading] = useState(false);
  const [passwordStrength, setPasswordStrength] = useState(0);
  const [messageApi, contextHolder] = message.useMessage();
  const serverInfo = useAppStore.getState().getServerInfo();
  const userInfo = useAppStore((state) => state.userDetailInfo);

  useEffect(() => {
    fetchUserInfo();
  }, []);

  const fetchUserInfo = async () => {
    try {
      const userDetailInfo = await GetUserDetailInfo();
      if (userDetailInfo) {
        useAppStore.getState().setUserDetailInfo(userDetailInfo);
        form.setFieldsValue({
          userName: userDetailInfo.userName,
          nickName: userDetailInfo.nickName,
          email: userDetailInfo.email,
          phone: userDetailInfo.phone,
        });
      }
    } catch (error) {
      messageApi.error('获取用户信息失败');
    }
  };

  const handleSave = async (values: any) => {
    // 检查用户名或邮箱是否发生变化
    const userNameChanged = values.userName !== userInfo?.userName;
    const emailChanged = values.email !== userInfo?.email;
    
    if (userNameChanged || emailChanged) {
      // 显示提示信息
      messageApi.info('正在保存用户名或邮箱的修改...');
    }
    
    // 直接保存
    await performSave(values);
  };

  const performSave = async (values: any) => {
    setUserInfoLoading(true);
    try {
      const client = GetApiClient();
      await client.api.user.update_user.post({
        userName: values.userName,
        nickName: values.nickName,
        email: values.email,
        phone: values.phone,
        userId: userInfo?.userId || 0,
      });
      
      // 更新后刷新用户信息
      await fetchUserInfo();
      
      messageApi.success('保存成功');
      setEditing(false);
    } catch (error) {
      proxyRequestError(error, messageApi, '保存失败');
    } finally {
      setUserInfoLoading(false);
    }
  };

  // 计算密码强度
  const calculatePasswordStrength = (password: string): number => {
    if (!password) return 0;
    
    let score = 0;
    
    // 长度检查
    if (password.length >= 6) score += 20;
    if (password.length >= 8) score += 10;
    if (password.length >= 12) score += 10;
    
    // 字符类型检查
    if (/[a-z]/.test(password)) score += 15;
    if (/[A-Z]/.test(password)) score += 15;
    if (/[0-9]/.test(password)) score += 15;
    if (/[^a-zA-Z0-9]/.test(password)) score += 15;
    
    // 复杂度检查
    if (password.length >= 8 && /[a-z]/.test(password) && /[A-Z]/.test(password) && /[0-9]/.test(password)) {
      score += 10;
    }
    
    return Math.min(score, 100);
  };

  const handlePasswordChange = async (values: any) => {
    setPasswordLoading(true);
    try {
      const serviceInfo = await GetServiceInfo();
      const encryptedPassword = RsaHelper.encrypt(
        serviceInfo.rsaPublic,
        values.newPassword
      );

      const client = GetApiClient();
      await client.api.user.update_password.post({
        password: encryptedPassword,
      });

      messageApi.success('密码修改成功');
      passwordForm.resetFields();
      setPasswordEditing(false);
      setPasswordStrength(0);
    } catch (error) {
      proxyRequestError(error, messageApi, '密码修改失败');
    } finally {
      setPasswordLoading(false);
    }
  };

  // 检查文件是否为图片
  const isImageFile = (file: File): boolean => {
    const fileType = FileTypeHelper.getFileType(file);
    return fileType.startsWith('image/');
  };

  // 处理头像上传
  const handleAvatarUpload = async (file: File) => {
    // 检查文件类型
    if (!isImageFile(file)) {
      messageApi.error('只能上传图片文件！');
      return false;
    }

    // 检查文件大小（限制为5MB）
    const maxSize = 5 * 1024 * 1024; // 5MB
    if (file.size > maxSize) {
      messageApi.error('图片文件大小不能超过5MB！');
      return false;
    }

    setUploading(true);
    try {
      const client = GetApiClient();
      
      // 第一步：上传文件到服务器
      const uploadResult = await UploadPublicFile(client, file);
      
      if (!uploadResult || !uploadResult.fileId) {
        throw new Error('文件上传失败');
      }

      // 第二步：保存用户头像
      await client.api.user.upload_avatar.post({
        fileId: uploadResult.fileId,
        userId: userInfo?.userId || 0,
      });

      // 上传成功后刷新用户信息
      await fetchUserInfo();
      
      messageApi.success('头像上传成功');
    } catch (error) {
      proxyRequestError(error, messageApi, '头像上传失败');
    } finally {
      setUploading(false);
    }

    return false; // 阻止默认的上传行为
  };

  return (
    <>
      {contextHolder}
      <div style={{ padding: '24px' }}>
        <Row gutter={[24, 24]}>
          {/* 用户信息卡片 */}
          <Col xs={24} lg={16}>
            <Card
              title={
                <Space>
                  <UserOutlined />
                  <span>个人信息</span>
                </Space>
              }
              extra={
                !editing && (
                  <Button
                    type="primary"
                    icon={<EditOutlined />}
                    onClick={() => setEditing(true)}
                  >
                    编辑信息
                  </Button>
                )
              }
              actions={
                editing
                  ? [
                      <Button
                        key="save"
                        type="primary"
                        icon={<SaveOutlined />}
                        loading={userInfoLoading}
                        onClick={async () => {
                          try {
                            const values = await form.validateFields();
                            await handleSave(values);
                          } catch (error) {
                            console.error('表单验证失败:', error);
                          }
                        }}
                      >
                        保存
                      </Button>,
                      <Button
                        key="cancel"
                        onClick={() => {
                          setEditing(false);
                          form.setFieldsValue({
                            userName: userInfo?.userName,
                            nickName: userInfo?.nickName,
                            email: userInfo?.email,
                            phone: userInfo?.phone,
                          });
                        }}
                        disabled={userInfoLoading}
                      >
                        取消
                      </Button>,
                    ]
                  : []
              }
            >
              <Spin spinning={userInfoLoading} tip="保存中...">
                <Form
                  form={form}
                  layout="vertical"
                  onFinish={handleSave}
                  size="large"
                >
                  {/* 提示信息区域 */}
                  {editing && (
                    <Alert
                      message="编辑提示"
                      description="修改用户名或邮箱后，将会影响登录，请确保信息准确无误。"
                      type="info"
                      showIcon
                      style={{ marginBottom: 16 }}
                    />
                  )}
                  <Row gutter={[16, 16]}>
                    <Col xs={24} sm={12}>
                      <Form.Item
                        name="userName"
                        label={
                          <Space>
                            <UserOutlined />
                            用户名
                          </Space>
                        }
                        rules={[{ required: true, message: '请输入用户名' }]}
                      >
                        <Input 
                          placeholder="请输入用户名" 
                          disabled={!editing}
                          prefix={<UserOutlined />}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={24} sm={12}>
                      <Form.Item
                        name="nickName"
                        label={
                          <Space>
                            <CrownOutlined />
                            昵称
                          </Space>
                        }
                      >
                        <Input 
                          placeholder="请输入昵称" 
                          disabled={!editing}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={24} sm={12}>
                      <Form.Item
                        name="email"
                        label={
                          <Space>
                            <MailOutlined />
                            邮箱
                          </Space>
                        }
                        rules={[
                          { type: 'email', message: '请输入正确的邮箱格式' }
                        ]}
                      >
                        <Input 
                          placeholder="请输入邮箱" 
                          disabled={!editing}
                          prefix={<MailOutlined />}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={24} sm={12}>
                      <Form.Item
                        name="phone"
                        label={
                          <Space>
                            <PhoneOutlined />
                            手机号
                          </Space>
                        }
                      >
                        <Input 
                          placeholder="请输入手机号" 
                          disabled={!editing}
                          prefix={<PhoneOutlined />}
                        />
                      </Form.Item>
                    </Col>
                  </Row>
                </Form>
              </Spin>
            </Card>
          </Col>

          {/* 用户头像卡片 */}
          <Col xs={24} lg={8}>
            <Card
              title={
                <Space>
                  <UserOutlined />
                  <span>头像设置</span>
                </Space>
              }
              bodyStyle={{ textAlign: 'center' }}
            >
              <Space direction="vertical" size="large" style={{ width: '100%' }}>
                <Image
                  width={120}
                  height={120}
                  src={userInfo?.avatarPath || undefined}
                  fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAMIAAADDCAYAAADQvc6UAAABRWlDQ1BJQ0MgUHJvZmlsZQAAKJFjYGASSSwoyGFhYGDIzSspCnJ3UoiIjFJgf8LAwSDCIMogwMCcmFxc4BgQ4ANUwgCjUcG3awyMIPqyLsis7PPOq3QdDFcvjV3jOD1boQVTPQrgSkktTgbSf4A4LbmgqISBgTEFyFYuLykAsTuAbJEioKOA7DkgdjqEvQHEToKwj4DVhAQ5A9k3gGyB5IxEoBmML4BsnSQk8XQkNtReEOBxcfXxUQg1Mjc0dyHgXNJBSWpFCYh2zi+oLMpMzyhRcASGUqqCZ19ynY6NxRAAGoydJ1zPw2Zgp2wgjTp0i2czRBUfQU1PFS0XJiMV6irxBiMvLlL4oZzJ4skZR4I/3e6+W3AI5BaXFs3Q/4ws5gq/IMRgFbAyQZGHYcRZ0YGC0x+jCDGfEO/AfH+GAplC7xIMwPEBPYqCD4/9eDgLdAHAb7JhDzA=="
                  style={{ 
                    border: '4px solid #f0f0f0',
                    borderRadius: '50%',
                    cursor: 'pointer',
                    objectFit: 'cover'
                  }}
                  preview={{
                    mask: '点击查看大图',
                    maskClassName: 'custom-mask'
                  }}
                />
                
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="用户名">
                    <Text strong>{userInfo?.userName}</Text>
                  </Descriptions.Item>
                  <Descriptions.Item label="昵称">
                    <Text>{userInfo?.nickName || '未设置'}</Text>
                  </Descriptions.Item>
                  <Descriptions.Item label="管理员">
                    <Tag color={userInfo?.isAdmin ? "red" : "default"} icon={userInfo?.isAdmin ? <CrownOutlined /> : <UserOutlined />}>
                      {userInfo?.isAdmin ? "管理员" : "普通用户"}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="状态">
                    <Tag color="green" icon={<SafetyOutlined />}>
                      正常
                    </Tag>
                  </Descriptions.Item>
                </Descriptions>

                <Upload
                  name="avatar"
                  showUploadList={false}
                  beforeUpload={handleAvatarUpload}
                  accept="image/*"
                  disabled={uploading}
                >
                  <Button 
                    icon={<UploadOutlined />} 
                    type="dashed"
                    loading={uploading}
                    block
                  >
                    {uploading ? '上传中...' : '更换头像'}
                  </Button>
                </Upload>
                
                <Alert
                  message="头像上传提示"
                  description="支持 JPG、PNG、GIF 等格式，文件大小不超过 5MB"
                  type="info"
                  showIcon
                  icon={<InfoCircleOutlined />}
                />
              </Space>
            </Card>
          </Col>

          {/* 修改密码卡片 */}
          <Col xs={24}>
            <Card
              title={
                <Space>
                  <LockOutlined />
                  <span>安全设置</span>
                </Space>
              }
            >
              <Row gutter={[24, 24]}>
                <Col xs={24} md={12}>
                  <Card
                    type="inner"
                    title={
                      <Space>
                        <LockOutlined />
                        修改密码
                      </Space>
                    }
                    extra={
                      !passwordEditing ? (
                        <Button
                          type="primary"
                          size="small"
                          icon={<EditOutlined />}
                          onClick={() => setPasswordEditing(true)}
                        >
                          更新密码
                        </Button>
                      ) : (
                        <Space>
                          <Button
                            type="primary"
                            size="small"
                            icon={<SaveOutlined />}
                            loading={passwordLoading}
                            onClick={() => passwordForm.submit()}
                          >
                            保存
                          </Button>
                          <Button
                            size="small"
                            onClick={() => {
                              setPasswordEditing(false);
                              passwordForm.resetFields();
                              setPasswordStrength(0);
                            }}
                            disabled={passwordLoading}
                          >
                            取消
                          </Button>
                        </Space>
                      )
                    }
                  >
                    <Form
                      form={passwordForm}
                      layout="vertical"
                      onFinish={handlePasswordChange}
                    >
                      <Form.Item
                        name="newPassword"
                        label="新密码"
                        rules={[
                          { required: true, message: '请输入新密码' },
                          { min: 6, message: '密码至少6个字符' }
                        ]}
                      >
                        <Input.Password
                          prefix={<LockOutlined />}
                          placeholder="请输入新密码"
                          disabled={!passwordEditing}
                          onChange={(e) => {
                            const strength = calculatePasswordStrength(e.target.value);
                            setPasswordStrength(strength);
                          }}
                        />
                      </Form.Item>

                      <Form.Item
                        name="confirmPassword"
                        label="确认新密码"
                        dependencies={['newPassword']}
                        rules={[
                          { required: true, message: '请确认新密码' },
                          ({ getFieldValue }) => ({
                            validator(_, value) {
                              if (!value || getFieldValue('newPassword') === value) {
                                return Promise.resolve();
                              }
                              return Promise.reject(new Error('两次输入的密码不一致'));
                            },
                          }),
                        ]}
                      >
                        <Input.Password
                          prefix={<LockOutlined />}
                          placeholder="请再次输入新密码"
                          disabled={!passwordEditing}
                        />
                      </Form.Item>

                      {passwordEditing && (
                        <Form.Item>
                          <Button
                            type="primary"
                            icon={<LockOutlined />}
                            htmlType="submit"
                            loading={passwordLoading}
                            block
                          >
                            修改密码
                          </Button>
                        </Form.Item>
                      )}
                    </Form>
                  </Card>
                </Col>

                <Col xs={24} md={12}>
                <Card
                    type="inner"
                    title={
                      <Space>
                        <SafetyOutlined />
                        安全提示
                      </Space>
                    }
                  >
                    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                      <Alert
                        message="密码安全建议"
                        description="建议使用包含字母、数字和特殊字符的强密码"
                        type="warning"
                        showIcon
                      />
                      
                      <Statistic
                        title="密码安全等级"
                        value={passwordStrength}
                        suffix="%"
                        valueStyle={{ 
                          color: passwordStrength === 0 ? '#999' : 
                                 passwordStrength < 30 ? '#ff4d4f' :
                                 passwordStrength < 60 ? '#faad14' :
                                 passwordStrength < 80 ? '#52c41a' : '#3f8600'
                        }}
                        prefix={passwordStrength === 0 ? '*' : undefined}
                      />
                      
                      {passwordStrength > 0 && (
                        <Alert
                          message={
                            passwordStrength < 30 ? "密码强度：弱" :
                            passwordStrength < 60 ? "密码强度：中等" :
                            passwordStrength < 80 ? "密码强度：强" : "密码强度：很强"
                          }
                          type={
                            passwordStrength < 30 ? "error" :
                            passwordStrength < 60 ? "warning" :
                            passwordStrength < 80 ? "success" : "success"
                          }
                          showIcon
                        />
                      )}
                      
                      <Paragraph type="secondary" style={{ margin: 0 }}>
                        <ul>
                          <li>定期更换密码</li>
                          <li>不要在多个网站使用相同密码</li>
                          <li>避免使用个人信息作为密码</li>
                          <li>启用双重认证（如果支持）</li>
                        </ul>
                      </Paragraph>
                    </Space>
                  </Card>
                </Col>
              </Row>
            </Card>
          </Col>
        </Row>
      </div>
    </>
  );
}