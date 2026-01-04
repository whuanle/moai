import { useState } from 'react';
import { Button, Popover } from 'antd';
import { SortAscendingOutlined, SortDescendingOutlined, SwapOutlined } from '@ant-design/icons';
import './SortPopover.css';

export type SortOrder = 'asc' | 'desc' | null;

export interface SortField {
  key: string;
  label: string;
}

export interface SortState {
  [key: string]: SortOrder;
}

interface SortPopoverProps {
  fields: SortField[];
  value: SortState;
  onChange: (value: SortState) => void;
}

export default function SortPopover({ fields, value, onChange }: SortPopoverProps) {
  const [open, setOpen] = useState(false);

  // 切换排序：null -> desc -> asc -> null
  const toggleSort = (key: string) => {
    const current = value[key];
    let next: SortOrder;
    if (current === null || current === undefined) next = 'desc';
    else if (current === 'desc') next = 'asc';
    else next = null;
    onChange({ ...value, [key]: next });
  };

  // 获取排序图标
  const getSortIcon = (order: SortOrder) => {
    if (order === 'desc') return <SortDescendingOutlined className="sort-icon active" />;
    if (order === 'asc') return <SortAscendingOutlined className="sort-icon active" />;
    return <span className="sort-icon inactive">-</span>;
  };

  // 检查是否有激活的排序
  const hasActiveSort = fields.some(f => value[f.key]);

  const content = (
    <div className="sort-popover-content">
      {fields.map(field => (
        <div
          key={field.key}
          className={`sort-item ${value[field.key] ? 'active' : ''}`}
          onClick={() => toggleSort(field.key)}
        >
          <span className="sort-item-label">{field.label}</span>
          {getSortIcon(value[field.key])}
        </div>
      ))}
    </div>
  );

  return (
    <Popover
      content={content}
      title="排序"
      trigger="click"
      placement="bottomLeft"
      open={open}
      onOpenChange={setOpen}
    >
      <Button icon={<SwapOutlined />} type={hasActiveSort ? 'primary' : 'default'}>
        排序
      </Button>
    </Popover>
  );
}
