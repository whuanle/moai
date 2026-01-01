import { Typography } from "antd";
import { useOutletContext } from "react-router";
import type { QueryTeamListQueryResponseItem } from "../../apiClient/models";

const { Title } = Typography;

interface TeamContext {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: number;
  refreshTeamInfo: () => void;
}

export default function TeamApps() {
  const { teamInfo } = useOutletContext<TeamContext>();

  return (
    <div>
      <Title level={4}>应用</Title>
      <p>团队 "{teamInfo?.name}" 的应用管理</p>
    </div>
  );
}
