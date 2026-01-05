/**
 * 创建插件抽屉组件
 */
import { Drawer, Button, Space } from "antd";
import { FullscreenOutlined } from "@ant-design/icons";
import type { FormInstance } from "antd";
import CreatePluginContent from "./CreatePluginContent";

interface CreatePluginDrawerProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  /** 切换到独立页面 */
  onSwitchToFullPage?: () => void;
  onOpenCodeEditor?: (
    fieldKey: string,
    currentValue: string,
    formInstance: FormInstance
  ) => void;
}

export default function CreatePluginDrawer({
  open,
  onClose,
  onSuccess,
  onSwitchToFullPage,
  onOpenCodeEditor,
}: CreatePluginDrawerProps) {
  return (
    <Drawer
      title={
        <Space>
          <span>选择模板创建插件</span>
          {onSwitchToFullPage && (
            <Button
              type="text"
              size="middle"
              icon={<FullscreenOutlined />}
              onClick={onSwitchToFullPage}
              title="在独立页面打开"
            >在独立页面打开</Button>
          )}
        </Space>
      }
      placement="right"
      onClose={onClose}
      open={open}
      width={1400}
      destroyOnClose
      maskClosable={false}
      className="plugin-drawer"
    >
      {open && (
        <CreatePluginContent
          onClose={onClose}
          onSuccess={onSuccess}
          onOpenCodeEditor={onOpenCodeEditor}
        />
      )}
    </Drawer>
  );
}
