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
} from 'antd';
import {
  UserOutlined,
  UploadOutlined,
  SaveOutlined,
  EditOutlined,
} from '@ant-design/icons';
import { GetApiClient, UploadPublicFile } from '../ServiceClient';
import { GetUserDetailInfo, SetUserDetailInfo } from '../../InitService';
import { proxyRequestError } from '../../helper/RequestError';
import { FileTypeHelper } from '../../helper/FileTypeHelper';
import useAppStore from '../../stateshare/store';

const { Title } = Typography;

export default function UserSetting() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [userInfo, setUserInfo] = useState<any>(null);
  const [messageApi, contextHolder] = message.useMessage();
  const serverInfo = useAppStore.getState().getServerInfo();

  useEffect(() => {
    fetchUserInfo();
  }, []);

  const fetchUserInfo = async () => {
    try {
      const userDetailInfo = await GetUserDetailInfo();
      if (userDetailInfo) {
        setUserInfo(userDetailInfo);
        form.setFieldsValue({
          userName: userDetailInfo.userName,
          nickName: userDetailInfo.nickName,
        });
      }
    } catch (error) {
      messageApi.error('获取用户信息失败');
    }
  };

  const handleSave = async (values: any) => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 这里需要调用更新用户信息的API
      // await client.api.account.update_user_info.post(values);
      
      // 更新本地用户信息
      const updatedUserInfo = { ...userInfo, ...values };
      SetUserDetailInfo(updatedUserInfo);
      setUserInfo(updatedUserInfo);
      
      messageApi.success('保存成功');
      setEditing(false);
    } catch (error) {
      proxyRequestError(error, messageApi, '保存失败');
    } finally {
      setLoading(false);
    }
  };

  const getAvatarUrl = (avatarPath: string | null | undefined) => {
    if (!avatarPath) return null;
    if (avatarPath.startsWith("http://") || avatarPath.startsWith("https://")) {
      return avatarPath;
    }
    return serverInfo?.publicStoreUrl
      ? `${serverInfo.publicStoreUrl}/${avatarPath}`
      : avatarPath;
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

      // 更新本地用户信息
      const updatedUserInfo = { 
        ...userInfo, 
        avatar: uploadResult.visibility === 'public' ? file.name : null 
      };
      SetUserDetailInfo(updatedUserInfo);
      setUserInfo(updatedUserInfo);
      
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
                  src={getAvatarUrl(userInfo?.avatar)}
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
              <Form
                form={form}
                layout="vertical"
                onFinish={handleSave}
                disabled={!editing}
              >
                <Row gutter={16}>
                  <Col span={12}>
                    <Form.Item
                      name="userName"
                      label="用户名"
                      rules={[{ required: true, message: '请输入用户名' }]}
                    >
                      <Input prefix={<UserOutlined />} placeholder="请输入用户名" />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item
                      name="nickName"
                      label="昵称"
                    >
                      <Input placeholder="请输入昵称" />
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
                      <Input placeholder="请输入邮箱" disabled />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item
                      name="phone"
                      label="手机号"
                    >
                      <Input placeholder="请输入手机号" disabled />
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
                          htmlType="submit"
                          loading={loading}
                        >
                          保存
                        </Button>
                        <Button
                          onClick={() => {
                            setEditing(false);
                            form.setFieldsValue({
                              userName: userInfo?.userName,
                              nickName: userInfo?.nickName,
                            });
                          }}
                        >
                          取消
                        </Button>
                      </>
                    ) : (
                      <Button
                        type="primary"
                        icon={<EditOutlined />}
                        onClick={() => setEditing(true)}
                      >
                        编辑信息
                      </Button>
                    )}
                  </Space>
                </Form.Item>
              </Form>
            </Col>
          </Row>
        </Card>
      </div>
    </>
  );
}