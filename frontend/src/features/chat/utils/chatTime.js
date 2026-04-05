function toValidDate(value) {
  if (!value) return null;

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date;
}

export function formatChatTimestamp(value) {
  const date = toValidDate(value);
  if (!date) return "";

  const parts = new Intl.DateTimeFormat("vi-VN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).formatToParts(date);

  const read = (type) => parts.find((part) => part.type === type)?.value ?? "";
  const hour = read("hour");
  const minute = read("minute");
  const second = read("second");
  const day = read("day");
  const month = read("month");
  const year = read("year");

  if (!hour || !minute || !second || !day || !month || !year) {
    return "";
  }

  return `${hour}:${minute}:${second} ${day}/${month}/${year}`;
}

export function formatChatDividerTime(value) {
  const date = toValidDate(value);
  if (!date) return "";

  const parts = new Intl.DateTimeFormat("vi-VN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
  }).formatToParts(date);

  const read = (type) => parts.find((part) => part.type === type)?.value ?? "";
  const hour = read("hour");
  const minute = read("minute");
  if (!hour || !minute) return "";

  return `${hour}:${minute}`;
}

export function shouldShowChatTimeDivider(previousValue, currentValue, gapMinutes = 120) {
  const previous = toValidDate(previousValue);
  const current = toValidDate(currentValue);
  if (!previous || !current) {
    return false;
  }

  const gapMs = Math.abs(current.getTime() - previous.getTime());
  const requiredGapMs = gapMinutes * 60 * 1000;
  if (gapMs >= requiredGapMs) {
    return true;
  }

  const previousDay = `${previous.getFullYear()}-${previous.getMonth()}-${previous.getDate()}`;
  const currentDay = `${current.getFullYear()}-${current.getMonth()}-${current.getDate()}`;
  return previousDay !== currentDay;
}
