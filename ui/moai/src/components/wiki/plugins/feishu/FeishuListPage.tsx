import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import {
  Button, Table, Tag, Space, message, Popconfirm, Modal,
  Form, Input, Switch, Row, Col
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, DeleteOutlined, EyeOutlined
} from '@ant-design/icons';
import { GetApiClient } from '../../../ServiceClient';
import {
  QueryWikiFeishuPluginConfigListCommand,
  QueryWikiFeishuPluginConfigListCommandResponse,
  WikiFeishuPluginConfigSimpleItem,
  AddWikiFeishuConfigCommand,
  DeleteWikiPluginConfigCommand,
  WorkerState,
  WorkerStateObject
} from '../../../../apiClient/models';
import { proxyFormRequestError, proxyRequestError } from '../../../../helper/RequestError';
import { formatDateTimeStandard } from '../../../../helper/DateTimeHelper';
import '../../../../styles/theme.css';

export default function FeishuListPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;

  const [loading, setLoading] = useState(false);
  const [configs, setConfigs] = useState<WikiFeishuPluginConfigSimpleItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  const [modalVisible, setModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const fetchConfigList = async () => {
    if (!wikiId) {
      messageApi.error('缺少知识库ID');
      return;
    }

    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryWikiFeishuPluginConfigListCommand = {
        wikiId: wikiId,
      };

      const response: QueryWikiFeishuPluginConfigListCommandResponse | undefined =
        await client.api.wiki.plugin.feishu.config_list.post(requestBody);

      setConfigs(response?.items || []);
    } catch (error) {
      console.error('获取飞书配置列表失败:', error);
      proxyRequestError(error, messageApi, '获取飞书配置列表失败');
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
    form.setFieldsValue({ isOverExistPage: false });
    setModalVisible(true);
  };

  const handleSave = async () => {
    try {
      await form.validateFields();
      const values = form.getFieldsValue();

      setSaving(true);
      const client = GetApiClient();

      const addBody: AddWikiFeishuConfigCommand = {
        wikiId: wikiId || 0,
        title: values.title,
        appId: values.appId,
        appSecret: values.appSecret,
        spaceId: values.spaceId,
        parentNodeToken: values.parentNodeToken,
        isOverExistPage: values.isOverExistPage || false,
      };

      await client.api.wiki.plugin.feishu.add_config.post(addBody);
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

  const handleDelete = async (record: WikiFeishuPluginConfigSimpleItem) => {
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

  const columns = [
    {
      title: '配置名称',
      dataIndex: 'title',
      key: 'title',
      width: 160,
      ellipsis: true,
    },
    {
      title: '飞书知识库ID',
      dataIndex: 'spaceId',
      key: 'spaceId',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '顶部文档Token',
      dataIndex: 'parentNodeToken',
      key: 'parentNodeToken',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '状态',
      dataIndex: 'workState',
      key: 'workState',
      width: 100,
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
      width: 100,
      render: (text: string) => text || '-',
    },
    {
      title: '更新时间',
      dataIndex: 'updateTime',
      key: 'updateTime',
      width: 180,
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
      render: (_: unknown, record: WikiFeishuPluginConfigSimpleItem) => (
        <Space size="middle">
          <Button
            type="text"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/feishu/${record.configId}`)}
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

      <div className="moai-page-header">
        <h1 className="moai-page-title">飞书知识库配置</h1>
        <p className="moai-page-subtitle">管理飞书知识库同步任务，自动导入飞书文档到知识库</p>
      </div>

      <div className="moai-toolbar">
        <div className="moai-toolbar-left">
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleCreate}
          >
            新增飞书配置
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

      <Table
        columns={columns}
        dataSource={configs}
        rowKey="configId"
        loading={loading}
        scroll={{ x: 1100 }}
        pagination={false}
      />

      <Modal
        title="新增飞书配置"
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

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="appId"
                label="飞书应用ID"
                rules={[{ required: true, message: '请输入飞书应用ID' }]}
              >
                <Input placeholder="请输入飞书应用ID" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="appSecret"
                label="飞书应用密钥"
                rules={[{ required: true, message: '请输入飞书应用密钥' }]}
              >
                <Input.Password placeholder="请输入飞书应用密钥" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="spaceId"
                label="飞书知识库ID"
                rules={[{ required: true, message: '请输入飞书知识库ID' }]}
              >
                <Input placeholder="请输入飞书知识库ID" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="parentNodeToken"
                label="顶部文档Token"
                tooltip="可选：指定从哪个文档节点开始同步"
              >
                <Input placeholder="可选，不填则同步整个知识库" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="isOverExistPage"
            label="覆盖已有页面"
            tooltip="开启后会覆盖已经同步过的相同页面"
            valuePropName="checked"
          >
            <Switch checkedChildren="开启" unCheckedChildren="关闭" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
