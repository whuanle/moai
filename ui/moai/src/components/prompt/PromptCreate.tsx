import React, { useEffect, useState, useRef } from "react";
import { useNavigate } from "react-router";
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
} from "@ant-design/icons";
import Vditor from "vditor";
import "vditor/dist/index.css";
import { GetApiClient } from "../ServiceClient";

const { Title, Text } = Typography;
const { TextArea } = Input;

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

export default function PromptCreate() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 表单相关
  const [form] = Form.useForm();
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const vditorId = useRef(`vditor-${Math.random().toString(36).substr(2, 9)}`);
  
  // 分类列表
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);

  // 获取分类列表
  const fetchClassList = async () => {
    setClassLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      const classes = (res?.items || [])
        .filter((item: any) => typeof item.id === 'number' && item.id != null)
        .map((item: any) => ({
          id: item.id ?? 0,
          name: item.name || '',
        }));
      setClassList(classes);
      
      // 如果有分类，设置默认值
      if (classes.length > 0) {
        form.setFieldsValue({
          promptClassId: classes[0].id,
          isPublic: false
        });
      }
    } catch (e) {
      messageApi.error("获取分类失败");
    } finally {
      setClassLoading(false);
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
      
      setSaving(true);
      const values = form.getFieldsValue();
      const client = GetApiClient();
      
      await client.api.prompt.create_prompt.post({
        ...values,
        content: content,
        promptClassId: values.promptClassId,
        isPublic: values.isPublic || false
      });
      
      messageApi.success("创建成功");
      navigate("/app/prompt/list");
    } catch (e) {
      console.error("创建失败:", e);
      messageApi.error("创建失败");
    } finally {
      setSaving(false);
    }
  };

  // 取消创建
  const handleCancel = () => {
    navigate("/app/prompt/list");
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
          height: 400,
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
            setVditorInstance(vditor);
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
  }, []);

  return (
    <>
      {contextHolder}
      <Card>
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>新增提示词</Title>
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
        
        <div style={{ marginTop: 24 }}>
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
                  height: '400px',
                  fontSize: '14px'
                }}
              />
            </Form.Item>
          </Form>
        </div>
      </Card>
    </>
  );
} 