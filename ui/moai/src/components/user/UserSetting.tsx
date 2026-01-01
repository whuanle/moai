import React, { useEffect, useState } from 'react';
import {
  Card,
  Form,
  Input,
  Button,
  Avatar,
  message,
  Space,
  Typography,
  Row,
  Col,
  Spin,
  Descriptions,
  Statistic,
  Tag,
  Alert,
  Upload,
} from 'antd';
import type { UploadProps } from 'antd';
import {
  UserOutlined,
  SaveOutlined,
  EditOutlined,
  LockOutlined,
  MailOutlined,
  PhoneOutlined,
  CrownOutlined,
  SafetyOutlined,
  CameraOutlined,
  LoadingOutlined,
} from '@ant-design/icons';
import { GetApiClient } from '../ServiceClient';
import { GetUserDetailInfo, GetServiceInfo } from '../../InitService';
import { proxyRequestError } from '../../helper/RequestError';
import { RsaHelper } from '../../helper/RsaHalper';
import { GetFileMd5 } from '../../helper/Md5Helper';
import useAppStore from '../../stateshare/store';
import './UserSetting.css';

const { Text, Paragraph } = Typography;

export default function UserSetting() {
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();
  const [editing, setEditing] = useState(false);
  const [passwordEditing, setPasswordEditing] = useState(false);
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [userInfoLoading, setUserInfoLoading] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [passwordStrength, setPasswordStrength] = useState(0);
  const [messageApi, contextHolder] = message.useMessage();
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
      console.error('获取用户信息失败:', error);
      proxyRequestError(error, messageApi, '获取用户信息失败');
    }
  };

  // 上传头像
  const handleAvatarUpload = async (file: File) => {
    setAvatarUploading(true);
    try {
      const client = GetApiClient();
      
      // 1. 计算文件 MD5
      const md5 = await GetFileMd5(file);
      
      // 2. 获取预上传地址
      const preUploadResponse = await client.api.storage.image.pre_upload.post({
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type,
        mD5: md5,
      });

      if (!preUploadResponse) {
        throw new Error('获取上传地址失败');
      }

      // 3. 如果文件已存在，直接使用
      if (preUploadResponse.isExist) {
        // 文件已存在，直接更新用户头像
        await updateUserAvatar(preUploadResponse.objectKey!);
        messageApi.success('头像更新成功');
        return;
      }

      // 4. 上传文件到预签名地址
      const uploadResponse = await fetch(preUploadResponse.uploadUrl!, {
        method: 'PUT',
        body: file,
        headers: {
          'Content-Type': file.type,
        },
      });

      if (!uploadResponse.ok) {
        throw new Error('上传文件失败');
      }

      // 5. 完成上传回调
      await client.api.storage.complate_url.post({
        fileId: preUploadResponse.fileId,
        isSuccess: true,
      });

      // 6. 更新用户头像
      await updateUserAvatar(preUploadResponse.objectKey!);
      messageApi.success('头像上传成功');
    } catch (error) {
      console.error('上传头像失败:', error);
      proxyRequestError(error, messageApi, '上传头像失败');
    } finally {
      setAvatarUploading(false);
    }
  };

  // 更新用户头像
  const updateUserAvatar = async (avatarPath: string) => {
    const client = GetApiClient();
    await client.api.user.account.update_user.post({
      userName: userInfo?.userName,
      nickName: userInfo?.nickName,
      email: userInfo?.email,
      phone: userInfo?.phone,
      userId: userInfo?.userId || 0,
      avatarPath: avatarPath,
    });
    
    // 刷新用户信息
    await fetchUserInfo();
  };

  // 上传前校验
  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    const isImage = file.type.startsWith('image/');
    if (!isImage) {
      messageApi.error('只能上传图片文件');
      return false;
    }
    
    const isLt5M = file.size / 1024 / 1024 < 5;
    if (!isLt5M) {
      messageApi.error('图片大小不能超过 5MB');
      return false;
    }
    
    handleAvatarUpload(file);
    return false; // 阻止默认上传行为
  };

  const handleSave = async (values: any) => {
    const userNameChanged = values.userName !== userInfo?.userName;
    const emailChanged = values.email !== userInfo?.email;
    
    if (userNameChanged || emailChanged) {
      messageApi.info('正在保存用户名或邮箱的修改...');
    }
    
    await performSave(values);
  };

  const performSave = async (values: any) => {
    setUserInfoLoading(true);
    try {
      const client = GetApiClient();
      await client.api.user.account.update_user.post({
        userName: values.userName,
        nickName: values.nickName,
        email: values.email,
        phone: values.phone,
        userId: userInfo?.userId || 0,
      });
      
      await fetchUserInfo();
      
      messageApi.success('保存成功');
      setEditing(false);
    } catch (error) {
      console.error('保存失败:', error);
      proxyRequestError(error, messageApi, '保存失败');
    } finally {
      setUserInfoLoading(false);
    }
  };

  // 计算密码强度
  const calculatePasswordStrength = (password: string): number => {
    if (!password) return 0;
    
    let score = 0;
    
    if (password.length >= 6) score += 20;
    if (password.length >= 8) score += 10;
    if (password.length >= 12) score += 10;
    
    if (/[a-z]/.test(password)) score += 15;
    if (/[A-Z]/.test(password)) score += 15;
    if (/[0-9]/.test(password)) score += 15;
    if (/[^a-zA-Z0-9]/.test(password)) score += 15;
    
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
      await client.api.user.account.update_password.post({
        password: encryptedPassword,
      });

      messageApi.success('密码修改成功');
      passwordForm.resetFields();
      setPasswordEditing(false);
      setPasswordStrength(0);
    } catch (error) {
      console.error('密码修改失败:', error);
      proxyRequestError(error, messageApi, '密码修改失败');
    } finally {
      setPasswordLoading(false);
    }
  };

  // 获取用户显示名称
  const getUserDisplayName = () => {
    return userInfo?.nickName || userInfo?.userName || '用户';
  };

  return (
    <>
      {contextHolder}
      <div className="user-setting-container">
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
                  {editing && (
                    <Alert
                      message="编辑提示"
                      description="修改用户名或邮箱后，将会影响登录，请确保信息准确无误。"
                      type="info"
                      showIcon
                      className="edit-alert"
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
                  <span>用户信息</span>
                </Space>
              }
            >
              <Space direction="vertical" size="large" className="user-info-card-content">
                <Upload
                  name="avatar"
                  showUploadList={false}
                  beforeUpload={beforeUpload}
                  accept="image/*"
                  disabled={avatarUploading}
                >
                  <div className="avatar-upload-wrapper">
                    <Avatar
                      size={120}
                      src={userInfo?.avatar}
                      icon={avatarUploading ? <LoadingOutlined /> : <UserOutlined />}
                      className="user-avatar"
                    >
                      {!userInfo?.avatar && getUserDisplayName().charAt(0).toUpperCase()}
                    </Avatar>
                    <div className="avatar-upload-overlay">
                      {avatarUploading ? <LoadingOutlined /> : <CameraOutlined />}
                      <span>{avatarUploading ? '上传中...' : '更换头像'}</span>
                    </div>
                  </div>
                </Upload>
                
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
                    <Space direction="vertical" size="middle" className="security-tips">
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
                      
                      <Paragraph type="secondary" className="security-list">
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
