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
import type { PublicModelInfo } from "../../apiClient/models";

export default function WikiSettings() {
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const { modal } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [clearingVectors, setClearingVectors] = useState(false);
  const [embeddingModels, setEmbeddingModels] = useState<PublicModelInfo[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [isLock, setIsLock] = useState(false);
  const { id } = useParams();
  const apiClient = GetApiClient();

  useEffect(() => {
    fetchWikiInfo();
    fetchEmbeddingModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const fetchEmbeddingModels = async () => {
    try {
      setModelsLoading(true);
      const response = await apiClient.api.aimodel.modellist.post({
        aiModelType: "embedding",
      });

      if (response?.aiModels) {
        setEmbeddingModels(response.aiModels);
      }
    } catch (error) {
      console.error("Failed to fetch embedding model list:", error);
      messageApi.error("获取向量化模型列表失败");
    } finally {
      setModelsLoading(false);
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
        setIsLock(response.isLock || false);
        form.setFieldsValue({
          name: response.name,
          description: response.description,
          isPublic: response.isPublic,
          embeddingDimensions: response.embeddingDimensions,
          embeddingModelId: response.embeddingModelId === 0 ? undefined : response.embeddingModelId,
        });
      }
    } catch (error) {
      console.error("Failed to fetch wiki info:", error);
      messageApi.error("获取知识库信息失败");
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (values: any) => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const updateData: any = {
        wikiId: parseInt(id!),
        name: values.name,
        description: values.description,
        isPublic: values.isPublic,
      };

      // 如果未锁定，才传递文档处理配置
      if (!isLock) {
        updateData.embeddingDimensions = values.embeddingDimensions;
        updateData.embeddingModelId = values.embeddingModelId;
      }

      await apiClient.api.wiki.manager.update_wiki_config.post(updateData);

      messageApi.success("保存成功");
      // 重新获取知识库信息并更新表单
      await fetchWikiInfo();
    } catch (error) {
      console.error("Failed to update wiki config:", error);
      messageApi.error("保存失败");
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
      
      await apiClient.api.wiki.document.clear_embeddingt.post({
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
        <Card title="知识库设置">
          <Form form={form} layout="vertical" onFinish={handleSubmit}>
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

            <Form.Item
              name="embeddingModelId"
              label="向量化模型"
              rules={[{ required: !isLock, message: "请选择向量化模型" }]}
            >
              <Select
                placeholder="请选择向量化模型"
                disabled={isLock}
                loading={modelsLoading}
                showSearch
                optionFilterProp="label"
                options={embeddingModels.map((model) => ({
                  label: model.title || model.name || `模型 ${model.id}`,
                  value: model.id,
                  disabled: !model.id,
                }))}
              />
            </Form.Item>

            <Form.Item
              name="embeddingDimensions"
              label="维度"
              rules={[{ required: !isLock, message: "请输入维度" }]}
            >
              <InputNumber
                min={1}
                disabled={isLock}
                style={{ width: "100%" }}
                placeholder="请输入维度"
              />
            </Form.Item>

            <Form.Item label="是否锁定">
              <Space>
                <Switch disabled checked={isLock} checkedChildren="锁定" unCheckedChildren="未锁定" />
                {isLock
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
                loading={loading}
              >
                保存
              </Button>
            </Form.Item>
          </Form>
        </Card>
      </div>
    </>
  );
}