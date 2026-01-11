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
  LockOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import type { QueryWikiBaseListCommand } from "../../apiClient/models";
import { useNavigate } from "react-router";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";
import SortPopover, { type SortState } from "../common/SortPopover";
import "./WikiListPage.css";

const { Title, Paragraph } = Typography;

// ============================================
// 类型定义
// ============================================
interface WikiItem {
  id: number;
  title: string;
  description: string;
  isPublic: boolean;
  createUserName?: string;
  createTime?: string;
  documentCount?: number;
  teamId?: number;
  teamName?: string;
  avatar?: string;
}

type FilterType = "personal" | "team";

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
          {item.teamId ? (
            <Tag color="orange" icon={<TeamOutlined />}>
              {item.teamName || "团队"}
            </Tag>
          ) : (
            <Tag color="blue" icon={<LockOutlined />}>
              私有
            </Tag>
          )}
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
interface CreateWikiFormValues {
  name: string;
  description: string;
}

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
    title="新建私有知识库"
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
export default function WikiListPage() {
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm<CreateWikiFormValues>();

  // 状态
  const [wikiList, setWikiList] = useState<WikiItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [filterType, setFilterType] = useState<FilterType>("personal");
  const [sortState, setSortState] = useState<SortState>({ createTime: "desc" });

  // 获取知识库列表
  const fetchWikiList = useCallback(
    async (filter: FilterType = filterType) => {
      try {
        setLoading(true);
        const client = GetApiClient();
        const command: QueryWikiBaseListCommand = {
          isOwn: filter === "personal" ? true : undefined,
          isInTeam: filter === "team" ? true : undefined,
        };
        const response = await client.api.wiki.query_wiki_list.post(command);
        if (response) {
          setWikiList(
            response.map((item) => ({
              id: item.wikiId!,
              title: item.name!,
              description: item.description || "",
              isPublic: item.isPublic || false,
              createUserName: item.createUserName || undefined,
              createTime: item.createTime || undefined,
              documentCount: item.documentCount || 0,
              teamId: item.teamId || undefined,
              teamName: item.teamName || undefined,
              avatar: item.avatar || undefined,
            }))
          );
        }
      } catch (error) {
        proxyRequestError(error, messageApi, "获取知识库列表失败");
      } finally {
        setLoading(false);
      }
    },
    [filterType, messageApi]
  );

  // 创建知识库
  const handleCreate = useCallback(
    async (values: { name: string; description: string }) => {
      try {
        setSubmitting(true);
        const client = GetApiClient();
        await client.api.wiki.create.post({
          name: values.name,
          description: values.description,
        });
        messageApi.success("创建成功");
        setModalOpen(false);
        form.resetFields();
        fetchWikiList();
      } catch (err) {
        proxyFormRequestError(err, messageApi, form, "创建失败");
      } finally {
        setSubmitting(false);
      }
    },
    [form, messageApi, fetchWikiList]
  );

  // 筛选变更
  const handleFilterChange = useCallback(
    (filter: FilterType) => {
      setFilterType(filter);
      fetchWikiList(filter);
    },
    [fetchWikiList]
  );

  // 排序后的列表
  const sortedWikiList = useMemo(
    () => sortWikiList(wikiList, sortState),
    [wikiList, sortState]
  );

  // 初始化加载
  useEffect(() => {
    fetchWikiList("personal");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="page-container">
      {contextHolder}

      <CreateWikiModal
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
          {/* <Space align="center" style={{ marginRight: 16 }}>
            <BookOutlined style={{ fontSize: 20, color: "var(--color-primary)" }} />
            <Title level={4} style={{ margin: 0 }}>
              管理和组织你的知识库文档
            </Title>
          </Space> */}
          <Radio.Group
            value={filterType}
            onChange={(e) => handleFilterChange(e.target.value)}
            optionType="button"
            buttonStyle="solid"
            className="wiki-filter-radio"
          >
            <Radio.Button value="personal">个人</Radio.Button>
            <Radio.Button value="team">团队</Radio.Button>
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
            onClick={() => fetchWikiList()}
            loading={loading}
          >
            刷新
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setModalOpen(true)}
          >
            新建知识库
          </Button>
        </div>
      </div>

      <Spin spinning={loading}>
        {sortedWikiList.length > 0 ? (
          <div className="wiki-grid">
            {sortedWikiList.map((item) => (
              <WikiCard
                key={item.id}
                item={item}
                onClick={(id) => navigate(`/app/wiki/${id}`)}
              />
            ))}
          </div>
        ) : (
          <Empty
            description="暂无知识库数据"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            style={{ padding: "60px 0" }}
          />
        )}
      </Spin>
    </div>
  );
}
