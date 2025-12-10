import { useState, useEffect } from "react";
import {
  Card,
  Table,
  Button,
  message,
  Modal,
  Form,
  Input,
  Space,
  Avatar,
  Tag,
  Popconfirm,
  Typography,
} from "antd";
import {
  UserAddOutlined,
  UserDeleteOutlined,
  UserOutlined,
  MailOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams } from "react-router";
import type { QueryWikiUsersCommandResponseItem } from "../../apiClient/models";
import { proxyRequestError } from "../../helper/RequestError";

const { Text } = Typography;

interface WikiUserProps {}

export default function WikiUser({}: WikiUserProps) {
  const [users, setUsers] = useState<QueryWikiUsersCommandResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [inviteLoading, setInviteLoading] = useState(false);
  const [removeLoading, setRemoveLoading] = useState(false);
  const [inviteModalVisible, setInviteModalVisible] = useState(false);
  const [selectedUserIds, setSelectedUserIds] = useState<number[]>([]);
  const [messageApi, contextHolder] = message.useMessage();
  const [inviteForm] = Form.useForm();
  
  const { id } = useParams();
  const apiClient = GetApiClient();

  useEffect(() => {
    if (id) {
      fetchWikiUsers();
    }
  }, [id]);

  const fetchWikiUsers = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response = await apiClient.api.wiki.query_wiki_users.post({
        wikiId: parseInt(id),
      });

      if (response && response.users) {
        setUsers(response.users);
      }
    } catch (error) {
      console.error("Failed to fetch wiki users:", error);
      messageApi.error("获取成员列表失败");
    } finally {
      setLoading(false);
    }
  };

  const handleInviteUser = async (values: { userName: string }) => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setInviteLoading(true);
      await apiClient.api.wiki.manager.invite_wiki_user.post({
        wikiId: parseInt(id),
        userNames: [values.userName],
      });

      messageApi.success("邀请用户成功");
      setInviteModalVisible(false);
      inviteForm.resetFields();
      fetchWikiUsers();
    } catch (error) {
      console.error("Failed to invite user:", error);
      proxyRequestError(error, messageApi, "邀请用户失败");
    } finally {
      setInviteLoading(false);
    }
  };

  const handleRemoveUsers = async () => {
    if (!id || selectedUserIds.length === 0) {
      messageApi.error("请选择要移除的成员");
      return;
    }

    try {
      setRemoveLoading(true);
      await apiClient.api.wiki.manager.invite_wiki_user.post({
        wikiId: parseInt(id),
        isInvite: false,
        userIds: selectedUserIds,
      });

      messageApi.success("移除成员成功");
      setSelectedUserIds([]);
      fetchWikiUsers();
    } catch (error) {
      console.error("Failed to remove users:", error);
      messageApi.error("移除成员失败");
    } finally {
      setRemoveLoading(false);
    }
  };

  const formatDateTime = (dateTimeString?: string | null): string => {
    if (!dateTimeString) return "";
    
    try {
      const date = new Date(dateTimeString);
      if (isNaN(date.getTime())) return dateTimeString;
      
      return date.toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
      });
    } catch (error) {
      return dateTimeString;
    }
  };

  const columns = [
    {
      title: "用户信息",
      key: "userInfo",
      render: (record: QueryWikiUsersCommandResponseItem) => (
        <Space>
          <Avatar 
            icon={<UserOutlined />}
            size="default"
          />
          <Space direction="vertical" size={0}>
            <Text strong>{record.nickName || record.userName}</Text>
            <Text type="secondary" style={{ fontSize: '12px' }}>
              {record.userName}
            </Text>
          </Space>
        </Space>
      ),
    },
    {
      title: "邮箱",
      dataIndex: "email",
      key: "email",
      render: (email: string) => (
        <Space>
          <MailOutlined />
          <Text>{email || "未设置"}</Text>
        </Space>
      ),
    },
    {
      title: "加入时间",
      key: "createTime",
      render: (record: QueryWikiUsersCommandResponseItem) => (
        <Space>
          <ClockCircleOutlined />
          <Text type="secondary" style={{ fontSize: '12px' }}>
            {formatDateTime(record.createTime)}
          </Text>
        </Space>
      ),
    },
    {
      title: "角色",
      key: "role",
      render: (record: QueryWikiUsersCommandResponseItem) => {
        const isCreator = record.createUserId === record.id;
        return (
          <Space>
            {isCreator ? (
              <Tag color="blue">创建者</Tag>
            ) : (
              <Tag color="green">成员</Tag>
            )}
          </Space>
        );
      },
    },
  ];

  const rowSelection = {
    selectedRowKeys: selectedUserIds,
    onChange: (selectedRowKeys: React.Key[]) => {
      setSelectedUserIds(selectedRowKeys as number[]);
    },
    getCheckboxProps: (record: QueryWikiUsersCommandResponseItem) => ({
      disabled: record.createUserId === record.id, // 不能移除创建者
    }),
  };

  return (
    <>
      {contextHolder}
      <Card>
        <Space style={{ marginBottom: 16 }}>
          <Button
            type="primary"
            icon={<UserAddOutlined />}
            onClick={() => setInviteModalVisible(true)}
          >
            邀请成员
          </Button>
          <Popconfirm
            title="确认移除"
            description={`确定要移除选中的 ${selectedUserIds.length} 个成员吗？`}
            icon={<ExclamationCircleOutlined style={{ color: 'red' }} />}
            onConfirm={handleRemoveUsers}
            okText="确定"
            cancelText="取消"
            disabled={selectedUserIds.length === 0}
          >
            <Button
              danger
              icon={<UserDeleteOutlined />}
              loading={removeLoading}
              disabled={selectedUserIds.length === 0}
            >
              移除成员 ({selectedUserIds.length})
            </Button>
          </Popconfirm>
        </Space>

        <Table
          rowKey="id"
          columns={columns}
          dataSource={users}
          loading={loading}
          rowSelection={rowSelection}
          pagination={{
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) =>
              `第 ${range[0]}-${range[1]} 条，共 ${total} 条`,
          }}
        />
      </Card>

      <Modal
        title="邀请新成员"
        open={inviteModalVisible}
        onCancel={() => {
          setInviteModalVisible(false);
          inviteForm.resetFields();
        }}
        footer={null}
        destroyOnClose
      >
        <Form
          form={inviteForm}
          layout="vertical"
          onFinish={handleInviteUser}
        >
          <Form.Item
            name="userName"
            label="用户账号"
            rules={[
              { required: true, message: "请输入用户账号" },
              { type: "string", message: "请输入正确的用户账号" },
            ]}
          >
            <Input
              placeholder="请输入要邀请的用户账号"
            />
          </Form.Item>

          <Form.Item>
            <Space>
              <Button
                type="primary"
                htmlType="submit"
                loading={inviteLoading}
              >
                邀请
              </Button>
              <Button
                onClick={() => {
                  setInviteModalVisible(false);
                  inviteForm.resetFields();
                }}
              >
                取消
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}