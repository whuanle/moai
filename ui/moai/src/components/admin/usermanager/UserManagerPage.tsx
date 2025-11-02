import React, { useEffect, useState } from 'react';
import {
  Table,
  Button,
  Input,
  Space,
  Popconfirm,
  message,
  Card,
  Row,
  Col,
  Typography,
  Avatar,
  Tag,
  Checkbox,
  Pagination,
  Modal,
  Spin,
} from 'antd';
import {
  UserOutlined,
  KeyOutlined,
  CrownOutlined,
  DeleteOutlined,
  LockOutlined,
  CopyOutlined,
} from '@ant-design/icons';
import { GetApiClient } from '../../ServiceClient';
import {
  QueryUserListCommand,
  QueryUserListCommandResponse,
  UserItem,
  DeleteUserCommand,
  ResetUserPasswordCommand,
  SetUserAdminCommand,
  DisableUserCommand,
} from '../../../apiClient/models';
import { proxyRequestError } from '../../../helper/RequestError';

const { Title } = Typography;

export default function UserManagerPage() {
  const [data, setData] = useState<UserItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: 10,
    total: 0,
  });
  const [searchText, setSearchText] = useState('');
  const [onlyAdmin, setOnlyAdmin] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [passwordModalVisible, setPasswordModalVisible] = useState(false);
  const [newPassword, setNewPassword] = useState('');
  
  // 全局操作加载状态
  const [operationLoading, setOperationLoading] = useState(false);

  const fetchUserList = async (page = 1, pageSize = 10) => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryUserListCommand = {
        pageNo: page, // API 使用 1-based 索引
        pageSize: pageSize,
        search: searchText || undefined,
        isAdmin: onlyAdmin || undefined,
      };

      const response: QueryUserListCommandResponse | undefined = await client.api.admin.user.user_list.post(requestBody);
      
      if (response) {
        setData(response.items || []);
        setPagination(prev => ({
          ...prev,
          current: page,
          pageSize: pageSize,
          total: response.total || 0,
        }));
      }
    } catch (error) {
      proxyRequestError(error, messageApi, '获取用户列表失败');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPagination(prev => ({ ...prev, current: 1 }));
    fetchUserList(1, pagination.pageSize);
  };

  const handleResetPassword = async (userId: number) => {
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: ResetUserPasswordCommand = {
        userId: userId,
      };
      const response = await client.api.admin.user.reset_password.put(requestBody);
      if (response) {
        setNewPassword(response.value || '');
        setPasswordModalVisible(true);
      }
    } catch (error) {
      proxyRequestError(error, messageApi, '密码重置失败');
    } finally {
      setOperationLoading(false);
    }
  };

  const handleSetAdmin = async (userId: number, isAdmin: boolean) => {
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: SetUserAdminCommand = {
        userIds: [userId],
        isAdmin: isAdmin,
      };
      await client.api.admin.user.set_admin.post(requestBody);
      messageApi.success(isAdmin ? '设置管理员成功' : '取消管理员成功');
      fetchUserList(pagination.current, pagination.pageSize);
    } catch (error) {
      proxyRequestError(error, messageApi, '设置管理员失败');
    } finally {
      setOperationLoading(false);
    }
  };

  const handleDisableUser = async (userId: number, isDisable: boolean) => {
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: DisableUserCommand = {
        userIds: [userId],
        isDisable: isDisable,
      };
      await client.api.admin.user.disable_user.post(requestBody);
      messageApi.success(isDisable ? '禁用用户成功' : '启用用户成功');
      fetchUserList(pagination.current, pagination.pageSize);
    } catch (error) {
      proxyRequestError(error, messageApi, '操作失败');
    } finally {
      setOperationLoading(false);
    }
  };

  const handleDeleteUser = async (userId: number) => {
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: DeleteUserCommand = {
        userIds: [userId],
      };
      await client.api.admin.user.delete_user.post(requestBody);
      messageApi.success('删除用户成功');
      fetchUserList(pagination.current, pagination.pageSize);
    } catch (error) {
      proxyRequestError(error, messageApi, '删除用户失败');
    } finally {
      setOperationLoading(false);
    }
  };

  const handleTableChange = (page: number, pageSize: number) => {
    fetchUserList(page, pageSize);
  };

  const handleCopyPassword = async () => {
    try {
      await navigator.clipboard.writeText(newPassword);
      messageApi.success('密码已复制到剪贴板');
    } catch (error) {
      messageApi.error('复制失败，请手动复制');
    }
  };

  useEffect(() => {
    fetchUserList();
  }, []);

  const columns = [
    {
      title: '头像',
      dataIndex: 'avatarPath',
      key: 'avatarPath',
      width: 80,
      render: (avatarPath: string) => (
        <Avatar 
          src={avatarPath} 
          size={40} 
          icon={<UserOutlined />} 
        />
      ),
    },
    {
      title: '用户名',
      dataIndex: 'userName',
      key: 'userName',
      render: (userName: string) => (
        <Typography.Text strong>{userName}</Typography.Text>
      ),
    },
    {
      title: '昵称',
      dataIndex: 'nickName',
      key: 'nickName',
      render: (nickName: string) => (
        <Typography.Text>{nickName || '-'}</Typography.Text>
      ),
    },
    {
      title: '邮箱',
      dataIndex: 'email',
      key: 'email',
      render: (email: string) => (
        <Typography.Text type="secondary">{email || '-'}</Typography.Text>
      ),
    },
    {
      title: '手机号',
      dataIndex: 'phone',
      key: 'phone',
      render: (phone: string) => (
        <Typography.Text type="secondary">{phone || '-'}</Typography.Text>
      ),
    },
    {
      title: '状态',
      key: 'status',
      render: (record: UserItem) => (
        <Space>
          {record.isAdmin && (
            <Tag color="gold" icon={<CrownOutlined />}>
              管理员
            </Tag>
          )}
          {record.isDisable === true ? (
            <Tag color="red">已禁用</Tag>
          ) : (
            <Tag color="green">正常</Tag>
          )}
        </Space>
      ),
    },
    {
      title: '操作',
      key: 'action',
      width: 280,
      render: (record: UserItem) => (
        <Space size="small">
          <Popconfirm
            title="确定要重置该用户的密码吗？"
            onConfirm={() => handleResetPassword(record.id!)}
            okText="确定"
            cancelText="取消"
          >
            <Button 
              type="link" 
              icon={<KeyOutlined />} 
              size="small"
            >
              重置密码
            </Button>
          </Popconfirm>
          
          <Button
            type="link"
            icon={<CrownOutlined />}
            onClick={() => handleSetAdmin(record.id!, !record.isAdmin)}
            size="small"
          >
            {record.isAdmin ? '取消管理员' : '设为管理员'}
          </Button>
          
          <Button
            type="link"
            icon={<LockOutlined />}
            onClick={() => handleDisableUser(record.id!, !record.isDisable)}
            size="small"
          >
            {record.isDisable ? '启用用户' : '禁用用户'}
          </Button>
          
          <Popconfirm
            title="确定要删除该用户吗？此操作不可恢复！"
            onConfirm={() => handleDeleteUser(record.id!)}
            okText="确定"
            cancelText="取消"
          >
            <Button 
              type="link" 
              danger 
              icon={<DeleteOutlined />} 
              size="small"
            >
              删除用户
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <>
      {contextHolder}
      <Spin spinning={operationLoading} tip="操作进行中，请稍候...">
      <div style={{ padding: '24px' }}>
        <Card>
          <div
            style={{
              marginBottom: '16px',
            }}
          >
            <Title level={3} style={{ margin: 0 }}>
              <UserOutlined style={{ marginRight: '8px' }} />
              用户管理
            </Title>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <Space size="large" align="center">
              <Input
                placeholder="搜索用户名、昵称、邮箱"
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                size="large"
                style={{ width: '300px' }}
              />
              <Checkbox
                checked={onlyAdmin}
                onChange={(e) => setOnlyAdmin(e.target.checked)}
                style={{ fontSize: '16px' }}
              >
                只看管理员
              </Checkbox>
              <Button 
                type="primary" 
                onClick={handleSearch}
                loading={loading}
                size="large"
              >
                查询
              </Button>
            </Space>
          </div>

          <Table
            columns={columns}
            dataSource={data}
            rowKey="id"
            loading={loading}
            pagination={false}
            scroll={{ x: 1200 }}
          />

          <div style={{ marginTop: '16px', textAlign: 'right' }}>
            <Pagination
              current={pagination.current}
              pageSize={pagination.pageSize}
              total={pagination.total}
              showSizeChanger
              showQuickJumper
              showTotal={(total, range) => 
                `第 ${range[0]}-${range[1]} 条，共 ${total} 条`
              }
              onChange={handleTableChange}
              onShowSizeChange={handleTableChange}
            />
          </div>
        </Card>

        <Modal
          title="密码重置成功"
          open={passwordModalVisible}
          onCancel={() => setPasswordModalVisible(false)}
          footer={[
            <Button key="copy" type="primary" icon={<CopyOutlined />} onClick={handleCopyPassword}>
              复制密码
            </Button>,
            <Button key="close" onClick={() => setPasswordModalVisible(false)}>
              关闭
            </Button>,
          ]}
          destroyOnClose
        >
          <div style={{ textAlign: 'center', padding: '20px 0' }}>
            <Typography.Title level={4} style={{ marginBottom: '16px' }}>
              新密码已生成
            </Typography.Title>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: '16px' }}>
              请复制以下密码并安全地提供给用户：
            </Typography.Text>
            <Input.TextArea
              value={newPassword}
              readOnly
              style={{ 
                fontFamily: 'monospace', 
                fontSize: '16px',
                textAlign: 'center',
                backgroundColor: '#f5f5f5',
                border: '1px solid #d9d9d9',
                borderRadius: '6px',
                padding: '12px',
                marginBottom: '16px'
              }}
              rows={3}
            />
            <Typography.Text type="secondary" style={{ fontSize: '12px' }}>
              注意：此密码仅显示一次，请及时复制保存
            </Typography.Text>
          </div>
        </Modal>
      </div>
      </Spin>
    </>
  );
}