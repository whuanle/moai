import React, { useState, useEffect } from 'react';
import { Card, Table, Button, Spin, message, Collapse, Tag, Space, Typography, Modal, Form, Input, Select, InputNumber, Switch, Row, Col, Popconfirm } from 'antd';
import { GetApiClient } from '../ServiceClient';
import { 
  QueryAiModelProviderListResponse, 
  QueryAiModelProviderCount,
  QueryAiModelListCommandResponse,
  AiNotKeyEndpoint,
  AiModelType,
  AiProvider,
  AddAiModelCommand,
  UpdateAiModelCommand,
  ModelAbilities,
  QueryUserAiModelListCommand
} from '../../apiClient/models';
import { DEFAULT_MODEL_PROVIDER_LIST } from '../../lobechat/types/modelProviders';
import { RsaHelper } from '../../helper/RsaHalper';
import { LOBE_DEFAULT_MODEL_LIST } from '../../lobechat/types/aiModels';
import { proxyFormRequestError } from "../../helper/RequestError";
import { GetServiceInfo } from '../../InitService';

const { Title, Text, Paragraph } = Typography;
const { Panel } = Collapse;
const { Option } = Select;

interface ProviderWithModels extends QueryAiModelProviderCount {
  models?: AiNotKeyEndpoint[];
  loading?: boolean;
}

// 获取服务商描述信息
const getProviderDescription = (providerId: string): string => {
  if (providerId === 'custom') {
    return '自定义服务商，支持用户自定义配置的AI模型。';
  }
  
  const provider = DEFAULT_MODEL_PROVIDER_LIST.find(p => p.id === providerId);
  return provider?.description || '暂无描述信息';
};

// 获取服务商显示名称
const getProviderDisplayName = (providerId: string): string => {
  if (providerId === 'custom') {
    return '自定义';
  }
  
  const provider = DEFAULT_MODEL_PROVIDER_LIST.find(p => p.id === providerId);
  return provider?.name || providerId;
};

// 获取模型类型对应的模型列表
const getModelListByType = (modelType: AiModelType) => {
  // 从 LOBE_DEFAULT_MODEL_LIST 中筛选对应类型的模型，不限制供应商
  return LOBE_DEFAULT_MODEL_LIST.filter(model => model.type === modelType);
};

// 检查模型名称是否已存在
const isModelNameExists = (providerId: string, modelName: string, providers: ProviderWithModels[]) => {
  const provider = providers.find(p => p.provider === providerId);
  if (!provider?.models) return false;
  
  return provider.models.some((model: AiNotKeyEndpoint) => model.name === modelName);
};

// 生成唯一的模型名称
const generateUniqueModelName = (providerId: string, baseName: string, providers: ProviderWithModels[]) => {
  let name = baseName;
  let counter = 1;
  
  while (isModelNameExists(providerId, name, providers)) {
    name = `${baseName}_${counter}`;
    counter++;
  }
  
  return name;
};

