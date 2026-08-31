import {
  Alert,
  App,
  Badge,
  Button,
  Card,
  Checkbox,
  Col,
  DatePicker,
  Divider,
  Input,
  InputNumber,
  Popconfirm,
  Radio,
  Row,
  Select,
  Space,
  Switch,
  Tag,
  Typography,
} from 'antd'
import {
  DownloadOutlined,
  PlusOutlined,
  ReloadOutlined,
  SettingOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Chat, DataTable, StatCard } from '@/design-system'
import {
  brandColors,
  neutralColors,
  radius,
  spacing,
  fontSize,
  fontFamily,
  colorPrimary,
  controlHeight,
} from '@/design-system/theme'
import type { TableColumnsType } from 'antd'
import type { ReactNode } from 'react'
import { ListTemplate } from '@/design-system/templates/ListTemplate'
import { FormTemplate } from '@/design-system/templates/FormTemplate'
import { DetailTemplate } from '@/design-system/templates/DetailTemplate'
import { DashboardTemplate } from '@/design-system/templates/DashboardTemplate'
import { ChatTemplate } from '@/design-system/templates/ChatTemplate'

const { Text } = Typography

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card title={title} style={{ marginBottom: spacing.lg }}>
      {children}
    </Card>
  )
}

function ColorSwatch() {
  const colors = Object.entries(brandColors)
  const neutrals = Object.entries(neutralColors)
  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: spacing.md }}>
        {colors.map(([name, value]) => (
          <div key={name}>
            <div
              style={{
                height: 48,
                backgroundColor: value,
                borderRadius: radius.default,
                border: `1px solid ${neutralColors.border}`,
              }}
            />
            <div style={{ marginTop: spacing.xs }}>
              <Text strong style={{ fontSize: fontSize.sm }}>{name}</Text>
              <br />
              <Text type="secondary" style={{ fontSize: fontSize.xs }}>{value}</Text>
            </div>
          </div>
        ))}
      </div>
      <Divider />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: spacing.md }}>
        {neutrals.map(([name, value]) => (
          <div key={name}>
            <div
              style={{
                height: 48,
                backgroundColor: value,
                borderRadius: radius.default,
                border: `1px solid ${neutralColors.border}`,
              }}
            />
            <div style={{ marginTop: spacing.xs }}>
              <Text strong style={{ fontSize: fontSize.sm }}>{name}</Text>
              <br />
              <Text type="secondary" style={{ fontSize: fontSize.xs }}>{value}</Text>
            </div>
          </div>
        ))}
      </div>
    </Space>
  )
}

function SpacingScale() {
  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      {Object.entries(spacing).map(([name, value]) => (
        <div key={name} style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
          <Text style={{ width: 60 }} type="secondary">{name}</Text>
          <div style={{ width: Math.min(value * 4, 320), height: 20, backgroundColor: colorPrimary, borderRadius: radius.sm }} />
          <Text style={{ width: 40 }}>{value}px</Text>
        </div>
      ))}
    </Space>
  )
}

function TypographyScale() {
  const sizes: Array<[string, number, ReactNode]> = [
    ['xxl', 24, '一级标题 Heading'],
    ['xl', 20, '二级标题 Heading'],
    ['lg', 16, '三级标题 / 正文重点'],
    ['md', 14, '正文 Body'],
    ['sm', 13, '辅助文本 Secondary'],
    ['xs', 12, '注释 Caption'],
  ]
  return (
    <Space direction="vertical" size="small">
      <Text type="secondary" style={{ fontSize: fontSize.xs }}>
        Font family: {fontFamily}
      </Text>
      {sizes.map(([name, size, content]) => (
        <div key={name}>
          <Text style={{ fontSize: size as number }}>{content}</Text>
          <Text type="secondary" style={{ marginLeft: spacing.md, fontSize: fontSize.xs }}>
            {name} / {size}px
          </Text>
        </div>
      ))}
    </Space>
  )
}

