import React, { useEffect, useState, useRef } from "react";
import { useParams, useNavigate } from "react-router";
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
  Divider,
  Modal,
  Tabs,
  List,
  Avatar,
  Tag,
} from "antd";
import {
  ArrowLeftOutlined,
  SaveOutlined,
  CloseOutlined,
  UserOutlined,
  CalendarOutlined,
  LockOutlined,
  GlobalOutlined,
  RobotOutlined,
} from "@ant-design/icons";
import Vditor from "vditor";
import "vditor/dist/index.css";
import { GetApiClient } from "../ServiceClient";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;

// 提示词类型
interface PromptItem {
  id: number;
  name: string;
  description?: string;
  content?: string;
  createTime?: string;
  createUserName?: string;
  createUserId?: number;
  updateTime?: string;
  updateUserName?: string;
  isOwn?: boolean;
  isPublic?: boolean;
  promptClassId?: number;
}

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

export default function PromptEdit() {
  const { promptId } = useParams();
  const navigate = useNavigate();
  const [prompt, setPrompt] = useState<PromptItem | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [optimizing, setOptimizing] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 模型选择相关状态
  const [modelModalVisible, setModelModalVisible] = useState(false);
  const [systemModels, setSystemModels] = useState<any[]>([]);
  const [userModels, setUserModels] = useState<any[]>([]);
  const [systemModelsLoading, setSystemModelsLoading] = useState(false);
  const [userModelsLoading, setUserModelsLoading] = useState(false);
  const [selectedModel, setSelectedModel] = useState<any>(null);
  
  // 表单相关
  const [form] = Form.useForm();
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const vditorId = useRef(`vditor-${Math.random().toString(36).substr(2, 9)}`);
  
  // 用户信息
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [isAdmin, setIsAdmin] = useState(false);
  
  // 分类列表
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);

  // 获取当前用户信息
  const fetchUserInfo = async () => {
    try {
      const client = GetApiClient();
      const res = await client.api.common.userinfo.get();
      setIsAdmin(res?.isAdmin === true);
      setCurrentUserId(res?.userId || null);
    } catch (e) {
      setIsAdmin(false);
      setCurrentUserId(null);
    }
  };

  // 获取分类列表
  const fetchClassList = async () => {
    setClassLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      setClassList(
        (res?.items || [])
          .filter((item: any) => typeof item.id === 'number' && item.id != null)
          .map((item: any) => ({
            id: item.id ?? 0,
            name: item.name || '',
          }))
      );
    } catch (e) {
      messageApi.error("获取分类失败");
    } finally {
      setClassLoading(false);
    }
  };

  // 检查编辑权限
  const canEditPrompt = (prompt: PromptItem | null) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId || isAdmin;
  };

  // 获取提示词内容
  const fetchPromptContent = async () => {
    if (!promptId) {
      messageApi.error("缺少提示词ID");
      return;
    }

    try {
      setLoading(true);
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: parseInt(promptId) }
      });
      
      if (res) {
        const promptItem: PromptItem = {
          id: res.id || 0,
          name: res.name || '',
          description: res.description || '',
          content: res.content || '',
          createTime: res.createTime || '',
          createUserName: res.createUserName || '',
          createUserId: res.createUserId || 0,
          updateTime: res.updateTime || '',
          updateUserName: res.updateUserName || '',
          isOwn: false,
          isPublic: res.isPublic || false,
          promptClassId: (res as any).promptClassId || null
        };
        setPrompt(promptItem);
        
        // 设置表单初始值
        form.setFieldsValue({
          name: promptItem.name,
          description: promptItem.description,
          promptClassId: promptItem.promptClassId || classList[0]?.id,
          isPublic: promptItem.isPublic || false
        });
      } else {
        messageApi.error("获取提示词内容失败");
      }
    } catch (e) {
      console.error("获取提示词内容失败:", e);
      messageApi.error("获取提示词内容失败");
    } finally {
      setLoading(false);
    }
  };

  // 保存编辑
  const handleSave = async () => {
    try {
      await form.validateFields();
      const content = vditorInstance?.getValue() || '';
      if (!content.trim()) {
        messageApi.error("请输入提示词内容");
        return;
      }
      
      setSaving(true);
      const values = form.getFieldsValue();
      const client = GetApiClient();
      
      await client.api.prompt.update_prompt.post({ 
        ...values, 
        content: content,
        promptId: prompt!.id,
        promptClassId: values.promptClassId,
        isPublic: values.isPublic || false
      });
      
      messageApi.success("保存成功");
      navigate(`/app/prompt/${prompt!.id}/content`);
    } catch (e) {
      console.error("保存失败:", e);
      messageApi.error("保存失败");
    } finally {
      setSaving(false);
    }
  };

  // 取消编辑
  const handleCancel = () => {
    navigate(`/app/prompt/${promptId}/content`);
  };

  // 获取系统模型列表
  const fetchSystemModels = async () => {
    try {
      setSystemModelsLoading(true);
      const client = GetApiClient();
      const response = await client.api.aimodel.type.public_system_aimodel_list.post({
        aiModelType: "chat",
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

  // 获取用户模型列表
  const fetchUserModels = async () => {
    try {
      setUserModelsLoading(true);
      const client = GetApiClient();
      const response = await client.api.aimodel.type.user_modellist.post({
        aiModelType: "chat",
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

  // 打开模型选择弹窗
  const handleModelSelect = () => {
    const currentContent = vditorInstance?.getValue() || '';
    if (!currentContent.trim()) {
      messageApi.error("请先输入提示词内容");
      return;
    }
    
    setModelModalVisible(true);
    // 加载模型列表
    fetchSystemModels();
    fetchUserModels();
  };

  // 直接使用已选择的模型进行优化
  const handleOptimizeWithSelectedModel = async () => {
    if (!selectedModel) {
      messageApi.error("请先选择AI模型");
      return;
    }

    const currentContent = vditorInstance?.getValue() || '';
    if (!currentContent.trim()) {
      messageApi.error("请先输入提示词内容");
      return;
    }

    try {
      setOptimizing(true);
      const client = GetApiClient();
      
      // 调用AI优化API
      const res = await client.api.prompt.ai_optmize_prompt.post({
        sourcePrompt: currentContent,
        aiModelId: selectedModel.id
      });
      
      if (res?.content) {
        // 将优化后的内容设置到编辑器
        vditorInstance?.setValue(res.content);
        messageApi.success("AI优化完成");
      } else {
        messageApi.error("AI优化失败，未返回优化结果");
      }
    } catch (e) {
      console.error("AI优化失败:", e);
      messageApi.error("AI优化失败");
    } finally {
      setOptimizing(false);
    }
  };

  // 确认选择模型
  const handleModelConfirm = (model: any) => {
    setSelectedModel(model);
    setModelModalVisible(false);
    messageApi.success(`已选择模型: ${model.name}`);
  };

  // 渲染模型列表
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
                  {model.contextWindowTokens && <span>上下文窗口: {model.contextWindowTokens} tokens</span>}
                  {model.textOutput && <span>最大输出: {model.textOutput} tokens</span>}
                </Space>
              }
            />
          </List.Item>
        )}
      />
    </Spin>
  );

  // 初始化 Vditor 编辑器
  useEffect(() => {
    if (prompt && !loading) {
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
            height: 'auto',
            width: '100%',
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
            after: () => {
              if (prompt.content) {
                vditor.setValue(prompt.content);
              }
              setVditorInstance(vditor);
              
              // 调整编辑器样式，让内容占满宽度
              setTimeout(() => {
                const editorElement = document.querySelector(`#${vditorId.current} .vditor-ir__editor`);
                if (editorElement) {
                  (editorElement as HTMLElement).style.maxWidth = 'none';
                  (editorElement as HTMLElement).style.width = '100%';
                  (editorElement as HTMLElement).style.padding = '0 20px';
                }
                
                const nodes = document.querySelectorAll(`#${vditorId.current} .vditor-ir__node`);
                nodes.forEach((node) => {
                  (node as HTMLElement).style.maxWidth = 'none';
                  (node as HTMLElement).style.width = '100%';
                });
              }, 100);
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
    }
  }, [prompt, loading]);

  useEffect(() => {
    fetchUserInfo();
    fetchClassList();
    fetchPromptContent();
  }, [promptId]);

  if (loading) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>加载中...</div>
      </div>
    );
  }

  if (!prompt) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Text type="secondary">提示词不存在或已被删除</Text>
        <br />
        <Button type="primary" onClick={() => navigate("/app/prompt/list")} style={{ marginTop: 16 }}>
          返回列表
        </Button>
      </div>
    );
  }

  if (!canEditPrompt(prompt)) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Text type="secondary">您没有编辑此提示词的权限</Text>
        <br />
        <Button type="primary" onClick={() => navigate(`/app/prompt/${promptId}/content`)} style={{ marginTop: 16 }}>
          返回查看
        </Button>
      </div>
    );
  }

  return (
    <>
      {contextHolder}
      <Card>
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>编辑提示词</Title>
              <Space>
                {selectedModel ? (
                  <>
                    <Button
                      icon={<RobotOutlined />}
                      onClick={handleOptimizeWithSelectedModel}
                      loading={optimizing}
                      disabled={saving}
                    >
                      使用 {selectedModel.name} 优化
                    </Button>
                    <Button
                      icon={<RobotOutlined />}
                      onClick={handleModelSelect}
                      disabled={saving || optimizing}
                    >
                      更换模型
                    </Button>
                  </>
                ) : (
                  <Button
                    icon={<RobotOutlined />}
                    onClick={handleModelSelect}
                    loading={optimizing}
                    disabled={saving}
                  >
                    使用AI优化
                  </Button>
                )}
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
          description={
            <Space direction="vertical" style={{ width: "100%" }}>
              <Space>
                <Text type="secondary">
                  <UserOutlined /> {prompt.createUserName}
                </Text>
                <Text type="secondary">
                  <CalendarOutlined /> {formatDateTime(prompt.createTime)}
                </Text>
                {prompt.promptClassId && (
                  <Text type="secondary">
                    分类: {classList.find(cls => cls.id === prompt.promptClassId)?.name || '未知分类'}
                  </Text>
                )}
                {prompt.isPublic ? (
                  <Text type="secondary" style={{ color: '#52c41a' }}>
                    <GlobalOutlined /> 公开
                  </Text>
                ) : (
                  <Text type="secondary" style={{ color: '#fa8c16' }}>
                    <LockOutlined /> 私密
                  </Text>
                )}
              </Space>
            </Space>
          }
        />
        
        <Divider />
        
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            name: prompt.name,
            description: prompt.description,
            promptClassId: prompt.promptClassId || classList[0]?.id,
            isPublic: prompt.isPublic || false
          }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="name"
                label="提示词名称"
                rules={[{ required: true, message: "请输入提示词名称" }]}
              >
                <Input placeholder="请输入提示词名称" />
              </Form.Item>
            </Col>
            <Col span={12}>
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
            </Col>
          </Row>
          
          <Form.Item name="description" label="描述">
            <TextArea rows={2} placeholder="请输入提示词描述（可选）" />
          </Form.Item>
          
          <Form.Item name="isPublic" label="是否公开" valuePropName="checked">
            <Switch 
              checkedChildren={<><GlobalOutlined /> 公开</>}
              unCheckedChildren={<><LockOutlined /> 私有</>}
            />
          </Form.Item>
          
          <Form.Item label="提示词内容" required>
            <div 
              id={vditorId.current}
              className="vditor" 
              style={{ 
                fontSize: '14px',
                width: '100%',
                minHeight: '200px'
              }}
            />

          </Form.Item>
        </Form>
      </Card>

      {/* 模型选择弹窗 */}
      <Modal
        title="选择AI模型进行提示词优化"
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
    </>
  );
} 