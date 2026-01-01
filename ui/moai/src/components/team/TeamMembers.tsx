import { useState, useEffect, useCallback } from "react";
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
  Select,
} from "antd";
import {
  UserAddOutlined,
  UserDeleteOutlined,
  UserOutlined,
  CrownOutlined,
  ExclamationCircleOutlined,
  SwapOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams } from "react-router";
import { proxyRequestError } from "../../helper/RequestError";
import type { QueryTeamMemberListQueryResponseItem, TeamRole } from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";

const { Text } = Typography;

const getRoleLabel = (role: TeamRole | null | undefined) => {
  switch (role) {
    case TeamRoleObject.Owner:
      return <Tag color="gold" icon={<CrownOutlined />}>所有者</Tag>;
    case TeamRoleObject.Admin:
      return <Tag color="blue">管理员</Tag>;
    case TeamRoleObject.Collaborator:
      return <Tag color="green">协作者</Tag>;
    default:
      return <Tag>未知</Tag>;
  }
};

export default function TeamMembers() {
  const { id } = useParams();
  const [members, setMembers] = useState<QueryTeamMemberListQueryResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [myRole, setMyRole] = useState<TeamRole | null>(null);
  const [inviteModalVisible, setInviteModalVisible] = useState(false);
  const [inviteLoading, setInviteLoading] = useState(false);
  const [roleModalVisible, setRoleModalVisible] = useState(false);
  const [roleLoading, setRoleLoading] = useState(false);
  const [transferModalVisible, setTransferModalVisible] = useState(false);
  const [transferLoading, setTransferLoading] = useState(false);
  const [selectedMember, setSelectedMember] = useState<QueryTeamMemberListQueryResponseItem | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState<number[]>([]);
  const [messageApi, contextHolder] = message.useMessage();
  const [inviteForm] = Form.useForm();
  const [roleForm] = Form.useForm();
  const [transferForm] = Form.useForm();

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;
  const isOwner = myRole === TeamRoleObject.Owner;

  // 获取用户在团队的角色
  const fetchMyRole = useCallback(async () => {
    if (!id) return;
    try {
      const apiClient = GetApiClient();
      const response = await apiClient.api.team.role.post({
        teamId: parseInt(id),
      });
      setMyRole(response?.role || null);
    } catch (error) {
      console.error("获取角色失败:", error);
    }
  }, [id]);

  // 获取成员列表
  const fetchMembers = useCallback(async () => {
    if (!id) return;
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.team.members.post({
        teamId: parseInt(id),
      });
      setMembers(response?.items || []);
    } catch (error) {
      console.error("获取成员列表失败:", error);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchMyRole();
    fetchMembers();
  }, [fetchMyRole, fetchMembers]);

  const handleInviteUser = async (values: { userName: string; role: TeamRole }) => {
    if (!id) return;

    try {
      setInviteLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.team.invite.post({
        teamId: parseInt(id),
        userNames: [values.userName],
      });

      messageApi.success("邀请成功");
      setInviteModalVisible(false);
      inviteForm.resetFields();
      fetchMembers();
    } catch (error) {
      proxyRequestError(error, messageApi, "邀请失败");
    } finally {
      setInviteLoading(false);
    }
  };

  const handleRemoveMembers = async () => {
    if (!id || selectedUserIds.length === 0) {
      messageApi.error("请选择要移除的成员");
      return;
    }

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.team.member.remove.post({
        teamId: parseInt(id),
        userIds: selectedUserIds,
      });

      messageApi.success("移除成功");
      setSelectedUserIds([]);
      fetchMembers();
    } catch (error) {
      proxyRequestError(error, messageApi, "移除失败");
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateRole = async (values: { role: TeamRole }) => {
    if (!id || !selectedMember) return;

    try {
      setRoleLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.team.member.role.post({
        teamId: parseInt(id),
        userId: selectedMember.userId,
        role: values.role,
      });

      messageApi.success("修改角色成功");
      setRoleModalVisible(false);
      roleForm.resetFields();
      setSelectedMember(null);
      fetchMembers();
    } catch (error) {
      proxyRequestError(error, messageApi, "修改角色失败");
    } finally {
      setRoleLoading(false);
    }
  };

  const handleTransferOwner = async (values: { newOwnerUserId: number }) => {
    if (!id) return;

    try {
      setTransferLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.team.transfer.post({
        teamId: parseInt(id),
        newOwnerUserId: values.newOwnerUserId,
      });

      messageApi.success("转让成功");
      setTransferModalVisible(false);
      transferForm.resetFields();
      fetchMyRole();
      fetchMembers();
    } catch (error) {
      proxyRequestError(error, messageApi, "转让失败");
    } finally {
      setTransferLoading(false);
    }
  };

  const handleLeaveTeam = async () => {
    if (!id) return;

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.team.leave.post({
        teamId: parseInt(id),
      });

      messageApi.success("已退出团队");
      window.location.href = "/app/team/list";
    } catch (error) {
      proxyRequestError(error, messageApi, "退出失败");
    } finally {
      setLoading(false);
    }
  };

  const columns = [
    {
      title: "用户信息",
      key: "userInfo",
      render: (_: unknown, record: QueryTeamMemberListQueryResponseItem) => (
        <Space>
          <Avatar src={record.avatar} icon={<UserOutlined />} size="default" />
          <Text strong>{record.userName}</Text>
        </Space>
      ),
    },
    {
      title: "加入时间",
      dataIndex: "joinTime",
      key: "joinTime",
    },
    {
      title: "角色",
      key: "role",
      render: (_: unknown, record: QueryTeamMemberListQueryResponseItem) => getRoleLabel(record.role),
    },
    {
      title: "操作",
      key: "action",
      render: (_: unknown, record: QueryTeamMemberListQueryResponseItem) => {
        if (record.role === TeamRoleObject.Owner) return null;
        if (!canManage) return null;
        
        return (
          <Space>
            <Button
              type="link"
              size="small"
              onClick={() => {
                setSelectedMember(record);
                roleForm.setFieldsValue({ role: record.role });
                setRoleModalVisible(true);
              }}
            >
              修改角色
            </Button>
          </Space>
        );
      },
    },
  ];

  const rowSelection = canManage
    ? {
        selectedRowKeys: selectedUserIds,
        onChange: (selectedRowKeys: React.Key[]) => {
          setSelectedUserIds(selectedRowKeys as number[]);
        },
        getCheckboxProps: (record: QueryTeamMemberListQueryResponseItem) => ({
          disabled: record.role === TeamRoleObject.Owner,
        }),
      }
    : undefined;


  return (
    <>
      {contextHolder}
      <Card
        title="成员管理"
        extra={
          <Space>
            {canManage && (
              <>
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
                  icon={<ExclamationCircleOutlined style={{ color: "red" }} />}
                  onConfirm={handleRemoveMembers}
                  okText="确定"
                  cancelText="取消"
                  disabled={selectedUserIds.length === 0}
                >
                  <Button
                    danger
                    icon={<UserDeleteOutlined />}
                    disabled={selectedUserIds.length === 0}
                  >
                    移除成员
                  </Button>
                </Popconfirm>
              </>
            )}
            {isOwner && (
              <Button
                icon={<SwapOutlined />}
                onClick={() => setTransferModalVisible(true)}
              >
                转让团队
              </Button>
            )}
            {!isOwner && (
              <Popconfirm
                title="确认退出"
                description="确定要退出该团队吗？"
                icon={<ExclamationCircleOutlined style={{ color: "red" }} />}
                onConfirm={handleLeaveTeam}
                okText="确定"
                cancelText="取消"
              >
                <Button danger>退出团队</Button>
              </Popconfirm>
            )}
          </Space>
        }
      >
        <Table
          rowKey="userId"
          columns={columns}
          dataSource={members}
          loading={loading}
          rowSelection={rowSelection}
          pagination={{
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) =>
              `第 ${range[0]}-${range[1]} 条，共 ${total} 条`,
          }}
          locale={{ emptyText: "暂无成员数据，请先邀请成员加入团队" }}
        />
      </Card>

      {/* 邀请成员弹窗 */}
      <Modal
        title="邀请新成员"
        open={inviteModalVisible}
        onCancel={() => {
          setInviteModalVisible(false);
          inviteForm.resetFields();
        }}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={inviteForm} layout="vertical" onFinish={handleInviteUser}>
          <Form.Item
            name="userName"
            label="用户账号"
            rules={[{ required: true, message: "请输入用户账号" }]}
          >
            <Input placeholder="请输入要邀请的用户账号" />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={inviteLoading}>
                邀请
              </Button>
              <Button onClick={() => setInviteModalVisible(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 修改角色弹窗 */}
      <Modal
        title="修改成员角色"
        open={roleModalVisible}
        onCancel={() => {
          setRoleModalVisible(false);
          roleForm.resetFields();
          setSelectedMember(null);
        }}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={roleForm} layout="vertical" onFinish={handleUpdateRole}>
          <Form.Item
            name="role"
            label="角色"
            rules={[{ required: true, message: "请选择角色" }]}
          >
            <Select>
              <Select.Option value={TeamRoleObject.Collaborator}>协作者</Select.Option>
              {isOwner && (
                <Select.Option value={TeamRoleObject.Admin}>管理员</Select.Option>
              )}
            </Select>
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={roleLoading}>
                确定
              </Button>
              <Button onClick={() => setRoleModalVisible(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 转让团队弹窗 */}
      <Modal
        title="转让团队"
        open={transferModalVisible}
        onCancel={() => {
          setTransferModalVisible(false);
          transferForm.resetFields();
        }}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={transferForm} layout="vertical" onFinish={handleTransferOwner}>
          <Form.Item
            name="newOwnerUserId"
            label="新所有者"
            rules={[{ required: true, message: "请选择新所有者" }]}
          >
            <Select placeholder="请选择新所有者">
              {members
                .filter((m) => m.role !== TeamRoleObject.Owner)
                .map((m) => (
                  <Select.Option key={m.userId} value={m.userId}>
                    {m.userName}
                  </Select.Option>
                ))}
            </Select>
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={transferLoading}>
                确认转让
              </Button>
              <Button onClick={() => setTransferModalVisible(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
