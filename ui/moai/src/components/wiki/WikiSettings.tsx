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
  const [modelList, setModelList] = useState<any[]>([]);
  const { id } = useParams();
  const apiClient = GetApiClient();

  useEffect(() => {
    fetchWikiInfo();
    fetchModelList();
  }, [id]);

  const fetchModelList = async () => {
    try {
      const response = await apiClient.api.aimodel.type.modellist.post({
        aiModelType: "embedding",
      });

      if (response) {
        setModelList(response.aiModels || []);
      }
    } catch (error) {
      console.error("Failed to fetch model list:", error);
      messageApi.error("获取模型列表失败");
    }
  };

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
      }
    } catch (error) {
      console.error("Failed to fetch wiki info:", error);
      messageApi.error("获取知识库信息失败");
    } finally {
      setLoading(false);
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
              <Select
                disabled={configForm.getFieldValue("isLock")}
                options={modelList.map((model) => ({
                  label: model.name + "(" + model.provider + ")",
                  value: model.id,
                }))}
              />
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
      </div>
    </>
  );
}