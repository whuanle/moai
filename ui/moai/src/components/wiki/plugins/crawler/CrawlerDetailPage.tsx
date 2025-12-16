import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import { Card, Button, Table, Tag, Space, message, Spin, Typography } from 'antd';
import { PlayCircleOutlined, StopOutlined, ReloadOutlined, ArrowLeftOutlined } from '@ant-design/icons';
import { GetApiClient } from '../../../ServiceClient';
import { 
  QueryWikiCrawlerPageTasksCommandResponse,
  WikiCrawlerPageItem,
  WikiCrawlerTask,
  StartWikiCrawlerPluginTaskCommand,
  CancalWikiPluginTaskCommand,
  WorkerState,
  WorkerStateObject
} from '../../../../apiClient/models';

const { Title, Text } = Typography;

export default function CrawlerDetailPage() {
  const navigate = useNavigate();
  const { id, configId } = useParams<{ id: string; configId: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  const crawlerConfigId = configId ? parseInt(configId) : undefined;
  
  const [loading, setLoading] = useState(false);
  const [task, setTask] = useState<WikiCrawlerTask | null>(null);
  const [pages, setPages] = useState<WikiCrawlerPageItem[]>([]);
  const [isWorking, setIsWorking] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [starting, setStarting] = useState(false);
  const [stopping, setStopping] = useState(false);

  // 获取爬虫状态和任务列表
  const fetchPageState = async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error('缺少必要参数');
      return;
    }

    setLoading(true);
    try {
      const client = GetApiClient();
      const response: QueryWikiCrawlerPageTasksCommandResponse | undefined = 
        await client.api.wiki.plugin.crawler.query_page_state.get({
          queryParameters: {
            configId: crawlerConfigId,
            wikiId: wikiId,
          }
        });
      
      if (response) {
        setTask(response.task || null);
        setPages(response.pages || []);
        // 判断是否在工作：task存在且state是Processing或Wait
        const taskState = response.task?.state;
        setIsWorking(
          taskState === WorkerStateObject.Processing || 
          taskState === WorkerStateObject.Wait
        );
      } else {
        setTask(null);
        setPages([]);
        setIsWorking(false);
      }
    } catch (error) {
      console.error('获取爬虫状态失败:', error);
      messageApi.error('获取爬虫状态失败');
    } finally {
      setLoading(false);
    }
  };

  // 启动爬虫
  const handleStart = async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error('缺少必要参数');
      return;
    }

    setStarting(true);
    try {
      const client = GetApiClient();
      const requestBody: StartWikiCrawlerPluginTaskCommand = {
        configId: crawlerConfigId,
        wikiId: wikiId,
      };

      await client.api.wiki.plugin.crawler.lanuch_task.post(requestBody);
      messageApi.success('爬虫已启动');
      // 延迟一下再刷新状态，给服务端一些时间
      setTimeout(() => {
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error('启动爬虫失败:', error);
      messageApi.error('启动爬虫失败');
    } finally {
      setStarting(false);
    }
  };

  // 停止爬虫
  const handleStop = async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error('缺少必要参数');
      return;
    }

    setStopping(true);
    try {
      const client = GetApiClient();
      const requestBody: CancalWikiPluginTaskCommand = {
        configId: crawlerConfigId,
        wikiId: wikiId,
      };

      await client.api.wiki.plugin.cancel_task.post(requestBody);
      messageApi.success('爬虫已停止');
      // 延迟一下再刷新状态
      setTimeout(() => {
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error('停止爬虫失败:', error);
      messageApi.error('停止爬虫失败');
    } finally {
      setStopping(false);
    }
  };

  // 页面初始化
  useEffect(() => {
    if (wikiId && crawlerConfigId) {
      fetchPageState();
    }
  }, [wikiId, crawlerConfigId]);

  // 任务状态表格列定义
  const pageColumns = [
    {
      title: 'URL',
      dataIndex: 'url',
      key: 'url',
      ellipsis: true,
      render: (text: string) => (
        <a href={text} target="_blank" rel="noopener noreferrer">
          {text || '-'}
        </a>
      ),
    },
    {
      title: '状态',
      dataIndex: 'crawleState',
      key: 'crawleState',
      width: 120,
      render: (state: WorkerState) => {
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
      },
    },
    {
      title: '文件名',
      dataIndex: 'fileName',
      key: 'fileName',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '文件大小',
      dataIndex: 'fileSize',
      key: 'fileSize',
      width: 120,
      render: (size: number) => {
        if (!size) return '-';
        if (size < 1024) return `${size} B`;
        if (size < 1024 * 1024) return `${(size / 1024).toFixed(2)} KB`;
        return `${(size / (1024 * 1024)).toFixed(2)} MB`;
      },
    },
    {
      title: '是否向量化',
      dataIndex: 'isEmbedding',
      key: 'isEmbedding',
      width: 100,
      render: (isEmbedding: boolean) => (
        <Tag color={isEmbedding ? 'success' : 'default'}>
          {isEmbedding ? '是' : '否'}
        </Tag>
      ),
    },
    {
      title: '消息',
      dataIndex: 'message',
      key: 'message',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '创建时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString() : '-',
    },
  ];

  if (!wikiId || !crawlerConfigId) {
    return (
      <Card>
        <Text type="secondary">缺少必要参数</Text>
      </Card>
    );
  }

  // 获取任务状态显示
  const getTaskStateDisplay = () => {
    if (!task) return { color: 'default', text: '未启动' };
    const stateMap: Record<string, { color: string; text: string }> = {
      [WorkerStateObject.None]: { color: 'default', text: '未开始' },
      [WorkerStateObject.Wait]: { color: 'default', text: '等待中' },
      [WorkerStateObject.Processing]: { color: 'processing', text: '处理中' },
      [WorkerStateObject.Successful]: { color: 'success', text: '成功' },
      [WorkerStateObject.Failed]: { color: 'error', text: '失败' },
      [WorkerStateObject.Cancal]: { color: 'default', text: '已取消' },
    };
    return stateMap[task.state || ''] || { color: 'default', text: '未知' };
  };

  const taskStateDisplay = getTaskStateDisplay();

  return (
    <div>
      {contextHolder}
      
      {/* 头部操作区域 */}
      <Card>
        <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
          <Button 
            icon={<ArrowLeftOutlined />} 
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/crawler`)}
          >
            返回列表
          </Button>
          <Space>
            <Button 
              type="primary"
              icon={<PlayCircleOutlined />} 
              onClick={handleStart}
              loading={starting}
              disabled={isWorking}
            >
              启动
            </Button>
            <Button 
              danger
              icon={<StopOutlined />} 
              onClick={handleStop}
              loading={stopping}
              disabled={!isWorking}
            >
              停止
            </Button>
            <Button 
              icon={<ReloadOutlined />} 
              onClick={fetchPageState}
              loading={loading}
            >
              刷新
            </Button>
          </Space>
        </Space>

        {/* 任务状态信息 */}
        {task && (
          <Card size="small" style={{ marginTop: 16 }}>
            <Space direction="vertical" style={{ width: '100%' }} size="small">
              <div>
                <Text strong>任务状态：</Text>
                <Tag color={taskStateDisplay.color} style={{ marginLeft: 8 }}>
                  {taskStateDisplay.text}
                </Tag>
              </div>
              <div>
                <Text strong>任务ID：</Text>
                <Text code>{task.taskId || '-'}</Text>
              </div>
              <div>
                <Text strong>当前地址：</Text>
                <Text>{task.address || '-'}</Text>
              </div>
              <div>
                <Text strong>成功页面数：</Text>
                <Text type="success">{task.pageCount || 0}</Text>
                <Text style={{ marginLeft: 16 }} strong>失败页面数：</Text>
                <Text type="danger">{task.faildPageCount || 0}</Text>
              </div>
              {task.message && (
                <div>
                  <Text strong>消息：</Text>
                  <Text>{task.message}</Text>
                </div>
              )}
              {task.createTime && (
                <div>
                  <Text strong>创建时间：</Text>
                  <Text>{new Date(task.createTime).toLocaleString()}</Text>
                </div>
              )}
            </Space>
          </Card>
        )}
      </Card>

      {/* 任务列表 */}
      <Card style={{ marginTop: 16 }}>
        <Title level={5}>正在爬取的任务列表</Title>
        <Spin spinning={loading}>
          <Table
            columns={pageColumns}
            dataSource={pages}
            rowKey="id"
            pagination={{
              total: pages.length,
              pageSize: 10,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条记录`,
            }}
          />
        </Spin>
      </Card>
    </div>
  );
}