function ButtonsSection() {
  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Button type="primary">主要按钮</Button>
        <Button>默认按钮</Button>
        <Button type="dashed">虚线按钮</Button>
        <Button type="text">文本按钮</Button>
        <Button type="link">链接按钮</Button>
      </Space>
      <Space wrap>
        <Button type="primary" loading>加载中</Button>
        <Button type="primary" disabled>禁用</Button>
        <Button type="primary" danger>危险按钮</Button>
        <Button type="primary" icon={<PlusOutlined />}>带图标</Button>
        <Button type="primary" size="large">大尺寸</Button>
        <Button size="small">小尺寸</Button>
      </Space>
      <Space wrap>
        <Button type="primary" shape="circle" icon={<ReloadOutlined />} />
        <Button type="primary" shape="round">圆角按钮</Button>
        <Button type="primary" icon={<DownloadOutlined />}>下载</Button>
        <Button type="primary" icon={<SettingOutlined />}>设置</Button>
      </Space>
    </Space>
  )
}

function FormsSection() {
  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} md={12}>
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Input placeholder="文本输入" prefix={<UserOutlined />} />
          <Input placeholder="密码输入" type="password" />
          <InputNumber style={{ width: '100%' }} placeholder="数字输入" />
          <DatePicker placeholder="选择日期" style={{ width: '100%' }} />
          <Select
            placeholder="下拉选择"
            style={{ width: '100%' }}
            options={[
              { value: 'a', label: '选项 A' },
              { value: 'b', label: '选项 B' },
            ]}
          />
        </Space>
      </Col>
      <Col xs={24} md={12}>
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <div>
            <div style={{ marginBottom: spacing.xs }}>
              <Text type="secondary">开关 Switch</Text>
            </div>
            <Switch checkedChildren="开" unCheckedChildren="关" />
          </div>
          <div>
            <div style={{ marginBottom: spacing.xs }}>
              <Text type="secondary">单选 Radio</Text>
            </div>
            <Radio.Group defaultValue="a">
              <Radio value="a">单选 A</Radio>
              <Radio value="b">单选 B</Radio>
            </Radio.Group>
          </div>
          <div>
            <div style={{ marginBottom: spacing.xs }}>
              <Text type="secondary">多选 Checkbox</Text>
            </div>
            <Checkbox.Group
              options={[
                { label: '选项 1', value: '1' },
                { label: '选项 2', value: '2' },
                { label: '选项 3', value: '3' },
              ]}
            />
          </div>
          <Input.TextArea placeholder="多行文本" autoSize={{ minRows: 2, maxRows: 4 }} />
        </Space>
      </Col>
    </Row>
  )
}

function TagsSection() {
  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Tag color="success">成功</Tag>
        <Tag color="warning">警告</Tag>
        <Tag color="error">错误</Tag>
        <Tag color="processing">处理中</Tag>
        <Tag color="default">默认</Tag>
        <Tag>普通标签</Tag>
      </Space>
      <Space wrap size="large">
        <Badge count={5}>
          <Button type="primary" shape="circle" icon={<UserAddOutlined />} />
        </Badge>
        <Badge dot>
          <Button shape="circle" icon={<UserOutlined />} />
        </Badge>
        <Badge count={99} overflowCount={9} status="processing" text="进行中" />
      </Space>
    </Space>
  )
}

function CardsSection() {
  return (
    <Row gutter={[16, 16]}>
      {[
        { title: '用户数', value: 128, icon: <UserOutlined />, trend: 12.5 },
        { title: '应用数', value: 32, icon: <SettingOutlined />, trend: -3 },
        { title: '调用次数', value: 2048, icon: <ReloadOutlined />, trend: 15 },
        { title: '待处理', value: 7, icon: <DownloadOutlined /> },
      ].map((item) => (
        <Col xs={24} sm={12} lg={6} key={item.title}>
          <StatCard title={item.title} value={item.value} icon={item.icon} trend={item.trend} />
        </Col>
      ))}
    </Row>
  )
}

interface Item {
  id: number
  name: string
  owner: string
  status: 'active' | 'disabled'
  createdAt: string
}

const mockData: Item[] = [
  { id: 1, name: '智能客服助手', owner: '张三', status: 'active', createdAt: '2026-08-01' },
  { id: 2, name: '文档知识库', owner: '李四', status: 'active', createdAt: '2026-08-02' },
  { id: 3, name: '数据分析工作流', owner: '王五', status: 'disabled', createdAt: '2026-08-05' },
  { id: 4, name: '营销文案生成', owner: '赵六', status: 'active', createdAt: '2026-08-09' },
]

