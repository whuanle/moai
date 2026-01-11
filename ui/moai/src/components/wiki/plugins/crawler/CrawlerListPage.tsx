import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import {
  Button, Table, Tag, Space, message, Popconfirm, Modal,
  Form, Input, Switch, InputNumber, Row, Col
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, DeleteOutlined, EyeOutlined
} from '@ant-design/icons';
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
import '../../../../styles/theme.css';
import { formatDateTimeStandard } from '../../../../helper/DateTimeHelper';

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

  useEffect(() => {
    if (wikiId) {
      fetchConfigList();
    }
  }, [wikiId]);

  const handleCreate = () => {
    form.resetFields();
    form.setFieldsValue({
      isCrawlOther: false,
      isOverExistPage: false,
      limitMaxCount: 100,
      timeOutSecond: 30,
      userAgent: 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)'
    });
    setModalVisible(true);
  };

  const handleSave = async () => {
    try {
      await form.validateFields();
      const values = form.getFieldsValue();

      setSaving(true);
      const client = GetApiClient();

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
        isOverExistPage: values.isOverExistPage || false,
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

  const handleDelete = async (record: WikiCrawlerPluginConfigSimpleItem) => {
    try {
      const client = GetApiClient();
      const deleteBody: DeleteWikiPluginConfigCommand = {
        configId: record.configId || 0,
        wikiId: wikiId || 0,
        isDeleteDocuments: false,
      };

      await client.api.wiki.plugin.delete_config.delete(deleteBody);
      messageApi.success('删除成功');
      fetchConfigList();
    } catch (error) {
      console.error('删除配置失败:', error);
      proxyRequestError(error, messageApi, '删除失败');
    }
  };

  // 状态渲染
  const renderStatus = (state: WorkerState) => {
    const stateMap: Record<string, { color: string; text: string }> = {
      [WorkerStateObject.None]: { color: 'default', text: '未开始' },
      [WorkerStateObject.Wait]: { color: 'gold', text: '等待中' },
      [WorkerStateObject.Processing]: { color: 'processing', text: '处理中' },
      [WorkerStateObject.Successful]: { color: 'success', text: '成功' },
      [WorkerStateObject.Failed]: { color: 'error', text: '失败' },
      [WorkerStateObject.Cancal]: { color: 'default', text: '已取消' },
    };
    const stateInfo = stateMap[state || ''] || { color: 'default', text: '未知' };
    return <Tag color={stateInfo.color}>{stateInfo.text}</Tag>;
  };

  // 表格列定义
  const columns = [
    {
      title: '配置名称',
      dataIndex: 'title',
      key: 'title',
      width: 160,
      ellipsis: true,
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
      width: 150,
      align: 'center' as const,
      render: renderStatus,
    },
    {
      title: '页面数',
      dataIndex: 'pageCount',
      key: 'pageCount',
      width: 80,
      align: 'center' as const,
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
      title: '创建人',
      dataIndex: 'createUserName',
      key: 'createUserName',
      width: 150,
      render: (text: string) => text || '-',
    },
    {
      title: '更新时间',
      dataIndex: 'updateTime',
      key: 'updateTime',
      width: 200,
      render: (text: string) => formatDateTimeStandard(text),
    },
    {
      title: '更新人',
      dataIndex: 'updateUserName',
      key: 'updateUserName',
      width: 150,
      render: (text: string) => text || '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 200,
      fixed: 'right' as const,
      render: (_: unknown, record: WikiCrawlerPluginConfigSimpleItem) => (
        <Space size="middle">
          <Button
            type="text"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/crawler/${record.configId}`)}
          >
            详情
          </Button>
          <Popconfirm
            title="确认删除"
            description="删除后不可恢复，确定要删除吗？"
            onConfirm={() => handleDelete(record)}
            okText="确定"
            cancelText="取消"
            placement="topRight"
          >
            <Button
              type="text"
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
      <div className="moai-empty">
        <span style={{ color: '#999' }}>缺少知识库ID</span>
      </div>
    );
  }

  return (
    <div className="page-container">
      {contextHolder}

      {/* 页面标题区域 */}
      <div className="moai-page-header">
        <h1 className="moai-page-title">网页爬虫配置</h1>
        <p className="moai-page-subtitle">管理知识库的网页爬虫任务，自动抓取网页内容并导入知识库</p>
      </div>

      {/* 工具栏 */}
      <div className="moai-toolbar">
        <div className="moai-toolbar-left">
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleCreate}
          >
            新增爬虫配置
          </Button>
        </div>
        <div className="moai-toolbar-right">
          <Button
            icon={<ReloadOutlined />}
            onClick={fetchConfigList}
            loading={loading}
          >
            刷新
          </Button>
        </div>
      </div>

      {/* 配置列表 */}
      <Table
        columns={columns}
        dataSource={configs}
        rowKey="configId"
        loading={loading}
        scroll={{ x: 1100 }}
        pagination={false}
      />

      {/* 创建 Modal */}
      <Modal
        title="新增爬虫配置"
        open={modalVisible}
        onCancel={() => setModalVisible(false)}
        onOk={handleSave}
        confirmLoading={saving}
        width={720}
        destroyOnClose
        maskClosable={false}
        closable={false}
      >
        <Form
          form={form}
          layout="vertical"
          style={{ marginTop: 16 }}
        >
          <Form.Item
            name="title"
            label="配置名称"
            rules={[{ required: true, message: '请输入配置名称' }]}
          >
            <Input placeholder="请输入配置名称，便于识别" />
          </Form.Item>

          <Form.Item
            name="address"
            label="目标网址"
            rules={[{ required: true, message: '请输入目标网址' }]}
          >
            <Input placeholder="请输入要爬取的网页地址，如 https://example.com/docs" />
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="isCrawlOther"
                label="抓取关联页面"
                tooltip="开启后会自动查找并抓取该页面链接的其他页面"
                valuePropName="checked"
              >
                <Switch checkedChildren="开启" unCheckedChildren="关闭" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="isOverExistPage"
                label="覆盖已有页面"
                tooltip="开启后会覆盖已经爬取过的相同页面"
                valuePropName="checked"
              >
                <Switch checkedChildren="开启" unCheckedChildren="关闭" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="limitAddress"
            label="限制地址范围"
            tooltip="限制自动爬取的网页都在该路径之下，需与目标网址具有相同域名"
          >
            <Input placeholder="可选，如 https://example.com/docs/" />
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="limitMaxCount"
                label="最大抓取数量"
                rules={[{ required: true, message: '请输入最大抓取数量' }]}
              >
                <InputNumber
                  min={1}
                  max={10000}
                  placeholder="最大抓取页面数"
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="timeOutSecond"
                label="超时时间（秒）"
                rules={[{ required: true, message: '请输入超时时间' }]}
              >
                <InputNumber
                  min={5}
                  max={300}
                  placeholder="单页超时时间"
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="selector"
            label="CSS 选择器"
            tooltip="用于定位要抓取的内容区域，如 article、.content 等"
          >
            <Input placeholder="可选，如 article、.main-content" />
          </Form.Item>

          <Form.Item
            name="userAgent"
            label="User Agent"
            tooltip="自定义请求的 User Agent 标识"
          >
            <TextArea
              rows={2}
              placeholder="可选，自定义 User Agent"
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
