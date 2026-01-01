import { Typography } from "antd";
import { useOutletContext } from "react-router";
import type { QueryTeamListQueryResponseItem } from "../../apiClient/models";

const { Title } = Typography;

interface TeamContext {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: number;
  refreshTeamInfo: () => void;
}

export default function TeamIntegration() {
  const { teamInfo } = useOutletContext<TeamContext>();

  return (
    <div>
      <Title level={4}>系统接入</Title>
      <p>团队 "{teamInfo?.name}" 的系统接入配置</p>
    </div>
  );
}
