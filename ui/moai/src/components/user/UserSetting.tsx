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
  Modal,
} from 'antd';
import {
  UserOutlined,
  UploadOutlined,
  SaveOutlined,
  EditOutlined,
  LockOutlined,
} from '@ant-design/icons';
import { GetApiClient, UploadPublicFile } from '../ServiceClient';
import { GetUserDetailInfo, GetServiceInfo } from '../../InitService';
import { proxyRequestError } from '../../helper/RequestError';
import { FileTypeHelper } from '../../helper/FileTypeHelper';
import { RsaHelper } from '../../helper/RsaHalper';
import useAppStore from '../../stateshare/store';

const { Title } = Typography;

export default function UserSetting() {
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [userInfoLoading, setUserInfoLoading] = useState(false);
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
      // 显示确认对话框
      Modal.confirm({
        title: '确认修改',
        content: '确认修改用户名或邮箱吗？',
        okText: '确认',
        cancelText: '取消',
        onOk: async () => {
          await performSave(values);
        },
      });
    } else {
      // 直接保存
      await performSave(values);
    }
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
        <Card>
          <div style={{ marginBottom: '24px' }}>
            <Title level={3} style={{ margin: 0 }}>
              <UserOutlined style={{ marginRight: '8px' }} />
              个人信息
            </Title>
          </div>

          <Row gutter={24}>
            <Col span={8}>
              <div style={{ textAlign: 'center', padding: '20px' }}>
                <Avatar
                  size={120}
                  src={userInfo?.avatarPath}
                  icon={<UserOutlined />}
                  style={{ marginBottom: '16px' }}
                />
                <div>
                  <Typography.Title level={4} style={{ margin: '8px 0' }}>
                    {userInfo?.nickName || userInfo?.userName || '用户'}
                  </Typography.Title>
                  <Typography.Text type="secondary">
                    {userInfo?.userName}
                  </Typography.Text>
                </div>
                <div style={{ marginTop: '16px' }}>
                  <Upload
                    name="avatar"
                    showUploadList={false}
                    beforeUpload={handleAvatarUpload}
                    accept="image/*"
                    disabled={uploading}
                  >
                    <Button 
                      icon={<UploadOutlined />} 
                      size="small"
                      loading={uploading}
                    >
                      {uploading ? '上传中...' : '更换头像'}
                    </Button>
                  </Upload>
                </div>
              </div>
            </Col>

            <Col span={16}>
              <Spin spinning={userInfoLoading} tip="保存中...">
                <Form
                  form={form}
                  layout="vertical"
                  onFinish={handleSave}
                >
                <Row gutter={16}>
                  <Col span={12}>
                    <Form.Item
                      name="userName"
                      label="用户名"
                      rules={[{ required: true, message: '请输入用户名' }]}
                    >
                      <Input prefix={<UserOutlined />} placeholder="请输入用户名" disabled={!editing} />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item
                      name="nickName"
                      label="昵称"
                    >
                      <Input placeholder="请输入昵称" disabled={!editing} />
                    </Form.Item>
                  </Col>
                </Row>

                <Row gutter={16}>
                  <Col span={12}>
                    <Form.Item
                      name="email"
                      label="邮箱"
                      rules={[
                        { type: 'email', message: '请输入正确的邮箱格式' }
                      ]}
                    >
                      <Input placeholder="请输入邮箱" disabled={!editing} />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item
                      name="phone"
                      label="手机号"
                    >
                      <Input placeholder="请输入手机号" disabled={!editing} />
                    </Form.Item>
                  </Col>
                </Row>

                <Divider />

                <Form.Item>
                  <Space>
                    {editing ? (
                      <>
                        <Button
                          type="primary"
                          icon={<SaveOutlined />}
                          htmlType="button"
                            loading={userInfoLoading}
                            
                        >
                          保存
                        </Button>
                        <Button
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
                        </Button>
                      </>
                    ) : null}
                  </Space>
                </Form.Item>
              </Form>
              
              {!editing && (
                <div style={{ marginTop: '16px' }}>
                  <Button
                    type="primary"
                    icon={<EditOutlined />}
                    onClick={() => setEditing(true)}
                  >
                    编辑信息
                  </Button>
                </div>
              )}
              </Spin>

              <Divider />

              {/* 修改密码区域 */}
              <div style={{ marginTop: '24px' }}>
                <Typography.Title level={4} style={{ marginBottom: '16px' }}>
                  <LockOutlined style={{ marginRight: '8px' }} />
                  修改密码
                </Typography.Title>
                
                <Form
                  form={passwordForm}
                  layout="vertical"
                  onFinish={handlePasswordChange}
                  style={{ maxWidth: '400px' }}
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
                    />
                  </Form.Item>

                  <Form.Item>
                    <Button
                      type="primary"
                      icon={<LockOutlined />}
                      htmlType="submit"
                      loading={passwordLoading}
                    >
                      修改密码
                    </Button>
                  </Form.Item>
                </Form>
              </div>
            </Col>
          </Row>
        </Card>
      </div>
    </>
  );
}