import React, { useEffect, useState, useCallback, useRef } from "react";
import {
  Button,
  Card,
  Modal,
  Form,
  Input,
  Space,
  message,
  Tag,
  Checkbox,
  Row,
  Col,
  Typography,
  Divider,
  Popconfirm,
  Tooltip,
  Empty,
  Select,
  Switch,
  Layout,
  List,
  Avatar,
  Descriptions,
  Badge,
  Alert,
  Spin
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SettingOutlined,
  EyeOutlined,
  CopyOutlined,
  UserOutlined,
  CalendarOutlined,
  CheckOutlined,
  CloseOutlined,
  ReloadOutlined,
  FullscreenOutlined,
  FullscreenExitOutlined,
  LockOutlined,
  GlobalOutlined
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import Vditor from "vditor";
import "vditor/dist/index.css";
import ReactMarkdown from "react-markdown";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;
const { Content } = Layout;

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

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
}

// 权限检查 Hook
const usePermissionCheck = (currentUserId: number | null, isAdmin: boolean) => {
  const canEditPrompt = useCallback((prompt: PromptItem) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId;
  }, [currentUserId]);

  const canDeletePrompt = useCallback((prompt: PromptItem) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId;
  }, [currentUserId]);

  return { canEditPrompt, canDeletePrompt };
};

// 分类管理 Hook
const useClassManagement = () => {
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);
  const [classModalOpen, setClassModalOpen] = useState(false);
  const [editingClassId, setEditingClassId] = useState<number | null>(null);
  const [newClassName, setNewClassName] = useState('');
  const [editingClassName, setEditingClassName] = useState('');
  const [classModalLoading, setClassModalLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchClassList = useCallback(async () => {
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
  }, [messageApi]);

  const handleCreateClass = async () => {
    if (!newClassName.trim()) {
      messageApi.error("请输入分类名称");
      return;
    }
    setClassModalLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.create_class.post({ name: newClassName.trim() });
      messageApi.success("创建成功");
      setNewClassName('');
      fetchClassList();
    } catch (e) {
      messageApi.error("创建失败");
    } finally {
      setClassModalLoading(false);
    }
  };

  const handleUpdateClass = async () => {
    if (!editingClassName.trim()) {
      messageApi.error("请输入分类名称");
      return;
    }
    setClassModalLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.update_class.post({ 
        id: editingClassId, 
        name: editingClassName.trim() 
      });
      messageApi.success("修改成功");
      setEditingClassId(null);
      setEditingClassName('');
      fetchClassList();
    } catch (e) {
      messageApi.error("修改失败");
    } finally {
      setClassModalLoading(false);
    }
  };

  const handleDeleteClass = async (cls: PromptClass) => {
    setClassModalLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.delete_class.delete({ promptId: cls.id });
      messageApi.success("删除成功");
      fetchClassList();
    } catch (e) {
      messageApi.error("删除失败");
    } finally {
      setClassModalLoading(false);
    }
  };

  return {
    classList,
    classLoading,
    classModalOpen,
    editingClassId,
    newClassName,
    editingClassName,
    classModalLoading,
    contextHolder,
    setClassModalOpen,
    setEditingClassId,
    setNewClassName,
    setEditingClassName,
    fetchClassList,
    handleCreateClass,
    handleUpdateClass,
    handleDeleteClass
  };
};

