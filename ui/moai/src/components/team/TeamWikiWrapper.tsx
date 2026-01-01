import { useOutletContext } from "react-router";
import type { QueryTeamListQueryResponseItem, TeamRole } from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";
import TeamWikiListPage from "../wiki/TeamWikiListPage";

interface TeamContext {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: TeamRole | null;
  refreshTeamInfo: () => void;
}

export default function TeamWikiWrapper() {
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  
  if (!teamInfo?.id) {
    return null;
  }

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  return <TeamWikiListPage teamId={teamInfo.id} canManage={canManage} />;
}
