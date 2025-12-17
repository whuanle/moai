import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import { Card, Button, Table, Tag, Space, message, Spin, Popconfirm, Modal, Typography, Form, Input, Switch, InputNumber } from 'antd';
import { PlusOutlined, ReloadOutlined, DeleteOutlined, EyeOutlined } from '@ant-design/icons';
import { GetApiClient } from '../../../ServiceClient';
import { 
  QueryWikiCrawlerPluginConfigListCommand,
  QueryWikiCrawlerPluginConfigListCommandResponse,
  WikiCrawlerPluginConfigSimpleItem,
  AddWikiCrawlerConfigCommand,
  DeleteWikiPluginConfigCommand,
  WorkerState,
  WorkerStateObject
} from '../../../../apiClient/models';
import { proxyFormRequestError, proxyRequestError } from '../../../../helper/RequestError';

const { Title, Text } = Typography;
const { TextArea } = Input;

export default function CrawlerListPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  
  const [loading, setLoading] = useState(false);
  const [configs, setConfigs] = useState<WikiCrawlerPluginConfigSimpleItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 创建 Modal 相关状态
  const [modalVisible, setModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  // 获取爬虫配置列表
  const fetchConfigList = async () => {
    if (!wikiId) {
      messageApi.error('缺少知识库ID');
      return;
    }

    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryWikiCrawlerPluginConfigListCommand = {
        wikiId: wikiId,
      };

      const response: QueryWikiCrawlerPluginConfigListCommandResponse | undefined = 
        await client.api.wiki.plugin.crawler.config_list.post(requestBody);
      
      if (response?.items) {
        setConfigs(response.items);
      } else {
        setConfigs([]);
      }
    } catch (error) {
      console.error('获取爬虫配置列表失败:', error);
      proxyRequestError(error, messageApi, '获取爬虫配置列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 页面初始化
  useEffect(() => {
    if (wikiId) {
      fetchConfigList();
    }
  }, [wikiId]);

  // 处理创建配置
  const handleCreate = () => {
    form.resetFields();
    form.setFieldsValue({
      isCrawlOther: false,
      isIgnoreExistPage: false,
      limitMaxCount: 100,
      timeOutSecond: 30,
      userAgent: 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)'
    });
    setModalVisible(true);
  };

  // 处理保存配置
  const handleSave = async () => {
    try {
      await form.validateFields();
      const values = form.getFieldsValue();
      
      setSaving(true);
      const client = GetApiClient();

      // 创建配置
      const addBody: AddWikiCrawlerConfigCommand = {
        wikiId: wikiId || 0,
        title: values.title,
        address: values.address,
        isCrawlOther: values.isCrawlOther || false,
        limitAddress: values.limitAddress,
        limitMaxCount: values.limitMaxCount,
        selector: values.selector,
        timeOutSecond: values.timeOutSecond,
        userAgent: values.userAgent,
        isIgnoreExistPage: values.isIgnoreExistPage || false,
      };

      await client.api.wiki.plugin.crawler.add_config.post(addBody);
      messageApi.success('创建成功');

      setModalVisible(false);
      fetchConfigList();
    } catch (error) {
      console.error('保存配置失败:', error);
      proxyFormRequestError(error, messageApi, form, '保存失败');
    } finally {
      setSaving(false);
    }
  };

  // 处理删除配置
  const handleDelete = async (record: WikiCrawlerPluginConfigSimpleItem) => {
    try {
      const client = GetApiClient();
      const deleteBody: DeleteWikiPluginConfigCommand = {
        configId: record.configId || 0,
        wikiId: wikiId || 0,
        isDeleteDocuments: false, // 默认不删除文档
      };

      await client.api.wiki.plugin.delete_config.delete(deleteBody);
      messageApi.success('删除成功');
      fetchConfigList();
    } catch (error) {
      console.error('删除配置失败:', error);
      proxyRequestError(error, messageApi, '删除失败');
    }
  };

  // 表格列定义
  const columns = [
    {
      title: '标题',
      dataIndex: 'title',
      key: 'title',
      render: (text: string) => <strong>{text}</strong>,
    },
    {
      title: '地址',
      dataIndex: 'address',
      key: 'address',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '状态',
      dataIndex: 'workState',
      key: 'workState',
      width: 120,
      render: (state: WorkerState) => {
        const stateMap: Record<string, { color: string; text: string }> = {
          [WorkerStateObject.None]: { color: 'default', text: '未开始' },
          [WorkerStateObject.Wait]: { color: 'default', text: '等待中' },
          [WorkerStateObject.Processing]: { color: 'processing', text: '处理中' },
          [WorkerStateObject.Successful]: { color: 'success', text: '成功' },
          [WorkerStateObject.Failed]: { color: 'error', text: '失败' },
          [WorkerStateObject.Cancal]: { color: 'default', text: '已取消' },
        };
        const stateInfo = stateMap[state || ''] || { color: 'default', text: '未知' };
        return <Tag color={stateInfo.color}>{stateInfo.text}</Tag>;
      },
    },
    {
      title: '页面数量',
      dataIndex: 'pageCount',
      key: 'pageCount',
      width: 100,
      render: (count: number) => count || 0,
    },
    {
      title: '运行信息',
      dataIndex: 'workMessage',
      key: 'workMessage',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '创建时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString() : '-',
    },
    {
      title: '创建人',
      dataIndex: 'createUserName',
      key: 'createUserName',
      render: (name: string) => name || '-',
    },
    {
      title: '更新时间',
      dataIndex: 'updateTime',
      key: 'updateTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString() : '-',
    },
    {
      title: '更新人',
      dataIndex: 'updateUserName',
      key: 'updateUserName',
      render: (name: string) => name || '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 150,
      render: (_: any, record: WikiCrawlerPluginConfigSimpleItem) => (
        <Space size="middle">
          <Button 
            type="link" 
            size="small" 
            icon={<EyeOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/crawler/${record.configId}`)}
          >
            查看
          </Button>
          <Popconfirm
            title="确认删除"
            description="确定要删除这个爬虫配置吗？"
            onConfirm={() => handleDelete(record)}
            okText="确定"
            cancelText="取消"
          >
            <Button 
              type="link" 
              size="small" 
              danger
              icon={<DeleteOutlined />}
            >
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  if (!wikiId) {
    return (
      <Card>
        <Text type="secondary">缺少知识库ID</Text>
      </Card>
    );
  }

  return (
    <div>
      {contextHolder}
      
      {/* 头部操作区域 */}
      <Card>
        <Space>
          <Button 
            type="primary" 
            icon={<PlusOutlined />} 
            onClick={handleCreate}
          >
            新增爬虫配置
          </Button>
          <Button 
            icon={<ReloadOutlined />} 
            onClick={fetchConfigList} 
            loading={loading}
          >
            刷新
          </Button>
        </Space>
      </Card>

      {/* 配置列表 */}
      <Card style={{ marginTop: 16 }}>
        <Spin spinning={loading}>
          <Table
            columns={columns}
            dataSource={configs}
            rowKey="configId"
            pagination={{
              total: configs.length,
              pageSize: 10,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条记录`,
            }}
          />
        </Spin>
      </Card>

      {/* 创建 Modal */}
      <Modal
        title="新增爬虫配置"
        open={modalVisible}
        onCancel={() => setModalVisible(false)}
        onOk={handleSave}
        confirmLoading={saving}
        width={800}
        destroyOnClose
      >
        <Form
          form={form}
          layout="vertical"
        >
          <Form.Item
            name="title"
            label="标题"
            rules={[{ required: true, message: '请输入标题' }]}
          >
            <Input placeholder="请输入配置标题" />
          </Form.Item>

          <Form.Item
            name="address"
            label="页面地址"
            rules={[{ required: true, message: '请输入页面地址' }]}
          >
            <Input placeholder="请输入要爬取的页面地址" />
          </Form.Item>

          <Form.Item
            name="isCrawlOther"
            label="是否抓取其他页面"
            tooltip="会自动查找这个页面或对应目录下的其它页面"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="isIgnoreExistPage"
            label="是否跳过已爬取页面"
            tooltip="如果开启，将跳过已经爬取过的页面，避免重复爬取"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="limitAddress"
            label="限制地址"
            tooltip="限制自动爬取的网页都在该路径之下，limitAddress跟address必须具有相同域名"
          >
            <Input placeholder="可选：限制爬取的地址范围" />
          </Form.Item>

          <Form.Item
            name="limitMaxCount"
            label="最大抓取数量"
            rules={[{ required: true, message: '请输入最大抓取数量' }]}
          >
            <InputNumber 
              min={1} 
              placeholder="最大抓取数量" 
              style={{ width: '100%' }}
            />
          </Form.Item>

          <Form.Item
            name="selector"
            label="选择器"
            tooltip="CSS选择器，用于定位要抓取的内容"
          >
            <Input placeholder="可选：CSS选择器" />
          </Form.Item>

          <Form.Item
            name="timeOutSecond"
            label="超时时间（秒）"
            rules={[{ required: true, message: '请输入超时时间' }]}
          >
            <InputNumber 
              min={1} 
              placeholder="超时时间（秒）" 
              style={{ width: '100%' }}
            />
          </Form.Item>

          <Form.Item
            name="userAgent"
            label="User Agent"
            tooltip="可选：自定义User Agent"
          >
            <TextArea 
              rows={2} 
              placeholder="可选：自定义User Agent" 
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

