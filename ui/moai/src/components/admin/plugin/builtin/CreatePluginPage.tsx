/**
 * 创建插件独立页面
 */
import { useState, useCallback } from "react";
import { useNavigate } from "react-router";
import { Card } from "antd";
import { CreatePluginContent } from "./components";
import CodeEditorModal from "../../../common/CodeEditorModal";
import "./NativePluginPage.css";

export default function CreatePluginPage() {
  const navigate = useNavigate();

  // 代码编辑器状态
  const [codeEditorVisible, setCodeEditorVisible] = useState(false);
  const [codeEditorFieldKey, setCodeEditorFieldKey] = useState<string | null>(null);
  const [codeEditorInitialValue, setCodeEditorInitialValue] = useState("");
  const [codeEditorFormInstance, setCodeEditorFormInstance] = useState<any>(null);

  const handleBack = useCallback(() => {
    navigate("/app/plugin/builtin");
  }, [navigate]);

  const handleSuccess = useCallback(() => {
    // 创建成功后不跳转，保持在当前页面
  }, []);

  const handleOpenCodeEditor = useCallback(
    (fieldKey: string, currentValue: string, formInstance: any) => {
      setCodeEditorFieldKey(fieldKey);
      setCodeEditorInitialValue(currentValue || "");
      setCodeEditorFormInstance(formInstance);
      setCodeEditorVisible(true);
    },
    []
  );

  const handleCloseCodeEditor = useCallback(() => {
    setCodeEditorVisible(false);
    setCodeEditorFieldKey(null);
    setCodeEditorInitialValue("");
    setCodeEditorFormInstance(null);
  }, []);

  const handleConfirmCodeEditor = useCallback(
    (value: string) => {
      if (codeEditorFieldKey && codeEditorFormInstance) {
        codeEditorFormInstance.setFieldsValue({ [codeEditorFieldKey]: value });
      }
      handleCloseCodeEditor();
    },
    [codeEditorFieldKey, codeEditorFormInstance, handleCloseCodeEditor]
  );

  return (
    <div className="native-plugin-page">
      <Card>
        <CreatePluginContent
          onClose={handleBack}
          onSuccess={handleSuccess}
          onOpenCodeEditor={handleOpenCodeEditor}
        />
      </Card>

      <CodeEditorModal
        open={codeEditorVisible}
        initialValue={codeEditorInitialValue}
        language="javascript"
        title="代码编辑器"
        onClose={handleCloseCodeEditor}
        onConfirm={handleConfirmCodeEditor}
        width={1200}
        height="70vh"
      />
    </div>
  );
}
