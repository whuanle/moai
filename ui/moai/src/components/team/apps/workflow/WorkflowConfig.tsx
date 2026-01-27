import { Button, Space, Typography, Empty } from "antd";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { useParams, useNavigate } from "react-router";
import "./WorkflowConfig.css";

const { Title, Paragraph } = Typography;

export default function WorkflowConfig() {
  const { id, appId } = useParams();
  const navigate = useNavigate();
  const teamId = parseInt(id!);

  const handleBack = () => {
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  return (
    <div className="workflow-config-container">
      {/* 头部 */}
      <div className="workflow-config-header">
        <Space>
          <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
          <Title level={4} style={{ margin: 0 }}>
            流程编排配置
          </Title>
        </Space>
      </div>

      {/* 内容区域 */}
      <div className="workflow-config-content">
        <div className="workflow-empty-state">
          <Empty
            description={
              <Space direction="vertical" size="large">
                <Paragraph>流程编排配置功能开发中</Paragraph>
                <Paragraph type="secondary">
                  此功能将支持可视化流程设计、节点配置、条件分支等高级功能
                </Paragraph>
              </Space>
            }
          />
        </div>
      </div>
    </div>
  );
}
