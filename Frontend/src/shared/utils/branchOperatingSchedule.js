function readField(source, keys, fallback = null) {
  for (const key of keys) {
    if (source?.[key] !== undefined && source?.[key] !== null) return source[key];
  }
  return fallback;
}

function parseDate(value) {
  if (!value) return null;
  const parsed = value instanceof Date ? value : new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function parseTimeToMinutes(value) {
  const text = String(value || "").trim();
  const match = text.match(/^(\d{1,2}):(\d{2})/);
  if (!match) return null;
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (!Number.isFinite(hour) || !Number.isFinite(minute)) return null;
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;
  return hour * 60 + minute;
}

export function isBranchClosedBySchedule(operatingHours, branchId, dateValue) {
  const normalizedBranchId = Number(branchId || 0);
  const date = parseDate(dateValue);
  if (!Number.isFinite(normalizedBranchId) || normalizedBranchId <= 0 || !date) return false;

  const row = (Array.isArray(operatingHours) ? operatingHours : []).find((item) => {
    const rowBranchId = Number(readField(item, ["branchId", "BranchId"], 0));
    const rowDayOfWeek = Number(readField(item, ["dayOfWeek", "DayOfWeek"], -1));
    return rowBranchId === normalizedBranchId && rowDayOfWeek === date.getDay();
  });

  if (!row) return false;

  if (readField(row, ["isDayOff", "IsDayOff"], false) === true) return true;

  const openMinutes = parseTimeToMinutes(readField(row, ["openTime", "OpenTime"], ""));
  const closeMinutes = parseTimeToMinutes(readField(row, ["closeTime", "CloseTime"], ""));
  if (openMinutes !== null && closeMinutes !== null && closeMinutes <= openMinutes) return true;

  return false;
}

export function getBranchClosedMessage(dateValue) {
  const targetDate = parseDate(dateValue);
  const now = new Date();
  if (
    targetDate &&
    targetDate.getFullYear() === now.getFullYear() &&
    targetDate.getMonth() === now.getMonth() &&
    targetDate.getDate() === now.getDate()
  ) {
    return "Gara không hoạt động vào hôm nay, vui lòng quay lại vào hôm sau.";
  }
  return "Gara không hoạt động vào ngày này, vui lòng chọn ngày khác.";
}


