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

/**
 * 排序组件使用规范
 * 
 * 【排序行为】
 * - 点击排序字段后立即在页面起效（前端排序），无需请求后端
 * - 点击刷新按钮时，将排序字段传递到后端进行服务端排序
 * 
 * 【使用方式】
 * 1. 定义排序字段：
 *    const sortFields: SortField[] = [
 *      { key: 'fieldName', label: '显示名称' },
 *    ];
 * 
 * 2. 创建排序状态：
 *    const [sortState, setSortState] = useState<SortState>({});
 * 
 * 3. 前端排序函数（点击排序时立即生效）：
 *    const sortedList = useMemo(() => {
 *      const list = [...dataList];
 *      const sortEntries = Object.entries(sortState).filter(([_, v]) => v);
 *      if (sortEntries.length === 0) return list;
 *      
 *      return list.sort((a, b) => {
 *        for (const [key, order] of sortEntries) {
 *          const aVal = a[key] ?? '';
 *          const bVal = b[key] ?? '';
 *          const cmp = String(aVal).localeCompare(String(bVal), 'zh-CN');
 *          if (cmp !== 0) return order === 'asc' ? cmp : -cmp;
 *        }
 *        return 0;
 *      });
 *    }, [dataList, sortState]);
 * 
 * 4. 后端排序参数构建（刷新时传递）：
 *    const buildOrderByFields = (sortState: SortState): KeyValueBool[] | undefined => {
 *      const fields: KeyValueBool[] = [];
 *      Object.entries(sortState).forEach(([key, value]) => {
 *        if (value) {
 *          fields.push({ key, value: value === 'asc' });
 *        }
 *      });
 *      return fields.length > 0 ? fields : undefined;
 *    };
 * 
 * 5. 在刷新请求中使用：
 *    await client.api.xxx.list.post({
 *      orderByFields: buildOrderByFields(sortState),
 *    });
 */

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
