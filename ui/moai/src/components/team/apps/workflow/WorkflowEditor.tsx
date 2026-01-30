/**
 * 工作流编辑器 - 重构版
 * 合并 WorkflowConfig.tsx 和 WorkflowEditor.tsx
 */

import { useEffect, useState, useCallback, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router';
import { Button, Space, Typography, message, Spin, Empty, Tag, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { 
  ArrowLeftOutlined, 
  SaveOutlined, 
  PlayCircleOutlined,
  EditOutlined,
  CopyOutlined,
  DeleteOutlined
} from '@ant-design/icons';
import { 
  FreeLayoutEditorProvider, 
  EditorRenderer,
  useClientContext,
  WorkflowNodeProps,
  WorkflowNodeRenderer,
  Field,
  useNodeRender,
  WorkflowNodeRegistry,
} from '@flowgram.ai/free-layout-editor';
import { createMinimapPlugin } from '@flowgram.ai/minimap-plugin';
import { createFreeSnapPlugin } from '@flowgram.ai/free-snap-plugin';
import '@flowgram.ai/free-layout-editor/index.css';

import { useWorkflowStore } from './store';
import { toEditorFormat, fromEditorFormat } from './utils';
import { proxyRequestError } from '../../../../helper/RequestError';
import { NodePanel } from './NodePanel';
import { Toolbar } from './Toolbar';
import { Minimap } from './Minimap';
import { ConfigPanel } from './ConfigPanel';
import { NodeType } from './types';
import { NODE_CONSTRAINTS, getNodeTemplate } from './constants';
import { useSaveWorkflow } from './hooks';
import './WorkflowEditor.css';

const { Title } = Typography;

// ==================== 默认节点渲染器 ====================

function DefaultNodeRenderer(props: WorkflowNodeProps) {
  const { form } = useNodeRender();
  const template = getNodeTemplate(props.node.type as NodeType);
  
  return (
    <WorkflowNodeRenderer 
      className="workflow-node" 
      node={props.node}
      data-node-id={props.node.id}
    >
      {form?.render()}
    </WorkflowNodeRenderer>
  );
}

// ==================== 工具栏组件 ====================

function EditorToolbar() {
  const { handleSave, saving, contextHolder } = useSaveWorkflow();

  const handleRun = () => {
    message.info('工作流执行功能开发中');
  };

  return (
    <>
      {contextHolder}
      <Space>
        <Button icon={<PlayCircleOutlined />} onClick={handleRun}>
          运行
        </Button>
        <Button 
          type="primary" 
          icon={<SaveOutlined />} 
          onClick={handleSave}
          loading={saving}
        >
          保存
        </Button>
        {useWorkflowStore.getState().isDraft && <Tag color="orange">草稿</Tag>}
        {useWorkflowStore.getState().dirty && <Tag color="red">未保存</Tag>}
      </Space>
    </>
  );
}

// ==================== 画布组件 ====================

interface CanvasProps {
  onNodeDoubleClick: (nodeId: string) => void;
  onNodeRightClick: (nodeId: string, event: React.MouseEvent) => void;
}

function Canvas({ onNodeDoubleClick, onNodeRightClick }: CanvasProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const { playground, document } = useClientContext();
  const [selectionMenuVisible, setSelectionMenuVisible] = useState(false);
  const [selectionMenuPosition, setSelectionMenuPosition] = useState({ x: 0, y: 0 });

  // 监听圈选区域的右键菜单（已整合到画布右键事件中，此处移除）
  // useEffect(() => {
  //   ...
  // }, [document]);

  // 圈选右键菜单项
  const selectionMenuItems: MenuProps['items'] = useMemo(() => [
    {
      key: 'delete-selected',
      label: '删除选中节点',
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => {
        const selectedNodes = (document as any).getSelectedNodes?.() || [];
        
        if (selectedNodes.length === 0) {
          messageApi.warning('请先选择要删除的节点');
          setSelectionMenuVisible(false);
          return;
        }

        const nodeIds = selectedNodes.map((node: any) => node.id);
        
        // 检查是否可以删除
        const cannotDeleteNodes: string[] = [];
        const canDeleteNodes: string[] = [];
        
        nodeIds.forEach((id: string) => {
          const canDelete = store.canDeleteNode(id);
          if (typeof canDelete === 'string') {
            cannotDeleteNodes.push(id);
          } else {
            canDeleteNodes.push(id);
          }
        });
        
        if (cannotDeleteNodes.length > 0 && canDeleteNodes.length === 0) {
          messageApi.warning('选中的节点不允许删除');
          setSelectionMenuVisible(false);
          return;
        }
        
        if (cannotDeleteNodes.length > 0) {
          messageApi.warning(`已删除 ${canDeleteNodes.length} 个节点，${cannotDeleteNodes.length} 个节点不允许删除`);
        }
        
        // 批量删除可删除的节点
        if (canDeleteNodes.length > 0) {
          store.deleteNodes(canDeleteNodes);
          
          // 同步到编辑器
          canDeleteNodes.forEach((id: string) => {
            const node = (document as any).getNodeByID?.(id);
            if (node) {
              (document as any).deleteNode?.(node);
            }
          });
          
          if (cannotDeleteNodes.length === 0) {
            messageApi.success(`已删除 ${canDeleteNodes.length} 个节点`);
          }
        }
        
        setSelectionMenuVisible(false);
      },
    },
  ], [document, store, messageApi]);

  // 关闭圈选菜单
  useEffect(() => {
    const handleClick = () => {
      if (selectionMenuVisible) {
        setSelectionMenuVisible(false);
      }
    };

    window.addEventListener('click', handleClick);
    return () => window.removeEventListener('click', handleClick);
  }, [selectionMenuVisible]);

  // 监听键盘事件 - Delete 键删除选中节点
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Delete' && document) {
        // 获取选中的节点 (使用类型断言，因为 API 可能不在类型定义中)
        const selectedNodes = (document as any).getSelectedNodes?.() || [];
        
        if (selectedNodes.length > 0) {
          const nodeIds = selectedNodes.map((node: any) => node.id);
          
          // 检查是否可以删除
          const cannotDeleteNodes: string[] = [];
          const canDeleteNodes: string[] = [];
          
          nodeIds.forEach((id: string) => {
            const canDelete = store.canDeleteNode(id);
            if (typeof canDelete === 'string') {
              cannotDeleteNodes.push(id);
            } else {
              canDeleteNodes.push(id);
            }
          });
          
          if (cannotDeleteNodes.length > 0 && canDeleteNodes.length === 0) {
            messageApi.warning('选中的节点不允许删除');
            return;
          }
          
          if (cannotDeleteNodes.length > 0) {
            messageApi.warning(`已删除 ${canDeleteNodes.length} 个节点，${cannotDeleteNodes.length} 个节点不允许删除`);
          }
          
          // 批量删除可删除的节点
          if (canDeleteNodes.length > 0) {
            store.deleteNodes(canDeleteNodes);
            
            // 同步到编辑器
            canDeleteNodes.forEach((id: string) => {
              const node = (document as any).getNodeByID?.(id);
              if (node) {
                (document as any).deleteNode?.(node);
              }
            });
            
            if (cannotDeleteNodes.length === 0) {
              messageApi.success(`已删除 ${canDeleteNodes.length} 个节点`);
            }
          }
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [document, store, messageApi]);

  // 监听画布点击事件 - 处理节点双击和右键
  useEffect(() => {
    // 尝试多种方式获取画布元素
    const canvasElement = (playground as any)?.canvas || 
                         (playground as any)?.canvasElement ||
                         window.document.querySelector('.workflow-canvas');
    
    console.log('🔍 Canvas element:', canvasElement);
    
    if (!canvasElement) {
      console.warn('⚠️ 未找到画布元素');
      return;
    }

    // 双击事件
    const handleDoubleClick = (e: MouseEvent) => {
      console.log('🖱️ 双击事件触发', e.target);
      const target = e.target as HTMLElement;
      const nodeElement = target.closest('[data-node-id]');
      
      console.log('🔍 找到的节点元素:', nodeElement);
      
      if (nodeElement) {
        const nodeId = nodeElement.getAttribute('data-node-id');
        console.log('✅ 节点 ID:', nodeId);
        if (nodeId) {
          onNodeDoubleClick(nodeId);
        }
      }
    };

    // 右键事件
    const handleContextMenu = (e: MouseEvent) => {
      console.log('🖱️ 右键事件触发', e.target);
      const target = e.target as HTMLElement;
      
      // 打印目标元素的详细信息
      console.log('🔍 目标元素类名:', target.className);
      console.log('🔍 目标元素标签:', target.tagName);
      
      // 检查是否点击的是圈选框
      if (target.className && target.className.includes('gedit-selector-bounds')) {
        console.log('� 点击的是圈选框，检查选中的节点');
        // 获取选中的节点
        const selectedNodes = (document as any).getSelectedNodes?.() || [];
        console.log('📋 选中的节点数量:', selectedNodes.length);
        
        if (selectedNodes.length > 0) {
          // 显示圈选菜单
          e.preventDefault();
          setSelectionMenuPosition({ x: e.clientX, y: e.clientY });
          setSelectionMenuVisible(true);
          console.log('✅ 显示圈选菜单');
        }
        return;
      }
      
      // 尝试多种方式查找节点
      let nodeElement = target.closest('[data-node-id]');
      console.log('🔍 方式1 [data-node-id]:', nodeElement);
      
      if (!nodeElement) {
        // 尝试通过类名查找
        nodeElement = target.closest('.workflow-node');
        console.log('🔍 方式2 .workflow-node:', nodeElement);
      }
      
      if (!nodeElement) {
        // 尝试查找任何包含 node 的类名
        nodeElement = target.closest('[class*="node"]');
        console.log('🔍 方式3 [class*="node"]:', nodeElement);
      }
      
      console.log('🔍 最终找到的节点元素:', nodeElement);
      
      if (nodeElement) {
        e.preventDefault();
        let nodeId = nodeElement.getAttribute('data-node-id');
        
        // 如果没有 data-node-id，尝试从其他属性获取
        if (!nodeId) {
          // 尝试从子元素查找
          const nodeIdElement = nodeElement.querySelector('[data-node-id]');
          if (nodeIdElement) {
            nodeId = nodeIdElement.getAttribute('data-node-id');
            console.log('🔍 从子元素找到 ID:', nodeId);
          }
        }
        
        // 尝试从 data 属性获取
        if (!nodeId && (nodeElement as any).dataset) {
          nodeId = (nodeElement as any).dataset.nodeId;
          console.log('🔍 从 dataset 找到 ID:', nodeId);
        }
        
        console.log('✅ 最终节点 ID:', nodeId);
        if (nodeId) {
          onNodeRightClick(nodeId, e as any);
        } else {
          console.warn('⚠️ 找到节点元素但没有 ID');
        }
      } else {
        console.warn('⚠️ 未找到节点元素');
      }
    };

    canvasElement.addEventListener('dblclick', handleDoubleClick);
    canvasElement.addEventListener('contextmenu', handleContextMenu);

    console.log('✅ 事件监听器已添加');

    return () => {
      canvasElement.removeEventListener('dblclick', handleDoubleClick);
      canvasElement.removeEventListener('contextmenu', handleContextMenu);
      console.log('🗑️ 事件监听器已移除');
    };
  }, [playground, onNodeDoubleClick, onNodeRightClick]);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    
    try {
      const templateData = e.dataTransfer.getData('application/json');
      if (!templateData) return;
      
      const template = JSON.parse(templateData);
      
      // 检查约束
      const canAdd = store.canAddNode(template.type);
      if (typeof canAdd === 'string') {
        messageApi.warning(canAdd);
        return;
      }
      
      // 获取画布坐标
      const canvasPos = playground.config.getPosFromMouseEvent(e.nativeEvent);
      
      // 添加节点
      const nodeId = store.addNode(template.type, canvasPos);
      
      if (nodeId) {
        // 同步到编辑器
        const node = store.getNode(nodeId);
        if (node) {
          document.createWorkflowNode({
            id: node.id,
            type: node.type,
            meta: {
              position: node.position,
            },
            data: {
              title: node.name,
              content: node.description,
              inputFields: node.config.inputFields,
              outputFields: node.config.outputFields,
            },
            blocks: [],
            edges: []
          });
        }
        
        messageApi.success(`已添加 ${template.name} 节点`);
      }
    } catch (error) {
      console.error('添加节点失败:', error);
      messageApi.error('添加节点失败');
    }
  };

  const isEmpty = !store.workflow || store.workflow.nodes.length === 0;

  return (
    <>
      {contextHolder}
      <div 
        className="workflow-canvas"
        onDrop={handleDrop}
        onDragOver={handleDragOver}
      >
        <EditorRenderer />
        {isEmpty && (
          <div className="workflow-empty-hint">
            <div className="empty-hint-content">
              <div className="empty-hint-icon">📋</div>
              <h3>开始设计你的工作流</h3>
              <p>从左侧节点面板拖拽节点到画布上</p>
            </div>
          </div>
        )}
        <Minimap />
        <Toolbar />
      </div>

      {/* 圈选右键菜单 */}
      {selectionMenuVisible && (
        <div
          style={{
            position: 'fixed',
            left: selectionMenuPosition.x,
            top: selectionMenuPosition.y,
            zIndex: 9999,
            background: 'white',
            boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
            borderRadius: '4px',
          }}
        >
          <Dropdown
            menu={{ items: selectionMenuItems }}
            open={true}
            onOpenChange={(open) => {
              if (!open) setSelectionMenuVisible(false);
            }}
          >
            <div style={{ width: 200, height: 1 }} />
          </Dropdown>
        </div>
      )}
    </>
  );
}

// ==================== 主编辑器组件 ====================

export default function WorkflowEditor() {
  const { id, appId } = useParams();
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const store = useWorkflowStore();
  const [initialized, setInitialized] = useState(false);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [contextMenuVisible, setContextMenuVisible] = useState(false);
  const [contextMenuPosition, setContextMenuPosition] = useState({ x: 0, y: 0 });
  const [contextMenuNodeId, setContextMenuNodeId] = useState<string | null>(null);

  const teamId = id ? parseInt(id) : 0;

  // 加载工作流
  useEffect(() => {
    if (!appId || !teamId || isNaN(teamId)) {
      return;
    }

    const loadWorkflow = async () => {
      try {
        await store.load(appId, teamId);
        setInitialized(true);
      } catch (error) {
        console.error('加载工作流失败:', error);
        proxyRequestError(error, messageApi, '加载工作流失败');
      }
    };

    loadWorkflow();

    return () => {
      store.reset();
    };
  }, [appId, teamId]);

  // 未保存警告
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (store.dirty) {
        e.preventDefault();
        e.returnValue = '您有未保存的更改，确定要离开吗？';
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [store.dirty]);

  // 内容变化处理
  const handleContentChange = useCallback((ctx: any) => {
    if (!initialized || !store.workflow) return;
    
    const editorData = ctx.document.toJSON();
    console.log('🔍 handleContentChange - 编辑器原始数据:', editorData);
    
    // 保存编辑器原始数据
    store.saveEditorData(editorData);
    
    // 同时更新 workflow（用于验证和其他逻辑）
    const updatedWorkflow = fromEditorFormat(editorData, store.workflow);
    
    useWorkflowStore.setState({ 
      workflow: updatedWorkflow,
      dirty: true 
    });
  }, [initialized, store.workflow]);

  // 节点注册配置
  const nodeRegistries: WorkflowNodeRegistry[] = useMemo(() => {
    return Object.values(NodeType).map(type => {
      const constraints = NODE_CONSTRAINTS[type];
      return {
        type,
        meta: {
          isStart: type === NodeType.Start,
          deleteDisable: !constraints.deletable,
          copyDisable: !constraints.copyable,
          defaultPorts: [
            ...(constraints.requiresInput ? [{ type: 'input' as const }] : []),
            ...(constraints.requiresOutput ? [{ type: 'output' as const }] : []),
          ],
        },
      };
    });
  }, []);

  // 编辑器配置
  const editorProps = useMemo(() => {
    // 优先使用编辑器原始数据，如果没有则转换 workflow
    const initialData = store.editorRawData 
      ? store.editorRawData 
      : (initialized && store.workflow ? toEditorFormat(store.workflow) : { nodes: [], edges: [] });
    
    console.log('🔍 editorProps - initialData:', {
      hasEditorRawData: !!store.editorRawData,
      hasWorkflow: !!store.workflow,
      dataSource: store.editorRawData ? 'editorRawData' : 'workflow'
    });
    
    return {
      initialData,
      background: true,
      readonly: false,
      nodeRegistries,
      // 节点默认配置
      getNodeDefaultRegistry: (type: string | number) => {
      const typeStr = String(type);
      const template = getNodeTemplate(typeStr as NodeType);
      return {
        type: typeStr,
        meta: {
          defaultExpanded: true,
        },
        formMeta: {
          render: () => (
            <>
              <Field<string> name="title">
                {({ field }) => (
                  <div style={{ 
                    padding: '8px 12px', 
                    background: template?.color || '#1677ff',
                    color: 'white',
                    fontWeight: 500,
                    borderRadius: '8px 8px 0 0'
                  }}>
                    {field.value || template?.name || 'Node'}
                  </div>
                )}
              </Field>
              <div style={{ padding: '8px 12px' }}>
                <Field<string> name="content">
                  {({ field }) => (
                    <div style={{ fontSize: '12px', color: '#666' }}>
                      {field.value || template?.description || ''}
                    </div>
                  )}
                </Field>
              </div>
            </>
          ),
        },
      };
    },
    // 节点渲染
    materials: {
      renderDefaultNode: (props: WorkflowNodeProps) => <DefaultNodeRenderer {...props} />,
    },
    // 启用节点表单引擎
    nodeEngine: {
      enable: true,
    },
    history: {
      enable: true,
      enableChangeNode: true,
    },
    // 渲染完成后适应视图
    onAllLayersRendered: (ctx: any) => {
      if (ctx.document && store.workflow && store.workflow.nodes.length > 0) {
        ctx.document.fitView(false);
      }
    },
    plugins: () => [
      // 缩略图插件
      createMinimapPlugin({
        disableLayer: true,
        canvasStyle: {
          canvasWidth: 182,
          canvasHeight: 102,
          canvasPadding: 50,
          canvasBackground: 'rgba(245, 245, 245, 1)',
          canvasBorderRadius: 10,
          viewportBackground: 'rgba(235, 235, 235, 1)',
          viewportBorderRadius: 4,
          viewportBorderColor: 'rgba(201, 201, 201, 1)',
          viewportBorderWidth: 1,
          viewportBorderDashLength: 2,
          nodeColor: 'rgba(255, 255, 255, 1)',
          nodeBorderRadius: 2,
          nodeBorderWidth: 0.145,
          nodeBorderColor: 'rgba(6, 7, 9, 0.10)',
          overlayColor: 'rgba(255, 255, 255, 0)',
        },
      }),
      // 自动对齐插件
      createFreeSnapPlugin({
        edgeColor: '#00B2B2',
        alignColor: '#00B2B2',
        edgeLineWidth: 1,
        alignLineWidth: 1,
        alignCrossWidth: 8,
      }),
    ],
    onContentChange: handleContentChange,
  };
  }, [initialized, store.workflow, store.editorRawData, nodeRegistries, handleContentChange]);

  const handleBack = () => {
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  // 处理节点双击 - 打开配置面板
  const handleNodeDoubleClick = useCallback((nodeId: string) => {
    setSelectedNodeId(nodeId);
  }, []);

  // 处理节点右键 - 显示上下文菜单
  const handleNodeRightClick = useCallback((nodeId: string, event: React.MouseEvent) => {
    console.log('🎯 handleNodeRightClick 被调用', { nodeId, x: event.clientX, y: event.clientY });
    event.preventDefault();
    setContextMenuNodeId(nodeId);
    setContextMenuPosition({ x: event.clientX, y: event.clientY });
    setContextMenuVisible(true);
    console.log('✅ 右键菜单状态已更新');
  }, []);

  // 上下文菜单项
  const contextMenuItems: MenuProps['items'] = useMemo(() => {
    if (!contextMenuNodeId) return [];

    const node = store.getNode(contextMenuNodeId);
    if (!node) return [];

    const canDelete = store.canDeleteNode(contextMenuNodeId);
    const canCopy = NODE_CONSTRAINTS[node.type]?.copyable;

    return [
      {
        key: 'edit',
        label: '编辑节点',
        icon: <EditOutlined />,
        onClick: () => {
          setSelectedNodeId(contextMenuNodeId);
          setContextMenuVisible(false);
        },
      },
      {
        key: 'copy',
        label: '复制节点',
        icon: <CopyOutlined />,
        disabled: !canCopy,
        onClick: () => {
          const newNodeId = store.copyNode(contextMenuNodeId);
          if (newNodeId) {
            messageApi.success('节点已复制');
          } else {
            messageApi.error('复制节点失败');
          }
          setContextMenuVisible(false);
        },
      },
      {
        type: 'divider',
      },
      {
        key: 'delete',
        label: '删除节点',
        icon: <DeleteOutlined />,
        danger: true,
        disabled: typeof canDelete === 'string',
        onClick: () => {
          const success = store.deleteNode(contextMenuNodeId);
          if (success) {
            messageApi.success('节点已删除');
          } else {
            messageApi.error(typeof canDelete === 'string' ? canDelete : '删除节点失败');
          }
          setContextMenuVisible(false);
        },
      },
    ];
  }, [contextMenuNodeId, store, messageApi]);

  // 关闭上下文菜单
  useEffect(() => {
    const handleClick = () => {
      if (contextMenuVisible) {
        setContextMenuVisible(false);
      }
    };

    window.addEventListener('click', handleClick);
    return () => window.removeEventListener('click', handleClick);
  }, [contextMenuVisible]);

  // 参数验证
  if (!id || !appId) {
    return (
      <div className="workflow-editor-container">
        {contextHolder}
        <Empty description="缺少必要参数">
          <Button type="primary" onClick={() => navigate(-1)}>
            返回
          </Button>
        </Empty>
      </div>
    );
  }

  // 加载中
  if (store.loading) {
    return (
      <div className="workflow-editor-container">
        {contextHolder}
        <div className="workflow-loading">
          <Spin size="large" tip="加载工作流中..." />
        </div>
      </div>
    );
  }

  // 未初始化
  if (!initialized || !store.workflow) {
    return (
      <div className="workflow-editor-container">
        {contextHolder}
        <div className="workflow-loading">
          <Spin size="large" tip="初始化中..." />
        </div>
      </div>
    );
  }

  return (
    <div className="workflow-editor-container">
      {contextHolder}
      
      <FreeLayoutEditorProvider key={initialized ? 'ready' : 'loading'} {...editorProps}>
        {/* 头部 */}
        <div className="workflow-editor-header">
          <Space>
            <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
            <Title level={4} style={{ margin: 0 }}>
              {store.workflow.name || '流程编排配置'}
            </Title>
          </Space>
          <EditorToolbar />
        </div>

        {/* 主内容区 */}
        <div className="workflow-editor-content">
          <NodePanel />
          <Canvas 
            onNodeDoubleClick={handleNodeDoubleClick}
            onNodeRightClick={handleNodeRightClick}
          />
          {selectedNodeId && (
            <ConfigPanel 
              nodeId={selectedNodeId} 
              onClose={() => setSelectedNodeId(null)} 
            />
          )}
        </div>

        {/* 右键菜单 */}
        {contextMenuVisible && (
          <div
            style={{
              position: 'fixed',
              left: contextMenuPosition.x,
              top: contextMenuPosition.y,
              zIndex: 9999,
              background: 'white',
              boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
              borderRadius: '4px',
            }}
          >
            <Dropdown
              menu={{ items: contextMenuItems }}
              open={true}
              onOpenChange={(open) => {
                console.log('📋 Dropdown onOpenChange:', open);
                if (!open) setContextMenuVisible(false);
              }}
            >
              <div style={{ width: 200, height: 1 }} />
            </Dropdown>
          </div>
        )}
      </FreeLayoutEditorProvider>
    </div>
  );
}
