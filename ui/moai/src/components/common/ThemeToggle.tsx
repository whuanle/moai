import React from 'react';
import { Button, Tooltip } from 'antd';
import { SunOutlined, MoonOutlined } from '@ant-design/icons';
import { useTheme } from '../../hooks/useTheme';
import './ThemeToggle.css';

/**
 * 主题切换按钮组件
 * 显示在导航栏用户头像左侧
 */
export default function ThemeToggle() {
  const { theme, toggleTheme, isDark } = useTheme();

  return (
    <Tooltip title={isDark ? '切换到亮色模式' : '切换到暗色模式'} placement="bottom">
      <Button
        type="text"
        icon={isDark ? <SunOutlined /> : <MoonOutlined />}
        onClick={toggleTheme}
        className="theme-toggle-button"
        aria-label={isDark ? '切换到亮色模式' : '切换到暗色模式'}
      />
    </Tooltip>
  );
}
