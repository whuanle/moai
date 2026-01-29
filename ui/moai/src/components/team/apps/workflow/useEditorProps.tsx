import { useMemo, useCallback } from 'react';
import {
  FreeLayoutProps,
  WorkflowNodeProps,
  WorkflowNodeRenderer,
  Field,
  useNodeRender,
  useClientContext,
} from '@flowgram.ai/free-layout-editor';
import { createMinimapPlugin } from '@flowgram.ai/minimap-plugin';
import { createFreeSnapPlugin } from '@flowgram.ai/free-snap-plugin';
import { nodeRegistries } from './nodeRegistries';
import { WorkflowJSON } from '@flowgram.ai/free-layout-editor';
import { Dropdown, message } from 'antd';
import type { MenuProps } from 'antd';
import { DeleteOutlined, CopyOutlined, EditOutlined } from '@ant-design/icons';

// 节点渲染组件
function NodeRenderer(props: WorkflowNodeProps) {
  const { form } = useNodeRender();
  const { document } = useClientContext();
  const [messageApi] = message.useMessage();

  // 复制节点处理函数
  const handleCopy = useCallback(() => {
    try {
      const node = props.node;
      const nodeData = node.toJSON?.() || node;
      const position = nodeData.meta?.position || { x: 0, y: 0 };
      
      const newNode = {
        ...nodeData,
        id: `${node.type}_${Date.now()}`,
        meta: {
          ...nodeData.meta,
          position: {
            x: position.x + 50,
            y: position.y + 50,
          },
        },
      };
      document.addNode(newNode);
      messageApi.success('节点已复制');
    } catch (error) {
      console.error('复制节点失败:', error);
      messageApi.error('复制节点失败');
    }
  }, [props.node, document, messageApi]);

  // 删除节点处理函数
  const handleDelete = useCallback(() => {
    try {
      // 尝试多种删除方法
      const doc = document as unknown as {
        deleteNode?: (id: string) => void;
        removeNode?: (id: string) => void;
      };
      const node = props.node as unknown as {
        id: string;
        remove?: () => void;
      };
      
      if (typeof doc.deleteNode === 'function') {
        doc.deleteNode(node.id);
      } else if (typeof doc.removeNode === 'function') {
        doc.removeNode(node.id);
      } else if (typeof node.remove === 'function') {
        node.remove();
      } else {
        throw new Error('未找到删除节点的方法');
      }
      messageApi.success('节点已删除');
    } catch (error) {
      console.error('删除节点失败:', error);
      messageApi.error('删除节点失败');
    }
  }, [props.node, document, messageApi]);

  // 右键菜单项
  const menuItems: MenuProps['items'] = [
    {
      key: 'edit',
      label: '编辑节点',
      icon: <EditOutlined />,
      onClick: () => {
        messageApi.info('编辑功能开发中');
      },
    },
    {
      key: 'copy',
      label: '复制节点',
      icon: <CopyOutlined />,
      onClick: handleCopy,
    },
    {
      type: 'divider',
    },
    {
      key: 'delete',
      label: '删除节点',
      icon: <DeleteOutlined />,
      danger: true,
      onClick: handleDelete,
    },
  ];

  return (
    <Dropdown menu={{ items: menuItems }} trigger={['contextMenu']}>
      <div>
        <WorkflowNodeRenderer className="workflow-node" node={props.node}>
          {form?.render()}
        </WorkflowNodeRenderer>
      </div>
    </Dropdown>
  );
}

export const useEditorProps = (initialData: WorkflowJSON) =>
  useMemo<FreeLayoutProps>(
    () => ({
      // 启用背景网格
      background: true,
      // 非只读模式
      readonly: false,
      // 初始数据
      initialData,
      // 画布配置
      playgroundConfig: {
        zoom: 1,
        minZoom: 0.1,
        maxZoom: 3,
        // 启用画布拖拽
        enablePan: true,
        // 启用鼠标滚轮缩放
        enableMouseWheelZoom: true,
        // 启用触摸板手势
        enableTouchPad: true,
      },
      // 节点类型注册
      nodeRegistries,
      // 默认节点注册
      getNodeDefaultRegistry(type) {
        return {
          type,
          meta: {
            defaultExpanded: true,
          },
          formMeta: {
            // 节点表单渲染
            render: () => (
              <>
                <Field<string> name="title">
                  {({ field }) => (
                    <div className="workflow-node-title">{field.value}</div>
                  )}
                </Field>
                <div className="workflow-node-content">
                  <Field<string> name="content">
                    {({ field }) => (
                      <div className="workflow-node-desc">{field.value}</div>
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
        renderDefaultNode: (props: WorkflowNodeProps) => {
          return <NodeRenderer {...props} />;
        },
      },
      // 内容变更回调
      onContentChange(_ctx, event) {
        // 工作流数据变更
      },
      // 启用节点表单引擎
      nodeEngine: {
        enable: true,
      },
      // 启用历史记录
      history: {
        enable: true,
        enableChangeNode: true, // 监听节点引擎数据变化
      },
      // 初始化回调
      onInit: (_ctx) => {
        // 编辑器初始化完成
      },
      // 渲染完成回调
      onAllLayersRendered(ctx) {
        // 适应视图
        ctx.document.fitView(false);
      },
      // 销毁回调
      onDispose() {
        // 编辑器已销毁
      },
      // 插件配置
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
          edgeColor: '#1677ff',
          alignColor: '#1677ff',
          edgeLineWidth: 1,
          alignLineWidth: 1,
          alignCrossWidth: 8,
        }),
      ],
    }),
    [initialData]
  );
