import { Button, Form, Input, Select, Tag, Space } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, QueryBar, DataTable } from '@/design-system'
import type { TableColumnsType } from 'antd'

interface Item {
  id: number
  name: string
  status: 'active' | 'disabled'
  createdAt: string
}

const mockData: Item[] = [
  { id: 1, name: '示例应用 A', status: 'active', createdAt: '2026-08-01' },
  { id: 2, name: '示例应用 B', status: 'disabled', createdAt: '2026-08-02' },
]

export function ListTemplate() {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)

  const columns: TableColumnsType<Item> = [
    { title: t('ds.list.name'), dataIndex: 'name' },
    {
      title: t('ds.list.status'),
      dataIndex: 'status',
      render: (s: Item['status']) => (
        <Tag color={s === 'active' ? 'green' : 'default'}>
          {s === 'active' ? t('ds.list.statusActive') : t('ds.list.statusDisabled')}
        </Tag>
      ),
    },
    { title: t('ds.list.createdAt'), dataIndex: 'createdAt' },
    {
      title: t('ds.list.actions'),
      key: 'actions',
      render: () => (
        <Space>
          <Button type="link">{t('ds.list.edit')}</Button>
          <Button type="link" danger>{t('ds.list.delete')}</Button>
        </Space>
      ),
    },
  ]

  return (
    <Page title={t('ds.list.title')} subtitle={t('ds.list.subtitle')}>
      <QueryBar
        loading={loading}
        onSearch={() => setLoading(true)}
        onReset={() => setLoading(false)}
      >
        <Form.Item name="name" label={t('ds.list.nameLabel')}>
          <Input placeholder={t('ds.list.namePlaceholder')} />
        </Form.Item>
        <Form.Item name="status" label={t('ds.list.status')}>
          <Select
            allowClear
            style={{ width: 160 }}
            options={[
              { value: 'active', label: t('ds.list.statusActive') },
              { value: 'disabled', label: t('ds.list.statusDisabled') },
            ]}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<Item>
        rowKey="id"
        columns={columns}
        dataSource={mockData}
        loading={loading}
        toolbar={<Button type="primary">{t('ds.list.create')}</Button>}
        onRefresh={() => setLoading(true)}
        refreshLoading={loading}
      />
    </Page>
  )
}