function TableSection() {
  const { t } = useTranslation()
  const columns: TableColumnsType<Item> = [
    { title: '名称', dataIndex: 'name' },
    { title: '负责人', dataIndex: 'owner' },
    {
      title: '状态',
      dataIndex: 'status',
      render: (s: Item['status']) => (
        <Tag color={s === 'active' ? 'success' : 'default'}>
          {s === 'active' ? '启用' : '停用'}
        </Tag>
      ),
    },
    { title: '创建时间', dataIndex: 'createdAt' },
    {
      title: '操作',
      key: 'actions',
      render: () => (
        <Space>
          <Button type="link" size="small">编辑</Button>
          <Button type="link" size="small" danger>删除</Button>
        </Space>
      ),
    },
  ]
  return (
    <DataTable<Item>
      rowKey="id"
      columns={columns}
      dataSource={mockData}
      toolbar={<Button type="primary" icon={<PlusOutlined />}>{t('ds.list.create')}</Button>}
      onRefresh={() => {}}
      pagination={{ showSizeChanger: true }}
    />
  )
}

function FeedbackSection() {
  const { message, notification, modal } = App.useApp()
  return (
    <Space wrap size="middle">
      <Button onClick={() => message.success('操作成功')}>Message 成功</Button>
      <Button onClick={() => message.error('发生错误')}>Message 错误</Button>
      <Button onClick={() => notification.info({ message: '通知', description: '这是一条通知内容。' })}>
        Notification
      </Button>
      <Button onClick={() => modal.info({ title: 'Modal 提示', content: '这是一条模态提示。' })}>
        Modal
      </Button>
      <Popconfirm title="确认删除？" description="删除后不可恢复。" onConfirm={() => message.success('已删除')}>
        <Button type="primary" danger>Popconfirm</Button>
      </Popconfirm>
    </Space>
  )
}

function ChatSection() {
  const { t } = useTranslation()
  const messages = [
    { id: '1', role: 'user' as const, content: '帮我总结这份文档的核心要点。' },
    { id: '2', role: 'assistant' as const, content: '好的，要点如下：1) 统一设计 token；2) 组件强约束；3) 模板快速复用。' },
    { id: '3', role: 'system' as const, content: '上下文已更新。' },
  ]
  return (
    <Chat
      messages={messages}
      inputValue="请输入你的提问..."
      height={320}
      empty={t('ds.chat.empty')}
    />
  )
}

function TemplatesSection() {
  return (
    <div>
      <ListTemplate />
      <Divider />
      <FormTemplate />
      <Divider />
      <DetailTemplate />
      <Divider />
      <DashboardTemplate />
      <Divider />
      <ChatTemplate />
    </div>
  )
}

export function DesignSystemPreview() {
  const { t } = useTranslation()
  return (
    <div style={{ padding: spacing.lg }}>
      <Alert
        type="info"
        showIcon
        message={t('ds.preview.title')}
        description={
          <>
            {t('ds.preview.desc')}
            <div style={{ marginTop: spacing.xs }}>
              控件高度 {controlHeight}px，圆角 {radius.default}px，常规内边距 {spacing.md}px。
            </div>
          </>
        }
        style={{ marginBottom: spacing.lg }}
      />
      <Section title="色彩令牌 Tokens">
        <ColorSwatch />
      </Section>
      <Section title="间距 Spacing / 字号 Typography">
        <Row gutter={[24, 24]}>
          <Col xs={24} lg={12}>
            <SpacingScale />
          </Col>
          <Col xs={24} lg={12}>
            <TypographyScale />
          </Col>
        </Row>
      </Section>
      <Section title="按钮 Buttons">
        <ButtonsSection />
      </Section>
      <Section title="表单控件 Form">
        <FormsSection />
      </Section>
      <Section title="标签与徽章 Tag / Badge">
        <TagsSection />
      </Section>
      <Section title="统计卡片 StatCard">
        <CardsSection />
      </Section>
      <Section title="数据表格 DataTable">
        <TableSection />
      </Section>
      <Section title="响应反馈 Feedback">
        <FeedbackSection />
      </Section>
      <Section title="对话 Chat">
        <ChatSection />
      </Section>
      <Section title="页面模板 Templates">
        <TemplatesSection />
      </Section>
    </div>
  )
}
