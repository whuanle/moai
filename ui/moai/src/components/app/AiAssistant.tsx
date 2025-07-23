import React, { useState, useRef, useEffect } from "react";
import { Typography, Input } from "antd";
import type { InputRef } from "antd";
import {
  ChatList,
  ChatInputArea,
  ChatSendButton,
  ChatInputActionBar,
  TokenTag,
  ActionsBar,
} from "@lobehub/ui/chat";
import { ActionIcon } from "@lobehub/ui";
import { Eraser, Languages } from "lucide-react";
import { Flexbox } from "react-layout-kit";

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  createAt: number;
  updateAt?: number;
  meta?: any;
}

const DEFAULT_TITLE = "默认话题";

const AiAssistant: React.FC = () => {
  const [title, setTitle] = useState(DEFAULT_TITLE);
  const [editing, setEditing] = useState(false);
  const [editValue, setEditValue] = useState(title);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const inputRef = useRef<InputRef>(null);
  const chatListRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (chatListRef.current) {
      chatListRef.current.scrollTop = chatListRef.current.scrollHeight;
    }
  }, [messages]);

  const handleSend = async () => {
    if (!input.trim()) return;
    const userMsg: ChatMessage = {
      id: Date.now().toString(),
      role: "user",
      content: input,
      createAt: Date.now(),
    };
    setMessages((prev) => [...prev, userMsg]);
    setInput("");

    // 生成AI回复消息占位
    const aiMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [
      ...prev,
      {
        id: aiMsgId,
        role: "assistant",
        content: "",
        createAt: Date.now(),
      },
    ]);

    // 构造请求体
    const payload = {
      modelid: 7,
      title: "测试",
      content: input,
      // 可选：history，可根据实际API需求调整
      // history: [...prev, userMsg].map(m => ({ role: m.role, content: m.content })),
    };

    try {
      const response = await fetch("/api/app/assistant/completions", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "text/event-stream",
        },
        body: JSON.stringify(payload),
      });
      if (!response.body) throw new Error("No response body");
      const reader = response.body.getReader();
      let aiContent = "";
      let done = false;
      let buffer = "";
      while (!done) {
        const { value, done: doneReading } = await reader.read();
        done = doneReading;
        if (value) {
          buffer += new TextDecoder().decode(value, { stream: true });
          // 处理多条 data: ...\n
          const lines = buffer.split("\n");
          buffer = lines.pop() || "";
          for (const line of lines) {
            if (line.startsWith("data:")) {
              const data = line.replace(/^data:/, "").trim();
              if (data === "[DONE]") {
                done = true;
                break;
              }
              try {
                const delta = JSON.parse(data);
                // OpenAI 格式：{ choices: [{ delta: { content: '...' } }] }
                const content = delta.choices?.[0]?.delta?.content;
                if (content) {
                  aiContent += content;
                  setMessages((prevMsgs) =>
                    prevMsgs.map((msg) =>
                      msg.id === aiMsgId ? { ...msg, content: aiContent } : msg
                    )
                  );
                }
              } catch (e) {
                // 忽略解析错误
              }
            }
          }
        }
      }
    } catch (err) {
      setMessages((prevMsgs) =>
        prevMsgs.map((msg) =>
          msg.id === aiMsgId ? { ...msg, content: "AI 回复失败" } : msg
        )
      );
    }
  };

  const handleTitleSave = () => {
    const newTitle = editValue.trim() || DEFAULT_TITLE;
    setTitle(newTitle);
    setEditValue(newTitle);
    setEditing(false);
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        background: "#f7faff",
        display: "flex",
        flexDirection: "row",
      }}
    >
      {/* 左侧窄栏菜单占位 */}
      <div
        style={{
          width: 72,
          background: "#fff",
          borderRight: "1px solid #eee",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          paddingTop: 32,
          boxSizing: "border-box",
        }}
      >
        {/* 菜单占位块 */}
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: 8,
            background: "#f0f0f0",
            marginBottom: 16,
          }}
        />
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: 8,
            background: "#f0f0f0",
            marginBottom: 16,
          }}
        />
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: 8,
            background: "#f0f0f0",
          }}
        />
      </div>
      {/* 右侧主内容区 */}
      <div
        style={{
          flex: 1,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          background: "#f7faff",
          minHeight: "100vh",
        }}
      >
        {/* 顶部大留白+可编辑标题 */}
        <div style={{ height: 64 }} />
        <div
          style={{
            width: "100%",
            maxWidth: 720,
            textAlign: "center",
            marginBottom: 32,
          }}
        >
          {editing ? (
            <Input
              ref={inputRef}
              value={editValue}
              onChange={(e) => setEditValue(e.target.value)}
              onBlur={handleTitleSave}
              onPressEnter={handleTitleSave}
              maxLength={30}
              style={{
                fontWeight: 700,
                fontSize: 28,
                width: 340,
                textAlign: "center",
                margin: "0 auto",
                borderRadius: 12,
              }}
              autoFocus
            />
          ) : (
            <Typography.Title
              level={2}
              style={{
                margin: 0,
                fontWeight: 700,
                fontSize: 28,
                cursor: "pointer",
                color: "#222",
                display: "inline-block",
                borderRadius: 12,
                padding: "4px 24px",
                background: "rgba(255,255,255,0.8)",
                boxShadow: "0 2px 8px rgba(0,0,0,0.03)",
              }}
              onClick={() => setEditing(true)}
              title="点击修改标题"
            >
              {title}
            </Typography.Title>
          )}
        </div>
        {/* 对话历史 */}
        <div
          ref={chatListRef}
          style={{
            width: "100%",
            maxWidth: 720,
            flex: 1,
            minHeight: 320,
            maxHeight: "calc(100vh - 320px)",
            overflowY: "auto",
            background: "#fff",
            borderRadius: 18,
            boxShadow: "0 4px 24px rgba(0,0,0,0.06)",
            padding: "36px 28px",
            margin: "0 auto 0 auto",
            display: "flex",
            flexDirection: "column",
          }}
        >
          <ChatList
            data={messages.map((msg) => ({
              ...msg,
              updateAt: msg.updateAt ?? msg.createAt,
              meta: msg.meta ?? undefined,
            }))}
            renderActions={{ default: ActionsBar }}
            renderMessages={{
              default: ({ id, editableContent }) => (
                <div id={id}>{editableContent}</div>
              ),
            }}
          />
        </div>
        {/* 输入区 */}
        <Flexbox
          style={{
            width: "100%",
            maxWidth: 720,
            background: "#fff",
            borderRadius: 18,
            boxShadow: "0 4px 24px rgba(0,0,0,0.06)",
            padding: "28px 36px",
            margin: "32px auto 32px auto",
            position: "relative",
            zIndex: 2,
          }}
        >
          <div style={{ flex: 1 }} />
          <ChatInputArea
            value={input}
            onInput={setInput}
            onSend={handleSend}
            placeholder="请输入你的问题..."
            disabled={false}
            bottomAddons={<ChatSendButton onSend={handleSend} />}
            topAddons={
              <ChatInputActionBar
                leftAddons={
                  <>
                    <ActionIcon icon={Languages} />
                    <ActionIcon icon={Eraser} />
                    <TokenTag maxValue={5000} value={input.length} />
                  </>
                }
              />
            }
          />
        </Flexbox>
      </div>
    </div>
  );
};

export default AiAssistant;
