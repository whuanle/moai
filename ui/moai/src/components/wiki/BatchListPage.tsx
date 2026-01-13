import { useState, useEffect, useCallback } from 'react';
import { useParams } from 'react-router';
import { 
  Card, 
  Button, 
  Table, 
  Tag, 
  Space, 
  message, 
  Spin, 
  Popconfirm,
  Typography,
  Empty
} from 'antd';
import { ReloadOutlined, DeleteOutlined } from '@ant-design/icons';
import { GetApiClient } from '../ServiceClient';
import { 
  QueryWikiBatchProcessDocumentListCommand,
  QueryWikiBatchProcessDocumentListCommandResponse,
  WikiBatchProcessDocumenItem,
  DeleteBatchProcessDocumentCommand,
  WorkerState,
  WorkerStateObject
} from '../../apiClient/models';
import { proxyRequestError } from '../../helper/RequestError';
import { formatDateTime, formatRelativeTime } from '../../helper/DateTimeHelper';

const { Text } = Typography;

export default function BatchListPage() {
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  
  const [loading, setLoading] = useState(false);
  const [tasks, setTasks] = useState<WikiBatchProcessDocumenItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取批处理任务列表
  const fetchTaskList = useCallback(async () => {
    if (!wikiId) {
      messageApi.error('缺少知识库ID');
      return;
    }

    setLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: QueryWikiBatchProcessDocumentListCommand = {
        wikiId: wikiId,
      };

      const response: QueryWikiBatchProcessDocumentListCommandResponse | undefined = 
        await client.api.wiki.batch.query_list.post(requestBody);
      
      if (response?.items) {
        setTasks(response.items);
      } else {
        setTasks([]);
      }
    } catch (error) {
      console.error('获取批处理任务列表失败:', error);
      proxyRequestError(error, messageApi, '获取批处理任务列表失败');
    } finally {
      setLoading(false);
    }
  }, [wikiId, messageApi]);

  // 页面初始化
  useEffect(() => {
    if (wikiId) {
      fetchTaskList();
    }
  }, [wikiId, fetchTaskList]);

  // 处理删除/取消任务
  const handleDelete = useCallback(async (task: WikiBatchProcessDocumenItem, isCancel: boolean = false) => {
    if (!wikiId || !task.taskId) {
      messageApi.error('缺少必要的参数');
      return;
    }

    try {
      const client = GetApiClient();
      const deleteBody: DeleteBatchProcessDocumentCommand = {
        taskId: task.taskId,
        wikiId: wikiId,
        isCancal: isCancel,
        isDelete: !isCancel,
      };

      await client.api.wiki.batch.deletePath.post(deleteBody);
      messageApi.success(isCancel ? '取消成功' : '删除成功');
      fetchTaskList();
    } catch (error) {
      console.error('删除/取消任务失败:', error);
      proxyRequestError(error, messageApi, isCancel ? '取消失败' : '删除失败');
    }
  }, [wikiId, messageApi, fetchTaskList]);

  // 获取状态标签
  const getStateTag = (state: WorkerState | null | undefined) => {
    const stateMap: Record<string, { color: string; text: string }> = {
      [WorkerStateObject.None]: { color: 'default', text: '未开始' },
      [WorkerStateObject.Wait]: { color: 'default', text: '等待中' },
      [WorkerStateObject.Processing]: { color: 'processing', text: '处理中' },
      [WorkerStateObject.Successful]: { color: 'success', text: '成功' },
      [WorkerStateObject.Failed]: { color: 'error', text: '失败' },
      [WorkerStateObject.Cancal]: { color: 'default', text: '已取消' },
    };
    const stateInfo = stateMap[state || ''] || { color: 'default', text: '未知' };
    return <Tag color={stateInfo.color}>{stateInfo.text}</Tag>;
  };

  // 表格列定义
  const columns = [
    {
      title: '任务ID',
      dataIndex: 'taskId',
      key: 'taskId',
      width: 200,
      ellipsis: true,
      render: (taskId: string) => taskId || '-',
    },
    {
      title: '状态',
      dataIndex: 'state',
      key: 'state',
      width: 120,
      render: (state: WorkerState) => getStateTag(state),
    },
    {
      title: '消息',
      dataIndex: 'message',
      key: 'message',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '自定义数据',
      dataIndex: 'data',
      key: 'data',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '创建时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
      render: (time: string) => time ? formatDateTime(time) : '-',
    },
    {
      title: '创建人',
      dataIndex: 'createUserName',
      key: 'createUserName',
      width: 120,
      render: (name: string) => name || '-',
    },
    {
      title: '更新时间',
      dataIndex: 'updateTime',
      key: 'updateTime',
      width: 180,
      render: (time: string) => formatRelativeTime(time),
    },
    {
      title: '更新人',
      dataIndex: 'updateUserName',
      key: 'updateUserName',
      width: 120,
      render: (name: string) => name || '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 200,
      fixed: 'right' as const,
      render: (_: any, record: WikiBatchProcessDocumenItem) => {
        const isProcessing = record.state === WorkerStateObject.Processing || record.state === WorkerStateObject.Wait;
        const isCompleted = record.state === WorkerStateObject.Successful || record.state === WorkerStateObject.Failed || record.state === WorkerStateObject.Cancal;
        
        return (
          <Space size="middle">
            {isProcessing && (
              <Popconfirm
                title="确认取消"
                description="确定要取消这个任务吗？"
                onConfirm={() => handleDelete(record, true)}
                okText="确定"
                cancelText="取消"
              >
                <Button 
                  type="link" 
                  size="small" 
                  danger
                >
                  取消
                </Button>
              </Popconfirm>
            )}
            {isCompleted && (
              <Popconfirm
                title="确认删除"
                description="确定要删除这个任务吗？"
                onConfirm={() => handleDelete(record, false)}
                okText="确定"
                cancelText="取消"
              >
                <Button 
                  type="link" 
                  size="small" 
                  danger
                  icon={<DeleteOutlined />}
                >
                  删除
                </Button>
              </Popconfirm>
            )}
          </Space>
        );
      },
    },
  ];

  if (!wikiId) {
    return (
      <Card>
        <Text type="secondary">缺少知识库ID</Text>
      </Card>
    );
  }

  return (
    <div>
      {contextHolder}
      
      {/* 头部操作区域 */}
      <Card>
        <Space>
          <Button 
            icon={<ReloadOutlined />} 
            onClick={fetchTaskList} 
            loading={loading}
          >
            刷新
          </Button>
        </Space>
      </Card>

      {/* 任务列表 */}
      <Card style={{ marginTop: 16 }}>
        <Spin spinning={loading}>
          <Table
            columns={columns}
            dataSource={tasks}
            rowKey="taskId"
            pagination={false}
            scroll={{ x: 1200 }}
            locale={{
              emptyText: (
                <Empty
                  description="暂无批处理任务"
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                />
              ),
            }}
          />
        </Spin>
      </Card>
    </div>
  );
}

