import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { Card, Input, Select, Button, Table, Tag, Space, message, Spin, Popconfirm, Modal, Typography } from 'antd';
import { SearchOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import Vditor from 'vditor';
import 'vditor/dist/index.css';
import { GetApiClient } from '../ServiceClient';
import { 
  QueryPromptListCommand, 
  QueryPromptListCommandResponse, 
  PromptItem, 
  PromptClassifyItem,
  QueryePromptClassCommandResponse,
  PromptFilterCondition,
  PromptFilterConditionObject,
  DeletePromptCommand
} from '../../apiClient/models';
import './PromptListPage.css';

const { Title, Text } = Typography;

const { Search } = Input;
const { Option } = Select;

export default function PromptListPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [prompts, setPrompts] = useState<PromptItem[]>([]);
  const [categories, setCategories] = useState<PromptClassifyItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<number | undefined>(undefined);
  const [searchText, setSearchText] = useState('');
  const [filterCondition, setFilterCondition] = useState<PromptFilterCondition>('none');
  const [messageApi, contextHolder] = message.useMessage();
  
  // 查看详情相关状态
  const [modalVisible, setModalVisible] = useState(false);
  const [viewingPrompt, setViewingPrompt] = useState<PromptItem | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const vditorId = useRef(`vditor-preview-${Math.random().toString(36).substr(2, 9)}`);

  // 筛选条件选项
  const filterOptions = [
    { value: 'none', label: '全部' },
    { value: 'own', label: '我创建的' },
    { value: 'ownPublic', label: '我共享的' },
    { value: 'ownPrivate', label: '个人私密' },
    { value: 'otherShare', label: '他人共享的' },
  ];

  // 获取分类列表
  const fetchCategories = async () => {
    try {
      const client = GetApiClient();
      const response: QueryePromptClassCommandResponse | undefined = await client.api.prompt.class_list.get();
      if (response?.items) {
        setCategories(response.items);
      }
    } catch (error) {
      console.error('获取分类列表失败:', error);
      messageApi.error('获取分类列表失败');
    }
  };

  // 获取提示词列表
  const fetchPrompts = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryPromptListCommand = {
        classId: selectedCategory,
        condition: filterCondition === 'none' ? undefined : filterCondition,
        search: searchText || undefined,
        pageNo: 1,
        pageSize: 100,
      };

      const response: QueryPromptListCommandResponse | undefined = await client.api.prompt.prompt_list.post(requestBody);
      if (response?.items) {
        setPrompts(response.items);
      }
    } catch (error) {
      console.error('获取提示词列表失败:', error);
      messageApi.error('获取提示词列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 页面初始化
  useEffect(() => {
    fetchCategories();
  }, []);

  // 当筛选条件改变时重新获取数据
  useEffect(() => {
    fetchPrompts();
  }, [selectedCategory, searchText, filterCondition]);

  // 处理搜索
  const handleSearch = (value: string) => {
    setSearchText(value);
  };

  // 处理分类筛选
  const handleCategoryChange = (value: number | undefined) => {
    setSelectedCategory(value);
  };

  // 处理筛选条件改变
  const handleFilterChange = (value: PromptFilterCondition) => {
    setFilterCondition(value);
  };

  // 处理删除提示词
  const handleDelete = async (record: PromptItem) => {
    try {
      const client = GetApiClient();
      const requestBody: DeletePromptCommand = {
        promptId: record.id
      };
      
      await client.api.prompt.delete_prompt.delete(requestBody);
      messageApi.success('删除成功');
      // 重新获取列表
      fetchPrompts();
    } catch (error) {
      console.error('删除提示词失败:', error);
      messageApi.error('删除失败');
    }
  };

  // 处理查看提示词详情
  const handleView = async (record: PromptItem) => {
    setModalVisible(true);
    setViewingPrompt(null);
    setLoadingDetail(true);

    try {
      const client = GetApiClient();
      const res: PromptItem | undefined = await client.api.prompt.prompt_content.get({
        queryParameters: {
          promptId: record.id || 0
        }
      });

      if (res) {
        setViewingPrompt(res);
        // 等待 vditor 初始化后再设置内容
        setTimeout(() => {
          if (vditorInstance && res.content) {
            vditorInstance.setValue(res.content);
          }
        }, 100);
      } else {
        messageApi.error('获取提示词详情失败');
        setModalVisible(false);
      }
    } catch (error) {
      console.error('获取提示词详情失败:', error);
      messageApi.error('获取提示词详情失败');
      setModalVisible(false);
    } finally {
      setLoadingDetail(false);
    }
  };

  // 关闭查看窗口
  const handleCloseModal = () => {
    if (vditorInstance) {
      vditorInstance.destroy();
      setVditorInstance(null);
    }
    setModalVisible(false);
    setViewingPrompt(null);
  };

  // 初始化 vditor 预览实例
  useEffect(() => {
    if (!modalVisible) return;

    const timer = setTimeout(() => {
      const container = document.getElementById(vditorId.current);
      if (!container) return;

      // 如果已存在实例，先销毁
      if (vditorInstance) {
        vditorInstance.destroy();
        setVditorInstance(null);
      }

      try {
        const vditor = new Vditor(vditorId.current, {
          placeholder: '加载中...',
          height: 600,
          mode: 'ir',
          toolbar: ['preview'],
          preview: {
            delay: 0,
            mode: 'both',
            maxWidth: 800
          },
          theme: 'classic',
          icon: 'material',
          cache: {
            enable: false
          },
          counter: {
            enable: false
          },
          after: () => {
            setVditorInstance(vditor);
            // 设置内容并自动切换到预览模式
            if (viewingPrompt?.content) {
              vditor.setValue(viewingPrompt.content);
              // 自动点击预览按钮切换到预览模式
              setTimeout(() => {
                const previewBtn = document.querySelector(`#${vditorId.current} .vditor-toolbar__item[data-type="preview"]`) as HTMLElement;
                if (previewBtn) {
                  previewBtn.click();
                }
              }, 200);
            }
          },
        });
      } catch (error) {
        console.error('Vditor initialization error:', error);
      }
    }, 100);

    return () => {
      clearTimeout(timer);
    };
  }, [modalVisible]);

  // 当查看的提示词变化时，更新 vditor 内容
  useEffect(() => {
    if (modalVisible && vditorInstance && viewingPrompt?.content) {
      vditorInstance.setValue(viewingPrompt.content);
      // 切换到预览模式
      setTimeout(() => {
        const previewBtn = document.querySelector(`#${vditorId.current} .vditor-toolbar__item[data-type="preview"]`) as HTMLElement;
        if (previewBtn && !previewBtn.classList.contains('vditor-toolbar__item--current')) {
          previewBtn.click();
        }
      }, 100);
    }
  }, [viewingPrompt, modalVisible, vditorInstance]);

  // 清理 vditor 实例
  useEffect(() => {
    return () => {
      if (vditorInstance) {
        vditorInstance.destroy();
        setVditorInstance(null);
      }
    };
  }, []);

  // 表格列定义
  const columns = [
    {
      title: '名称',
      dataIndex: 'name',
      key: 'name',
      render: (text: string) => <strong>{text}</strong>,
    },
    {
      title: '描述',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: '分类',
      dataIndex: 'promptClassId',
      key: 'promptClassId',
      render: (classId: number) => {
        const category = categories.find(c => c.classifyId === classId);
        return category ? <Tag color="blue">{category.name}</Tag> : '-';
      },
    },
    {
      title: '状态',
      dataIndex: 'isPublic',
      key: 'isPublic',
      render: (isPublic: boolean) => (
        <Tag color={isPublic ? 'green' : 'orange'}>
          {isPublic ? '公开' : '私有'}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'createTime',
      key: 'createTime',
      render: (time: string) => new Date(time).toLocaleString(),
    },
    {
      title: '操作',
      key: 'action',
      render: (_: any, record: PromptItem) => (
        <Space size="middle">
          <Button type="link" size="small" onClick={() => handleView(record)}>查看</Button>
          <Button type="link" size="small" onClick={() => navigate(`/app/prompt/${record.id}/edit`)}>编辑</Button>
          <Popconfirm
            title="确认删除"
            description="确定要删除这个提示词吗？"
            onConfirm={() => handleDelete(record)}
            okText="确定"
            cancelText="取消"
          >
            <Button type="link" size="small" danger>删除</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div className="prompt-list-page">
      {contextHolder}
      
      {/* 头部筛选区域 */}
      <Card>
        <Space wrap size="middle">
          {/* 分类筛选 */}
          <Select
            placeholder="选择分类"
            allowClear
            value={selectedCategory}
            onChange={handleCategoryChange}
          >
            {categories.map(category => (
              <Option key={category.classifyId} value={category.classifyId}>
                {category.name}
              </Option>
            ))}
          </Select>

          {/* 搜索框 */}
          <Search
            placeholder="搜索提示词名称"
            onSearch={handleSearch}
            enterButton={<SearchOutlined />}
          />

          {/* 筛选条件下拉框 */}
          <Select
            placeholder="筛选条件"
            value={filterCondition}
            onChange={handleFilterChange}
          >
            {filterOptions.map(option => (
              <Option key={option.value} value={option.value}>
                {option.label}
              </Option>
            ))}
          </Select>

          {/* 新增按钮 */}
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/app/prompt/create')}>
            新增提示词
          </Button>
          
          {/* 刷新按钮 */}
          <Button icon={<ReloadOutlined />} onClick={fetchPrompts} loading={loading}>
            刷新
          </Button>
        </Space>
      </Card>

      {/* 提示词列表 */}
      <Card>
        <Spin spinning={loading}>
          <Table
            columns={columns}
            dataSource={prompts}
            rowKey="id"
            pagination={{
              total: prompts.length,
              pageSize: 10,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条记录`,
            }}
          />
        </Spin>
      </Card>

      {/* 查看详情 Modal */}
      <Modal
        title={
          viewingPrompt ? (
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Title level={4} style={{ margin: 0 }}>{viewingPrompt.name}</Title>
              {viewingPrompt.description && (
                <Text type="secondary" style={{ fontSize: 14 }}>{viewingPrompt.description}</Text>
              )}
            </Space>
          ) : (
            '提示词详情'
          )
        }
        open={modalVisible}
        onCancel={handleCloseModal}
        footer={[
          <Button key="close" onClick={handleCloseModal}>
            关闭
          </Button>
        ]}
        width={1200}
        destroyOnClose
      >
        <Spin spinning={loadingDetail}>
          <div
            id={vditorId.current}
            className="vditor"
          />
        </Spin>
      </Modal>
    </div>
  );
}