// 提示词管理 Hook
const usePromptManagement = (classList: PromptClass[], selectedClassId: number | null, fetchPromptList: () => void) => {
  const [promptModalOpen, setPromptModalOpen] = useState(false);
  const [editingPrompt, setEditingPrompt] = useState<PromptItem | null>(null);
  const [promptForm] = Form.useForm();
  const [promptModalLoading, setPromptModalLoading] = useState(false);
  const [selectedPromptClassId, setSelectedPromptClassId] = useState<number | null>(null);
  const [promptContent, setPromptContent] = useState('');
  const [vditorInstance, setVditorInstance] = useState<Vditor | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const vditorId = useRef(`vditor-${Math.random().toString(36).substr(2, 9)}`);
  const [messageApi, contextHolder] = message.useMessage();

  const openPromptModal = () => {
    if (classList.length === 0) {
      messageApi.warning("请先创建分类");
      return;
    }
    setEditingPrompt(null);
    setSelectedPromptClassId(selectedClassId || classList[0]?.id);
    setPromptContent('');
    setIsFullscreen(false);
    setPromptModalOpen(true);
    setTimeout(() => {
      promptForm.setFieldsValue({ 
        name: '', 
        description: '',
        promptClassId: selectedClassId || classList[0]?.id,
        isPublic: false
      });
    }, 0);
  };

  const handleEditPrompt = async (prompt: PromptItem) => {
    if (classList.length === 0) {
      messageApi.warning("请先创建分类");
      return;
    }
    
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: prompt.id }
      });
      
      if (res) {
        const updatedPrompt: PromptItem = {
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
          isPublic: res.isPublic || false
        };
        
        setEditingPrompt(updatedPrompt);
        setSelectedPromptClassId(selectedClassId || classList[0]?.id);
        setPromptContent(updatedPrompt.content || '');
        setIsFullscreen(false);
        promptForm.setFieldsValue({
          name: updatedPrompt.name,
          description: updatedPrompt.description,
          promptClassId: selectedClassId || classList[0]?.id,
          isPublic: updatedPrompt.isPublic || false
        });
        setPromptModalOpen(true);
      } else {
        messageApi.error("获取提示词详情失败");
      }
    } catch (e) {
      messageApi.error("获取提示词详情失败");
    }
  };

  const handlePromptModalOk = async () => {
    try {
      await promptForm.validateFields();
      const content = vditorInstance?.getValue() || '';
      if (!content.trim()) {
        messageApi.error("请输入提示词内容");
        return;
      }
      setPromptModalLoading(true);
      const values = promptForm.getFieldsValue();
      const client = GetApiClient();
      if (editingPrompt) {
        await client.api.prompt.update_prompt.post({ 
          ...values, 
          content: content,
          promptId: editingPrompt.id,
          promptClassId: selectedPromptClassId,
          isPublic: values.isPublic || false
        });
        messageApi.success("修改成功");
      } else {
        await client.api.prompt.create_prompt.post({
          ...values,
          content: content,
          promptClassId: selectedPromptClassId,
          isPublic: values.isPublic || false
        });
        messageApi.success("新增成功");
      }
      setPromptModalOpen(false);
      setPromptContent('');
      setVditorInstance(null);
      setIsFullscreen(false);
      fetchPromptList();
    } catch (e) {
      // 校验失败或接口异常
    } finally {
      setPromptModalLoading(false);
    }
  };

  return {
    promptModalOpen,
    editingPrompt,
    promptForm,
    promptModalLoading,
    selectedPromptClassId,
    promptContent,
    vditorInstance,
    isFullscreen,
    vditorId,
    contextHolder,
    setPromptModalOpen,
    setSelectedPromptClassId,
    setPromptContent,
    setVditorInstance,
    setIsFullscreen,
    openPromptModal,
    handleEditPrompt,
    handlePromptModalOk
  };
};

// 提示词查看 Hook
const usePromptView = () => {
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedPrompt, setSelectedPrompt] = useState<PromptItem | null>(null);
  const [previewModalOpen, setPreviewModalOpen] = useState(false);
  const [previewPrompt, setPreviewPrompt] = useState<PromptItem | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const handleViewPrompt = async (prompt: PromptItem) => {
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: prompt.id }
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
          isPublic: res.isPublic || false
        };
        setSelectedPrompt(promptItem);
        setDetailModalOpen(true);
      } else {
        messageApi.error("获取提示词详情失败");
      }
    } catch (e) {
      messageApi.error("获取提示词详情失败");
    }
  };

  const handlePreviewPrompt = async (prompt: PromptItem) => {
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: prompt.id }
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
          isPublic: res.isPublic || false
        };
        setPreviewPrompt(promptItem);
        setIsFullscreen(false);
        setPreviewModalOpen(true);
      } else {
        messageApi.error("获取提示词详情失败");
      }
    } catch (e) {
      messageApi.error("获取提示词详情失败");
    }
  };

  const handleCopyContent = (content: string) => {
    navigator.clipboard.writeText(content).then(() => {
      messageApi.success("已复制到剪贴板");
    }).catch(() => {
      messageApi.error("复制失败");
    });
  };

  return {
    detailModalOpen,
    selectedPrompt,
    previewModalOpen,
    previewPrompt,
    isFullscreen,
    contextHolder,
    setDetailModalOpen,
    setPreviewModalOpen,
    setIsFullscreen,
    handleViewPrompt,
    handlePreviewPrompt,
    handleCopyContent
  };
};

