import { useParams } from "react-router";
import ApplicationPage from "../../application/ApplicationPage";

/**
 * 团队应用中心页面 - 直接复用 ApplicationPage，通过 URL 参数传递 teamId
 */
export default function TeamApplicationListPage() {
  const { id: teamId } = useParams<{ id: string }>();
  
  // 直接渲染 ApplicationPage，teamId 会从 URL 参数中获取
  return <ApplicationPage teamId={teamId ? parseInt(teamId) : undefined} />;
}
