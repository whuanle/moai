import { useState, useEffect } from "react";
import {
  Card,
  Form,
  Input,
  Button,
  message,
  Switch,
  InputNumber,
  Select,
  Space,
  Tooltip,
  App,
  Modal,
  Tabs,
  List,
  Avatar,
  Tag,
  Spin,
} from "antd";
import { GetApiClient } from "../ServiceClient";
import { useParams } from "react-router";

export default function WikiSettings() {
  const [infoForm] = Form.useForm();
  const [configForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const { modal } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [infoLoading, setInfoLoading] = useState(false);
  const [clearingVectors, setClearingVectors] = useState(false);
  const [modelModalVisible, setModelModalVisible] = useState(false);
  const [systemModels, setSystemModels] = useState<any[]>([]);
  const [userModels, setUserModels] = useState<any[]>([]);
  const [systemModelsLoading, setSystemModelsLoading] = useState(false);
  const [userModelsLoading, setUserModelsLoading] = useState(false);
  const [selectedModel, setSelectedModel] = useState<any>(null);
  const { id } = useParams();
  const apiClient = GetApiClient();

  useEffect(() => {
    fetchWikiInfo();
  }, [id]);

  const fetchSystemModels = async () => {
    try {
      setSystemModelsLoading(true);
      const response = await apiClient.api.aimodel.type.public_system_aimodel_list.post({
        aiModelType: "embedding",
      });

      if (response) {
        setSystemModels(response.aiModels || []);
      }
    } catch (error) {
      console.error("Failed to fetch system model list:", error);
      messageApi.error("获取系统模型列表失败");
    } finally {
      setSystemModelsLoading(false);
    }
  };

  const fetchUserModels = async () => {
    try {
      setUserModelsLoading(true);
      const response = await apiClient.api.aimodel.type.user_modellist.post({
        aiModelType: "embedding",
      });

      if (response) {
        setUserModels(response.aiModels || []);
      }
    } catch (error) {
      console.error("Failed to fetch user model list:", error);
      messageApi.error("获取私有模型列表失败");
    } finally {
      setUserModelsLoading(false);
    }
  };

  const handleModelSelect = () => {
    setModelModalVisible(true);
    // 获取当前选中的模型信息
    const currentModelId = configForm.getFieldValue("embeddingModelId");
    if (currentModelId) {
      const allModels = [...systemModels, ...userModels];
      const model = allModels.find(m => m.id === currentModelId);
      setSelectedModel(model);
    }
    // 加载模型列表
    fetchSystemModels();
    fetchUserModels();
  };

  const handleModelConfirm = (model: any) => {
    setSelectedModel(model);
    configForm.setFieldsValue({
      embeddingModelId: model.id,
    });
    setModelModalVisible(false);
  };

  const renderModelList = (models: any[], loading: boolean) => (
    <Spin spinning={loading}>
      <List
        dataSource={models}
        renderItem={(model) => (
          <List.Item
            actions={[
              <Button
                type={selectedModel?.id === model.id ? "primary" : "default"}
                onClick={() => handleModelConfirm(model)}
              >
                {selectedModel?.id === model.id ? "已选择" : "选择"}
              </Button>
            ]}
          >
            <List.Item.Meta
              avatar={<Avatar>{model.provider?.charAt(0)?.toUpperCase()}</Avatar>}
              title={
                <Space>
                  <span>{model.name}</span>
                  <Tag color="blue">{model.provider}</Tag>
                  {model.isPublic && <Tag color="green">公开</Tag>}
                </Space>
              }
              description={
                <Space direction="vertical" size="small">
                  <span>标题: {model.title}</span>
                  <span>端点: {model.endpoint}</span>
                  {model.maxDimension && <span>最大维度: {model.maxDimension}</span>}
                </Space>
              }
            />
          </List.Item>
        )}
      />
    </Spin>
  );

  const fetchWikiInfo = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response = await apiClient.api.wiki.query_wiki_info.post({
        wikiId: parseInt(id!),
      });

      if (response) {
        infoForm.setFieldsValue({
          name: response.name,
          description: response.description,
          isPublic: response.isPublic,
        });
        
        configForm.setFieldsValue({
          embeddingBatchSize: response.embeddingBatchSize,
          embeddingDimensions: response.embeddingDimensions,
          embeddingModelId: response.embeddingModelId === 0 ? undefined : response.embeddingModelId,
          embeddingModelTokenizer: response.embeddingModelTokenizer,
          maxRetries: response.maxRetries,
          isLock: response.isLock,
        });

        // 设置当前选中的模型
        if (response.embeddingModelId && response.embeddingModelId !== 0) {
          setSelectedModel({ id: response.embeddingModelId });
          // 获取模型详细信息
          await fetchModelDetails(response.embeddingModelId);
        }
      }
    } catch (error) {
      console.error("Failed to fetch wiki info:", error);
      messageApi.error("获取知识库信息失败");
    } finally {
      setLoading(false);
    }
  };

  const fetchModelDetails = async (modelId: number) => {
    try {
      // 先尝试从系统模型获取
      const systemResponse = await apiClient.api.aimodel.type.public_system_aimodel_list.post({
        aiModelType: "embedding",
      });
      
      if (systemResponse?.aiModels) {
        const systemModel = systemResponse.aiModels.find(m => m.id === modelId);
        if (systemModel) {
          setSelectedModel(systemModel);
          return;
        }
      }

      // 如果系统模型中没有找到，尝试从用户模型获取
      const userResponse = await apiClient.api.aimodel.type.user_modellist.post({
        aiModelType: "embedding",
      });
      
      if (userResponse?.aiModels) {
        const userModel = userResponse.aiModels.find(m => m.id === modelId);
        if (userModel) {
          setSelectedModel(userModel);
        }
      }
    } catch (error) {
      console.error("Failed to fetch model details:", error);
    }
  };

  const handleInfoSubmit = async (values: any) => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setInfoLoading(true);
      await apiClient.api.wiki.update_wiki_info.post({
        wikiId: parseInt(id!),
        name: values.name,
        description: values.description,
        isPublic: values.isPublic,
      });

      messageApi.success("知识库信息保存成功");
      // 重新获取知识库信息并更新表单
      await fetchWikiInfo();
    } catch (error) {
      console.error("Failed to update wiki info:", error);
      messageApi.error("保存知识库信息失败");
    } finally {
      setInfoLoading(false);
    }
  };

  const handleConfigSubmit = async (values: any) => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      await apiClient.api.wiki.update_wiki_config.post({
        wikiId: parseInt(id!),
        embeddingBatchSize: values.embeddingBatchSize,
        embeddingDimensions: values.embeddingDimensions,
        embeddingModelId: values.embeddingModelId,
        embeddingModelTokenizer: values.embeddingModelTokenizer,
        maxRetries: values.maxRetries,
      });

      messageApi.success("文档处理配置保存成功");
    } catch (error) {
      console.error("Failed to update wiki config:", error);
      messageApi.error("保存文档处理配置失败");
    } finally {
      setLoading(false);
    }
  };

  const handleClearVectors = () => {
    modal.confirm({
      title: "确认清空知识库向量",
      content: "此操作将删除所有已生成的向量数据，操作不可逆。确定要继续吗？",
      okText: "确定",
      cancelText: "取消",
      okType: "danger",
      onOk: clearWikiVectors,
    });
  };

  const clearWikiVectors = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setClearingVectors(true);
      
      await apiClient.api.wiki.document.clear_document.post({
        wikiId: parseInt(id!),
      });

      messageApi.success("知识库向量已清空");
      // 重新获取配置，因为清空后isLock状态可能会改变
      await fetchWikiInfo();
    } catch (error) {
      console.error("Failed to clear wiki vectors:", error);
      messageApi.error("清空向量失败");
    } finally {
      setClearingVectors(false);
    }
  };

  return (
    <>
      {contextHolder}
      <div style={{ maxWidth: '800px' }}>
        <Card title="知识库信息设置" style={{ marginBottom: '16px' }}>
          <Form form={infoForm} layout="vertical" onFinish={handleInfoSubmit}>
            <Form.Item
              name="name"
              label="知识库名称"
              rules={[{ required: true, message: "请输入知识库名称" }]}
            >
              <Input placeholder="请输入知识库名称" />
            </Form.Item>

            <Form.Item
              name="description"
              label="知识库描述"
              rules={[{ required: true, message: "请输入知识库描述" }]}
            >
              <Input.TextArea 
                placeholder="请输入知识库描述" 
                rows={4}
              />
            </Form.Item>

            <Form.Item name="isPublic" label="是否公开" valuePropName="checked">
              <Switch checkedChildren="公开" unCheckedChildren="私有" />
            </Form.Item>

            <Form.Item>
              <Button
                type="primary"
                htmlType="submit"
                loading={infoLoading}
              >
                保存知识库信息
              </Button>
            </Form.Item>
          </Form>
        </Card>

        <Card title="文档处理配置">
          <Form form={configForm} layout="vertical" onFinish={handleConfigSubmit}>
            <Form.Item
              name="embeddingBatchSize"
              label="批处理大小"
              rules={[{ required: true, message: "请输入批处理大小" }]}
            >
              <InputNumber
                min={1}
                disabled={configForm.getFieldValue("isLock")}
                style={{ width: "100%" }}
              />
            </Form.Item>

            <Form.Item
              name="embeddingDimensions"
              label="维度"
              rules={[{ required: true, message: "请输入维度" }]}
            >
              <InputNumber
                min={1}
                disabled={configForm.getFieldValue("isLock")}
                style={{ width: "100%" }}
              />
            </Form.Item>

            <Form.Item
              name="embeddingModelId"
              label="向量化模型"
              rules={[{ required: true, message: "请选择向量化模型" }]}
            >
              <div>
                <Button
                  onClick={handleModelSelect}
                  disabled={configForm.getFieldValue("isLock")}
                  style={{ width: "100%", textAlign: "left" }}
                >
                  {selectedModel ? `${selectedModel.name || '已选择模型'} (${selectedModel.provider || '未知提供商'})` : "请选择向量化模型"}
                </Button>
              </div>
            </Form.Item>

            <Form.Item
              name="embeddingModelTokenizer"
              label="分词器"
              rules={[{ required: true, message: "请选择分词器" }]}
            >
              <Select
                disabled={configForm.getFieldValue("isLock")}
                options={[
                  { label: "50k", value: "50k" },
                  { label: "cl100k", value: "cl100k" },
                  { label: "o200k", value: "o200k" },
                ]}
              />
            </Form.Item>

            <Form.Item
              name="maxRetries"
              label="最大重试次数"
              rules={[{ required: true, message: "请输入最大重试次数" }]}
            >
              <InputNumber
                min={0}
                disabled={configForm.getFieldValue("isLock")}
                style={{ width: "100%" }}
              />
            </Form.Item>

            <Form.Item label="是否锁定">
              <Space>
                <Switch disabled checked={configForm.getFieldValue("isLock")} checkedChildren="锁定" unCheckedChildren="未锁定" />
                {configForm.getFieldValue("isLock")
                  ? "知识库已生成数据，禁止修改，如必须修改可点击："
                  : "第一次向量化文档后自动锁定"}
                <Tooltip title="强制清空该文档的所有向量">
                  <Button 
                    type="default" 
                    danger
                    loading={clearingVectors}
                    onClick={handleClearVectors}
                  >
                    强制清空知识库向量
                  </Button>
                </Tooltip>
              </Space>
            </Form.Item>

            <Form.Item>
              <Button
                type="primary"
                htmlType="submit"
                disabled={configForm.getFieldValue("isLock")}
                loading={loading}
              >
                保存文档处理配置
              </Button>
            </Form.Item>
          </Form>
        </Card>

        <Modal
          title="选择向量化模型"
          open={modelModalVisible}
          onCancel={() => setModelModalVisible(false)}
          footer={null}
          width={800}
        >
          <Tabs
            defaultActiveKey="system"
            items={[
              {
                key: "system",
                label: "系统模型",
                children: renderModelList(systemModels, systemModelsLoading),
              },
              {
                key: "user",
                label: "私有模型",
                children: renderModelList(userModels, userModelsLoading),
              },
            ]}
          />
        </Modal>
      </div>
    </>
  );
}