export default function PromptList() {
  // 用户信息
  const [isAdmin, setIsAdmin] = useState(false);
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  // 分类相关
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);
  const [showOwnOnly, setShowOwnOnly] = useState(true);

  // 提示词相关
  const [promptList, setPromptList] = useState<PromptItem[]>([]);
  const [promptLoading, setPromptLoading] = useState(false);

  // 获取当前用户信息
  const fetchUserInfo = useCallback(async () => {
    try {
      const client = GetApiClient();
      const res = await client.api.common.userinfo.get();
      setIsAdmin(res?.isAdmin === true);
      setCurrentUserId(res?.userId || null);
    } catch (e) {
      setIsAdmin(false);
      setCurrentUserId(null);
    }
  }, []);

  // 获取提示词列表
  const fetchPromptList = useCallback(async (classId?: number | null) => {
    setPromptLoading(true);
    try {
      const client = GetApiClient();
      const req: any = {
        isOwn: showOwnOnly
      };
      if (classId) req.classId = classId;
      const res = await client.api.prompt.prompt_list.post(req);
      setPromptList(
        (res?.items || [])
          .filter((item: any) => typeof item.id === 'number' && item.id != null)
          .map((item: any) => ({
            id: item.id ?? 0,
            name: item.name || '',
            description: item.description || '',
            content: item.content || '',
            createTime: item.createTime || '',
            createUserName: item.createUserName || '',
            createUserId: item.createUserId || 0,
            updateTime: item.updateTime || '',
            updateUserName: item.updateUserName || '',
            isOwn: item.isOwn || false,
            isPublic: item.isPublic || false,
          }))
      );
    } catch (e) {
      messageApi.error("获取提示词失败");
    } finally {
      setPromptLoading(false);
    }
  }, [showOwnOnly, messageApi]);

  // 页面初始化
  useEffect(() => {
    fetchUserInfo();
  }, [fetchUserInfo]);

  useEffect(() => {
    fetchPromptList(selectedClassId);
  }, [fetchPromptList, selectedClassId]);

  // 权限检查
  const { canEditPrompt, canDeletePrompt } = usePermissionCheck(currentUserId, isAdmin);

  // 分类管理
  const classManagement = useClassManagement();

  // 提示词管理
  const promptManagement = usePromptManagement(classManagement.classList, selectedClassId, () => fetchPromptList(selectedClassId));

  // 提示词查看
  const promptView = usePromptView();

  // 切换分类
  const handleClassSelect = (id: number | null) => {
    setSelectedClassId(id);
  };

  // 切换"我创建的"筛选
  const handleOwnFilterChange = (checked: boolean) => {
    setShowOwnOnly(checked);
  };

  // 刷新数据
  const handleRefresh = async () => {
    await Promise.all([
      classManagement.fetchClassList(),
      fetchPromptList(selectedClassId)
    ]);
  };

  // 删除提示词
  const handleDeletePrompt = async (prompt: PromptItem) => {
    Modal.confirm({
      title: `确定删除提示词"${prompt.name}"吗？`,
      content: "删除后无法恢复，请确认操作",
      onOk: async () => {
        try {
          const client = GetApiClient();
          await client.api.prompt.delete_prompt.delete({ promptId: prompt.id });
          messageApi.success("删除成功");
          fetchPromptList(selectedClassId);
        } catch (e) {
          console.error("删除失败:", e);
          messageApi.error("删除失败");
        }
      },
    });
  };

  // 渲染 Markdown 预览
  useEffect(() => {
    if (promptView.detailModalOpen && promptView.selectedPrompt) {
      const previewElement = document.getElementById('prompt-preview');
      if (previewElement && promptView.selectedPrompt.content) {
        // 简单显示 Markdown 内容
        previewElement.innerHTML = `<pre style="margin: 0; white-space: pre-wrap; word-break: break-word; font-family: monospace; font-size: 14px; line-height: 1.5;">${promptView.selectedPrompt.content}</pre>`;
      }
    }
  }, [promptView.detailModalOpen, promptView.selectedPrompt]);

  // 初始化 Vditor 编辑器
  useEffect(() => {
    if (promptManagement.promptModalOpen) {
      // 使用 setTimeout 确保 DOM 元素完全渲染
      const timer = setTimeout(() => {
        // 确保容器存在
        const container = document.getElementById(promptManagement.vditorId.current);
        if (!container) return;

        // 清除可能存在的旧实例
        if (promptManagement.vditorInstance) {
          promptManagement.vditorInstance.destroy();
          promptManagement.setVditorInstance(null);
        }

        // 添加自定义样式来减小字体大小
        const style = document.createElement('style');
        style.textContent = `
          #${promptManagement.vditorId.current} .vditor-ir,
          #${promptManagement.vditorId.current} .vditor-wysiwyg,
          #${promptManagement.vditorId.current} .vditor-sv {
            font-size: 14px !important;
            line-height: 1.6 !important;
          }
          #${promptManagement.vditorId.current} .vditor-toolbar {
            font-size: 13px !important;
          }
        `;
        document.head.appendChild(style);

        try {
          const vditor = new Vditor(promptManagement.vditorId.current, {
            placeholder: "请输入提示词内容，支持 Markdown 格式",
            height: promptManagement.isFullscreen ? 'calc(100vh - 320px)' : 400,
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
              // 初始化完成后设置值
              if (promptManagement.promptContent) {
                vditor.setValue(promptManagement.promptContent);
              }
              promptManagement.setVditorInstance(vditor);
            },
          });
        } catch (error) {
          console.error('Vditor initialization error:', error);
        }

        return () => {
          // 清理自定义样式
          const existingStyle = document.querySelector(`style[data-vditor-style="${promptManagement.vditorId.current}"]`);
          if (existingStyle) {
            existingStyle.remove();
          }
        };
      }, 100);

      return () => {
        clearTimeout(timer);
        if (promptManagement.vditorInstance) {
          promptManagement.vditorInstance.destroy();
          promptManagement.setVditorInstance(null);
        }
        // 清理自定义样式
        const existingStyle = document.querySelector(`style[data-vditor-style="${promptManagement.vditorId.current}"]`);
        if (existingStyle) {
          existingStyle.remove();
        }
      };
    } else {
      // 当模态框关闭时清理实例
      if (promptManagement.vditorInstance) {
        promptManagement.vditorInstance.destroy();
        promptManagement.setVditorInstance(null);
      }
      // 清理自定义样式
      const existingStyle = document.querySelector(`style[data-vditor-style="${promptManagement.vditorId.current}"]`);
      if (existingStyle) {
        existingStyle.remove();
      }
    }
  }, [promptManagement.promptModalOpen, promptManagement.promptContent]);

  // 分类管理弹窗内容
  const renderClassModal = () => (
    <Modal
      open={classManagement.classModalOpen}
      title="分类管理"
      onCancel={() => classManagement.setClassModalOpen(false)}
      footer={null}
      destroyOnClose
      width={500}
      maskClosable={false}
      keyboard={false}
    >
      <List
        size="small"
        dataSource={[
          {
            id: 'new',
            name: '',
            isNew: true
          },
          ...classManagement.classList
        ]}
        renderItem={(item: any) => {
          if (item.isNew) {
            return (
              <List.Item>
                <Space.Compact style={{ width: '100%' }}>
                  <Input
                    placeholder="输入分类名称"
                    value={classManagement.newClassName}
                    onChange={(e) => classManagement.setNewClassName(e.target.value)}
                    onPressEnter={classManagement.handleCreateClass}
                  />
                  <Button
                    type="primary"
                    icon={<CheckOutlined />}
                    onClick={classManagement.handleCreateClass}
                    loading={classManagement.classModalLoading}
                  />
                  <Button
                    icon={<CloseOutlined />}
                    onClick={() => classManagement.setNewClassName('')}
                  />
                </Space.Compact>
              </List.Item>
            );
          }

          const isEditing = classManagement.editingClassId === item.id;
          return (
            <List.Item
              actions={[
                isEditing ? (
                  <Space key="edit-actions">
                    <Button
                      type="primary"
                      size="small"
                      icon={<CheckOutlined />}
                      onClick={classManagement.handleUpdateClass}
                      loading={classManagement.classModalLoading}
                    />
                    <Button
                      size="small"
                      icon={<CloseOutlined />}
                      onClick={() => {
                        classManagement.setEditingClassId(null);
                        classManagement.setEditingClassName('');
                      }}
                    />
                  </Space>
                ) : (
                  <Space key="view-actions">
                    <Button
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => {
                        classManagement.setEditingClassId(item.id);
                        classManagement.setEditingClassName(item.name);
                      }}
                    />
                                         <Popconfirm
                       title={`确定删除分类"${item.name}"吗？`}
                       description="删除分类后，该分类下的提示词将变为未分类状态"
                       onConfirm={() => classManagement.handleDeleteClass(item)}
                       okText="确定"
                       cancelText="取消"
                     >
                       <Button
                         size="small"
                         danger
                         icon={<DeleteOutlined />}
                         loading={classManagement.classModalLoading}
                       />
                     </Popconfirm>
                  </Space>
                )
              ]}
            >
              <List.Item.Meta
                title={
                  isEditing ? (
                    <Input
                      value={classManagement.editingClassName}
                      onChange={(e) => classManagement.setEditingClassName(e.target.value)}
                      onPressEnter={classManagement.handleUpdateClass}
                      autoFocus
                    />
                  ) : (
                    <Text strong>{item.name}</Text>
                  )
                }
              />
            </List.Item>
          );
        }}
      />
    </Modal>
  );

  // 提示词管理弹窗内容
  const renderPromptModal = () => (
    <Modal
      open={promptManagement.promptModalOpen}
      title={
        <Space>
          <span>{promptManagement.editingPrompt ? "编辑提示词" : "新增提示词"}</span>
          <Button
            type="text"
            icon={promptManagement.isFullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
            onClick={() => promptManagement.setIsFullscreen(!promptManagement.isFullscreen)}
            title={promptManagement.isFullscreen ? "退出全屏" : "全屏"}
          />
        </Space>
      }
      onCancel={() => {
        promptManagement.setPromptModalOpen(false);
        promptManagement.setPromptContent('');
        promptManagement.setVditorInstance(null);
        promptManagement.setIsFullscreen(false);
      }}
      onOk={promptManagement.handlePromptModalOk}
      confirmLoading={promptManagement.promptModalLoading}
      footer={null}
      destroyOnClose
      width={promptManagement.isFullscreen ? '100vw' : 1200}
      style={promptManagement.isFullscreen ? { 
        top: 0, 
        paddingBottom: 0, 
        maxWidth: '100vw',
        height: '100vh'
      } : {}}
      bodyStyle={promptManagement.isFullscreen ? { 
        height: 'calc(100vh - 140px)', 
        overflow: 'hidden',
        padding: '16px'
      } : {}}
      maskClosable={false}
      keyboard={false}
    >
      <Form
        form={promptManagement.promptForm}
        layout="vertical"
        initialValues={promptManagement.editingPrompt || {
          name: '',
          description: '',
          content: '',
          promptClassId: promptManagement.selectedPromptClassId || selectedClassId || classManagement.classList[0]?.id,
          isPublic: false
        }}
        key={promptManagement.editingPrompt ? promptManagement.editingPrompt.id : 'new'}
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
                value={promptManagement.selectedPromptClassId}
                onChange={(value) => promptManagement.setSelectedPromptClassId(value)}
                options={classManagement.classList.map(cls => ({
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
            defaultChecked={false}
          />
        </Form.Item>
        <Form.Item 
          label="提示词内容" 
          required
        >
          <div 
            id={promptManagement.vditorId.current}
            className="vditor" 
            style={{ 
              height: promptManagement.isFullscreen ? 'calc(100vh - 320px)' : '400px',
              fontSize: '14px'
            }}
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button type="primary" onClick={promptManagement.handlePromptModalOk} loading={promptManagement.promptModalLoading}>
              保存
            </Button>
            <Button onClick={() => promptManagement.setPromptModalOpen(false)}>取消</Button>
          </Space>
        </Form.Item>
      </Form>
    </Modal>
  );

  // 提示词详情弹窗
  const renderDetailModal = () => (
    <Modal
      open={promptView.detailModalOpen}
      title={promptView.selectedPrompt?.name}
      onCancel={() => promptView.setDetailModalOpen(false)}
      footer={[
        <Button key="copy" icon={<CopyOutlined />} onClick={() => promptView.handleCopyContent(promptView.selectedPrompt?.content || '')}>
          复制内容
        </Button>,
        <Button key="close" onClick={() => promptView.setDetailModalOpen(false)}>
          关闭
        </Button>
      ]}
      width={1000}
      maskClosable={false}
      keyboard={false}
    >
      {promptView.selectedPrompt && (
        <div>
          <div style={{ marginBottom: 16 }}>
            <Text type="secondary">{promptView.selectedPrompt.description}</Text>
          </div>
          <div style={{ marginBottom: 16 }}>
            <Space>
              <Text type="secondary">
                <UserOutlined /> {promptView.selectedPrompt.createUserName}
              </Text>
              <Text type="secondary">
                <CalendarOutlined /> {formatDateTime(promptView.selectedPrompt.createTime)}
              </Text>
              {promptView.selectedPrompt.isPublic ? (
                <Tag color="green" icon={<GlobalOutlined />}>公开</Tag>
              ) : (
                <Tag color="orange" icon={<LockOutlined />}>私密</Tag>
              )}
            </Space>
          </div>
          <Divider />
          <div 
            id="prompt-preview"
            style={{
              background: '#f5f5f5',
              padding: 16,
              borderRadius: 6,
              maxHeight: 400,
              overflow: 'auto'
            }}
          />
        </div>
      )}
    </Modal>
  );

  // 预览模态窗口
  const renderPreviewModal = () => (
    <Modal
      open={promptView.previewModalOpen}
      title={
        <Space>
          <span>{promptView.previewPrompt?.name}</span>
          <Button
            type="text"
            icon={promptView.isFullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
            onClick={() => promptView.setIsFullscreen(!promptView.isFullscreen)}
            title={promptView.isFullscreen ? "退出全屏" : "全屏"}
          />
        </Space>
      }
      onCancel={() => {
        promptView.setPreviewModalOpen(false);
        promptView.setIsFullscreen(false);
      }}
      footer={[
        <Button key="copy" icon={<CopyOutlined />} onClick={() => promptView.handleCopyContent(promptView.previewPrompt?.content || '')}>
          复制内容
        </Button>,
        <Button 
          key="edit" 
          icon={<EditOutlined />} 
          disabled={!canEditPrompt(promptView.previewPrompt!)}
          onClick={async () => {
            if (canEditPrompt(promptView.previewPrompt!)) {
              promptView.setPreviewModalOpen(false);
              promptView.setIsFullscreen(false);
              await promptManagement.handleEditPrompt(promptView.previewPrompt!);
            }
          }}
        >
          编辑
        </Button>,
        <Button key="close" onClick={() => {
          promptView.setPreviewModalOpen(false);
          promptView.setIsFullscreen(false);
        }}>
          关闭
        </Button>
      ]}
      width={promptView.isFullscreen ? '100vw' : 1000}
      style={promptView.isFullscreen ? { 
        top: 0, 
        paddingBottom: 0, 
        maxWidth: '100vw',
        height: '100vh'
      } : {}}
      bodyStyle={promptView.isFullscreen ? { 
        height: 'calc(100vh - 140px)', 
        overflow: 'hidden',
        padding: '16px'
      } : {}}
      maskClosable={false}
      keyboard={false}
    >
      {promptView.previewPrompt && (
        <div>
          {promptView.previewPrompt.description && (
            <div style={{ marginBottom: 16 }}>
              <Text type="secondary">{promptView.previewPrompt.description}</Text>
            </div>
          )}
          <div style={{ marginBottom: 16 }}>
            <Space>
              <Text type="secondary">
                <UserOutlined /> {promptView.previewPrompt.createUserName}
              </Text>
              <Text type="secondary">
                <CalendarOutlined /> {formatDateTime(promptView.previewPrompt.createTime)}
              </Text>
              {promptView.previewPrompt.isPublic ? (
                <Tag color="green" icon={<GlobalOutlined />}>公开</Tag>
              ) : (
                <Tag color="orange" icon={<LockOutlined />}>私密</Tag>
              )}
            </Space>
          </div>
          <Divider />
          <div 
            style={{
              background: '#f8f9fa',
              padding: 20,
              borderRadius: 8,
              height: promptView.isFullscreen ? 'calc(100vh - 280px)' : '500px',
              overflow: 'auto',
              border: '1px solid #e9ecef'
            }}
          >
            <ReactMarkdown
              components={{
                h1: ({node, ...props}) => <h1 style={{fontSize: '24px', marginBottom: '16px'}} {...props} />,
                h2: ({node, ...props}) => <h2 style={{fontSize: '20px', marginBottom: '14px'}} {...props} />,
                h3: ({node, ...props}) => <h3 style={{fontSize: '18px', marginBottom: '12px'}} {...props} />,
                h4: ({node, ...props}) => <h4 style={{fontSize: '16px', marginBottom: '10px'}} {...props} />,
                h5: ({node, ...props}) => <h5 style={{fontSize: '14px', marginBottom: '8px'}} {...props} />,
                h6: ({node, ...props}) => <h6 style={{fontSize: '12px', marginBottom: '6px'}} {...props} />,
                p: ({node, ...props}) => <p style={{marginBottom: '12px', lineHeight: '1.6'}} {...props} />,
                ul: ({node, ...props}) => <ul style={{marginBottom: '12px', paddingLeft: '20px'}} {...props} />,
                ol: ({node, ...props}) => <ol style={{marginBottom: '12px', paddingLeft: '20px'}} {...props} />,
                li: ({node, ...props}) => <li style={{marginBottom: '4px'}} {...props} />,
                blockquote: ({node, ...props}) => (
                  <blockquote style={{
                    borderLeft: '4px solid #1890ff',
                    paddingLeft: '16px',
                    margin: '16px 0',
                    color: '#666',
                    fontStyle: 'italic'
                  }} {...props} />
                ),
                code: ({children, ...props}: any) => {
                  const isInline = !children?.toString().includes('\n');
                  return isInline ? (
                    <code style={{
                      background: '#f1f3f4',
                      padding: '2px 6px',
                      borderRadius: '4px',
                      fontSize: '0.9em',
                      fontFamily: 'Consolas, Monaco, "Andale Mono", monospace'
                    }} {...props}>
                      {children}
                    </code>
                  ) : (
                    <pre style={{
                      background: '#f6f8fa',
                      padding: '16px',
                      borderRadius: '6px',
                      overflow: 'auto',
                      margin: '16px 0',
                      border: '1px solid #e1e4e8'
                    }}>
                      <code style={{
                        fontFamily: 'Consolas, Monaco, "Andale Mono", monospace',
                        fontSize: '14px',
                        lineHeight: '1.5'
                      }} {...props}>
                        {children}
                      </code>
                    </pre>
                  );
                },
                table: ({node, ...props}) => (
                  <div style={{overflow: 'auto', margin: '16px 0'}}>
                    <table style={{
                      borderCollapse: 'collapse',
                      width: '100%',
                      border: '1px solid #e1e4e8'
                    }} {...props} />
                  </div>
                ),
                th: ({node, ...props}) => (
                  <th style={{
                    border: '1px solid #e1e4e8',
                    padding: '8px 12px',
                    background: '#f6f8fa',
                    fontWeight: 'bold',
                    textAlign: 'left'
                  }} {...props} />
                ),
                td: ({node, ...props}) => (
                  <td style={{
                    border: '1px solid #e1e4e8',
                    padding: '8px 12px'
                  }} {...props} />
                ),
              }}
            >
              {promptView.previewPrompt.content || ''}
            </ReactMarkdown>
          </div>
        </div>
      )}
    </Modal>
  );

  // 渲染提示词卡片
  const renderPromptCard = (prompt: PromptItem) => (
    <Col xs={24} sm={12} md={8} lg={6} key={prompt.id}>
      <Card
        hoverable
        style={{ height: '100%', cursor: 'pointer', position: 'relative' }}
        onClick={() => promptView.handlePreviewPrompt(prompt)}
        actions={[
          <Tooltip title="查看详情">
            <EyeOutlined onClick={async (e) => {
              e.stopPropagation();
              await promptView.handleViewPrompt(prompt);
            }} />
          </Tooltip>,
          <Tooltip title={canEditPrompt(prompt) ? "编辑" : "无编辑权限"}>
            <EditOutlined 
              onClick={async (e) => {
                e.stopPropagation();
                console.log("编辑权限检查:", {
                  isAdmin,
                  currentUserId,
                  promptCreateUserId: prompt.createUserId,
                  canEdit: canEditPrompt(prompt)
                });
                if (canEditPrompt(prompt)) {
                  await promptManagement.handleEditPrompt(prompt);
                } else {
                  messageApi.warning("您没有编辑此提示词的权限");
                }
              }}
              style={{ 
                color: canEditPrompt(prompt) ? undefined : '#d9d9d9',
                cursor: canEditPrompt(prompt) ? 'pointer' : 'not-allowed'
              }}
            />
          </Tooltip>,
          <Tooltip title="复制内容">
            <CopyOutlined onClick={async (e) => {
              e.stopPropagation();
              try {
                const client = GetApiClient();
                const res = await client.api.prompt.prompt_content.get({
                  queryParameters: { promptId: prompt.id }
                });
                if (res?.content) {
                  promptView.handleCopyContent(res.content);
                } else {
                  promptView.handleCopyContent(prompt.content || '');
                }
              } catch (e) {
                promptView.handleCopyContent(prompt.content || '');
              }
            }} />
          </Tooltip>,
          <Tooltip title={canDeletePrompt(prompt) ? "删除" : "无删除权限"}>
            <DeleteOutlined
              onClick={async (e) => {
                e.stopPropagation();
                if (canDeletePrompt(prompt)) {
                  await handleDeletePrompt(prompt);
                }
              }}
              style={{ 
                color: canDeletePrompt(prompt) ? '#ff4d4f' : '#d9d9d9',
                cursor: canDeletePrompt(prompt) ? 'pointer' : 'not-allowed'
              }}
            />
          </Tooltip>
        ]}

      >
        <Card.Meta
          title={
            <Space>
              <Text strong ellipsis style={{ flex: 1 }}>
                {prompt.name}
              </Text>
              {prompt.isOwn && (
                <Badge status="processing" text="我的" />
              )}
              {prompt.isPublic ? (
                <Tag color="green" icon={<GlobalOutlined />} style={{ fontSize: '12px' }}>公开</Tag>
              ) : (
                <Tag color="orange" icon={<LockOutlined />} style={{ fontSize: '12px' }}>私密</Tag>
              )}
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              {prompt.description && (
                <Paragraph
                  ellipsis={{ rows: 2 }}
                  style={{ marginBottom: 0, fontSize: '12px' }}
                >
                  {prompt.description}
                </Paragraph>
              )}
              <Space size="small">
                <Avatar size="small" icon={<UserOutlined />} />
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {prompt.createUserName}
                </Text>
              </Space>
              <Space size="small">
                <CalendarOutlined style={{ fontSize: '12px', color: '#999' }} />
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {formatDateTime(prompt.createTime)}
                </Text>
              </Space>
            </Space>
          }
        />
      </Card>
    </Col>
  );

  return (
    <>
      {contextHolder}
      {classManagement.contextHolder}
      {promptManagement.contextHolder}
      {promptView.contextHolder}
      
      <Layout style={{ background: '#f5f5f5', minHeight: '100vh' }}>
        <Content>
          <Card style={{ margin: '16px 24px 0' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Title level={4} style={{ margin: 0 }}>提示词管理</Title>
              <Space>
                <Checkbox
                  checked={showOwnOnly}
                  onChange={(e) => handleOwnFilterChange(e.target.checked)}
                >
                  我创建的
                </Checkbox>
                <Button 
                  icon={<ReloadOutlined />} 
                  onClick={handleRefresh}
                  loading={classManagement.classLoading || promptLoading}
                  title="刷新"
                >
                  刷新
                </Button>
                {isAdmin && (
                  <Button icon={<SettingOutlined />} onClick={() => classManagement.setClassModalOpen(true)}>
                    分类管理
                  </Button>
                )}
                <Button type="primary" icon={<PlusOutlined />} onClick={promptManagement.openPromptModal}>
                  新增提示词
                </Button>
              </Space>
            </div>
          </Card>
          
          <div style={{ padding: '0 24px 24px' }}>
            <Card>
              <Space wrap style={{ marginBottom: 16 }}>
                <Button
                  type={!selectedClassId ? "primary" : "default"}
                  onClick={() => handleClassSelect(null)}
                  loading={classManagement.classLoading}
                >
                  全部
                </Button>
                {classManagement.classList.map((cls) => (
                  <Button
                    key={cls.id}
                    type={selectedClassId === cls.id ? "primary" : "default"}
                    onClick={() => handleClassSelect(cls.id)}
                    loading={classManagement.classLoading}
                  >
                    {cls.name}
                  </Button>
                ))}
              </Space>
              
              <Row gutter={[16, 16]}>
                {promptLoading ? (
                  <Col span={24}>
                    <div style={{ textAlign: 'center', padding: '40px' }}>
                      <Spin size="large" />
                    </div>
                  </Col>
                ) : promptList.length > 0 ? (
                  promptList.map(renderPromptCard)
                ) : (
                  <Col span={24}>
                    <Empty
                      description="暂无提示词"
                      style={{ margin: '40px 0' }}
                    >
                      <Button 
                        type="primary" 
                        icon={<PlusOutlined />} 
                        onClick={promptManagement.openPromptModal}
                        disabled={classManagement.classList.length === 0}
                      >
                        新增提示词
                      </Button>
                    </Empty>
                  </Col>
                )}
              </Row>
            </Card>
          </div>
        </Content>
      </Layout>

      {renderClassModal()}
      {renderPromptModal()}
      {renderDetailModal()}
      {renderPreviewModal()}
    </>
  );
}