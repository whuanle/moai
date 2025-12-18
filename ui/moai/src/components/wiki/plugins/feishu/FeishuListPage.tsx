import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import { Card, Button, Table, Tag, Space, message, Spin, Popconfirm, Modal, Typography, Form, Input, Switch } from 'antd';
import { PlusOutlined, ReloadOutlined, DeleteOutlined, EyeOutlined } from '@ant-design/icons';
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

const { Title, Text } = Typography;

export default function FeishuListPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  
  const [loading, setLoading] = useState(false);
  const [configs, setConfigs] = useState<WikiFeishuPluginConfigSimpleItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 创建 Modal 相关状态
  const [modalVisible, setModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  // 获取飞书配置列表
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
      
      if (response?.items) {
        setConfigs(response.items);
      } else {
        setConfigs([]);
      }
    } catch (error) {
      console.error('获取飞书配置列表失败:', error);
      proxyRequestError(error, messageApi, '获取飞书配置列表失败');
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
      isOverExistPage: false,
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

  // 处理删除配置
  const handleDelete = async (record: WikiFeishuPluginConfigSimpleItem) => {
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
      render: (_: any, record: WikiFeishuPluginConfigSimpleItem) => (
        <Space size="middle">
          <Button 
            type="link" 
            size="small" 
            icon={<EyeOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/feishu/${record.configId}`)}
          >
            查看
          </Button>
          <Popconfirm
            title="确认删除"
            description="确定要删除这个飞书配置吗？"
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
            新增飞书配置
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
        title="新增飞书配置"
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
            name="appId"
            label="飞书应用ID"
            rules={[{ required: true, message: '请输入飞书应用ID' }]}
          >
            <Input placeholder="请输入飞书应用ID" />
          </Form.Item>

          <Form.Item
            name="appSecret"
            label="飞书应用密钥"
            rules={[{ required: true, message: '请输入飞书应用密钥' }]}
          >
            <Input.Password placeholder="请输入飞书应用密钥" />
          </Form.Item>

          <Form.Item
            name="spaceId"
            label="飞书知识库ID"
            rules={[{ required: true, message: '请输入飞书知识库ID' }]}
          >
            <Input placeholder="请输入飞书知识库ID" />
          </Form.Item>

          <Form.Item
            name="parentNodeToken"
            label="顶部文档Token"
            tooltip="可选：指定从哪个文档节点开始同步，如果不填则同步整个知识库"
          >
            <Input placeholder="可选：顶部文档Token" />
          </Form.Item>

          <Form.Item
            name="isOverExistPage"
            label="是否覆盖已存在的页面"
            tooltip="如果开启，将覆盖已经同步过的页面"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