export default function AIModel() {
  const [providers, setProviders] = useState<ProviderWithModels[]>([]);
  const [loading, setLoading] = useState(false);
  const [addModalVisible, setAddModalVisible] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [currentProvider, setCurrentProvider] = useState<string>('');
  const [currentModel, setCurrentModel] = useState<AiNotKeyEndpoint | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [modelTypeKey, setModelTypeKey] = useState<string>('chat');
  const [messageApi, contextHolder] = message.useMessage();

  // 获取服务商列表
  const fetchProviders = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.aimodel.user_providerlist.get();
      if (response?.providers) {
        // 只保留 provider 字段有效的项
        setProviders(
          response.providers
            .filter(provider => typeof provider.provider === 'string' && provider.provider.trim() !== '')
            .map(provider => ({
              ...provider,
              models: [],
              loading: false
            }))
        );
      }
    } catch (error) {
      console.error('获取服务商列表失败:', error);
      messageApi.error('获取服务商列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 获取指定服务商的模型列表
  const fetchModels = async (provider: string, index: number) => {
    setProviders(prev => prev.map((p, i) => 
      i === index ? { ...p, loading: true } : p
    ));

    try {
      const client = GetApiClient();
      const requestBody: QueryUserAiModelListCommand = {
        provider: provider as AiProvider
      };
      
      const response = await client.api.aimodel.type.user_modellist.post(requestBody);
      
      setProviders(prev => prev.map((p, i) => 
        i === index ? { 
          ...p, 
          models: response?.aiModels || [],
          loading: false 
        } : p
      ));
    } catch (error) {
      console.error('获取模型列表失败:', error);
      messageApi.error('获取模型列表失败');
      setProviders(prev => prev.map((p, i) => 
        i === index ? { ...p, loading: false } : p
      ));
    }
  };

  // 处理服务商点击事件
  const handleProviderClick = (provider: string, index: number) => {
    const currentProvider = providers[index];
    if (!currentProvider.models || currentProvider.models.length === 0) {
      fetchModels(provider, index);
    }
  };

  // 处理新增按钮点击
  const handleAddClick = (provider: string) => {
    setCurrentProvider(provider);
    form.resetFields();
    form.setFieldsValue({
      provider: provider,
      aiModelType: 'chat'
    });
    setAddModalVisible(true);
  };

  // 处理编辑按钮点击
  const handleEditClick = (model: AiNotKeyEndpoint, provider: string) => {
    setCurrentModel(model);
    setCurrentProvider(provider);
    editForm.resetFields();
    editForm.setFieldsValue({
      aiModelId: model.id,
      name: model.name,
      title: model.title,
      provider: provider,
      aiModelType: model.aiModelType,
      endpoint: model.endpoint,
      deploymentName: model.deploymentName,
      contextWindowTokens: model.contextWindowTokens,
      textOutput: model.textOutput,
      maxDimension: model.maxDimension,
      vision: model.abilities?.vision || false,
      functionCall: model.abilities?.functionCall || false,
      files: model.abilities?.files || false,
      imageOutput: model.abilities?.imageOutput || false
    });
    setEditModalVisible(true);
  };

  // 处理删除按钮点击
  const handleDeleteClick = async (model: AiNotKeyEndpoint, provider: string) => {
    try {
      const client = GetApiClient();
      await client.api.aimodel.delete_model.delete({ aiModelId: model.id });
      messageApi.success('模型删除成功');
      
      // 刷新当前服务商的模型列表
      const providerIndex = providers.findIndex(p => p.provider === provider);
      if (providerIndex !== -1) {
        await fetchModels(provider, providerIndex);
      }
    } catch (error) {
      console.error('删除模型失败:', error);
      messageApi.error('删除模型失败');
    }
  };

  // 处理模型类型变化
  const handleModelTypeChange = (modelType: AiModelType) => {
    // 清空快速配置选择和相关字段
    form.setFieldsValue({
      quickConfig: undefined,
      name: '',
      title: '',
      deploymentName: '',
      contextWindowTokens: undefined,
      textOutput: undefined,
      maxDimension: undefined
    });
    
    // 更新模型类型key，触发快速配置下拉框重新渲染
    setModelTypeKey(modelType);
  };

  // 处理快速配置选择
  const handleQuickConfigChange = (modelId: string) => {
    const modelType = form.getFieldValue('aiModelType');
    const modelList = getModelListByType(modelType);
    const selectedModel = modelList.find(model => model.id === modelId);
    
    if (selectedModel) {
      const uniqueName = generateUniqueModelName(currentProvider, selectedModel.id, providers);
      
      form.setFieldsValue({
        name: uniqueName,
        title: selectedModel.displayName || selectedModel.id,
        deploymentName: selectedModel.id,
        contextWindowTokens: selectedModel.contextWindowTokens,
        textOutput: undefined, // LobeDefaultAiModelListItem 没有 maxOutput 属性
        maxDimension: selectedModel.maxDimension,
        // 设置模型能力
        vision: selectedModel.abilities?.vision || false,
        functionCall: selectedModel.abilities?.functionCall || false,
        files: selectedModel.abilities?.files || false,
        imageOutput: selectedModel.abilities?.imageOutput || false
      });
    }
  };

  // 处理新增模型提交
  const handleAddSubmit = async (values: any) => {
    setSubmitting(true);
    try {
      // 获取服务器信息并加密API密钥
      const serviceInfo = await GetServiceInfo();
      const encryptedKey = RsaHelper.encrypt(
        serviceInfo.rsaPublic,
        values.key
      );

      const client = GetApiClient();
      
      // 构建请求参数
      const requestBody: AddAiModelCommand = {
        name: values.name,
        title: values.title,
        provider: values.provider as AiProvider,
        aiModelType: values.aiModelType as AiModelType,
        endpoint: values.endpoint,
        key: encryptedKey, // 使用加密后的API密钥
        deploymentName: values.deploymentName,
        contextWindowTokens: values.contextWindowTokens,
        textOutput: values.textOutput,
        maxDimension: values.maxDimension,
        abilities: {
          vision: values.vision || false,
          functionCall: values.functionCall || false,
          files: values.files || false,
          imageOutput: values.imageOutput || false
        } as ModelAbilities
      };

      const response = await client.api.aimodel.add.post(requestBody);
      
      if (response?.value) {
        messageApi.success('模型添加成功');
        setAddModalVisible(false);
        
        // 刷新当前服务商的模型列表
        const providerIndex = providers.findIndex(p => p.provider === currentProvider);
        if (providerIndex !== -1) {
          // 立即刷新模型列表
          await fetchModels(currentProvider, providerIndex);
        }
      }
    } catch (error) {
      console.error('添加模型失败:', error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitting(false);
    }
  };

  // 处理编辑模型提交
  const handleEditSubmit = async (values: any) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();
      
      // 构建请求参数
      const requestBody: UpdateAiModelCommand = {
        aiModelId: values.aiModelId,
        name: values.name,
        title: values.title,
        provider: values.provider as AiProvider,
        aiModelType: values.aiModelType as AiModelType,
        endpoint: values.endpoint,
        deploymentName: values.deploymentName,
        contextWindowTokens: values.contextWindowTokens,
        textOutput: values.textOutput,
        maxDimension: values.maxDimension,
        abilities: {
          vision: values.vision || false,
          functionCall: values.functionCall || false,
          files: values.files || false,
          imageOutput: values.imageOutput || false
        } as ModelAbilities
      };

      // 如果用户输入了API密钥，则加密并添加到请求中
      if (values.key && values.key.trim() !== '') {
        const serviceInfo = await GetServiceInfo();
        const encryptedKey = RsaHelper.encrypt(
          serviceInfo.rsaPublic,
          values.key
        );
        requestBody.key = encryptedKey;
      } else {
        // 如果用户没有输入API密钥，则设置为空字符串
        requestBody.key = '*';
      }

      const response = await client.api.aimodel.update.post(requestBody);
      
      if (response) {
        messageApi.success('模型更新成功');
        setEditModalVisible(false);
        
        // 刷新当前服务商的模型列表
        const providerIndex = providers.findIndex(p => p.provider === currentProvider);
        if (providerIndex !== -1) {
          await fetchModels(currentProvider, providerIndex);
        }
      }
    } catch (error) {
      console.error('更新模型失败:', error);
      proxyFormRequestError(error, messageApi, editForm);
    } finally {
      setSubmitting(false);
    }
  };

  // 获取模型类型标签颜色
  const getModelTypeColor = (type: AiModelType | null) => {
    switch (type) {
      case 'chat':
        return 'blue';
      case 'embedding':
        return 'green';
      case 'image':
        return 'purple';
      case 'tts':
        return 'orange';
      case 'stts':
        return 'cyan';
      case 'realtime':
        return 'magenta';
      case 'text2video':
        return 'volcano';
      case 'text2music':
        return 'geekblue';
      default:
        return 'default';
    }
  };

  // 获取能力标签
  const getAbilityTags = (abilities: any) => {
    if (!abilities) return [];
    
    const tags = [];
    if (abilities.vision) tags.push({ text: '视觉', color: 'cyan' });
    if (abilities.functionCall) tags.push({ text: '函数调用', color: 'magenta' });
    if (abilities.files) tags.push({ text: '文件上传', color: 'geekblue' });
    if (abilities.imageOutput) tags.push({ text: '图像输出', color: 'volcano' });
    
    return tags;
  };

  // 模型列表表格列定义
  const modelColumns = [
    {
      title: '模型名称',
      dataIndex: 'name',
      key: 'name',
      render: (text: string) => <Text strong>{text}</Text>
    },
    {
      title: '显示名称',
      dataIndex: 'title',
      key: 'title',
    },
    {
      title: '模型类型',
      dataIndex: 'aiModelType',
      key: 'aiModelType',
      render: (type: AiModelType) => (
        <Tag color={getModelTypeColor(type)}>
          {type}
        </Tag>
      )
    },
    {
      title: '上下文窗口',
      dataIndex: 'contextWindowTokens',
      key: 'contextWindowTokens',
      render: (value: number) => value ? `${value.toLocaleString()}` : '-'
    },
    {
      title: '输出限制',
      dataIndex: 'textOutput',
      key: 'textOutput',
      render: (value: number) => value ? `${value.toLocaleString()}` : '-'
    },
    {
      title: '模型能力',
      dataIndex: 'abilities',
      key: 'abilities',
      render: (abilities: any) => (
        <Space wrap>
          {getAbilityTags(abilities).map((tag, index) => (
            <Tag key={index} color={tag.color}>{tag.text}</Tag>
          ))}
        </Space>
      )
    },
    {
      title: '部署名称',
      dataIndex: 'deploymentName',
      key: 'deploymentName',
    },
    {
      title: '端点',
      dataIndex: 'endpoint',
      key: 'endpoint',
      render: (text: string) => (
        <Text code style={{ fontSize: '12px' }}>
          {text ? (text.length > 30 ? `${text.substring(0, 30)}...` : text) : '-'}
        </Text>
      )
    },
    {
      title: '操作',
      key: 'action',
      width: 150,
      render: (a: any, record: AiNotKeyEndpoint, index: number) => {
        // 从当前展开的服务商中获取provider信息
        const currentExpandedProvider = providers.find(p => p.models && p.models.length > 0);
        const providerId = currentExpandedProvider?.provider || '';
        
        return (
          <Space size="small">
            <Button
              type="link"
              size="small"
              onClick={() => handleEditClick(record, providerId)}
            >
              修改
            </Button>
            <Popconfirm
              title="确定要删除这个模型吗？"
              onConfirm={() => handleDeleteClick(record, providerId)}
              okText="确定"
              cancelText="取消"
            >
              <Button type="link" danger size="small">
                删除
              </Button>
            </Popconfirm>
          </Space>
        );
      }
    }
  ];

  useEffect(() => {
    fetchProviders();
  }, []);

  return (
    <>
    {contextHolder}
    <div style={{ paddingLeft: '24px' }}>
      <Title level={2}>个人 AI 模型管理</Title>
      
      {loading ? (
        <div style={{ textAlign: 'center', padding: '50px' }}>
          <Spin size="large" />
          <div style={{ marginTop: '16px' }}>加载服务商列表...</div>
        </div>
      ) : (
        <Collapse 
          accordion 
          style={{ background: 'transparent' }}
          expandIconPosition="end"
        >
          {providers.map((provider, index) => (
            <Panel
              key={provider.provider || index}
              header={
                <div 
                  style={{ 
                    display: 'flex', 
                    justifyContent: 'space-between', 
                    alignItems: 'center',
                    width: '100%'
                  }}
                  onClick={() => handleProviderClick(provider.provider || '', index)}
                >
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', marginBottom: '4px' }}>
                      <Text strong style={{ fontSize: '16px' }}>
                        {getProviderDisplayName(provider.provider || '')}
                      </Text>
                      <Tag color="blue" style={{ marginLeft: '8px' }}>
                        {provider.count} 个模型
                      </Tag>
                    </div>
                    <Paragraph 
                      style={{ 
                        margin: 0, 
                        fontSize: '12px', 
                        color: '#666',
                        lineHeight: '1.4'
                      }}
                      ellipsis={{ rows: 2, tooltip: getProviderDescription(provider.provider || '') }}
                    >
                      {getProviderDescription(provider.provider || '')}
                    </Paragraph>
                  </div>
                  {provider.loading && <Spin size="small" />}
                </div>
              }
            >
              {provider.models && provider.models.length > 0 ? (
                <>
                  <div style={{ marginBottom: '16px', textAlign: 'right' }}>
                    <Button 
                      type="primary" 
                      onClick={() => handleAddClick(provider.provider || '')}
                    >
                      新增模型
                    </Button>
                  </div>
                  <Table
                    dataSource={provider.models}
                    columns={modelColumns.map(col => {
                      if (col.key === 'action') {
                        return {
                          ...col,
                          render: (_, record: AiNotKeyEndpoint) => (
                            <Space size="small">
                              <Button
                                type="link"
                                size="small"
                                onClick={() => handleEditClick(record, provider.provider || '')}
                              >
                                修改
                              </Button>
                              <Popconfirm
                                title="确定要删除这个模型吗？"
                                onConfirm={() => handleDeleteClick(record, provider.provider || '')}
                                okText="确定"
                                cancelText="取消"
                              >
                                <Button type="link" danger size="small">
                                  删除
                                </Button>
                              </Popconfirm>
                            </Space>
                          )
                        };
                      }
                      return col;
                    })}
                    rowKey={(record) => record.id?.toString() || record.name || ''}
                    pagination={false}
                    scroll={{ x: 1200 }}
                  />
                </>
              ) : provider.loading ? (
                <div style={{ textAlign: 'center', padding: '20px' }}>
                  <Spin />
                  <div style={{ marginTop: '8px' }}>加载模型列表...</div>
                </div>
              ) : (
                <div style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                  <div>暂无模型数据</div>
                  <Button 
                    type="primary" 
                    style={{ marginTop: '8px' }}
                    onClick={() => handleAddClick(provider.provider || '')}
                  >
                    新增模型
                  </Button>
                </div>
              )}
            </Panel>
          ))}
        </Collapse>
      )}

      {/* 新增模型模态窗口 */}
      <Modal
        title="新增模型"
        open={addModalVisible}
        onCancel={() => setAddModalVisible(false)}
        footer={null}
        width={800}
        maskClosable={false}
        keyboard={false}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleAddSubmit}
        >
          {/* 第一行：模型类型和快速配置 */}
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="aiModelType"
                label="模型类型"
                rules={[{ required: true, message: '请选择模型类型' }]}
              >
                <Select 
                  placeholder="请选择模型类型"
                  onChange={handleModelTypeChange}
                >
                  <Option value="chat">聊天</Option>
                  <Option value="embedding">嵌入</Option>
                  <Option value="image">图像</Option>
                  <Option value="tts">语音合成</Option>
                  <Option value="stts">语音转文字</Option>
                  <Option value="realtime">实时</Option>
                  <Option value="text2video">文本转视频</Option>
                  <Option value="text2music">文本转音乐</Option>
                </Select>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="quickConfig"
                label="快速配置"
              >
                <Form.Item
                  noStyle
                  shouldUpdate={(prevValues, currentValues) => 
                    prevValues.aiModelType !== currentValues.aiModelType
                  }
                >
                  {({ getFieldValue }) => (
                    <Select 
                      key={modelTypeKey}
                      placeholder="选择预设模型快速配置"
                      onChange={handleQuickConfigChange}
                      allowClear
                      showSearch
                      filterOption={(input, option) => {
                        if (!option?.children) return false;
                        const searchText = input.toLowerCase();
                        const optionText = option.children.toString().toLowerCase();
                        return optionText.includes(searchText);
                      }}
                      optionFilterProp="children"
                    >
                      {(() => {
                        const modelType = getFieldValue('aiModelType');
                        const modelList = getModelListByType(modelType);
                        
                        // 更严格的去重：按显示名称和ID双重去重
                        const uniqueModels = modelList.filter((model, index, self) => {
                          const currentDisplayName = model.displayName || model.id;
                          const currentId = model.id;
                          
                          // 检查是否已经存在相同的显示名称或ID
                          const isDuplicate = self.findIndex(m => {
                            const existingDisplayName = m.displayName || m.id;
                            return (existingDisplayName === currentDisplayName || m.id === currentId) && 
                                   self.indexOf(m) < index;
                          }) !== -1;
                          
                          return !isDuplicate;
                        });
                        
                        const sortedModels = uniqueModels.sort((a, b) => {
                          const nameA = (a.displayName || a.id).toLowerCase();
                          const nameB = (b.displayName || b.id).toLowerCase();
                          return nameA.localeCompare(nameB);
                        });
                        
                        return sortedModels.map(model => (
                          <Option key={model.id} value={model.id}>
                            {model.displayName || model.id}
                          </Option>
                        ));
                      })()}
                    </Select>
                  )}
                </Form.Item>
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="name"
                label="模型名称"
                rules={[{ required: true, message: '请输入模型名称' }]}
              >
                <Input placeholder="请输入模型名称" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="title"
                label="显示名称"
                rules={[{ required: true, message: '请输入显示名称' }]}
                extra="* 仅用于显示，以便区分用途"
              >
                <Input placeholder="请输入显示名称" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="provider"
                label="服务商"
                rules={[{ required: true, message: '请选择服务商' }]}
              >
                <Select placeholder="请选择服务商" disabled>
                  <Option value={currentProvider}>{getProviderDisplayName(currentProvider)}</Option>
                </Select>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="endpoint"
                label="请求端点"
                rules={[{ required: true, message: '请输入请求端点' }]}
              >
                <Input placeholder="请输入请求端点" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="key"
                label="API密钥"
                rules={[{ required: true, message: '请输入API密钥' }]}
              >
                <Input.Password placeholder="请输入API密钥" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="deploymentName"
                label="部署名称"
                rules={[{ required: true, message: '请输入部署名称' }]}
                extra="* azure需要填写"
              >
                <Input placeholder="请输入部署名称" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="contextWindowTokens"
                label="上下文窗口"
              >
                <InputNumber 
                  placeholder="请输入上下文窗口大小" 
                  style={{ width: '100%' }}
                  min={1}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="textOutput"
                label="输出限制"
              >
                <InputNumber 
                  placeholder="请输入输出限制" 
                  style={{ width: '100%' }}
                  min={1}
                />
              </Form.Item>
            </Col>
          </Row>

          {/* 向量维度字段，只在嵌入模型时显示 */}
          <Form.Item
            noStyle
            shouldUpdate={(prevValues, currentValues) => 
              prevValues.aiModelType !== currentValues.aiModelType
            }
          >
            {({ getFieldValue }) => {
              const modelType = getFieldValue('aiModelType');
              if (modelType === 'embedding') {
                return (
                  <Row gutter={16}>
                    <Col span={12}>
                      <Form.Item
                        name="maxDimension"
                        label="向量维度"
                        rules={[{ required: true, message: '请输入向量维度' }]}
                      >
                        <InputNumber 
                          placeholder="请输入向量维度" 
                          style={{ width: '100%' }}
                          min={1}
                        />
                      </Form.Item>
                    </Col>
                  </Row>
                );
              }
              return null;
            }}
          </Form.Item>

          <Form.Item label="模型能力">
            <Row gutter={16}>
              <Col span={6}>
                <Form.Item name="vision" valuePropName="checked">
                  <Switch checkedChildren="视觉" unCheckedChildren="视觉" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="functionCall" valuePropName="checked">
                  <Switch checkedChildren="函数调用" unCheckedChildren="函数调用" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="files" valuePropName="checked">
                  <Switch checkedChildren="文件上传" unCheckedChildren="文件上传" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="imageOutput" valuePropName="checked">
                  <Switch checkedChildren="图像输出" unCheckedChildren="图像输出" />
                </Form.Item>
              </Col>
            </Row>
          </Form.Item>

          <Form.Item style={{ marginTop: '24px', textAlign: 'right' }}>
            <Space>
              <Button onClick={() => setAddModalVisible(false)}>
                取消
              </Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                确定
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 编辑模型模态窗口 */}
      <Modal
        title="编辑模型"
        open={editModalVisible}
        onCancel={() => setEditModalVisible(false)}
        footer={null}
        width={800}
        maskClosable={false}
        keyboard={false}
      >
        <Form
          form={editForm}
          layout="vertical"
          onFinish={handleEditSubmit}
        >
          <Form.Item name="aiModelId" hidden>
            <Input />
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="aiModelType"
                label="模型类型"
                rules={[{ required: true, message: '请选择模型类型' }]}
              >
                <Select 
                  placeholder="请选择模型类型"
                  disabled
                >
                  <Option value="chat">聊天</Option>
                  <Option value="embedding">嵌入</Option>
                  <Option value="image">图像</Option>
                  <Option value="tts">语音合成</Option>
                  <Option value="stt">语音转文字</Option>
                  <Option value="realtime">实时</Option>
                  <Option value="text2video">文本转视频</Option>
                  <Option value="text2music">文本转音乐</Option>
                </Select>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="provider"
                label="服务商"
                rules={[{ required: true, message: '请选择服务商' }]}
              >
                <Select placeholder="请选择服务商" disabled>
                  <Option value={currentProvider}>{getProviderDisplayName(currentProvider)}</Option>
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="name"
                label="模型名称"
                rules={[{ required: true, message: '请输入模型名称' }]}
              >
                <Input placeholder="请输入模型名称" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="title"
                label="显示名称"
                rules={[{ required: true, message: '请输入显示名称' }]}
                extra="* 仅用于显示，以便区分用途"
              >
                <Input placeholder="请输入显示名称" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="endpoint"
                label="请求端点"
                rules={[{ required: true, message: '请输入请求端点' }]}
              >
                <Input placeholder="请输入请求端点" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="deploymentName"
                label="部署名称"
                rules={[{ required: true, message: '请输入部署名称' }]}
                extra="* azure需要填写"
              >
                <Input placeholder="请输入部署名称" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="key"
                label="API密钥"
                extra="* 留空则不更新API密钥"
              >
                <Input.Password placeholder="请输入API密钥（留空则不更新）" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="contextWindowTokens"
                label="上下文窗口"
              >
                <InputNumber 
                  placeholder="请输入上下文窗口大小" 
                  style={{ width: '100%' }}
                  min={1}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="textOutput"
                label="输出限制"
              >
                <InputNumber 
                  placeholder="请输入输出限制" 
                  style={{ width: '100%' }}
                  min={1}
                />
              </Form.Item>
            </Col>
          </Row>

          {/* 向量维度字段，只在嵌入模型时显示 */}
          <Form.Item
            noStyle
            shouldUpdate={(prevValues, currentValues) => 
              prevValues.aiModelType !== currentValues.aiModelType
            }
          >
            {({ getFieldValue }) => {
              const modelType = getFieldValue('aiModelType');
              if (modelType === 'embedding') {
                return (
                  <Row gutter={16}>
                    <Col span={12}>
                      <Form.Item
                        name="maxDimension"
                        label="向量维度"
                        rules={[{ required: true, message: '请输入向量维度' }]}
                      >
                        <InputNumber 
                          placeholder="请输入向量维度" 
                          style={{ width: '100%' }}
                          min={1}
                        />
                      </Form.Item>
                    </Col>
                  </Row>
                );
              }
              return null;
            }}
          </Form.Item>

          <Form.Item label="模型能力">
            <Row gutter={16}>
              <Col span={6}>
                <Form.Item name="vision" valuePropName="checked">
                  <Switch checkedChildren="视觉" unCheckedChildren="视觉" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="functionCall" valuePropName="checked">
                  <Switch checkedChildren="函数调用" unCheckedChildren="函数调用" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="files" valuePropName="checked">
                  <Switch checkedChildren="文件上传" unCheckedChildren="文件上传" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="imageOutput" valuePropName="checked">
                  <Switch checkedChildren="图像输出" unCheckedChildren="图像输出" />
                </Form.Item>
              </Col>
            </Row>
          </Form.Item>

          <Form.Item style={{ marginTop: '24px', textAlign: 'right' }}>
            <Space>
              <Button onClick={() => setEditModalVisible(false)}>
                取消
              </Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                确定
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
      </div>
      </>
  );
}