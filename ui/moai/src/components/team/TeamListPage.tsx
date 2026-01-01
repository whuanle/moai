import { useEffect, useState, useCallback } from "react";
import {
  Card,
  Typography,
  Button,
  Space,
  Tag,
  Empty,
  Spin,
  message,
  Modal,
  Form,
  Input,
  Row,
  Col,
  Radio,
  Avatar,
} from "antd";
import {
  PlusOutlined,
  TeamOutlined,
  UserOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  CrownOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useNavigate } from "react-router";
import { proxyFormRequestError } from "../../helper/RequestError";
import type {
  QueryTeamListQueryResponseItem,
  CreateTeamCommand,
  TeamRole,
} from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";

const { Title, Text, Paragraph } = Typography;

// 筛选类型
type FilterType = "all" | "created" | "joined";

// 工具函数
const formatDateTime = (dateTimeString?: string | null): string => {
  if (!dateTimeString) return "";
  try {
    const date = new Date(dateTimeString);
    if (isNaN(date.getTime())) return dateTimeString;
    return date.toLocaleString("zh-CN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    });
  } catch {
    return dateTimeString;
  }
};

const getRoleTag = (role?: TeamRole | null) => {
  switch (role) {
    case TeamRoleObject.Owner:
      return <Tag color="gold" icon={<CrownOutlined />}>所有者</Tag>;
    case TeamRoleObject.Admin:
      return <Tag color="blue">管理员</Tag>;
    case TeamRoleObject.Collaborator:
      return <Tag color="green">协作者</Tag>;
    default:
      return null;
  }
};

// 团队卡片组件
interface TeamCardProps {
  item: QueryTeamListQueryResponseItem;
  onClick: (id: number) => void;
}

const TeamCard: React.FC<TeamCardProps> = ({ item, onClick }) => {
  return (
    <Col xs={24} sm={12} md={8} lg={6}>
      <Card
        hoverable
        onClick={() => onClick(item.id!)}
        style={{ cursor: "pointer", height: "100%" }}
      >
        <Card.Meta
          avatar={
            item.avatar ? (
              <Avatar size={48} src={item.avatar} />
            ) : (
              <Avatar size={48} icon={<TeamOutlined />} />
            )
          }
          title={
            <Space align="center" style={{ width: "100%" }}>
              <Text strong style={{ fontSize: "16px" }}>
                {item.name}
              </Text>
              {getRoleTag(item.role)}
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Paragraph
                type="secondary"
                ellipsis={{ rows: 2, tooltip: item.description }}
                style={{ margin: 0 }}
              >
                {item.description || "暂无描述"}
              </Paragraph>

              <Space size="small" wrap>
                <Tag color="purple" icon={<UserOutlined />}>
                  {item.memberCount || 0} 成员
                </Tag>
              </Space>

              <Space direction="vertical" size={0} style={{ marginTop: 8 }}>
                <Space size="small">
                  <ClockCircleOutlined />
                  <Text type="secondary" style={{ fontSize: "12px" }}>
                    {formatDateTime(item.createTime)}
                  </Text>
                </Space>
              </Space>
            </Space>
          }
        />
      </Card>
    </Col>
  );
};

// 创建团队表单
interface CreateTeamModalProps {
  open: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateTeamCommand) => void;
  form: any;
  loading: boolean;
}

const CreateTeamModal: React.FC<CreateTeamModalProps> = ({
  open,
  onCancel,
  onSubmit,
  form,
  loading,
}) => (
  <Modal
    title="新建团队"
    open={open}
    onCancel={onCancel}
    onOk={() => form.submit()}
    confirmLoading={loading}
    maskClosable={false}
    okText="创建"
    cancelText="取消"
    destroyOnClose
  >
    <Form form={form} layout="vertical" preserve={false} onFinish={onSubmit}>
      <Form.Item
        label="团队名称"
        name="name"
        rules={[{ required: true, message: "请输入团队名称" }]}
      >
        <Input placeholder="请输入团队名称" maxLength={100} />
      </Form.Item>
      <Form.Item label="团队描述" name="description">
        <Input.TextArea placeholder="请输入团队描述" maxLength={500} rows={3} />
      </Form.Item>
    </Form>
  </Modal>
);

// 主组件
export default function TeamListPage() {
  const navigate = useNavigate();
  const [teamList, setTeamList] = useState<QueryTeamListQueryResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [filterType, setFilterType] = useState<FilterType>("all");
  const [modalOpen, setModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  const fetchTeamList = useCallback(
    async (filter: FilterType = "all") => {
      try {
        setLoading(true);
        const client = GetApiClient();
        const filterTypeMap: Record<FilterType, string | undefined> = {
          all: undefined,
          created: "created",
          joined: "joined",
        };
        const response = await client.api.team.list.post({
          filterType: filterTypeMap[filter],
        });

        if (response?.items) {
          setTeamList(response.items);
        }
      } catch (error) {
        console.error("获取团队列表失败:", error);
        messageApi.error("获取团队列表失败");
      } finally {
        setLoading(false);
      }
    },
    [messageApi]
  );

  useEffect(() => {
    fetchTeamList("all");
  }, []);

  const handleFilterChange = (filter: FilterType) => {
    setFilterType(filter);
    fetchTeamList(filter);
  };

  const handleCreateTeam = async (values: CreateTeamCommand) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      const response = await client.api.team.create.post(values);
      if (response?.teamId) {
        messageApi.success("创建成功");
        setModalOpen(false);
        form.resetFields();
        fetchTeamList(filterType);
      }
    } catch (err: any) {
      proxyFormRequestError(err, messageApi, form, "创建失败");
    } finally {
      setCreateLoading(false);
    }
  };

  const handleCardClick = (id: number) => {
    navigate(`/app/team/${id}`);
  };

  return (
    <>
      {contextHolder}

      <CreateTeamModal
        open={modalOpen}
        onCancel={() => {
          setModalOpen(false);
          form.resetFields();
        }}
        onSubmit={handleCreateTeam}
        form={form}
        loading={createLoading}
      />

      <Card style={{ margin: "24px 24px 0" }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <Space size="large">
            <Space>
              <TeamOutlined />
              <Title level={3} style={{ margin: 0 }}>
                团队管理
              </Title>
            </Space>
            <Space>
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => setModalOpen(true)}
              >
                新建团队
              </Button>
              <Button
                icon={<ReloadOutlined />}
                onClick={() => fetchTeamList(filterType)}
                loading={loading}
              >
                刷新
              </Button>
              <Radio.Group
                value={filterType}
                onChange={(e) => handleFilterChange(e.target.value)}
              >
                <Radio.Button value="all">全部</Radio.Button>
                <Radio.Button value="created">我创建的</Radio.Button>
                <Radio.Button value="joined">我加入的</Radio.Button>
              </Radio.Group>
            </Space>
          </Space>
        </div>
      </Card>

      <div style={{ padding: "0 24px 24px" }}>
        <Card>
          <Spin spinning={loading}>
            {teamList.length > 0 ? (
              <Row gutter={[16, 16]}>
                {teamList.map((item) => (
                  <TeamCard
                    key={item.id}
                    item={item}
                    onClick={handleCardClick}
                  />
                ))}
              </Row>
            ) : (
              <Empty
                description="暂无团队数据"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            )}
          </Spin>
        </Card>
      </div>
    </>
  );
}
