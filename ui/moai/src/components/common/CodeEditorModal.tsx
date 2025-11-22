import { useState, useEffect } from "react";
import { Modal, Button } from "antd";
import Editor from "@monaco-editor/react";
import { CheckOutlined, CloseOutlined } from "@ant-design/icons";

interface CodeEditorModalProps {
  open: boolean;
  initialValue?: string;
  language?: string;
  title?: string;
  onClose: () => void;
  onConfirm: (value: string) => void;
  width?: number | string;
  height?: number | string;
}

export default function CodeEditorModal({
  open,
  initialValue = "",
  language = "javascript",
  title = "代码编辑器",
  onClose,
  onConfirm,
  width = 1200,
  height = "70vh",
}: CodeEditorModalProps) {
  const [editorValue, setEditorValue] = useState<string>(initialValue);

  // 当 initialValue 变化时更新编辑器值
  useEffect(() => {
    if (open) {
      setEditorValue(initialValue || "");
    }
  }, [initialValue, open]);

  const handleChange = (value: string | undefined) => {
    setEditorValue(value || "");
  };

  const handleConfirm = () => {
    onConfirm(editorValue);
    onClose();
  };

  const handleCancel = () => {
    onClose();
  };

  return (
    <Modal
      title={title}
      open={open}
      onCancel={handleCancel}
      width={width}
      footer={[
        <Button key="cancel" icon={<CloseOutlined />} onClick={handleCancel}>
          取消
        </Button>,
        <Button
          key="confirm"
          type="primary"
          icon={<CheckOutlined />}
          onClick={handleConfirm}
        >
          确认
        </Button>,
      ]}
      destroyOnClose
    >
      <div
        style={{
          width: "100%",
          height: height,
          border: "1px solid #d9d9d9",
          borderRadius: "4px",
          overflow: "hidden",
        }}
      >
        <Editor
          height="100%"
          language={language}
          theme="vs-dark"
          value={editorValue}
          onChange={handleChange}
          options={{
            minimap: { enabled: true },
            scrollBeyondLastLine: false,
            fontSize: 14,
            lineNumbers: "on",
            wordWrap: "on",
            formatOnPaste: true,
            formatOnType: true,
            selectOnLineNumbers: true,
            automaticLayout: true,
          }}
        />
      </div>
    </Modal>
  );
}

