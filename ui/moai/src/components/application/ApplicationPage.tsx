import { Card, Empty, Button } from "antd";
import { AppstoreAddOutlined } from "@ant-design/icons";
import "./ApplicationPage.css";

/**
 * 应用页面 - 空白占位页面
 */
export default function ApplicationPage() {
  return (
    <div className="application-page">
      <Card>
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description="应用功能开发中..."
        >
          <Button type="primary" icon={<AppstoreAddOutlined />} disabled>
            创建应用
          </Button>
        </Empty>
      </Card>
    </div>
  );
}
