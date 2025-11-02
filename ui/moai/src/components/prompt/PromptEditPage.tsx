import React, { useEffect, useState, useRef } from "react";
import { useNavigate, useParams } from "react-router";
import {
  Card,
  Typography,
  Space,
  Button,
  message,
  Spin,
  Form,
  Input,
  Select,
  Switch,
  Row,
  Col,
} from "antd";
import {
  ArrowLeftOutlined,
  SaveOutlined,
  CloseOutlined,
  LockOutlined,
  GlobalOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons";
import Vditor from "vditor";
import "vditor/dist/index.css";
import { GetApiClient } from "../ServiceClient";
import { UpdatePromptCommand, PromptItem } from "../../apiClient/models";
import "./PromptCreatePage.css";
import { proxyRequestError } from "../../helper/RequestError";

const { Title, Text } = Typography;
const { TextArea } = Input;

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

// 模型类型
interface AiModel {
  id: number;
  name: string;
  title: string;
}

export default function PromptEditPage() {
  const navigate = useNavigate();
  const { promptId } = useParams<{ promptId: string }>();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [fetching, setFetching] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 表单相关
  const [form] = Form.useForm();
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const vditorId = useRef(`vditor-${Math.random().toString(36).substr(2, 9)}`);
  
  // 分类列表
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);
  
  // AI 模型列表
  const [aiModelList, setAiModelList] = useState<AiModel[]>([]);
  const [aiModelLoading, setAiModelLoading] = useState(false);
  const [selectedModelId, setSelectedModelId] = useState<number | null>(null);
  const [optimizing, setOptimizing] = useState(false);

  // 获取分类列表
  const fetchClassList = async () => {
    setClassLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      const classes = (res?.items || [])
        .filter((item: any) => typeof item.classifyId === 'number' && item.classifyId != null)
        .map((item: any) => ({
          id: item.classifyId ?? 0,
          name: item.name || '',
        }));
      setClassList(classes);
    } catch (e) {
      messageApi.error("获取分类失败");
    } finally {
      setClassLoading(false);
    }
  };

  // 获取提示词详情
  const fetchPromptDetails = async (vditor?: Vditor) => {
    if (!promptId) {
      messageApi.error("提示词ID不存在");
      navigate("/app/prompt/list");
      return;
    }

    setFetching(true);
    try {
      const client = GetApiClient();
      const res: PromptItem | undefined = await client.api.prompt.prompt_content.get({
        queryParameters: {
          promptId: parseInt(promptId, 10)
        }
      });

      if (res) {
        // 设置表单字段
        form.setFieldsValue({
          name: res.name || '',
          description: res.description || '',
          promptClassId: res.promptClassId,
          isPublic: res.isPublic || false
        });

        // 设置编辑器内容 - 优先使用传入的 vditor 参数
        const editor = vditor || vditorInstance;
        if (res.content && editor) {
          editor.setValue(res.content);
        }
      }
    } catch (e) {
      console.error("获取提示词详情失败:", e);
      messageApi.error("获取提示词详情失败");
      navigate("/app/prompt/list");
    } finally {
      setFetching(false);
    }
  };

  // 保存提示词
  const handleSave = async () => {
    try {
      await form.validateFields();
      const content = vditorInstance?.getValue() || '';
      if (!content.trim()) {
        messageApi.error("请输入提示词内容");
        return;
      }
      
      if (!promptId) {
        messageApi.error("提示词ID不存在");
        return;
      }

      setSaving(true);
      const values = form.getFieldsValue();
      const client = GetApiClient();
      
      const requestBody: UpdatePromptCommand = {
        promptId: parseInt(promptId, 10),
        ...values,
        content: content,
        promptClassId: values.promptClassId,
        isPublic: values.isPublic || false
      };
      
      await client.api.prompt.update_prompt.post(requestBody);
      
      messageApi.success("更新成功");
    } catch (e) {
      proxyRequestError(e, messageApi, "更新失败");
    } finally {
      setSaving(false);
    }
  };

  // 取消编辑
  const handleCancel = () => {
    navigate("/app/prompt/list");
  };

  // 获取 AI 模型列表
  const fetchAiModelList = async () => {
    setAiModelLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.aimodel.public_modellist.post({
        aiModelType: "chat"
      });
      const models = (res?.aiModels || [])
        .filter((item: any) => item.id != null && item.name != null)
        .map((item: any) => ({
          id: item.id,
          name: item.name,
          title: item.title || item.name,
        }));
      setAiModelList(models);
      
      // 设置默认选中的模型
      if (models.length > 0) {
        setSelectedModelId(models[0].id);
      }
    } catch (e) {
      console.error("获取 AI 模型列表失败:", e);
      messageApi.error("获取 AI 模型列表失败");
    } finally {
      setAiModelLoading(false);
    }
  };

  // AI 优化提示词
  const handleOptimizePrompt = async () => {
    if (!selectedModelId) {
      messageApi.warning("请先选择 AI 模型");
      return;
    }
    
    const currentContent = vditorInstance?.getValue() || '';
    if (!currentContent.trim()) {
      messageApi.warning("请先输入提示词内容");
      return;
    }
    
    setOptimizing(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.ai_optmize_prompt.post({
        aiModelId: selectedModelId,
        sourcePrompt: currentContent
      });
      
      if (res?.content) {
        vditorInstance?.setValue(res.content);
        messageApi.success("AI 优化成功");
      } else {
        messageApi.warning("优化结果为空");
      }
    } catch (e) {
      console.error("AI 优化失败:", e);
      messageApi.error("AI 优化失败");
    } finally {
      setOptimizing(false);
    }
  };

  // 初始化 Vditor 编辑器
  useEffect(() => {
    const timer = setTimeout(() => {
      const container = document.getElementById(vditorId.current);
      if (!container) return;

      if (vditorInstance) {
        vditorInstance.destroy();
        setVditorInstance(null);
      }

      try {
        const vditor = new Vditor(vditorId.current, {
          placeholder: "请输入提示词内容，支持 Markdown 格式",
          height: '100%',
          mode: 'ir',
          toolbar: [
            'emoji', 'headings', 'bold', 'italic', 'strike', 'link', 
            '|', 'list', 'ordered-list', 'check', 'outdent', 'indent',
            '|', 'quote', 'line', 'code', 'inline-code', 'insert-before', 'insert-after',
            '|', 'table',
            '|', 'undo', 'redo',
            '|', 'edit-mode', 'content-theme', 'code-theme', 'export',
            '|', 'fullscreen', 'preview'
          ],
          cache: {
            enable: false
          },
          preview: {
            delay: 1000
          },
          counter: {
            enable: false
          },
          typewriterMode: false,
          theme: 'classic',
          icon: 'material',
          outline: {
            enable: true,
            position: 'left'
          },
          after: async () => {
            setVditorInstance(vditor);
            // 编辑器初始化完成后获取数据并设置内容
            if (promptId) {
              await fetchPromptDetails(vditor);
            }
          },
        });
      } catch (error) {
        console.error('Vditor initialization error:', error);
      }
    }, 100);

    return () => {
      clearTimeout(timer);
      if (vditorInstance) {
        vditorInstance.destroy();
        setVditorInstance(null);
      }
    };
  }, []);

  useEffect(() => {
    fetchClassList();
    fetchAiModelList();
  }, []);

  return (
    <>
      {contextHolder}
      <Card className="prompt-create-page-card">
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>编辑提示词</Title>
              <Space>
                <Button
                  type="primary"
                  icon={<SaveOutlined />}
                  onClick={handleSave}
                  loading={saving}
                >
                  保存
                </Button>
                <Button
                  icon={<CloseOutlined />}
                  onClick={handleCancel}
                  disabled={saving}
                >
                  取消
                </Button>
                <Button icon={<ArrowLeftOutlined />} onClick={() => navigate("/app/prompt/list")}>
                  返回列表
                </Button>
              </Space>
            </Space>
          }
        />
        
        <div className="prompt-create-page-form-container">
          <Spin spinning={fetching}>
            <Form
              form={form}
              layout="vertical"
              initialValues={{
                name: '',
                description: '',
                content: '',
                promptClassId: null,
                isPublic: false
              }}
            >
              <Row gutter={16}>
                <Col span={8}>
                  <Form.Item
                    name="promptClassId"
                    label="所属分类"
                    rules={[{ required: true, message: "请选择分类" }]}
                  >
                    <Select
                      placeholder="请选择分类"
                      loading={classLoading}
                      options={classList.map(cls => ({
                        value: cls.id,
                        label: cls.name
                      }))}
                    />
                  </Form.Item>
                  
                  <Form.Item
                    name="name"
                    label="提示词标题"
                    rules={[{ required: true, message: "请输入提示词标题" }]}
                  >
                    <Input placeholder="请输入提示词标题" />
                  </Form.Item>
                </Col>
                
                <Col span={8}>
                  <Form.Item name="isPublic" label="是否公开(其他人可以访问)" valuePropName="checked">
                    <Switch 
                      checkedChildren={<><GlobalOutlined /> 公开</>}
                      unCheckedChildren={<><LockOutlined /> 私有</>}
                    />
                  </Form.Item>
                </Col>
                
                <Col span={8}>
                  <Form.Item name="description" label="描述">
                    <TextArea rows={5} placeholder="请输入提示词描述（可选）" />
                  </Form.Item>
                </Col>
              </Row>
              
              <Form.Item label="提示词内容" required style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                <Space direction="vertical" style={{ width: '100%', flex: 1 }} size="middle">
                  <Space style={{ width: '100%', justifyContent: 'flex-start' }}>
                    <Select
                      style={{ width: 200 }}
                      placeholder="选择 AI 模型"
                      loading={aiModelLoading}
                      value={selectedModelId}
                      onChange={setSelectedModelId}
                      options={aiModelList.map(model => ({
                        value: model.id,
                        label: model.title
                      }))}
                    />
                    <Button
                      icon={<ThunderboltOutlined />}
                      onClick={handleOptimizePrompt}
                      loading={optimizing}
                      type="primary"
                    >
                      AI一键优化提示词
                    </Button>
                  </Space>
                  <div 
                    id={vditorId.current}
                    className="vditor prompt-create-page-vditor"
                  />
                </Space>
              </Form.Item>
            </Form>
          </Spin>
        </div>
      </Card>
    </>
  );
}

