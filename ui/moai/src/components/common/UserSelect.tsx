import { useState, useCallback, useRef } from "react";
import { Input, Avatar, Typography, Spin, Checkbox, Empty } from "antd";
import { UserOutlined, SearchOutlined } from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import type { UserSelectItem } from "../../apiClient/models";
import "./UserSelect.css";

const { Text } = Typography;

interface UserSelectProps {
  value?: string[];
  onChange?: (value: string[]) => void;
  placeholder?: string;
  excludeUserIds?: number[];
}

export default function UserSelect({
  value = [],
  onChange,
  placeholder = "搜索用户...",
  excludeUserIds = [],
}: UserSelectProps) {
  const [userList, setUserList] = useState<UserSelectItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [total, setTotal] = useState(0);
  const [initialized, setInitialized] = useState(false);
  const pageRef = useRef(1);
  const searchRef = useRef("");
  const pageSize = 20;

  const fetchUsers = useCallback(async (pageNum: number, keyword: string, reset: boolean) => {
    if (loading) return;
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.common.userlist.post({
        pageNo: pageNum,
        pageSize,
        search: keyword || undefined,
      });
      
      const items = response?.items || [];
      const totalCount = response?.total || 0;
      
      if (reset) {
        setUserList(items);
      } else {
        setUserList((prev) => [...prev, ...items]);
      }
      
      setTotal(totalCount);
      setHasMore(pageNum * pageSize < totalCount);
      pageRef.current = pageNum;
      searchRef.current = keyword;
    } catch (error) {
      console.error("获取用户列表失败:", error);
    } finally {
      setLoading(false);
    }
  }, [loading]);

  if (!initialized) {
    setInitialized(true);
    fetchUsers(1, "", true);
  }

  const handleSearch = (keyword: string) => {
    setHasMore(true);
    fetchUsers(1, keyword, true);
  };

  const handleScroll = (e: React.UIEvent<HTMLDivElement>) => {
    const { scrollTop, scrollHeight, clientHeight } = e.currentTarget;
    if (scrollHeight - scrollTop - clientHeight < 50 && hasMore && !loading) {
      fetchUsers(pageRef.current + 1, searchRef.current, false);
    }
  };

  const handleToggle = (userName: string) => {
    const newValue = value.includes(userName)
      ? value.filter((v) => v !== userName)
      : [...value, userName];
    onChange?.(newValue);
  };

  const filteredUsers = excludeUserIds.length > 0
    ? userList.filter((u) => !excludeUserIds.includes(u.id ?? 0))
    : userList;

  return (
    <div className="user-select-container">
      <div className="user-select-header">
        <Input
          placeholder={placeholder}
          prefix={<SearchOutlined />}
          allowClear
          onChange={(e) => handleSearch(e.target.value)}
        />
        {value.length > 0 && (
          <div className="user-select-selected-info">
            <span className="user-select-selected-count">已选择 {value.length} 人</span>
          </div>
        )}
      </div>
      
      <div className="user-select-list" onScroll={handleScroll}>
        {loading && userList.length === 0 ? (
          <div className="user-select-loading">
            <Spin />
          </div>
        ) : filteredUsers.length === 0 ? (
          <div className="user-select-empty">
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无用户" />
          </div>
        ) : (
          <>
            {filteredUsers.map((user) => {
              const isSelected = value.includes(user.userName || "");
              return (
                <div
                  key={user.id}
                  className={`user-select-item ${isSelected ? "selected" : ""}`}
                  onClick={() => handleToggle(user.userName || "")}
                >
                  <Checkbox checked={isSelected} />
                  <Avatar
                    src={user.avatarPath}
                    icon={<UserOutlined />}
                    size={40}
                    className="user-select-avatar"
                  />
                  <div className="user-select-info">
                    <div className="user-select-name">{user.nickName || user.userName}</div>
                    <Text type="secondary" className="user-select-username">
                      @{user.userName}
                    </Text>
                  </div>
                </div>
              );
            })}
            {loading && (
              <div className="user-select-loading">
                <Spin size="small" />
              </div>
            )}
          </>
        )}
      </div>
      
      {!loading && userList.length > 0 && (
        <div className="user-select-footer">
          {hasMore ? "下滑加载更多" : `共 ${total} 人`}
        </div>
      )}
    </div>
  );
}
