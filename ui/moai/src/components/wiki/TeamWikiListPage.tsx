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
  Tooltip,
} from "antd";
import {
  PlusOutlined,
  BookOutlined,
  UserOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  FileTextOutlined,
  TeamOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import type {
  QueryTeamWikiBaseListCommand,
  QueryWikiInfoResponse,
} from "../../apiClient/models";
import { useNavigate } from "react-router";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";
import SortPopover, { type SortState } from "../common/SortPopover";
import "./WikiListPage.css";

const { Paragraph } = Typography;

// ============================================
// 类型定义
// ============================================
interface WikiItem {
  id: number;
  title: string;
  description: string;
  createUserName?: string;
  createTime?: string;
  documentCount?: number;
  avatar?: string;
}

interface CreateWikiFormValues {
  name: string;
  description: string;
}

interface TeamWikiListPageProps {
  teamId: number;
  canManage?: boolean;
}

// ============================================
// 常量配置
// ============================================
const SORT_FIELDS = [
  { key: "name", label: "名称" },
  { key: "createTime", label: "创建时间" },
  { key: "updateTime", label: "更新时间" },
];

// ============================================
// 工具函数
// ============================================
const formatDateTime = (dateTimeString?: string): string => {
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

const sortWikiList = (list: WikiItem[], sortState: SortState): WikiItem[] => {
  return [...list].sort((a, b) => {
    for (const field of SORT_FIELDS) {
      const order = sortState[field.key];
      if (!order) continue;
      const multiplier = order === "asc" ? 1 : -1;
      if (field.key === "name") {
        return (a.title || "").localeCompare(b.title || "") * multiplier;
      }
      return (new Date(a.createTime || 0).getTime() - new Date(b.createTime || 0).getTime()) * multiplier;
    }
    return 0;
  });
};

const mapResponseToWikiItem = (item: QueryWikiInfoResponse): WikiItem => ({
  id: item.wikiId!,
  title: item.name!,
  description: item.description || "",
  createUserName: item.createUserName || undefined,
  createTime: item.createTime || undefined,
  documentCount: item.documentCount || 0,
  avatar: item.avatar || undefined,
});

// ============================================
// Wiki 卡片组件
// ============================================
interface WikiCardProps {
  item: WikiItem;
  onClick: (id: number) => void;
}

const WikiCard: React.FC<WikiCardProps> = ({ item, onClick }) => (
  <div className="wiki-card" onClick={() => onClick(item.id)}>
    <div className="wiki-card-header">
      <div className="wiki-card-avatar">
        {item.avatar ? (
          <img src={item.avatar} alt={item.title} />
        ) : (
          <BookOutlined />
        )}
      </div>
      <div className="wiki-card-title-section">
        <Tooltip title={item.title}>
          <h3 className="wiki-card-title">{item.title}</h3>
        </Tooltip>
        <div className="wiki-card-tags">
          <Tag color="orange" icon={<TeamOutlined />}>团队</Tag>
        </div>
      </div>
    </div>

    <div className="wiki-card-content">
      <Paragraph className="wiki-card-description">
        {item.description || "暂无描述"}
      </Paragraph>
    </div>

    <div className="wiki-card-footer">
      <div className="wiki-card-meta">
        <span className="wiki-card-meta-item">
          <UserOutlined />
          {item.createUserName || "未知用户"}
        </span>
        {item.createTime && (
          <span className="wiki-card-meta-item">
            <ClockCircleOutlined />
            {formatDateTime(item.createTime)}
          </span>
        )}
      </div>
      <div className="wiki-card-stats">
        <Tag color="purple" icon={<FileTextOutlined />}>
          {item.documentCount || 0} 文档
        </Tag>
      </div>
    </div>
  </div>
);

// ============================================
// 创建 Wiki 弹窗组件
// ============================================
interface CreateWikiModalProps {
  open: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateWikiFormValues) => void;
  form: ReturnType<typeof Form.useForm<CreateWikiFormValues>>[0];
  loading?: boolean;
}

const CreateWikiModal: React.FC<CreateWikiModalProps> = ({
  open,
  onCancel,
  onSubmit,
  form,
  loading,
}) => (
  <Modal
    title="新建团队知识库"
    open={open}
    onCancel={onCancel}
    footer={null}
    width={480}
    maskClosable={false}
    destroyOnClose
  >
    <Form form={form} layout="vertical" preserve={false} onFinish={onSubmit}>
      <Form.Item
        label="知识库名称"
        name="name"
        rules={[{ required: true, message: "请输入知识库名称" }]}
      >
        <Input placeholder="请输入知识库名称" maxLength={32} />
      </Form.Item>
      <Form.Item label="描述" name="description">
        <Input.TextArea
          placeholder="请输入描述"
          maxLength={200}
          rows={3}
          showCount
        />
      </Form.Item>
      <Form.Item style={{ marginBottom: 0, marginTop: 24, textAlign: "right" }}>
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
export default function TeamWikiListPage({ teamId, canManage = false }: TeamWikiListPageProps) {
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm<CreateWikiFormValues>();

  // 状态
  const [wikiList, setWikiList] = useState<WikiItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [sortState, setSortState] = useState<SortState>({ createTime: "desc" });

  // 获取知识库列表
  const fetchWikiList = useCallback(async () => {
    if (!teamId) return;
    setLoading(true);
    try {
      const client = GetApiClient();
      const command: QueryTeamWikiBaseListCommand = { teamId };
      const response = await client.api.wiki.query_team_wiki_list.post(command);
      setWikiList(response?.map(mapResponseToWikiItem) || []);
    } catch (error) {
      proxyRequestError(error, messageApi, "获取团队知识库列表失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, messageApi]);

  // 创建知识库
  const handleCreate = useCallback(
    async (values: CreateWikiFormValues) => {
      if (!teamId) return;
      setSubmitting(true);
      try {
        const client = GetApiClient();
        await client.api.wiki.create.post({
          name: values.name,
          description: values.description,
          teamId,
        });
        messageApi.success("创建成功");
        setModalOpen(false);
        form.resetFields();
        fetchWikiList();
      } catch (error) {
        proxyFormRequestError(error, messageApi, form, "创建失败");
      } finally {
        setSubmitting(false);
      }
    },
    [teamId, form, messageApi, fetchWikiList]
  );

  // 关闭弹窗
  const handleModalClose = useCallback(() => {
    setModalOpen(false);
    form.resetFields();
  }, [form]);

  // 跳转到知识库详情
  const handleWikiClick = useCallback(
    (id: number) => navigate(`/app/wiki/${id}`),
    [navigate]
  );

  // 排序后的列表
  const sortedWikiList = useMemo(
    () => sortWikiList(wikiList, sortState),
    [wikiList, sortState]
  );

  // 初始化加载
  useEffect(() => {
    fetchWikiList();
  }, [fetchWikiList]);

  return (
    <div className="page-container">
      {contextHolder}

      <CreateWikiModal
        open={modalOpen}
        onCancel={handleModalClose}
        onSubmit={handleCreate}
        form={form}
        loading={submitting}
      />

      <div className="moai-toolbar">
        <div className="moai-toolbar-left" />
        <div className="moai-toolbar-right">
          <SortPopover
            fields={SORT_FIELDS}
            value={sortState}
            onChange={setSortState}
          />
          <Button
            icon={<ReloadOutlined />}
            onClick={fetchWikiList}
            loading={loading}
          >
            刷新
          </Button>
          {canManage && (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setModalOpen(true)}
            >
              新建知识库
            </Button>
          )}
        </div>
      </div>

      <Spin spinning={loading}>
        {sortedWikiList.length > 0 ? (
          <div className="wiki-grid">
            {sortedWikiList.map((item) => (
              <WikiCard key={item.id} item={item} onClick={handleWikiClick} />
            ))}
          </div>
        ) : (
          <Empty
            description="暂无团队知识库"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            style={{ padding: "60px 0" }}
          />
        )}
      </Spin>
    </div>
  );
}
