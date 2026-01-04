import { useEffect, useState, useRef, useCallback } from "react";
import { useNavigate, useParams } from "react-router";
import {
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
import { proxyRequestError } from "../../helper/RequestError";
import "./PromptEditPage.css";

const { TextArea } = Input;

interface PromptClass {
  id: number;
  name: string;
}

interface AiModel {
  id: number;
  name: string;
  title: string;
}

// 生成唯一 ID
const generateId = () => Math.random().toString(36).substring(2, 11);

export default function PromptEditPage() {
  const navigate = useNavigate();
  const { promptId } = useParams<{ promptId: string }>();
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  // 状态
  const [saving, setSaving] = useState(false);
  const [fetching, setFetching] = useState(false);
  const [optimizing, setOptimizing] = useState(false);

  // 编辑器
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const vditorId = useRef(`vditor-${generateId()}`);

  // 数据列表
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);
  const [aiModelList, setAiModelList] = useState<AiModel[]>([]);
  const [aiModelLoading, setAiModelLoading] = useState(false);
  const [selectedModelId, setSelectedModelId] = useState<number | null>(null);

  // 获取分类列表
  const fetchClassList = useCallback(async () => {
    setClassLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      const classes = (res?.items || [])
        .filter((item: any) => typeof item.classifyId === "number" && item.classifyId != null)
        .map((item: any) => ({
          id: item.classifyId ?? 0,
          name: item.name || "",
        }));
      setClassList(classes);
    } catch {
      messageApi.error("获取分类失败");
    } finally {
      setClassLoading(false);
    }
  }, [messageApi]);

  // 获取 AI 模型列表
  const fetchAiModelList = useCallback(async () => {
    setAiModelLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.aimodel.modellist.post({ aiModelType: "chat" });
      const models = (res?.aiModels || [])
        .filter((item: any) => item.id != null && item.name != null)
        .map((item: any) => ({
          id: item.id,
          name: item.name,
          title: item.title || item.name,
        }));
      setAiModelList(models);
      if (models.length > 0) {
        setSelectedModelId(models[0].id);
      }
    } catch {
      messageApi.error("获取 AI 模型列表失败");
    } finally {
      setAiModelLoading(false);
    }
  }, [messageApi]);

  // 获取提示词详情
  const fetchPromptDetails = useCallback(
    async (vditor?: Vditor) => {
      if (!promptId) {
        messageApi.error("提示词ID不存在");
        navigate("/app/prompt/list");
        return;
      }

      setFetching(true);
      try {
        const client = GetApiClient();
        const res: PromptItem | undefined = await client.api.prompt.prompt_content.get({
          queryParameters: { promptId: parseInt(promptId, 10) },
        });

        if (res) {
          form.setFieldsValue({
            name: res.name || "",
            description: res.description || "",
            promptClassId: res.promptClassId,
            isPublic: res.isPublic || false,
          });

          const editor = vditor || vditorInstance;
          if (res.content && editor) {
            editor.setValue(res.content);
          }
        }
      } catch {
        messageApi.error("获取提示词详情失败");
        navigate("/app/prompt/list");
      } finally {
        setFetching(false);
      }
    },
    [promptId, form, vditorInstance, messageApi, navigate]
  );

  // 保存提示词
  const handleSave = async () => {
    try {
      await form.validateFields();
      const content = vditorInstance?.getValue() || "";
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
        content,
        promptClassId: values.promptClassId,
        isPublic: values.isPublic || false,
      };

      await client.api.prompt.update_prompt.post(requestBody);
      messageApi.success("更新成功");
    } catch (error) {
      proxyRequestError(error, messageApi, "更新失败");
    } finally {
      setSaving(false);
    }
  };

  // AI 优化提示词
  const handleOptimizePrompt = async () => {
    if (!selectedModelId) {
      messageApi.warning("请先选择 AI 模型");
      return;
    }

    const currentContent = vditorInstance?.getValue() || "";
    if (!currentContent.trim()) {
      messageApi.warning("请先输入提示词内容");
      return;
    }

    setOptimizing(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.ai_optmize_prompt.post({
        aiModelId: selectedModelId,
        sourcePrompt: currentContent,
      });

      if (res?.content) {
        vditorInstance?.setValue(res.content);
        messageApi.success("AI 优化成功");
      } else {
        messageApi.warning("优化结果为空");
      }
    } catch {
      messageApi.error("AI 优化失败");
    } finally {
      setOptimizing(false);
    }
  };

  // 返回列表
  const handleBack = () => navigate("/app/prompt/list");

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
          height: "100%",
          mode: "ir",
          toolbar: [
            "emoji", "headings", "bold", "italic", "strike", "link",
            "|", "list", "ordered-list", "check", "outdent", "indent",
            "|", "quote", "line", "code", "inline-code", "insert-before", "insert-after",
            "|", "table",
            "|", "undo", "redo",
            "|", "edit-mode", "content-theme", "code-theme", "export",
            "|", "fullscreen", "preview",
          ],
          cache: { enable: false },
          preview: { delay: 1000 },
          counter: { enable: false },
          typewriterMode: false,
          theme: "classic",
          icon: "material",
          outline: { enable: true, position: "left" },
          after: async () => {
            setVditorInstance(vditor);
            if (promptId) {
              await fetchPromptDetails(vditor);
            }
          },
        });
      } catch (error) {
        console.error("Vditor initialization error:", error);
      }
    }, 100);

    return () => {
      clearTimeout(timer);
      if (vditorInstance) {
        vditorInstance.destroy();
        setVditorInstance(null);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // 加载初始数据
  useEffect(() => {
    fetchClassList();
    fetchAiModelList();
  }, [fetchClassList, fetchAiModelList]);

  return (
    <>
      {contextHolder}
      <div className="prompt-edit-page">
        {/* 页面头部 */}
        <header className="prompt-edit-header">
          <h1 className="prompt-edit-header-title">编辑提示词</h1>
          <div className="prompt-edit-header-actions">
            <Button type="primary" icon={<SaveOutlined />} onClick={handleSave} loading={saving}>
              保存
            </Button>
            <Button icon={<CloseOutlined />} onClick={handleBack} disabled={saving}>
              取消
            </Button>
            <Button icon={<ArrowLeftOutlined />} onClick={handleBack}>
              返回列表
            </Button>
          </div>
        </header>

        {/* 表单内容 */}
        <div className="prompt-edit-form-container">
          <Spin spinning={fetching}>
            <Form
              form={form}
              layout="vertical"
              initialValues={{ name: "", description: "", promptClassId: null, isPublic: false }}
            >
              {/* 基础信息 - 三列布局 */}
              <div className="prompt-edit-form-info">
                <Row gutter={24}>
                  {/* 第一列：分类 + 标题 */}
                  <Col xs={24} md={8}>
                    <Form.Item
                      name="promptClassId"
                      label="所属分类"
                      rules={[{ required: true, message: "请选择分类" }]}
                    >
                      <Select
                        placeholder="请选择分类"
                        loading={classLoading}
                        options={classList.map((cls) => ({ value: cls.id, label: cls.name }))}
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

                  {/* 第二列：公开 + AI优化 */}
                  <Col xs={24} md={8}>
                    <Form.Item name="isPublic" label="是否公开" valuePropName="checked">
                      <Switch
                        checkedChildren={<><GlobalOutlined /> 公开</>}
                        unCheckedChildren={<><LockOutlined /> 私有</>}
                      />
                    </Form.Item>
                    <Form.Item label="AI 优化">
                      <Space>
                        <Select
                          style={{ width: 250 }}
                          placeholder="选择模型"
                          loading={aiModelLoading}
                          value={selectedModelId}
                          onChange={setSelectedModelId}
                          options={aiModelList.map((model) => ({ value: model.id, label: model.title }))}
                        />
                        <Button
                          icon={<ThunderboltOutlined />}
                          onClick={handleOptimizePrompt}
                          loading={optimizing}
                          type="primary"
                        >
                          一键优化
                        </Button>
                      </Space>
                    </Form.Item>
                  </Col>

                  {/* 第三列：描述 */}
                  <Col xs={24} md={8}>
                    <Form.Item name="description" label="描述">
                      <TextArea rows={5} placeholder="请输入提示词描述（可选）" />
                    </Form.Item>
                  </Col>
                </Row>
              </div>

              {/* 编辑器区域 */}
              <div className="prompt-edit-editor-section">
                <div className="prompt-edit-editor-label">提示词内容</div>
                <div id={vditorId.current} className="vditor prompt-edit-vditor" />
              </div>
            </Form>
          </Spin>
        </div>
      </div>
    </>
  );
}
