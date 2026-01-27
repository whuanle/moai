import { Card, Empty } from "antd";
import { useOutletContext } from "react-router";
import { ToolOutlined } from "@ant-design/icons";
import type { TeamRole } from "../../apiClient/models";

interface TeamContext {
  teamInfo: { id?: number; name?: string } | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

export default function TeamIntegration() {
  const { teamInfo } = useOutletContext<TeamContext>();

  return (
    <Card title={`${teamInfo?.name || ""} - 系统接入`}>
      <Empty
        image={<ToolOutlined style={{ fontSize: 64, color: "#bfbfbf" }} />}
        description={
          <div style={{ fontSize: 16, color: "#666" }}>
            <div>功能开发中...</div>
            <div style={{ fontSize: 14, marginTop: 8, color: "#999" }}>
              外部系统可通过对接操作团队下的资源
            </div>
          </div>
        }
      />
    </Card>
  );
}
