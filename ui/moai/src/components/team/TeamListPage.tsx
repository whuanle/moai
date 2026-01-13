import { useEffect, useState, useCallback, useMemo } from "react";
import {
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
  Radio,
  Avatar,
  Tooltip,
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
import SortPopover, { type SortState } from "../common/SortPopover";
import "./TeamListPage.css";

const { Paragraph } = Typography;

// ============================================
// 类型定义
// ============================================
type FilterType = "all" | "created" | "joined";

// ============================================
// 常量配置
// ============================================
const SORT_FIELDS = [
  { key: "name", label: "名称" },
  { key: "createTime", label: "创建时间" },
];

// ============================================
// 工具函数
// ============================================
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

const sortTeamList = (
  list: QueryTeamListQueryResponseItem[],
  sortState: SortState
): QueryTeamListQueryResponseItem[] => {
  return [...list].sort((a, b) => {
    for (const field of SORT_FIELDS) {
      const order = sortState[field.key];
      if (!order) continue;
      const multiplier = order === "asc" ? 1 : -1;
      if (field.key === "name") {
        return (a.name || "").localeCompare(b.name || "") * multiplier;
      }
      return (
        (new Date(a.createTime || 0).getTime() -
          new Date(b.createTime || 0).getTime()) *
        multiplier
      );
    }
    return 0;
  });
};

// ============================================
// 团队卡片组件
// ============================================
interface TeamCardProps {
  item: QueryTeamListQueryResponseItem;
  onClick: (id: number) => void;
}

const TeamCard: React.FC<TeamCardProps> = ({ item, onClick }) => (
  <div className="team-card" onClick={() => onClick(item.id!)}>
    <div className="team-card-header">
      <div className="team-card-avatar">
        {item.avatar ? (
          <Avatar size={56} src={item.avatar} />
        ) : (
          <Avatar size={56} icon={<TeamOutlined />} />
        )}
      </div>
      <div className="team-card-title-section">
        <Tooltip title={item.name}>
          <h3 className="team-card-title">{item.name}</h3>
        </Tooltip>
        <div className="team-card-tags">{getRoleTag(item.role)}</div>
      </div>
    </div>

    <div className="team-card-content">
      <Paragraph className="team-card-description">
        {item.description || "暂无描述"}
      </Paragraph>
    </div>

    <div className="team-card-footer">
      <div className="team-card-meta">
        <span className="team-card-meta-item">
          <UserOutlined />
          {item.memberCount || 0} 成员
        </span>
        {item.createTime && (
          <span className="team-card-meta-item">
            <ClockCircleOutlined />
            {formatDateTime(item.createTime)}
          </span>
        )}
      </div>
    </div>
  </div>
);

// ============================================
// 创建团队弹窗组件
// ============================================
interface CreateTeamFormValues {
  name: string;
  description?: string;
}

interface CreateTeamModalProps {
  open: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateTeamFormValues) => void;
  form: ReturnType<typeof Form.useForm<CreateTeamFormValues>>[0];
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
    footer={null}
    width={480}
    maskClosable={false}
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
        <Input.TextArea
          placeholder="请输入团队描述"
          maxLength={500}
          rows={3}
          showCount
        />
      </Form.Item>
      <Form.Item className="modal-footer-actions">
        <Space>
          <Button onClick={onCancel}>取消</Button>
          <Button type="primary" htmlType="submit" loading={loading}>
            创建
          </Button>
        </Space>
      </Form.Item>
    </Form>
  </Modal>
);

// ============================================
// 主组件
// ============================================
export default function TeamListPage() {
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm<CreateTeamFormValues>();

  // 状态
  const [teamList, setTeamList] = useState<QueryTeamListQueryResponseItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [filterType, setFilterType] = useState<FilterType>("all");
  const [sortState, setSortState] = useState<SortState>({ createTime: "desc" });

  // 获取团队列表
  const fetchTeamList = useCallback(
    async (filter: FilterType = filterType) => {
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
    [filterType, messageApi]
  );

  // 创建团队
  const handleCreate = useCallback(
    async (values: CreateTeamCommand) => {
      try {
        setSubmitting(true);
        const client = GetApiClient();
        const response = await client.api.team.create.post(values);
        if (response?.teamId) {
          messageApi.success("创建成功");
          setModalOpen(false);
          form.resetFields();
          fetchTeamList(filterType);
        }
      } catch (err) {
        proxyFormRequestError(err, messageApi, form, "创建失败");
      } finally {
        setSubmitting(false);
      }
    },
    [form, messageApi, fetchTeamList, filterType]
  );

  // 筛选变更
  const handleFilterChange = useCallback(
    (filter: FilterType) => {
      setFilterType(filter);
      fetchTeamList(filter);
    },
    [fetchTeamList]
  );

  // 排序后的列表
  const sortedTeamList = useMemo(
    () => sortTeamList(teamList, sortState),
    [teamList, sortState]
  );

  // 初始化加载
  useEffect(() => {
    fetchTeamList("all");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="page-container">
      {contextHolder}

      <CreateTeamModal
        open={modalOpen}
        onCancel={() => {
          setModalOpen(false);
          form.resetFields();
        }}
        onSubmit={handleCreate}
        form={form}
        loading={submitting}
      />

      <div className="moai-toolbar">
        <div className="moai-toolbar-left">
          <Radio.Group
            value={filterType}
            onChange={(e) => handleFilterChange(e.target.value)}
            optionType="button"
            buttonStyle="solid"
            className="team-filter-radio"
          >
            <Radio.Button value="all">全部</Radio.Button>
            <Radio.Button value="created">我创建的</Radio.Button>
            <Radio.Button value="joined">我加入的</Radio.Button>
          </Radio.Group>
          <SortPopover
            fields={SORT_FIELDS}
            value={sortState}
            onChange={setSortState}
          />
        </div>
        <div className="moai-toolbar-right">
          <Button
            icon={<ReloadOutlined />}
            onClick={() => fetchTeamList()}
            loading={loading}
          >
            刷新
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setModalOpen(true)}
          >
            新建团队
          </Button>
        </div>
      </div>

      <Spin spinning={loading}>
        {sortedTeamList.length > 0 ? (
          <div className="team-grid">
            {sortedTeamList.map((item) => (
              <TeamCard
                key={item.id}
                item={item}
                onClick={(id) => navigate(`/app/team/${id}`)}
              />
            ))}
          </div>
        ) : (
          <Empty
            description="暂无团队数据"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            className="moai-empty"
          />
        )}
      </Spin>
    </div>
  );
}
