import { useState, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router';
import { Input, Button, Tag, message, Spin, Popconfirm, Pagination, Empty, Checkbox } from 'antd';
import { PlusOutlined, EditOutlined, EyeOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';
import { GetApiClient } from '../ServiceClient';
import { proxyRequestError } from '../../helper/RequestError';
import useAppStore from '../../stateshare/store';
import SortPopover, { SortState } from '../common/SortPopover';
import { 
  QueryPromptListCommand, 
  QueryPromptListCommandResponse, 
  PromptItem, 
  PromptClassifyItem,
  QueryePromptClassCommandResponse,
  DeletePromptCommand,
  KeyValueBool
} from '../../apiClient/models';
import './PromptListPage.css';

// 排序字段配置
const SORT_FIELDS = [
  { key: 'name', label: '名称' },
  { key: 'createTime', label: '创建时间' },
];

export default function PromptListPage() {
  const navigate = useNavigate();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  const isAdmin = userDetailInfo?.isAdmin === true;
  const currentUserId = userDetailInfo?.userId;
  
  const [loading, setLoading] = useState(false);
  const [prompts, setPrompts] = useState<PromptItem[]>([]);
  const [categories, setCategories] = useState<PromptClassifyItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [searchText, setSearchText] = useState('');
  const [onlyMine, setOnlyMine] = useState(true);
  const [messageApi, contextHolder] = message.useMessage();
  
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;
  const [sortState, setSortState] = useState<SortState>({ name: null, createTime: null });


  const fetchCategories = async () => {
    try {
      const client = GetApiClient();
      const response: QueryePromptClassCommandResponse | undefined = await client.api.prompt.class_list.get();
      if (response?.items) setCategories(response.items);
    } catch (error) {
      console.error('获取分类列表失败:', error);
      proxyRequestError(error, messageApi, '获取分类列表失败');
    }
  };

  const fetchPrompts = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      
      // 构建排序字段
      const orderByFields: KeyValueBool[] = [];
      if (sortState.name) {
        orderByFields.push({ key: 'Name', value: sortState.name === 'desc' });
      }
      if (sortState.createTime) {
        orderByFields.push({ key: 'CreateTime', value: sortState.createTime === 'desc' });
      }
      
      const requestBody: QueryPromptListCommand = {
        classId: selectedCategory ?? undefined,
        search: searchText || undefined,
        isOwn: onlyMine ? true : undefined,
        orderByFields: orderByFields.length > 0 ? orderByFields : undefined,
        pageNo: 1,
        pageSize: 500,
      };
      const response: QueryPromptListCommandResponse | undefined = await client.api.prompt.prompt_list.post(requestBody);
      if (response?.items) {
        setPrompts(response.items);
        setCurrentPage(1);
      }
    } catch (error) {
      console.error('获取提示词列表失败:', error);
      proxyRequestError(error, messageApi, '获取提示词列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 由于后端已处理排序，前端直接使用返回数据
  const sortedPrompts = prompts;

  const paginatedPrompts = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return sortedPrompts.slice(start, start + pageSize);
  }, [sortedPrompts, currentPage, pageSize]);

  useEffect(() => { fetchCategories(); fetchPrompts(); }, []);


  const handleDelete = async (record: PromptItem) => {
    try {
      const client = GetApiClient();
      const requestBody: DeletePromptCommand = { promptId: record.id };
      await client.api.prompt.delete_prompt.delete(requestBody);
      messageApi.success('删除成功');
      fetchPrompts();
    } catch (error) {
      console.error('删除提示词失败:', error);
      proxyRequestError(error, messageApi, '删除提示词失败');
    }
  };

  const handleView = (record: PromptItem) => {
    navigate(`/app/prompt/${record.id}/view`);
  };

  const getCategoryName = (classId: number) => categories.find(c => c.classifyId === classId)?.name || '未分类';
  const isOwner = (prompt: PromptItem) => prompt.createUserId === currentUserId;


  const renderPromptCard = (prompt: PromptItem) => {
    const owned = isOwner(prompt);
    return (
      <div key={prompt.id} className="prompt-card">
        {owned && (
          <Popconfirm title="确认删除" description="确定要删除这个提示词吗？" onConfirm={() => handleDelete(prompt)} okText="确定" cancelText="取消">
            <Button danger type='text' size="small" icon={<DeleteOutlined />} className="prompt-card-delete" />
          </Popconfirm>
        )}
        <div className="prompt-card-body" onClick={() => handleView(prompt)}>
          <div className="prompt-card-title">{prompt.name}</div>
          <div className="prompt-card-desc">{prompt.description || '暂无描述'}</div>
          <div className="prompt-card-meta">
            <Tag color="blue">{getCategoryName(prompt.promptClassId || 0)}</Tag>
            <Tag color={prompt.isPublic ? 'green' : 'orange'}>{prompt.isPublic ? '公开' : '私有'}</Tag>
            <span className="prompt-card-time">{prompt.createTime ? new Date(prompt.createTime).toLocaleDateString() : ''}</span>
          </div>
        </div>
        <div className="prompt-card-footer">
          <Button type="text" size="small" icon={<EyeOutlined />} onClick={() => handleView(prompt)}>查看</Button>
          {owned && <Button type="text" size="small" icon={<EditOutlined />} onClick={() => navigate(`/app/prompt/${prompt.id}/edit`)}>编辑</Button>}
        </div>
      </div>
    );
  };


  return (
    <div className="page-container">
      {contextHolder}
      
      {/* 分类标签栏 */}
      <div className="prompt-category-bar">
        <div className="category-tags">
          <Tag className={`category-tag ${selectedCategory === null ? 'active' : ''}`} onClick={() => setSelectedCategory(null)}>全部</Tag>
          {categories.map(category => (
            <Tag key={category.classifyId} className={`category-tag ${selectedCategory === category.classifyId ? 'active' : ''}`} onClick={() => setSelectedCategory(category.classifyId ?? null)}>
              {category.name}
            </Tag>
          ))}
          {isAdmin && (
            <Tag className="category-tag category-tag-edit" onClick={() => navigate('/app/prompt/class')}>
              <EditOutlined /> 编辑分类
            </Tag>
          )}
        </div>
      </div>

      {/* 筛选工具栏 - 左对齐 */}
      <div className="prompt-toolbar">
        <Input
          placeholder="搜索提示词"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
          allowClear
          className="prompt-search-input"
        />
        <Checkbox checked={onlyMine} onChange={(e) => setOnlyMine(e.target.checked)}>只看我的</Checkbox>
        <Button icon={<SearchOutlined />} onClick={fetchPrompts} loading={loading}>查找</Button>
        <SortPopover fields={SORT_FIELDS} value={sortState} onChange={setSortState} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/app/prompt/create')}>新增提示词</Button>
      </div>


      {/* 提示词卡片列表 */}
      <Spin spinning={loading}>
        {paginatedPrompts.length > 0 ? (
          <>
            <div className="prompt-card-grid">{paginatedPrompts.map(renderPromptCard)}</div>
            <div className="prompt-pagination">
              <Pagination current={currentPage} pageSize={pageSize} total={sortedPrompts.length} onChange={setCurrentPage} showSizeChanger={false} showQuickJumper showTotal={(total) => `共 ${total} 条记录`} />
            </div>
          </>
        ) : (
          <Empty description="暂无提示词" />
        )}
      </Spin>
    </div>
  );
}
