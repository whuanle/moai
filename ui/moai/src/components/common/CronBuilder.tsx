import { useState, useEffect } from "react";
import { Form, Radio, InputNumber, Select, Space, Tag, Typography } from "antd";

const { Text } = Typography;

interface CronBuilderProps {
  value?: string;
  onChange?: (value: string) => void;
}

/**
 * Cron 表达式可视化构建器
 * 最小单位：分钟
 * 格式：分 时 日 月 周
 */
export default function CronBuilder({ value, onChange }: CronBuilderProps) {
  const [type, setType] = useState<"minute" | "hour" | "day" | "week" | "month">("day");
  const [minuteInterval, setMinuteInterval] = useState(30);
  const [hourInterval, setHourInterval] = useState(6);
  const [hourMinute, setHourMinute] = useState(0);
  const [dayHour, setDayHour] = useState(0);
  const [dayMinute, setDayMinute] = useState(0);
  const [weekDay, setWeekDay] = useState(1);
  const [weekHour, setWeekHour] = useState(0);
  const [weekMinute, setWeekMinute] = useState(0);
  const [monthDay, setMonthDay] = useState(1);
  const [monthHour, setMonthHour] = useState(0);
  const [monthMinute, setMonthMinute] = useState(0);

  // 解析传入的 cron 表达式
  useEffect(() => {
    if (!value) return;
    
    const parts = value.trim().split(/\s+/);
    if (parts.length !== 5) return;

    const [minute, hour, day, month, week] = parts;

    // 每 N 分钟
    if (minute.startsWith("*/") && hour === "*" && day === "*" && month === "*" && week === "*") {
      setType("minute");
      setMinuteInterval(parseInt(minute.substring(2)) || 30);
    }
    // 每 N 小时
    else if (minute !== "*" && hour.startsWith("*/") && day === "*" && month === "*" && week === "*") {
      setType("hour");
      setHourInterval(parseInt(hour.substring(2)) || 6);
      setHourMinute(parseInt(minute) || 0);
    }
    // 每天
    else if (minute !== "*" && hour !== "*" && day === "*" && month === "*" && week === "*") {
      setType("day");
      setDayHour(parseInt(hour) || 0);
      setDayMinute(parseInt(minute) || 0);
    }
    // 每周
    else if (minute !== "*" && hour !== "*" && day === "*" && month === "*" && week !== "*") {
      setType("week");
      setWeekDay(parseInt(week) || 1);
      setWeekHour(parseInt(hour) || 0);
      setWeekMinute(parseInt(minute) || 0);
    }
    // 每月
    else if (minute !== "*" && hour !== "*" && day !== "*" && month === "*" && week === "*") {
      setType("month");
      setMonthDay(parseInt(day) || 1);
      setMonthHour(parseInt(hour) || 0);
      setMonthMinute(parseInt(minute) || 0);
    }
  }, [value]);

  // 生成 cron 表达式
  const generateCron = () => {
    let cron = "";
    switch (type) {
      case "minute":
        cron = `*/${minuteInterval} * * * *`;
        break;
      case "hour":
        cron = `${hourMinute} */${hourInterval} * * *`;
        break;
      case "day":
        cron = `${dayMinute} ${dayHour} * * *`;
        break;
      case "week":
        cron = `${weekMinute} ${weekHour} * * ${weekDay}`;
        break;
      case "month":
        cron = `${monthMinute} ${monthHour} ${monthDay} * *`;
        break;
    }
    return cron;
  };

  // 当配置改变时，生成新的 cron 表达式
  useEffect(() => {
    const cron = generateCron();
    onChange?.(cron);
  }, [type, minuteInterval, hourInterval, hourMinute, dayHour, dayMinute, weekDay, weekHour, weekMinute, monthDay, monthHour, monthMinute]);

  // 获取可读描述
  const getDescription = () => {
    switch (type) {
      case "minute":
        return `每 ${minuteInterval} 分钟执行一次`;
      case "hour":
        return `每 ${hourInterval} 小时的第 ${hourMinute} 分钟执行`;
      case "day":
        return `每天 ${String(dayHour).padStart(2, "0")}:${String(dayMinute).padStart(2, "0")} 执行`;
      case "week":
        const weekDayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
        return `每${weekDayNames[weekDay]} ${String(weekHour).padStart(2, "0")}:${String(weekMinute).padStart(2, "0")} 执行`;
      case "month":
        return `每月 ${monthDay} 号 ${String(monthHour).padStart(2, "0")}:${String(monthMinute).padStart(2, "0")} 执行`;
      default:
        return "";
    }
  };

  const weekDayOptions = [
    { label: "周日", value: 0 },
    { label: "周一", value: 1 },
    { label: "周二", value: 2 },
    { label: "周三", value: 3 },
    { label: "周四", value: 4 },
    { label: "周五", value: 5 },
    { label: "周六", value: 6 },
  ];

  return (
    <div>
      <Radio.Group value={type} onChange={(e) => setType(e.target.value)} style={{ marginBottom: 16 }}>
        <Radio value="minute">每隔 N 分钟</Radio>
        <Radio value="hour">每隔 N 小时</Radio>
        <Radio value="day">每天</Radio>
        <Radio value="week">每周</Radio>
        <Radio value="month">每月</Radio>
      </Radio.Group>

      <div style={{ marginBottom: 16, padding: "12px", background: "#f5f5f5", borderRadius: "6px" }}>
        {type === "minute" && (
          <Space>
            <Text>每</Text>
            <InputNumber
              min={1}
              max={59}
              value={minuteInterval}
              onChange={(val) => setMinuteInterval(val || 30)}
              style={{ width: 80 }}
            />
            <Text>分钟执行一次</Text>
          </Space>
        )}

        {type === "hour" && (
          <Space>
            <Text>每</Text>
            <InputNumber
              min={1}
              max={23}
              value={hourInterval}
              onChange={(val) => setHourInterval(val || 6)}
              style={{ width: 80 }}
            />
            <Text>小时的第</Text>
            <InputNumber
              min={0}
              max={59}
              value={hourMinute}
              onChange={(val) => setHourMinute(val || 0)}
              style={{ width: 80 }}
            />
            <Text>分钟执行</Text>
          </Space>
        )}

        {type === "day" && (
          <Space>
            <Text>每天</Text>
            <InputNumber
              min={0}
              max={23}
              value={dayHour}
              onChange={(val) => setDayHour(val || 0)}
              style={{ width: 80 }}
            />
            <Text>时</Text>
            <InputNumber
              min={0}
              max={59}
              value={dayMinute}
              onChange={(val) => setDayMinute(val || 0)}
              style={{ width: 80 }}
            />
            <Text>分执行</Text>
          </Space>
        )}

        {type === "week" && (
          <Space>
            <Text>每</Text>
            <Select
              value={weekDay}
              onChange={setWeekDay}
              options={weekDayOptions}
              style={{ width: 100 }}
            />
            <InputNumber
              min={0}
              max={23}
              value={weekHour}
              onChange={(val) => setWeekHour(val || 0)}
              style={{ width: 80 }}
            />
            <Text>时</Text>
            <InputNumber
              min={0}
              max={59}
              value={weekMinute}
              onChange={(val) => setWeekMinute(val || 0)}
              style={{ width: 80 }}
            />
            <Text>分执行</Text>
          </Space>
        )}

        {type === "month" && (
          <Space>
            <Text>每月</Text>
            <InputNumber
              min={1}
              max={31}
              value={monthDay}
              onChange={(val) => setMonthDay(val || 1)}
              style={{ width: 80 }}
            />
            <Text>号</Text>
            <InputNumber
              min={0}
              max={23}
              value={monthHour}
              onChange={(val) => setMonthHour(val || 0)}
              style={{ width: 80 }}
            />
            <Text>时</Text>
            <InputNumber
              min={0}
              max={59}
              value={monthMinute}
              onChange={(val) => setMonthMinute(val || 0)}
              style={{ width: 80 }}
            />
            <Text>分执行</Text>
          </Space>
        )}
      </div>

      <Space direction="vertical" size="small" style={{ width: "100%" }}>
        <div>
          <Text type="secondary">执行规则：</Text>
          <Text strong style={{ marginLeft: 8 }}>{getDescription()}</Text>
        </div>
        <div>
          <Text type="secondary">Cron 表达式：</Text>
          <Tag color="blue" style={{ marginLeft: 8 }}>{generateCron()}</Tag>
        </div>
      </Space>
    </div>
  );
}
